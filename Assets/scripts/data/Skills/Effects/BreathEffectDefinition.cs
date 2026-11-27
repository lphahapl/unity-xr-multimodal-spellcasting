using UnityEngine;

namespace NarutoVR.SealCombat
{
    public enum BreathCastMode
    {
        [InspectorName("单次吹出")] Burst,
        [InspectorName("持续喷射")] Sustained,
        [InspectorName("掌上结印发射")] SealProjectile
    }

    [CreateAssetMenu(menuName = "NarutoVR/技能效果/吐息")]
    public sealed class BreathEffectDefinition : SkillEffectDefinition
    {
        [InspectorName("吐息场景痕迹"), Tooltip("命中实体表面时使用；留空关闭。")] public SurfaceMarkStyle surfaceMark;
        [InspectorName("释放方式")] public BreathCastMode mode;
        [InspectorName("火焰预制体（局部 +Z 向前）")] public GameObject visualPrefab;
        [InspectorName("单发命中爆散预制体"), Tooltip("选配。碰撞时在接触点播放，局部 +Z 沿表面向外。")] public GameObject impactPrefab;
        [InspectorName("成印火苗"), Tooltip("结印后显示的小火苗。飞炎在发射时切换为完整弹体。")] public GameObject pilotFlamePrefab;
        [InspectorName("嘴部挂点（相对头显，米）")] public Vector3 mouthOffset = new(0, -.09f, .08f);
        [InspectorName("特效缩放"), Min(.001f)] public float visualScale = 1;
        [InspectorName("射程（米）"), Min(.1f)] public float range = 4;
        [InspectorName("碰撞半径（米）"), Min(.001f)] public float radius = .10f;
        [InspectorName("每次命中伤害"), Min(0)] public float damage = 8;
        [InspectorName("每次削韧"), Min(0)] public float poiseDamage = 2;
        [InspectorName("单次吹出的速度（米/秒）"), Min(.1f)] public float speed = 8;
        [InspectorName("持续喷射每秒消耗度力"), Min(.01f)] public float manaPerSecond = 10;
        [InspectorName("持续伤害间隔（秒）"), Min(.03f)] public float damageInterval = .2f;
        [InspectorName("停喷最小冷却（秒）"), Min(0)] public float minCooldownSeconds = 2;
        [InspectorName("每喷一秒增加冷却（秒）"), Min(0)] public float cooldownPerSecond = 1;
        [InspectorName("停喷最大冷却（秒）"), Min(0)] public float maxCooldownSeconds = 8;

        internal void ConfigureVisual(GameObject instance)
        {
            instance.transform.localScale = Vector3.one * visualScale;
            foreach (var collider in instance.GetComponentsInChildren<Collider>(true))
                collider.enabled = false;
            foreach (var particles in instance.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = particles.main;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                if (mode == BreathCastMode.Sustained) main.simulationSpace = ParticleSystemSimulationSpace.World;
            }
        }

        public override string Summary => mode switch
        {
            BreathCastMode.Sustained => "嘴部持续喷射",
            BreathCastMode.SealProjectile => "结印蓄火、掌上瞄准发射",
            _ => "嘴部单次吹出"
        };
        public override float CooldownOnActivation(float seconds) => mode == BreathCastMode.Sustained ? 0 : seconds;

        internal float CooldownAfterSpraying(double seconds) =>
            (float)System.Math.Min(maxCooldownSeconds, minCooldownSeconds + seconds * cooldownPerSecond);

        public override string ValidateDefinition()
        {
            if (!visualPrefab) return "吐息缺少火焰预制体";
            if (mode == BreathCastMode.Sustained &&
                (!Rpg.RpgDataRules.InRange(maxCooldownSeconds, 0, Rpg.RpgDataRules.MaximumDuration) ||
                 !Rpg.RpgDataRules.InRange(minCooldownSeconds, 0, maxCooldownSeconds) ||
                 !float.IsFinite(cooldownPerSecond) || cooldownPerSecond < 0))
                return "持续喷射冷却无效：检查每秒增量以及最小和最大冷却的范围";
            if (!float.IsFinite(mouthOffset.sqrMagnitude) || !Positive(visualScale) || !Positive(range) ||
                !Positive(radius) || !Positive(damage) || !float.IsFinite(poiseDamage) || poiseDamage < 0)
                return "吐息挂点、缩放、射程、碰撞或伤害无效";
            return mode switch
            {
                BreathCastMode.Burst when Positive(speed) => null,
                BreathCastMode.SealProjectile when pilotFlamePrefab && Positive(speed) => null,
                BreathCastMode.Sustained when Positive(manaPerSecond) && Positive(damageInterval) => null,
                _ => "吐息模式、速度、持续消耗或伤害间隔无效"
            };
        }

        static bool Positive(float value) => float.IsFinite(value) && value > 0;
    }
}
