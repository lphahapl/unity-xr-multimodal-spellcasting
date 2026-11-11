using System;
using System.Collections.Generic;
using NeuGameMRTools.Runtime.Components;
using UnityEngine;

namespace NarutoVR.SealCombat.Rpg
{
    [DefaultExecutionOrder(330)]
    public sealed class OpeningLeafPuzzle : MonoBehaviour
    {
        public LinearRpgController rpg;
        public Transform outerDisc, innerDisc, outerHandle, innerHandle, prizeRest, verifier, slotEntry;
        public GameObject leaf, lid;
        [InspectorName("实物铜叶标识")] public string leafId = "leaf.fangyuan";
        public SnapZone slot;
        /// <summary>机关解开后的完成阶段，用于恢复开盖状态；铜叶归属只读取实际持有记录。</summary>
        public RpgStageDefinition solvedStage;
        /// <summary>实际物品进度的来源，机关不读取通用会话内部存档结构。</summary>
        public RpgItemInventory inventory;
        [Min(.005f)] public float grabRadius = .055f, slotRadius = .026f, verifyRadius = .14f;
        [Header("铜叶入匣手感")]
        [InspectorName("导向头本地坐标（米）")] public Vector3 leafGuideLocal = new Vector3(0, .07f, 0);
        [InspectorName("铜叶松手指距（米）"), Min(.01f)] public float leafReleaseDistance = .035f;
        [InspectorName("入槽后松手宽限（秒）"), Min(0)] public float slotReleaseGrace = .25f;
        [InspectorName("松手时指尖退让距离（米）"), Min(.01f)] public float releaseFingerTravel = .10f;
        [InspectorName("短暂追踪丢失宽限（秒）"), Min(0)] public float leafTrackingGrace = .35f;
        [Range(1,15)] public float tolerance = 5;
        public float outerStartDegrees = 45, innerStartDegrees = 20;
        [Min(.1f)] public float settleSeconds = .6f;
        public event Action Solved, Collected;
        public bool IsSeated { get; private set; }
        public bool VerifiesWrist => IsSeated && rpg.wristMenu.wornDevice.gameObject.activeInHierarchy &&
            Vector3.Distance(slot.transform.position, verifier.position) <= verifyRadius;

        enum Grip { None, Outer, Inner, Leaf }
        Grip grip;
        long gripClaim;
        readonly DiscAlignmentState discs = new();
        float lastAngle, dropTime;
        bool activePuzzle, canCollect, taken, canInsert, pinchReleased;
        double trackingLostAt = double.NaN, slotContactAt = double.NegativeInfinity;
        Vector3 slotContactLocal;

        public void Restore(IReadOnlyList<string> completed)
        {
            if (slot.SnappedObject) slot.ReleaseObject();
            discs.Restore(System.Linq.Enumerable.Contains(completed, solvedStage.stageId), outerStartDegrees, innerStartDegrees);
            var state = inventory ? inventory.GetState(leafId) : ProgressionItemState.Missing;
            taken = state != ProgressionItemState.Missing;
            IsSeated = state == ProgressionItemState.Activated;
            DropTracking();
            activePuzzle = canCollect = canInsert = pinchReleased = false;
            dropTime = 0;
            SetDiscs();
            lid.SetActive(!discs.Solved);
            leaf.SetActive(true);
            PoseLeaf(IsSeated ? slot.transform : prizeRest);
            slot.enabled = false;
            // The leaf is moved by this custom pinch driver, not a Meta Grabbable.
            // Trigger auto-snap cannot infer its held state; only this driver's release may insert it.
            slot.AutomaticSnapping = false;
        }

        public void EnablePuzzle() { activePuzzle = true; }
        public void EnableReward() { canCollect = discs.Solved; }
        public void EnableInsertion() { canInsert = taken; }
        public bool IsEarnedLeaf(GameObject item) => taken && leaf == item;
        public void Seat(GameObject item)
        {
            if (!IsEarnedLeaf(item)) return;
            IsSeated = true;
            DropTracking();
            PoseLeaf(slot.transform);
        }

        void PoseLeaf(Transform anchor) => leaf.transform.SetPositionAndRotation(anchor.position, anchor.rotation);
        void SetDiscs()
        {
            outerDisc.localRotation = Quaternion.Euler(0, discs.OuterDegrees, 0);
            innerDisc.localRotation = Quaternion.Euler(0, discs.InnerDegrees, 0);
        }
        float Angle(Vector3 tip)
        {
            var local = outerDisc.parent.InverseTransformPoint(tip) - outerDisc.localPosition;
            return Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
        }
        bool Near(Vector3 point, Transform handle) => (point - handle.position).sqrMagnitude <= grabRadius * grabRadius;

