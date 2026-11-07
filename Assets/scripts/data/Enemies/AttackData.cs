using System;
using UnityEngine;

namespace NarutoVR.SealCombat.Enemies
{
    public enum AttackShape { [InspectorName("前方扇形")] Sector, [InspectorName("周围圆形")] Circle }

    /// <summary>每个独立动画的一次攻击配置。水平范围以米计，不受模型缩放影响。</summary>
    [Serializable]
    public sealed class AttackData
    {
        [Header("招式与伤害")]
        [InspectorName("招式名称")] public string displayName = "挥砍";
        [InspectorName("判定形状")] public AttackShape shape;
        [InspectorName("单次伤害"), Min(0)] public float damage = 35;
        [Header("判定范围（以敌人脚下为原点）")]
        [InspectorName("半径（米）"), Min(.01f)] public float radius = 2.1f;
        [InspectorName("扇形总角度"), Range(1, 360)] public float angle = 100;
        [InspectorName("判定中心偏移（局部米）")] public Vector3 centerOffset = new(0, 1, 0);
        [InspectorName("中心上下各允许的高度（米）"), Min(0)] public float halfHeight = 1.5f;
        [Header("生效时间（动画进度，0.5 即播放到一半）")]
        [InspectorName("开始判定进度"), Range(0, .98f)] public float activeStart = .55f;
        [InspectorName("结束判定进度"), Range(0, .98f)] public float activeEnd = .7f;

        public AttackData Snapshot() => (AttackData)MemberwiseClone();

        public bool Validate(out string reason)
        {
            bool Finite(float value) => float.IsFinite(value);
            if (!Enum.IsDefined(typeof(AttackShape), shape) || !Finite(damage) || damage < 0 || damage > 10000 ||
                !Finite(radius) || radius <= 0 || !Finite(angle) || angle < 1 || angle > 360 ||
                !Finite(halfHeight) || halfHeight < 0 || !Finite(centerOffset.x) || !Finite(centerOffset.y) || !Finite(centerOffset.z) ||
                !Finite(activeStart) || !Finite(activeEnd) || activeStart < 0 || activeEnd > .98f || activeStart > activeEnd)
            { reason = "攻击范围、伤害或生效进度无效（开始须不大于结束，结束不大于 0.98）"; return false; }
            reason = null; return true;
        }

        public static Quaternion HorizontalRotation(Transform root)
        {
            Vector3 forward = Vector3.ProjectOnPlane(root.forward, Vector3.up);
            return forward.sqrMagnitude > .0001f ? Quaternion.LookRotation(forward) : Quaternion.identity;
        }

        public Vector3 Center(Transform root) => root.position + HorizontalRotation(root) * centerOffset;

        public bool Contains(Vector3 center, Vector3 forward, Vector3 point)
        {
            Vector3 delta = point - center;
            if (!float.IsFinite(delta.sqrMagnitude) || Mathf.Abs(delta.y) > halfHeight) return false;
            delta.y = forward.y = 0;
            if (delta.sqrMagnitude > radius * radius) return false;
            return shape == AttackShape.Circle || angle >= 360 || delta.sqrMagnitude < .000001f ||
                Vector3.Dot(forward.normalized, delta.normalized) >= Mathf.Cos(angle * .5f * Mathf.Deg2Rad) - .000001f;
        }
    }
}
