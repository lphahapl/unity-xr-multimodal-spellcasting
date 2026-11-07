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
            GameObject root = null, player = null, wall = null, holder = null;
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
                Check(enemy.Entity.DetectionSensor && enemy.Entity.DetectionSensor.GetComponent<SphereCollider>().isTrigger,
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
                Check(!enemy.HasTarget, "近距默认不隔墙发现");
                enemy.Entity.DetectionSensor.ignoreOcclusion = true; enemy.Tick(.05f);
                Check(enemy.HasTarget, "可配置近距忽略遮挡");
                enemy.Entity.DetectionSensor.ignoreOcclusion = false; wall.SetActive(false); Physics.SyncTransforms();
                player.transform.position = origin + new Vector3(0, 1, -20);
                enemy.InitializeSpawn(null, false);
                Check(enemy.Entity.ReceiveHit("provoked", 1, 1, null, player.transform), "带攻击者的真实命中被接受");
                enemy.Tick(.05f);
                Check(enemy.target == player.transform && enemy.HasTarget && enemy.HasEverDetectedPlayer, "视野外受击自动锁定攻击者并触发发现");
                enemy.Tick(1.1f); Check(!enemy.HasTarget, "强制锁定到期重新使用丢失规则");
                enemy.InitializeSpawn(null, false); receiver.allowed = false;
                player.transform.position = origin + Vector3.back + Vector3.up; enemy.Tick(.05f);
                Check(!enemy.HasTarget, "无可受击资格的注册角色不被自动选作目标");
                return $"敌人近距Trigger/遮挡/受击锁定 {count} 项通过";
            }
            finally { if (wall) Object.DestroyImmediate(wall); if (player) Object.DestroyImmediate(player); if (root) Object.DestroyImmediate(root); if (holder) Object.DestroyImmediate(holder); }
        }
    }
}
#endif