        void LateUpdate()
        {
            if (IsSeated)
            {
                bool visible = slot.gameObject.activeInHierarchy;
                if (leaf.activeSelf != visible) leaf.SetActive(visible);
                if (visible) PoseLeaf(slot.transform);
                return;
            }
            if (!rpg || !rpg.CanAct || !rpg.wristMenu || rpg.UiInputOwned ||
                (grip != Grip.None && !rpg.Input.OwnsInteraction(this, gripClaim))) { DropTracking(); return; }
            var hand = rpg.wristMenu.LatestInput;
            double now = Time.realtimeSinceStartupAsDouble;
            bool valid = !hand.tipUnavailable && WristMenuState.Fresh(hand.rightValid, hand.rightObserved,
                now, rpg.wristMenu.settings.maxAge);
            if (!valid)
            {
                if (grip == Grip.Leaf)
                {
                    if (double.IsNaN(trackingLostAt)) trackingLostAt = now;
                    // Freeze the last valid pose. Missing samples are neither release nor movement.
                    if (now - trackingLostAt <= leafTrackingGrace) return;
                }
                DropTracking(); CheckAlignment(); Recall(); return;
            }
            if (!double.IsNaN(trackingLostAt) && now - trackingLostAt > leafTrackingGrace) DropTracking();
            trackingLostAt = double.NaN;
            var tip = hand.rightTip;
            bool release = grip == Grip.Leaf ? LeafReleased(hand) : hand.PinchUp(rpg.wristMenu.settings);
            if (release)
            {
                if (grip == Grip.Outer || grip == Grip.Inner) discs.Release();
                if (grip == Grip.Leaf) TryInsertReleasedLeaf(tip, now);
                ReleaseGrip();
                slotContactAt = double.NegativeInfinity;
                pinchReleased = true;
            }
            else if (pinchReleased && hand.PinchDown(rpg.wristMenu.settings))
            {
                pinchReleased = false;
                if (activePuzzle && !discs.Solved)
                {
                    if (Near(tip, outerHandle)) TryGrip(Grip.Outer);
                    else if (Near(tip, innerHandle)) TryGrip(Grip.Inner);
                    if (grip != Grip.None) discs.BeginTurn();
                    lastAngle = Angle(tip);
                }
                if ((canCollect || canInsert) && discs.Solved && !slot.SnappedObject && Near(tip, leaf.transform) && TryGrip(Grip.Leaf))
                {
                    slotContactAt = double.NegativeInfinity;
                    if (!taken) { taken = true; Collected?.Invoke(); }
                }
            }
            if (grip == Grip.Outer || grip == Grip.Inner)
            {
                float angle = Angle(tip);
                float delta = Mathf.DeltaAngle(lastAngle, angle);
                discs.Turn(grip == Grip.Outer, delta);
                lastAngle = angle;
                SetDiscs();
            }
            if (grip == Grip.Leaf)
            {
                leaf.transform.position = tip;
                if (canInsert && slot.isActiveAndEnabled && Vector3.Distance(tip, slotEntry.position) <= grabRadius * 2)
                    leaf.transform.rotation = slot.transform.rotation;
                if (canInsert && slot.isActiveAndEnabled)
                {
                    if (AtSlot(leaf.transform.position))
                    {
                        slotContactAt = now;
                        slotContactLocal = slot.transform.InverseTransformPoint(leaf.transform.position);
                    }
                    else if (Vector3.Distance(tip, slot.transform.TransformPoint(slotContactLocal)) > releaseFingerTravel)
                        slotContactAt = double.NegativeInfinity;
                }
                dropTime = 0;
            }
            else Recall();
            CheckAlignment();
        }

        void CheckAlignment()
        {
            if (!activePuzzle || !discs.TrySettle(grip == Grip.None, tolerance, settleSeconds, Time.deltaTime)) return;
            SetDiscs();
            lid.SetActive(false);
            Solved?.Invoke();
        }

        bool LeafReleased(WristMenuInput hand) => hand.systemPinch
            ? hand.PinchUp(rpg.wristMenu.settings)
            : hand.pinchValid && !hand.pinchTransition &&
              hand.pinchDistance >= Mathf.Max(leafReleaseDistance, rpg.wristMenu.settings.pinchClose + .005f);

        // Both the guide head and the existing root-at-entry gesture are valid. The renderer's
        // position is the last held pose, which should not jump when the fingers spread to release.
        bool AtSlot(Vector3 rootPosition)
        {
            Vector3 guideOffset = leaf.transform.TransformVector(leafGuideLocal);
            return (rootPosition - slotEntry.position).sqrMagnitude <= slotRadius * slotRadius ||
                (rootPosition + guideOffset - slotEntry.position).sqrMagnitude <= slotRadius * slotRadius;
        }

        void TryInsertReleasedLeaf(Vector3 finger, double now)
        {
            if (!canInsert || !slot.isActiveAndEnabled || slot.SnappedObject) return;
            float reach = releaseFingerTravel + .0001f;
            bool lastHeldPose = AtSlot(leaf.transform.position) && Vector3.Distance(finger, leaf.transform.position) <= reach;
            bool recentContact = now - slotContactAt <= slotReleaseGrace &&
                Vector3.Distance(finger, slot.transform.TransformPoint(slotContactLocal)) <= reach;
            if (AtSlot(finger) || lastHeldPose || recentContact) slot.TrySnapObject(leaf);
        }

        bool TryGrip(Grip requested)
        {
            if (!rpg.Input.TryAcquireInteraction(this, out long claim)) return false;
            gripClaim = claim;
            grip = requested;
            return true;
        }
        void ReleaseGrip()
        {
            if (rpg && gripClaim != 0) rpg.Input.ReleaseInteraction(this, gripClaim);
            gripClaim = 0;
            grip = Grip.None;
        }
        void DropTracking()
        {
            ReleaseGrip(); pinchReleased = false;
            trackingLostAt = double.NaN; slotContactAt = double.NegativeInfinity;
        }
        void OnDisable() => DropTracking();
        void Recall()
        {
            if (!taken || IsSeated || slot.SnappedObject || Vector3.Distance(leaf.transform.position, prizeRest.position) < .01f) return;
            dropTime += Time.deltaTime;
            if (dropTime >= 2) { PoseLeaf(prizeRest); dropTime = 0; }
        }
    }
}
