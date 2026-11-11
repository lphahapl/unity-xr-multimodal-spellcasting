using System;
using NarutoVR.SealLab;
using Oculus.Interaction.Input;
using UnityEngine;

namespace NarutoVR.SealCombat.Rpg
{
    /// <summary>单个玩家的手部采样、输入占用和跨帧请求代次。</summary>
    public sealed class PlayerInputController
    {
        readonly LinearRpgController player;
        UnityEngine.Object interactionOwner;
        long activeClaim, nextClaim;
        double inputReadyAt = double.NegativeInfinity;
        OfficialHandPoseDetector handPoses;
        public long Generation { get; private set; }
        public string HandPoseStatus => handPoses?.Status ?? "官方手形：等待初始化";
        public bool UiOwnsInput => player.wristMenu && player.wristMenu.OwnsInput;
        public bool InteractionOwnsHand => interactionOwner;
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
            handPoses?.Reset();
        }

        public bool CanCast(out string reason)
        {
            reason = player.StateCommitInProgress ? "玩家状态正在提交" : !player.CanAct ? "当前无法施法" : UiOwnsInput ? "正在操作教学界面；手部恢复中立后再施法" :
                InteractionOwnsHand ? "右手正在操作机关或持有物品" :
                player.locomotion && player.locomotion.NavigationActive ? "导航准备中：放下左手后再开始结印或吟唱" : null;
            return reason == null;
        }
        public bool CanMove(bool dodge, out string reason)
        {
            reason = MovementBlockReason;
            if (reason == null && dodge && (InteractionOwnsHand || player.skills.effects.HasRightHeldObject))
                reason = "右手正在持有物品，先释放或收起再闪避";
            return reason == null;
        }
        public bool CanInteract(out string reason)
        {
            reason = player.StateCommitInProgress ? "玩家状态正在提交" : !player.CanAct ? "角色当前不能操作物品" : UiOwnsInput ? "腕部界面占用输入" :
                InteractionOwnsHand || (player.skills && player.skills.effects && player.skills.effects.HasRightHeldObject)
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
        public NavigationHandInput ReadNavigationFrame(double now)
        {
            var result = new NavigationHandInput(); var frame = player.skills.lab.Frame;
            if (frame == null) { handPoses?.Reset(); return result; }
            if (handPoses == null) handPoses = new OfficialHandPoseDetector(player.handPoseSettings);
            handPoses.Update(frame, player.skills.lab.Features, now, player.locomotion.gestures.sampleMaxAge, player.locomotion.gestures.sampleMaxGap, inputReadyAt);
            result.leftObserved = frame.left.observedAt; result.rightObserved = frame.right.observedAt;
            result.leftValid = Wrist(frame.left, now, out result.leftWrist);
            result.rightValid = Wrist(frame.right, now, out result.rightWrist);
            int index = (int)HandJointId.HandIndexTip;
            if (result.leftValid && frame.left.jointValid[index]) result.leftIndex = World(frame.left.joints[index].position);
            result.leftPointing = result.leftValid && frame.left.jointValid[index] && handPoses.Left.pointing;
            int indexBase = (int)HandJointId.HandIndex1;
            if (result.leftPointing && frame.left.jointValid[indexBase])
            {
                Vector3 direction = result.leftIndex - World(frame.left.joints[indexBase].position);
                float length = direction.magnitude;
                result.leftRayValid = RpgGestureState.Finite(direction) && length >= .02f && length <= .16f;
                if (result.leftRayValid)
                {
                    result.leftRayOrigin = result.leftIndex;
                    result.leftRayDirection = direction / length;
                }
            }
            result.rightOpen = result.rightValid && handPoses.Right.open;
            result.rightMotionOwnedBySpell = player.skills.effects && player.skills.effects.RightMotionOwnedBySpell;
            result.bothHandsLow = result.leftValid && result.rightValid && Math.Abs(result.leftObserved - result.rightObserved) <= SealFeatureExtractor.MaximumHandSkew &&
                result.leftWrist.y < player.locomotion.head.position.y - player.lowHandsBelowHead && result.rightWrist.y < player.locomotion.head.position.y - player.lowHandsBelowHead;
            return result;
        }
        public WristMenuInput ReadWristMenuFrame(NavigationHandInput navigation)
        {
            var f = new WristMenuInput { leftValid = navigation.leftValid, rightValid = navigation.rightValid,
                leftObserved = navigation.leftObserved, rightObserved = navigation.rightObserved,
                leftWrist = navigation.leftWrist, leftClosed = handPoses != null && handPoses.Left.valid && handPoses.Left.closed,
                leftPointing = navigation.leftPointing, rightOpen = navigation.rightOpen,
                rightPointing = handPoses != null && handPoses.Right.valid && handPoses.Right.pointing,
                rightClosed = handPoses != null && handPoses.Right.valid && handPoses.Right.closed,
                headPosition = player.locomotion.head.position, headForward = player.locomotion.head.forward };
            var frame = player.skills.lab.Frame;
            if (frame == null) return f;
            f.source = frame.source;
            int wrist = (int)HandJointId.HandWristRoot, index = (int)HandJointId.HandIndex1,
                pinky = (int)HandJointId.HandPinky1, tip = (int)HandJointId.HandIndexTip, thumb = (int)HandJointId.HandThumbTip;
            Quaternion origin = player.skills.lab.recordingOrigin ? player.skills.lab.recordingOrigin.rotation : Quaternion.identity;
            double menuNow=Time.realtimeSinceStartupAsDouble;
            var leftHand=frame.left;
            f.leftValid=leftHand.connected && leftHand.tracked && leftHand.jointValid[wrist] && leftHand.observedAt > inputReadyAt &&
                WristMenuState.Fresh(leftHand.fresh,leftHand.observedAt,menuNow,player.wristMenu.settings.maxAge);
            if(f.leftValid){
                f.leftWrist=World(leftHand.joints[wrist].position);
                f.leftValid=WristMenuState.Finite(f.leftWrist);
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
                WristMenuState.Fresh(true,hand.observedAt,menuNow,player.wristMenu.settings.maxAge);
            f.tipUnavailable=true;
            if(!f.rightValid)return f;
            Vector3 wristWorld=World(hand.joints[wrist].position);
            if(!WristMenuState.Finite(wristWorld)){f.rightValid=false;return f;}
            if(hand.jointValid[tip]){
                f.rightTip=World(hand.joints[tip].position);
                f.tipUnavailable=!WristMenuState.Finite(f.rightTip);
            }
            int middle=(int)HandJointId.HandMiddle1;
            bool hasIndex=hand.jointValid[index],hasMiddle=hand.jointValid[middle];
            if(hasIndex || hasMiddle){
                Vector3 basePoint=hasIndex && hasMiddle ? (hand.joints[index].position+hand.joints[middle].position)*.5f
                    : hand.joints[hasIndex?index:middle].position;
                Vector3 axis=World(basePoint)-wristWorld;
                if(WristMenuState.Finite(axis) && axis.magnitude>=.02f && axis.magnitude<=.16f){
                    f.rayOrigin=wristWorld;f.rayDirection=axis.normalized;f.rayValid=true;f.raySource=hasIndex && hasMiddle?"骨骼·腕到双指根":hasIndex?"骨骼·腕到食指根":"骨骼·腕到中指根";
                }
            }
            if(!f.tipUnavailable && hand.jointValid[thumb]){
                f.pinchDistance=Vector3.Distance(f.rightTip,World(hand.joints[thumb].position));
                f.pinchValid=float.IsFinite(f.pinchDistance);
                if(f.pinchValid)f.pinchSource="骨骼·拇食指距离";
            }
            WristHandChannels.Apply(ref f,player.skills.lab.recordingOrigin,wristWorld,player.wristMenu.settings,
                player.skills.lab.xrHandsSource,player.skills.lab.rightTrackedHand as IHand);
            f.rayAnchorValid=f.rayValid;
            return f;
        }
        public ThrowGripSample ReadThrowGrip(bool left, HandThrowSettings settings, double now)
        {
            var result = new ThrowGripSample(); var frame = player.skills.lab.Frame;
            if (frame == null || handPoses == null) return result;
            HandSnapshot hand = left ? frame.left : frame.right;
            if (!Wrist(hand, now, out var wrist)) return result;
            int indexBase=(int)HandJointId.HandIndex1, middleBase=(int)HandJointId.HandMiddle1;
            if(hand.jointValid[indexBase] && hand.jointValid[middleBase]) {
                var midpoint=(hand.joints[indexBase].position+hand.joints[middleBase].position)*.5f;
                var axis=World(midpoint)-wrist;
                if(axis.sqrMagnitude>=.0004f && axis.sqrMagnitude<=.0256f) result.wristToKnucklesWorld=axis.normalized;
            }
            var shape = left ? handPoses.Left : handPoses.Right;
            result.observed = hand.observedAt;
            result.fistValid = shape.valid; result.fistClosed = shape.closed;
            result.fistOpen = settings.allowPartialFistRelease ? shape.releaseOpen : shape.open;
            int thumb = (int)HandJointId.HandThumbTip, index = (int)HandJointId.HandIndexTip;
            if (hand.jointValid[thumb] && hand.jointValid[index])
            {
                float distance = Vector3.Distance(World(hand.joints[thumb].position), World(hand.joints[index].position));
                result.pinchValid = float.IsFinite(distance);
                result.pinchClosed = distance <= settings.pinchCloseDistance;
                result.pinchOpen = distance >= settings.pinchOpenDistance;
            }
            return result;
        }
        bool Wrist(HandSnapshot h, double now, out Vector3 position)
        {
            position = default; int wrist = (int)HandJointId.HandWristRoot;
            if (h == null || !h.connected || !h.tracked || !h.fresh || !h.highConfidence || !h.jointValid[wrist] ||
                h.observedAt <= inputReadyAt || now - h.observedAt < 0 || now - h.observedAt > player.locomotion.gestures.sampleMaxAge) return false;
            position = World(h.joints[wrist].position); return float.IsFinite(position.x) && float.IsFinite(position.y) && float.IsFinite(position.z);
        }
        Vector3 World(Vector3 position) => player.skills.lab.recordingOrigin ? player.skills.lab.recordingOrigin.TransformPoint(position) : position;
    }
}
