using System;
using UnityEngine;

namespace NarutoVR.SealCombat.Rpg
{
    /// <summary>读取已领取该阶段奖励的存档事实。开始和结束监听不会更改玩家进度。</summary>
    public sealed class CompletedStageChapterObjective : ChapterObjective
    {
        [InspectorName("需要已完成的阶段")] public RpgStageDefinition stage;
        public override bool IsComplete => IsRunning && stage && Owner.Model != null &&
            Owner.Model.Chapter.IsComplete(stage.stageId);

        int observedRevision;
        bool previouslyCompleted;
        protected override void OnBegin()
        {
            observedRevision = Owner.ProgressRevision;
            previouslyCompleted = IsComplete;
        }
        void Update()
        {
            if (!IsRunning || observedRevision == Owner.ProgressRevision) return;
            observedRevision = Owner.ProgressRevision;
            bool completed = IsComplete;
            if (completed == previouslyCompleted) return;
            previouslyCompleted = completed;
            NotifyStateChanged();
        }

        public override void ValidateObjective()
        {
            if (!stage || string.IsNullOrWhiteSpace(stage.stageId))
                throw new InvalidOperationException("已完成阶段目标需要有效的阶段配置：" + name);
        }
    }
}
