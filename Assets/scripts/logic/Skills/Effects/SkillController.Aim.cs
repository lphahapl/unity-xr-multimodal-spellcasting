using System;
using NarutoVR.SealCombat.Enemies;
using UnityEngine;

namespace NarutoVR.SealCombat
{
    public sealed partial class SkillController
    {
        SkillAimReticle fireAimView, mouthAimView;
        BreathChannel aimedChannel;
        SkillProjectile aimedBurst;
        SkillDefinition burstSkill;
        Pose burstPose;
        double burstAimUntil;

        internal SkillAimProfile HeldAimProfile => currentFireball?.Source?.aimProfile;
        internal bool ProfileThrowDirectionVisible => HeldAimProfile && HeldAimProfile.showDirectionLine &&
            fireAimView?.Visible == true;

        void RefreshCastReticles()
        {
            var fire = HeldAimProfile;
            if (fire && fire.showReticle && TryGetThrowPreview(out var aim))
                DrawReticle(ref fireAimView, "火球术 · 瞄准", fire, aim);
            else fireAimView?.Hide();

            if (head && RightHandActionAllowed && TryMouthPreview(out var skill,out var breathAim))
                DrawReticle(ref mouthAimView, "吐息 · 瞄准", skill.aimProfile, breathAim);
            else mouthAimView?.Hide();
        }

        void DrawReticle(ref SkillAimReticle view, string name, SkillAimProfile profile, FireballAim aim)
        {
            view ??= new SkillAimReticle(gameObject.scene,name);
            view.Show(profile,aim,head);
        }

        bool TryMouthPreview(out SkillDefinition skill, out FireballAim aim)
        {
            aim = default;
            Pose pose;
            float range;
            if (chargeSkill)
            {
                skill = chargeSkill;
                return skill.aimProfile && skill.aimProfile.showReticle && chargeFlame && chargeFlame.activeSelf &&
                    SealProjectileAim(skill, (BreathEffectDefinition)skill.effectDefinition, out aim);
            }
            if (aimedChannel != null)
            {
                skill = aimedChannel.Skill;
                if (!aimedChannel.TryGetSprayPose(out pose)) return false;
                range = ((BreathEffectDefinition)skill.effectDefinition).range;
            }
            else if (pendingBreathSkill)
            {
                skill = pendingBreathSkill;
                if (!skill.aimProfile || !skill.aimProfile.showReticle ||
                    !(skill.effectDefinition is BreathEffectDefinition waiting)) return false;
                var state = GetMouthPose(skill,false,ref pendingBreathAim,out pose);
                if (state != MouthState.Ready && state != MouthState.TooFar) return false;
                range = waiting.range;
            }
            else
            {
                skill = burstSkill;
                if (!aimedBurst || aimedBurst.IsFinished || Now >= burstAimUntil || !skill) return false;
                if (skill.triggerMode != SkillTriggerMode.VoiceOnly &&
                    (!TryGetWrist(true,out _,out var left) || !TryGetWrist(false,out _,out var right) ||
                    Math.Abs(left-right)>SealLab.SealFeatureExtractor.MaximumHandSkew)) return false;
                pose = new Pose(aimedBurst.transform.position,burstPose.rotation);
                range = ((BreathEffectDefinition)skill.effectDefinition).range -
                    Vector3.Dot(pose.position-burstPose.position,pose.forward);
            }
            if (!skill.aimProfile || !skill.aimProfile.showReticle || range<=.001f) return false;
            var settings = (BreathEffectDefinition)skill.effectDefinition;
            // In flight the projectile already cleared the muzzle and can move independently of the head.
            bool atMuzzle = aimedChannel != null || pendingBreathSkill;
            if (atMuzzle && !WeaponOriginClear(pose.position,settings.radius)) return false;
            if (!TraceBreath(pose.position,pose.forward,settings.radius,range,out var first,out var sweep)) return false;
            var end = pose.position + pose.forward * (sweep?.distance ?? (first ? 0f : range));
            aim = new FireballAim(pose.position,pose.forward,pose.position+pose.forward*range,end,
                first && !new SkillHitTarget(first).IsTarget);
            return true;
        }

        readonly Collider[] assistCandidates = new Collider[64];

        Collider FirstAimContact(Vector3 origin, Vector3 direction, float distance, float radius, out bool complete)
        {
            int count = radius > 0
                ? Physics.SphereCastNonAlloc(origin, radius, direction, throwSweeps, distance, WeaponHitMask, QueryTriggerInteraction.Collide)
                : Physics.RaycastNonAlloc(origin, direction, throwSweeps, distance, WeaponHitMask, QueryTriggerInteraction.Collide);
            complete = count < throwSweeps.Length;
            Collider first = null;
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                var hit = throwSweeps[i];
                if (IgnoreThrowCollider(hit.collider) || hit.distance > nearest) continue;
                if (hit.distance == nearest && new SkillHitTarget(hit.collider).IsTarget) continue;
                first = hit.collider;
                nearest = hit.distance;
            }
            return first;
        }

