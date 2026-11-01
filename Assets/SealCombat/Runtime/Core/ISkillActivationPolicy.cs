namespace NarutoVR.SealCombat
{
    /// <summary>
    /// Install before SkillTestController.Start. Execute synchronously on the Unity main thread.
    /// The implementation owns game-specific admission (resources, cooldowns, input ownership)
    /// and effect execution. False must preserve the implementation's existing rejection semantics.
    /// </summary>
    public interface ISkillActivationPolicy
    {
        bool TryActivate(SkillDefinition skill);
        string ActivationStatus { get; }
        bool OwnsCooldowns { get; }
    }
}
