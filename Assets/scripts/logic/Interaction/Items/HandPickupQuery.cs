using System.Collections.Generic;
using NeuGameMRTools.Runtime.Components;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace NarutoVR.SealCombat.Rpg
{
    public interface IHandPickupSource
    {
        void CollectPickups(HandPickupQuery query);
        bool TryPickup(Transform target, bool left, PlayerHandObservation hand);
    }

    /// <summary>Arbitrates authored pickup offers before pen, menu and combat consume this hand frame.</summary>
    public sealed class HandPickupQuery
    {
        static readonly List<IHandPickupSource> Sources = new();
        public static void Register(IHandPickupSource source)
        {
            if (!Sources.Contains(source))
                Sources.Add(source);
        }
        public static void Unregister(IHandPickupSource source) => Sources.Remove(source);

        readonly HandState leftState = new(), rightState = new();
        readonly RaycastHit[] hits = new RaycastHit[32];
        readonly Collider[] overlaps = new Collider[16];
        int sampledFrame = -1;
        double sampledAt = double.NegativeInfinity;
        bool liveSample;
        double directRightAt = double.NegativeInfinity;
        HandState state;
        PlayerHandObservation hand;
        bool canTake, hasRay, nearestIsNear;
        float best;
        Transform selected;
        Vector3 point;
        IHandPickupSource owner;
        Material material;
        public LinearRpgController Player { get; }
        public bool Left { get; private set; }
        public Transform Highlighted(bool left) => (left ? leftState : rightState).Shown;
        internal HandPickupQuery(LinearRpgController player) => Player = player;

        public bool Sampled(double now) => sampledAt == now ||
                                           (liveSample && sampledFrame == Time.frameCount && now >= sampledAt &&
                                            now <= Time.realtimeSinceStartupAsDouble + .001);

        bool CanQuery => Player.CanAct && !Player.StateCommitInProgress &&
            !Player.Input.PassengerTransitActive && !Player.UiInputOwned;

        // Direct right-hand providers can run without a full XR sample. The left hand
        // keeps its last tracked observation, highlight and pinch state.
        internal void EvaluateDirectRight(PlayerHandObservation right, double now)
        {
            if (Sampled(now) || directRightAt == now) return;
            directRightAt = now;
            if (CanQuery) EvaluateHand(rightState, right, false, now);
            else rightState.Clear();
        }

        public void Evaluate(PlayerHandObservation left, PlayerHandObservation right, double now)
        {
            Player.Input.LeftPickupHand = left;
            sampledAt = now;
            sampledFrame = Time.frameCount;
            liveSample = left.rightValid || right.rightValid;
            if (!CanQuery)
            {
                leftState.Clear();
                rightState.Clear();
                return;
            }
            EvaluateHand(leftState, left, true, now);
            EvaluateHand(rightState, right, false, now);
        }

        void EvaluateHand(HandState current, PlayerHandObservation sample, bool left, double now)
        {
            state = current;
            hand = sample;
            Left = left;
            if (!sample.FreshTip(now, Player.handSampleMaxAge) || !sample.pinchValid)
            {
                state.Clear();
                return;
            }
            if (sample.rightObserved < state.LastStamp ||
                sample.rightObserved - state.LastStamp > Player.handSampleMaxAge || sample.source != state.Source)
                state.Clear();
            state.Source = sample.source;
            state.LastStamp = sample.rightObserved;
            bool down = sample.PinchDown(.03f), up = sample.PinchUp(.045f);
            bool select = down && state.Armed;
            if (up)
                state.Armed = true;
            if (down)
                state.Armed = false;
            bool recentHover = state.LastOpenTarget && now - state.LastHover <= Player.handSampleMaxAge &&
                               Vector3.Distance(sample.rightTip, state.LastTip) <= .085f;
            Transform locked = select ? (state.Shown ? state.Shown : recentHover ? state.LastOpenTarget : null) : null;
            var direction = sample.rayValid ? sample.rayDirection : sample.rightIndexDirection;
            var origin = sample.rayValid ? sample.rayOrigin : sample.rightTip;
            hasRay = (sample.rightIndexRayValid || sample.rayValid) && direction.sqrMagnitude > .5f &&
                     PlayerHandObservation.Finite(direction) && PlayerHandObservation.Finite(origin);
            // Curling the index during a pinch can change its ray. Recheck the last marked target
            // and current occlusion from the hand; a stale ray may never select a different object.
            if (select && locked && locked == state.LastOpenTarget && recentHover)
            {
                origin = sample.rightTip;
                direction = state.LastHit - origin;
                hasRay = direction.sqrMagnitude > .00001f;
            }
            if (hasRay)
                state.SetAim(Player, origin, direction, Player.pickupRayReach);
            canTake = Player.Input.CanInteract(out _) &&
                      !(Player.skills && Player.skills.effects && Player.skills.effects.HasOccupiedHand);
            owner = null;
            selected = null;
            best = float.PositiveInfinity;
            nearestIsNear = false;
            CollectOffers();
            // An explicitly extended finger may aim somewhere else than the tracked wrist.
            // Preserve that input when the primary ray has no eligible target.
            if (!selected && !locked && sample.rayValid && sample.rightIndexRayValid &&
                sample.rightIndexDirection.sqrMagnitude > .5f &&
                Vector3.Dot(direction.normalized, sample.rightIndexDirection.normalized) < .999f)
            {
                hasRay = true;
                state.SetAim(Player, sample.rightTip, sample.rightIndexDirection, Player.pickupRayReach);
                CollectOffers();
            }
            if (up) state.LastOpenTarget = selected;
            if (up && selected)
            {
                state.LastHover = now;
                state.LastHit = point;
                state.LastTip = sample.rightTip;
            }
            if (select && selected && (nearestIsNear || selected == locked) && owner.TryPickup(selected, left, sample))
            {
                state.Hide();
                return;
            }
            if (selected && !down)
                state.Show(this, selected, hasRay ? state.Cone.transform.position : sample.rightTip, point);
            else
                state.Hide();
        }

        void CollectOffers()
        {
            for (int i = 0; i < Sources.Count; i++)
                if (Sources[i] is Behaviour b && b.isActiveAndEnabled && b.gameObject.scene == Player.gameObject.scene)
                    Sources[i].CollectPickups(this);
        }

        public void Offer(IHandPickupSource source, Transform target, Vector3 anchor, float nearReach,
                          bool docked = false)
        {
            if (canTake)
                Consider(source, target, anchor, nearReach, target, docked);
        }
        // Only the provider that owns an open bag may offer a serving to the opposite hand.
        public void OfferOwned(IHandPickupSource source, Transform target, Vector3 anchor, float nearReach,
                               Transform colliderRoot = null) => Consider(source, target, anchor, nearReach,
                                                                          colliderRoot ? colliderRoot : target, false);

        void Consider(IHandPickupSource source, Transform target, Vector3 anchor, float nearReach, Transform root,
                      bool docked)
        {
            if (!target || !target.gameObject.activeInHierarchy || !PlayerHandObservation.Finite(anchor))
                return;
            var wrapper = Bind(target);
            var native = wrapper.DistanceGrab;
            if (!wrapper.isActiveAndEnabled || !wrapper.IsGrabEnabled)
                return;
            // SnapZone suspends the movement driver and solid colliders until the item leaves the pocket.
            // The pocket still owns a usable handle after seating, including the second and later pickups.
            if (native && !native.isActiveAndEnabled && !docked) return;
            if (hasRay) state.Cone.ApertureDegrees = target == state.Shown ? 20f : 10f;
            // A pocket opening and a biscuit can be authored handles without a separate solid collider.
            OfferContact(source, target, root, anchor, nearReach);
            if (!native || !native.isActiveAndEnabled) return;
            if (native.Colliders == null)
            {
                // A dynamically bound SDK interactable enumerates its colliders in Start next frame.
                if (native.Rigidbody && native.Rigidbody.TryGetComponent<Collider>(out var first))
                    OfferSurface(source, target, root, first, native.Rigidbody, nearReach);
                return;
            }
            foreach (var collider in native.Colliders)
                OfferSurface(source, target, root, collider, native.Rigidbody, nearReach);
        }

        void OfferSurface(IHandPickupSource source, Transform target, Transform root, Collider collider,
                          Rigidbody body, float nearReach)
        {
            if (!collider || !collider.enabled || collider.isTrigger || collider.attachedRigidbody != body ||
                !collider.gameObject.activeInHierarchy) return;
            var bounds = collider.bounds;
            float minDistance = bounds.SqrDistance(hand.rightTip);
            if (minDistance <= nearReach * nearReach && !(collider is MeshCollider mesh && !mesh.convex))
                OfferContact(source, target, root, collider.ClosestPoint(hand.rightTip), nearReach);
            if (nearestIsNear || !hasRay || minDistance > Player.pickupRayReach * Player.pickupRayReach) return;

            var eye = state.Cone.transform.position;
            var ray = new Ray(eye, state.Cone.Direction);
            if (collider.Raycast(ray, out var hit, Player.pickupRayReach))
            {
                OfferContact(source, target, root, hit.point, nearReach);
                return;
            }
            // The SDK cone supplies a broad-phase point on the world AABB. A ray from
            // the aiming hand to the actual shape selects its visible face above the floor.
            if (!state.Cone.HitsCollider(collider, out _, out var broadPoint)) return;
            var probe = collider is MeshCollider nonconvex && !nonconvex.convex
                ? bounds.center : collider.ClosestPoint(broadPoint);
            Vector3 toShape = probe - eye;
            if (toShape.sqrMagnitude < .000001f) return;
            if (collider.Raycast(new Ray(eye, toShape.normalized), out hit, Player.pickupRayReach))
                OfferContact(source, target, root, hit.point, nearReach);
        }

        void OfferContact(IHandPickupSource source, Transform target, Transform root, Vector3 contact, float nearReach)
        {
            float distance = (contact - hand.rightTip).sqrMagnitude;
            bool near = distance <= nearReach * nearReach;
            float score = distance;
            if (!near)
            {
                float reachSquared = Player.pickupRayReach * Player.pickupRayReach;
                if (nearestIsNear || !hasRay || distance > reachSquared ||
                    (contact - hand.headPosition).sqrMagnitude > reachSquared ||
                    !state.Cone.IsPointInConeFrustum(contact)) return;
                var offset = contact - state.Cone.transform.position;
                score = Vector3.Angle(state.Cone.Direction, offset) + .12f * offset.magnitude;
            }
            if (near == nearestIsNear && score >= best) return;
            if (!Clear(hand.rightTip, contact, root) || !Clear(hand.headPosition, contact, root)) return;
            if (!near && (state.Cone.transform.position - hand.rightTip).sqrMagnitude > .000001f &&
                !Clear(state.Cone.transform.position, contact, root)) return;
            owner = source;
            selected = target;
            point = contact;
            best = score;
            nearestIsNear = near;
        }

        static DistanceGrabInteractionController Bind(Transform t)
        {
            if (t.TryGetComponent<DistanceGrabInteractionController>(out var authored))
                return authored;
            // The original wizard binds a DistanceHandGrabInteractable to the object's rigidbody.
            // Keep the chapter's existing owner as its sole physics driver. Grip-only children
            // (e.g. paper-wrapped biscuits) share the SDK cone without manufacturing physics bodies.
            if (!t.GetComponentInChildren<DistanceHandGrabInteractable>(true) &&
                t.TryGetComponent<Rigidbody>(out var rb) && t.GetComponentInChildren<Collider>())
            {
                var native = t.gameObject.AddComponent<DistanceHandGrabInteractable>();
                native.InjectRigidbody(rb);
            }
            return t.gameObject.AddComponent<DistanceGrabInteractionController>();
        }

        bool Clear(Vector3 from, Vector3 to, Transform root)
        {
            var scene = Player.gameObject.scene.GetPhysicsScene();
            int n = scene.OverlapSphere(from, .002f, overlaps, ~0, QueryTriggerInteraction.Ignore);
            if (n == overlaps.Length)
                return false;
            for (int i = 0; i < n; i++)
                if (Blocks(overlaps[i], root))
                    return false;
            Vector3 delta = to - from;
            float length = delta.magnitude;
            return length <= .002f || (NoHit(scene, from, delta / length, length, root) &&
                                       NoHit(scene, to, -delta / length, length, root));
        }
        bool NoHit(PhysicsScene scene, Vector3 from, Vector3 dir, float length, Transform root)
        {
            int n = scene.Raycast(from, dir, hits, length, ~0, QueryTriggerInteraction.Ignore);
            if (n == hits.Length)
                return false;
            for (int i = 0; i < n; i++)
                if (Blocks(hits[i].collider, root))
                    return false;
            return true;
        }
        bool Blocks(Collider c, Transform root)
        {
            if (!c || c.transform.IsChildOf(root))
                return false;
            var rig = Player.locomotion.rigRoot;
            return !rig || !(c is CharacterController && c.transform.IsChildOf(rig));
        }

        public void Reset()
        {
            sampledAt = directRightAt = double.NegativeInfinity;
            sampledFrame = -1;
            liveSample = false;
            leftState.Clear();
            rightState.Clear();
        }
        public void Dispose()
        {
            leftState.Dispose();
            rightState.Dispose();
            if (material)
                Object.Destroy(material);
        }

        sealed class HandState
        {
            public bool Armed;
            public string Source;
            public double LastStamp = double.NegativeInfinity, LastHover = double.NegativeInfinity;
            public Vector3 LastTip, LastHit;
            public Transform Shown, LastOpenTarget;
            public ConicalFrustum Cone;
            LineRenderer line;
            public void SetAim(LinearRpgController player, Vector3 origin, Vector3 direction, float reach)
            {
                if (!Cone)
                {
                    var go = new GameObject("道具指向");
                    go.transform.SetParent(player.transform, false);
                    Cone = go.AddComponent<ConicalFrustum>();
                    Cone.MinLength = .015f;
                    Cone.RadiusStart = .03f;
                    Cone.ApertureDegrees = 10f;
                }
                Cone.MaxLength = Mathf.Max(.15f, reach);
                Cone.transform.SetPositionAndRotation(origin, Quaternion.LookRotation(direction));
            }
            public void Show(HandPickupQuery q, Transform target, Vector3 origin, Vector3 end)
            {
                Shown = target;
                if (!Cone)
                    SetAim(q.Player, origin, Vector3.forward, q.Player.pickupRayReach);
                if (!line)
                {
                    if (!q.material)
                    {
                        var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
                        if (!shader)
                            return;
                        q.material = new Material(shader) { color = new Color(1, .76f, .32f) };
                    }
                    line = Cone.gameObject.AddComponent<LineRenderer>();
                    line.sharedMaterial = q.material;
                    line.useWorldSpace = true;
                    line.positionCount = 19;
                    line.widthMultiplier = .0025f;
                    line.shadowCastingMode = ShadowCastingMode.Off;
                    line.receiveShadows = false;
                }
                line.enabled = true;
                line.SetPosition(0, origin);
                line.SetPosition(1, end);
                var facing =
                    Quaternion.LookRotation((end - origin).sqrMagnitude > .00001f ? end - origin : Cone.Direction);
                for (int i = 0; i <= 16; i++)
                {
                    float a = i * Mathf.PI / 8;
                    line.SetPosition(2 + i, end + facing * new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * .011f);
                }
            }
            public void Hide()
            {
                Shown = null;
                if (line)
                    line.enabled = false;
            }
            public void Clear()
            {
                Armed = false;
                LastOpenTarget = null;
                LastStamp = LastHover = double.NegativeInfinity;
                Hide();
            }
            public void Dispose()
            {
                Clear();
                if (Cone)
                    Object.Destroy(Cone.gameObject);
            }
        }
    }
    internal static class PickupHandFreshness
    {
        internal static bool FreshTip(this PlayerHandObservation hand, double now,
                                      float maxAge) => !hand.tipUnavailable
                                                       && PlayerHandObservation.Finite(hand.rightTip) &&
                                                       PlayerHandObservation.Fresh(hand.rightValid, hand.rightObserved,
                                                                                   now, maxAge);
    }
}
