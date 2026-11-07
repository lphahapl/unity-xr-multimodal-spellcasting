#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using NarutoVR.SealCombat.Enemies;
using NarutoVR.SealCombat.Events;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NarutoVR.SealCombat.Editor
{
    public static class EnemyCombatChecks
    {
        public static string Run()
        {
            int count = 0;
            void Check(bool condition, string message) { if (!condition) throw new Exception("削韧检查：" + message); count++; }
            var config = ScriptableObject.CreateInstance<EnemyCombatDefinition>();
            GameObject enemyGo = null, projectileGo = null, obstacle = null, holder = null;
            try
            {
                config.maxHealth = 1000; config.maxPoise = 100; config.recoveryDelay = 4; config.recoveryPerSecond = 10;
                var state = new EnemyCombatState(config);
                Check(state.Apply("a", 10, 30) && state.Health == 990 && state.Poise == 70, "生命与削韧同时提交");
                Check(!state.Apply("a", 10, 30) && state.Poise == 70, "同一命中去重");
                state.Tick(3); Check(state.Poise == 70, "恢复延迟期间不回韧");
                state.Apply("b", 0, 10); state.Tick(3); Check(state.Poise == 60, "再次削韧重新等待");
                state.Tick(2); Check(state.Poise == 70, "跨过等待仅计剩余一秒恢复");
                state.Apply("c", 1, 100); state.Tick(100);
                Check(state.Broken && state.Poise == 0, "削空立即标记瘫痪且期间不自然恢复");
                state.CompleteSeated(); Check(!state.Broken && state.Poise == 100, "瘫痪动画完成才回韧");
                config.maxPoise = 200; Check(state.MaxPoise == 100 && new EnemyCombatState(config).MaxPoise == 200, "SO 与每只运行态隔离");
                config.maxPoise = 100;
                Check(!state.Apply("invalid", -1, 20) && !state.Apply("nan", 1, float.NaN), "无效输入不扣血削韧");
                Check(state.Apply("dead", 1000, 100) && state.Dead, "死亡优先");
                state.CompleteSeated(); state.Tick(100); Check(state.Poise == 0 && state.Dead, "死亡不回韧或复活");

                Vector3 origin = new(6500, 0, 6500);
                holder = new GameObject("削韧检查_隔离父物体"); holder.SetActive(false);
                enemyGo = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(EnemySetupAndChecks.PrefabPath), origin, Quaternion.identity, holder.transform);
                enemyGo.GetComponent<UnityEngine.AI.NavMeshAgent>().enabled = false;
                holder.SetActive(true);
                var enemy = enemyGo.GetComponent<EnemyController>(); enemy.tickAutomatically = false;
                enemy.Agent.enabled = false; enemy.checkLineOfSight = false; enemy.reactionTimeout = 100;
                var entity = enemy.Entity; entity.CombatDefinition = config;
                enemy.InitializeSpawn(null, true); enemy.animator.Rebind(); enemy.animator.Update(0);
                var weak = enemyGo.GetComponentsInChildren<EnemyHitZone>().Single(z => z.isWeakPoint);
                Check(weak.Owner == entity && weak.GetComponent<Collider>().isTrigger && weak.transform.parent.name == "mixamorig:Head", "后脑弱点跟随头骨且不附加物理阻挡");
                Check(enemyGo.GetComponent<EnemyPoiseBar>() && entity.Combat.MaxPoise == 100, "预制体韧性条与SO接线");
                weak.poiseMultiplier = 3; weak.damageMultiplier = 1; weak.bonusDamage = 0;
                Check(weak.TryApplyHit("weak1", 10, 20) && entity.Combat.Health == 990 && entity.Combat.Poise == 40, "默认弱点只放大削韧");
                weak.damageMultiplier = 2; weak.bonusDamage = 7;
                Check(weak.TryApplyHit("weak2", 10, 20) && entity.Combat.Health == 963 && entity.Combat.Broken, "倍率和固定额外伤害可配置");
                Check(enemy.CurrentBehaviour == EnemyController.Behaviour.Seated && enemy.CurrentAttack < 0, "本次削空立即进入Seated并停止攻击");
                Check(!weak.TryApplyHit("weak2", 10, 20) && entity.Combat.Health == 963, "多个身体区域不重复结算同一hitId");
                bool entered = false;
                for (int i = 0; i < 300; i++)
                {
                    enemy.Tick(.05f); enemy.animator.Update(.05f);
                    entered |= enemy.animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Seated");
                    if (entered && !entity.Combat.Broken) break;
                }
                Check(entered && !entity.Combat.Broken && entity.Combat.Poise == 100 && !enemy.IsDead, "真实Seated动画自然结束后恢复韧性及行动");

                // 实际球形扫掠：从后脑方向先撞弱点；正面则由头部实体挡住。
                weak.damageMultiplier = 1; weak.bonusDamage = 0;
                void Shoot(Vector3 start, Vector3 direction)
                {
                    projectileGo = new GameObject("削韧投射物检查"); projectileGo.transform.position = start;
                    var projectile = projectileGo.AddComponent<SkillProjectile>(); projectile.damage = 10; projectile.poiseDamage = 20;
                    // This probe invokes Update once. Cover its fixed 4 m test lane even on fast headless frames.
                    if (Time.deltaTime <= 0) throw new Exception("削韧扫掠测试需要播放中的正时间步");
                    projectile.speed = 4f / Time.deltaTime; projectile.radius = .02f; projectile.GameClock = () => 0;
                    Physics.SyncTransforms(); projectile.Initialize(direction, null, ~0, 0, null, null);
                    typeof(SkillProjectile).GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(projectile, null);
                    Check(projectile.IsFinished, "实际扫掠完成命中");
                    if (projectileGo) Object.DestroyImmediate(projectileGo); projectileGo = null;
                }
                enemy.ResetForSpawn(); enemy.animator.Rebind(); enemy.animator.Update(0);
                Physics.SyncTransforms();
                Vector3 weakCenter = weak.GetComponent<SphereCollider>().bounds.center;
                Shoot(weakCenter - Vector3.forward * 2, Vector3.forward);
                Check(entity.Combat.Poise == 40, "背后火球实际命中后脑并三倍削韧");
                enemy.ResetForSpawn(); enemy.animator.Rebind(); enemy.animator.Update(0);
                Physics.SyncTransforms();
                weakCenter = weak.GetComponent<SphereCollider>().bounds.center;
                Shoot(weakCenter + Vector3.forward * 2, Vector3.back);
                Check(entity.Combat.Poise == 80, "正面火球不穿透头部领取后脑加成");
                enemy.ResetForSpawn();
                obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube); obstacle.transform.position = weakCenter - Vector3.forward;
                obstacle.transform.localScale = new Vector3(1, 1, .2f);
                Shoot(weakCenter - Vector3.forward * 2, Vector3.forward);
                Check(entity.Combat.Poise == 100, "墙体先命中时不能隔墙削韧");
                obstacle.GetComponent<Collider>().isTrigger = true;
                Shoot(weakCenter - Vector3.forward * 2, Vector3.forward);
                Check(entity.Combat.Poise == 40, "无关Trigger不会挡住火球");
                CheckReentrantHits(enemy, Check);
                Check(entity.ReceiveHit("kill", 10000, 100) && enemy.IsDead && entity.Combat.Dead, "实际生命归零进入死亡状态");
                return $"敌人SO/韧性/瘫痪/弱点/实际投射物 {count} 项通过";
            }
            finally
            {
                if (projectileGo) Object.DestroyImmediate(projectileGo);
                if (obstacle) Object.DestroyImmediate(obstacle);
                if (enemyGo) Object.DestroyImmediate(enemyGo);
                if (holder) Object.DestroyImmediate(holder);
                Object.DestroyImmediate(config);
            }
        }

        static void CheckReentrantHits(EnemyController enemy, Action<bool, string> check)
        {
            var entity = enemy.Entity;
            var target = new GameObject("削韧重入检查目标");
            try
            {
                target.transform.position = enemy.transform.position + enemy.transform.forward;
                int died = 0, broken = 0, vitals = 0;
                using var deathToken = EventCenter.Subscribe(EnemyEvents.Died, notice => { if (notice.Enemy == entity) died++; });
                using var brokenToken = EventCenter.Subscribe(EnemyEvents.PoiseBroken, notice => { if (notice.Enemy == entity) broken++; });
                using var vitalsToken = EventCenter.Subscribe(EnemyEvents.VitalsChanged, notice => { if (notice.Enemy == entity) vitals++; });
                foreach (bool lethal in new[] { true, false })
                {
                    enemy.InitializeSpawn(target.transform, true); enemy.animator.Rebind(); enemy.animator.Update(0);
                    for (int i = 0; i < 40 && enemy.CurrentAttack < 0; i++)
                    { enemy.Tick(.05f); enemy.animator.Update(.05f); }
                    check(enemy.CurrentAttack >= 0, "重入检查先进入真实攻击流程");
                    var previous = entity.Combat;
                    int resets = 0;
                    UnityEngine.Events.UnityAction<int> resetOnFinished = _ => { resets++; enemy.InitializeSpawn(null, true); };
                    enemy.onAttackFinished.AddListener(resetOnFinished);
                    bool accepted;
                    died = broken = vitals = 0;
                    try { accepted = entity.ReceiveHit(lethal ? "reset-from-death" : "reset-from-break", lethal ? 10000 : 0, 100000); }
                    finally { enemy.onAttackFinished.RemoveListener(resetOnFinished); }
                    check(accepted && resets == 1 && !ReferenceEquals(previous, entity.Combat),
                        lethal ? "致死打断攻击的结束回调可重生" : "削空打断攻击的结束回调可重生");
                    check(enemy.CanReceiveHit && !enemy.IsDead && !entity.Combat.Broken &&
                        entity.Combat.Health == entity.Combat.MaxHealth && entity.Combat.Poise == entity.Combat.MaxPoise,
                        "旧命中返回后不暂停或破坏新生命周期");
                    check(died == 0 && broken == 0 && vitals == 0, "重生后不发布旧生命的数值、死亡或破韧通知");
                }

                enemy.InitializeSpawn(null, true); enemy.animator.Rebind(); enemy.animator.Update(0);
                died = broken = vitals = 0;
                bool nestedAccepted = false, correctDeathPayload = true;
                using var payloadToken = EventCenter.Subscribe(EnemyEvents.Died, notice =>
                {
                    if (notice.Enemy == entity)
                        correctDeathPayload &= notice.Dead && notice.Health == 0 && notice.HitId == "nested-lethal";
                });
                using var nestedToken = EventCenter.Subscribe(EnemyEvents.VitalsChanged, notice =>
                {
                    if (notice.Enemy == entity && notice.HitId == "outer-hit")
                        nestedAccepted = entity.ReceiveHit("nested-lethal", 10000, 0);
                });
                check(entity.ReceiveHit("outer-hit", 1, 1) && nestedAccepted && enemy.IsDead,
                    "数值监听中另一个命中可完成致死结算");
                check(died == 1 && correctDeathPayload && vitals == 2 && broken == 0,
                    "嵌套致死只发布一次死亡且快照为生命零，外层不追加错误死亡通知");
            }
            finally
            {
                enemy.InitializeSpawn(null, true); enemy.animator.Rebind(); enemy.animator.Update(0);
                Object.DestroyImmediate(target);
            }
        }
    }
}
#endif
