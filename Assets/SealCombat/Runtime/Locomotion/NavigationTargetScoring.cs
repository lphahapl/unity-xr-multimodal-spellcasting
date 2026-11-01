using System;
using UnityEngine;

namespace NarutoVR.SealCombat.Rpg
{
    [Serializable]
    public sealed class NavigationScoringSettings
    {
        [Header("选点分项权重（有效项自动归一）")]
        [InspectorName("四方向意图权重"), Min(0)] public float directionWeight = .45f;
        [InspectorName("左食指射线权重"), Min(0)] public float handRayWeight = .30f;
        [InspectorName("近距离偏好权重"), Min(0)] public float distanceWeight = .15f;
        [InspectorName("头部朝向与视线权重"), Min(0)] public float headIntentWeight = .10f;
        [Header("射线软选择（不需要击中节点碰撞体）")]
        [InspectorName("手指射线容差半径（米）"), Min(.05f)] public float handRayRadius = .35f;
        [InspectorName("头部视线容差半径（米）"), Min(.05f)] public float headRayRadius = .8f;
        [InspectorName("距离衰减尺度（米）"), Min(.1f)] public float distanceScale = 3;
        [Header("选点防抖")]
        [InspectorName("换点所需分数优势"), Range(0, 1)] public float switchAdvantage = .12f;
        [InspectorName("换点优势保持秒数"), Min(.02f)] public float switchSeconds = .12f;

        public bool Valid => Weight(directionWeight) && Weight(handRayWeight) && Weight(distanceWeight) && Weight(headIntentWeight) &&
            float.IsFinite(directionWeight + handRayWeight + distanceWeight + headIntentWeight) &&
            directionWeight + distanceWeight > 0 && Positive(handRayRadius) && Positive(headRayRadius) && Positive(distanceScale) &&
            float.IsFinite(switchAdvantage) && switchAdvantage >= 0 && switchAdvantage <= 1 && Positive(switchSeconds);
        static bool Weight(float value) => float.IsFinite(value) && value >= 0;
        static bool Positive(float value) => float.IsFinite(value) && value > 0;
    }

    public struct NavigationScore
    {
        public float total, direction, handRay, distance, headIntent;
        public bool handRayUsed, headIntentUsed;
        public string Describe() => $"评分 {total:F2}｜方向 {direction:F2}｜手指 {(handRayUsed ? handRay.ToString("F2") : "无有效射线")}｜距离 {distance:F2}｜头向 {(headIntentUsed ? headIntent.ToString("F2") : "后退不计")}";
    }

    /// <summary>Soft scores only. Registration, reachability, floors and occupancy remain mandatory controller checks.</summary>
    public static class NavigationTargetScoring
    {
        public static NavigationScore Evaluate(NavigationScoringSettings settings, Vector3 playerFloor, Vector3 target,
            Vector3 desiredDirection, Vector3 headPosition, Vector3 headForward,
            bool handRayValid, Vector3 handRayOrigin, Vector3 handRayDirection, bool ignoreHeadIntent = false)
        {
            if (settings == null || !settings.Valid) throw new ArgumentException("导航评分参数无效");
            Vector3 delta = target - playerFloor; delta.y = 0;
            float distance = delta.magnitude;
            if (!RpgGestureState.Finite(delta) || !float.IsFinite(distance) || distance < .001f || !RpgGestureState.Finite(desiredDirection)) return default;
            Vector3 desired = RpgGestureState.HorizontalForward(desiredDirection, Vector3.forward);
            Vector3 heading = RpgGestureState.HorizontalForward(headForward, desired);
            var result = new NavigationScore
            {
                direction = Mathf.Clamp01(Vector3.Dot(delta / distance, desired)),
                distance = 1f / (1f + distance / settings.distanceScale),
                handRayUsed = handRayValid && ValidRay(handRayOrigin, handRayDirection),
                // Backward movement must work while looking at an enemy in front.
                headIntentUsed = !ignoreHeadIntent && ValidRay(headPosition, headForward) && Vector3.Dot(desired, heading) >= -.01f
            };
            if (result.handRayUsed) result.handRay = RayScore(target, handRayOrigin, handRayDirection, settings.handRayRadius);
            if (result.headIntentUsed)
                result.headIntent = .5f * Mathf.Clamp01(Vector3.Dot(delta / distance, heading)) +
                    .5f * RayScore(target, headPosition, headForward, settings.headRayRadius);
            float weight = settings.directionWeight + settings.distanceWeight;
            float sum = result.direction * settings.directionWeight + result.distance * settings.distanceWeight;
            if (result.handRayUsed) { weight += settings.handRayWeight; sum += result.handRay * settings.handRayWeight; }
            if (result.headIntentUsed) { weight += settings.headIntentWeight; sum += result.headIntent * settings.headIntentWeight; }
            result.total = Mathf.Clamp01(sum / weight);
            return result;
        }

        /// <summary>Ray mode has a hard hit gate; gaze/distance never select a missed node.</summary>
        public static bool TryEvaluateRay(Vector3 target, Vector3 origin, Vector3 direction, float radius,
            out NavigationScore score)
        {
            score = default;
            if (!ValidRay(origin, direction) || !RpgGestureState.Finite(target) || !float.IsFinite(radius) || radius <= 0) return false;
            Vector3 delta = target - origin, unit = direction.normalized;
            float along = Vector3.Dot(delta, unit);
            float miss = (delta - along * unit).magnitude;
            if (along <= 0 || !float.IsFinite(miss) || miss > radius) return false;
            score.handRayUsed = true;
            score.handRay = 1 - miss / radius;
            score.distance = 1 / (1 + along);
            score.total = .98f * score.handRay + .02f * score.distance;
            return true;
        }

        static bool ValidRay(Vector3 origin, Vector3 direction) => RpgGestureState.Finite(origin) &&
            RpgGestureState.Finite(direction) && direction.sqrMagnitude > .0001f;

        static float RayScore(Vector3 target, Vector3 origin, Vector3 direction, float radius)
        {
            Vector3 unit = direction.normalized, delta = target - origin;
            float along = Vector3.Dot(delta, unit);
            if (along <= 0) return 0;
            float missSquared = (delta - unit * along).sqrMagnitude;
            return Mathf.Exp(-.5f * missSquared / (radius * radius));
        }
    }
}
