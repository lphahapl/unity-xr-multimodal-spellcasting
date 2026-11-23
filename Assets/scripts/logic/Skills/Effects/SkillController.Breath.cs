using System;
using Oculus.Interaction.Input;
using UnityEngine;

namespace NarutoVR.SealCombat
{
    public sealed partial class SkillController
    {
        internal Func<SkillDefinition, bool, bool> CurrentSealHeld { get; set; }
        static readonly Vector3 DefaultMouthOffset = new(0, -.09f, .08f);
        const float JointSwitchHeight = .004f;
        struct BreathAim
        {
            public HandJointId? left, right;
        }

        BreathAim pendingBreathAim;
        SkillDefinition pendingBreathSkill;
        enum MouthState { Ready, NoTracking, Released, TooFar }

        internal void SetPendingMouthCondition(SkillDefinition skill)
        {
            pendingBreathAim = default;
            pendingBreathSkill = skill;
            // A voice event can complete the match between hand frames, before the seal is raised.
            if (skill && skill.effectDefinition is BreathEffectDefinition && skill.triggerMode != SkillTriggerMode.VoiceOnly)
                GetMouthPose(skill, false, ref pendingBreathAim, out _);
        }

        internal bool IsMouthConditionMet(SkillDefinition skill, bool sustaining)
        {
            if (!skill) return false;
            if (skill.castCondition == SkillCastCondition.None) return true;
            if (pendingBreathSkill == skill)
                return GetMouthPose(skill, sustaining, ref pendingBreathAim, out _) == MouthState.Ready;
            BreathAim aim = default;
            return GetMouthPose(skill, sustaining, ref aim, out _) == MouthState.Ready;
        }

        MouthState GetMouthPose(SkillDefinition skill, bool sustaining, ref BreathAim aim, out Pose pose)
        {
            pose = default;
            if (!head) { aim = default; return MouthState.NoTracking; }
            var breath = skill.effectDefinition as BreathEffectDefinition;
            pose = new Pose(head.TransformPoint(breath ? breath.mouthOffset : DefaultMouthOffset), head.rotation);
            bool nearMouth = skill.castCondition == SkillCastCondition.SealNearMouth;
            bool aimWithSeal = breath && skill.triggerMode != SkillTriggerMode.VoiceOnly;
            if (!nearMouth && !aimWithSeal) return MouthState.Ready;
            if (!TryGetWrist(true, out var left, out double lt) ||
                !TryGetWrist(false, out var right, out double rt) ||
                !double.IsFinite(lt) || !double.IsFinite(rt) ||
                Math.Abs(lt - rt) > SealLab.SealFeatureExtractor.MaximumHandSkew)
            { aim = default; return MouthState.NoTracking; }

            if ((nearMouth || breath.mode == BreathCastMode.Sustained) &&
                CurrentSealHeld?.Invoke(skill, sustaining || pendingBreathSkill == skill) != true)
                return MouthState.Released;

            if (aimWithSeal)
            {
                if (WristPoseOverride != null || !TryGetSealTop(lab.Frame.left, ref aim.left, out var leftTop) ||
                    !TryGetSealTop(lab.Frame.right, ref aim.right, out var rightTop))
                { aim = default; return MouthState.NoTracking; }
                // Blend across the top few millimetres so tracking noise cannot swap the aim between hands.
                const float heightBand = .008f;
                float leftWeight = .5f + (leftTop.y - rightTop.y) / (2 * heightBand);
                Vector3 top = Vector3.Lerp(rightTop, leftTop, leftWeight);
                Vector3 direction = top - pose.position;
                if (Vector3.Dot(direction, head.forward) < .01f) return MouthState.TooFar;
                pose.rotation = Quaternion.LookRotation(direction, head.up);
            }
            if (!nearMouth) return MouthState.Ready;

            Vector3 center = GetSealCenter(left, right);
            float limit = sustaining ? skill.mouthStopDistance : skill.mouthStartDistance;
            return (center - pose.position).sqrMagnitude <= limit * limit ? MouthState.Ready : MouthState.TooFar;
        }

        // Prefer matching knuckles; wrist-only providers still give a symmetric fallback.
        Vector3 GetSealCenter(Pose left, Pose right)
        {
            if (WristPoseOverride == null && TryGetBreathJoint(lab.Frame.left, HandJointId.HandIndex1, out var a) &&
                TryGetBreathJoint(lab.Frame.right, HandJointId.HandIndex1, out var b)) return (a + b) * .5f;
            return (left.position + right.position) * .5f;
        }

