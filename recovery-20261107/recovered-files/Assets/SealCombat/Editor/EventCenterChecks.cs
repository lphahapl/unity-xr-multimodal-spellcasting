#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using NarutoVR.SealCombat.Events;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NarutoVR.SealCombat.Editor
{
    /// <summary>全部使用独立事件域，不清理或替换正在运行的全局业务监听。</summary>
    public static class EventCenterChecks
    {
        sealed class OwnerProbe : ScriptableObject
        {
            public Action<int> received;
            public void Receive(int value) => received(value);
        }

        public static int Run()
        {
            int checks = 0;
            void Check(bool passed, string reason) { if (!passed) throw new Exception("事件中心检查：" + reason); ++checks; }
            var errors = new List<string>();
            var hub = new EventHub((key, error) => errors.Add(key + ":" + error.Message));
            var key = new EventKey<int>("独立测试事件");
            var sameName = new EventKey<int>("独立测试事件");
            int sum = 0;
            Action<int> add = value => sum += value;
            var token = hub.Subscribe(key, add);
            hub.Publish(sameName, 9);
            Check(sum == 0, "事件键按实例身份区分，不按名称合并");
            hub.Publish(key, 3);
            Check(sum == 3, "类型化 payload 原样传递");
            var duplicate = hub.Subscribe(key, add);
            Check(ReferenceEquals(token, duplicate), "重复回调共享同一取消 token");
            hub.Publish(key, 2);
            Check(sum == 5, "重复订阅不重复通知");
            duplicate.Dispose(); token.Dispose(); token.Dispose(); hub.Publish(key, 8);
            Check(sum == 5, "任一共享 token 取消唯一监听，重复 Dispose 幂等");

            var resubscribe = hub.Subscribe(key, add);
            token.Dispose(); hub.Publish(key, 1);
            Check(sum == 6, "旧已取消 token 不移除后来同回调的订阅");
            hub.Unsubscribe(key, add); hub.Unsubscribe(key, add); hub.Publish(key, 10);
            Check(sum == 6, "显式退订与重复退订有效");
            resubscribe.Dispose();

            CheckSnapshotAndReentry(Check);
            CheckReset(Check);

            var badKey = new EventKey<int>("异常隔离");
            int afterError = 0;
            using var bad = hub.Subscribe<int>(badKey, _ => throw new InvalidOperationException("预期异常"));
            using var good = hub.Subscribe<int>(badKey, _ => ++afterError);
            hub.Publish(badKey, 0);
            Check(errors.Count == 1 && errors[0].Contains("预期异常") && afterError == 1,
                "监听异常送诊断回调且不阻断后续监听，不产生错误暂停日志");

            var owner = new GameObject("事件测试_owner") { hideFlags = HideFlags.HideAndDontSave };
            var otherOwner = new GameObject("事件测试_other_owner") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                int ownedCalls = 0;
                Action<int> owned = _ => ++ownedCalls;
                var ownedKey = new EventKey<int>("生命周期");
                var ownedToken = hub.Subscribe(ownedKey, owned, owner);
                int pruneCalls = 0;
                var pruneKey = new EventKey<int>("订阅时清理死亡 owner");
                Action<int> pruneHandler = _ => ++pruneCalls;
                using var staleOwnerToken = hub.Subscribe(pruneKey, pruneHandler, owner);
                bool rejected = false;
                try { hub.Subscribe(ownedKey, owned, otherOwner); }
                catch (ArgumentException) { rejected = true; }
                Check(rejected, "重复 delegate 换 owner 必须显式退订，不能暗改生命周期");
                hub.Publish(ownedKey, 1);
                Check(ownedCalls == 1, "有效 owner 正常接收");
                Object.DestroyImmediate(owner);
                using var replacedOwnerToken = hub.Subscribe(pruneKey, pruneHandler, otherOwner);
                staleOwnerToken.Dispose(); hub.Publish(pruneKey, 0);
                Check(pruneCalls == 1, "发布前重新订阅可清理死亡 owner，不丢失新监听的事件桶");
                hub.Publish(ownedKey, 1); ownedToken.Dispose();
                Check(ownedCalls == 1, "已销毁 owner 自动跳过且 token 仍安全释放");
                using var deadToken = hub.Subscribe(ownedKey, owned, owner);
                hub.Publish(ownedKey, 1);
                Check(ownedCalls == 1, "已经销毁的 owner 不建立新订阅");
                using var freshToken = hub.Subscribe(ownedKey, owned, otherOwner);
                hub.Publish(ownedKey, 1);
                Check(ownedCalls == 2, "清理死亡监听后新 owner 可重新订阅同回调");
            }
            finally
            {
                if (owner) Object.DestroyImmediate(owner);
                if (otherOwner) Object.DestroyImmediate(otherOwner);
                hub.Clear();
            }
            var inferredOwner = ScriptableObject.CreateInstance<OwnerProbe>();
            inferredOwner.hideFlags = HideFlags.HideAndDontSave;
            int inferredCalls = 0;
            inferredOwner.received = _ => ++inferredCalls;
            try
            {
                using var inferred = hub.Subscribe(key, inferredOwner.Receive);
                hub.Publish(key, 1); Object.DestroyImmediate(inferredOwner); hub.Publish(key, 1);
                Check(inferredCalls == 1, "实例方法自动推断 Unity owner，销毁后跳过");
            }
            finally { if (inferredOwner) Object.DestroyImmediate(inferredOwner); hub.Clear(); }
            bool rejectedNull = false, rejectedMulticast = false;
            try { hub.Subscribe<int>(null, add); } catch (ArgumentNullException) { rejectedNull = true; }
            try { hub.Subscribe(key, add + add); } catch (ArgumentException) { rejectedMulticast = true; }
            Check(rejectedNull && rejectedMulticast, "无效键与多播复合委托明确拒绝");
            return checks;
        }

        static void CheckSnapshotAndReentry(Action<bool, string> check)
        {
            var hub = new EventHub(); var key = new EventKey<int>("快照与重入");
            var order = new List<string>();
            IDisposable removed = null, added = null;
            Action<int> third = value => order.Add("C" + value);
            using var first = hub.Subscribe<int>(key, value =>
            {
                order.Add("A" + value);
                if (value != 0) return;
                removed.Dispose();
                added = hub.Subscribe(key, third);
                hub.Publish(key, 1);
            });
            removed = hub.Subscribe<int>(key, value => order.Add("B" + value));
            hub.Publish(key, 0);
            check(string.Join(",", order) == "A0,A1,C1", "退订跳过当前快照；新监听仅被后续重入发布看到");
            hub.Publish(key, 2);
            check(string.Join(",", order) == "A0,A1,C1,A2,C2", "重入完成后监听集合稳定");
            added.Dispose(); removed.Dispose(); hub.Clear();
            int selfCalls = 0; IDisposable self = null;
            self = hub.Subscribe<int>(key, _ => { ++selfCalls; self.Dispose(); hub.Publish(key, 4); });
            hub.Publish(key, 3);
            check(selfCalls == 1, "回调可自退订并重入，不重复自己");
        }

        static void CheckReset(Action<bool, string> check)
        {
            var hub = new EventHub(); var key = new EventKey<int>("独立域重置");
            int received = 0; Action<int> callback = _ => ++received;
            var old = hub.Subscribe(key, callback);
            hub.Clear();
            var current = hub.Subscribe(key, callback);
            old.Dispose(); hub.Publish(key, 1);
            check(received == 1 && !ReferenceEquals(old, current), "Clear 后旧 token 无法移除新订阅");
            current.Dispose(); hub.Clear();
            int stale = 0, fresh = 0;
            IDisposable replacement = null;
            using var resetter = hub.Subscribe<int>(key, _ =>
            {
                hub.Clear();
                replacement = hub.Subscribe<int>(key, __ => ++fresh);
            });
            using var staleToken = hub.Subscribe<int>(key, _ => ++stale);
            hub.Publish(key, 0);
            check(stale == 0 && fresh == 0, "派发期间 Clear 使旧快照失效，新订阅不混入旧派发");
            resetter.Dispose(); staleToken.Dispose(); hub.Publish(key, 1);
            check(fresh == 1, "清理旧快照 token 不影响新一代监听");
            replacement.Dispose(); hub.Clear();
        }
    }
}
#endif
