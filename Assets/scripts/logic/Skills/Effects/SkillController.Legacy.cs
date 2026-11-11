using System.Collections.Generic;
using UnityEngine;

namespace NarutoVR.SealCombat
{
    public sealed partial class SkillController
    {
        [Header("二、旧场景兼容材质（技能绑定效果资产时不使用）")]
        [InspectorName("火球材质")] public Material fireMaterial;
        [InspectorName("护盾材质")] public Material buffMaterial;
        [Header("二（补充）、旧场景兼容预置体")]
        [InspectorName("火球视觉预置体")] public GameObject fireballPrefab;
        [InspectorName("护盾视觉预置体")] public GameObject shieldPrefab;
        [InspectorName("命中爆散预置体")] public GameObject impactPrefab;
        [Header("三、旧场景兼容护盾数值")]
        [InspectorName("护盾上限"), Min(1)] public float shieldCapacity = 100;
        [Header("四、旧场景兼容持握与轻推参数")]
        [InspectorName("火球半径（米）"), Min(0.01f)] public float ballRadius = 0.065f;
        [InspectorName("持握偏移（头显朝向坐标）")] public Vector3 heldOffset = new(0, 0.05f, 0.08f);
        [InspectorName("未发射储存超时（秒，旧配置）"), Min(0.1f)] public float heldLifetime = 60;
        [InspectorName("生成和追踪恢复后的准备时间（秒）"), Min(0.25f)] public float armDelay = 0.25f;
        [HideInInspector] public float trackingLossGrace = .5f; // Legacy serialization only; loss no longer cancels.
        [InspectorName("前推最小位移（米）"), Min(0.05f)] public float pushDistance = 0.12f;
        [InspectorName("前推最短时长（秒）"), Min(0.05f)] public float pushMinimumSeconds = 0.15f;
        [InspectorName("前推最长时长（秒）"), Min(0.1f)] public float pushMaximumSeconds = 0.6f;
        [Header("五、飞行与碰撞")]
        [InspectorName("飞行速度（米/秒）"), Min(0.1f)] public float projectileSpeed = 9;
        [InspectorName("火球命中伤害"), Min(0)] public float projectileDamage = 25;
        [InspectorName("飞行寿命（秒）"), Min(0.1f)] public float projectileLifetime = 4;

        // 旧试验场景生成器仅保存 effectKind 和场景字段。转换后的配置由此玩家拥有。
        ProjectileEffectDefinition legacyProjectile;
        ShieldEffectDefinition legacyShield;
        readonly Dictionary<SkillDefinition,SkillEffectDefinition> legacyBindings = new();

        void BindLegacyDefinitions(IReadOnlyList<SkillDefinition> skills)
        {
            legacyBindings.Clear();
            foreach (var skill in skills)
                if (skill && !skill.effectDefinition) legacyBindings[skill] = LegacyEffect(skill.effectKind);
        }

        SkillEffectDefinition ResolveDefinition(SkillDefinition skill)
        {
            if (skill.effectDefinition) return skill.effectDefinition;
            // 直接注入的旧技能没有经过场景目录装配，仍可由同一个转换器解析。
            return legacyBindings.TryGetValue(skill,out var definition) ? definition : LegacyEffect(skill.effectKind);
        }

        SkillEffectDefinition LegacyEffect(SkillEffectKind kind)
        {
            if (kind == SkillEffectKind.HeldFireball)
            {
                if (!legacyProjectile) legacyProjectile = MakeLegacy<ProjectileEffectDefinition>();
                CopyLegacySettings(legacyProjectile);
                return legacyProjectile;
            }
            if (kind == SkillEffectKind.SelfBuff)
            {
                if (!legacyShield) legacyShield = MakeLegacy<ShieldEffectDefinition>();
                CopyLegacySettings(legacyShield);
                return legacyShield;
            }
            return null;
        }

        static T MakeLegacy<T>() where T : SkillEffectDefinition
        {
            var definition = ScriptableObject.CreateInstance<T>();
            definition.hideFlags = HideFlags.HideAndDontSave;
            return definition;
        }

        public void CopyLegacySettings(ProjectileEffectDefinition target)
        {
            target.radius = ballRadius; target.damage = projectileDamage; target.speed = projectileSpeed;
            target.flightLifetime = projectileLifetime; target.heldLifetime = heldLifetime;
            target.releaseMode = ProjectileReleaseMode.ForwardPush; target.heldOffset = heldOffset;
            target.armDelay = armDelay; target.trackingLossGrace = trackingLossGrace;
            target.pushDistance = pushDistance; target.pushMinimumSeconds = pushMinimumSeconds;
            target.pushMaximumSeconds = pushMaximumSeconds; target.visualPrefab = fireballPrefab;
            target.impactPrefab = impactPrefab; target.material = fireMaterial;
        }

        public void CopyLegacySettings(ShieldEffectDefinition target)
        {
            target.capacity = shieldCapacity; target.visualPrefab = shieldPrefab; target.material = buffMaterial;
        }

        void ReleaseLegacyDefinitions()
        {
            legacyBindings.Clear();
            if (Application.isPlaying)
            {
                if (legacyProjectile) Destroy(legacyProjectile);
                if (legacyShield) Destroy(legacyShield);
            }
            else
            {
                if (legacyProjectile) DestroyImmediate(legacyProjectile);
                if (legacyShield) DestroyImmediate(legacyShield);
            }
            legacyProjectile = null; legacyShield = null;
        }
    }
}
