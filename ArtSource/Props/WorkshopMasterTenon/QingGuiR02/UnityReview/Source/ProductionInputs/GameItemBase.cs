using NeuGameMRTools.Runtime.Components;
using UnityEngine;
using UnityEngine.Serialization;

namespace NarutoVR.SealCombat.Items
{
    [DisallowMultipleComponent, RequireComponent(typeof(SnapItem), typeof(Rigidbody))]
    public class GameItemBase : MonoBehaviour
    {
        public SnapZone home;
        [FormerlySerializedAs("config")] public GameItemConfigSO definition;
        public GameItemKind Kind => definition ? definition.kind : GameItemKind.Generic;
        public bool startAtDisplaySlot;
        public bool keyItem;
        [Tooltip("可选的世界坠落下界；必须是固定在世界中的参照。关键物越过才尝试回默认槽。")]
        public Transform worldFallBoundary;
        public Transform grip;
        [Tooltip("可选的入槽端（如笔尖）；长道具的握点可以留在开口外。")]
        public Transform insertionTip;
        [Tooltip("初次陈列和防坠归位时，道具相对于开口的放入姿态。手动放入使用松手时的实际姿态。")]
        public Vector3 stowEuler;
        [Tooltip("可指定收拢状态的局部包围盒。留空则逐个测量当前有效的实体网格。")]
        public BoxCollider stowEnvelope;
        public bool IsHeld { get; private set; }
        public SnapZone Dock { get; private set; }
        public SnapZone LastDock { get; private set; }
        public SnapZone PickupSource { get; private set; }
        Quaternion dockRotation, lastDockRotation;
        public Vector3 GripPosition => grip ? grip.position : transform.position;
        Rigidbody body;
        MeshFilter[] meshes;
        float nextRecoveryTime;
        bool dropped;
        protected virtual void Awake()
        {
            body = GetComponent<Rigidbody>();
            meshes = GetComponentsInChildren<MeshFilter>(true);
        }

        internal bool MeasureInSlot(Transform opening, Quaternion rotation, out Bounds result)
        {
            var project = opening.worldToLocalMatrix *
                Matrix4x4.TRS(opening.position, rotation * Quaternion.Inverse(transform.rotation), Vector3.one) *
                Matrix4x4.Translate(-transform.position);
            var bounds = new Bounds();
            bool measured = false;
            void Include(Transform part, Bounds local)
            {
                // The sum of the three projected half-axes is the extent of all eight corners.
                var matrix = project * part.localToWorldMatrix;
                var e = local.extents;
                var x = matrix.MultiplyVector(Vector3.right * e.x);
                var y = matrix.MultiplyVector(Vector3.up * e.y);
                var z = matrix.MultiplyVector(Vector3.forward * e.z);
                var extent = new Vector3(Mathf.Abs(x.x) + Mathf.Abs(y.x) + Mathf.Abs(z.x),
                    Mathf.Abs(x.y) + Mathf.Abs(y.y) + Mathf.Abs(z.y),
                    Mathf.Abs(x.z) + Mathf.Abs(y.z) + Mathf.Abs(z.z));
                var projected = new Bounds(matrix.MultiplyPoint3x4(local.center), extent * 2f);
                if (measured) bounds.Encapsulate(projected);
                else { bounds = projected; measured = true; }
            }
            if (stowEnvelope) Include(stowEnvelope.transform, new Bounds(stowEnvelope.center, stowEnvelope.size));
            else foreach (var mesh in meshes)
                if (mesh && mesh.gameObject.activeInHierarchy && mesh.sharedMesh) Include(mesh.transform, mesh.sharedMesh.bounds);
            result = bounds;
            return measured;
        }

        public bool Take()
        {
            if (IsHeld || !isActiveAndEnabled || (Dock && Dock.IsSnapping)) return false;
            PickupSource = Dock && Dock.SnappedObject == gameObject && Dock == LastDock ? Dock : null;
            Undock();
            if (Dock) return false;
            Freeze();
            IsHeld = true;
            dropped = false;
            OnTaken();
            return true;
        }

        protected virtual void OnTaken() { }
        internal virtual void ReleasedInWorld(Rpg.LinearRpgController owner) { }

        public void MoveHeld(Vector3 position, Quaternion rotation)
        {
            if (!IsHeld) return;
            transform.rotation = rotation;
            transform.position += position - GripPosition;
        }

        public void Drop()
        {
            if (!IsHeld) return;
            IsHeld = false;
            dropped = true;
            nextRecoveryTime = 0;
            body.isKinematic = false;
            body.useGravity = true;
        }

        internal bool TryDock(SnapZone slot, Quaternion? localRotation = null)
        {
            if (Dock || IsHeld || !(slot is ToolBeltSlot pocket) || !pocket.opening) return false;
            bool accepted = localRotation.HasValue ?
                slot.TrySnapObjectAtRotation(gameObject, pocket.opening.rotation * localRotation.Value) : slot.TrySnapObject(gameObject);
            return accepted && Dock == slot && slot.SnappedObject == gameObject;
        }

        internal void EnterDock(ToolBeltSlot slot)
        {
            Dock = slot;
            dockRotation = Quaternion.Inverse(slot.opening.rotation) * slot.LandingPose.rotation;
            dropped = false;
        }

        internal void ConfirmDock(ToolBeltSlot slot)
        {
            if (Dock != slot) return;
            LastDock = slot;
            lastDockRotation = dockRotation;
        }

        internal void LeaveDock(ToolBeltSlot slot, bool completed)
        {
            if (Dock != slot) return;
            if (completed) ConfirmDock(slot);
            Dock = null;
            dropped = true;
        }

        internal bool ReturnToPickupSource() => PickupSource && TryDock(PickupSource, lastDockRotation);

        internal float DistanceToOpeningSquared(Vector3 position)
        {
            float gripDistance = (position - GripPosition).sqrMagnitude;
            return insertionTip ? Mathf.Min(gripDistance, (position - insertionTip.position).sqrMagnitude) : gripDistance;
        }

        internal void Undock()
        {
            var previous = Dock;
            if (previous && previous.SnappedObject == gameObject) previous.ReleaseObject();
            if (Dock == previous && (!previous || previous.SnappedObject != gameObject)) Dock = null;
        }

        void Freeze()
        {
            if (!body.isKinematic) { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
            body.useGravity = false;
            body.isKinematic = true;
        }

        protected virtual void LateUpdate()
        {
            if (Dock)
            {
                if (Dock.SnappedObject != gameObject) { Undock(); dropped = true; }
                else if (!Dock.IsSnapping)
                {
                    var pose = Dock.LandingPose;
                    transform.SetPositionAndRotation(pose.position, pose.rotation);
                }
            }
            if (!dropped || !keyItem || !home || !worldFallBoundary || Time.deltaTime <= 0 ||
                transform.position.y >= worldFallBoundary.position.y || Time.time < nextRecoveryTime) return;
            nextRecoveryTime = Time.time + .5f;
            TryDock(home, Quaternion.Euler(stowEuler));
        }

        protected virtual void OnDisable() { Drop(); Undock(); }
    }
}
