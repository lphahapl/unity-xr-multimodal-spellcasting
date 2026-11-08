#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NarutoVR.SealCombat.Enemies;
using NarutoVR.SealCombat.Rpg;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;
using Object = UnityEngine.Object;

namespace NarutoVR.SealCombat.Editor
{
    public static class EnemyPoolingChecks
    {
        public static async Task WithTimeout(Task task)
        {
            if (await Task.WhenAny(task, Task.Delay(30000)) != task) throw new TimeoutException("敌人异步检查超过30秒");
            await task;
        }
        public static async Task<string> Run()
        {
            var room = Object.FindFirstObjectByType<CombatRoomEncounter>();
            var rpg = room.rpg; var move = rpg.locomotion;
            room.ResetEncounter(); room.enabled = false;
            var root = new GameObject("对象池回归临时服务"); root.SetActive(false);
            var manager = root.AddComponent<EnemyManager>(); manager.catalog = room.enemyManager.catalog;
            var catalog = ScriptableObject.CreateInstance<EnemyCatalog>();
            int checks = 0;
            void Check(bool ok, string message) { if (!ok) throw new Exception("对象池：" + message); checks++; }
            async Task Reject<T>(Task task, string message) where T : Exception
            { bool failed = false; try { await WithTimeout(task); } catch (T) { failed = true; } Check(failed, message); }
            var spawn = room.spawnPoints.Single(); var pos = spawn.point.position; var rot = spawn.point.rotation;
            string id = spawn.enemyId;
            try
            {
                var definition = manager.catalog.enemies.Single();
                catalog.enemies = new[] { definition, definition };
                Check(!catalog.Validate(out var reason) && reason.Contains("重复"), "重复ID应报错");
                catalog.enemies = new[] { definition };
                Check(catalog.Validate(out _) && catalog.TryGet(id, out var lookup) && lookup == definition, "目录ID索引");
                root.SetActive(true);
                using (var cancelled = new CancellationTokenSource())
                {
                    cancelled.Cancel();
                    await Reject<OperationCanceledException>(manager.SpawnEnemyAsync(id, pos, rot, move.head, false, cancelled.Token), "预取消请求不生成");
                }
                Check(manager.ActiveCount == 0 && manager.LoadedTypeCount == 0, "预取消不加载资源");
                using (var cancel = new CancellationTokenSource())
                {
                    var first = manager.PreloadAsync(id, cancel.Token);
                    var other = manager.PreloadAsync(id);
                    cancel.Cancel();
                    try { await WithTimeout(first); } catch (OperationCanceledException) { }
                    try { await WithTimeout(other); }
                    catch (Exception error)
                    {
                        var session = typeof(EnemyManager).GetField("session", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(manager);
                        var loads = (System.Collections.IDictionary)session.GetType().GetField("Loads").GetValue(session);
                        var load = loads[id];
                        var handle = (UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle<GameObject>)load.GetType().GetField("Handle").GetValue(load);
                        throw new Exception($"共享加载诊断 status={manager.Status}, first={first.Status}, other={other.Status}, handleDone={handle.IsDone}, handleStatus={handle.Status}, paused={UnityEditor.EditorApplication.isPaused}, timeScale={Time.timeScale}, frame={Time.frameCount}, context={SynchronizationContext.Current?.GetType().Name}", error);
                    }
                    Check(manager.LoadedTypeCount == 1, "一方取消不影响共享加载");
                }
                await WithTimeout(manager.PrewarmAsync(id, 2));
                Check(manager.PooledCount == 2 && manager.ActiveCount == 0, "预热不激活敌人");
                await Reject<ArgumentException>(manager.SpawnEnemyAsync("missing", pos, rot, move.head), "未知ID显式失败");
                await Reject<InvalidOperationException>(manager.SpawnEnemyAsync(id, pos + Vector3.up * 100, rot, move.head), "无导航面不得生成");
                Check(manager.PooledCount == 2 && manager.ActiveCount == 0, "无效出生不消耗池或登记实例");
                var normal = await manager.SpawnEnemyAsync(id, pos, rot, move.head);
                var controller = normal.Controller; controller.tickAutomatically = false;
                Check(manager.IsCurrent(normal) && manager.TryGetActive(normal.Id, out var found) && ReferenceEquals(normal, found), "活跃注册与查询");
                Check(!controller.isSpawnedEnemy && controller.Agent.isOnNavMesh, "普通敌人与导航接入");
                float maxHp = controller.Entity.Combat.MaxHealth, maxPoise = controller.Entity.Combat.MaxPoise;
                Check(controller.Entity.ReceiveHit("pooled-hit", 10, 20, null, move.head), "首轮受击");
                typeof(EnemyController).GetMethod("BeginAttack", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, null);
                Check(controller.CurrentAttack >= 0, "通过真实攻击入口建立回收前的攻击状态");
                Check(manager.Recycle(normal) && !controller.gameObject.activeSelf && controller.target == null && controller.CurrentAttack == -1, "回收停用、清目标与攻击");
                Check(!controller.Entity.EvaluateStrike(.6f, false, 1.4f, ~0), "回收后攻击窗口失效");
                Check(!manager.Recycle(normal), "重复回收被拒绝");
                var summoned = await manager.SpawnEnemyAsync(id, pos, rot, move.head, true);
                Check(summoned.Controller == controller && summoned.Id != normal.Id, "复用同对象但新出生ID");
                Check(!manager.IsCurrent(normal) && !manager.Recycle(normal) && manager.IsCurrent(summoned), "旧租约不能回收新出生");
                Check(controller.Entity.Combat.Health == maxHp && controller.Entity.Combat.Poise == maxPoise && !controller.HasEverDetectedPlayer && controller.CurrentAttack == -1, "新出生重置生命韧性和行为记录");
                Check(controller.isSpawnedEnemy && controller.target == move.head, "召唤标志与目标透传");
                controller.SetTarget(null); controller.Tick(.1f);
                Check(controller.CurrentBehaviour == EnemyController.Behaviour.Idle, "召唤敌人无目标待机");
                Check(controller.Entity.ReceiveHit("pooled-hit", 1, 0), "新出生受击去重集重置");
                controller.Entity.ReceiveHit("pool-lethal", 10000, 0);
                Check(controller.IsDead, "死亡状态");
                manager.Recycle(summoned);
                var reborn = await manager.SpawnEnemyAsync(id, pos, rot, move.head);
                Check(!reborn.Controller.IsDead && reborn.Controller.Entity.Combat.Health == maxHp, "死亡后池化重生");
                manager.Recycle(reborn);
                await WithTimeout(manager.PrewarmAsync(id, 20));
                Check(manager.PooledCount == definition.maxInactive, "预热受闲置上限约束");
                var many = new EnemyLease[definition.maxInactive + 1];
                for (int i = 0; i < many.Length; i++) many[i] = await manager.SpawnEnemyAsync(id, pos, rot, null, true);
                foreach (var lease in many) manager.Recycle(lease);
                Check(manager.PooledCount == definition.maxInactive && manager.ActiveCount == 0, "回收超额销毁，不无界缓存");
                var beforeDisable = await manager.SpawnEnemyAsync(id, pos, rot, null);
                root.SetActive(false);
                Check(manager.ActiveCount == 0 && manager.PooledCount == 0 && manager.LoadedTypeCount == 0 && !manager.Recycle(beforeDisable), "关闭服务清注册与失效租约");
                root.SetActive(true);
                var reopened = await manager.SpawnEnemyAsync(id, pos, rot, null);
                Check(reopened.Id != beforeDisable.Id && manager.IsCurrent(reopened) && !manager.IsCurrent(beforeDisable), "新会话隔离旧租约");
                manager.Recycle(reopened);
                root.SetActive(false); root.SetActive(true);
                var late = manager.SpawnEnemyAsync(id, pos, rot, null);
                root.SetActive(false);
                try { await WithTimeout(late); } catch (OperationCanceledException) { }
                Check(manager.ActiveCount == 0, "缓存命中后立即关闭无残留活跃实例");
                checks += await CheckDelayedLoading(pos, rot);
                root.SetActive(true);
                var coordinator = root.AddComponent<GameManager>(); coordinator.enemyManager = manager;
                coordinator.prepareEnemies = new[] { new GameManager.EnemyPreparation { enemyId = id, count = 1 } };
                coordinator.BeginPreparation(); await WithTimeout(coordinator.Ready);
                Check(coordinator.IsReady && manager.PooledCount == 1, "启动协调器预热");
                root.SetActive(false); root.SetActive(true);
                await WithTimeout(coordinator.Ready);
                Check(coordinator.IsReady && manager.PooledCount == 1, "同根服务重新启用的执行顺序");
                root.SetActive(false);

                // Room cancellation uses its own manager and must not cancel that manager's shared cache.
                room.enabled = true; rpg.RestoreCheckpoint();
                move.rigRoot.position += new Vector3(0, -.03f, 20) - move.PlayerFloorPosition;
                Physics.SyncTransforms(); room.TickEncounter(); var pending = room.SpawnTask;
                room.ResetEncounter(); room.enabled = false;
                await WithTimeout(pending);
                Check(!room.HasStarted && room.enemyManager.ActiveCount == 0 && !room.IsLoadingEnemies, "重置在途房间批次后不留下敌人");
                for (int mode = 0; mode < 2; mode++)
                {
                    room.enabled = true; room.TickEncounter(); await WithTimeout(room.SpawnTask);
                    Check(room.HasStarted, "重入回归已生成房间敌人");
                    var attacking = room.enemies.Single(); attacking.tickAutomatically = false;
                    typeof(EnemyController).GetMethod("BeginAttack", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(attacking, null);
                    Check(attacking.CurrentAttack >= 0, "重入回归已建立真实攻击");
                    UnityEngine.Events.UnityAction<int> reset = _ => room.ResetEncounter();
                    attacking.onAttackFinished.AddListener(reset);
                    try
                    {
                        if (mode == 0)
                        {
                            move.rigRoot.position += new Vector3(0, -.03f, 17) - move.PlayerFloorPosition;
                            Physics.SyncTransforms(); room.TickEncounter();
                        }
                        else room.enabled = false;
                        Check(!room.HasStarted && room.enemyManager.ActiveCount == 0 && !move.EncounterCombat, "结束攻击回调重置房间，不覆盖新生命周期");
                    }
                    finally { attacking.onAttackFinished.RemoveListener(reset); }
                    move.rigRoot.position += new Vector3(0, -.03f, 20) - move.PlayerFloorPosition;
                    Physics.SyncTransforms();
                }
                return $"对象池 {checks} 项通过：目录、共享异步加载、取消、预热上限、导航失败、出生类型、复用重置、旧租约、关闭重开与房间取消。";
            }
            finally
            {
                root.SetActive(false); Object.Destroy(root); Object.Destroy(catalog);
                room.ResetEncounter(); room.enabled = true; rpg.RestoreCheckpoint();
            }
        }

        // Control the provider's completion, not production clocks: cached loads may finish synchronously.
        sealed class DelayedProvider : ResourceProviderBase
        {
            ProvideHandle pending;
            public bool Started, Completed;
            public int Releases;
            public override void Provide(ProvideHandle handle) { pending = handle; Started = true; }
            public void Complete(GameObject prefab)
            { if (Started && !Completed) { Completed = true; pending.Complete(prefab, true, null); } }
            public override void Release(IResourceLocation location, object obj) => Releases++;
        }
        static async Task Until(Func<bool> condition)
        { while (!condition()) await Task.Yield(); }
        static async Task<int> CheckDelayedLoading(Vector3 position, Quaternion rotation)
        {
            int checks = 0;
            void Check(bool pass, string reason) { if (!pass) throw new Exception("可控延迟加载：" + reason); checks++; }
            for (int mode = 0; mode < 2; mode++)
            {
                string key = Guid.NewGuid().ToString("N");
                var provider = new DelayedProvider(); provider.Initialize("enemy-test-" + key, "");
                var locator = new ResourceLocationMap(key); locator.Add(key, new ResourceLocationBase(key, key, provider.ProviderId, typeof(GameObject)));
                Addressables.ResourceManager.ResourceProviders.Add(provider); Addressables.AddResourceLocator(locator);
                var definition = ScriptableObject.CreateInstance<EnemyDefinition>();
                definition.enemyId = "delayed"; definition.prefab = new AssetReferenceGameObject(key);
                var catalog = ScriptableObject.CreateInstance<EnemyCatalog>(); catalog.enemies = new[] { definition };
                var root = new GameObject("延迟加载生命周期检查"); root.SetActive(false);
                var manager = root.AddComponent<EnemyManager>(); manager.catalog = catalog; root.SetActive(true);
                var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPoolingSetup.PrefabPath);
                try
                {
                    if (mode == 0)
                    {
                        using var cancel = new CancellationTokenSource();
                        var first = manager.PreloadAsync("delayed", cancel.Token); var other = manager.PreloadAsync("delayed");
                        await WithTimeout(Until(() => provider.Started));
                        cancel.Cancel(); bool cancelled = false;
                        try { await WithTimeout(first); } catch (OperationCanceledException) { cancelled = true; }
                        Check(cancelled && !other.IsCompleted && provider.Releases == 0, "取消只影响单个请求，不释放共享资源");
                        provider.Complete(prefab); await WithTimeout(other);
                        Check(manager.LoadedTypeCount == 1, "共享加载剩余调用者成功");
                    }
                    else
                    {
                        var old = manager.SpawnEnemyAsync("delayed", position, rotation, null);
                        await WithTimeout(Until(() => provider.Started));
                        Check(!old.IsCompleted, "真正等待资源期间关闭");
                        root.SetActive(false); bool cancelled = false;
                        try { await WithTimeout(old); } catch (OperationCanceledException) { cancelled = true; }
                        Check(cancelled && manager.ActiveCount == 0 && provider.Releases == 0, "旧会话及时取消且不提前释放未完成句柄");
                        root.SetActive(true); var next = manager.SpawnEnemyAsync("delayed", position, rotation, null);
                        provider.Complete(prefab); await WithTimeout(next);
                        Check(manager.IsCurrent(next.Result) && manager.ActiveCount == 1, "迟到结果仅允许新会话生成");
                    }
                }
                finally
                {
                    root.SetActive(false); provider.Complete(prefab); Object.Destroy(root);
                    await WithTimeout(Until(() => provider.Releases > 0));
                    Check(provider.Releases == 1, "所有实例销毁后底层资源恰好释放一次");
                    Addressables.RemoveResourceLocator(locator); Addressables.ResourceManager.ResourceProviders.Remove(provider);
                    Object.Destroy(catalog); Object.Destroy(definition);
                }
            }
            return checks;
        }
    }
}
#endif
