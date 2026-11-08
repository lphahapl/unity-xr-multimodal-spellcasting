#if UNITY_EDITOR
using System;
using NarutoVR.SealCombat.Enemies;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NarutoVR.SealCombat.Editor
{
    public static class EnemyDetectionChecks
    {
        public static string Run()
        {
            int count = 0;
            void Check(bool condition, string reason) { if (!condition) throw new Exception("探测检查：" + reason); count++; }
            GameObject root = null, player = null, wall = null, holder = null, hand = null;
            var simulation = Physics.simulationMode;
            try
            {
                Vector3 origin = new(7200, 0, 7200);
                holder = new GameObject("探测检查_隔离父物体"); holder.SetActive(false);
                root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(EnemySetupAndChecks.PrefabPath), origin, Quaternion.identity, holder.transform);
                root.GetComponent<UnityEngine.AI.NavMeshAgent>().enabled = false;
                holder.SetActive(true);
                var enemy = root.GetComponent<EnemyController>(); enemy.tickAutomatically = false; enemy.Agent.enabled = false;
                enemy.checkLineOfSight = false; enemy.loseSightSeconds = .1f; enemy.provokedLockSeconds = 1;
                player = new GameObject("探测检查_玩家"); player.transform.position = origin + new Vector3(0, 1, -1.5f);
                var receiver = player.AddComponent<EnemyAttackCheckReceiver>();
                var body = root.GetComponentInChildren<CapsuleCollider>();
                Check(enemy.Entity.DetectionSensor && enemy.Entity.DetectionSensor.GetComponent<SphereCollider>().isTrigger &&
                    body && !body.isTrigger && body.gameObject != enemy.Entity.DetectionSensor.gameObject,
                    "独立近距Trigger接线");
                enemy.InitializeSpawn(null, true); enemy.Tick(.05f);
                Check(!enemy.HasTarget && !enemy.target && enemy.CurrentBehaviour == EnemyController.Behaviour.Idle,
                    "召唤敌人没有指定目标时保持待机，不被近距自动搜索改变契约");
                enemy.InitializeSpawn(null, false); enemy.Tick(.05f);
                Check(enemy.HasTarget && enemy.target == player.transform, "身后近距玩家无实体Collider也可通过XR角色注册兜底发现");
                player.transform.position = origin + new Vector3(0, 1, -6); enemy.Tick(.2f);
                Check(enemy.HasTarget, "已锁定目标离开首次视野但仍在追踪距离内不会立即丢失");
                player.transform.position = origin + new Vector3(0, 1, -20); enemy.Tick(.2f);
                Check(!enemy.HasTarget, "超过丢失距离后解除追踪");
                root.transform.rotation = Quaternion.identity; player.transform.position = origin + new Vector3(0, 1, -1.5f);
                wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.transform.position = origin + new Vector3(0, 1, -.75f);
                wall.transform.localScale = new Vector3(2, 3, .2f); Physics.SyncTransforms();
                enemy.checkLineOfSight = true; enemy.InitializeSpawn(null, false); enemy.Tick(.05f);
                Check(enemy.HasTarget && enemy.DetectionReason == "近身范围", "背后近距隔墙仍能通过XR代表点发现");
                player.transform.position = origin + new Vector3(0, 1, -20);
                int notices = 0;
                enemy.onFirstPlayerDetected.AddListener(() => notices++);
                // 真正的 XR 碰撞体可在另一个物体树，且进入时头部仍在探测球外。
                hand = new GameObject("探测检查_XR手部");
                hand.transform.position = player.transform.position;
                hand.AddComponent<SphereCollider>().radius = .1f;
                Physics.simulationMode = SimulationMode.Script;
                void StepPhysics() { Physics.SyncTransforms(); Physics.Simulate(.02f); }
                enemy.InitializeSpawn(null, false);
                StepPhysics();
                hand.transform.position = origin + Vector3.up + Vector3.back * 1.7f;
                StepPhysics();
                Check(!enemy.HasTarget && notices == 0, "普通碰撞物进入不报发现");
                hand.transform.position = player.transform.position; StepPhysics();
                receiver.rig = hand.transform;
                hand.transform.position = origin + Vector3.up + Vector3.back * 1.7f; StepPhysics();
                Check(enemy.HasTarget && enemy.target == player.transform && notices == 1 &&
                    enemy.DetectionReason == "近身Trigger接触", "无需AI Tick，XR手物理进入即穿过遮挡与背向锁定实际玩家");
                StepPhysics();
                hand.transform.position = player.transform.position; StepPhysics();
                hand.transform.position = origin + Vector3.up + Vector3.back * 1.7f; StepPhysics();
                Check(notices == 1, "停留与二次进入不重复首次发现");
                hand.SetActive(false);
                enemy.InitializeSpawn(null, false);
                enemy.SetSuspended(true);
                hand.SetActive(true); StepPhysics();
                Check(!enemy.HasTarget && notices == 1, "暂停敌人忽略进入");
                hand.SetActive(false);
                enemy.InitializeSpawn(null, false);
                enemy.TryDie(); hand.SetActive(true); StepPhysics();
                Check(enemy.IsDead && !enemy.HasTarget && notices == 1, "死亡敌人忽略进入");
                hand.SetActive(false);
                enemy.InitializeSpawn(null, true);
                hand.SetActive(true); StepPhysics();
                Check(!enemy.HasTarget && notices == 1, "未指定目标的召唤敌人忽略物理触发");
                hand.SetActive(false);
                enemy.InitializeSpawn(null, false);
                Check(enemy.Entity.ReceiveHit("provoked", 1, 1, null, player.transform), "带攻击者的真实命中被接受");
                enemy.TryNotifyHit();
                enemy.Tick(.05f);
                Check(enemy.target == player.transform && enemy.HasTarget && notices == 2 &&
                    enemy.CurrentBehaviour == EnemyController.Behaviour.Hit && enemy.DetectionReason == "受击锁定",
                    "身后二十米隔墙命中在受击动作内完成发现");
                enemy.Tick(enemy.reactionTimeout + .1f); enemy.Tick(.05f);
                Check(!enemy.HasTarget, "强制锁定到期重新使用丢失规则");
                wall.SetActive(false); Physics.SyncTransforms();
                void Position(float distance, float angle)
                {
                    root.transform.rotation = Quaternion.identity;
                    player.transform.position = origin + Vector3.up + Quaternion.Euler(0, angle, 0) * Vector3.forward * distance;
                    Physics.SyncTransforms();
                }
                enemy.InitializeSpawn(null, false); Position(6, 61); enemy.Tick(.05f);
                Check(!enemy.HasTarget, "六米处偏转61度超出半视角60度");
                Position(9, 0); enemy.Tick(.05f);
                Check(!enemy.HasTarget, "正前方九米超出配置的八米");
                Position(6, -59); enemy.Tick(.05f);
                Check(enemy.HasTarget && enemy.target == player.transform && enemy.DetectionReason == "巡逻视野",
                    "无预设目标时扫描到六米左前方59度的注册玩家");
                enemy.InitializeSpawn(null, false);
                Position(5, 0);
                wall.transform.position = origin + Vector3.up + Vector3.forward * 3;
                wall.SetActive(true); Physics.SyncTransforms(); enemy.Tick(.05f);
                Check(!enemy.HasTarget, "巡逻视线被墙遮挡时不锁定");
                wall.SetActive(false); Position(6, 59); enemy.Tick(.05f);
                Check(enemy.HasTarget, "六米右前方59度且无遮挡时索敌");
                enemy.InitializeSpawn(null, false);
                receiver.allowed = false;
                player.transform.position = origin + Vector3.back + Vector3.up; enemy.Tick(.05f);
                Check(!enemy.HasTarget, "无可受击资格的注册角色不被自动选作目标");
                enemy.InitializeSpawn(player.transform, false); enemy.Tick(.05f);
                Check(!enemy.HasTarget, "预指定的无资格玩家也不会被近身范围选中");
                receiver.allowed = true; enemy.Tick(.05f); receiver.allowed = false; enemy.Tick(.05f);
                Check(!enemy.HasTarget, "已索敌后玩家失去资格会解除锁定");
                receiver.head = hand.transform; receiver.allowed = true; hand.SetActive(true);
                receiver.enabled = false; enemy.InitializeSpawn(hand.transform, false); enemy.Tick(.05f);
                Check(!enemy.HasTarget, "预设目标在独立XR树上时也识别已禁用的接收器");
                return $"敌人双碰撞器/近身真实触发/受击锁定/巡逻120度 {count} 项通过";
            }
            finally
            {
                Physics.simulationMode = simulation;
                if (hand) Object.DestroyImmediate(hand);
                if (wall) Object.DestroyImmediate(wall);
                if (player) Object.DestroyImmediate(player);
                if (holder) Object.DestroyImmediate(holder);
            }
        }
    }
}
#endif
