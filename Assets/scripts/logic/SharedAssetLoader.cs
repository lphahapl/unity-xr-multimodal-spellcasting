using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Object = UnityEngine.Object;

namespace NarutoVR.SealCombat
{
    /// <summary>目录把稳定的业务 ID 映射为本版本的内容地址。每个目录可限定自己的资源类型。</summary>
    public interface IResourceCatalog
    {
        bool TryResolve(Type assetType, string id, out string address);
    }

    internal interface IAssetLeaseOwner
    {
        bool Retired { get; }
        void ReleaseUser();
    }

    /// <summary>一位调用方对共享原型的使用权。请在 Unity 主线程释放，实例及副本的寿命由使用方管理。</summary>
    public sealed class AssetLease<T> : IDisposable where T : Object
    {
        T asset;
        IAssetLeaseOwner owner;
        internal AssetLease(T asset, IAssetLeaseOwner owner) { this.asset = asset; this.owner = owner; }
        public T Asset => owner != null && !owner.Retired ? asset : throw new ObjectDisposedException(nameof(AssetLease<T>));
        public void Dispose()
        {
            if (owner == null) return;
            SharedAssetLoader.RequireMainThread();
            var previous = owner;
            owner = null;
            asset = null;
            previous.ReleaseUser();
        }
    }

    /// <summary>进程内共用的 Addressables 加载入口。缓存键为实际类型与内容地址，最后一位使用者离开后释放。</summary>
    public static class SharedAssetLoader
    {
        static readonly Dictionary<(Type, string), IEntry> entries = new();
        static int mainThread;
        interface IEntry { void Retire(); }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSession()
        {
            mainThread = Thread.CurrentThread.ManagedThreadId;
            RetireAll();
        }

#if UNITY_EDITOR
        // 禁用 Domain Reload 时静态实例跨 Play 保留。场景已退出再清句柄，避免压住旧资源至下次 Play。
        [UnityEditor.InitializeOnLoadMethod]
        static void InstallEditorCleanup()
        {
            mainThread = Thread.CurrentThread.ManagedThreadId;
            UnityEditor.EditorApplication.playModeStateChanged -= OnPlayStateChanged;
            UnityEditor.EditorApplication.playModeStateChanged += OnPlayStateChanged;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= RetireAll;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += RetireAll;
        }
        static void OnPlayStateChanged(UnityEditor.PlayModeStateChange state)
        {
            if (state == UnityEditor.PlayModeStateChange.EnteredEditMode) RetireAll();
        }
#endif
        static void RetireAll()
        {
            var previous = new List<IEntry>(entries.Values);
            entries.Clear();
            foreach (var entry in previous) entry.Retire();
        }

        internal static void RequireMainThread()
        {
            if (Thread.CurrentThread.ManagedThreadId != mainThread)
                throw new InvalidOperationException("资源租约必须在 Unity 主线程取得和释放。");
        }

        public static async Task<AssetLease<T>> AcquireAsync<T>(IResourceCatalog catalog, string id,
            CancellationToken token = default) where T : Object
        {
            RequireMainThread();
            if (SynchronizationContext.Current == null)
                throw new InvalidOperationException("异步加载需要 Unity 主线程同步上下文。");
            token.ThrowIfCancellationRequested();
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (!catalog.TryResolve(typeof(T), id, out var address) || string.IsNullOrWhiteSpace(address))
                throw new KeyNotFoundException($"目录未定义 {typeof(T).Name}：{id}");
            var key = (typeof(T), address);
            Entry<T> entry;
            if (entries.TryGetValue(key, out var found))
            {
                entry = (Entry<T>)found;
                entry.Users++;
            }
            else
            {
                entry = new Entry<T>(address) { Users = 1 };
                entries.Add(key, entry);
                _ = entry.LoadAsync();
            }
            try
            {
                var pending = entry.Result.Task;
                if (!pending.IsCompleted && token.CanBeCanceled)
                {
                    var cancel = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    using var registration = token.Register(() => cancel.TrySetResult(true));
                    await Task.WhenAny(pending, cancel.Task);
                }
                token.ThrowIfCancellationRequested();
                var asset = await pending;
                token.ThrowIfCancellationRequested();
                if (entry.Retired || !asset) throw new OperationCanceledException("资源所属会话已结束。");
                return new AssetLease<T>(asset, entry);
            }
            catch { entry.ReleaseUser(); throw; }
        }

        sealed class Entry<T> : IEntry, IAssetLeaseOwner where T : Object
        {
            readonly string address;
            AsyncOperationHandle<T> handle;
            public readonly TaskCompletionSource<T> Result = new(TaskCreationOptions.RunContinuationsAsynchronously);
            public int Users;
            public bool Retired { get; private set; }
            bool settled, released;
            public Entry(string address) => this.address = address;

            public async Task LoadAsync()
            {
                try
                {
                    handle = Addressables.LoadAssetAsync<T>(address);
                    if (Retired) { ReleaseHandle(); return; }
                    var value = await handle.Task;
                    if (Retired) throw new OperationCanceledException("资源所属会话已结束。");
                    if (handle.Status != AsyncOperationStatus.Succeeded || !value)
                        throw new InvalidOperationException($"加载 {typeof(T).Name} 失败：{address}", handle.OperationException);
                    Result.TrySetResult(value);
                }
                catch (Exception error)
                {
                    RemoveIfCurrent();
                    Result.TrySetException(error);
                    _ = Result.Task.Exception; // 已取消的最后一个等待者也不会遗留未观察异常。
                }
                finally { settled = true; Collect(); }
            }

            public void ReleaseUser() { Users--; Collect(); }
            public void Retire()
            {
                Retired = true;
                Result.TrySetCanceled();
                // Play 退出或程序集重载后旧 Unity 上下文可能不再泵送待完成任务。
                // 直接归还已取得的引用，进行中的操作由 Addressables 完成后自行回收。
                ReleaseHandle();
            }
            void RemoveIfCurrent()
            {
                var key = (typeof(T), address);
                if (entries.TryGetValue(key, out var current) && ReferenceEquals(this, current)) entries.Remove(key);
            }
            void Collect()
            {
                if (!settled || released || (Users > 0 && !Retired)) return;
                released = true;
                RemoveIfCurrent();
                ReleaseHandle();
            }
            void ReleaseHandle()
            {
                var owned = handle;
                handle = default;
                if (owned.IsValid()) Addressables.Release(owned);
            }
        }
    }
}