        bool VisibleToAim(Vector3 from, Vector3 point, EnemyObj enemy, float radius)
        {
            var delta = point - from;
            float depth = delta.magnitude;
            if (depth < .001f) return false;
            var contact = FirstAimContact(from, delta / depth, depth, radius, out bool complete);
            return complete && (!contact || new SkillHitTarget(contact).Enemy == enemy);
        }

        bool TryAssistDirection(SkillAimProfile profile, Vector3 axisOrigin, Vector3 axis, float axisRange,
            Vector3 origin, Vector3 raw, float radius, float reach, out Vector3 direction, out float depth)
        {
            direction = raw;
            depth = 0;
            if (!profile || !SkillAimProfile.AssistEnabled || !(profile.assistCone > 0) ||
                !(profile.assistMaxCorrection > 0) || !(profile.assistStrength > 0) || !head) return false;

            float range = Mathf.Min(axisRange, reach);
            if (!(range > .01f)) return false;
            float halfAngle = Mathf.Min(6f, profile.assistCone);
            float width = radius + Mathf.Max(.02f, range * Mathf.Tan(halfAngle * Mathf.Deg2Rad));
            int count = Physics.OverlapCapsuleNonAlloc(axisOrigin, axisOrigin + axis * range, width, assistCandidates,
                WeaponHitMask, QueryTriggerInteraction.Ignore);
            if (count == 0 || count == assistCandidates.Length) return false;

            // A normal shot which already lands on a target retains the user's direction (including weak points).
            var direct = FirstAimContact(origin, raw, reach, radius, out bool complete);
            if (!complete || (direct && new SkillHitTarget(direct).IsTarget)) return false;

            Vector3 selected = default;
            float bestAngle = halfAngle, bestDepth = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                var collider = assistCandidates[i];
                if (!(collider is CapsuleCollider body) || !body.TryGetComponent<EnemyController>(out var enemy) ||
                    !enemy.CanReceiveHit || !enemy.Entity || enemy.Entity.Combat == null || enemy.Entity.Combat.Dead) continue;

                var center = body.bounds.center;
                float along = Vector3.Dot(center - axisOrigin, axis);
                if (along <= 0 || along > range + body.bounds.extents.magnitude) continue;
                // Aim just inside the visible silhouette so wide and narrow enemies use the same angular tolerance.
                var edge = body.ClosestPoint(axisOrigin + axis * along);
                var point = Vector3.MoveTowards(edge, center, .02f);
                var offset = point - axisOrigin;
                // Measure the angular miss beyond the existing projectile width. This also
                // lets the short, wide breath benefit on a genuine near miss.
                float angularRadius = Mathf.Atan2(radius, Mathf.Max(along, .001f)) * Mathf.Rad2Deg;
                float angle = Mathf.Max(0f, Vector3.Angle(axis, offset) - angularRadius);
                float distance = Vector3.Distance(point, origin);
                if (distance > reach || offset.magnitude > axisRange || angle > bestAngle ||
                    (Mathf.Approximately(angle, bestAngle) && distance >= bestDepth)) continue;
                if (!VisibleToAim(head.position, point, enemy.Entity, 0) ||
                    !VisibleToAim(origin, point, enemy.Entity, radius)) continue;
                bestAngle = angle;
                bestDepth = distance;
                selected = point;
            }
            if (float.IsPositiveInfinity(bestDepth)) return false;
            var desired = (selected - origin).normalized;
            float correction = Mathf.Min(Mathf.Min(3, profile.assistMaxCorrection),
                Vector3.Angle(raw, desired) * Mathf.Clamp01(profile.assistStrength));
            if (!(correction > 0)) return false;
            direction = Vector3.RotateTowards(raw, desired, correction * Mathf.Deg2Rad, 0).normalized;
            depth = bestDepth;
            return true;
        }

        void AssistMouthPose(SkillDefinition skill, BreathEffectDefinition breath, ref Pose pose)
        {
            if (breath && TryAssistDirection(skill.aimProfile, pose.position, pose.forward, breath.range,
                pose.position, pose.forward, breath.radius, breath.range, out var assisted, out _))
                pose.rotation = Quaternion.FromToRotation(pose.forward, assisted) * pose.rotation;
        }

        void ClearBurstAim()
        {
            if (aimedBurst && aimedChannel == null && !pendingBreathSkill) mouthAimView?.Hide();
            aimedBurst = null;
            burstSkill = null;
        }

        void ClearCastReticles()
        {
            ClearBurstAim();
            fireAimView?.Dispose();
            mouthAimView?.Dispose();
            fireAimView = mouthAimView = null;
        }
    }
}