        bool TryGetBreathJoint(SealLab.HandSnapshot hand, HandJointId id, out Vector3 world)
        {
            int joint = (int)id;
            world = default;
            if (joint >= hand.joints.Length || joint >= hand.jointValid.Length || !hand.jointValid[joint] ||
                !SealLab.SealLab.FinitePose(hand.joints[joint])) return false;
            world = lab.recordingOrigin.TransformPoint(hand.joints[joint].position);
            return true;
        }

        bool TryGetSealTop(SealLab.HandSnapshot hand, ref HandJointId? selected, out Vector3 top)
        {
            top = default;
            HandJointId highestJoint = default;
            float highest = float.NegativeInfinity;
            bool hasJointBeyondWrist = false;
            for (int i = 0; i < Constants.NUM_HAND_JOINTS; ++i)
            {
                var joint = (HandJointId)i;
                if (!TryGetBreathJoint(hand, joint, out var point)) continue;
                if (joint != HandJointId.HandWristRoot) hasJointBeyondWrist = true;
                if (point.y <= highest) continue;
                highestJoint = joint;
                top = point;
                highest = point.y;
            }
            // A wrist-only observation locates the hand but cannot locate the top of a seal.
            if (!hasJointBeyondWrist) return false;
            // Keep the joint's identity inside the band, using its position from this observation.
            if (selected.HasValue && TryGetBreathJoint(hand, selected.Value, out var selectedPoint) &&
                highest <= selectedPoint.y + JointSwitchHeight)
                top = selectedPoint;
            else
                selected = highestJoint;
            return true;
        }

        PreparedSkillCast PrepareBreath(BreathEffectDefinition settings, SkillDefinition skill)
        {
            if (!head || HasOccupiedHand || !RightHandActionAllowed)
            { Report("当前无法从嘴部施法"); return null; }
            var instance = Instantiate(settings.visualPrefab, StagingRoot);
            try
            {
                instance.SetActive(false);
                instance.name = skill.displayName;
                settings.ConfigureVisual(instance);
                Pose launchPose = default;
                BreathAim aim = pendingBreathSkill == skill ? pendingBreathAim : default;
                bool CanStart() => !HasOccupiedHand && RightHandActionAllowed &&
                    GetMouthPose(skill, false, ref aim, out launchPose) == MouthState.Ready;
                if (settings.mode == BreathCastMode.Sustained)
                {
                    var channel = new BreathChannel(this, instance, settings, skill);
                    return new PreparedSkillCast(() => CanStart() && TryRegisterRightHandEffect(channel),
                        () => channel.Publish(launchPose, aim), channel.Cancel);
                }
                var projectile = instance.AddComponent<SkillProjectile>();
                projectile.speed = settings.speed;
                projectile.damage = settings.damage;
                projectile.poiseDamage = settings.poiseDamage;
                projectile.radius = settings.radius;
                projectile.lifetime = settings.range / settings.speed;
                projectile.AttackerTarget = head;
                var mark = settings.surfaceMark;
                if (mark) projectile.MarkSurface = hit => StampSurface(hit, mark, Vector3.zero);
                projectile.GameClock = GameClock;
                return new PreparedSkillCast(() => CanStart() && WeaponOriginClear(launchPose.position, settings.radius),
                    () => LaunchBreath(projectile, launchPose), () => FreePrepared(instance));
            }
            catch { FreePrepared(instance); throw; }
        }

        void LaunchBreath(SkillProjectile projectile, Pose pose)
        {
            try
            {
                projectile.transform.SetParent(null, false);
                projectile.transform.SetPositionAndRotation(pose.position, pose.rotation);
                projectile.ArmFlight(pose.forward, WeaponOwnerRoot, hitLayers, playerLayers, OnTargetHit, OnProjectileFinished);
                projectiles.Add(projectile);
                LastLaunchPosition = pose.position;
                LastLaunchDirection = pose.forward;
                ++LaunchCount;
                projectile.gameObject.SetActive(true);
                if (projectile && !projectile.IsFinished) projectile.CheckLaunchOverlap();
            }
            catch { if (projectile) projectile.Finish(); throw; }
        }

        sealed class BreathChannel : ISkillEffectLifetime
        {
            readonly SkillController owner;
            readonly GameObject visual;
            readonly SkillDefinition skill;
            readonly BreathEffectDefinition settings;
            readonly Rpg.RpgPlayerState vitals;
            double lastTick, nextDamage, paidSpraySeconds, lostAt = double.NaN;
            BreathAim aim;
            bool cancelled;

            public BreathChannel(SkillController owner, GameObject visual, BreathEffectDefinition settings, SkillDefinition skill)
            {
                this.owner = owner;
                this.visual = visual;
                this.settings = settings;
                this.skill = skill;
                vitals = owner.Vitals;
            }

