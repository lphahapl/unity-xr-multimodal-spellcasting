namespace NarutoVR.SealCombat.Events
{
    /// <summary>通知哪一个对话的哪一步已完成。步骤 ID 是句子在列表中的下标。</summary>
    public readonly struct NarrationOperationNotice
    {
        public NarrationController Target { get; }
        public int StepId { get; }
        public NarrationOperationNotice(NarrationController target, int stepId)
        {
            Target = target;
            StepId = stepId;
        }
    }

    public static class NarrationEvents
    {
        public static readonly EventKey<NarrationOperationNotice> OperationCompleted = new("旁白等待的操作完成");
        /// <summary>在 Unity 主线程上发布。目标必填，ID 从 target.CurrentStep.Id 获取。</summary>
        public static void PublishOperation(NarrationController target, int stepId)
        {
            if (!target || stepId < 0) return;
            EventCenter.Publish(OperationCompleted, new NarrationOperationNotice(target, stepId));
        }
    }
}
