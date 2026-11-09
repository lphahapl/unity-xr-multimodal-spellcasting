using System;
using Oculus.Interaction.Input;
using UnityEngine;

namespace NarutoVR.SealCombat
{
    public sealed partial class SkillEffects
    {
        double nextSwordLightningAt = double.NegativeInfinity;

        internal bool TryEquipThunderSword(ThunderSwordEffectDefinition settings, SkillDefinition skill)
        {
            if (!skill || skill.leftHand || !float.IsFinite(skill.durationSeconds) || skill.durationSeconds <= 0)
            { Report("雷光剑需要右手和有效持续时间"); return false; }
            if (HasOccupiedHand) { Report("手中已有法宝，请先释放或收起"); return false; }
            if (!RightHandActionAllowed || !WeaponAttacker)
            { Report("当前无法使用右手雷光剑"); return false; }
            if (!TryGetSwordGrip(out var grip, out var stamp, out bool hasPalm))
            { Report("请让头显追踪到右手，再召出雷光剑"); return false; }
            if (!ThunderSwordCast.TryStart(this, settings, skill.durationSeconds, grip, stamp, hasPalm)) return false;
            Report("雷光剑已在右手生成。看向目标，横挥出横波、竖挥出竖波；取消施法可收剑。");
            return true;
        }

        // Build a grip from the rigid palm bones, not the SDK wrist's arbitrary local +Z.
        // Blade exits the thumb/index side of the fist; curled fingertips cannot flip it.
        internal bool TryGetSwordGrip(out Pose pose, out double stamp, out bool hasPalm)
        {
            hasPalm = false;
            if (!TryGetRightWeaponWrist(out pose, out stamp)) return false;
            if (WristPoseOverride == null)
            {
                var hand = lab.Frame.right;
                bool Joint(HandJointId id, out Vector3 point)
                {
                    int i=(int)id; point=default;
                    if(i>=hand.jointValid.Length || i>=hand.joints.Length || !hand.jointValid[i] ||
                        !NarutoVR.SealLab.SealLab.FinitePose(hand.joints[i])) return false;
                    point=lab.recordingOrigin.TransformPoint(hand.joints[i].position); return true;
                }
                if(!Joint(HandJointId.HandIndex1,out var index) || !Joint(HandJointId.HandPinky1,out var pinky)) return false;
                Vector3 blade=index-pinky, fingers=(index+pinky)*.5f-pose.position;
                Vector3 normal=Vector3.Cross(blade,fingers);
                if(blade.sqrMagnitude<.0001f || normal.sqrMagnitude<1e-8f) return false;
                pose.rotation=Quaternion.LookRotation(blade,normal);
                pose.position+=(fingers*.5f);
                hasPalm=true;
                int palm = (int)HandJointId.HandPalm;
                if (palm < hand.jointValid.Length && palm < hand.joints.Length && hand.jointValid[palm] &&
                    NarutoVR.SealLab.SealLab.FinitePose(hand.joints[palm]))
                {
                    pose.position = lab.recordingOrigin.TransformPoint(hand.joints[palm].position);
                }
                return true;
            }
            pose.position += pose.rotation * new Vector3(0, 0, .04f);
            return true;
        }

        internal bool TryApplySwordLightning(SkillHitResult hit, ThunderSwordEffectDefinition settings)
        {
            double now = Now;
            if (now < nextSwordLightningAt || !hit.CanTriggerLightning) return false;
            // Reserve before notifying enemies. Their damage listeners can re-enter skill code.
            double previous = nextSwordLightningAt;
            nextSwordLightningAt = now + settings.lightningCooldown;
            if (hit.Enemy.ReceiveHit("thunder-bolt:" + Guid.NewGuid().ToString("N"),
                settings.lightningDamage, settings.lightningPoise, null, WeaponAttacker)) return true;
            nextSwordLightningAt = previous;
            return false;
        }

        internal bool TryGetWeaponWavePreview(Vector3 origin, float radius, out FireballAim aim)
        {
            aim = default;
            if (!TryGetWeaponWaveDirection(origin, out var direction)) return false;
            Vector3 point=GetAimPoint(), endpoint=point;
            bool originBlocked=!WeaponOriginClear(origin,radius), blocked=originBlocked;
            float distance=Vector3.Distance(origin,point), nearest=distance;
            int count=Physics.SphereCastNonAlloc(origin,radius,direction,throwSweeps,distance,WeaponHitMask,QueryTriggerInteraction.Collide);
            bool incomplete=count==throwSweeps.Length;
            if(incomplete) blocked=true;
            for(int i=0;i<count;i++)
            {
                var hit=throwSweeps[i];
                if(IgnoreThrowCollider(hit.collider) || hit.distance>nearest) continue;
                nearest=hit.distance; endpoint=origin+direction*nearest;
                blocked=originBlocked || incomplete || !new SkillHitTarget(hit.collider).IsTarget;
            }
            aim=new FireballAim(origin,direction,point,endpoint,blocked); return true;
        }

        internal bool WeaponOriginClear(Vector3 origin, float radius)
        {
            // The wrist can cross a thin wall while the headset stays on the near side.
            Vector3 from = WeaponAttacker.position;
            if (WeaponOverlapBlocked(from, radius) || WeaponOverlapBlocked(origin, radius)) return false;
            Vector3 delta = origin - from;
            float distance = delta.magnitude;
            if (distance < .001f) return true;
            int count = Physics.SphereCastNonAlloc(from, radius, delta / distance, throwSweeps, distance,
                WeaponHitMask, QueryTriggerInteraction.Ignore);
            if (count == throwSweeps.Length) return false;
            for (int i = 0; i < count; ++i)
                if (BlocksWeaponOrigin(throwSweeps[i].collider)) return false;
            return true;
        }

        bool WeaponOverlapBlocked(Vector3 point, float radius)
        {
            int count = Physics.OverlapSphereNonAlloc(point, radius, throwOverlaps, WeaponHitMask, QueryTriggerInteraction.Ignore);
            if (count == throwOverlaps.Length) return true;
            for (int i = 0; i < count; ++i)
                if (BlocksWeaponOrigin(throwOverlaps[i])) return true;
            return false;
        }

        bool BlocksWeaponOrigin(Collider c) => !IgnoreThrowCollider(c) && !new SkillHitTarget(c).IsTarget;
    }
}
