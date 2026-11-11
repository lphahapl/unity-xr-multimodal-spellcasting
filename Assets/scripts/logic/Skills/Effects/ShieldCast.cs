using System;
using UnityEngine;

namespace NarutoVR.SealCombat
{
    public sealed partial class SkillController
    {

        float lastShieldCapacity;

        sealed class ShieldCast
        {
            public readonly string DisplayName;
            public readonly float Capacity, Duration;
            public readonly GameObject Visual;
            public readonly Material Material;
            public bool Refreshing { get; private set; }

            public ShieldCast(ShieldEffectDefinition definition, SkillDefinition skill)
            {
                DisplayName = skill.displayName;
                Capacity = definition.capacity;
                Duration = float.IsFinite(skill.durationSeconds) ? Mathf.Max(.1f, skill.durationSeconds) : 10;
                Visual = definition.visualPrefab;
                Material = definition.material;
            }

            public bool Arm(SkillController owner)
            {
                Refreshing = owner.BuffRemaining > 0;
                owner.Vitals.SetShield(Capacity, owner.Now + Duration);
                return true;
            }
        }

        PreparedSkillCast PrepareShield(ShieldEffectDefinition definition, SkillDefinition skill)
        {
            var cast = new ShieldCast(definition, skill);
            return new PreparedSkillCast(() => cast.Arm(this), () => PresentShield(cast), null);
        }

        /// <summary>护盾配置的执行入口，设置容量与到期时间；成功次数由外层 TryCast 统一统计。</summary>
        void PresentShield(ShieldCast cast)
        {
            GameObject visual = cast.Visual;
            Material material = cast.Material;
            lastShieldCapacity = cast.Capacity;
            if (activeShieldPrefab != visual || activeShieldMaterial != material)
            {
                FreePrepared(buffObject);
                buffObject = null;
            }
            activeShieldPrefab = visual; activeShieldMaterial = material;
            // 护盾数值已经生效后，纯视觉创建失败只记录告警。
            try
            {
                CreateBuffVisual();
                PlayShieldPulse(ShieldPulseKind.Cast, 1);
            }
            catch (Exception error) { Debug.LogWarning("护盾已生效，但视觉创建失败：" + error.Message, this); }
            Report($"{cast.DisplayName}：{(cast.Refreshing ? "已刷新" : "已获得")}护盾 {shieldPoints:0}，持续 {cast.Duration:0.#} 秒");

        }
    }
}
