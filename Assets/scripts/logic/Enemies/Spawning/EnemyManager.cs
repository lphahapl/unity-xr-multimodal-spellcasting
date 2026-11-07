using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AI;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;

namespace NarutoVR.SealCombat.Enemies
{
    /// <summary>
    /// 场景/会话拥有的敌人加载与对象池。所有公开方法限 Unity 主线程调用。
    /// 加载由会话共享，调用者取消不取消其他调用者；关闭会话后销毁全部克隆，再释放资源句柄。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyManager : MonoBehaviour
    {
        [Header("一、敌人资源")]
        [InspectorName("敌人目录")] public EnemyCatalog catalog;
        [InspectorName("出生点导航采样半径（米）"), Min(.01f)] public float sampleRadius = 1;
        [Header("二、运行状态")]
        [SerializeField, InspectorName("加载与对象池状态（运行时）")] string status = "等待启动";
        [SerializeField, InspectorName("已加载类型数（运行时）")] int loadedTypeCount;
        [SerializeField, InspectorName("活跃敌人数（运行时）")] int activeCount;
        [SerializeField, InspectorName("闲置池数量（运行时）")] int pooledCount;
        [SerializeField, InspectorName("本会话累计创建实例")] int createdCount;
        [SerializeField, InspectorName("本会话累计从池取出")] int reusedCount;
        public string Status => status;
        public int LoadedTypeCount => loadedTypeCount;
        public int ActiveCount => activeCount;
        public int PooledCount => pooledCount;
        public int CreatedCount => createdCount;
        public int ReusedCount => reusedCount;
        readonly ReadOnlyDictionary<long, EnemyLease> empty = new(new Dictionary<long, EnemyLease>());
        public IReadOnlyDictionary<long, EnemyLease> ActiveEnemies => session != null ? session.View : empty;

        sealed class Load
        {
            public string Id;
            public object Key;
            public EnemyCombatDefinition Combat;
            public int Limit;
            public AsyncOperationHandle<GameObject> Handle;
            public bool HasHandle, Released;
            public Task<GameObject> Task;
            public readonly Stack<Instance> Pool = new();
        }
        sealed class Instance
        {
            public GameObject Root;
            public EnemyController Controller;
            public EnemyCombatDefinition OriginalCombat;
            public Load Type;
            public bool Transitioning, RecyclePending;
        }
        sealed class Session
        {
            public long Id;
            public bool Closed;
            public GameObject Root, InactiveRoot;
            public readonly CancellationTokenSource Stop = new();
            public readonly CancellationToken StopToken;
            public readonly Dictionary<string, Load> Loads = new(StringComparer.Ordinal);
            public readonly Dictionary<long, EnemyLease> Active = new();
            public readonly Dictionary<long, Instance> Instances = new();
            public readonly HashSet<Instance> AllInstances = new();
            public readonly ReadOnlyDictionary<long, EnemyLease> View;
            public Session() { View = new ReadOnlyDictionary<long, EnemyLease>(Active); StopToken = Stop.Token; }
        }
        Session session;
        int mainThread;
        long nextSession, nextLease;
        readonly List<long> destroyed = new();

        void Awake() => mainThread = Thread.CurrentThread.ManagedThreadId;
        void OnEnable()
        {
            if (mainThread == 0) mainThread = Thread.CurrentThread.ManagedThreadId;
            if (session != null) return;
            var current = new Session { Id = ++nextSession };
            current.Root = new GameObject(name + "·敌人会话");
            SceneManager.MoveGameObjectToScene(current.Root, gameObject.scene);
            current.InactiveRoot = new GameObject("闲置实例（不激活）");
            current.InactiveRoot.transform.SetParent(current.Root.transform, false);
            current.InactiveRoot.SetActive(false);
            session = current; createdCount = reusedCount = 0; status = "敌人会话已就绪"; UpdateCounts();
        }
        void OnDisable() => CloseSession();
        void OnDestroy() => CloseSession();
        void Update()
        {
            var current = session;
            if (current == null) return;
            destroyed.Clear();
            foreach (var pair in current.Active) if (!pair.Value.Controller) destroyed.Add(pair.Key);
            foreach (long id in destroyed)
            {
                if (current.Instances.TryGetValue(id, out var instance)) DestroyInstance(instance);
                current.Active.Remove(id); current.Instances.Remove(id);
            }
            current.AllInstances.RemoveWhere(instance => !instance.Root);
            foreach (var entry in current.Loads.Values) CleanPool(entry);
            UpdateCounts();
        }
        void RequireMainThread()
        {
            if (mainThread == 0 || mainThread != Thread.CurrentThread.ManagedThreadId)
                throw new InvalidOperationException("EnemyManager 只能在 Unity 主线程调用");
        }
        Session RequireSession(CancellationToken cancellation)
        {
            RequireMainThread(); cancellation.ThrowIfCancellationRequested();
            if (!this || !isActiveAndEnabled || session == null || session.Closed)
                throw new InvalidOperationException("敌人管理器会话已关闭");
            return session;
        }
        void RequireCurrent(Session current, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            if (!this || !isActiveAndEnabled || current.Closed || !ReferenceEquals(session, current) || !current.Root || !current.InactiveRoot)
                throw new OperationCanceledException("敌人加载所属会话已结束");
        }

        Load GetLoad(Session current, string id)
        {
            if (id != null && current.Loads.TryGetValue(id, out var existing)) return existing;
            string reason = null;
            if (!catalog || !catalog.Validate(out reason))
            { status = "敌人目录无效：" + (catalog ? reason : "未绑定目录"); throw new InvalidOperationException(status); }
            if (!catalog.TryGet(id, out var definition))
            { status = "未知敌人编号：" + id; throw new ArgumentException(status, nameof(id)); }
            var entry = new Load { Id = id, Key = definition.prefab.RuntimeKey, Combat = definition.combatDefinition, Limit = definition.maxInactive };
            // 先公开完整 promise，Addressables 同步抛错或初始化回调停用管理器时也不会观察到空 Task。
            var completion = new TaskCompletionSource<GameObject>(TaskCreationOptions.RunContinuationsAsynchronously);
            entry.Task = completion.Task;
            current.Loads.Add(id, entry);
            _ = LoadPrefab(current, entry, completion);
            _ = ObserveFailure(entry.Task); // 取消全部调用者后，后台共享任务的失败仍被观察；LoadPrefab负责状态与日志。
            return entry;
        }
        static async Task ObserveFailure(Task task) { try { await task; } catch { } }
        async Task LoadPrefab(Session current, Load entry, TaskCompletionSource<GameObject> completion)
        {
            try
            {
                entry.Handle = Addressables.LoadAssetAsync<GameObject>(entry.Key); entry.HasHandle = true;
                var prefab = await entry.Handle.Task;
                RequireCurrent(current, default);
                if (entry.Handle.Status != AsyncOperationStatus.Succeeded || !prefab)
                    throw new InvalidOperationException("Addressables 未返回敌人预置体", entry.Handle.OperationException);
                if (!prefab.GetComponent<EnemyController>() || !prefab.GetComponent<EnemyObj>() || !prefab.GetComponent<NavMeshAgent>())
                    throw new InvalidOperationException("预置体根节点需要 EnemyController、EnemyObj、NavMeshAgent：" + entry.Id);
                completion.TrySetResult(prefab);
                status = "已加载敌人：" + entry.Id; UpdateCounts();
            }
            catch (Exception error)
            {
                if (!current.Closed)
                {
                    current.Loads.Remove(entry.Id); Release(entry);
                    if (this && ReferenceEquals(session, current))
                    { status = "敌人加载失败：" + entry.Id + "；" + error.Message; Debug.LogWarning(status, this); UpdateCounts(); }
                }
                completion.TrySetException(error);
            }
        }
        static async Task<T> WaitForCaller<T>(Task<T> shared, CancellationToken caller, CancellationToken closed)
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(caller, closed);
            var cancelled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var registration = linked.Token.Register(() => cancelled.TrySetResult(true));
            await Task.WhenAny(shared, cancelled.Task);
            linked.Token.ThrowIfCancellationRequested();
            return await shared;
        }

