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

        public void ExtractLeft(HandSnapshot snapshot, List<SealFeature> output)
        {
            output.Clear();
            left.Set(snapshot);
            if (snapshot != null) AddFingers("L", snapshot, left, output);
        }

        public void Extract(SealFrame frame, List<SealFeature> output, bool pico = false)
        {
            output.Clear(); left.Set(frame.left); right.Set(frame.right);
            AddFingers("L", frame.left, left, output, pico);
            AddFingers("R", frame.right, right, output, pico);
            bool pair = left.IsTrackedDataValid && right.IsTrackedDataValid &&
                        System.Math.Abs(frame.left.observedAt - frame.right.observedAt) <= MaximumHandSkew;
            bool confident = pair && (pico || left.IsHighConfidence && right.IsHighConfidence);
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
                pair && direction, confident && (pico || frame.left.fingerConfidence[1] && frame.right.fingerConfidence[1])));
            if (pico)
            {
                bool lProximal = IndexSegments(frame.left, out var lFirst, out var lSecond);
                bool rProximal = IndexSegments(frame.right, out var rFirst, out var rSecond);
                output.Add(new SealFeature("L.Index.ProximalBend", "deg", Vector3.Angle(lFirst, lSecond), left.IsTrackedDataValid && lProximal, lProximal));
                output.Add(new SealFeature("R.Index.ProximalBend", "deg", Vector3.Angle(rFirst, rSecond), right.IsTrackedDataValid && rProximal, rProximal));
                bool directionValid = pair && lProximal && rProximal;
                output.Add(new SealFeature("Both.IndexProximalDirectionAngle", "deg", Vector3.Angle(lFirst + lSecond, rFirst + rSecond),
                    directionValid, directionValid));
            }
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
                    confident && (pico || frame.left.fingerConfidence[(int)finger] && frame.right.fingerConfidence[(int)finger])));
            }
        }

        private void AddFingers(string side, HandSnapshot snapshot, SnapshotHand hand, List<SealFeature> output, bool pico = false)
        {
            for (int f = 0; f < 5; ++f)
            for (int k = 0; k < 4; ++k)
            {
                var finger = (HandFinger)f; var feature = (FingerFeature)k;
                // SDK returns 0 for these unsupported combinations; expose them as N/A instead.
                if ((f == 4 && feature == FingerFeature.Abduction) || (f == 0 && feature == FingerFeature.Opposition)) continue;
                bool valid = hand.IsTrackedDataValid && snapshot.wristPosesValid;
                bool confidence = pico || snapshot.fingerConfidence[f];
                if (pico)
                {
                    var proximal = HandJointUtils.GetHandFingerProximal(finger);
                    if (feature == FingerFeature.Curl || feature == FingerFeature.Flexion)
                        valid &= snapshot.jointValid[(int)proximal];
                    if (feature != FingerFeature.Flexion)
                        foreach (var joint in shapes.GetJointsAffected(finger, feature))
                            valid &= snapshot.jointValid[(int)joint];
                }
                else
                {
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
                }
                float value = valid ? shapes.GetValue(finger, feature, hand) : 0;
                output.Add(new SealFeature($"{side}.{finger}.{feature}", feature == FingerFeature.Opposition ? "m@scale1" : "deg",
                    value, valid, confidence));
            }
        }

        private static bool IndexSegments(HandSnapshot hand, out Vector3 first, out Vector3 second)
        {
            int a = (int)HandJointId.HandIndex1, b = (int)HandJointId.HandIndex2, c = (int)HandJointId.HandIndex3;
            first = hand.joints[b].position - hand.joints[a].position;
            second = hand.joints[c].position - hand.joints[b].position;
            return hand.jointValid[a] && hand.jointValid[b] && hand.jointValid[c] &&
                first.sqrMagnitude > 1e-8f && second.sqrMagnitude > 1e-8f && (first + second).sqrMagnitude > 1e-8f;
        }

        private static bool PairPositions(SealFrame frame, HandJointId lj, HandJointId rj, out Vector3 l, out Vector3 r)
        {
            l = frame.left.joints[(int)lj].position; r = frame.right.joints[(int)rj].position;
            return frame.left.jointValid[(int)lj] && frame.right.jointValid[(int)rj];
        }
        internal static bool PalmNormal(HandSnapshot hand, bool isLeft, out Vector3 normal)
        {
            int wrist = (int)HandJointId.HandWristRoot, index = (int)HandJointId.HandIndex1, pinky = (int)HandJointId.HandPinky1;
            normal = Vector3.Cross(hand.joints[index].position - hand.joints[wrist].position,
                hand.joints[pinky].position - hand.joints[wrist].position) * (isLeft ? 1 : -1);
            bool valid = hand.jointValid[wrist] && hand.jointValid[index] && hand.jointValid[pinky] && normal.sqrMagnitude > 1e-10f;
            normal.Normalize(); return valid;
        }
    }
}
