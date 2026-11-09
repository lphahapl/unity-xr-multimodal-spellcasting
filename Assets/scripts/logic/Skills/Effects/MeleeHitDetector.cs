using System;
using UnityEngine;

namespace NarutoVR.SealCombat
{
    /// <summary>按所属技能提供的挥击窗口扫掠剑刃；成功直击后通知视觉层，不自行创建特效。</summary>
    [DisallowMultipleComponent]
    public sealed class MeleeHitDetector : MonoBehaviour
    {
        [InspectorName("剑身起点")] public Transform bladeBase;
        [InspectorName("剑尖")] public Transform bladeTip;
        [InspectorName("剑身接触半径（米）"), Min(.005f)] public float radius = .035f;

        /// <summary>本次直击已经成功结算时触发；同一挥击对同一敌人只通知一次。</summary>
        public event Action<SkillHitResult> HitResolved;

        readonly Collider[] contacts = new Collider[48], originChecks = new Collider[32];
        readonly RaycastHit[] sweeps = new RaycastHit[48], walls = new RaycastHit[32];
        bool hasPose;
        Vector3 previousBase, previousEdge;

        public void ResetTracking() => hasPose = false;
        void OnDisable() => ResetTracking();

        public void Sample(SkillStrike activeSwing, Transform playerRoot, LayerMask hitMask, LayerMask playerLayers)
        {
            if (!isActiveAndEnabled || !bladeBase || !bladeTip || Time.timeScale <= 0)
            {
                ResetTracking();
                return;
            }
            var hilt = bladeBase.position;
            var edge = bladeTip.position - hilt;
            if (!float.IsFinite(hilt.sqrMagnitude) || !float.IsFinite(edge.sqrMagnitude) || edge.sqrMagnitude < 1e-8f)
            {
                ResetTracking();
                return;
            }
            var lastHilt = hasPose ? previousBase : hilt;
            var lastEdge = hasPose ? previousEdge : edge;
            previousBase = hilt;
            previousEdge = edge;
            hasPose = true;
            if (activeSwing == null) return;

            float r = Mathf.Max(.005f, radius);
            int mask = hitMask.value & ~playerLayers.value;
            // Each interval sweeps the whole capsule through translation. For
            // rotation, bound the tip arc per interval to half the blade radius.
            float arc = Mathf.Deg2Rad * Vector3.Angle(lastEdge, edge) * Mathf.Max(lastEdge.magnitude, edge.magnitude);
            int intervals = Mathf.Max(1, Mathf.CeilToInt((arc + Mathf.Abs(lastEdge.magnitude - edge.magnitude)) / (.5f * r)));
            if (intervals > 128) return; // A discontinuity is not a sweep.
            Vector3 a = lastHilt, b = lastHilt + lastEdge;
            Overlap(activeSwing, a, b, r, playerRoot, mask);
            if ((lastHilt - hilt).sqrMagnitude < 1e-12f && (lastEdge - edge).sqrMagnitude < 1e-12f) return;
            for (int i = 1; i <= intervals && hasPose && isActiveAndEnabled; ++i)
            {
                float t = (float)i / intervals;
                Vector3 nextA = Vector3.Lerp(lastHilt, hilt, t);
                Vector3 nextB = nextA + Vector3.Slerp(lastEdge, edge, t);
                Vector3 delta = ((nextA - a) + (nextB - b)) * .5f;
                float distance = delta.magnitude;
                if (distance > .00001f)
                {
                    int count = Physics.CapsuleCastNonAlloc(a, b, r, delta / distance, sweeps, distance,
                        mask, QueryTriggerInteraction.Collide);
                    if (count < sweeps.Length)
                        for (int j = 0; j < count && hasPose && isActiveAndEnabled; ++j)
                        {
                            var hit = sweeps[j];
                            Vector3 grip = Vector3.Lerp(a, nextA, hit.distance / distance);
                            if (SkillHitTarget.Ignore(hit.collider, transform, playerRoot)) continue;
                            CheckContact(activeSwing, new SkillHitTarget(hit.collider), grip, hit.point, r, playerRoot, mask);
                        }
                }
                if (hasPose && isActiveAndEnabled) Overlap(activeSwing, nextA, nextB, r, playerRoot, mask);
                a = nextA;
                b = nextB;
            }
        }

        void Overlap(SkillStrike swing, Vector3 a, Vector3 b, float r, Transform player, int mask)
        {
            int count = Physics.OverlapCapsuleNonAlloc(a, b, r, contacts, mask, QueryTriggerInteraction.Collide);
            if (count == contacts.Length) return;
            Vector3 edge = b - a;
            for (int i = 0; i < count && hasPose && isActiveAndEnabled; ++i)
            {
                var c = contacts[i];
                if (SkillHitTarget.Ignore(c, transform, player)) continue;
                var target = new SkillHitTarget(c);
                if (!target.IsTarget || swing.AlreadyHit(target.Enemy)) continue;
                float t = Mathf.Clamp01(Vector3.Dot(c.bounds.center - a, edge) / edge.sqrMagnitude);
                Vector3 point = c.ClosestPoint(a + t * edge);
                CheckContact(swing, target, a, point, r, player, mask);
            }
        }

        void CheckContact(SkillStrike swing, SkillHitTarget target, Vector3 grip, Vector3 point, float r, Transform player, int mask)
        {
            if (!target.IsTarget || swing.AlreadyHit(target.Enemy) ||
                (swing.Attacker && Blocked(swing.Attacker.position, grip, .005f, player, mask)) ||
                Blocked(grip, point, r, player, mask)) return;
            var hit = swing.Resolve(target, point);
            // 伤害回调可能立即禁用或销毁武器；只有仍然有效的采样才能继续通知视觉层。
            if (this && isActiveAndEnabled && hasPose && hit.DirectApplied) HitResolved?.Invoke(hit);
        }

        bool Blocked(Vector3 from, Vector3 to, float r, Transform player, int mask)
        {
            int count = Physics.OverlapSphereNonAlloc(from, r, originChecks, mask, QueryTriggerInteraction.Ignore);
            if (count == originChecks.Length) return true;
            for (int i = 0; i < count; ++i)
                if (IsObstacle(originChecks[i], player)) return true;
            Vector3 delta = to - from;
            float length = delta.magnitude;
            if (length < .00001f) return false;
            count = Physics.SphereCastNonAlloc(from, r, delta / length, walls, length, mask, QueryTriggerInteraction.Ignore);
            if (count == walls.Length) return true;
            for (int i = 0; i < count; ++i)
                if (IsObstacle(walls[i].collider, player)) return true;
            return false;
        }

        bool IsObstacle(Collider c, Transform player) =>
            !SkillHitTarget.Ignore(c, transform, player) && !new SkillHitTarget(c).IsTarget;
    }
}
