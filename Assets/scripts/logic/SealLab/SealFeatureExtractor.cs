using System.Collections.Generic;
using Oculus.Interaction.Input;
using Oculus.Interaction.PoseDetection;
using UnityEngine;

namespace NarutoVR.SealLab
{
    public sealed class SealFeatureExtractor
    {
        private readonly FingerShapes shapes = new();
        private readonly SnapshotHand left = new() { Handedness = Handedness.Left };
        private readonly SnapshotHand right = new() { Handedness = Handedness.Right };
        public const float MaximumHandSkew = 0.06f;

        public void Extract(SealFrame frame, List<SealFeature> output)
        {
            output.Clear(); left.Set(frame.left); right.Set(frame.right);
            AddFingers("L", frame.left, left, output);
            AddFingers("R", frame.right, right, output);
            bool pair = left.IsTrackedDataValid && right.IsTrackedDataValid &&
                        System.Math.Abs(frame.left.observedAt - frame.right.observedAt) <= MaximumHandSkew;
            bool confident = pair && left.IsHighConfidence && right.IsHighConfidence;
            bool wrists = PairPositions(frame, HandJointId.HandWristRoot, HandJointId.HandWristRoot, out var l, out var r);
            output.Add(new SealFeature("Both.WristDistance", "m", Vector3.Distance(l, r), pair && wrists, confident));

            // Infer palm-facing directions from actual wrist/index/pinky positions, correcting handedness.
            bool normals = PalmNormal(frame.left, true, out var ln) & PalmNormal(frame.right, false, out var rn);
            output.Add(new SealFeature("Both.PalmAngle", "deg", Vector3.Angle(ln, rn), pair && normals, confident));
            AddDistance("Both.IndexTipDistance", HandJointId.HandIndexTip, HandJointId.HandIndexTip, HandFinger.Index);
            AddDistance("Both.MiddleTipDistance", HandJointId.HandMiddleTip, HandJointId.HandMiddleTip, HandFinger.Middle);
            AddDistance("Both.ThumbTipDistance", HandJointId.HandThumbTip, HandJointId.HandThumbTip, HandFinger.Thumb);
            bool direction = PairPositions(frame, HandJointId.HandIndex1, HandJointId.HandIndex1, out var lb, out var rb) &
                             PairPositions(frame, HandJointId.HandIndexTip, HandJointId.HandIndexTip, out var lt, out var rt);
            direction &= (lt - lb).sqrMagnitude > 1e-8f && (rt - rb).sqrMagnitude > 1e-8f;
            output.Add(new SealFeature("Both.IndexDirectionAngle", "deg", Vector3.Angle(lt - lb, rt - rb),
                pair && direction, confident && frame.left.fingerConfidence[1] && frame.right.fingerConfidence[1]));
            // Left wrist frame removes global translation/rotation while keeping left/right relation.
            Quaternion inverse = Quaternion.Inverse(frame.left.joints[(int)HandJointId.HandWristRoot].rotation);
            Vector3 offset = inverse * (r - l);
            output.Add(new SealFeature("Both.RightWristInLeft.X", "m", offset.x, pair && wrists, confident));
            output.Add(new SealFeature("Both.RightWristInLeft.Y", "m", offset.y, pair && wrists, confident));
            output.Add(new SealFeature("Both.RightWristInLeft.Z", "m", offset.z, pair && wrists, confident));

            void AddDistance(string id, HandJointId lj, HandJointId rj, HandFinger finger)
            {
                bool valid = PairPositions(frame, lj, rj, out var a, out var b);
                output.Add(new SealFeature(id, "m", Vector3.Distance(a, b), pair && valid,
                    confident && frame.left.fingerConfidence[(int)finger] && frame.right.fingerConfidence[(int)finger]));
            }
        }

        private void AddFingers(string side, HandSnapshot snapshot, SnapshotHand hand, List<SealFeature> output)
        {
            for (int f = 0; f < 5; ++f)
            for (int k = 0; k < 4; ++k)
            {
                var finger = (HandFinger)f; var feature = (FingerFeature)k;
                // SDK returns 0 for these unsupported combinations; expose them as N/A instead.
                if ((f == 4 && feature == FingerFeature.Abduction) || (f == 0 && feature == FingerFeature.Opposition)) continue;
                bool valid = hand.IsTrackedDataValid && snapshot.wristPosesValid;
                bool confidence = snapshot.fingerConfidence[f];
                foreach (var joint in HandJointUtils.FingerToJointList[f]) valid &= snapshot.jointValid[(int)joint];
                if (feature == FingerFeature.Abduction)
                {
                    confidence &= snapshot.fingerConfidence[f + 1];
                    foreach (var joint in HandJointUtils.FingerToJointList[f + 1]) valid &= snapshot.jointValid[(int)joint];
                }
                if (feature == FingerFeature.Opposition)
                {
                    confidence &= snapshot.fingerConfidence[0];
                    valid &= snapshot.jointValid[(int)HandJointId.HandThumbTip];
                }
                float value = valid ? shapes.GetValue(finger, feature, hand) : 0;
                output.Add(new SealFeature($"{side}.{finger}.{feature}", feature == FingerFeature.Opposition ? "m@scale1" : "deg",
                    value, valid, confidence));
            }
        }

        private static bool PairPositions(SealFrame frame, HandJointId lj, HandJointId rj, out Vector3 l, out Vector3 r)
        {
            l = frame.left.joints[(int)lj].position; r = frame.right.joints[(int)rj].position;
            return frame.left.jointValid[(int)lj] && frame.right.jointValid[(int)rj];
        }
        private static bool PalmNormal(HandSnapshot hand, bool isLeft, out Vector3 normal)
        {
            int wrist = (int)HandJointId.HandWristRoot, index = (int)HandJointId.HandIndex1, pinky = (int)HandJointId.HandPinky1;
            normal = Vector3.Cross(hand.joints[index].position - hand.joints[wrist].position,
                hand.joints[pinky].position - hand.joints[wrist].position) * (isLeft ? 1 : -1);
            bool valid = hand.jointValid[wrist] && hand.jointValid[index] && hand.jointValid[pinky] && normal.sqrMagnitude > 1e-10f;
            normal.Normalize(); return valid;
        }
    }
}
