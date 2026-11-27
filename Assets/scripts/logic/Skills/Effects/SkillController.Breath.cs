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
        GameObject pendingPilotFlame;
        SkillDefinition chargeSkill;
        GameObject chargeFlame;

        // Both the small flame and the final projectile use this tracked world-space point.
        bool TrySealFlamePose(out Pose pose)
        {
            pose = default;
            if (!head || !TryGetWrist(true, out var left, out var lt) ||
                !TryGetWrist(false, out var right, out var rt) ||
                Math.Abs(lt - rt) > SealLab.SealFeatureExtractor.MaximumHandSkew) return false;
            Vector3 center = GetSealCenter(left, right) + head.forward * .05f + Vector3.up * .055f;
            pose = new Pose(center, Quaternion.LookRotation(head.forward, Vector3.up));
            return true;
        }

        void ShowSealFlame(GameObject flame)
        {
            if (!flame) return;
            bool visible = TrySealFlamePose(out var pose);
            if (visible) flame.transform.SetPositionAndRotation(pose.position, pose.rotation);
            if (visible != flame.activeSelf) flame.SetActive(visible);
        }

        internal void SetSequenceCharge(SkillDefinition skill)
        {
            if (!skill || !(skill.effectDefinition is BreathEffectDefinition { mode: BreathCastMode.SealProjectile }) ||
                HasOccupiedHand || !RightHandActionAllowed) skill = null;
            if (skill != chargeSkill)
            {
                FreePrepared(chargeFlame);
                chargeFlame = null;
                chargeSkill = skill;
                if (!skill) mouthAimView?.Hide();
            }
            if (!chargeSkill || !head) return;
            if (!chargeFlame)
            {
                if (!TrySealFlamePose(out var p)) return;
                var settings = (BreathEffectDefinition)skill.effectDefinition;
                chargeFlame = Instantiate(settings.pilotFlamePrefab, p.position, p.rotation, head);
            }
            ShowSealFlame(chargeFlame);
        }

        bool SealProjectileAim(SkillDefinition skill, BreathEffectDefinition settings, out FireballAim aim)
        {
            aim = default;
            return TrySealFlamePose(out var pose) && WeaponOriginClear(pose.position, settings.radius) &&
                TryResolveProjectileAim(pose.position, skill.aimProfile, settings.radius, settings.range, out aim);
        }

        enum MouthState { Ready, NoTracking, Released, TooFar, AimBehindMouth }

        internal void SetPendingMouthCondition(SkillDefinition skill)
        {
            if (!skill) SetSequenceCharge(null);
            if (skill) ClearBurstAim();
            if (pendingBreathSkill && aimedChannel == null && !aimedBurst) mouthAimView?.Hide();
            FreePrepared(pendingPilotFlame);
            pendingPilotFlame = null;
            pendingBreathAim = default;
            pendingBreathSkill = skill;
            // A voice event can complete the match between hand frames, before the seal is raised.
            if (skill && skill.effectDefinition is BreathEffectDefinition breath && skill.triggerMode != SkillTriggerMode.VoiceOnly)
            {
                GetMouthPose(skill, false, ref pendingBreathAim, out var pose);
                if (head && breath.mode == BreathCastMode.Sustained && breath.pilotFlamePrefab)
                {
                    pendingPilotFlame = Instantiate(breath.pilotFlamePrefab, pose.position, pose.rotation, head);
                    ShowSealFlame(pendingPilotFlame);
                }
            }
        }

        internal bool IsMouthConditionMet(SkillDefinition skill, bool sustaining)
        {
            if (!skill) return false;
            if (skill.castCondition == SkillCastCondition.None) return true;
            if (pendingBreathSkill == skill)
            {
                var state = GetMouthPose(skill, sustaining, ref pendingBreathAim, out var pose);
                ShowSealFlame(pendingPilotFlame);
                return state == MouthState.Ready;
            }
            BreathAim aim = default;
            return GetMouthPose(skill, sustaining, ref aim, out _) == MouthState.Ready;
        }

        MouthState GetMouthPose(SkillDefinition skill, bool sustaining, ref BreathAim aim, out Pose pose, bool resuming = false)
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
                if (Vector3.Dot(direction, head.forward) < .01f) return MouthState.AimBehindMouth;
                pose.rotation = Quaternion.LookRotation(direction, head.up);
            }
            if (!nearMouth) return MouthState.Ready;

            Vector3 center = GetSealCenter(left, right);
            float limit = sustaining && !resuming ? skill.mouthStopDistance : skill.mouthStartDistance;
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
                bool CanStart()
                {
                    if (HasOccupiedHand || !RightHandActionAllowed) return false;
                    if (settings.mode != BreathCastMode.SealProjectile)
                        return GetMouthPose(skill, false, ref aim, out launchPose) == MouthState.Ready;
                    if (chargeSkill != skill || !chargeFlame || !SealProjectileAim(skill, settings, out var shot)) return false;
                    launchPose = new Pose(shot.origin, Quaternion.LookRotation(shot.direction, Vector3.up));
                    return true;
                }
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
                ConfigureProjectileImpact(projectile, settings.impactPrefab);
                projectile.AlignImpactToSurface = true;
                var mark = settings.surfaceMark;
                if (mark) projectile.MarkSurface = hit => StampSurface(hit, mark, Vector3.zero);
                projectile.GameClock = GameClock;
                return new PreparedSkillCast(() => CanStart() && WeaponOriginClear(launchPose.position, settings.radius),
                    () => LaunchBreath(projectile, launchPose, skill), () => FreePrepared(instance));
            }
            catch { FreePrepared(instance); throw; }
        }

        void LaunchBreath(SkillProjectile projectile, Pose pose, SkillDefinition skill)
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
                ClearBurstAim();
                if (skill.aimProfile && skill.aimProfile.showReticle && skill.aimProfile.launchedSeconds > 0)
                {
                    aimedBurst = projectile;
                    burstSkill = skill;
                    burstPose = pose;
                    burstAimUntil = Now + skill.aimProfile.launchedSeconds;
                }
                projectile.gameObject.SetActive(true);
                if (skill.effectDefinition is BreathEffectDefinition { mode: BreathCastMode.SealProjectile } &&
                    projectile.TryGetComponent<FlyingFlameVisual>(out var flame)) flame.LaunchFormed();
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
            GameObject pilotFlame;
            bool spraying;
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
            public SkillDefinition Skill => skill;
            public bool TryGetSprayPose(out Pose pose)
            {
                pose = default;
                if (!IsAlive || !spraying || !visual.activeInHierarchy) return false;
                pose = new Pose(visual.transform.position,visual.transform.rotation);
                return true;
            }

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
                    if (settings.pilotFlamePrefab)
                    {
                        if (owner.pendingBreathSkill == skill && owner.pendingPilotFlame)
                        {
                            pilotFlame = owner.pendingPilotFlame;
                            owner.pendingPilotFlame = null;
                        }
                        else
                            pilotFlame = Instantiate(settings.pilotFlamePrefab, pose.position, pose.rotation, owner.head);
                        owner.ShowSealFlame(pilotFlame);
                    }
                    spraying = true;
                    owner.ClearBurstAim();
                    owner.aimedChannel = this;
                    visual.SetActive(true);
                }
                catch { Cancel(); throw; }
            }

            public void Tick(double now)
            {
                if (!owner.head || !owner.RightHandActionAllowed || !vitals.Alive || vitals.Mana <= 0) { Cancel(); return; }
                double elapsed = Math.Max(0, now - lastTick);
                lastTick = now;
                var state = owner.GetMouthPose(skill, true, ref aim, out var pose, resuming: !spraying);
                if (state == MouthState.NoTracking)
                {
                    // Brief occlusion keeps the cast, with emission, damage and cost suspended.
                    if (double.IsNaN(lostAt)) lostAt = now;
                    visual.SetActive(false);
                    if (pilotFlame) pilotFlame.SetActive(false);
                    nextDamage = now + settings.damageInterval;
                    if (now - lostAt >= .10) Cancel();
                    return;
                }
                bool pausedAtDistance = state == MouthState.TooFar && pilotFlame;
                if (state != MouthState.Ready && !pausedAtDistance) { Cancel(); return; }
                visual.transform.SetPositionAndRotation(pose.position, pose.rotation);
                owner.ShowSealFlame(pilotFlame);
                if (!double.IsNaN(lostAt))
                {
                    if (now - lostAt >= .10) { Cancel(); return; }
                    lostAt = double.NaN;
                    elapsed = 0;
                }
                if (pausedAtDistance)
                {
                    spraying = false;
                    if (visual.activeSelf) visual.SetActive(false);
                    if (!pilotFlame.activeSelf) pilotFlame.SetActive(true);
                    return;
                }
                if (!visual.activeSelf)
                {
                    spraying = true;
                    elapsed = 0;
                    nextDamage = now + settings.damageInterval;
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
                if (owner.aimedChannel == this)
                {
                    owner.aimedChannel = null;
                    owner.mouthAimView?.Hide();
                }
                vitals.EndSustainedCost(this, owner.Now);
                if (visual) owner.StopHeldAudio(visual.transform);
                FreePrepared(pilotFlame);
                FreePrepared(visual);
            }
        }

        bool TraceBreath(Vector3 origin, Vector3 forward, float radius, float range, out Collider closest, out RaycastHit? surfaceSweep)
        {
            closest = null;
            surfaceSweep = null;
            int overlaps = Physics.OverlapSphereNonAlloc(origin, radius, throwOverlaps, WeaponHitMask, QueryTriggerInteraction.Collide);
            if (overlaps == throwOverlaps.Length) return false;
            for (int i = 0; i < overlaps; i++)
            {
                var other = throwOverlaps[i];
                if (IgnoreThrowCollider(other)) continue;
                if (!new SkillHitTarget(other).IsTarget) { closest = other; return true; }
                closest = other;
            }
            if (!closest)
            {
                int count = Physics.SphereCastNonAlloc(origin, radius, forward, throwSweeps, range, WeaponHitMask, QueryTriggerInteraction.Collide);
                if (count == throwSweeps.Length) return false;
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
            return true;
        }

        void HitWithBreath(Vector3 origin, Vector3 forward, BreathEffectDefinition settings, BreathChannel channel)
        {
            if (!WeaponOriginClear(origin, settings.radius)) return;
            if (!TraceBreath(origin, forward, settings.radius, settings.range, out var closest, out var surfaceSweep)) return;
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
