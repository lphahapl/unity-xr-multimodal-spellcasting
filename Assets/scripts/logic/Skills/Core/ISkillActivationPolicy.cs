namespace NarutoVR.SealCombat
{
    /// <summary>
    /// 在 SkillTestController.Start 之前安装，由 Unity 主线程同步调用。
    /// 实现类负责玩法层准入，包括资源、冷却和输入占用，
    /// 并负责调用效果执行器；返回 false 表示按该玩法的规则拒绝本次激活。
    /// </summary>
    public interface ISkillActivationPolicy
    {
        bool TryActivate(SkillDefinition skill);
        string ActivationStatus { get; }
        bool OwnsCooldowns { get; }
    }
}
