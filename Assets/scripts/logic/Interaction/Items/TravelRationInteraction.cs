using NarutoVR.SealCombat.Rpg;
using NeuGameMRTools.Runtime.Components;
using NarutoVR.SealLab;
using Oculus.Interaction.Input;
using UnityEngine;

namespace NarutoVR.SealCombat.Items
{
    [DefaultExecutionOrder(370)]
    public class TravelRationInteraction : MonoBehaviour, IHandPickupSource
    {
        public LinearRpgController player;
        public TravelRationPouch pouch;
        public MouthConsumeZone mouth;
        public Transform belt;
        // When supplied, the chapter belt alone owns both the input claim and return-to-slot policy.
        [HideInInspector] public ToolBeltInteraction sharedBelt;
        public Vector3 beltOffset = new(0, -.39f, .16f);
        public Vector3 mouthOffset = new(0, -.085f, .055f);
        public float bagReach = .04f;
        public float biscuitReach = .023f;
        [Min(.01f), Tooltip("独立干粮样板的水平丢弃距离（米）。章节中使用共享工具带上的设置。")]
        public float discardReach = GameItemBase.DefaultDiscardReach;

        readonly bool[] armed = new bool[2];
        int bagHand = -1;
        long claim;
        ConsumableItem serving;
        bool finishing, initialised, lostBag, lostServing;
        Quaternion yaw = Quaternion.identity;
        Quaternion pouchFromGrip;

        public readonly struct PinchSample
        {
            public readonly Vector3 Position, GripPosition;
            public readonly Quaternion Rotation;
            public readonly float Span;
            public readonly bool Valid;

            public PinchSample(Vector3 position, Quaternion rotation, float span, Vector3? gripPosition = null)
            { Position = position; GripPosition = gripPosition ?? position; Rotation = rotation; Span = span; Valid = true; }
        }

        public bool HasServing => serving;
        protected virtual bool LocalPickupInput => false;
        bool Ready => isActiveAndEnabled && player && player.CanAct && !player.UiInputOwned &&
            !player.StateCommitInProgress && pouch && pouch.isActiveAndEnabled && mouth && mouth.isActiveAndEnabled;
        bool CanLiftBag => bagHand < 0 && !pouch.IsHeld && (!pouch.Dock || !pouch.Dock.IsSnapping);
        bool CanLiftServing(int index) => bagHand >= 0 && index != bagHand && pouch.IsHeld &&
            !lostBag && !serving && !finishing && !pouch.HasLifted &&
            (sharedBelt ? sharedBelt.OwnsHeldItem(pouch) : player.Input.OwnsInteraction(this, claim));

        void OnEnable() => HandPickupQuery.Register(this);

        public void CollectPickups(HandPickupQuery query)
        {
            if (!Ready || query.Player != player || LocalPickupInput) return;
            if (CanLiftBag)
            {
                query.Offer(this, pouch.transform, pouch.GripPosition, bagReach, pouch.Dock);
                return;
            }
            if (!CanLiftServing(query.Left ? 0 : 1)) return;
            for (int i = 0; i < pouch.biscuits.Length; i++)
            {
                if (!pouch.CanLiftBiscuit(i)) continue;
                var piece = pouch.biscuits[i];
                for (int side = -1; side <= 1; side++)
                    query.OfferOwned(this, piece, pouch.BiscuitGripPoint(i, side), biscuitReach);
            }
        }

        public bool TryPickup(Transform target, bool left, PlayerHandObservation hand)
        {
            if (!Ready || !target || LocalPickupInput || !hand.gripPoseValid || hand.tipUnavailable ||
                !hand.PinchDown(.03f)) return false;
            int index = left ? 0 : 1;
            var sample = new PinchSample(hand.rightTip, hand.gripRotation, .012f, hand.gripPosition);
            if (target == pouch.transform) return CanLiftBag && TryLiftBag(index, sample);
            if (!CanLiftServing(index)) return false;
            return HoldServing(pouch.LiftBiscuit(target), sample);
        }

        bool HoldServing(ConsumableItem piece, PinchSample sample)
        {
            if (!piece) return false;
            serving = piece;
            lostServing = false;
            if (sharedBelt) serving.transform.SetParent(player.locomotion.transform, true);
            serving.MoveHeld(sample.Position, sample.Rotation);
            return true;
        }

        public int BagHand => bagHand;

