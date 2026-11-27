using NarutoVR.SealCombat.Rpg;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NarutoVR.SealCombat.Items
{
    // Only the isolated item lab instantiates this segmented greybox.
    public sealed class PineInkPrototype : GameItemBase
    {
        public ToolBeltInteraction interaction;
        public Transform sleeve;
        public Transform[] segments;
        public RestoreDuLiItemEffectSO recovery;
        public bool testRecoveryOnBreak;
        [Min(.005f)] public float slideTravel = .031f;
        [Range(10f,70f)] public float breakAngle = 32f;
        public bool SleeveOpen { get; private set; }
        public int Remaining { get; private set; }
        Vector3 sleeveClosed, startPinch, startLocal, hinge;
        Pose segmentRest;
        Quaternion handRest;
        Transform movingSegment;
        bool armed, dragging, pullingPiece;

        protected override void Awake()
        {
            base.Awake();
            sleeveClosed = sleeve.localPosition;
            Remaining = segments.Length;
        }

        protected override void LateUpdate()
        {
            base.LateUpdate();
            if (!interaction || !interaction.player || !interaction.OwnsHeldItem(this))
            {
                ResetGesture();
                if (sleeve.localPosition != sleeveClosed) SetSleeve(false);
                return;
            }

            var keys = Keyboard.current;
            if (interaction is ItemLabControls lab && lab.Desktop && keys != null)
            {
                if (keys.uKey.wasPressedThisFrame) SetSleeve(!SleeveOpen);
                if (keys.bKey.wasPressedThisFrame) BreakOne(interaction.player.Player);
                return;
            }

            var hand = TravelRationInteraction.ObserveHand(interaction.player, !interaction.HeldInLeftHand, Time.realtimeSinceStartupAsDouble);
            StepGrip(hand, interaction.player.Player);
        }

        internal void StepGrip(TravelRationInteraction.PinchSample sample, RpgPlayerState player)
        {
            if (!IsHeld || !sample.Valid || player == null || !player.Alive || player.CastInProgress)
            { ResetGesture(); return; }
            if (sample.Span >= .045f) { ResetGesture(); armed = true; return; }
            var local = transform.InverseTransformPoint(sample.Position);
            if (!dragging)
            {
                if (!armed || sample.Span > .03f) return;
                armed = false;
                pullingPiece = SleeveOpen && Remaining > 0 &&
                    Vector3.Distance(sample.Position, segments[Remaining-1].position) <= .016f;
                if (!pullingPiece && Vector3.Distance(sample.Position, sleeve.position) > .019f) return;
                startPinch = local;
                startLocal = sleeve.localPosition;
                if (pullingPiece)
                {
                    movingSegment = segments[Remaining-1];
                    segmentRest = new Pose(movingSegment.localPosition, movingSegment.localRotation);
                    hinge = segmentRest.position - segmentRest.rotation * Vector3.up * (movingSegment.localScale.y * .5f);
                    handRest = Quaternion.Inverse(transform.rotation) * sample.Rotation;
                }
                dragging = true;
            }
            if (pullingPiece)
            {
                // Measure the bend transverse to the scored line. Pulling straight out or twisting around the stick cannot snap it.
                var movement = local - startPinch;
                var axial = segmentRest.rotation * Vector3.up;
                var side = Vector3.ProjectOnPlane(movement, axial);
                var wristDelta = Quaternion.Inverse(transform.rotation) * sample.Rotation * Quaternion.Inverse(handRest);
                var direction = wristDelta * axial;
                float wristAngle = Vector3.Angle(axial, direction);
                float lever = Mathf.Max(.01f, Vector3.Distance(startPinch, hinge));
                float angle = Mathf.Atan2(side.magnitude, lever) * Mathf.Rad2Deg;
                var axis = Vector3.Cross(axial, side);
                if (wristAngle > angle) { angle = wristAngle; axis = Vector3.Cross(axial, direction); }
                if (axis.sqrMagnitude > 1e-8f)
                {
                    var bend = Quaternion.AngleAxis(Mathf.Min(angle,breakAngle), axis.normalized);
                    movingSegment.SetLocalPositionAndRotation(hinge + bend * (segmentRest.position - hinge), bend * segmentRest.rotation);
                }
                if (angle >= breakAngle)
                {
                    bool broken = BreakOne(player);
                    if (broken) movingSegment = null;
                    ResetGesture();
                }
                return;
            }
            float distance = Mathf.Clamp(startLocal.y - sleeveClosed.y + local.y - startPinch.y, 0, slideTravel);
            sleeve.localPosition = sleeveClosed + Vector3.up * distance;
            SleeveOpen = distance >= slideTravel * .93f;
        }

        internal void SetSleeve(bool open)
        {
            sleeve.localPosition = sleeveClosed + Vector3.up * (open ? slideTravel : 0);
            SleeveOpen = open;
        }

        internal bool BreakOne(RpgPlayerState player)
        {
            if (!IsHeld || !SleeveOpen || Remaining == 0 || player == null || !player.Alive || player.CastInProgress) return false;
            if (testRecoveryOnBreak && (!recovery || !recovery.CanApply(player))) return false;
            segments[--Remaining].gameObject.SetActive(false);
            if (testRecoveryOnBreak) recovery.Apply(player);
            return true;
        }

        void ResetGesture()
        {
            if (movingSegment) movingSegment.SetLocalPositionAndRotation(segmentRest.position,segmentRest.rotation);
            movingSegment = null;
            dragging = false;
            pullingPiece = false;
            armed = false;
        }
    }
}
