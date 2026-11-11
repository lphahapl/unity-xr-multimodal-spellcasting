using System;
using UnityEngine;

namespace NarutoVR.SealCombat.Rpg
{
    /// <summary>由遭遇负责保存本轮清房状态，目标只报告该事实。</summary>
    public sealed class ClearedEncounterChapterObjective : ChapterObjective
    {
        [InspectorName("需要清空的房间遭遇")] public CombatRoomEncounter encounter;
        public override bool IsComplete => IsRunning && encounter && encounter.IsComplete;
        protected override void OnBegin() => encounter.CompletionChanged += NotifyStateChanged;
        protected override void OnEnd() { if (encounter) encounter.CompletionChanged -= NotifyStateChanged; }
        public override void ValidateObjective()
        {
            if (!encounter) throw new InvalidOperationException("清房目标缺少遭遇组件：" + name);
        }
    }
}