        protected virtual void LateUpdate()
        {
            if (!player || !player.IsReady || !pouch || !mouth) return;
            var head = player.locomotion ? player.locomotion.head : null;
            if (!head) return;
            var forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (forward.sqrMagnitude > .01f) yaw = Quaternion.LookRotation(forward);
            if (!sharedBelt && belt) belt.SetPositionAndRotation(head.position + yaw * beltOffset, yaw);
            mouth.transform.SetPositionAndRotation(head.TransformPoint(mouthOffset), head.rotation);
            if (!sharedBelt && !initialised)
            {
                if (pouch.home is ToolBeltSlot slot && slot.opening)
                {
                    var end = slot.opening;
                    pouch.transform.SetPositionAndRotation(end.position - end.up * .038f,
                        end.rotation * Quaternion.Euler(pouch.stowEuler));
                    pouch.TryDock(slot, Quaternion.Euler(pouch.stowEuler));
                }
                initialised = true;
            }
            double now = Time.realtimeSinceStartupAsDouble;
            Step(ReadHand(true, now), ReadHand(false, now), Time.deltaTime, LocalPickupInput);
        }

        protected virtual PinchSample ReadHand(bool left, double now)
        {
            var hand = left ? player.Input.LeftPickupHand : player.Input.LatestHand;
            if (!hand.FreshTip(now, player.handSampleMaxAge) || !hand.gripPoseValid ||
                !hand.pinchValid || hand.pinchTransition) return default;
            float span = hand.systemPinch ? (hand.systemPressed ? 0 : .06f) : hand.pinchDistance;
            return new PinchSample(hand.rightTip, hand.gripRotation, span, hand.gripPosition);
        }

        internal static PinchSample ObserveHand(LinearRpgController player, bool left, double now)
        {
            var tracking = player.handTracking ? player.handTracking : player.skills ? player.skills.lab : null;
            var frame = tracking ? tracking.Frame : null;
            if (frame == null) return default;
            var hand = left ? frame.left : frame.right;
            if (hand == null || !hand.connected || !hand.tracked ||
                !PlayerHandObservation.Fresh(hand.fresh, hand.observedAt, now, player.handSampleMaxAge)) return default;
            int index = (int)HandJointId.HandIndexTip, thumb = (int)HandJointId.HandThumbTip;
            int wrist = (int)HandJointId.HandWristRoot, baseIndex = (int)HandJointId.HandIndex1;
            if (!hand.jointValid[index] || !hand.jointValid[thumb] || !hand.jointValid[wrist] ||
                !hand.jointValid[baseIndex] || !SealFeatureExtractor.PalmNormal(hand, left, out var normal)) return default;
            var origin = tracking.recordingOrigin;
            Vector3 World(int joint) => origin ? origin.TransformPoint(hand.joints[joint].position) : hand.joints[joint].position;
            var a = World(index);
            var b = World(thumb);
            var axis = World(baseIndex) - World(wrist);
            var up = origin ? -origin.TransformDirection(normal) : -normal;
            if (!PlayerHandObservation.Finite(a) || !PlayerHandObservation.Finite(b) ||
                !PlayerHandObservation.Finite(axis) || !PlayerHandObservation.Finite(up) ||
                axis.sqrMagnitude < .0004f || Vector3.Cross(axis.normalized, up).sqrMagnitude < .05f) return default;
            return new PinchSample((a + b) * .5f, Quaternion.LookRotation(axis, up), Vector3.Distance(a, b));
        }

        internal void Step(PinchSample left, PinchSample right, float delta, bool allowLocalPickup = true)
        {
            if (!pouch || !pouch.isActiveAndEnabled || !mouth || !player.CanAct || player.UiInputOwned || player.StateCommitInProgress ||
                (bagHand >= 0 && sharedBelt && !sharedBelt.OwnsHeldItem(pouch)) ||
                (claim != 0 && !player.Input.OwnsInteraction(this, claim)))
            { Release(false); armed[0] = armed[1] = false; return; }

            bool leftDown = allowLocalPickup && Pressed(0, left);
            bool rightDown = allowLocalPickup && Pressed(1, right);
            // Re-confirm both pinches after a tracking gap before the last bite can commit.
            if (finishing && (!left.Valid || !right.Valid || left.Span >= .045f || right.Span >= .045f))
            {
                FinishServing();
                armed[0] = armed[1] = false;
            }
            mouth.UpdateFinish(player.Player, true);
            if (pouch.HasLifted && !serving) FinishServing();

            if (bagHand < 0)
            {
                if (leftDown && NearBag(left) && TryLiftBag(0, left)) return;
                if (rightDown && NearBag(right)) TryLiftBag(1, right);
                return;
            }

            var bag = bagHand == 0 ? left : right;
            var free = bagHand == 0 ? right : left;
            if (!pouch.IsHeld) { Release(false); return; }
            lostBag = !bag.Valid || (lostBag && bag.Span > .03f);
            if (!lostBag && bag.Span >= .045f)
            {
                HoldBag(bag);
                // A newly collected world pouch has no withdrawal slot. Give the close-fitting
                // central soft pocket its folded orientation only after normal placement is tried.
                float placementReach = sharedBelt ? sharedBelt.placementReach : bagReach;
                var central = pouch.home as ToolBeltSlot;
                bool atCentral = !pouch.PickupSource && central && central.isActiveAndEnabled && central.opening &&
                    !central.SnappedObject && !central.penHolster &&
                    pouch.DistanceToOpeningSquared(central.opening.position) < placementReach * placementReach;
                if (atCentral && sharedBelt)
                {
                    float centralDistance = pouch.DistanceToOpeningSquared(central.opening.position);
                    foreach (var zone in sharedBelt.slots)
                    {
                        if (zone is ToolBeltSlot other && other != central && other.isActiveAndEnabled && other.opening &&
                            pouch.DistanceToOpeningSquared(other.opening.position) < centralDistance)
                        { atCentral = false; break; }
                    }
                }
                Release(true);
                if (atCentral && !pouch.IsHeld && !pouch.Dock && !central.SnappedObject)
                    pouch.TryDock(central, Quaternion.Euler(pouch.stowEuler));
                return;
            }
            if (!lostBag) HoldBag(bag);

            if (serving)
            {
                if (serving.Remaining == 0 || (finishing && !mouth.GetComponent<SnapZone>().SnappedObject))
                { FinishServing(); return; }
                if (finishing) return;
                lostServing = !free.Valid || (lostServing && free.Span > .03f);
                if (lostBag || lostServing) { mouth.Observe(serving, player.Player, false, 0); return; }
                if (free.Span >= .045f) { FinishServing(); return; }
                serving.MoveHeld(free.Position, free.Rotation);
                finishing = mouth.Observe(serving, player.Player, true, delta);
                return;
            }
            if (!lostBag && (bagHand == 0 ? rightDown : leftDown))
            {
                HoldServing(pouch.LiftBiscuit(free.Position, biscuitReach), free);
            }
        }

