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
        public RpgStageDefinition solvedStage, collectedStage, seatedStage;
        [Min(.005f)] public float grabRadius = .055f, slotRadius = .026f, verifyRadius = .14f;
        [Range(1,15)] public float tolerance = 5;
        public float outerStartDegrees = 45, innerStartDegrees = 20;
        [Min(.1f)] public float settleSeconds = .6f;
        public event Action Solved, Collected;
        public bool IsSeated { get; private set; }
        public bool VerifiesWrist => IsSeated && rpg.wristMenu.wornDevice.gameObject.activeInHierarchy &&
            Vector3.Distance(slot.transform.position, verifier.position) <= verifyRadius;

        enum Grip { None, Outer, Inner, Leaf }
        Grip grip;
        float outerAngle, innerAngle, lastAngle, stable, dropTime;
        bool activePuzzle, solved, canCollect, taken, canInsert, pinchReleased, discReleased;

        public void Restore(List<string> completed)
        {
            if (slot.SnappedObject) slot.ReleaseObject();
            solved = completed.Contains(solvedStage.stageId);
            var owned = rpg ? rpg.Progress?.copperLeaves?.Find(item => item.leafId == leafId) : null;
            taken = owned != null || completed.Contains(collectedStage.stageId);
            IsSeated = owned?.registered == true || completed.Contains(seatedStage.stageId);
            grip = Grip.None;
            activePuzzle = canCollect = canInsert = pinchReleased = discReleased = false;
            stable = dropTime = 0;
            outerAngle = solved ? 0 : outerStartDegrees;
            innerAngle = solved ? 0 : innerStartDegrees;
            SetDiscs();
            lid.SetActive(!solved);
            leaf.SetActive(true);
            PoseLeaf(IsSeated ? slot.transform : prizeRest);
            slot.enabled = false;
        }

        public void EnablePuzzle() { activePuzzle = true; }
        public void EnableReward() { canCollect = solved; }
        public void EnableInsertion() { canInsert = taken; }
        public bool IsEarnedLeaf(GameObject item) => taken && leaf == item;
        public void Seat(GameObject item)
        {
            if (!IsEarnedLeaf(item)) return;
            IsSeated = true;
            grip = Grip.None;
            PoseLeaf(slot.transform);
        }

        void PoseLeaf(Transform anchor) => leaf.transform.SetPositionAndRotation(anchor.position, anchor.rotation);
        void SetDiscs()
        {
            outerDisc.localRotation = Quaternion.Euler(0,outerAngle,0);
            innerDisc.localRotation = Quaternion.Euler(0,innerAngle,0);
        }
        static float Error(float angle, float period) => Mathf.Abs(Mathf.Repeat(angle + period * .5f, period) - period * .5f);
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
            if (!rpg || !rpg.CanAct || !rpg.wristMenu || rpg.UiInputOwned) { DropTracking(); return; }
            var hand = rpg.wristMenu.LatestInput;
            bool valid = !hand.tipUnavailable && WristMenuState.Fresh(hand.rightValid, hand.rightObserved,
                Time.realtimeSinceStartupAsDouble, rpg.wristMenu.settings.maxAge);
            if (!valid) { DropTracking(); CheckAlignment(); Recall(); return; }
            var tip = hand.rightTip;
            bool release = hand.PinchUp(rpg.wristMenu.settings);
            if (release)
            {
                if (grip == Grip.Outer || grip == Grip.Inner) discReleased = true;
                if (grip == Grip.Leaf && canInsert && slot.isActiveAndEnabled &&
                    Vector3.Distance(tip, slotEntry.position) <= slotRadius) slot.TrySnapObject(leaf);
                grip = Grip.None;
                pinchReleased = true;
            }
            else if (pinchReleased && hand.PinchDown(rpg.wristMenu.settings))
            {
                pinchReleased = false;
                if (activePuzzle && !solved)
                {
                    if (Near(tip, outerHandle)) grip = Grip.Outer;
                    else if (Near(tip, innerHandle)) grip = Grip.Inner;
                    if (grip != Grip.None) { stable = 0; discReleased = false; }
                    lastAngle = Angle(tip);
                }
                if ((canCollect || canInsert) && solved && !slot.SnappedObject && Near(tip, leaf.transform))
                {
                    grip = Grip.Leaf;
                    if (!taken) { taken = true; Collected?.Invoke(); }
                }
            }
            if (grip == Grip.Outer || grip == Grip.Inner)
            {
                float angle = Angle(tip);
                float delta = Mathf.DeltaAngle(lastAngle, angle);
                if (grip == Grip.Outer) outerAngle += delta;
                innerAngle += delta;
                lastAngle = angle;
                SetDiscs();
            }
            if (grip == Grip.Leaf)
            {
                leaf.transform.position = tip;
                if (canInsert && slot.isActiveAndEnabled && Vector3.Distance(tip, slotEntry.position) <= grabRadius * 2)
                    leaf.transform.rotation = slot.transform.rotation;
                dropTime = 0;
            }
            else Recall();
            CheckAlignment();
        }

        void CheckAlignment()
        {
            if (!activePuzzle || solved || !discReleased) return;
            bool aligned = grip == Grip.None && Error(outerAngle,180) <= tolerance && Error(innerAngle,90) <= tolerance;
            stable = aligned ? stable + Time.deltaTime : 0;
            if (stable < settleSeconds) return;
            solved = true;
            outerAngle = Mathf.Round(outerAngle / 180) * 180;
            innerAngle = Mathf.Round(innerAngle / 90) * 90;
            SetDiscs();
            lid.SetActive(false);
            Solved?.Invoke();
        }

        void DropTracking() { grip = Grip.None; pinchReleased = false; }
        void Recall()
        {
            if (!taken || IsSeated || slot.SnappedObject || Vector3.Distance(leaf.transform.position, prizeRest.position) < .01f) return;
            dropTime += Time.deltaTime;
            if (dropTime >= 2) { PoseLeaf(prizeRest); dropTime = 0; }
        }
    }
}
