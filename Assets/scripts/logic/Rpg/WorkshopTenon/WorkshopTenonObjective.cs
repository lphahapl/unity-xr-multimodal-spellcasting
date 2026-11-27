using System;
using NarutoVR.SealCombat.Items;
using UnityEngine;

namespace NarutoVR.SealCombat.Rpg
{
    [DefaultExecutionOrder(360)]
    public sealed class WorkshopTenonObjective : ChapterObjective, IHandPickupSource
    {
        public LinearRpgController player;
        public RpgStageDefinition stage;
        public CombatRoomEncounter roof, courtyard;
        public GameItemBase tenon;
        public WorkshopTenonSocket socket;
        public Transform holder;
        public GameObject closedJaws, openJaws;
        public GameObject consoleFault, consoleReady;
        public GameObject[] poweredCircuits = Array.Empty<GameObject>();
        public bool IsUnlocked { get; private set; }
        public bool IsSeated { get; private set; }
        public override bool IsComplete => IsRunning && IsSeated;
        public event Action Seated;
        long claim;
        bool heldInLeft, picked, needsPinch, permitInsertion, initializedWorld;
        float recoverAt;
        double lostAt = double.NaN;

        void Start()
        {
            socket.AutomaticSnapping = false;
            if (!IsRunning && player.IsReady) RestoreWorld();
        }
        protected override void OnEnable()
        {
            HandPickupQuery.Register(this);
            if (player) player.CheckpointRestored += RestoreWorld;
            base.OnEnable();
        }
        protected override void OnDisable()
        {
            HandPickupQuery.Unregister(this);
            if (player) player.CheckpointRestored -= RestoreWorld;
            base.OnDisable();
        }
        void RestoreWorld()
        {
            if (!stage || !tenon || !socket) return;
            initializedWorld = player && player.IsReady;
            Release(false);
            socket.ReleaseObject();
            IsSeated = player && player.Model != null && player.Model.Chapter.IsComplete(stage.stageId);
            picked = false; recoverAt = 0;
            SetLatch(false);
            Feedback();
            if (IsSeated) Park(socket.bottom); else Park(holder);
            if (!IsRunning || IsSeated) Rooms(false);
            if (!IsSeated) { roof.ResetEncounter(); courtyard.ResetEncounter(); }
        }
        protected override void OnBegin()
        {
            if (Owner != player) throw new InvalidOperationException("主榫与房间必须属于同一个玩家会话");
            RestoreWorld();
            socket.AutomaticSnapping = false;
            roof.CompletionChanged += EvaluateRooms;
            courtyard.CompletionChanged += EvaluateRooms;
            socket.OnSnapCompleted += Finish;
            socket.OnSnapReleased += ReleasedFromSocket;
            Rooms(true);
            EvaluateRooms();
        }
        protected override void OnEnd()
        {
            roof.CompletionChanged -= EvaluateRooms;
            courtyard.CompletionChanged -= EvaluateRooms;
            socket.OnSnapCompleted -= Finish;
            socket.OnSnapReleased -= ReleasedFromSocket;
            Release(false);
            Rooms(false);
            if (IsSeated) return;
            if (socket) socket.ReleaseObject();
            SetLatch(false);
            if (tenon && holder) Park(holder);
        }

