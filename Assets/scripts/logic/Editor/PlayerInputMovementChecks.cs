#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NarutoVR.Audio;
using NarutoVR.SealCombat.Rpg;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NarutoVR.SealCombat.Editor
{
    /// <summary>在首章副本实际运行玩家输入、移动、脚步和受击，隔离存档与音乐。</summary>
    [InitializeOnLoad]
    public static class PlayerInputMovementChecks
    {
        const string Key = "NarutoVR.PlayerInputMovementChecks";
        const string Report = "Logs/player-input-movement.txt";
        static double deadline;
        static PlayerInputMovementChecks()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (!SessionState.GetBool(Key, false)) return;
                if (state == PlayModeStateChange.EnteredPlayMode)
                { deadline = EditorApplication.timeSinceStartup + 100; EditorApplication.update += Tick; }
                if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(Key + ".done", false))
                { SessionState.SetBool(Key, false); EditorApplication.Exit(SessionState.GetInt(Key + ".code", 1)); }
            };
        }
        public static void RunBatch()
        {
            var temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!Application.isBatchMode || !Path.GetFullPath(Application.dataPath).StartsWith(temp, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("请在临时目录的隔离项目执行。");
            Directory.CreateDirectory("Logs");
            File.WriteAllText(Report, DateTimeOffset.Now.ToString("o") + Environment.NewLine);
            const string chapter = "Assets/Scene/Chapter01_Awakening.unity";
            CheckOldFootsteps(chapter);
            var scene = EditorSceneManager.OpenScene(chapter);
            Check(RpgHandPoseChecks.Run() > 0, "官方手形识别与投掷适配既有回归");
            Check(WristMenuChecks.Run() > 0 && WristTapChecks.Run() > 0, "腕扣、教学卡与输入时间屏障既有回归");
            Check(RpgLocomotionChecks.Run() > 0 && EncounterMovementChecks.Run() > 0, "导航、墙体阻挡、战斗滑退与取消既有回归");
            var player = Object.FindFirstObjectByType<LinearRpgController>();
            Object.DestroyImmediate(player.GetComponentInChildren<P1AudioManager>(true));
            player.autoLoad = false;
            player.saveFileName = "qa-input-movement-" + Guid.NewGuid().ToString("N") + ".json";
            player.skills.voice.autoStartInEditor = false;
            if (!EditorSceneManager.SaveScene(scene, "Assets/Scene/QA_PlayerInputMovement.unity"))
                throw new IOException("无法保存隔离场景。");
            SessionState.SetBool(Key, true); SessionState.SetBool(Key + ".done", false);
            EditorApplication.EnterPlaymode();
        }
        static void CheckOldFootsteps(string chapter)
        {
            const string copy = "Assets/Scene/QA_LegacyPlayerFootsteps.unity";
            var audio = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Resources/Audio/Footsteps/SoftSole_02.wav");
            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(audio, out string guid, out long id)) throw new IOException("缺少旧脚步样本。");
            string source = File.ReadAllText(chapter).Replace("\r\n", "\n");
            string legacy = $"  footstepClips:\n  - {{fileID: {id}, guid: {guid}, type: 3}}\n  footstepVolume: 0.37\n  footstepSpacing: 0.71\n";
            source = new Regex(@"(?m)^  duckRelease: [^\n]*\n").Replace(source, match => match.Value + legacy, 1);
            File.WriteAllText(copy, source);
            AssetDatabase.ImportAsset(copy, ImportAssetOptions.ForceSynchronousImport);
            var scene = EditorSceneManager.OpenScene(copy);
            var player = Object.FindFirstObjectByType<LinearRpgController>();
            var steps = player.locomotion.GetComponent<FootstepPlayer>();
            Object.DestroyImmediate(steps);
            Check(PlayerFootstepMigration.Migrate(player), "旧音乐组件的自定义脚步成功迁入玩家");
            steps = player.locomotion.GetComponent<FootstepPlayer>();
            Check(steps && steps.clip == audio && steps.variations.Length == 0 &&
                  Mathf.Approximately(steps.volume, .37f) && Mathf.Approximately(steps.stepDistance, .71f) &&
                  !PlayerFootstepMigration.Migrate(player), "迁移保留旧 Clip 引用、间距和音量，可安全重复执行");
            EditorSceneManager.SaveScene(scene);
        }
        static void Tick()
        {
            if (!Application.isPlaying) return;
            try
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("等待首章装配超时");
                var player = Object.FindFirstObjectByType<LinearRpgController>();
                if (!player || !player.CanAct) return;
                Verify(player);
                Finish(null);
            }
            catch (Exception error) { Finish(error); }
        }
        static void Verify(LinearRpgController player)
        {
            var entity = player.Entity;
            var move = player.locomotion;
            var input = player.Input;
            var steps = move.GetComponent<FootstepPlayer>();
            Check(!player.GetComponentInChildren<P1AudioManager>(true) && steps && steps.clip && steps.variations.Count(clip => clip) == 2 &&
                Mathf.Approximately(steps.stepDistance,.55f) && Mathf.Approximately(steps.volume,.22f), "无音乐管理器时首章保留三种原有脚步、0.55米步距和0.22音量");
            int emitted = 0, walking = 0, checkpoint = 0;
            entity.FootstepPlayed += _ => emitted++;
            move.MovementCommitted += result =>
            {
                if (result.Kind == PlayerMovementKind.Walk && result.Delta.sqrMagnitude > 0) walking++;
                if (result.Kind == PlayerMovementKind.Checkpoint) checkpoint++;
            };
            int initial = steps.StepCount;
            double now = Time.realtimeSinceStartupAsDouble;
            move.SetEncounterCombat(false);
            void Sample(bool point)
            {
                now += .04;
                var head = move.head.position;
                move.ProcessInput(new NavigationHandInput {leftValid=true,rightValid=true,leftObserved=now,rightObserved=now,
                    leftPointing=point,leftWrist=head+new Vector3(-.45f,-.28f,.2f),leftIndex=head+new Vector3(-.45f,-.22f,.27f),
                    rightWrist=head+new Vector3(.08f,-.32f,.16f)}, false, false, now);
            }
            Sample(false);
            for (int i = 0; i < 20; i++) Sample(true);
            Sample(false);
            Check(walking > 0 && emitted > 0 && emitted == steps.StepCount - initial, "实际步行产生移动结果和一套脚步");
            int beforeWarp = emitted;
            Check(move.TryRestoreCheckpoint(player.checkpointNodes[0]) && checkpoint == 1 && emitted == beforeWarp, "恢复节点报告重定位且不会播放脚步");
            var claimant = ((OpeningTrialChapter)player.chapter).puzzle;
            long beforeClaim = input.Generation;
            int processed = player.skills.VoiceEventCount;
            double priorWord = Time.realtimeSinceStartupAsDouble;
            player.skills.ReceiveVoice("雷光剑", priorWord);
            Check(input.TryAcquireInteraction(claimant, out long claim) && !input.IsCurrent(beforeClaim), "实体物品取得右手，旧输入代次失效");
            player.skills.SendMessage("LateUpdate", SendMessageOptions.RequireReceiver);
            Check(player.skills.VoiceEventCount == processed && player.skills.LastVoiceBlock.Contains("输入占用已变化"), "领取物品之前进入队列的词尾不会透过新占用施法");
            Check(!player.DebugCast("thunder.sword") && !player.skills.effects.HasOccupiedHand, "持物品的同一帧召剑不会生成第二个持有物");
            player.CancelCasting();
            Check(!input.OwnsInteraction(claimant, claim), "取消使旧物品占用凭证失效");
            Check(input.TryAcquireInteraction(claimant, out long next) && next != claim, "新一轮可以重新申请手部");
            input.ReleaseInteraction(claimant, claim);
            Check(input.OwnsInteraction(claimant, next), "较晚抵达的旧释放不会夺走新物品");
            input.ReleaseInteraction(claimant, next);
            player.skills.ReceiveVoice("雷光剑", priorWord);
            Check(player.skills.VoiceEventCount == processed && player.skills.LastVoiceBlock.Contains("早于输入切换屏障"), "解除持有后才收到的旧词尾也无法重新排队");
            var effects = player.skills.effects;
            effects.WristPoseOverride = left => left ? (Pose?)null : new Pose(move.head.position + move.head.forward * .35f, move.head.rotation);
            try
            {
                Check(player.DebugCast("thunder.sword") && !input.TryAcquireInteraction(claimant, out _), "反向顺序下，实际召剑阻止同帧持叶");
                player.CancelCasting();
            }
            finally { effects.WristPoseOverride = null; }
            int damageEvents = 0;
            entity.DamageSettled += result => { if (result.HealthLost > 0) damageEvents++; };
            var vignette = entity.GetComponent<PlayerHitVignette>();
            int prior = vignette.PulseCount;
            Check(player.ReceiveDamage("p2.regression", 3) && !player.ReceiveDamage("p2.regression", 3) &&
                damageEvents == 1 && vignette.PulseCount == prior + 1, "同一受击只结算一次，玩家遮罩只显示一次");
        }
        static void Check(bool ok, string message)
        {
            if (!ok) throw new InvalidOperationException(message);
            File.AppendAllText(Report, "PASS " + message + Environment.NewLine);
        }
        static void Finish(Exception error)
        {
            EditorApplication.update -= Tick;
            File.AppendAllText(Report, (error == null ? "PASS END" : "FAIL " + error) + Environment.NewLine);
            SessionState.SetBool(Key + ".done", true); SessionState.SetInt(Key + ".code", error == null ? 0 : 1);
            EditorApplication.ExitPlaymode();
        }
    }
}
#endif
