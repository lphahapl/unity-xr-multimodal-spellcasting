using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NarutoVR.SealCombat.Events
{
    /// <summary>
    /// 类型安全的事件身份。名称仅用于诊断；同名新实例不是同一个事件，请集中声明 static readonly Key。
    /// 多个参数使用一个不可变 payload 结构体承载，不复制不同参数数量的接口。
    /// </summary>
    public sealed class EventKey<T>
    {
        public string Name { get; }
        public EventKey(string name = null) => Name = string.IsNullOrWhiteSpace(name) ? typeof(T).Name : name;
        public override string ToString() => Name;
    }

    /// <summary>
    /// 主线程同步事件入口，没有后台队列、场景单例或自动游戏对象。
    /// 订阅者应在 OnEnable 订阅、OnDisable 释放 token；owner 只兜底过滤已销毁对象，不替代生命周期退订。
    /// </summary>
    public static class EventCenter
    {
        static readonly EventHub Shared = new();

        public static IDisposable Subscribe<T>(EventKey<T> key, Action<T> handler, Object owner = null) =>
            Shared.Subscribe(key, handler, owner);

        public static void Unsubscribe<T>(EventKey<T> key, Action<T> handler) => Shared.Unsubscribe(key, handler);
        public static void Publish<T>(EventKey<T> key, T payload) => Shared.Publish(key, payload);

        // 禁用 Domain Reload 也会执行，防止上次 Play 的静态监听跨会话残留。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Shared.Clear();
    }

    /// <summary>
    /// 可隔离使用的同步事件域，所有方法仅在 Unity 主线程调用。测试创建自己的实例，不清全局业务订阅。
    /// 发布取快照：新增监听从下一次发布（含重入发布）生效；本轮尚未调用的已退订监听会跳过。
    /// 同 Key、同 delegate 的重复订阅共享一个 token；任一持有者 Dispose 即取消唯一监听，Dispose 幂等。
    /// 同一 delegate 不允许改绑不同 owner；需要先退订再重新订阅。
    /// </summary>
    public sealed class EventHub
    {
        readonly Dictionary<object, IBucket> buckets = new();
        readonly Action<string, Exception> diagnostic;

        /// <param name="diagnostic">监听者异常的诊断出口。默认写 Warning；异常不会中断后续监听者。</param>
        public EventHub(Action<string, Exception> diagnostic = null) => this.diagnostic = diagnostic ?? DefaultDiagnostic;

        public IDisposable Subscribe<T>(EventKey<T> key, Action<T> handler, Object owner = null)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            if (handler.GetInvocationList().Length != 1)
                throw new ArgumentException("请逐个订阅单一回调，以便分别隔离异常和管理生命周期。", nameof(handler));
            if (ReferenceEquals(owner, null)) owner = handler.Target as Object;
            if (!ReferenceEquals(owner, null) && !owner) return EmptySubscription.Instance;
            if (!buckets.TryGetValue(key, out var untyped))
            {
                untyped = new Bucket<T>(this, key);
                buckets.Add(key, untyped);
            }
            return ((Bucket<T>)untyped).Subscribe(handler, owner);
        }

        public void Unsubscribe<T>(EventKey<T> key, Action<T> handler)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            if (buckets.TryGetValue(key, out var untyped)) ((Bucket<T>)untyped).Unsubscribe(handler);
        }

        public void Publish<T>(EventKey<T> key, T payload)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (buckets.TryGetValue(key, out var untyped)) ((Bucket<T>)untyped).Publish(payload);
        }

        /// <summary>仅清本事件域。先失效旧 token 和发布快照，再清表；旧 token 无法解除重置后的新订阅。</summary>
        public void Clear()
        {
            foreach (var bucket in buckets.Values) bucket.Clear();
            buckets.Clear();
        }

        void RemoveBucket(object key, IBucket bucket)
        {
            if (buckets.TryGetValue(key, out var current) && ReferenceEquals(current, bucket)) buckets.Remove(key);
        }

        void Report(string name, Exception error)
        {
            try { diagnostic(name, error); }
            catch (Exception diagnosticError)
            {
                // 诊断出口自身失败也不能阻断游戏事件派发。
                Debug.LogWarning($"事件 {name} 的诊断回调异常：{diagnosticError}\n原监听异常：{error}");
            }
        }

        static void DefaultDiagnostic(string name, Exception error) =>
            Debug.LogWarning($"事件 {name} 的监听回调异常，已继续通知其余监听者：{error}");

        interface IBucket { void Clear(); }

        sealed class EmptySubscription : IDisposable
        {
            internal static readonly EmptySubscription Instance = new();
            public void Dispose() { }
        }

        sealed class Bucket<T> : IBucket
        {
            readonly EventHub hub;
            readonly EventKey<T> key;
            readonly List<Subscription> listeners = new();

            public Bucket(EventHub hub, EventKey<T> key) { this.hub = hub; this.key = key; }

            public IDisposable Subscribe(Action<T> handler, Object owner)
            {
                // 此处清理不能移除字典中的 bucket，否则新订阅会落入已被摘除的 bucket。
                for (int i = listeners.Count - 1; i >= 0; i--)
                    if (listeners[i].OwnerDestroyed) Remove(listeners[i], false);
                foreach (var entry in listeners)
                {
                    if (!entry.Handler.Equals(handler)) continue;
                    if (!ReferenceEquals(entry.Owner, owner))
                        throw new ArgumentException("同一事件回调已绑定其他 owner，请先退订。", nameof(owner));
                    return entry;
                }
                var subscription = new Subscription(this, handler, owner);
                listeners.Add(subscription);
                return subscription;
            }

            public void Unsubscribe(Action<T> handler)
            {
                for (int i = listeners.Count - 1; i >= 0; i--)
                    if (listeners[i].Handler.Equals(handler)) Remove(listeners[i]);
            }

            public void Publish(T payload)
            {
                var snapshot = listeners.ToArray();
                foreach (var entry in snapshot)
                {
                    if (!entry.Active) continue;
                    if (entry.OwnerDestroyed) { Remove(entry); continue; }
                    try { entry.Handler(payload); }
                    catch (Exception error) { hub.Report(key.Name, error); }
                }
            }

            void Remove(Subscription entry, bool removeEmptyBucket = true)
            {
                if (!entry.Active) return;
                entry.Deactivate();
                listeners.Remove(entry);
                if (removeEmptyBucket && listeners.Count == 0) hub.RemoveBucket(key, this);
            }

            public void Clear()
            {
                foreach (var entry in listeners) entry.Deactivate();
                listeners.Clear();
            }

            sealed class Subscription : IDisposable
            {
                Bucket<T> bucket;
                public Action<T> Handler { get; private set; }
                public Object Owner { get; private set; }
                readonly bool hasOwner;
                public bool Active => bucket != null;
                public bool OwnerDestroyed => hasOwner && !Owner;

                public Subscription(Bucket<T> bucket, Action<T> handler, Object owner)
                { this.bucket = bucket; Handler = handler; Owner = owner; hasOwner = !ReferenceEquals(owner, null); }

                public void Dispose() => bucket?.Remove(this);
                public void Deactivate() { bucket = null; Handler = null; Owner = null; }
            }
        }
    }
}
