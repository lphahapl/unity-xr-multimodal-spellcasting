using System;
using System.Collections.Generic;
using UnityEngine;

namespace NarutoVR.SealCombat.Rpg
{
    /// <summary>可组合的关卡目标。每次进入或恢复阶段都会重新 Begin，离开时 End。</summary>
    public abstract class ChapterObjective : MonoBehaviour
    {
        object binding;
        bool observing;
        /// <summary>状态可能已改变。订阅者重新读取 IsComplete，包括已失效的连续条件。</summary>
        public event Action<ChapterObjective> StateChanged;
        /// <summary>本轮目标所属玩家。</summary>
        protected LinearRpgController Owner { get; private set; }
        /// <summary>只有拥有者存在且目标组件启用时，才允许提供完成结果。</summary>
        public bool IsRunning => observing && Owner && isActiveAndEnabled;
        /// <summary>由具体目标计算完成状态；目标本身不推进章节或发奖励。</summary>
        public abstract bool IsComplete { get; }
        /// <summary>先清理上一轮，再绑定拥有者并启动本轮事件或状态。</summary>
        public void Begin(LinearRpgController owner) => BeginOwned(owner, this);
        /// <summary>停止当前根目标。组合内部的子目标由组合释放。</summary>
        public void End() => EndOwned(this);

        internal void BeginOwned(LinearRpgController owner, object consumer)
        {
            if (!owner) throw new ArgumentNullException(nameof(owner));
            if (binding != null && !ReferenceEquals(binding, consumer))
                throw new InvalidOperationException("目标已绑定其他监听方：" + name);
            EndOwned(consumer);
            binding = consumer;
            Owner = owner;
            try
            {
                if (isActiveAndEnabled)
                {
                    StartObserving();
                    if (IsComplete) NotifyStateChanged();
                }
            }
            catch { EndOwned(consumer); throw; }
        }

        internal void EndOwned(object consumer)
        {
            if (!ReferenceEquals(binding, consumer)) return;
            bool wasComplete = IsRunning && IsComplete;
            binding = null;
            try { StopObserving(); }
            finally { Owner = null; }
            if (wasComplete) NotifyStateChanged();
        }

        void StartObserving()
        {
            observing = true;
            try { OnBegin(); }
            catch { StopObserving(); throw; }
        }

        void StopObserving()
        {
            if (!observing) return;
            observing = false;
            OnEnd();
        }

        protected void NotifyStateChanged() => StateChanged?.Invoke(this);
        protected virtual void OnDisable()
        {
            if (binding == null) return;
            StopObserving();
            NotifyStateChanged();
        }
        protected virtual void OnEnable()
        {
            if (binding == null) return;
            try { StartObserving(); }
            catch { EndOwned(binding); throw; }
            NotifyStateChanged();
        }

        /// <summary>一次配置验证共用一个集合，防止跨组合、跨根重复接线或形成环。</summary>
        public static void ValidateTree(ChapterObjective root, HashSet<ChapterObjective> used)
        {
            if (!root || !used.Add(root))
                throw new InvalidOperationException("章节目标缺失、重复接线或循环引用：" + (root ? root.name : "空"));
            if (root is CompositeChapterObjective group) group.ValidateChildren(used);
            else root.ValidateObjective();
        }
        /// <summary>子类在此重置进度并订阅实际成功事件。</summary>
        protected virtual void OnBegin() { }
        /// <summary>子类在此解除事件、取消异步操作并清空临时状态。</summary>
        protected virtual void OnEnd() { }
        /// <summary>进入游戏前验证场景引用与参数，不启动目标。</summary>
        public virtual void ValidateObjective() { }
    }
}
