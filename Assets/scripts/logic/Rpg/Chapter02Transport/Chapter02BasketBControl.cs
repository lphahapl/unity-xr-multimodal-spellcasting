using System;
using System.Linq;
using UnityEngine;

namespace NarutoVR.SealCombat.Rpg
{
    [DefaultExecutionOrder(390), DisallowMultipleComponent]
    public sealed partial class Chapter02BasketBControl : MonoBehaviour
    {
        public const string StageId = "CH02_B_DESCENT";
        public const string ValleyId = "CH02_B_VALLEY";

        public LinearRpgController player;
        public WorkshopTenonObjective tenon;
        public Chapter02BasketMotion motion;
        public SignalChapterObjective arrival;
        public Transform leverGrip;
        public MeshFilter leverMesh;

        // The original authored mesh has a two-part static base and a separate rod and cross grip.
        static readonly Vector3 Pivot = new Vector3(-.11f, .52f, 0);
        const float ThrowAngle = 34f;
        const float GripRadius = .145f;
        Mesh original, moving;
        Vector3[] rest, positions, restNormals, normals;
        int[] rodVertices;
        float throwFraction;
        bool openObserved;
        long claim;
        Vector3 initialTip;
        Action completeRide;
        public bool DepartureArmed { get; private set; }

        void OnEnable()
        {
            if (motion) motion.BArrived += OnArrival;
            if (player) player.CheckpointRestored += ResetControl;
        }

        void Start()
        {
            if (!leverMesh || !leverGrip || !leverMesh.sharedMesh) return;
            original = leverMesh.sharedMesh;
            if (!original.isReadable || original.subMeshCount != 4) return;
            moving = Instantiate(original);
            moving.name = original.name + " · 活动杆柄";
            rest = moving.vertices;
            positions = (Vector3[])rest.Clone();
            restNormals = moving.normals;
            normals = (Vector3[])restNormals.Clone();
            rodVertices = moving.GetIndices(2).Concat(moving.GetIndices(3)).Distinct().ToArray();
            gripTriangles = original.GetTriangles(3);
            moving.MarkDynamic();
            leverMesh.sharedMesh = moving;
        }

        void OnDisable()
        {
            if (motion) motion.BArrived -= OnArrival;
            if (player) player.CheckpointRestored -= ResetControl;
            ResetControl();
        }

        void OnDestroy()
        {
            if (rayMaterial) Destroy(rayMaterial);
            if (leverMesh && moving && leverMesh.sharedMesh == moving) leverMesh.sharedMesh = original;
            if (moving) Destroy(moving);
        }

        public Vector3 GripPosition => leverMesh.transform.TransformPoint(Rotated(leverGrip.localPosition, throwFraction));
        static Vector3 Rotated(Vector3 point, float fraction) =>
            Pivot + Quaternion.AngleAxis(ThrowAngle * fraction, Vector3.forward) * (point - Pivot);

        void SetThrow(float fraction)
        {
            if (Mathf.Abs(fraction - throwFraction) < .001f) return;
            throwFraction = fraction;
            if (!moving) return;
            var rotation = Quaternion.AngleAxis(ThrowAngle * fraction, Vector3.forward);
            foreach (int i in rodVertices)
            {
                positions[i] = Pivot + rotation * (rest[i] - Pivot);
                if (normals.Length == positions.Length) normals[i] = rotation * restNormals[i];
            }
            moving.vertices = positions;
            if (normals.Length == positions.Length) moving.normals = normals;
            moving.RecalculateBounds();
        }

        void ReleaseGrip()
        {
            if (player && claim != 0) player.Input.ReleaseInteraction(this, claim);
            claim = 0;
            openObserved = false;
            farGrab = false;
            lastAimTime = double.NegativeInfinity;
            HideRemoteRay();
        }

        void ResetControl()
        {
            ReleaseGrip();
            completeRide = null;
            DepartureArmed = false;
            SetThrow(0);
        }

        void OnArrival()
        {
            if (!motion.AtBottom || !arrival || !arrival.IsRunning || !player.CanAct) return;
            completeRide?.Invoke();
            if (arrival.IsComplete) completeRide = null;
        }

