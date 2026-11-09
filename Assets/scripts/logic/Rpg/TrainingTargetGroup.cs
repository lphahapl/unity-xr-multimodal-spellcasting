using System;
using System.Linq;
using UnityEngine;

namespace NarutoVR.SealCombat.Rpg
{
    /// <summary>训练靶配置与状态汇总。靶的生命、自动复位属于训练设施，不写入通用关卡阶段资产。</summary>
    [DisallowMultipleComponent]
    public sealed class TrainingTargetGroup : MonoBehaviour
    {
        /// <summary>本组实际训练靶。</summary>
        public SkillTrainingTarget[] targets = Array.Empty<SkillTrainingTarget>();
        /// <summary>各阶段重置时使用的靶生命，顺序与关卡阶段一一对应。</summary>
        public float[] healthByStage = Array.Empty<float>();
        /// <summary>靶被击破后自动复位的秒数；零表示等阶段进入时复位。</summary>
        [Min(0)] public float resetAfterSeconds;
        /// <summary>当前击破数量，不推进关卡。</summary>
        public int DefeatedCount => targets.Count(t => t && t.IsDefeated);
        /// <summary>至少存在一个有效训练靶，且全部击破。</summary>
        public bool AllDefeated => targets.Length > 0 && targets.All(t => t && t.IsDefeated);

        /// <summary>按指定阶段重建训练靶，读取训练设施自身的生命与复位配置。</summary>
        public void ResetForStage(int index)
        {
            foreach (var target in targets)
            {
                target.health = healthByStage[index]; target.resetAfterSeconds = resetAfterSeconds; target.ResetTarget();
            }
        }
        /// <summary>调试击破本组靶；调用方负责确认当前会话允许行动。</summary>
        public void DebugDefeatAll() { foreach (var target in targets) if (target) target.ApplyHit(10000); }
        /// <summary>校验有效靶、阶段数量和有限生命值。</summary>
        public void Validate(int stageCount)
        {
            if (targets == null || targets.Length == 0 || targets.Any(t => !t) || healthByStage == null || healthByStage.Length != stageCount ||
                healthByStage.Any(h => !float.IsFinite(h) || h <= 0) || !float.IsFinite(resetAfterSeconds) || resetAfterSeconds < 0)
                throw new InvalidOperationException("训练靶组引用或阶段生命配置无效");
        }
    }
}
