namespace NarutoVR.SealCombat
{
    /// <summary>
    /// 在 SkillInputController.Start 之前安装，由 Unity 主线程同步调用。
    /// 实现类负责玩法层准入，包括资源、冷却和输入占用，
    /// 并负责调用效果执行器；回执携带稳定的结果码和此次请求的上下文。
    /// </summary>
    public interface ISkillActivationPolicy
    {
        SkillCastResult TryActivate(SkillDefinition skill);
        string ActivationStatus { get; }
    }
}
