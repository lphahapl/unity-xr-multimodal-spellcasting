using UnityEngine;

namespace NarutoVR.SealCombat.Rpg
{
    /// <summary>
    /// 接收场景机关或对话完成信号的目标，可在 UnityEvent 中绑定 Complete。
    /// 只接受本目标运行期间的信号，进入或恢复阶段会重置，不把旧完成状态带入新一轮。
    /// </summary>
    public sealed class SignalChapterObjective : ChapterObjective
    {
        /// <summary>只属于当前运行轮次的完成标记，不写入存档。</summary>
        bool completed;
        /// <summary>组件运行且收到本轮成功信号时，向章节提供完成状态。</summary>
        public override bool IsComplete => IsRunning && completed;
        /// <summary>新一轮目标清空上次信号。</summary>
        protected override void OnBegin() => completed = false;
        /// <summary>离开目标时清空信号，避免下一阶段继承完成状态。</summary>
        protected override void OnEnd() => completed = false;
        /// <summary>实际机关成功后调用；目标未运行或玩家不能行动时忽略信号。</summary>
        public void Complete() { if (IsRunning && Owner.CanAct) completed = true; }
    }
}
