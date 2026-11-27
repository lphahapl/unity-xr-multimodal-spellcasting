using System;
using System.Collections;
using System.IO;
using System.Linq;
using NarutoVR.SealCombat.Items;
using NarutoVR.SealCombat.Rpg;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NarutoVR.SealCombat.Editor
{
    [InitializeOnLoad]
    public static class Chapter02BridgeEquipmentPlayChecks
    {
        const string Key = "NarutoVR.Chapter02BridgeEquipmentQACase";
        const string Scene = "Assets/Scene/Chapter02_SuspendedPath.unity";
        const string Report = "Logs/Chapter02BridgeEquipmentPlay.txt";
        static IEnumerator probe;
        static double deadline;
        static int frame = -1;

        static Chapter02BridgeEquipmentPlayChecks() => EditorApplication.playModeStateChanged += OnPlayMode;

        public static void Run() => Enter("fresh");
        public static void RunColdDrawing() => Enter("drawing");
        public static void RunColdLegacy() => Enter("legacy");

        static void Enter(string testCase)
        {
            if (!Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Run in an isolated editor with -batchmode and without -quit.");

            EditorSceneManager.OpenScene(Scene);
            var lesson = Object.FindFirstObjectByType<Chapter02BridgeEquipment>();
            if (!lesson) throw new InvalidOperationException("Install the bridge lesson in the isolated scene first.");
            if (testCase == "fresh") GlyphNibDragChecks.Run();
            lesson.player.autoLoad = true;
            lesson.player.saveFileName = FileName(testCase);
            Directory.CreateDirectory("Logs");
            File.AppendAllText(Report, $"\n[{testCase}] Unity {Application.unityVersion}\n");
            if (testCase == "fresh" && File.Exists(Path.Combine(Application.persistentDataPath, FileName(testCase))))
                throw new InvalidOperationException("Use a fresh CH02_BRIDGE_QA_TOKEN.");
            SessionState.SetString(Key, testCase);
            SessionState.SetInt(Key + ".code", 1);
            EditorApplication.EnterPlaymode();
        }

        static string FileName(string testCase)
        {
            var token = Environment.GetEnvironmentVariable("CH02_BRIDGE_QA_TOKEN");
            if (string.IsNullOrEmpty(token) || !token.All(c => "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ".Contains(c)))
                throw new InvalidOperationException("CH02_BRIDGE_QA_TOKEN must be unique and alphanumeric.");
            return "qa-ch02-bridge-" + token + "-" + testCase + ".json";
        }

        static void OnPlayMode(PlayModeStateChange state)
        {
            var testCase = SessionState.GetString(Key, "");
            if (testCase.Length == 0) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                probe = Test(testCase);
                deadline = EditorApplication.timeSinceStartup + 65;
                EditorApplication.update += Tick;
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.EraseString(Key);
                EditorApplication.Exit(SessionState.GetInt(Key + ".code", 1));
            }
        }

        static void Tick()
        {
            if (frame == Time.frameCount) return;
            frame = Time.frameCount;
            try
            {
                if (EditorApplication.timeSinceStartup >= deadline)
                    throw new TimeoutException("Bridge lesson Play timeout");
                if (probe.MoveNext()) return;
                File.AppendAllText(Report, "PASS END\n");
                SessionState.SetInt(Key + ".code", 0);
            }
            catch (Exception e)
            {
                File.AppendAllText(Report, "FAIL " + e + "\n");
                Debug.LogException(e);
            }
            EditorApplication.update -= Tick;
            EditorApplication.ExitPlaymode();
        }

        static void Check(bool passed, string message)
        {
            if (!passed) throw new InvalidOperationException(message);
            File.AppendAllText(Report, "PASS " + message + "\n");
        }

        static IEnumerator Test(string testCase)
        {
            var lesson = Object.FindFirstObjectByType<Chapter02BridgeEquipment>();
            var player = lesson.player;
            var item = lesson.equipment.pen;
            var belt = lesson.equipment.interaction;
            var glyph = lesson.glyph;
            var home = (ToolBeltSlot)item.home;
            var wood = (ToolBeltSlot)belt.slots[3];
            var panel = lesson.panel;
            var board = panel.board;
            var head = player.locomotion.head;
            while (!player.IsReady || !player.CanAct)
            {
                if (!player.enabled || player.CanRetryInitialCheckpoint)
                    throw new InvalidOperationException(player.Status);
                yield return null;
            }
            yield return null;
            yield return null;
            Check(Path.GetFileName(player.SavePath) == FileName(testCase), "private QA save path");
            Check(player.stages.Select(s => s.stageId).SequenceEqual(new[] {
                "CH02_BRIDGE_APPROACH", "CH02_BRIDGE_DRAW", "CH02_REACH_HUB", "CH02_WORKSHOP_TENON", "CH02_B_DESCENT"
            }), "two lessons precede the original three route stages");
            Check(belt.slots.Length == 5 && item.Kind == GameItemKind.GlyphPen && item.insertionTip == glyph.nib &&
                glyph.penRoot == item.transform && UsesApprovedNib(item), "five sockets and one approved physical pen with the authored Nib");

            if (testCase == "drawing")
            {
                Check(player.StageIndex == 1 && Near(player.locomotion.PlayerFloorPosition, player.CheckpointAt(1).floorPosition) &&
                    lesson.brokenBarrier.activeInHierarchy && !HitRepairedDeck(lesson) && !panel.IsWaiting,
                    "cold drawing checkpoint restores at the stone with the bridge closed and panel folded");
                Check(item.Dock == home && glyph.isActiveAndEnabled && player.SaveCheckpoint(),
                    "cold drawing restores the approved pen to its sleeve and can still save");
                yield break;
            }
            if (testCase == "legacy")
            {
                Check(player.StageIndex == 3 && player.StageAt(3).stageId == "CH02_WORKSHOP_TENON" &&
                    Near(player.locomotion.PlayerFloorPosition, player.CheckpointAt(3).floorPosition) &&
                    player.Model.Chapter.CompletedStages.SequenceEqual(new[] { "CH02_BRIDGE_APPROACH", "CH02_BRIDGE_DRAW", "CH02_REACH_HUB" }),
                    "legacy hub save opens its original workshop stage and preserves the hub landing");
                Check(item.Dock == home && HitRepairedDeck(lesson) && !lesson.brokenBarrier.activeInHierarchy &&
                    player.SaveCheckpoint() && player.RestoreCheckpoint(), "legacy route migration preserves writing and repair across another save and restore");
                Check(player.CaptureProgressSnapshot().currency == 0, "legacy migration does not credit teaching rewards");
                yield break;
            }

            Check(player.StageIndex == 0 && Near(player.locomotion.PlayerFloorPosition, player.CheckpointAt(0).floorPosition),
                "new Play starts at the formal entrance");
            lesson.OpenPanel();
            Check(!glyph.isActiveAndEnabled && !item.isActiveAndEnabled && belt.isActiveAndEnabled &&
                !item.Take() && !panel.IsWaiting && !lesson.button.gameObject.activeInHierarchy,
                "the belt is already worn at the entrance, while the pen and board are locked");
            Check(lesson.brokenBarrier.GetComponent<Collider>().enabled && lesson.brokenBarrier.activeInHierarchy && !HitRepairedDeck(lesson),
                "the original blocker is active and the bridge deck has no collision before solving");
            Check(player.locomotion.TryRestoreCheckpoint(player.CheckpointAt(1)), "actual capsule can land at the safe stone approach: " + player.locomotion.Status);
            while (player.StageIndex != 1 || !lesson.button.gameObject.activeInHierarchy || item.Dock != home || home.IsSnapping)
                yield return null;
            Check(player.Model.Chapter.IsComplete(lesson.approachStage.stageId) && glyph.isActiveAndEnabled &&
                item.Dock == home && Mathf.Abs(home.opening.InverseTransformPoint(item.insertionTip.position).y + .052f) < .0005f,
                "the existing teaching approach enables the one real pen at its calibrated depth");
            Check(player.SaveCheckpoint(), "drawing permission autosaves and remains explicitly saveable");
            File.Copy(player.SavePath, Path.Combine(Application.persistentDataPath, FileName("drawing")));

            double clock = Time.realtimeSinceStartupAsDouble;
            void Hand(Vector3 position, Quaternion orientation, bool pinch, double advance = .02)
            {
                clock = Math.Max(clock + advance, Time.realtimeSinceStartupAsDouble);
                var observation = new PlayerHandObservation
                {
                    rightValid = true, rightObserved = clock, gripPoseValid = true,
                    pinchValid = true, systemPinch = true, systemPressed = pinch,
                    headPosition = head.position, headForward = head.forward,
                    rightTip = position, rightWrist = position, gripPosition = position, gripRotation = orientation
                };
                glyph.Observe(observation, clock);
                glyph.Advance(clock);
            }
            void Take()
            {
                var grip = item.GripPosition;
                Hand(grip, item.transform.rotation, false);
                Hand(grip, item.transform.rotation, true);
            }
            Quaternion Seat(ToolBeltSlot slot) => slot.opening.rotation * Quaternion.Euler(item.stowEuler);
            void Press()
            {
                var origin = lesson.button.RestSurface;
                var axis = lesson.button.cap.forward;
                lesson.button.Sample(origin - axis * .012f);
                lesson.button.Sample(origin + axis * .003f);
                lesson.button.Sample(origin - axis * .012f);
            }

            Take();
            Check(belt.Held == item && glyph.IsHeld && player.Input.InteractionOwnsHand && !item.Dock,
                "normal pinch owns the actual approved pen and its writing session");
            Press();
            Check(panel.IsWaiting && board.IsOpen, "physical copper button opens the existing SquareRule story board");
            var obstruction = new GameObject("QA checkpoint obstruction");
            obstruction.transform.position = player.CheckpointAt(1).floorPosition + Vector3.up * .72f;
            obstruction.AddComponent<BoxCollider>().size = Vector3.one * .4f;
            Check(!player.RestoreCheckpoint() && glyph.IsHeld && panel.IsWaiting && player.StageIndex == 1,
                "a separate world collider still rejects the checkpoint without discarding the held pen or board");
            obstruction.SetActive(false);
            Object.Destroy(obstruction);
            Check(player.RestoreCheckpoint(), "explicit restore loads the saved drawing checkpoint: " +
                player.Status + " / " + player.locomotion.Status);
            yield return null;
            while (item.Dock == home && home.IsSnapping) yield return null;
            Check(item.Dock == home && !belt.Held && !glyph.IsHeld && !panel.IsWaiting && !HitRepairedDeck(lesson),
                "reload cancels held pen and open board, returns the same pen, and preserves the unsolved bridge");
            Press();
            Take();
            int completed = 0;
            panel.onCompleted.AddListener(() => completed++);
            void Draw(Vector3 point, double delay = .02)
            {
                var orientation = board.transform.rotation;
                Hand(board.transform.TransformPoint(point) - orientation * Vector3.forward * .108f,
                    orientation, true, delay);
            }
            GlyphNibDragChecks.CheckHeldStroke(glyph, board, Draw);
            Check(board.PointCount == 0 && glyph.IsHeld && panel.IsWaiting,
                "production B brushes both real LODs, keeps Nib on the ink, and returns straight on lift");
            foreach (var stroke in panel.template.Preview())
            {
                Vector3 first = stroke[0] * .115f;
                Draw(first + Vector3.back * .055f);
                Draw(first + Vector3.forward * .001f);
                for (int i = 1; i < stroke.Length; i++)
                    for (int t = 1; t <= 12; t++)
                        Draw((Vector3)Vector2.Lerp(stroke[i - 1], stroke[i], t / 12f) * .115f + Vector3.forward * .001f);
                Draw(new Vector3(0, 0, -.055f));
            }
            Draw(new Vector3(0, 0, -.055f), board.idleSeconds + .1);
            Check(panel.IsResolving && !HitRepairedDeck(lesson) && belt.Held == item,
                "the held real Nib traces the approved symbol and only resolves after the validation period");
            glyph.Advance(clock + board.successDuration + .15);
            Check(completed == 1 && !lesson.brokenBarrier.activeInHierarchy && HitRepairedDeck(lesson),
                "matching repairs the existing collider first and removes the original blocker once");
            while (player.StageIndex != 2) yield return null;
            Check(player.StageAt(2).stageId == "CH02_REACH_HUB" && !player.TryAdvanceStage() && !lesson.button.gameObject.activeInHierarchy,
                "repair advances only into the original hub objective; it cannot skip that route");
            Check(belt.Held == item && item.IsHeld && player.Input.InteractionOwnsHand && item.PickupSource == home,
                "repair advances the chapter without dropping the player's physical pen or forgetting its sleeve");

            var nearHome = home.opening.position + head.forward * .11f;
            // While real frames advanced the scene, the desktop replay supplied no tracked hand.
            // Re-establish its held pinch before testing a normal release.
            Hand(nearHome, Seat(home), true);
            Hand(nearHome, Seat(home), false);
            while (item.Dock == home && home.IsSnapping) yield return null;
            Check(item.Dock == home && !glyph.IsHeld, "normal near release returns the real pen to its sleeve");
            Take();
            var woodRotation = Seat(wood);
            Hand(wood.opening.position - woodRotation * Vector3.forward * .108f, woodRotation, false);
            while (item.Dock == wood && wood.IsSnapping) yield return null;
            Check(item.Dock == wood && !home.SnappedObject, "same pen can be rotated into the approved universal wood case");
            Take();
            Check(item.PickupSource == wood, "new socket is remembered for ordinary returns");
            Vector3 levelForward=Vector3.ProjectOnPlane(head.forward,Vector3.up).normalized;
            if(levelForward.sqrMagnitude<.5f) levelForward=Vector3.forward;
            Hand(head.position + levelForward * (belt.discardReach + .05f), head.rotation, false);
            Check(!item.Dock && !belt.Held && item.GetComponent<Rigidbody>().useGravity,
                "release beyond the player's horizontal discard radius drops the same physical pen and releases the hand");
            Check(player.RestoreCheckpoint(), "restore the post-repair checkpoint");
            yield return null;
            while (item.Dock == home && home.IsSnapping) yield return null;
            Check(item.Dock == home && HitRepairedDeck(lesson) && !lesson.brokenBarrier.activeInHierarchy,
                "post-repair restore recovers both the original pen instance and repaired collision");

            var hub = player.CheckpointAt(3);
            Check(player.locomotion.TryRestoreCheckpoint(hub), "original hub checkpoint still passes physics");
            while (player.StageIndex != 3) yield return null;
            Check(player.StageAt(3).stageId == "CH02_WORKSHOP_TENON" && player.SaveCheckpoint(),
                "original real hub target enters the workshop and saves");
            var legacy = JsonUtility.FromJson<SaveEnvelope>(File.ReadAllText(player.SavePath));
            legacy.progress.completedStageIds.RemoveRange(0, 2);
            legacy.progress.claimedRewardIds.RemoveAll(id => id == "reward.CH02_BRIDGE_APPROACH" || id == "reward.CH02_BRIDGE_DRAW");
            File.WriteAllText(Path.Combine(Application.persistentDataPath, FileName("legacy")), JsonUtility.ToJson(legacy));
        }

        static bool Near(Vector3 position, Vector3 target) => Vector3.Distance(position, target) < .08f;

        static bool HitRepairedDeck(Chapter02BridgeEquipment lesson)
        {
            Physics.SyncTransforms();
            return Physics.Raycast(new Vector3(-9.121364f, 80.6f, 55f), Vector3.down, out var hit, 1f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) &&
                hit.collider.transform.IsChildOf(lesson.repairedDeck.transform);
        }

        static bool UsesApprovedNib(GameItemBase item) =>
            item.insertionTip.name == "Nib" && item.GetComponentsInChildren<MeshFilter>(true).Any(mesh =>
                AssetDatabase.GetAssetPath(mesh.sharedMesh).StartsWith("Assets/Art/Props/MoGuiPen/", StringComparison.Ordinal));
    }
}
