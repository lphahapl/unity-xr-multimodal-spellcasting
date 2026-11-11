using System;
using UnityEngine;

namespace NarutoVR.SealCombat.Rpg
{
    /// <summary>到达区域目标。使用脚底坐标和盒体局部空间，支持旋转区域，无需物理触发器。</summary>
    public sealed class ReachAreaChapterObjective : ChapterObjective
    {
        /// <summary>到达判定的盒体区域；玩家脚底需位于其局部边界内。</summary>
        public BoxCollider area;
        /// <summary>实时计算到达状态，区域禁用、追踪控制不能行动时不满足。</summary>
        public override bool IsComplete => IsRunning && Owner.CanAct && Owner.locomotion &&
            ContainsFloor(area, Owner.locomotion.PlayerFloorPosition);

        internal static bool ContainsFloor(BoxCollider area, Vector3 floorPosition)
        {
            if (!area || !area.enabled || !area.gameObject.activeInHierarchy) return false;
            var local = area.transform.InverseTransformPoint(floorPosition) - area.center;
            var half = area.size * .5f;
            return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y && Mathf.Abs(local.z) <= half.z;
        }
        bool previouslyInside;
        protected override void OnBegin() => previouslyInside = IsComplete;
        void Update()
        {
            if (!IsRunning) return;
            bool inside = IsComplete;
            if (inside == previouslyInside) return;
            previouslyInside = inside;
            NotifyStateChanged();
        }
        /// <summary>区域引用必须在进入游戏前配置。</summary>
        public override void ValidateObjective()
        {
            if (!area) throw new InvalidOperationException("到达区域目标缺少 BoxCollider");
        }
    }
}
