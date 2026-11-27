using UnityEngine;

namespace NarutoVR.SealCombat.Rpg
{
    [CreateAssetMenu(menuName = "考工律度/章节/断桥通路符教学")]
    public sealed class Chapter02PassageStage : RpgStageDefinition
    {
        public GlyphTemplate passage;
        public GameObject socketPrefab;
        [Tooltip("相对于修复桥面的两个实际走面接点。")]
        public Vector3 nearLanding, farLanding;
        public NarrationLine drawingCue, aimingCue;
    }
}
