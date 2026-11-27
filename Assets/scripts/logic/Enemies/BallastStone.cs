using NarutoVR.SealCombat.Rpg;
using UnityEngine;

namespace NarutoVR.SealCombat.Enemies
{
    /// <summary>一份背载网格与刚体贯穿背载、落地和入槽。入篮前由对应篮的乘员门控锁住符力。</summary>
    [RequireComponent(typeof(GlyphHeavyBody), typeof(BoxCollider)), DisallowMultipleComponent]
    public sealed class BallastStone : MonoBehaviour
    {
        public enum Phase { Carried, Dropped, Docked }
        public Phase State { get; private set; }
        public bool MovedByGlyph { get; private set; }
        GlyphHeavyBody heavy;
        BoxCollider hull;
        Collider[] releaseColliders;
        void Awake()
        {
            heavy = GetComponent<GlyphHeavyBody>();
            hull = GetComponent<BoxCollider>();
            heavy.GlyphImpulseApplied += OnGlyphImpulse;
        }
        void OnDestroy() { if (heavy) heavy.GlyphImpulseApplied -= OnGlyphImpulse; }
        void OnGlyphImpulse(Vector3 impulse) => MovedByGlyph = true;
        internal void Carry(Vector3 offset)
        {
            State = Phase.Carried;
            MovedByGlyph = false;
            heavy.enabled = false;
            heavy.Body.isKinematic = true;
            hull.enabled = false;
            transform.SetLocalPositionAndRotation(offset, Quaternion.identity);
        }
        internal void Drop()
        {
            releaseColliders = transform.parent ? transform.parent.GetComponentsInChildren<Collider>() : null;
            transform.SetParent(null, true);
            State = Phase.Dropped;
            hull.enabled = true;
            if (releaseColliders != null)
                foreach (var c in releaseColliders)
                    if (c && c != hull && !c.isTrigger && c.enabled)
                        Physics.IgnoreCollision(hull, c, true);
            heavy.Body.constraints &= ~RigidbodyConstraints.FreezeRotation;
            heavy.Body.isKinematic = false;
        }
        void FixedUpdate()
        {
            if (releaseColliders == null) return;
            foreach (var c in releaseColliders)
                if (c && c != hull && !c.isTrigger && c.enabled && c.gameObject.activeInHierarchy &&
                    hull.bounds.Intersects(c.bounds)) return;
            RestoreReleaseCollisions();
        }

        void RestoreReleaseCollisions()
        {
            if (releaseColliders == null) return;
            foreach (var c in releaseColliders)
                if (c && c != hull && !c.isTrigger && c.enabled)
                    Physics.IgnoreCollision(hull, c, false);
            releaseColliders = null;
        }

        internal void AllowGlyph() { if (State == Phase.Dropped) heavy.enabled = true; }
        internal void Dock(Transform slot)
        {
            RestoreReleaseCollisions();
            State = Phase.Docked;
            heavy.enabled = false;
            if (!heavy.Body.isKinematic) heavy.Body.linearVelocity = heavy.Body.angularVelocity = Vector3.zero;
            heavy.Body.isKinematic = true;
            transform.SetParent(slot, true);
            transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        }
    }
}
