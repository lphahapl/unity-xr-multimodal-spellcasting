using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace NarutoVR.SealCombat.Rpg
{
    /// <summary>训练靶配置与状态汇总。靶的生命、自动复位属于训练设施，不写入通用关卡阶段资产。</summary>
    [DisallowMultipleComponent]
    public sealed class TrainingTargetGroup : MonoBehaviour
    {
        /// <summary>本组实际训练靶。</summary>
        public SkillTrainingTarget[] targets = Array.Empty<SkillTrainingTarget>();
        [Serializable]
        public struct StageHealth { public RpgStageDefinition stage; public float health; }
        public StageHealth[] stageHealth = Array.Empty<StageHealth>();
        [SerializeField, HideInInspector, UnityEngine.Serialization.FormerlySerializedAs("healthByStage")]
        float[] legacyHealth = Array.Empty<float>();
        Dictionary<string, StageHealth> byId;
        /// <summary>靶被击破后自动复位的秒数；零表示等阶段进入时复位。</summary>
        [Min(0)] public float resetAfterSeconds;
        /// <summary>当前击破数量，不推进关卡。</summary>
        public int DefeatedCount => targets.Count(t => t && t.IsDefeated);
        /// <summary>至少存在一个有效训练靶，且全部击破。</summary>
        public bool AllDefeated => targets.Length > 0 && targets.All(t => t && t.IsDefeated);

        /// <summary>按指定阶段重建训练靶，读取训练设施自身的生命与复位配置。</summary>
        public void ResetForStage(string stageId)
        {
            foreach (var target in targets)
            {
                target.health = byId[stageId].health; target.resetAfterSeconds = resetAfterSeconds; target.ResetTarget();
            }
        }
        /// <summary>调试击破本组靶；调用方负责确认当前会话允许行动。</summary>
        public void DebugDefeatAll() { foreach (var target in targets) if (target) target.ApplyHit(10000); }
        /// <summary>校验有效靶、阶段数量和有限生命值。</summary>
        public void Validate(LinearRpgController owner)
        {
            var rows = stageHealth;
            if (rows.Length == 0 && legacyHealth.Length == owner.StageCount)
                rows = owner.stages.Select((stage, i) => new StageHealth { stage = stage, health = legacyHealth[i] }).ToArray();
            byId = RpgChapterFlow.BindStages(owner, rows, row => row.stage, "训练靶");
            foreach (var row in rows)
                if (!float.IsFinite(row.health) || row.health <= 0)
                    throw new InvalidOperationException("训练靶生命无效：" + row.stage.stageId);
            if (targets == null || targets.Length == 0 || targets.Any(t => !t) || !float.IsFinite(resetAfterSeconds) || resetAfterSeconds < 0)
                throw new InvalidOperationException("训练靶组引用或复位时间无效");
        }
    }
}
