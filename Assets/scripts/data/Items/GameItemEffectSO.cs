using NarutoVR.SealCombat.Rpg;
using UnityEngine;

namespace NarutoVR.SealCombat.Items
{
    public abstract class GameItemEffectSO : ScriptableObject
    {
        public abstract bool CanApply(RpgPlayerState player);
        public abstract void Apply(RpgPlayerState player);
    }
}
