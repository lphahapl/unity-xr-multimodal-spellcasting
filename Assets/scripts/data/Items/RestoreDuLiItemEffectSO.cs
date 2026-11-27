using NarutoVR.SealCombat.Rpg;
using UnityEngine;

namespace NarutoVR.SealCombat.Items
{
    [CreateAssetMenu(menuName = "考工律度/道具/恢复度力")]
    public sealed class RestoreDuLiItemEffectSO : GameItemEffectSO
    {
        [Min(0)] public float amount = 20;
        public override bool CanApply(RpgPlayerState p) => p != null && p.Alive && !p.CastInProgress && amount > 0 && p.Mana < p.MaxMana;
        public override void Apply(RpgPlayerState p) => p.RestoreMana(Mathf.Min(amount, p.MaxMana));
    }
}