        bool Pressed(int index, PinchSample sample)
        {
            if (!sample.Valid) { armed[index] = false; return false; }
            if (sample.Span >= .045f) { armed[index] = true; return false; }
            if (sample.Span > .03f || !armed[index]) return false;
            armed[index] = false;
            return true;
        }

        void HoldBag(PinchSample hand) =>
            pouch.MoveHeld(hand.GripPosition, hand.Rotation * pouchFromGrip);

        Quaternion InitialPouchRotation(int handIndex)
        {
            // The paper bed is local XY, with the food on +Z. Offer a comfortable pose at pickup.
            Vector3 across = (handIndex == 0 ? 1f : -1f) * (yaw * Vector3.right);
            Vector3 surface = (Vector3.up + .24f * across + .30f * (yaw * Vector3.back)).normalized;
            Vector3 along = Vector3.ProjectOnPlane(yaw * Vector3.forward, surface);
            return Quaternion.LookRotation(surface, along);
        }

        bool NearBag(PinchSample sample) => (sample.Position - pouch.GripPosition).sqrMagnitude <= bagReach * bagReach;

        bool TryLiftBag(int index, PinchSample sample)
        {
            if (sharedBelt)
            {
                if (!sharedBelt.TryTake(pouch, true)) return false;
                pouch.transform.SetParent(player.locomotion.transform, true);
            }
            else
            {
                if (!player.Input.TryAcquireInteraction(this, out claim)) return false;
                if (!pouch.Take()) { player.Input.ReleaseInteraction(this, claim); claim = 0; return false; }
            }
            bagHand = index;
            pouchFromGrip = Quaternion.Inverse(sample.Rotation) * InitialPouchRotation(index);
            lostBag = false;
            HoldBag(sample);
            if (!sharedBelt && player.skills) player.skills.InvalidateEarlierVoice();
            return true;
        }

        void FinishServing()
        {
            if (!serving && (!pouch || !pouch.HasLifted)) return;
            if (mouth)
            {
                if (finishing) mouth.CancelFinish();
                else mouth.ResetContact();
            }
            if (pouch) pouch.ReturnOrConsume(serving);
            serving = null;
            finishing = false;
            lostServing = false;
        }

        void Release(bool putAway)
        {
            FinishServing();
            if (claim != 0 && player) player.Input.ReleaseInteraction(this, claim);
            claim = 0;
            if (bagHand < 0) return;
            bagHand = -1;
            if (!pouch) return;
            pouch.SetOpenCollision(false);
            if (sharedBelt)
            {
                sharedBelt.ReleaseAuxiliary(pouch, putAway);
                if (!pouch.Dock && !pouch.IsHeld) pouch.transform.SetParent(null, true);
                return;
            }
            pouch.Drop();
            if (putAway) pouch.StowOrRelease(player, bagReach, discardReach, pouch.home);
        }

        internal void ResetInteraction()
        {
            Release(false);
            armed[0] = armed[1] = false;
        }

        void OnDisable()
        {
            HandPickupQuery.Unregister(this);
            ResetInteraction();
        }
    }
}