        public async Task PreloadAsync(string enemyId, CancellationToken ct = default)
        {
            var current = RequireSession(ct); var entry = GetLoad(current, enemyId);
            await WaitForCaller(entry.Task, ct, current.StopToken); RequireCurrent(current, ct); UpdateCounts();
        }
        public async Task PrewarmAsync(string id, int count, CancellationToken ct = default)
        {
            var current = RequireSession(ct);
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            var entry = GetLoad(current, id);
            var prefab = await WaitForCaller(entry.Task, ct, current.StopToken); RequireCurrent(current, ct);
            CleanPool(entry);
            int target = Mathf.Min(count, entry.Limit);
            while (entry.Pool.Count < target)
            {
                RequireCurrent(current, ct);
                var instance = CreateInstance(current, entry, prefab);
                entry.Pool.Push(instance);
                UpdateCounts();
                await Task.Yield(); // 每帧至多为本次预热创建一个实例，避免集中 Instantiate 抢占手部追踪帧。
            }
            RequireCurrent(current, ct);
            status = $"预热完成：{id}，闲置 {entry.Pool.Count}/{entry.Limit}"; UpdateCounts();
        }

        public async Task<EnemyLease> SpawnEnemyAsync(string id, Vector3 pos, Quaternion rot, Transform target,
            bool isSummoned = false, CancellationToken ct = default)
        {
            var current = RequireSession(ct); var entry = GetLoad(current, id);
            var prefab = await WaitForCaller(entry.Task, ct, current.StopToken); RequireCurrent(current, ct);
            float rotationLength = rot.x * rot.x + rot.y * rot.y + rot.z * rot.z + rot.w * rot.w;
            if (!Finite(pos) || !float.IsFinite(rot.x) || !float.IsFinite(rot.y) || !float.IsFinite(rot.z) || !float.IsFinite(rot.w) ||
                !float.IsFinite(rotationLength) || rotationLength < .00001f)
                throw SpawnFailure(id, "出生位置或旋转无效");
            var templateAgent = prefab.GetComponent<NavMeshAgent>();
            var filter = new NavMeshQueryFilter { agentTypeID = templateAgent.agentTypeID, areaMask = templateAgent.areaMask };
            if (!float.IsFinite(sampleRadius) || sampleRadius <= 0 || !NavMesh.SamplePosition(pos, out var sampled, sampleRadius, filter))
                throw SpawnFailure(id, "出生位置附近没有匹配代理类型的 NavMesh");
            Instance instance = null;
            while (entry.Pool.Count > 0 && instance == null)
            {
                var candidate = entry.Pool.Pop();
                if (candidate.Root && candidate.Controller) { instance = candidate; reusedCount++; }
                else DestroyInstance(candidate);
            }
            instance ??= CreateInstance(current, entry, prefab);
            EnemyLease lease = null;
            instance.Transitioning = true;
            try
            {
                instance.Root.SetActive(false);
                instance.Controller.enabled = true;
                instance.Controller.BindEntityComponents();
                instance.Controller.Agent.enabled = true;
                instance.Controller.Agent.areaMask = templateAgent.areaMask;
                instance.Controller.Entity.CombatDefinition = entry.Combat ? entry.Combat : instance.OriginalCombat;
                instance.Controller.target = target; instance.Controller.isSpawnedEnemy = isSummoned;
                instance.Root.transform.SetParent(current.Root.transform, false);
                instance.Root.transform.SetPositionAndRotation(sampled.position, rot.normalized);
                lease = new EnemyLease(++nextLease, instance.Controller, id, this, current.Id);
                current.Active.Add(lease.Id, lease); current.Instances.Add(lease.Id, instance);
                instance.Root.SetActive(true);
                RequireCurrent(current, ct);
                if (!IsCurrent(lease)) throw new OperationCanceledException("出生过程中租约已被回收");
                var agent = instance.Controller.Agent;
                if (!agent || !agent.enabled || !agent.isOnNavMesh || !agent.Warp(sampled.position))
                    throw SpawnFailure(id, "代理无法进入出生位置的 NavMesh");
                instance.Controller.InitializeSpawn(target, isSummoned);
                RequireCurrent(current, ct);
                if (!IsCurrent(lease)) throw new OperationCanceledException("初始化回调结束了本次出生");
                if (!agent || !agent.enabled || !agent.isOnNavMesh || !instance.Controller.ValidateAnimator(out _))
                    throw SpawnFailure(id, "导航代理或动画接线无效");
                status = "敌人已出生：" + id + " #" + lease.Id; UpdateCounts(); return lease;
            }
            catch (Exception error)
            {
                if (!(error is OperationCanceledException) && this && ReferenceEquals(session, current))
                { status = "敌人出生失败：" + id + "；" + error.Message; Debug.LogWarning(status, this); }
                if (lease == null || CurrentRecord(current, lease))
                {
                    if (lease != null) { current.Active.Remove(lease.Id); current.Instances.Remove(lease.Id); }
                    DestroyInstance(instance);
                }
                throw;
            }
            finally
            {
                instance.Transitioning = false;
                if (instance.RecyclePending) { instance.RecyclePending = false; FinishRecycle(current, instance); }
                UpdateCounts();
            }
        }

