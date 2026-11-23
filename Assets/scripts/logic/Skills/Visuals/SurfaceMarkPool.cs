using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace NarutoVR.SealCombat
{
    /// <summary>复用有限数量的场景贴花。对象由施法者管理，接触点以命中物体的局部坐标保存。</summary>
    internal sealed class SurfaceMarkPool
    {
        sealed class Mark
        {
            public DecalProjector decal;
            public Collider receiver;
            public SurfaceMarkStyle style;
            public object stroke;
            public Vector3 localStart, localEnd, localNormal, localTangent;
            public double started;
        }

        readonly List<Mark> marks = new();
        readonly System.Random roll = new();

        internal void Stamp(SurfaceContact hit, SurfaceMarkStyle style, Vector3 travel, double now,
            int capacity, object stroke = null, bool continuousFire = false)
        {
            if (!style || !style.Accepts(hit.Collider) || capacity < 1) return;
            // 接收物体可能卸载其所属的附加场景，连同池内投影对象一起销毁。
            for (int i = marks.Count - 1; i >= 0; i--)
                if (!marks[i].decal) marks.RemoveAt(i);
            var surface = hit.Collider.transform;
            // 一次挥剑在同一面上延长一条痕；持续火焰在原位刷新，扫到新位置再盖印。
            if (stroke != null)
                foreach (var existing in marks)
                {
                    if (existing.stroke != stroke || existing.receiver != hit.Collider || existing.style != style ||
                        now >= existing.started + style.holdSeconds + style.fadeSeconds) continue;
                    var normal = surface.worldToLocalMatrix.transpose.MultiplyVector(existing.localNormal).normalized;
                    if (Vector3.Dot(normal, hit.Normal) < Mathf.Cos(style.joinAngle * Mathf.Deg2Rad)) continue;
                    if (continuousFire && Vector3.Distance(surface.TransformPoint(existing.localStart), hit.Point) > style.repeatSpacing) continue;
                    if (!continuousFire) existing.localEnd = surface.InverseTransformPoint(hit.Point);
                    existing.started = now;
                    Present(existing, now);
                    return;
                }

            Mark mark = null;
            foreach (var candidate in marks)
                if (!candidate.receiver || !candidate.decal.gameObject.activeSelf) { mark = candidate; break; }
            if (mark == null && marks.Count < capacity)
            {
                var go = new GameObject("场景技能痕迹");
                go.SetActive(false);
                SceneManager.MoveGameObjectToScene(go, surface.gameObject.scene);
                mark = new Mark { decal = go.AddComponent<DecalProjector>() };
                mark.decal.pivot = Vector3.zero;
                marks.Add(mark);
            }
            if (mark == null)
            {
                mark = marks[0];
                foreach (var other in marks) if (other.started < mark.started) mark = other;
            }
            Vector3 tangent = Vector3.ProjectOnPlane(travel, hit.Normal);
            if (tangent.sqrMagnitude < 1e-6f)
                tangent = Vector3.ProjectOnPlane(Mathf.Abs(hit.Normal.y) < .9f ? Vector3.up : Vector3.right, hit.Normal);
            if (style.randomRoll)
                tangent = Quaternion.AngleAxis((float)(roll.NextDouble() * 360), hit.Normal) * tangent;

            if (mark.decal.gameObject.scene != surface.gameObject.scene)
                SceneManager.MoveGameObjectToScene(mark.decal.gameObject, surface.gameObject.scene);
            mark.receiver = hit.Collider;
            mark.style = style;
            mark.stroke = stroke;
            mark.started = now;
            mark.localStart = mark.localEnd = surface.InverseTransformPoint(hit.Point);
            mark.localNormal = surface.localToWorldMatrix.transpose.MultiplyVector(hit.Normal);
            mark.localTangent = surface.worldToLocalMatrix.MultiplyVector(tangent.normalized);
            mark.decal.material = style.material;
            Present(mark, now);
            mark.decal.gameObject.SetActive(true);
        }

        static void Present(Mark mark, double now)
        {
            var surface = mark.receiver.transform;
            var start = surface.TransformPoint(mark.localStart);
            var normal = surface.worldToLocalMatrix.transpose.MultiplyVector(mark.localNormal).normalized;
            var along = Vector3.ProjectOnPlane(surface.TransformPoint(mark.localEnd) - start, normal);
            along = Vector3.ClampMagnitude(along, mark.style.maxStrokeLength);
            var tangent = along.sqrMagnitude > 1e-6f ? along : surface.localToWorldMatrix.MultiplyVector(mark.localTangent);
            var size = mark.style.size;
            if (along.sqrMagnitude > 1e-6f) size.y = Mathf.Min(mark.style.maxStrokeLength, Mathf.Max(size.x, along.magnitude));
            mark.decal.transform.SetPositionAndRotation(start + along * .5f, Quaternion.LookRotation(-normal, tangent));
            if (mark.decal.size != size) mark.decal.size = size;
            double fading = now - mark.started - mark.style.holdSeconds;
            float alpha = fading <= 0 ? 1 : mark.style.fadeSeconds > 0 ? Mathf.Clamp01(1 - (float)fading / mark.style.fadeSeconds) : 0;
            if (mark.decal.fadeFactor != alpha) mark.decal.fadeFactor = alpha;
        }

        internal void Tick(double now, int capacity)
        {
            for (int i = marks.Count - 1; i >= 0; i--)
            {
                var mark = marks[i];
                if (!mark.decal) { marks.RemoveAt(i); continue; }
                if (i >= capacity)
                {
                    Release(mark.decal.gameObject);
                    marks.RemoveAt(i);
                    continue;
                }
                if (!mark.decal.gameObject.activeSelf) continue;
                if (!mark.receiver || !mark.receiver.enabled || !mark.receiver.gameObject.activeInHierarchy || !mark.style ||
                    now >= mark.started + mark.style.holdSeconds + mark.style.fadeSeconds)
                {
                    mark.decal.gameObject.SetActive(false);
                    mark.stroke = null;
                    mark.receiver = null;
                }
                else Present(mark, now);
            }
        }

        internal void Clear()
        {
            foreach (var mark in marks)
            {
                if (!mark.decal) continue;
                Release(mark.decal.gameObject);
            }
            marks.Clear();
        }

        static void Release(GameObject go)
        {
            go.SetActive(false);
            if (Application.isPlaying) Object.Destroy(go); else Object.DestroyImmediate(go);
        }
    }
}
