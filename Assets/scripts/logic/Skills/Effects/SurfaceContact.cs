using UnityEngine;

namespace NarutoVR.SealCombat
{
    /// <summary>可用于贴花的实体表面点。扫掠保留实际接触面，初始重叠从来向补查近处表面。</summary>
    internal readonly struct SurfaceContact
    {
        internal readonly Collider Collider;
        internal readonly Vector3 Point, Normal;
        internal SurfaceContact(RaycastHit hit)
        {
            Collider = hit.collider;
            Point = hit.point;
            Normal = hit.normal.normalized;
        }

        internal static bool TryResolve(Collider collider, Vector3 center, Vector3 incoming,
            float radius, RaycastHit? sweep, out SurfaceContact contact, bool includeTriggers = false)
        {
            contact = default;
            if (!collider || (collider.isTrigger && !includeTriggers)) return false;
            if (sweep is RaycastHit hit && hit.distance > 0 && hit.normal.sqrMagnitude > .5f)
            {
                // 球扫在棱边的法线可能朝向球心，短射线可取到真正的碰撞面法线。
                var probe = new Ray(hit.point + hit.normal * radius, -hit.normal);
                contact = collider.Raycast(probe, out var face, radius * 2) ? new(face) : new(hit);
                return true;
            }
            // 优先保留来向。擦墙或从表面内侧起步时，沿坐标轴选最近的可信表面。
            if (Probe(collider, center, incoming.normalized, radius, out var entry))
            {
                contact = new(entry);
                return true;
            }
            var best = float.PositiveInfinity;
            foreach (var axis in ProbeAxes)
            {
                if (!Probe(collider, center, axis, radius, out var face)) continue;
                float distance = (face.point - center).sqrMagnitude;
                if (distance >= best) continue;
                best = distance;
                contact = new(face);
            }
            return !float.IsPositiveInfinity(best);
        }

        static readonly Vector3[] ProbeAxes = { Vector3.forward, Vector3.back, Vector3.right, Vector3.left, Vector3.up, Vector3.down };

        static bool Probe(Collider collider, Vector3 center, Vector3 axis, float radius, out RaycastHit hit)
        {
            hit = default;
            if (axis.sqrMagnitude < .5f) return false;
            var ray = new Ray(center - axis * (2 * radius), axis);
            return collider.Raycast(ray, out hit, 3 * radius) &&
                (hit.point - center).sqrMagnitude <= radius * radius * 1.001f;
        }
    }
}
