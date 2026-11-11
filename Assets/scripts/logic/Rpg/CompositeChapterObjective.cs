using System;
using System.Collections.Generic;
using UnityEngine;

namespace NarutoVR.SealCombat.Rpg
{
    public enum ChapterObjectiveMatch { All, Any }

    /// <summary>组合负责子目标的监听期。每次读取都使用各子目标当前的事实。</summary>
    public sealed class CompositeChapterObjective : ChapterObjective
    {
        [InspectorName("满足方式（全部 / 任一）")]
        public ChapterObjectiveMatch match;
        [InspectorName("子目标")]
        public ChapterObjective[] children = Array.Empty<ChapterObjective>();
        ChapterObjective[] boundChildren = Array.Empty<ChapterObjective>();
        int startedChildren;

        public override bool IsComplete
        {
            get
            {
                if (!IsRunning || startedChildren == 0 || startedChildren != boundChildren.Length) return false;
                foreach (var child in boundChildren)
                {
                    bool met = child && child.IsRunning && child.IsComplete;
                    if (match == ChapterObjectiveMatch.All && !met) return false;
                    if (match == ChapterObjectiveMatch.Any && met) return true;
                }
                return match == ChapterObjectiveMatch.All;
            }
        }

        protected override void OnBegin()
        {
            ValidateObjective();
            boundChildren = (ChapterObjective[])children.Clone();
            foreach (var child in boundChildren)
            {
                child.StateChanged += ChildChanged;
                try { child.BeginOwned(Owner, this); }
                catch { child.StateChanged -= ChildChanged; throw; }
                startedChildren++;
            }
        }

        void ChildChanged(ChapterObjective child)
        {
            if (startedChildren == boundChildren.Length) NotifyStateChanged();
        }

        protected override void OnEnd()
        {
            while (startedChildren > 0)
            {
                var child = boundChildren[--startedChildren];
                if (!child) continue;
                child.StateChanged -= ChildChanged;
                child.EndOwned(this);
            }
            boundChildren = Array.Empty<ChapterObjective>();
        }

        public override void ValidateObjective() => ValidateTree(this, new HashSet<ChapterObjective>());
        internal void ValidateChildren(HashSet<ChapterObjective> used)
        {
            if (children == null || children.Length == 0 || !Enum.IsDefined(typeof(ChapterObjectiveMatch), match))
                throw new InvalidOperationException("组合目标缺少子目标或匹配方式无效：" + name);
            foreach (var child in children) ValidateTree(child, used);
        }
    }
}
