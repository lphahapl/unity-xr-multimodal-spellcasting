#if UNITY_EDITOR
using System;
using System.Reflection;
using NarutoVR.SealCombat.Enemies;
using NarutoVR.SealCombat.Rpg;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace NarutoVR.SealCombat.Editor
{
    public static class P1RoomEncounterChecks
    {
        public static string Run()
        {
            if (!Application.isPlaying || Time.timeScale <= 0) throw new Exception("房间检查须在未暂停的隔离 Play 场景运行");
            int count = 0;
            void Check(bool ok, string reason) { if (!ok) throw new Exception("房间检查：" + reason); count++; }
            using var f = new Fixture();
            var room = f.Room; var enemy = f.Enemy; var rpg = f.Rpg;
            void Move(float z) { f.Rig.transform.position = f.Origin + Vector3.forward * z; Physics.SyncTransforms(); room.TickEncounter(); }
            room.TickEncounter();
            Check(!room.HasStarted && !enemy.gameObject.activeSelf && !rpg.locomotion.EncounterCombat, "开局练习区敌人关闭且探索");
            Check(rpg.GetComponent<TrainingTargetGroup>().targets.Length == 1 && f.Target.resetAfterSeconds == 3 && !rpg.HazardsEnabled, "单练习靶三秒恢复且预警关闭");
            f.Target.ApplyHit(10000); rpg.ToggleHazards();
            Check(!rpg.TryAdvanceStage() && !rpg.HazardsEnabled, "手动推进和训练预警也不能绕过房间模式");
            Move(10);
            Check(room.HasStarted && room.PlayerInside && enemy.gameObject.activeSelf && rpg.locomotion.EncounterCombat,
                "玩家地面位置进入房间启动敌人与战斗模式");
            Invoke(rpg, "LateUpdate");
            Check(rpg.locomotion.EncounterCombat && rpg.StageIndex == 0, "击破练习靶不会覆盖区域战斗或推进阶段");
            var state = enemy.Entity.Combat;
            Check(enemy.Entity.ReceiveHit("room-injury", 10, 1), "房间战斗可受伤");
            float hp = state.Health;
            Move(4.85f);
            Check(room.PlayerInside && rpg.locomotion.EncounterCombat, "退出边界余量防止门口轻微抖动切换");
            Move(4.5f);
            Check(!room.PlayerInside && !enemy.CanReceiveHit && !rpg.locomotion.EncounterCombat, "离开后暂停敌人并进入探索");
            Check(!enemy.Entity.ReceiveHit("outside-shot", 100, 100) && state.Health == hp, "练习区远程发射不能激活或伤害暂停敌人");
            Move(10);
            Check(enemy.CanReceiveHit && ReferenceEquals(enemy.Entity.Combat, state) && state.Health == hp,
                "重入恢复原生命状态而非满血重生");
            room.enabled = false;
            Check(!enemy.CanReceiveHit && !rpg.locomotion.EncounterCombat, "关闭区域管理器会暂停敌人");
            room.enabled = true; room.TickEncounter();
            Check(enemy.CanReceiveHit && ReferenceEquals(enemy.Entity.Combat, state), "重启区域组件不重置敌人");

            int restored = 0; Action restoredListener = () => restored++;
            rpg.CheckpointRestored += restoredListener;
            try
            {
                Check(rpg.ReceiveDamage("room-player-death", 10000) && !rpg.Player.Alive, "真实玩家死亡");
                room.TickEncounter();
                Check(!enemy.CanReceiveHit && !rpg.locomotion.EncounterCombat, "死亡暂停房间敌人");
                Check(rpg.RestoreCheckpoint() && restored == 1, "成功检查点恢复发布一次通知");
                Check(!room.HasStarted && !room.IsComplete && !enemy.gameObject.activeSelf &&
                    Vector3.Distance(enemy.transform.position, f.SpawnPosition) < .001f, "恢复检查点重置位置并关闭敌人等待进入");
                Move(10);
                Check(enemy.Entity.Combat.Health == enemy.Entity.Combat.MaxHealth && !ReferenceEquals(enemy.Entity.Combat, state),
                    "检查点后再次进入是新生命周期满血敌人");
                f.Floor.enabled = false;
                Check(!rpg.RestoreCheckpoint() && restored == 1 && room.HasStarted && enemy.gameObject.activeSelf,
                    "检查点落地失败不发布恢复通知或重置遭遇");
                f.Floor.enabled = true;
            }
            finally { rpg.CheckpointRestored -= restoredListener; }
            Check(enemy.Entity.ReceiveHit("room-complete", 10000, 10000), "房间敌人可完成致死结算");
            room.TickEncounter();
            Check(room.IsComplete && !rpg.locomotion.EncounterCombat, "全灭后标记完成并回探索");
            Move(0); Move(10);
            Check(room.IsComplete && enemy.Entity.Combat.Dead, "完成后反复进出不刷出新敌人");
            room.ResetEncounter();
            Check(!room.IsComplete && !room.HasStarted && !enemy.gameObject.activeSelf, "显式重置清除完成标记并等待下一次进入");
            return $"房间区域/练习隔离/暂停重入/检查点/完成 {count} 项通过";
        }

        static void Invoke(object target, string method) => target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);

        sealed class Fixture : IDisposable
        {
            readonly GameObject root;
            readonly RpgStageDefinition stage;
            public readonly Vector3 Origin = new(7800, 0, 7800);
            public Vector3 SpawnPosition => Origin + Vector3.forward * 12;
            public GameObject Rig { get; }
            public LinearRpgController Rpg { get; }
            public CombatRoomEncounter Room { get; }
            public EnemyController Enemy { get; }
            public SkillTrainingTarget Target { get; }
            public BoxCollider Floor { get; }
            public Fixture()
            {
                root = new GameObject("P1房间隔离检查") { hideFlags = HideFlags.HideAndDontSave };
                root.SetActive(false); root.transform.position = Origin;
                try
                {
                    GameObject Child(string name, Transform parent = null)
                    { var go = new GameObject(name); go.transform.SetParent(parent ? parent : root.transform, false); return go; }
                    Rig = Child("玩家");
                    var head = Child("头部", Rig.transform).transform; head.localPosition = Vector3.up * 1.6f;
                    var lab = Rig.AddComponent<NarutoVR.SealLab.SealLab>(); lab.enabled = false;
                    var skills = Rig.AddComponent<SkillTestController>(); skills.enabled = false;
                    var effects = Rig.AddComponent<SkillEffects>(); effects.head = head; effects.playerRoot = Rig.transform; effects.lab = lab;
                    var locomotion = Rig.AddComponent<RpgLocomotionController>(); locomotion.head = head; locomotion.rigRoot = Rig.transform;
                    locomotion.floorLayers = 1; locomotion.obstacleLayers = 0;
                    Floor = Child("地面").AddComponent<BoxCollider>(); Floor.center = Vector3.down * .5f; Floor.size = new Vector3(30, 1, 40);
                    var node = Child("检查点").AddComponent<NavigationNode>(); node.nodeId = "room-check";
                    Target = Child("练习靶").AddComponent<SkillTrainingTarget>();
                    stage = ScriptableObject.CreateInstance<RpgStageDefinition>(); stage.stageId = "room-check";
                    stage.startsInCombat = true; stage.enableHazards = true; // 区域模式必须覆盖旧阶段设置。
                    skills.effects = effects; skills.lab = lab;
                    skills.catalog = AssetDatabase.LoadAssetAtPath<SkillCatalog>("Assets/ScriptableObjects/技能目录配置/线性RPG技能目录.asset");
                    Rpg = Rig.AddComponent<LinearRpgController>();
                Rpg.chapter = Rig.AddComponent<TrainingChapterFlow>();
                ((TrainingChapterFlow)Rpg.chapter).rpg = Rpg; ((TrainingChapterFlow)Rpg.chapter).initialSkills = new[]{"fireball"}; Rpg.skills = skills; Rpg.locomotion = locomotion;
                    Rpg.handPoseSettings = AssetDatabase.LoadAssetAtPath<BasicHandPoseSettings>("Assets/ScriptableObjects/基础手形配置/官方基础手形.asset");
                    Rpg.stages = new[] { stage }; Rpg.GetComponent<TrainingTargetGroup>().targets = new[] { Target }; Rpg.GetComponent<TrainingTargetGroup>().healthByStage = new[]{60f}; Rpg.GetComponent<TrainingTargetGroup>().resetAfterSeconds=3; Rpg.checkpointNodes = new[] { node };
                    Rpg.autoLoad = false; Rpg.GetComponent<TrainingChapterFlow>().autoAdvance = true; Rpg.roomControlsEncounter = true;
                    Rpg.saveFileName = "room-check-" + Guid.NewGuid().ToString("N") + ".json";
                    var go = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(EnemySetupAndChecks.PrefabPath), root.transform);
                    go.SetActive(false); go.transform.position = SpawnPosition;
                    Enemy = go.GetComponent<EnemyController>(); Enemy.tickAutomatically = false;
                    go.GetComponent<NavMeshAgent>().enabled = false;
                    Room = Child("房间").AddComponent<CombatRoomEncounter>(); Room.rpg = Rpg; Room.enemies = new[] { Enemy };
                    Room.area = Room.gameObject.AddComponent<BoxCollider>(); Room.area.isTrigger = true;
                    Room.area.center = new Vector3(0, 1, 10); Room.area.size = new Vector3(10, 4, 10);
                    root.SetActive(true); skills.enabled = false; Physics.SyncTransforms();
                    if (!Rpg.CanAct) throw new Exception("房间检查玩家初始化失败：" + Rpg.Status);
                    Invoke(Rpg, "Start");
                }
                catch { Dispose(); throw; }
            }
            public void Dispose()
            {
                if (root) Object.DestroyImmediate(root);
                if (stage) Object.DestroyImmediate(stage);
            }
        }
    }
}
#endif