        void LateUpdate()
        {
            if (!player || !motion || !arrival || !tenon || !leverGrip || !leverMesh) return;
            if (motion.AtBottom)
            {
                // The shore lever can be re-enabled after the moving basket has already signalled.
                if (arrival.IsRunning && !arrival.IsComplete && tenon.IsSeated && !player.CampaignComplete)
                {
                    completeRide ??= arrival.CaptureCompletion();
                    OnArrival();
                }
                return;
            }
            if (motion.IsMoving) return;
            // Disabling the shared transport during a ride returns the basket to its source station.
            if (completeRide != null) ResetControl();
            if (!arrival.IsRunning || !tenon.IsSeated || !motion.AtMiddle || !player.CanAct)
            {
                if (claim != 0 || DepartureArmed || openObserved || RemoteHighlighted || throwFraction > 0) ResetControl();
                return;
            }

            if (DepartureArmed)
            {
                if (motion.PlayerInB)
                {
                    var completion = arrival.CaptureCompletion();
                    if (completion == null) return;
                    completeRide = completion;
                    if (motion.TryBeginB()) DepartureArmed = false;
                    else completeRide = null;
                }
                else if (Vector3.Distance(player.locomotion.head.position, GripPosition) > Mathf.Max(.25f, remoteReach) + 1f)
                    ResetControl();
                return;
            }
            Step(player.Input.LatestHand, Time.realtimeSinceStartupAsDouble);
        }

        internal void Step(PlayerHandObservation hand, double now)
        {
            if (!player.CanAct || !arrival.IsRunning || !tenon.IsSeated || !motion.AtMiddle ||
                motion.IsMoving || DepartureArmed || completeRide != null || player.UiInputOwned ||
                player.GlyphBlocksCombat || (claim != 0 && !player.Input.OwnsInteraction(this, claim)) ||
                hand.tipUnavailable || !hand.pinchValid ||
                !PlayerHandObservation.Fresh(hand.rightValid, hand.rightObserved, now, player.handSampleMaxAge) ||
                !PlayerHandObservation.Finite(hand.rightTip))
            {
                ReleaseGrip();
                if (!DepartureArmed && completeRide == null) SetThrow(0);
                return;
            }

            if (hand.PinchUp(.045f))
            {
                if (claim != 0) ReleaseGrip();
                SetThrow(0);
                openObserved = true;
                if (player.Input.CanInteract(out _) && TryAimGrip(hand, out var origin, out var hit))
                {
                    lastAimTime = now;
                    aimedTip = hand.rightTip;
                    aimingContact = leverMesh.transform.InverseTransformPoint(hit);
                    ShowRemoteRay(origin, hit, false);
                }
                else
                {
                    lastAimTime = double.NegativeInfinity;
                    HideRemoteRay();
                }
                return;
            }
            if (hand.pinchTransition)
            {
                if (farGrab && !CanReachContact(hand.rightTip, RemoteContact))
                {
                    ReleaseGrip();
                    SetThrow(0);
                }
                else if (claim == 0 && !TryAimGrip(hand, out _, out _))
                    HideRemoteRay();
                return;
            }
            if (claim == 0)
            {
                if (!openObserved || !hand.PinchDown(.03f)) return;
                openObserved = false;
                bool remote = (hand.rightTip - GripPosition).sqrMagnitude > GripRadius * GripRadius;
                if ((remote && !CanStartRemote(hand, now)) || !player.Input.TryAcquireInteraction(this, out claim))
                {
                    ReleaseGrip();
                    return;
                }
                if (remote)
                {
                    BeginRemote(hand);
                    return;
                }
                HideRemoteRay();
                initialTip = leverMesh.transform.InverseTransformPoint(hand.rightTip);
                return;
            }

            if (farGrab)
            {
                PullRemote(hand);
                return;
            }
            Vector3 here = leverMesh.transform.InverseTransformPoint(hand.rightTip);
            var stroke = Rotated(leverGrip.localPosition, 1f) - leverGrip.localPosition;
            var offset = here - initialTip;
            var progress = Vector3.Dot(offset, stroke) / stroke.sqrMagnitude;
            if ((offset - Mathf.Clamp01(progress) * stroke).sqrMagnitude > .18f * .18f)
            {
                ReleaseGrip();
                SetThrow(0);
                return;
            }
            SetThrow(Mathf.Clamp01(progress));
            if (progress < .82f) return;
            ArmDeparture();
        }

        void ArmDeparture()
        {
            ReleaseGrip();
            DepartureArmed = true;
            SetThrow(1);
        }

        internal static void MigrateCompletedCheckpoint(LinearRpgController player, RpgProgressData save)
        {
            if (save?.checkpointId != "CH02_HUB" || !player || !player.chapter ||
                !player.chapter.FinalCheckpoint || player.chapter.FinalCheckpoint.nodeId != ValleyId ||
                save.completedStageIds == null || save.completedStageIds.Count == 0 ||
                save.completedStageIds[save.completedStageIds.Count - 1] != StageId ||
                save.claimedRewardIds == null || !save.claimedRewardIds.Contains("reward." + StageId)) return;
            save.checkpointId = ValleyId;
        }
    }
}
