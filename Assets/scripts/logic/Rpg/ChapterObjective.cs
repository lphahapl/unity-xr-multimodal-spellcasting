using UnityEngine;

namespace NarutoVR.SealCombat.Rpg
{
    /// <summary>可组合的关卡目标。每次进入或恢复阶段都会重新 Begin，离开时 End。</summary>
    public abstract class ChapterObjective : MonoBehaviour
    {
        /// <summary>本轮目标的拥有者；End 后清空，异步回调应检查是否仍属本轮。</summary>
        protected LinearRpgController Owner { get; private set; }
        /// <summary>只有拥有者存在且目标组件启用时，才允许提供完成结果。</summary>
        public bool IsRunning => Owner && isActiveAndEnabled;
        /// <summary>由具体目标计算完成状态；目标本身不推进章节或发奖励。</summary>
        public abstract bool IsComplete { get; }
        /// <summary>先清理上一轮，再绑定拥有者并启动本轮事件或状态。</summary>
        public void Begin(LinearRpgController owner) { End(); Owner = owner; OnBegin(); }
        /// <summary>停止本轮目标并清空拥有者；清理钩子必须允许重复调用。</summary>
        public void End() { OnEnd(); Owner = null; }
        /// <summary>子类在此重置进度并订阅实际成功事件。</summary>
        protected virtual void OnBegin() { }
        /// <summary>子类在此解除事件、取消异步操作并清空临时状态。</summary>
        protected virtual void OnEnd() { }
        /// <summary>进入游戏前验证场景引用与参数，不启动目标。</summary>
        public virtual void ValidateObjective() { }
    }
}
