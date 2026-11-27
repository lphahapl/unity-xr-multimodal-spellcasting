using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace NarutoVR.SealCombat.Rpg
{
    public sealed partial class Chapter02BasketBControl
    {
        [Header("站立远拉")]
        [InspectorName("最大瞄准距离（米）"), Min(.25f)] public float remoteReach = 3f;
        [InspectorName("回拉行程（米）"), Min(.04f)] public float remoteStroke = .14f;
        public bool RemoteHighlighted => remoteRay && remoteRay.enabled;
        public float PullFraction => throwFraction;

        bool farGrab, useGripAnchor, showingHold;
        int[] gripTriangles;
        double lastAimTime = double.NegativeInfinity;
        Vector3 aimedTip, aimingContact, initialHandOffset;
        Vector3 RemoteContact => leverMesh.transform.TransformPoint(Rotated(aimingContact, throwFraction));
        readonly RaycastHit[] blockers = new RaycastHit[32];
        readonly Collider[] overlaps = new Collider[16];
        LineRenderer remoteRay;
        Material rayMaterial;
        MeshRenderer bronze;
        MaterialPropertyBlock normalGrip, highlightedGrip;
        int tintId;

        bool TryAimGrip(PlayerHandObservation hand, out Vector3 origin, out Vector3 hit)
        {
            origin = hand.rightTip;
            hit = default;
            if (!InRemoteRange(origin) || gripTriangles == null ||
                (origin - GripPosition).sqrMagnitude <= GripRadius * GripRadius) return false;
            var direction = hand.rightIndexDirection;
            if (!hand.rightIndexRayValid)
            {
                if (!hand.rayValid || (hand.rayOrigin - hand.rightTip).sqrMagnitude > .09f) return false;
                origin = hand.rayOrigin;
                direction = hand.rayDirection;
            }
            if (!PlayerHandObservation.Finite(origin) || !PlayerHandObservation.Finite(direction) ||
                direction.sqrMagnitude < .0001f) return false;

            // The authored collider includes the fixed base. Hit-test only the rendered cross grip.
            var ray = new Ray(leverMesh.transform.InverseTransformPoint(origin),
                leverMesh.transform.InverseTransformVector(direction.normalized));
            if (!original.GetSubMesh(3).bounds.IntersectRay(ray)) return false;
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < gripTriangles.Length; i += 3)
            {
                var a = rest[gripTriangles[i]];
                var edge1 = rest[gripTriangles[i + 1]] - a;
                var edge2 = rest[gripTriangles[i + 2]] - a;
                var h = Vector3.Cross(ray.direction, edge2);
                float det = Vector3.Dot(edge1, h);
                if (Mathf.Abs(det) < 1e-8f) continue;
                var s = ray.origin - a;
                float u = Vector3.Dot(s, h) / det;
                var q = Vector3.Cross(s, edge1);
                float v = Vector3.Dot(ray.direction, q) / det;
                if (u < 0 || v < 0 || u + v > 1) continue;
                float t = Vector3.Dot(edge2, q) / det;
                if (t >= 0 && t < nearest) nearest = t;
            }
            if (float.IsPositiveInfinity(nearest)) return false;
            hit = leverMesh.transform.TransformPoint(ray.GetPoint(nearest));
            return CanReachContact(origin, hit);
        }

        bool InRemoteRange(Vector3 hand)
        {
            float maxSquared = remoteReach * remoteReach;
            return remoteReach > 0 && player.locomotion && player.locomotion.head &&
                (player.locomotion.head.position - GripPosition).sqrMagnitude <= maxSquared &&
                (hand - GripPosition).sqrMagnitude <= maxSquared;
        }

        bool CanReachContact(Vector3 hand, Vector3 contact) =>
            InRemoteRange(hand) && ClearSight(player.locomotion.head.position, contact) && ClearSight(hand, contact);

        bool ClearSight(Vector3 from, Vector3 to)
        {
            var scene = gameObject.scene.GetPhysicsScene();
            // A ray beginning inside a wall cannot hit that wall's entry face.
            int count = scene.OverlapSphere(from, .004f, overlaps, ~0, QueryTriggerInteraction.Ignore);
            if (count == overlaps.Length) return false;
            for (int i = 0; i < count; i++)
                if (!OwnCollider(overlaps[i])) return false;
            var delta = to - from;
            float distance = delta.magnitude;
            if (distance <= .002f) return true;
            var direction = delta / distance;
            // A ground or wall MeshCollider can be one-sided. Test the reverse segment as well.
            return RayUnblocked(scene, from, direction, distance) && RayUnblocked(scene, to, -direction, distance);
        }

        bool RayUnblocked(PhysicsScene scene, Vector3 origin, Vector3 direction, float distance)
        {
            int count = scene.Raycast(origin, direction, blockers, distance, ~0, QueryTriggerInteraction.Ignore);
            if (count == blockers.Length) return false;
            for (int i = 0; i < count; i++)
                if (!OwnCollider(blockers[i].collider)) return false;
            return true;
        }

        bool OwnCollider(Collider collider)
        {
            var node = collider.transform;
            return node.IsChildOf(leverMesh.transform) || node.IsChildOf(player.transform) ||
                (player.locomotion.rigRoot && node.IsChildOf(player.locomotion.rigRoot));
        }

        bool CanStartRemote(PlayerHandObservation hand, double now)
        {
            if (now < lastAimTime || now - lastAimTime > .22 ||
                (hand.rightTip - aimedTip).sqrMagnitude > .12f * .12f) return false;
            if (hand.rightIndexRayValid || hand.rayValid)
            {
                if (!TryAimGrip(hand, out _, out var hit)) return false;
                aimingContact = leverMesh.transform.InverseTransformPoint(hit);
                return true;
            }
            // When the pointing channel drops as the index curls, allow only the recent, still-visible hit.
            return CanReachContact(hand.rightTip, RemoteContact);
        }

        void BeginRemote(PlayerHandObservation hand)
        {
            farGrab = true;
            useGripAnchor = hand.gripPoseValid && PlayerHandObservation.Finite(hand.gripPosition);
            var anchor = useGripAnchor ? hand.gripPosition : hand.rightTip;
            initialHandOffset = anchor - player.locomotion.head.position;
            ShowRemoteRay(hand.rightTip, RemoteContact, true);
        }

        void PullRemote(PlayerHandObservation hand)
        {
            var anchor = useGripAnchor ? hand.gripPosition : hand.rightTip;
            // Measure the stroke against the user's head so walking backward cannot operate the lever.
            var offset = anchor - player.locomotion.head.position - initialHandOffset;
            var toward = -initialHandOffset.normalized;
            float along = Vector3.Dot(offset, toward);
            if ((useGripAnchor && !hand.gripPoseValid) || !PlayerHandObservation.Finite(anchor) ||
                (offset - along * toward).sqrMagnitude > .15f * .15f || along < -.09f)
            {
                ReleaseGrip();
                SetThrow(0);
                return;
            }
            float progress = Mathf.Clamp01(along / Mathf.Max(.04f, remoteStroke));
            SetThrow(progress);
            if (!CanReachContact(hand.rightTip, RemoteContact))
            {
                ReleaseGrip();
                SetThrow(0);
                return;
            }
            if (progress >= .999f) ArmDeparture();
            else ShowRemoteRay(hand.rightTip, RemoteContact, true);
        }

        static readonly Color AimColor = new Color(1f, .81f, .43f);
        static readonly Color GrabColor = new Color(1f, .94f, .72f);

        void ShowRemoteRay(Vector3 origin, Vector3 contact, bool holding)
        {
            if (!remoteRay)
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
                if (!shader) return;
                rayMaterial = new Material(shader);
                var line = new GameObject("B 分控杆 · 右手命中射线");
                line.transform.SetParent(transform, false);
                remoteRay = line.AddComponent<LineRenderer>();
                remoteRay.enabled = false;
                remoteRay.useWorldSpace = true;
                remoteRay.positionCount = 2;
                remoteRay.shadowCastingMode = ShadowCastingMode.Off;
                remoteRay.receiveShadows = false;
                remoteRay.sharedMaterial = rayMaterial;
                bronze = leverMesh.GetComponent<MeshRenderer>();
                if (bronze && bronze.sharedMaterials.Length > 3)
                {
                    var material = bronze.sharedMaterials[3];
                    tintId = Shader.PropertyToID(material && material.HasProperty("_BaseColor") ? "_BaseColor" : "_Color");
                    normalGrip = new MaterialPropertyBlock();
                    highlightedGrip = new MaterialPropertyBlock();
                }
            }
            if (!remoteRay.enabled || showingHold != holding)
            {
                var color = holding ? GrabColor : AimColor;
                remoteRay.widthMultiplier = holding ? .012f : .008f;
                rayMaterial.color = color;
                if (normalGrip != null)
                {
                    if (!remoteRay.enabled) bronze.GetPropertyBlock(normalGrip, 3);
                    bronze.GetPropertyBlock(highlightedGrip, 3);
                    highlightedGrip.SetColor(tintId, color * (holding ? 1.6f : 1.25f));
                    bronze.SetPropertyBlock(highlightedGrip, 3);
                }
                showingHold = holding;
                remoteRay.enabled = true;
            }
            remoteRay.SetPosition(0, origin);
            remoteRay.SetPosition(1, contact);
        }

        void HideRemoteRay()
        {
            if (!RemoteHighlighted) return;
            remoteRay.enabled = false;
            if (normalGrip != null && bronze) bronze.SetPropertyBlock(normalGrip.isEmpty ? null : normalGrip, 3);
        }
    }
}