            public bool IsAlive => !cancelled && visual;

            public void Publish(Pose pose, BreathAim initialAim)
            {
                try
                {
                    string group = string.IsNullOrEmpty(skill.cooldownGroup) ? skill.skillId : skill.cooldownGroup;
                    if (!vitals.BeginSustainedCost(this, group, settings.minCooldownSeconds))
                        throw new InvalidOperationException("无法开始消耗度力");
                    lastTick = owner.Now;
                    aim = initialAim;
                    nextDamage = lastTick + settings.damageInterval;
                    visual.transform.SetParent(owner.head, false);
                    visual.transform.SetPositionAndRotation(pose.position, pose.rotation);
                    owner.SetActivationAudioAnchor(visual.transform);
                    visual.SetActive(true);
                }
                catch { Cancel(); throw; }
            }

            public void Tick(double now)
            {
                if (!owner.head || !owner.RightHandActionAllowed || !vitals.Alive || vitals.Mana <= 0) { Cancel(); return; }
                double elapsed = Math.Max(0, now - lastTick);
                lastTick = now;
                var state = owner.GetMouthPose(skill, true, ref aim, out var pose);
                if (state == MouthState.NoTracking)
                {
                    // Brief occlusion keeps the cast, with emission, damage and cost suspended.
                    if (double.IsNaN(lostAt)) lostAt = now;
                    visual.SetActive(false);
                    nextDamage = now + settings.damageInterval;
                    if (now - lostAt >= .10) Cancel();
                    return;
                }
                if (state != MouthState.Ready) { Cancel(); return; }
                visual.transform.SetPositionAndRotation(pose.position, pose.rotation);
                if (!double.IsNaN(lostAt))
                {
                    if (now - lostAt >= .10) { Cancel(); return; }
                    lostAt = double.NaN;
                    elapsed = 0;
                    visual.SetActive(true);
                }
                paidSpraySeconds += Math.Min(elapsed, (double)vitals.Mana / settings.manaPerSecond);
                if (!vitals.SpendSustainedCost(this, elapsed * settings.manaPerSecond, now,
                    settings.CooldownAfterSpraying(paidSpraySeconds))) { Cancel(); return; }
                if (now < nextDamage) return;
                nextDamage = now + settings.damageInterval;
                owner.HitWithBreath(visual.transform.position, visual.transform.forward, settings, this);
            }

            public void Cancel()
            {
                if (cancelled) return;
                cancelled = true;
                vitals.EndSustainedCost(this, owner.Now);
                if (visual) owner.StopHeldAudio(visual.transform);
                FreePrepared(visual);
            }
        }

        void HitWithBreath(Vector3 origin, Vector3 forward, BreathEffectDefinition settings, BreathChannel channel)
        {
            if (!WeaponOriginClear(origin, settings.radius)) return;
            int overlaps = Physics.OverlapSphereNonAlloc(origin, settings.radius, throwOverlaps, WeaponHitMask, QueryTriggerInteraction.Collide);
            if (overlaps == throwOverlaps.Length) return;
            Collider closest = null;
            RaycastHit? surfaceSweep = null;
            for (int i = 0; i < overlaps; i++)
            {
                var other = throwOverlaps[i];
                if (IgnoreThrowCollider(other)) continue;
                if (!new SkillHitTarget(other).IsTarget) return;
                closest = other;
            }
            if (!closest)
            {
                int count = Physics.SphereCastNonAlloc(origin, settings.radius, forward, throwSweeps, settings.range, WeaponHitMask, QueryTriggerInteraction.Collide);
                if (count == throwSweeps.Length) return;
                float nearest = float.PositiveInfinity;
                for (int i = 0; i < count; i++)
                {
                    var hit = throwSweeps[i];
                    if (hit.distance > nearest || IgnoreThrowCollider(hit.collider)) continue;
                    if (hit.distance == nearest && new SkillHitTarget(hit.collider).IsTarget) continue;
                    nearest = hit.distance;
                    closest = hit.collider;
                    surfaceSweep = hit;
                }
            }
            if (closest)
            {
                var target = new SkillHitTarget(closest);
                if (target.IsTarget) target.Apply("breath:" + Guid.NewGuid().ToString("N"), settings.damage, settings.poiseDamage, head);
                else if (settings.surfaceMark && surfaceSweep is RaycastHit sweep &&
                    SurfaceContact.TryResolve(closest, origin + forward * sweep.distance, forward,
                        settings.radius, sweep, out var surface))
                    StampSurface(surface, settings.surfaceMark, forward, channel, true);
            }
        }
    }
}
