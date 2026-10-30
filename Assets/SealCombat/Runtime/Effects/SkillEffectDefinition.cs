using UnityEngine;

namespace NarutoVR.SealCombat
{
    /// <summary>Shared, read-only effect configuration. Mutable effect state belongs to the executor or a registered lifetime.</summary>
    public abstract class SkillEffectDefinition : ScriptableObject
    {
        public virtual string Summary => GetType().Name;
        public virtual string ValidateDefinition() => null;
        /// <summary>Return true only after activation succeeds. On false, leave no new effect behind.</summary>
        public abstract bool TryActivate(SkillEffects executor, SkillDefinition skill);
    }

    /// <summary>
    /// An activated custom effect's owned runtime state. Register once; the executor ticks it and calls Cancel
    /// once when it ends, is explicitly cancelled, or the player pauses/disables. Cancel must release owned resources.
    /// </summary>
    public interface ISkillEffectLifetime
    {
        bool IsAlive { get; }
        void Tick(double now);
        void Cancel();
    }
}
