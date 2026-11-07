#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NarutoVR.SealCombat.Rpg;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Management;

namespace NarutoVR.SealCombat.Editor
{
    [InitializeOnLoad]
    public static class P1RoomPlayChecks
    {
        const string Key = "NarutoVR.Room.PlayChecks", StartKey = Key + ".PreviousStart";
        static double runAt;
        static P1RoomPlayChecks()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (!SessionState.GetBool(Key, false)) return;
                if (state == PlayModeStateChange.EnteredPlayMode) runAt = EditorApplication.timeSinceStartup + 2;
                if (state == PlayModeStateChange.EnteredEditMode)
                {
                    SessionState.EraseBool(Key); LinearRpgController.EditorTestSaveFile = null;
                    EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString(StartKey, ""));
                    SessionState.EraseString(StartKey);
                }
            };
            EditorApplication.update += Poll;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void IsolateSave()
        {
            if (SessionState.GetBool(Key, false)) LinearRpgController.EditorTestSaveFile = "room-check-" + Guid.NewGuid().ToString("N") + ".json";
        }
        public static void Begin()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().isDirty || SceneManager.GetActiveScene().path != LinearRpgValidation.P1ScenePath)
                throw new InvalidOperationException("需要已保存的 P1 场景");
            SessionState.SetString(StartKey, AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(LinearRpgValidation.P1ScenePath);
            SessionState.SetBool(Key, true); EditorApplication.isPlaying = true;
        }
        static async void Poll()
        {
            if (!SessionState.GetBool(Key, false) || !Application.isPlaying || runAt <= 0 || EditorApplication.timeSinceStartup < runAt) return;
            runAt = 0;
            var manager = XRGeneralSettings.Instance ? XRGeneralSettings.Instance.Manager : null;
            bool restoreXr = manager && manager.isInitializationComplete;
            bool background = Application.runInBackground;
            try
            {
                Application.runInBackground = true; // Only this desktop fixture needs frames while tools hold focus.
                if (restoreXr) manager.StopSubsystems();
                string actual = await RunActualScene(); string isolated = P1RoomEncounterChecks.Run();
                string pooling = UnityEngine.Object.FindFirstObjectByType<CombatRoomEncounter>().UsesEnemyManager ? await EnemyPoolingChecks.Run() : "旧版预置敌人，无池化测试";
                Directory.CreateDirectory(EnemyPoolingSetup.Log);
                File.WriteAllText(EnemyPoolingSetup.Log + "/play-check.txt", DateTimeOffset.Now.ToString("O") + "\nPASSED\n" + actual + "\n" + isolated + "\n" + pooling);
                File.WriteAllText(P1RoomLayoutSetup.LogFolder + "/play-check.txt", DateTimeOffset.Now.ToString("O") + "\nPASSED\n" + actual + "\n" + isolated);
            }
            catch (Exception e)
            {
                Directory.CreateDirectory(EnemyPoolingSetup.Log);
                File.WriteAllText(EnemyPoolingSetup.Log + "/play-check.txt", DateTimeOffset.Now.ToString("O") + "\nFAILED\n" + e);
                File.WriteAllText(P1RoomLayoutSetup.LogFolder + "/play-check.txt", DateTimeOffset.Now.ToString("O") + "\nFAILED\n" + e);
                Debug.LogException(e);
            }
            finally { Application.runInBackground = background; if (restoreXr) manager.StartSubsystems(); EditorApplication.isPlaying = false; }
        }
        static async Task<string> RunActualScene()
        {
            var room = UnityEngine.Object.FindFirstObjectByType<CombatRoomEncounter>(); var rpg = room.rpg; var move = rpg.locomotion;
            var game = UnityEngine.Object.FindFirstObjectByType<GameManager>();
            if (room.UsesEnemyManager && game) await EnemyPoolingChecks.WithTimeout(game.Ready);
            var enemy = room.UsesEnemyManager ? null : room.enemies.Single();
            bool NoActiveEnemy() => room.UsesEnemyManager ? room.enemyManager.ActiveCount == 0 : !enemy.gameObject.activeSelf;
            // Desktop-only synthetic input: an unavailable/unworn XR runtime pauses the app.
            // Exercise its normal resume callback instead of weakening the production pause gate.
            typeof(LinearRpgController).GetMethod("OnApplicationPause", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(rpg, new object[] { false });
            int checks = 0;
            void Check(bool ok, string why) { if (!ok) throw new Exception("实际场景：" + why); checks++; }
            void Position(float z) { move.rigRoot.position += new Vector3(0, -.03f, z) - move.PlayerFloorPosition; Physics.SyncTransforms(); room.TickEncounter(); }
            Position(0);
            Check(rpg.CanAct && !move.EncounterCombat && NoActiveEnemy(), $"靶场开局应为探索且敌人未激活；rpg={rpg.Status},room={room.Status}");
            rpg.DebugDefeatTargets(); typeof(LinearRpgController).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(rpg, null); room.TickEncounter();
            Check(!move.EncounterCombat && NoActiveEnemy() && !room.HasStarted, "击破练习靶不触发战斗");
            Position(17); Check(!room.HasStarted, "通廊不唤醒敌人");
            Position(20); await EnemyPoolingChecks.WithTimeout(room.SpawnTask); enemy = room.enemies.Single();
            Check(room.HasStarted && move.EncounterCombat && enemy.isActiveAndEnabled && enemy.Agent.isOnNavMesh, "进入房间激活敌人与真实导航面");
            enemy.Tick(.1f); Check(enemy.HasEverDetectedPlayer, "房间内敌人视野能够发现玩家");
            Check(enemy.Entity.ReceiveHit("room-test-hit", 10, 0, null, move.head), "室内命中可扣血");
            float hp = enemy.Entity.Combat.Health;
            rpg.targets[0].ResetTarget(); rpg.DebugDefeatTargets();
            typeof(LinearRpgController).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(rpg, null);
            Check(move.EncounterCombat, "室内击破练习靶不得覆盖房间战斗状态");
            Position(17); Check(!move.EncounterCombat && !enemy.CanReceiveHit, "退出暂停战斗与受击");
            Check(!enemy.Entity.ReceiveHit("outside-room-shot", 10, 0, null, move.head) && enemy.Entity.Combat.Health == hp, "室外射击不能挑衅暂停敌人");
            Position(20); Check(move.EncounterCombat && enemy.CanReceiveHit && enemy.Entity.Combat.Health == hp, "重新进入保留敌人血量");
            Check(rpg.RestoreCheckpoint(), "恢复检查点成功"); room.TickEncounter();
            Check(!room.HasStarted && !move.EncounterCombat && !enemy.gameObject.activeSelf && move.PlayerFloorPosition.z < 10, "恢复后回练习区且房间重置");
            Position(20); await EnemyPoolingChecks.WithTimeout(room.SpawnTask); enemy = room.enemies.Single();
            enemy.Entity.ReceiveHit("room-test-lethal", 10000, 0, null, move.head); room.TickEncounter();
            Check(room.IsComplete && !move.EncounterCombat, "全灭完成恢复探索");
            Position(17); Position(20); Check(room.IsComplete && enemy.Entity.Combat.Dead, "完成后重进不刷怪");
            return $"实际 P1 场景 {checks} 项通过：靶场/通廊隔离、入房、真实导航与发现、扣血、靶击破不覆盖战斗、退出暂停/免疫、保血重入、检查点重置、全灭。";
        }
    }
}
#endif
