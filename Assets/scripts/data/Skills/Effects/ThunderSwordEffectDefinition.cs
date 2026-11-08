using UnityEngine;

namespace NarutoVR.SealCombat
{
    [CreateAssetMenu(menuName = "NarutoVR/技能效果/雷光剑")]
    public sealed class ThunderSwordEffectDefinition : SkillEffectDefinition
    {
        [Header("法宝与特效（沿本地 +Z 握持、发波）")]
        public GameObject swordPrefab;
        public GameObject wavePrefab;
        public GameObject lightningPrefab;
        [InspectorName("腕部握柄偏移（米）")] public Vector3 gripOffset;
        [InspectorName("腕部握柄转角")] public Vector3 gripEuler;

        [Header("有效挥动")]
        [InspectorName("最短剑尖位移（米）"), Min(.01f)] public float swingDistance = .14f;
        [InspectorName("最低剑尖速度（米/秒）"), Min(.01f)] public float minSwingSpeed = .75f;
        [InspectorName("最短出波间隔（秒）"), Min(.05f)] public float waveInterval = .45f;
        [InspectorName("近战有效窗口（秒）"), Min(.01f)] public float contactWindow = .12f;

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

        public override string Summary => "右手挥剑，沿头向发出雷光；波命中敌人时可追加落雷";

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
            if (!InRange(waveSpeed, .1f, 200f) || !InRange(waveLifetime, .1f, 30f) ||
                !InRange(waveRadius, .005f, 2f)) return "剑波飞行参数无效";
            if (!InRange(directDamage, 0, 10000) || !InRange(lightningDamage, 0, 10000) ||
                !InRange(directPoise, 0, 100000) || !InRange(lightningPoise, 0, 100000) ||
                !InRange(lightningCooldown, .05f, 600f)) return "伤害、削韧或落雷冷却无效";
            return null;
        }

        static bool InRange(float value, float min, float max) => value >= min && value <= max;

        public override bool TryActivate(SkillEffects executor, SkillDefinition skill) =>
            executor && executor.TryEquipThunderSword(this, skill);
    }
}
