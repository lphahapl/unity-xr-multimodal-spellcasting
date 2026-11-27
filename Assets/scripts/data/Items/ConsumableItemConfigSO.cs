using System;
using UnityEngine;

namespace NarutoVR.SealCombat.Items
{
    [CreateAssetMenu(menuName = "考工律度/道具/吃喝配置")]
    public sealed class ConsumableItemConfigSO : GameItemConfigSO
    {
        [Min(1)] public int maxUses = 3;
        [Range(.3f, .5f)] public float dwellSeconds = .4f;
        public bool requireFacing = true;
        [Range(0, 180)] public float maxFacingAngle = 65f;
        public GameItemEffectSO[] effects = Array.Empty<GameItemEffectSO>();
        public AudioClip biteSound;
        [Range(0, 1)] public float volume = .7f;
        [Range(0, 1)] public float hapticStrength = .2f;
        [Range(.01f, .3f)] public float hapticSeconds = .04f;
    }
}
