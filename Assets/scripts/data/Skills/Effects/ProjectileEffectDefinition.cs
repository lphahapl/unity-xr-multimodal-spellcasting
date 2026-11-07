using UnityEngine;

namespace NarutoVR.SealCombat
{
    /// <summary>手持投射物的共享配置，激活时由执行器复制为本发投射物的参数快照。</summary>
    [CreateAssetMenu(menuName = "NarutoVR/技能效果/手持投射物")]
    public sealed class ProjectileEffectDefinition : SkillEffectDefinition
    {
        [Header("一、飞行与伤害")]
        [InspectorName("碰撞半径（米）"), Tooltip("视觉按两倍半径缩放；纯视觉粒子不负责伤害。"), Min(.01f)] public float radius = .065f;
        [InspectorName("命中伤害"), Tooltip("每个投射物只结算一次。"), Min(0)] public float damage = 25;
        [InspectorName("基础削韧值"), Tooltip("对敌人的削韧；弱点倍率由命中区域配置，不改变玩家技能消耗。"), Min(0)] public float poiseDamage = 20;
        [InspectorName("飞行速度（米/秒）"), Min(.1f)] public float speed = 9;
        [InspectorName("飞行寿命（秒）"), Min(.1f)] public float flightLifetime = 4;
        [Header("二、持握与生成")]
        [InspectorName("释放方式")] public ProjectileReleaseMode releaseMode;
        [InspectorName("投掷参数")] public HandThrowSettings throwing = new();
        [InspectorName("投掷模式骨骼局部偏移（米）"), Tooltip("相对于手腕骨骼旋转；零表示精确跟随腕关节。")]
        public Vector3 throwLocalOffset = new(0, .025f, .065f);
        [InspectorName("未发射储存超时（秒）"), Tooltip("生成后开始计时；超时消散。独立于技能效果持续时间和发射后的飞行寿命。每个执行器最多储存一发。"), Min(.1f)] public float heldLifetime = 60;
        [InspectorName("持握偏移（头显朝向坐标）")] public Vector3 heldOffset = new(0, .05f, .08f);
        [InspectorName("生成及追踪恢复准备时间（秒）"), Min(.25f)] public float armDelay = .25f;
        [InspectorName("追踪丢失消散时间（秒）"), Min(.05f)] public float trackingLossGrace = .5f;
        [Header("三、轻推发射")]
        [InspectorName("前推最小位移（米）"), Min(.05f)] public float pushDistance = .12f;
        [InspectorName("前推最短时长（秒）"), Min(.05f)] public float pushMinimumSeconds = .15f;
        [InspectorName("前推最长时长（秒）"), Min(.1f)] public float pushMaximumSeconds = .6f;
        [Header("四、纯视觉资源")]
        [InspectorName("持握与飞行预置体"), Tooltip("根节点约一单位直径；留空使用基础球体。")]
        public GameObject visualPrefab;
        [InspectorName("命中爆散预置体"), Tooltip("短生命周期表现，不负责再次造成伤害。")]
        public GameObject impactPrefab;
        [InspectorName("基础球体材质"), Tooltip("没有视觉预置体时使用；留空自动创建基础材质。")]
        public Material material;
        public override string Summary => $"手持投射物 · {damage:0} 伤害 / {speed:0.#} 米每秒";
        public override string ValidateDefinition()
        {
            if (!AtLeast(radius,.01f) || !AtLeast(damage,0) || !AtLeast(poiseDamage,0) || poiseDamage > 100000 || !AtLeast(speed,.1f) || !AtLeast(flightLifetime,.1f)) return "投射物半径、伤害、削韧、速度或飞行寿命无效";
            if (!AtLeast(heldLifetime,.1f) || !AtLeast(armDelay,.25f) || !AtLeast(trackingLossGrace,.05f)) return "持握时间、准备时间或追踪丢失宽限无效";
            if (!float.IsFinite(heldOffset.x) || !float.IsFinite(heldOffset.y) || !float.IsFinite(heldOffset.z)) return "持握偏移包含非有限数值";
            if (!AtLeast(pushDistance,.05f) || !AtLeast(pushMinimumSeconds,.05f) || !AtLeast(pushMaximumSeconds,.1f) || pushMaximumSeconds < pushMinimumSeconds) return "轻推距离或时间区间无效";
            if (!System.Enum.IsDefined(typeof(ProjectileReleaseMode), releaseMode)) return "未知释放方式";
            if (releaseMode == ProjectileReleaseMode.GripThrow && (throwing == null || !throwing.Valid ||
                !float.IsFinite(throwLocalOffset.x) || !float.IsFinite(throwLocalOffset.y) || !float.IsFinite(throwLocalOffset.z))) return "投掷参数或骨骼偏移无效";
            return null;
        }
        private static bool AtLeast(float value,float minimum) => float.IsFinite(value) && value >= minimum;
        public override bool TryActivate(SkillEffects executor, SkillDefinition skill) => executor && executor.TryCreateProjectile(this, skill);
    }
}
