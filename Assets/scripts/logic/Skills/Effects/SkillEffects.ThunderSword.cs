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
            Report("雷光剑已在右手生成。看向目标，挥动右手出波。");
            return true;
        }

        // The sword follows the palm centre while preserving the tracked wrist orientation.
        internal bool TryGetSwordGrip(out Pose pose, out double stamp, out bool hasPalm)
        {
            hasPalm = false;
            if (!TryGetRightWeaponWrist(out pose, out stamp)) return false;
            if (WristPoseOverride == null)
            {
                var hand = lab.Frame.right;
                int palm = (int)HandJointId.HandPalm;
                if (palm < hand.jointValid.Length && palm < hand.joints.Length && hand.jointValid[palm] &&
                    NarutoVR.SealLab.SealLab.FinitePose(hand.joints[palm]))
                {
                    pose.position = lab.recordingOrigin.TransformPoint(hand.joints[palm].position);
                    hasPalm = true;
                    return true;
                }
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
