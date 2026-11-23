using System;
using NarutoVR.SealLab;
using Oculus.Interaction.Input;
using UnityEngine;

namespace NarutoVR.SealCombat.Rpg
{
    /// <summary>玩家本次观测的世界坐标、来源、逐手时间戳与捏合通道；判定阈值由动作的使用方提供。</summary>
    public struct PlayerHandObservation
    {
        public bool tipUnavailable,systemPinch,systemPressed,pinchTransition;
        public string raySource,pinchSource;
        public bool PinchDown(float closeDistance)=>pinchValid && !pinchTransition && (systemPinch?systemPressed:pinchDistance<=closeDistance);
        public bool PinchUp(float openDistance)=>pinchValid && !pinchTransition && (systemPinch?!systemPressed:pinchDistance>=openDistance);
        public bool leftValid, leftClosed, rightValid, rightPointing, rightOpen, rightClosed, leftPointing, wristFacing;
        public double leftObserved, rightObserved;
        public Vector3 leftWrist, rightTip, rayOrigin, rayDirection, headPosition, headForward;
        public Vector3 rightWrist, rightPalmNormal;
        public bool gripPoseValid;
        public Vector3 gripPosition;
        public Quaternion gripRotation;
        public bool rightPalmValid;
        public Quaternion leftRotation, mountRotation;
        public bool mountValid;
        public bool rayValid, rayAnchorValid, pinchValid;
        public float pinchDistance;
        public string source;
        public static bool Fresh(bool valid, double observed, double now, double maxAge) => valid && double.IsFinite(observed) && now >= observed && now-observed <= maxAge;
        public static bool Finite(Vector3 v) => float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);
    }

    /// <summary>单个玩家的手部采样、输入占用和跨帧请求代次。</summary>
    public sealed class PlayerInputController
    {
        readonly LinearRpgController player;
        UnityEngine.Object interactionOwner;
        long activeClaim, nextClaim;
        string observationSource;
        double inputReadyAt = double.NegativeInfinity;
        OfficialHandPoseDetector handPoses;
        public long Generation { get; private set; }
        public string HandPoseStatus => handPoses?.Status ?? "官方手形：等待初始化";
        public bool UiOwnsInput => player.wristMenu && player.wristMenu.OwnsInput;
        public bool InteractionOwnsHand => interactionOwner;
        public PlayerHandObservation LatestHand { get; private set; }
        NarutoVR.SealLab.SealLab Tracking => player.handTracking ? player.handTracking : player.skills ? player.skills.lab : null;
        bool RightHandHeld => player.skills && player.skills.effects && player.skills.effects.HasRightHeldObject;
        internal bool RightHandBlocksMotionGesture => InteractionOwnsHand || (RightHandHeld && !player.skills.effects.HasRightThrowObject);
        internal NavigationHandInput SampleHands(double now)
        {
            var frame = Tracking ? Tracking.Frame : null;
            if (frame != null && frame.source != null)
            {
                if (observationSource != null && observationSource != frame.source) Invalidate();
                observationSource = frame.source;
            }
            var navigation = ReadNavigationFrame(frame, now);
            LatestHand = ReadHandFrame(navigation, frame, now);
            return navigation;
        }
        internal PlayerInputController(LinearRpgController player) { this.player = player; }
        public bool IsCurrent(long generation) => generation == Generation && player && player.CanAct;
        internal void InvalidateRequests(bool clearMatch = false)
        {
            Generation++;
            if (player.skills) player.skills.AdvanceInputOwnership(clearMatch);
        }
        internal void Invalidate()
        {
            InvalidateRequests();
            inputReadyAt = Time.realtimeSinceStartupAsDouble;
            interactionOwner = null;
            activeClaim = 0;
            LatestHand = default;
            handPoses?.Reset();
        }

        public bool CanCast(out string reason) => CanCast(out reason, includeWristMenu: true);

        // Reading the spell ledger temporarily owns menu input. Its cells should still report
        // which spells will be ready when the player lowers the menu.
        internal bool CanCast(out string reason, bool includeWristMenu)
        {
            reason = player.StateCommitInProgress ? "玩家状态正在提交" : !player.CanAct ? "当前无法施法" : player.GlyphBlocksCombat ? "墨规笔占用输入" : includeWristMenu && UiOwnsInput ? "正在操作教学界面；手部恢复中立后再施法" :
                InteractionOwnsHand ? "右手正在操作机关或持有物品" :
                player.locomotion && player.locomotion.NavigationActive ? "导航准备中：放下左手后再开始结印或吟唱" : null;
            return reason == null;
        }
        public bool CanMove(bool dodge, out string reason)
        {
            reason = MovementBlockReason;
            if (reason == null && dodge && (InteractionOwnsHand || RightHandHeld))
                reason = "右手正在持有物品，先释放或收起再闪避";
            return reason == null;
        }
        public bool CanInteract(out string reason)
        {
            reason = player.StateCommitInProgress ? "玩家状态正在提交" : !player.CanAct ? "角色当前不能操作物品" : UiOwnsInput ? "腕部界面占用输入" :
                InteractionOwnsHand || RightHandHeld
                    ? "右手已持有物品" : null;
            return reason == null;
        }
        public bool CanCancel(out string reason)
        {
            reason = player.IsReady ? null : "关卡未就绪";
            return reason == null;
        }
        public string MovementBlockReason
        {
            get
            {
                if (player.StateCommitInProgress) return "玩家状态正在提交";
                if (!player.IsReady || player.Player == null) return "关卡未就绪";
                if (player.InputPaused || Time.timeScale <= 0) return "游戏已暂停，恢复播放后才能移动";
                if (!player.Player.Alive) return "生命为零，点击「恢复检查点」后可继续移动";
                return UiOwnsInput ? "腕部界面占用输入，请先结束点击或拖动并关闭主菜单" : null;
            }
        }

        public bool TryAcquireInteraction(UnityEngine.Object claimant, out long claim)
        {
            claim = 0;
            if (!claimant || !CanInteract(out _)) return false;
            interactionOwner = claimant;
            activeClaim = claim = ++nextClaim;
            InvalidateRequests(true);
            return true;
        }
        public bool OwnsInteraction(UnityEngine.Object claimant, long claim) =>
            claim != 0 && claim == activeClaim && ReferenceEquals(interactionOwner, claimant);
        public void ReleaseInteraction(UnityEngine.Object claimant, long claim)
        {
            if (!OwnsInteraction(claimant, claim)) return;
            interactionOwner = null;
            activeClaim = 0;
            InvalidateRequests(true);
        }
        public NavigationHandInput ReadNavigationFrame(double now) => ReadNavigationFrame(Tracking ? Tracking.Frame : null, now);

        NavigationHandInput ReadNavigationFrame(SealFrame frame, double now)
        {
            var result = new NavigationHandInput();
            if (frame == null) { handPoses?.Reset(); return result; }
            if (handPoses == null) handPoses = new OfficialHandPoseDetector(player.handPoseSettings);
            handPoses.Update(frame, Tracking.Features, now, player.locomotion.gestures.sampleMaxAge, player.locomotion.gestures.sampleMaxGap, inputReadyAt);
            result.leftObserved = frame.left.observedAt; result.rightObserved = frame.right.observedAt;
            result.leftValid = Wrist(frame.left, now, out result.leftWrist);
            result.rightValid = Wrist(frame.right, now, out result.rightWrist);
            int index = (int)HandJointId.HandIndexTip;
            if (result.leftValid && frame.left.jointValid[index]) result.leftIndex = World(frame.left.joints[index].position);
            bool leftPoseReady = result.leftValid && frame.left.jointValid[index] && handPoses.Left.valid;
            result.leftPointing = leftPoseReady && handPoses.Left.pointing;
            result.leftNavigationPointing = leftPoseReady && handPoses.Left.navigationPointing;
            result.leftPoseUnconfirmed = !leftPoseReady;
            result.leftClosed = leftPoseReady && handPoses.Left.closed;
            result.leftOpen = leftPoseReady && handPoses.Left.open;
            int indexBase = (int)HandJointId.HandIndex1;
            int middleBase = (int)HandJointId.HandMiddle1;
            if (result.leftClosed && frame.left.jointValid[indexBase] && frame.left.jointValid[middleBase])
            {
                Vector3 knuckles = (World(frame.left.joints[indexBase].position) + World(frame.left.joints[middleBase].position)) * .5f;
                Vector3 direction = knuckles - result.leftWrist;
                float length = direction.magnitude;
                if (RpgGestureState.Finite(direction) && length >= .02f && length <= .16f)
                    result.leftFistDirection = direction / length;
            }
            bool previewIndex = player.locomotion.PickingGround && result.leftValid && frame.left.jointValid[index] && !result.leftOpen;
            if ((result.leftNavigationPointing || previewIndex) && frame.left.jointValid[indexBase])
            {
                Vector3 knuckle = World(frame.left.joints[indexBase].position);
                Vector3 direction = result.leftIndex - knuckle;
                float length = direction.magnitude;
                if (RpgGestureState.Finite(direction) && length >= .02f && length <= .16f)
                {
                    int middle = (int)HandJointId.HandIndex2;
                    if (previewIndex && frame.left.jointValid[middle])
                    {
                        Vector3 joint = World(frame.left.joints[middle].position);
                        Vector3 proximal = joint - knuckle, distal = result.leftIndex - joint;
                        result.leftIndexAim = proximal.sqrMagnitude > .000025f && distal.sqrMagnitude > .000025f &&
                            Vector3.Dot(proximal.normalized, distal.normalized) >= .85f;
                    }
                    result.leftRayValid = result.leftNavigationPointing || result.leftIndexAim;
                    if (result.leftRayValid)
                    {
                        result.leftRayOrigin = result.leftIndex;
                        result.leftRayDirection = direction / length;
                    }
                }
            }
            result.rightOpen = result.rightValid && handPoses.Right.open;
            result.rightMotionOwnedBySpell = player.skills.effects && player.skills.effects.RightMotionOwnedBySpell;
            result.bothHandsLow = result.leftValid && result.rightValid && Math.Abs(result.leftObserved - result.rightObserved) <= SealFeatureExtractor.MaximumHandSkew &&
                result.leftWrist.y < player.locomotion.head.position.y - player.lowHandsBelowHead && result.rightWrist.y < player.locomotion.head.position.y - player.lowHandsBelowHead;
            return result;
        }
        public PlayerHandObservation ReadWristMenuFrame(NavigationHandInput navigation) =>
            ReadHandFrame(navigation, Tracking ? Tracking.Frame : null, Time.realtimeSinceStartupAsDouble);

        PlayerHandObservation ReadHandFrame(NavigationHandInput navigation, SealFrame frame, double now)
        {
            var f = new PlayerHandObservation { leftValid = navigation.leftValid, rightValid = navigation.rightValid,
                leftObserved = navigation.leftObserved, rightObserved = navigation.rightObserved,
                leftWrist = navigation.leftWrist, leftClosed = handPoses != null && handPoses.Left.valid && handPoses.Left.closed,
                leftPointing = navigation.leftPointing, rightOpen = navigation.rightOpen,
                rightPointing = handPoses != null && handPoses.Right.valid && handPoses.Right.pointing,
                rightClosed = handPoses != null && handPoses.Right.valid && handPoses.Right.closed,
                headPosition = player.locomotion.head.position, headForward = player.locomotion.head.forward };
            if (frame == null) return f;
            f.source = frame.source;
            int wrist = (int)HandJointId.HandWristRoot, index = (int)HandJointId.HandIndex1,
                pinky = (int)HandJointId.HandPinky1, tip = (int)HandJointId.HandIndexTip, thumb = (int)HandJointId.HandThumbTip;
            Quaternion origin = Tracking.recordingOrigin ? Tracking.recordingOrigin.rotation : Quaternion.identity;
            var leftHand=frame.left;
            f.leftValid=leftHand.connected && leftHand.tracked && leftHand.jointValid[wrist] && leftHand.observedAt > inputReadyAt &&
                PlayerHandObservation.Fresh(leftHand.fresh,leftHand.observedAt,now,player.handSampleMaxAge);
            if(f.leftValid){
                f.leftWrist=World(leftHand.joints[wrist].position);
                f.leftValid=PlayerHandObservation.Finite(f.leftWrist);
            }
            if (f.leftValid)
            {
                f.leftRotation = origin*frame.left.joints[wrist].rotation;
                if (frame.left.jointValid[index] && frame.left.jointValid[pinky])
                {
                    Vector3 normal = Vector3.Cross(World(frame.left.joints[index].position)-f.leftWrist, World(frame.left.joints[pinky].position)-f.leftWrist);
                    Vector3 fingers = (World(frame.left.joints[index].position) + World(frame.left.joints[pinky].position)) * .5f - f.leftWrist;
                    f.mountValid = normal.sqrMagnitude > .0000001f && fingers.sqrMagnitude > .0001f;
                    if (f.mountValid) f.mountRotation = Quaternion.LookRotation(fingers, -normal);
                    // Both dorsal/palmar conventions are accepted; vendor-specific wrist axes stay in this adapter.
                    f.wristFacing = normal.sqrMagnitude > .0000001f && Mathf.Abs(Vector3.Dot(normal.normalized,(f.headPosition-f.leftWrist).normalized)) >= .2f;
                }
            }
            // Menu input needs only the joints used by each channel. Navigation's confidence
            // includes every finger and must not suppress a valid wrist when one tip is occluded.
            var hand=frame.right;
            f.rightValid=hand.connected && hand.tracked && hand.fresh && hand.jointValid[wrist] && hand.observedAt > inputReadyAt &&
                PlayerHandObservation.Fresh(true,hand.observedAt,now,player.handSampleMaxAge);
            f.tipUnavailable=true;
            if(!f.rightValid)return f;
            Vector3 wristWorld=World(hand.joints[wrist].position);
            if(!PlayerHandObservation.Finite(wristWorld)){f.rightValid=false;return f;}
            f.rightWrist = wristWorld;
            if (SealFeatureExtractor.PalmNormal(hand, false, out var normalInTrackingSpace))
            {
                f.rightPalmNormal = origin * normalInTrackingSpace;
                f.rightPalmValid = PlayerHandObservation.Finite(f.rightPalmNormal) && f.rightPalmNormal.sqrMagnitude > .9f;
            }
            if(hand.jointValid[tip]){
                f.rightTip=World(hand.joints[tip].position);
                f.tipUnavailable=!PlayerHandObservation.Finite(f.rightTip);
            }
            int middle=(int)HandJointId.HandMiddle1;
            bool hasIndex=hand.jointValid[index],hasMiddle=hand.jointValid[middle];
            if(hasIndex || hasMiddle){
                Vector3 basePoint=hasIndex && hasMiddle ? (hand.joints[index].position+hand.joints[middle].position)*.5f
                    : hand.joints[hasIndex?index:middle].position;
                Vector3 axis=World(basePoint)-wristWorld;
                if (f.rightPalmValid && axis.sqrMagnitude >= .0004f && axis.sqrMagnitude <= .0256f &&
                    Vector3.Cross(axis.normalized, f.rightPalmNormal).sqrMagnitude > .05f)
                {
                    // Knuckles remain a rigid pen anchor while fingertips curl or unpinch.
                    f.gripPosition = World(basePoint);
                    f.gripRotation = Quaternion.LookRotation(axis, -f.rightPalmNormal);
                    f.gripPoseValid = true;
                }
                if(PlayerHandObservation.Finite(axis) && axis.magnitude>=.02f && axis.magnitude<=.16f){
                    f.rayOrigin=wristWorld;f.rayDirection=axis.normalized;f.rayValid=true;f.raySource=hasIndex && hasMiddle?"骨骼·腕到双指根":hasIndex?"骨骼·腕到食指根":"骨骼·腕到中指根";
                }
            }
            if(!f.tipUnavailable && hand.jointValid[thumb]){
                f.pinchDistance=Vector3.Distance(f.rightTip,World(hand.joints[thumb].position));
                f.pinchValid=float.IsFinite(f.pinchDistance);
                if(f.pinchValid)f.pinchSource="骨骼·拇食指距离";
            }
            WristHandChannels.Apply(ref f,Tracking.recordingOrigin,wristWorld,player.handSampleMaxAge,
                Tracking.xrHandsSource,Tracking.rightTrackedHand as IHand);
            f.rayAnchorValid=f.rayValid;
            return f;
        }
        public ThrowGripSample ReadThrowGrip(bool left, HandThrowSettings settings, double now)
        {
            var tracking = Tracking;
            var frame = tracking ? tracking.Frame : null;
            if (frame == null || handPoses == null) return default;
            return ThrowGripSample.Read(left ? frame.left : frame.right, tracking.recordingOrigin, left ? handPoses.Left : handPoses.Right,
                settings, now, player.locomotion.gestures.sampleMaxAge, inputReadyAt);
        }
        bool Wrist(HandSnapshot h, double now, out Vector3 position)
        {
            position = default; int wrist = (int)HandJointId.HandWristRoot;
            if (h == null || !h.connected || !h.tracked || !h.fresh || !h.highConfidence || !h.jointValid[wrist] ||
                h.observedAt <= inputReadyAt || now - h.observedAt < 0 || now - h.observedAt > player.locomotion.gestures.sampleMaxAge) return false;
            position = World(h.joints[wrist].position); return float.IsFinite(position.x) && float.IsFinite(position.y) && float.IsFinite(position.z);
        }
        Vector3 World(Vector3 position) => Tracking.recordingOrigin ? Tracking.recordingOrigin.TransformPoint(position) : position;
    }
}
