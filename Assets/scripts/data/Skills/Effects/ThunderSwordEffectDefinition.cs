using UnityEngine;

namespace NarutoVR.SealCombat
{
    /// <summary>雷光剑的判定参数与视觉引用；瞬时特效只负责反馈，不参与伤害结算。</summary>
    [CreateAssetMenu(menuName = "NarutoVR/技能效果/雷光剑")]
    public sealed class ThunderSwordEffectDefinition : SkillEffectDefinition
    {
        [Header("法宝与特效（沿本地 +Z 握持、发波）")]
        public GameObject swordPrefab;
        public GameObject wavePrefab;
        public GameObject lightningPrefab;
        [Header("瞬时视觉（可选，不改变伤害与判定范围）")]
        [Tooltip("有效挥动时生成的弧光；沿本地 +Z 指向剑尖，+X 指向挥动方向。")]
        public GameObject slashPrefab;
        [Tooltip("剑刃命中敌人或剑波终止于表面时生成。")]
        public GameObject impactPrefab;
        [InspectorName("挥砍余光寿命（秒）"), Min(.05f)] public float slashVisualLifetime = .3f;
        [InspectorName("命中余光寿命（秒）"), Min(.05f)] public float impactVisualLifetime = .45f;
        [Tooltip("整组落雷的回收时间；主电弧和地面余波由预制体内各粒子系统分别控制。")]
        [InspectorName("落雷余波寿命（秒）"), Min(.05f)] public float lightningVisualLifetime = .16f;

        [InspectorName("掌心握柄偏移（米）")] public Vector3 gripOffset;
        [InspectorName("握柄微调转角")] public Vector3 gripEuler;
        [Header("近战刃轨（仅视觉，不触发攻击）")]
        public Material bladeTrailMaterial;
        [InspectorName("刃轨保留秒数"), Range(.05f,.3f)] public float bladeTrailLifetime = .14f;

        [Header("有效挥动")]
        [InspectorName("最短剑尖位移（米）"), Min(.01f)] public float swingDistance = .40f;
        [InspectorName("最低剑尖速度（米/秒）"), Min(.01f)] public float minSwingSpeed = 1.8f;
        [InspectorName("最短横竖手掌位移（米）"), Min(.01f)] public float minimumHandTravel = .16f;
        [InspectorName("最短出波间隔（秒）"), Min(.05f)] public float waveInterval = .85f;
        [InspectorName("近战有效窗口（秒）"), Min(.01f)] public float contactWindow = .12f;
        [InspectorName("生成及追踪恢复缓冲（秒）"), Min(.05f)] public float rearmDelay = .25f;
        [Tooltip("新方向比另一方向明显超出此比例才切换预览，避免斜挥附近横竖闪动。")]
        [InspectorName("横竖方向切换比例"), Range(1,2)] public float directionHysteresis = 1.25f;
        [InspectorName("显示发波方向提示")] public bool showWavePreview = true;

        [Header("剑刃与雷光共用同一挥的直击")]
        [InspectorName("直击伤害"), Min(0)] public float directDamage = 6f;
        [InspectorName("直击削韧"), Min(0)] public float directPoise = 2f;
        [InspectorName("剑波速度（米/秒）"), Min(.1f)] public float waveSpeed = 10f;
        [InspectorName("剑波寿命（秒）"), Min(.1f)] public float waveLifetime = 1.2f;
        [InspectorName("剑波碰撞半径（米）"), Min(.005f)] public float waveRadius = .055f;

        [Header("波命中敌人后的独立落雷")]
        [InspectorName("落雷伤害"), Min(0)] public float lightningDamage = 7f;
        [InspectorName("落雷削韧"), Min(0)] public float lightningPoise = 2f;
        [InspectorName("施法者落雷冷却（秒）"), Min(.05f)] public float lightningCooldown = 2f;

        public override string Summary => "横挥出横波，竖挥出竖波，均朝视线瞄准点飞行；波命中敌人时可追加落雷";

        public override string ValidateDefinition()
        {
            if (!swordPrefab || !wavePrefab || !lightningPrefab) return "请指定剑、剑波与落雷预制体";
            var detector = swordPrefab.GetComponentInChildren<MeleeHitDetector>(true);
            if (detector && detector.enabled && (!detector.bladeBase || !detector.bladeTip ||
                !detector.bladeBase.IsChildOf(swordPrefab.transform) || !detector.bladeTip.IsChildOf(swordPrefab.transform) ||
                (detector.bladeTip.position - detector.bladeBase.position).sqrMagnitude < 1e-8f ||
                !float.IsFinite(detector.radius) || detector.radius < .005f))
                return "近战判定需要绑定法宝上的刃根和剑尖，并设置有效接触半径";
            if (!float.IsFinite(gripOffset.sqrMagnitude) || !float.IsFinite(gripEuler.sqrMagnitude)) return "握姿无效";
            if (!InRange(swingDistance, .01f, 2f) || !InRange(minSwingSpeed, .01f, 20f) ||
                !InRange(waveInterval, .05f, 60f) || !InRange(contactWindow, .01f, waveInterval)) return "挥动阈值或近战窗口无效";
            if (!InRange(minimumHandTravel, .01f, 1f) || !InRange(rearmDelay, .05f, 1f) ||
                !InRange(directionHysteresis, 1f, 2f)) return "挥动恢复或方向阈值无效";
            if (!InRange(waveSpeed, .1f, 200f) || !InRange(waveLifetime, .1f, 30f) ||
                !InRange(waveRadius, .005f, 2f)) return "剑波飞行参数无效";
            if (!InRange(directDamage, 0, 10000) || !InRange(lightningDamage, 0, 10000) ||
                !InRange(directPoise, 0, 100000) || !InRange(lightningPoise, 0, 100000) ||
                !InRange(lightningCooldown, .05f, 600f)) return "伤害、削韧或落雷冷却无效";
            if (!InRange(slashVisualLifetime, .05f, 5f) || !InRange(impactVisualLifetime, .05f, 5f) ||
                !InRange(lightningVisualLifetime, .05f, 5f)) return "瞬时视觉寿命无效";
            if (!InRange(bladeTrailLifetime, .05f, .3f)) return "刃轨寿命无效";
            return null;
        }

        static bool InRange(float value, float min, float max) => value >= min && value <= max;

        public override bool TryActivate(SkillEffects executor, SkillDefinition skill) =>
            executor && executor.TryEquipThunderSword(this, skill);
    }
}
