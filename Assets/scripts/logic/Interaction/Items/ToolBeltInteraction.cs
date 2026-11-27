using System;
using NeuGameMRTools.Runtime.Components;
using NarutoVR.SealCombat.Rpg;
using UnityEngine;

namespace NarutoVR.SealCombat.Items
{
    [DefaultExecutionOrder(360)]
    public class ToolBeltInteraction : MonoBehaviour, IHandPickupSource
    {
        public LinearRpgController player;
        public Transform belt;
        public MouthConsumeZone mouth;
        public SnapZone[] slots = Array.Empty<SnapZone>();
        public GameItemBase[] items = Array.Empty<GameItemBase>();
        public Vector3 beltOffset = new(0, -.39f, .16f);
        public Vector3 mouthOffset = new(0, -.085f, .055f);
        [Min(.01f)] public float reach = .09f;
        [Min(.005f), Tooltip("明确放入开口的距离（米）。")] public float placementReach = .045f;
        [Min(.01f), Tooltip("握点距当前身体的水平距离达到此值时，松手丢弃（米）。")] public float discardReach = GameItemBase.DefaultDiscardReach;
        public GameItemBase Held { get; private set; }
        public bool HeldInLeftHand { get; private set; }
        public bool Initialised { get; private set; }
        internal bool DrivenByGlyph { get; set; }
        internal bool OwnsHeldItem(GameItemBase item) => item && Held == item && item.IsHeld &&
            player && player.Input.OwnsInteraction(this, claim);
        long claim;
        bool auxiliaryGrip;
        bool confirmPinchAfterLoss;
        Quaternion yaw = Quaternion.identity;

        void Awake()
        {
            foreach (var slot in slots) if (slot) slot.AutomaticSnapping = false;
        }

        void OnEnable()
        {
            HandPickupQuery.Register(this);
            if (player) player.CheckpointRestored += ResetItems;
        }

        void LateUpdate()
        {
            if (DrivenByGlyph) return;
            double now = Time.realtimeSinceStartupAsDouble;
            if (ReadyForInput) RunFrame(ReadHand(now), now, Time.deltaTime);
        }

        bool ReadyForInput => isActiveAndEnabled && player && player.IsReady &&
            player.locomotion && player.locomotion.head && belt;

        internal void StepFromGlyph(PlayerHandObservation hand, double now)
        {
            if (DrivenByGlyph && ReadyForInput) RunFrame(hand, now, Time.deltaTime);
        }

        void RunFrame(PlayerHandObservation hand, double now, float delta)
        {
            var head = player.locomotion.head;
            var forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (forward.sqrMagnitude > .01f) yaw = Quaternion.LookRotation(forward);
            belt.SetPositionAndRotation(head.position + yaw * beltOffset, yaw);
            if (mouth) mouth.transform.SetPositionAndRotation(head.TransformPoint(mouthOffset), head.rotation);
            if (!Initialised)
            {
                foreach (var item in items)
                {
                    if (!item || !item.isActiveAndEnabled) continue;
                    if (!item.home && item.startAtDisplaySlot && item.Kind != GameItemKind.Generic)
                        item.home = Array.Find(slots, s => s && !s.SnappedObject && s.TryGetComponent<ToolBeltSlot>(out var p) && p.displayKind == item.Kind);
                    if (item.home) item.TryDock(item.home, Quaternion.Euler(item.stowEuler));
                }
                Initialised = true;
            }
            Step(hand, now, delta);
        }

        protected virtual HandPickupQuery RightPickups => player.Input.Pickups;

        protected virtual PlayerHandObservation ReadHand(double now) => player.Input.LatestHand;

