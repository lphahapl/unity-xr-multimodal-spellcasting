using System;
using NarutoVR.SealLab;
using Oculus.Interaction.Input;
using UnityEngine;

namespace NarutoVR.SealCombat.Rpg
{
    // Read the same validated frame as seal recognition, after SealLab (order 200).
    [DefaultExecutionOrder(420), DisallowMultipleComponent]
    public sealed class TrackedAvatarArms : MonoBehaviour
    {
        [InspectorName("玩家的手部采样器")] public SealLab.SealLab source;
        [SerializeField] Arm left = new(), right = new();

        // Wrist first, then three moving segments per finger. Palm, non-thumb metacarpals
        // and tips have no corresponding rotating Humanoid slot.
        public static HandJointId Joint(int slot) => slot switch
        {
            0 => HandJointId.HandWristRoot,
            1 => HandJointId.HandThumb1, 2 => HandJointId.HandThumb2, 3 => HandJointId.HandThumb3,
            4 => HandJointId.HandIndex1, 5 => HandJointId.HandIndex2, 6 => HandJointId.HandIndex3,
            7 => HandJointId.HandMiddle1, 8 => HandJointId.HandMiddle2, 9 => HandJointId.HandMiddle3,
            10 => HandJointId.HandRing1, 11 => HandJointId.HandRing2, 12 => HandJointId.HandRing3,
            13 => HandJointId.HandPinky1, 14 => HandJointId.HandPinky2, 15 => HandJointId.HandPinky3,
            _ => throw new ArgumentOutOfRangeException(nameof(slot))
        };

        [Serializable]
        public sealed class Arm
        {
            public Transform upper, lower;
            public Transform[] targets = new Transform[16];
            public Quaternion[] axisOffsets = new Quaternion[16];
            readonly Quaternion[] baseline = new Quaternion[18];
            bool applied;

            internal void Restore()
            {
                if (!applied) return;
                upper.localRotation = baseline[0];
                lower.localRotation = baseline[1];
                for (int i = 0; i < targets.Length; ++i)
                    targets[i].localRotation = baseline[i + 2];
                applied = false;
            }

            internal void Apply(HandSnapshot hand, Transform origin, Vector3 elbowHint, double now, float maxAge)
            {
                if (!upper || !lower || !origin || hand == null || !hand.connected || !hand.tracked || !hand.fresh ||
                    !(now >= hand.observedAt && now - hand.observedAt <= maxAge) ||
                    !Read(hand, 0, out Pose wrist)) return;

                baseline[0] = upper.localRotation;
                baseline[1] = lower.localRotation;
                for (int i = 0; i < targets.Length; ++i) baseline[i + 2] = targets[i].localRotation;
                applied = true;

                AimArm(origin.TransformPoint(wrist.position), elbowHint);
                // Pronation is rotation about the forearm. Applying all of it at the wrist
                // collapses the narrow strip of skinned vertices between wrist and sleeve.
                Vector3 axis = (targets[0].position - lower.position).normalized;
                Quaternion delta = origin.rotation * wrist.rotation * axisOffsets[0] * Quaternion.Inverse(targets[0].rotation);
                Vector3 projected = Vector3.Project(new Vector3(delta.x, delta.y, delta.z), axis);
                Quaternion twist = new Quaternion(projected.x,projected.y,projected.z,delta.w);
                if (Quaternion.Dot(twist,twist) > 1e-6f) lower.rotation = twist.normalized * lower.rotation;
                for (int slot = 0; slot < targets.Length; ++slot)
                {
                    if (Read(hand, slot, out Pose joint))
                        targets[slot].rotation = origin.rotation * joint.rotation * axisOffsets[slot];
                }
            }

            static bool Read(HandSnapshot hand, int slot, out Pose pose)
            {
                int i = (int)Joint(slot);
                pose = Pose.identity;
                if (hand.joints == null || hand.jointValid == null || i >= hand.joints.Length ||
                    i >= hand.jointValid.Length || !hand.jointValid[i]) return false;
                pose = hand.joints[i];
                return SealLab.SealLab.FinitePose(pose);
            }

            void AimArm(Vector3 goal, Vector3 elbowHint)
            {
                Transform wrist = targets[0];
                Vector3 shoulder = upper.position;
                Vector3 upperSegment = lower.position - shoulder;
                float a = upperSegment.magnitude, b = Vector3.Distance(lower.position, wrist.position);
                if (Mathf.Min(a, b) < 0.0001f) return;
                Vector3 reach = goal - shoulder;
                Vector3 direction = reach.sqrMagnitude > 1e-10f ? reach.normalized : upperSegment / a;
                float d = Mathf.Clamp(reach.magnitude, Mathf.Max(Mathf.Abs(a - b), 0.00001f), a + b);
                float along = (a * a - b * b + d * d) / (2 * d);
                float height = Mathf.Sqrt(Mathf.Max(0, a * a - along * along));
                // Body-relative hint keeps the elbow on the same side as a straight animation arm bends.
                Vector3 pole = Vector3.ProjectOnPlane(elbowHint, direction);
                if (pole.sqrMagnitude < .001f) pole = Vector3.ProjectOnPlane(upperSegment, direction);
                if (pole.sqrMagnitude < 1e-8f)
                    pole = Vector3.Cross(direction, Mathf.Abs(direction.y) < .9f ? Vector3.up : Vector3.right);
                Vector3 elbow = shoulder + along * direction + height * pole.normalized;
                upper.rotation = Quaternion.FromToRotation(upperSegment, elbow - shoulder) * upper.rotation;
                Vector3 clampedGoal = shoulder + d * direction;
                lower.rotation = Quaternion.FromToRotation(wrist.position - lower.position, clampedGoal - lower.position) * lower.rotation;
            }
        }

        public Arm Side(bool isLeft) => isLeft ? left : right;
        // Release the previous visual override before Animator samples the new frame.
        // Also restores unanimated rest bones when a hand leaves tracking.
        void Update() => RestoreAnimation();
        void OnDisable() => RestoreAnimation();
        public void RestoreAnimation() { left.Restore(); right.Restore(); }
        void LateUpdate()
        {
            if (source && source.isActiveAndEnabled)
                ApplyFrame(source.Frame, source.recordingOrigin, Time.realtimeSinceStartupAsDouble, source.staleAfterSeconds);
        }

        public void ApplyFrame(SealFrame frame, Transform origin, double now, float maxAge)
        {
            RestoreAnimation();
            if (frame == null) return;
            Vector3 backDown = -transform.forward - transform.up * .5f;
            left.Apply(frame.left, origin, backDown - transform.right * .65f, now, maxAge);
            right.Apply(frame.right, origin, backDown + transform.right * .65f, now, maxAge);
        }
    }
}
