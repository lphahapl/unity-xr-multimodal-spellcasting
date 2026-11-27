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
        [InspectorName("开盾弹反火核窗口（秒，0 表示关闭）"), Min(0)]
        public float fireCoreParrySeconds;
        [Header("二、护盾表现")]
        [InspectorName("周身盾壳"), Tooltip("根节点带 MeshRenderer 的球壳预制体。预制体以直径 1 米制作，随玩家躯干移动。")]
        public MeshRenderer bodyPrefab;
        [InspectorName("附加腕部视觉预置体"), Tooltip("附加在左腕的表现；周身护盾独立跟随玩家。")]
        public GameObject visualPrefab;
        [InspectorName("基础光环材质"), Tooltip("没有视觉预置体时使用；留空自动创建基础材质。")]
        public Material material;
        public override string Summary => $"自身护盾 {capacity:0} 点";
        public override string ValidateDefinition() => !float.IsFinite(capacity) || capacity < 1 ? "护盾容量必须是至少1的有限数值" :
            !float.IsFinite(fireCoreParrySeconds) || fireCoreParrySeconds < 0 ? "弹反窗口须为非负有限秒数" : null;
    }
}
