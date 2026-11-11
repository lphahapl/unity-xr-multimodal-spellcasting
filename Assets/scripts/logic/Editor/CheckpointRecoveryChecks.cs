using System;
using System.IO;
using System.Linq;
using System.Text;
using NarutoVR.SealCombat.Rpg;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace NarutoVR.SealCombat.Editor
{
    [InitializeOnLoad]
    public static class CheckpointRecoveryChecks
    {
        const string Key = "NarutoVR.CheckpointRecoveryChecks";
        const string Report = "Logs/checkpoint-recovery.txt";
        const string SourceScene = "Assets/Scene/Chapter01_Awakening.unity";
        const string HealthyScene = "Assets/Scene/QA_CheckpointCallbacks.unity";
        const string FutureScene = "Assets/Scene/QA_FutureCheckpoint.unity";
        const string FutureContents = "{\"version\":999,\"profileId\":\"linear-rpg-player\",\"sentinel\":\"preserve-exact-bytes\"}";
        static AsyncOperation changingScene;
        static int failed;
        static double deadline;

        static CheckpointRecoveryChecks()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (!SessionState.GetBool(Key, false)) return;
                if (state == PlayModeStateChange.EnteredPlayMode)
                {
                    deadline = EditorApplication.timeSinceStartup + 90;
                    EditorApplication.update += Tick;
                }
                else if (state == PlayModeStateChange.EnteredEditMode)
                {
                    SessionState.SetBool(Key, false);
                    EditorApplication.Exit(SessionState.GetInt(Key + ".exit", 1));
                }
            };
        }

        public static void RunBatch()
        {
            string project = Directory.GetParent(Application.dataPath).FullName;
            if (!Application.isBatchMode || !File.Exists(Path.Combine(project, ".skill-assembly-test-project")) ||
                !File.ReadAllText("Assets/scripts/logic/Rpg/LinearRpgController.cs").Contains("SkillAssemblyPlayerSave"))
                throw new InvalidOperationException("Requires disposable clone and isolated save directory");
            Directory.CreateDirectory("Logs");
            File.WriteAllText(Report, DateTimeOffset.Now.ToString("o") + Environment.NewLine);
            string tag = Guid.NewGuid().ToString("N");
            string future = "qa-future-" + tag + ".json";
            string saveDirectory = Path.Combine(project, "Temp", "SkillAssemblyPlayerSave");
            Directory.CreateDirectory(saveDirectory);
            File.WriteAllBytes(Path.Combine(saveDirectory, future), Encoding.UTF8.GetBytes(FutureContents));
            Configure(FutureScene, future, true);
            Configure(HealthyScene, "qa-callbacks-" + tag + ".json", false);
            SessionState.SetBool(Key, true);
            SessionState.SetInt(Key + ".exit", 1);
            EditorApplication.EnterPlaymode();
        }

        static void Configure(string scene, string filename, bool autoLoad)
        {
            var copy = EditorSceneManager.OpenScene(SourceScene);
            var owner = Object.FindFirstObjectByType<LinearRpgController>();
            owner.saveFileName = filename;
            owner.autoLoad = autoLoad;
            owner.skills.voice.autoStartInEditor = false;
            if (!EditorSceneManager.SaveScene(copy, scene)) throw new IOException("Cannot save isolated scene " + scene);
        }

        static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Waiting for checkpoint scene");
                if (changingScene != null && !changingScene.isDone) return;
                var owner = Object.FindFirstObjectByType<LinearRpgController>();
                if (!owner || !owner.CanAct) return;
                if (SceneManager.GetActiveScene().path == HealthyScene)
                {
                    CheckReentrantWriter(owner.SavePath + ".coordinator");
                    CheckCallbacks(owner);
                    changingScene = EditorSceneManager.LoadSceneAsyncInPlayMode(FutureScene, new LoadSceneParameters(LoadSceneMode.Single));
                    return;
                }
                if (SceneManager.GetActiveScene().path != FutureScene) return;
                var original = Encoding.UTF8.GetBytes(FutureContents);
                Check(!owner.CheckpointSavePending && owner.StageIndex == 0 && owner.Player.Health == owner.Player.MaxHealth,
                    "future archive starts a temporary healthy player with no pending checkpoint");
                Check(File.ReadAllBytes(owner.SavePath).SequenceEqual(original), "future archive exact bytes present at startup");
                Check(owner.ReceiveDamage("qa-lethal", owner.Player.MaxHealth) && !owner.Player.Alive && !owner.CheckpointSavePending,
                    "temporary player dies before any pending save exists");
                Check(owner.RestoreCheckpoint() && owner.CanAct && owner.Player.Health == owner.Player.MaxHealth && owner.StageIndex == 0,
                    "temporary player restores startup snapshot after death");
                Check(!owner.CheckpointSavePending && File.ReadAllBytes(owner.SavePath).SequenceEqual(original) &&
                    !File.Exists(owner.SavePath + ".bak") && !File.Exists(owner.SavePath + ".tmp"),
                    "death and restore leave future archive byte-identical without backup or temporary writes");
                Finish();
            }
            catch (Exception error) { Check(false, error.ToString()); Finish(); }
        }

        static void CheckCallbacks(LinearRpgController owner)
        {
            int calls = 0;
            int observedMoves = 0;
            bool nestedSave = false, nestedRestore = false;
            bool moveAccepted = false, claimAccepted = false;
            void TryNested()
            {
                if (++calls != 1) return; // Bound the regression on the unfixed implementation.
                nestedSave = owner.SaveCheckpoint();
                nestedRestore = owner.RestoreCheckpoint();
            }
            void OnProgress(RpgActionCue cue) { if (cue == RpgActionCue.Progress) TryNested(); }
            void OnMovement(MovementResult move)
            {
                if (move.Kind != PlayerMovementKind.Checkpoint) return;
                if (calls == 0)
                {
                    moveAccepted = owner.DebugMove(false, Vector2.up);
                    claimAccepted = owner.Input.TryAcquireInteraction(owner, out var claim);
                    if (claimAccepted) owner.Input.ReleaseInteraction(owner, claim);
                }
                TryNested();
            }
            void ThrowOnMove(MovementResult move) { if (move.Kind == PlayerMovementKind.Checkpoint) throw new InvalidOperationException("QA deliberate movement listener failure"); }
            void ObserveMove(MovementResult move) { if (move.Kind == PlayerMovementKind.Checkpoint) observedMoves++; }

            owner.Feedback += OnProgress;
            try { Check(owner.SaveCheckpoint(), "outer save commits its first file"); }
            finally { owner.Feedback -= OnProgress; }
            Check(calls == 1 && !nestedSave && !nestedRestore, "Progress callback runs once and refuses nested save and restore");
            Check(File.Exists(owner.SavePath) && !File.Exists(owner.SavePath + ".bak"), "outer save performs exactly one file creation");

            void CheckRestore(string source, Action subscribe, Action unsubscribe)
            {
                calls = 0; nestedSave = nestedRestore = false;
                // A second write rotates this unique save into a backup, even if its JSON is unchanged.
                if (File.Exists(owner.SavePath + ".bak")) File.Delete(owner.SavePath + ".bak");
                byte[] original = File.ReadAllBytes(owner.SavePath);
                subscribe();
                try { Check(owner.RestoreCheckpoint(), source + " outer restore succeeds"); }
                catch (Exception error) { Check(false, source + " outer restore threw " + error.GetType().Name); }
                finally { unsubscribe(); }
                Check(calls == 1 && !nestedSave && !nestedRestore, source + " callback runs once and refuses nested save and restore");
                Check(File.ReadAllBytes(owner.SavePath).SequenceEqual(original) && !File.Exists(owner.SavePath + ".bak"),
                    source + " restore writes neither primary nor backup");
            }
            CheckRestore("CheckpointRestored", () => owner.CheckpointRestored += TryNested, () => owner.CheckpointRestored -= TryNested);
            CheckRestore("MovementCommitted", () => {
                owner.locomotion.MovementCommitted += OnMovement;
                owner.locomotion.MovementCommitted += ThrowOnMove;
                owner.locomotion.MovementCommitted += ObserveMove;
            }, () => {
                owner.locomotion.MovementCommitted -= OnMovement;
                owner.locomotion.MovementCommitted -= ThrowOnMove;
                owner.locomotion.MovementCommitted -= ObserveMove;
            });
            Check(observedMoves == 1, "later movement observer receives checkpoint after earlier observer throws");
            Check(!moveAccepted && !claimAccepted, "checkpoint movement callback cannot move or take hand ownership during restore");
            bool canClaim = owner.Input.TryAcquireInteraction(owner, out long after);
            Check(owner.Input.CanMove(false, out _) && canClaim, "movement and hand claim are available again after restore finishes");
            if (canClaim) owner.Input.ReleaseInteraction(owner, after);
        }

        sealed class ReentrantJson : IRpgSaveJson
        {
            internal Action OnEnvelope;
            public string Serialize<T>(T value)
            {
                if (value is SaveEnvelope && OnEnvelope != null)
                {
                    var action = OnEnvelope;
                    OnEnvelope = null;
                    action();
                }
                return UnityRpgSaveJson.Instance.Serialize(value);
            }
            public T Deserialize<T>(string data) => UnityRpgSaveJson.Instance.Deserialize<T>(data);
        }

        static void CheckReentrantWriter(string path)
        {
            var json = new ReentrantJson();
            var store = new RpgSaveStore(json, path, "qa-checkpoint", Array.Empty<string>());
            var checkpoints = new CheckpointSaveCoordinator(store, json);
            checkpoints.Queue(new RpgProgressData { currency = 11 }, 0);
            bool attempted = false, wroteNested = false;
            json.OnEnvelope = () => {
                attempted = true;
                checkpoints.Queue(new RpgProgressData { currency = 22 }, 1);
                wroteNested = checkpoints.TryWrite(1, out _);
            };
            Check(checkpoints.TryWrite(0, out _) && attempted && !wroteNested, "encoder callback queues a newer checkpoint and cannot nest disk write");
            Check(store.TryLoad(out var first, out _) && first.currency == 11 && !File.Exists(path + ".bak"),
                "first write contains only the frozen candidate");
            Check(checkpoints.Pending && checkpoints.CaptureLatest().currency == 22, "newer checkpoint survives the first write");
            Check(checkpoints.TryWrite(4, out _) && !checkpoints.Pending && store.TryLoad(out var second, out _) && second.currency == 22,
                "retry persists the newer queued checkpoint");
        }

        static void Check(bool passed, string message)
        {
            if (!passed) failed++;
            File.AppendAllText(Report, (passed ? "PASS " : "FAIL ") + message + Environment.NewLine);
        }

        static void Finish()
        {
            EditorApplication.update -= Tick;
            File.AppendAllText(Report, failed == 0 ? "PASS END" : "FAIL END count=" + failed);
            SessionState.SetInt(Key + ".exit", failed == 0 ? 0 : 1);
            EditorApplication.ExitPlaymode();
        }
    }
}
