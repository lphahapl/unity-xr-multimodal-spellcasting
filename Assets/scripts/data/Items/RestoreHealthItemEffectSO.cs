using NarutoVR.SealCombat.Rpg;
using UnityEngine;

namespace NarutoVR.SealCombat.Items
{
    [CreateAssetMenu(menuName = "考工律度/道具/恢复气血")]
    public sealed class RestoreHealthItemEffectSO : GameItemEffectSO
    {
        [Min(0)] public float amount = 15;
        public override bool CanApply(RpgPlayerState p) => p != null && p.Alive && !p.CastInProgress && amount > 0 && p.Health < p.MaxHealth;
        public override void Apply(RpgPlayerState p) => p.Heal(Mathf.Min(amount, p.MaxHealth));
    }
}