        void Rooms(bool value)
        {
            if (roof) roof.enabled = value;
            if (courtyard) courtyard.enabled = value;
        }
        void EvaluateRooms()
        {
            if (IsSeated) return;
            bool clear = roof.HasStarted && roof.IsComplete && courtyard.HasStarted && courtyard.IsComplete;
            if (clear == IsUnlocked) return;
            if (!clear) { Release(false); socket.ReleaseObject(); picked = false; Park(holder); }
            SetLatch(clear);
        }
        void SetLatch(bool released)
        {
            IsUnlocked = released || IsSeated;
            if (closedJaws) closedJaws.SetActive(!IsUnlocked);
            if (openJaws) openJaws.SetActive(IsUnlocked);
            if (tenon) tenon.enabled = released && !IsSeated;
        }
        void Feedback()
        {
            if (consoleFault) consoleFault.SetActive(!IsSeated);
            if (consoleReady) consoleReady.SetActive(IsSeated);
            foreach (var circuit in poweredCircuits) if (circuit) circuit.SetActive(IsSeated);
        }
        void Park(Transform anchor)
        {
            recoverAt = 0;
            tenon.SetStowedSize();
            var body = tenon.GetComponent<Rigidbody>();
            if (!body.isKinematic) { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
            body.useGravity = false; body.isKinematic = true;
            body.position = anchor.position; body.rotation = anchor.rotation;
            tenon.transform.SetPositionAndRotation(anchor.position,anchor.rotation);
        }
        void LateUpdate()
        {
            if (!initializedWorld && player && player.IsReady && !IsRunning) RestoreWorld();
            if (!IsRunning || !IsUnlocked || IsSeated) return;
            if (!tenon.IsHeld && !socket.SnappedObject && recoverAt > 0 &&
                (tenon.transform.position.y < holder.position.y - 2 || Time.time >= recoverAt)) Park(holder);
            UpdateHold(heldInLeft ? player.Input.LeftPickupHand : player.Input.LatestHand, Time.realtimeSinceStartupAsDouble);
        }

        bool TenonAvailable => IsRunning && IsUnlocked && !IsSeated && tenon && tenon.isActiveAndEnabled &&
            !tenon.IsHeld && socket && !socket.SnappedObject;

        public void CollectPickups(HandPickupQuery query)
        {
            if (query.Player == player && TenonAvailable)
                query.Offer(this, tenon.transform, tenon.GripPosition, .09f);
        }

        public bool TryPickup(Transform target, bool left, PlayerHandObservation hand)
        {
            if (!TenonAvailable || target != tenon.transform || !hand.gripPoseValid || !hand.PinchDown(.03f) ||
                !player.Input.TryAcquireInteraction(this, out var newClaim)) return false;
            if (!tenon.Take())
            {
                player.Input.ReleaseInteraction(this, newClaim);
                return false;
            }
            claim = newClaim;
            heldInLeft = left;
            picked = true;
            needsPinch = false; lostAt = double.NaN; recoverAt = 0;
            tenon.MoveHeld(hand.gripPosition, hand.gripRotation);
            player.skills.InvalidateEarlierVoice();
            return true;
        }

        // Observation-driven callers use the same picker as the live player.
        internal void Step(PlayerHandObservation hand, double now)
        {
            player.Input.Pickups.Evaluate(default, hand, now);
            UpdateHold(heldInLeft ? player.Input.LeftPickupHand : hand, now);
        }

        void UpdateHold(PlayerHandObservation hand, double now)
        {
            if (!IsRunning || !IsUnlocked || IsSeated || socket.SnappedObject || !tenon.IsHeld) return;
            if (!player.CanAct || player.UiInputOwned || (claim != 0 && !player.Input.OwnsInteraction(this,claim)))
            { Release(false); return; }
            bool fresh = hand.gripPoseValid && hand.pinchValid && !hand.tipUnavailable && !hand.pinchTransition &&
                PlayerHandObservation.Fresh(hand.rightValid,hand.rightObserved,now,player.handSampleMaxAge);
            if (!fresh)
            {
                needsPinch = true;
                if (double.IsNaN(lostAt)) lostAt = now;
                if (now - lostAt > .35) Release(false);
                return;
            }
            if (needsPinch && !hand.PinchDown(.03f)) return;
            lostAt = double.NaN; needsPinch = false;
            if (hand.PinchUp(.045f))
            {
                tenon.MoveHeld(hand.gripPosition, hand.gripRotation);
                Release(true);
                return;
            }
            tenon.MoveHeld(hand.gripPosition,hand.gripRotation);
        }
        void Release(bool insert)
        {
            if (claim != 0 && player) player.Input.ReleaseInteraction(this,claim);
            claim = 0; heldInLeft = false; needsPinch = false; lostAt = double.NaN;
            if (!tenon || !tenon.IsHeld) return;
            tenon.Drop();
            recoverAt = Time.time + 12;
            if (!insert) return;
            permitInsertion = true;
            try
            {
                if (socket.TryInsert(tenon))
                {
                    // The authored insertion path and socket clearance are in physical metres.
                    tenon.SetStowedSize();
                    recoverAt = 0;
                }
            }
            finally { permitInsertion = false; }
        }
        public bool Accepts(GameItemBase item) => permitInsertion && IsRunning && IsUnlocked && picked && !IsSeated &&
            item && item == tenon && !item.IsHeld && player.CanAct;
        void ReleasedFromSocket(GameObject obj, bool completed)
        {
            if (!completed && IsRunning && !IsSeated && obj == tenon.gameObject)
            {
                tenon.SetUseSize();
                recoverAt = Time.time + 12;
            }
        }
        void Finish(GameObject obj)
        {
            if (!IsRunning || !IsUnlocked || IsSeated || !picked || obj != tenon.gameObject) return;
            tenon.SetStowedSize();
            IsSeated = true; SetLatch(true); Feedback(); NotifyStateChanged(); Seated?.Invoke();
        }
        public override void ValidateObjective()
        {
            if (!player || !stage || !roof || !courtyard || roof == courtyard || roof.rpg != player || courtyard.rpg != player ||
                !tenon || !tenon.GetComponent<Rigidbody>() || !socket || socket.objective != this ||
                !socket.mouth || !socket.align || !socket.bottom || !holder || !closedJaws || !openJaws)
                throw new InvalidOperationException("二章工坊房间、主榫或校准座接线不完整");
        }
    }
}
