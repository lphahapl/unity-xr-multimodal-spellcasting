namespace NarutoVR.SealCombat.Events
{
    /// <summary>操作开始时捕获的台词实例。接收方可验证控制器、播放轮次和具体台词。</summary>
    public readonly struct NarrationOperationNotice
    {
        public NarrationController.Step Step { get; }
        public NarrationController Target => Step?.Target;
        public NarrationOperationNotice(NarrationController.Step step) { Step = step; }
    }

    public static class NarrationEvents
    {
        public static readonly EventKey<NarrationOperationNotice> OperationCompleted = new("旁白等待的操作完成");
        /// <summary>在 Unity 主线程递交操作启动时保存的 Step，停止或重启后的旧通知会被拒绝。</summary>
        public static void PublishOperation(NarrationController.Step step)
        {
            if (step == null || !step.Target || !step.Target.CanCompleteOperation(step)) return;
            EventCenter.Publish(OperationCompleted, new NarrationOperationNotice(step));
        }
    }
}
