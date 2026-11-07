using System;
using System.Collections.Generic;
using Oculus.Interaction.Input;
using Oculus.Interaction.Input.UnityXR;
using UnityEngine;
using UnityEngine.XR.Hands;
using OpenXRRuntime = UnityEngine.XR.OpenXR.OpenXRRuntime;
using MetaHand = Oculus.Interaction.Input.Hand;

namespace NarutoVR.SealLab
{
    // SDK converts joint conventions/scale; this gate preserves XR Hands joint validity and
    // per-hand update times so cached SDK joints cannot masquerade as current measurements.
    [DefaultExecutionOrder(100)]
    public sealed class SealXRHandsSource : MonoBehaviour
    {
        [Header("一、通用手部追踪来源")]
        [InspectorName("左手实际追踪组件")]
        [Tooltip("绑定直接使用Unity XR Hands数据的左手Hand组件，不能使用保留上一姿态或合成的手。")]
        public MetaHand leftHand;
        [InspectorName("右手实际追踪组件")]
        [Tooltip("绑定直接使用Unity XR Hands数据的右手Hand组件；左右手必须分别绑定。")]
        public MetaHand rightHand;
        public bool IsConfigured => Direct(leftHand) && Direct(rightHand) && leftHand != rightHand;
        public string Status => subsystem == null ? "等待手追踪子系统" :
            subsystem.running ? "手追踪子系统运行中 / 质量由关节有效性推导" : "手追踪子系统已停止";
        public string RuntimeName => string.IsNullOrEmpty(OpenXRRuntime.name) ? "尚未初始化" : OpenXRRuntime.name;

        private sealed class State
        {
            public readonly bool[] valid = new bool[Constants.NUM_HAND_JOINTS];
            public int version;
            public double observed;
            public bool tracked, hasSample;
            public void Clear() { Array.Clear(valid, 0, valid.Length); tracked = hasSample = false; ++version; }
        }
        private readonly State left = new(), right = new();
        private readonly List<XRHandSubsystem> subsystems = new();
        private XRHandSubsystem subsystem;
        private double nextBind;
        private static bool Direct(MetaHand hand) => hand && hand.GetType() == typeof(MetaHand) &&
            hand.GetComponent<FromUnityXRHandDataSource>() != null;

        private void Update()
        {
            if (subsystem != null && subsystem.running) return;
            if (Time.realtimeSinceStartupAsDouble < nextBind) return;
            nextBind = Time.realtimeSinceStartupAsDouble + 0.5;
            Unbind();
            SubsystemManager.GetSubsystems(subsystems);
            foreach (var candidate in subsystems)
                if (candidate.running) { subsystem = candidate; break; }
            if (subsystem == null) return;
            subsystem.updatedHands += Updated;
            subsystem.trackingLost += Lost;
        }
        private void OnDisable() => Unbind();
        private void Unbind()
        {
            if (subsystem != null) { subsystem.updatedHands -= Updated; subsystem.trackingLost -= Lost; }
            subsystem = null; left.Clear(); right.Clear();
        }
        private void Lost(XRHand hand) => (hand.handedness == UnityEngine.XR.Hands.Handedness.Left ? left : right).Clear();
        private void Updated(XRHandSubsystem source, XRHandSubsystem.UpdateSuccessFlags flags, XRHandSubsystem.UpdateType type)
        {
            if (type != XRHandSubsystem.UpdateType.Dynamic) return;
            Read(source.leftHand, left, (flags & XRHandSubsystem.UpdateSuccessFlags.LeftHandJoints) != 0);
            Read(source.rightHand, right, (flags & XRHandSubsystem.UpdateSuccessFlags.RightHandJoints) != 0);
        }
        private static void Read(XRHand hand, State state, bool jointsUpdated)
        {
            if (!hand.isTracked) { if (state.tracked || state.hasSample) state.Clear(); return; }
            // A root-only update is not a new finger pose. Let the old joint sample expire.
            if (!jointsUpdated) return;
            state.observed = Time.realtimeSinceStartupAsDouble;
            state.hasSample = state.tracked = true; ++state.version;
            Array.Clear(state.valid, 0, state.valid.Length);
            for (int i = XRHandJointID.BeginMarker.ToIndex(); i < XRHandJointID.EndMarker.ToIndex(); ++i)
            {
                var id = XRHandJointIDUtility.FromIndex(i);
                var joint = hand.GetJoint(id);
                int mapped = MetaJointIndex(id);
                if (mapped >= 0 && mapped < state.valid.Length)
                    state.valid[mapped] = (joint.trackingState & XRHandJointTrackingState.Pose) != 0 &&
                        joint.TryGetPose(out var pose) && SealLab.FinitePose(pose);
            }
        }
        public static int MetaJointIndex(XRHandJointID id) => id switch
        {
            XRHandJointID.Palm => (int)HandJointId.HandPalm,
            XRHandJointID.Wrist => (int)HandJointId.HandWristRoot,
            _ => id.ToIndex()
        };

        public HandSnapshot Capture(bool isLeft, Transform origin, double now, double staleAfterSeconds)
        {
            var state = isLeft ? left : right;
            var hand = isLeft ? leftHand : rightHand;
            bool connected = isActiveAndEnabled && subsystem != null && subsystem.running;
            bool fresh = connected && state.hasSample && now - state.observed <= staleAfterSeconds;
            var snapshot = connected && fresh && state.tracked && hand && hand.isActiveAndEnabled && hand.Started &&
                hand.GetComponent<FromUnityXRHandDataSource>().isActiveAndEnabled ?
                SealLab.CaptureHand(hand, origin, state.observed, now, staleAfterSeconds) : new HandSnapshot();
            snapshot.version = state.version; snapshot.observedAt = state.observed;
            snapshot.connected = connected; snapshot.fresh = fresh;
            snapshot.tracked &= connected && fresh && state.tracked;
            ApplyValidity(snapshot, state.valid);
            return snapshot;
        }

        public static void ApplyValidity(HandSnapshot snapshot, bool[] valid)
        {
            snapshot.confidenceSource = "derived joint validity (not Meta native confidence)";
            bool available = snapshot.connected && snapshot.tracked && snapshot.fresh;
            for (int i = 0; i < snapshot.jointValid.Length; ++i)
                snapshot.jointValid[i] &= available && valid[i];
            bool wrist = snapshot.jointValid[(int)HandJointId.HandWristRoot];
            snapshot.tracked &= wrist;
            snapshot.wristPosesValid &= available && wrist;
            snapshot.highConfidence = available && wrist;
            for (int f = 0; f < 5; ++f)
            {
                bool complete = available && wrist;
                foreach (var joint in HandJointUtils.FingerToJointList[f]) complete &= snapshot.jointValid[(int)joint];
                snapshot.fingerConfidence[f] = complete;
                snapshot.highConfidence &= complete;
            }
        }
    }
}