        Exception SpawnFailure(string id, string reason)
        { status = "敌人出生失败：" + id + "；" + reason; Debug.LogWarning(status, this); return new InvalidOperationException(status); }
        static bool Finite(Vector3 p) => float.IsFinite(p.x) && float.IsFinite(p.y) && float.IsFinite(p.z);
        Instance CreateInstance(Session current, Load entry, GameObject prefab)
        {
            var root = Instantiate(prefab, current.InactiveRoot.transform, false);
            root.SetActive(false);
            var controller = root.GetComponent<EnemyController>();
            var instance = new Instance { Root = root, Controller = controller, Type = entry, OriginalCombat = root.GetComponent<EnemyObj>().CombatDefinition };
            current.AllInstances.Add(instance);
            createdCount++;
            return instance;
        }
        bool CurrentRecord(Session current, EnemyLease lease) => !current.Closed && lease != null && lease.Owner == this &&
            lease.SessionId == current.Id && current.Active.TryGetValue(lease.Id, out var owned) && ReferenceEquals(owned, lease);
        public bool IsCurrent(EnemyLease lease)
        {
            RequireMainThread();
            return session != null && CurrentRecord(session, lease) && lease.Controller && lease.Controller.isActiveAndEnabled;
        }
        public bool TryGetActive(long id, out EnemyLease lease)
        {
            RequireMainThread(); lease = null;
            if (session == null || !session.Active.TryGetValue(id, out var found) || !IsCurrent(found)) return false;
            lease = found; return true;
        }
        public bool TryGetActive(EnemyLease lease, out EnemyController controller)
        {
            controller = null;
            if (!IsCurrent(lease)) return false;
            controller = lease.Controller; return true;
        }
        public bool Recycle(EnemyLease lease)
        {
            RequireMainThread(); var current = session;
            if (current == null || !CurrentRecord(current, lease)) return false;
            var instance = current.Instances[lease.Id];
            current.Active.Remove(lease.Id); current.Instances.Remove(lease.Id);
            if (instance.Transitioning) instance.RecyclePending = true;
            else FinishRecycle(current, instance);
            UpdateCounts(); return true;
        }
        void FinishRecycle(Session current, Instance instance)
        {
            if (!instance.Root) return;
            if (!instance.Controller) { DestroyInstance(instance); return; }
            if (current.Closed) { DestroyInstance(instance); return; }
            instance.Controller.SetSuspended(true);
            if (current.Closed || !instance.Root) { DestroyInstance(instance); return; }
            instance.Controller.SetTarget(null);
            if (current.Closed || !instance.Root) { DestroyInstance(instance); return; }
            instance.Root.SetActive(false);
            if (current.Closed || !instance.Root) { DestroyInstance(instance); return; }
            if (instance.Type.Pool.Count >= instance.Type.Limit) { DestroyInstance(instance); return; }
            instance.Root.transform.SetParent(current.InactiveRoot.transform, false);
            instance.Type.Pool.Push(instance);
        }
        static void DestroyInstance(Instance instance)
        {
            if (!instance.Root) return;
            instance.Root.SetActive(false); Destroy(instance.Root);
        }
        static void CleanPool(Load entry)
        {
            bool dirty = false;
            foreach (var instance in entry.Pool) if (!instance.Root || !instance.Controller) { dirty = true; break; }
            if (!dirty) return;
            var previous = entry.Pool.ToArray(); entry.Pool.Clear();
            for (int i = previous.Length - 1; i >= 0; i--)
                if (previous[i].Root && previous[i].Controller) entry.Pool.Push(previous[i]);
                else DestroyInstance(previous[i]);
        }
        void CloseSession()
        {
            var current = session; if (current == null) return;
            session = null; current.Closed = true; status = "敌人会话关闭，正在释放实例与加载资源";
            current.Stop.Cancel(); current.Active.Clear(); current.Instances.Clear();
            foreach (var entry in current.Loads.Values) entry.Pool.Clear();
            foreach (var instance in current.AllInstances) DestroyInstance(instance);
            if (current.Root) { current.Root.SetActive(false); Destroy(current.Root); }
            UpdateCounts(); _ = ReleaseClosedSession(current);
        }
        static async Task ReleaseClosedSession(Session current)
        {
            try
            {
                foreach (var entry in current.Loads.Values) try { await entry.Task; } catch { }
                bool HasClones() { foreach (var instance in current.AllInstances) if (instance.Root) return true; return false; }
                while (current.Root || HasClones()) await Task.Yield(); // Destroy 在帧末执行；不能先卸掉仍被克隆引用的资源。
                foreach (var entry in current.Loads.Values) Release(entry);
                current.AllInstances.Clear();
            }
            catch (Exception error) { Debug.LogWarning("敌人会话资源释放失败：" + error); }
            finally { current.Stop.Dispose(); }
        }
        static void Release(Load entry)
        {
            if (!entry.HasHandle || entry.Released) return;
            entry.Released = true;
            if (entry.Handle.IsValid()) Addressables.Release(entry.Handle);
        }
        void UpdateCounts()
        {
            loadedTypeCount = activeCount = pooledCount = 0;
            if (session == null) return;
            activeCount = session.Active.Count;
            foreach (var entry in session.Loads.Values)
            {
                if (entry.Task != null && entry.Task.Status == TaskStatus.RanToCompletion) loadedTypeCount++;
                pooledCount += entry.Pool.Count;
            }
        }
    }
}
