using System;
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
