using UnityEngine;

namespace NarutoVR.SealCombat
{
    /// <summary>自身护盾配置，每次生效刷新容量，到期时间来自对应技能的持续时间。</summary>
    [CreateAssetMenu(menuName = "NarutoVR/技能效果/自身护盾")]
    public sealed class ShieldEffectDefinition : SkillEffectDefinition
    {
        [Header("一、护盾数值")]
        [InspectorName("护盾容量"), Tooltip("本技能成功时刷新到此容量，不与已有护盾相加；时长来自技能配置。"), Min(1)]
        public float capacity = 100;
        [Header("二、护盾表现")]
        [InspectorName("护盾视觉预置体"), Tooltip("纯视觉资源；留空时使用基础护腕光环。")]
        public GameObject visualPrefab;
        [InspectorName("基础光环材质"), Tooltip("没有视觉预置体时使用；留空自动创建基础材质。")]
        public Material material;
        public override string Summary => $"自身护盾 {capacity:0} 点";
        public override string ValidateDefinition() => !float.IsFinite(capacity) || capacity < 1 ? "护盾容量必须是至少1的有限数值" : null;
        public override bool TryActivate(SkillEffects executor, SkillDefinition skill) => executor && executor.TryApplyShield(this, skill);
    }
}