        internal void Step(PlayerHandObservation input, double now, float delta)
        {
            bool canAct = player.CanAct && !player.UiInputOwned && !player.StateCommitInProgress;
            if (mouth) mouth.UpdateFinish(player.Player, canAct);
            if (!canAct || (claim != 0 && (!Held || !Held.isActiveAndEnabled || !player.Input.OwnsInteraction(this, claim))))
            { ReleaseHand(false); if (mouth) mouth.ResetContact(); return; }
            if (auxiliaryGrip) return;
            if (!Held) RightPickups.EvaluateDirectRight(input, now);
            if (HeldInLeftHand) input = player.Input.LeftPickupHand;
            bool fresh = !input.tipUnavailable && input.gripPoseValid && input.pinchValid && !input.pinchTransition &&
                PlayerHandObservation.Fresh(input.rightValid, input.rightObserved, now, player.handSampleMaxAge);
            if (!fresh || (confirmPinchAfterLoss && !input.PinchDown(.03f)))
            {
                confirmPinchAfterLoss = Held;
                if (mouth) mouth.Observe(Held as ConsumableItem, player.Player, false, 0);
                return;
            }
            confirmPinchAfterLoss = false;
            if (Held && input.PinchUp(.045f))
            {
                Held.MoveHeld(input.gripPosition, input.gripRotation);
                ReleaseHand(true);
            }
            if (!Held) return;
            Held.MoveHeld(input.gripPosition, input.gripRotation);
            if (mouth && mouth.isActiveAndEnabled && mouth.Observe(Held as ConsumableItem, player.Player, true, delta))
            {
                // SnapZone owns motion until completion. Drop restores gravity only after it releases the item.
                player.Input.ReleaseInteraction(this, claim);
                Held = null;
                HeldInLeftHand = false;
                claim = 0;
            }
        }

        public void CollectPickups(HandPickupQuery query)
        {
            if (query.Player != player || !Initialised || Held) return;
            foreach (var item in items)
            {
                if (!item || !item.isActiveAndEnabled || item is TravelRationPouch || item.IsHeld ||
                    (item.Dock && item.Dock.IsSnapping)) continue;
                query.Offer(this,item.transform,item.GripPosition,reach,item.Dock);
                if (item.Dock is ToolBeltSlot pocket && pocket.opening)
                    query.Offer(this,item.transform,pocket.opening.position,reach,true);
            }
        }

        public bool TryPickup(Transform target, bool left, PlayerHandObservation hand)
        {
            if (!hand.gripPoseValid || !target.TryGetComponent<GameItemBase>(out var item) ||
                Array.IndexOf(items,item)<0 || !item.isActiveAndEnabled || (item.Dock && item.Dock.IsSnapping) ||
                !TryTake(item,false)) return false;
            HeldInLeftHand=left;
            item.MoveHeld(hand.gripPosition,hand.gripRotation);
            return true;
        }

        internal bool TryTake(GameItemBase item, bool auxiliary)
        {
            if (!item || !isActiveAndEnabled || Held || !player.CanAct || player.UiInputOwned ||
                player.StateCommitInProgress || !player.Input.TryAcquireInteraction(this, out claim)) return false;
            if (!item.Take())
            {
                player.Input.ReleaseInteraction(this, claim);
                claim = 0;
                return false;
            }
            Held = item;
            auxiliaryGrip = auxiliary;
            HeldInLeftHand = false;
            if (player.skills) player.skills.InvalidateEarlierVoice();
            return true;
        }

        internal void ReleaseAuxiliary(GameItemBase item, bool putAway)
        {
            if (auxiliaryGrip && Held == item) ReleaseHand(putAway);
        }

        internal void RegisterItem(GameItemBase item)
        {
            if (!item || Array.IndexOf(items, item) >= 0) return;
            Array.Resize(ref items, items.Length + 1);
            items[items.Length - 1] = item;
        }

        internal void UnregisterItem(GameItemBase item)
        {
            if (Held == item) ReleaseHand(false);
            items = Array.FindAll(items, other => other != item);
        }

        void ReleaseHand(bool putAway)
        {
            HeldInLeftHand = false;
            var item = Held;
            Held = null;
            auxiliaryGrip = false;
            confirmPinchAfterLoss = false;
            if (player && claim != 0) player.Input.ReleaseInteraction(this, claim);
            claim = 0;
            if (!item) return;
            item.Drop();
            if (mouth) mouth.ResetContact();
            if (!putAway) return;
            item.StowOrRelease(player, placementReach, discardReach, slots);
        }

        void ResetItems()
        {
            ReleaseHand(false);
            if (mouth) mouth.CancelFinish();
        }

        internal bool RestoreToHome(GameItemBase item)
        {
            ResetItems();
            if (!item || !item.home) return false;
            if (item.Dock == item.home) return true;
            item.Undock();
            return item.TryDock(item.home, Quaternion.Euler(item.stowEuler));
        }

        void OnDisable()
        {
            HandPickupQuery.Unregister(this);
            if (player) player.CheckpointRestored -= ResetItems;
            ResetItems();
        }
    }
}
