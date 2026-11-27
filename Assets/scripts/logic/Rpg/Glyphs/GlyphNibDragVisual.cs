using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace NarutoVR.SealCombat.Rpg
{
    /// <summary>The approved B brush deflection. Only the rendered bristle vertices can move.</summary>
    [DisallowMultipleComponent]
    public sealed class GlyphNibDragVisual : MonoBehaviour
    {
        internal const float RootZ = .0677f, TipZ = .108f;
        internal const float MaxBend = .0045f;
        const double GainSeconds = .080, FollowSeconds = .045, ReturnSeconds = .060;
        const double DeadSpeed = .005, MaxGap = .120;
        const float MinimalVisibleBend = .0000001f;
        [SerializeField] Transform brushVisual;

        MeshBuffer[] buffers;
        bool triedBuffers;
        Transform previousBoard;
        double previousTime = double.NaN;
        Vector3 previousPoint;
        bool previousContact;
        Vector2 boardBend;
        Vector3 appliedBend;

        internal void Observe(Transform board, Vector3 nibWorld, bool tracked, bool contact, double now)
        {
            if (!isActiveAndEnabled) return;
            if (!double.IsFinite(now))
            {
                ResetDrag();
                return;
            }

            Vector3 point = tracked && board ? board.InverseTransformPoint(nibWorld) : default;
            bool active = tracked && board && contact &&
                float.IsFinite(point.x) && float.IsFinite(point.y) && float.IsFinite(point.z) &&
                Mathf.Abs(point.x) <= GlyphBoard.HalfWidth && Mathf.Abs(point.y) <= GlyphBoard.HalfHeight;
            double dt = now - previousTime;
            if (dt == 0) return;

            if (double.IsNaN(dt) || dt < 0 || dt > MaxGap || board != previousBoard)
                boardBend = Vector2.zero;
            else if (!active || !previousContact)
                Follow(Vector2.zero, dt, ReturnSeconds);
            else
            {
                double vx = (point.x - (double)previousPoint.x) / dt;
                double vy = (point.y - (double)previousPoint.y) / dt;
                double speed = Math.Sqrt(vx * vx + vy * vy);
                if (!double.IsFinite(speed))
                    boardBend = Vector2.zero;
                else if (speed > DeadSpeed)
                {
                    double gain = Math.Min(MaxBend, speed * GainSeconds) / speed;
                    Follow(new Vector2((float)(-vx * gain), (float)(-vy * gain)), dt, FollowSeconds);
                }
                else
                    Follow(Vector2.zero, dt, ReturnSeconds);
            }

            previousBoard = board;
            previousTime = now;
            previousPoint = point;
            previousContact = active;
            ApplyBend(board
                ? transform.InverseTransformVector(board.TransformVector(new Vector3(boardBend.x, boardBend.y, 0)))
                : Vector3.zero);
        }

        void Follow(Vector2 target, double dt, double tau) =>
            boardBend += (target - boardBend) * (float)(1 - Math.Exp(-dt / tau));

        internal void ResetDrag()
        {
            previousBoard = null;
            previousTime = double.NaN;
            previousPoint = default;
            previousContact = false;
            boardBend = Vector2.zero;
            ApplyBend(Vector3.zero);
        }

        void ApplyBend(Vector3 bend)
        {
            if (bend.sqrMagnitude < MinimalVisibleBend * MinimalVisibleBend)
                bend = Vector3.zero;
            if (appliedBend.Equals(bend)) return;
            if (!triedBuffers) CreateBuffers();
            if (buffers == null) return;
            foreach (var buffer in buffers) buffer.Apply(bend);
            appliedBend = bend;
        }

        void CreateBuffers()
        {
            triedBuffers = true;
            var filters = brushVisual && brushVisual.IsChildOf(transform)
                ? brushVisual.GetComponentsInChildren<MeshFilter>(true) : Array.Empty<MeshFilter>();
            if (filters.Length != 2 || !filters[0].sharedMesh || !filters[1].sharedMesh ||
                filters[0].sharedMesh == filters[1].sharedMesh ||
                !filters[0].sharedMesh.isReadable || !filters[1].sharedMesh.isReadable)
            {
                Debug.LogWarning("墨规笔锋拖弯需要 BrushVisual 下两档独立、可读的游戏网格。", this);
                return;
            }
            var near = new MeshBuffer(transform, filters[0]);
            var far = new MeshBuffer(transform, filters[1]);
            if (!near.HasBristles || !far.HasBristles)
            {
                Debug.LogWarning("墨规笔锋拖弯找不到两档原尺寸的笔毫顶点。", this);
                return;
            }
            buffers = new[] { near, far };
            foreach (var buffer in buffers) buffer.Bind();
        }

        void OnDisable() => ResetDrag();

        void OnDestroy()
        {
            if (buffers == null) return;
            foreach (var buffer in buffers) buffer.Dispose();
        }

        // Compose authored child transforms without subtracting large world coordinates.
        // The exact endpoint classification also applies to the chapter's +80 m stone.
        internal static Matrix4x4 MeshToPen(Transform pen, Transform mesh)
        {
            var relative = Matrix4x4.identity;
            for (var part = mesh; part != pen; part = part.parent)
                relative = Matrix4x4.TRS(part.localPosition, part.localRotation, part.localScale) * relative;
            return relative;
        }

        sealed class MeshBuffer
        {
            readonly struct Bristle
            {
                public readonly int index;
                public readonly float weight;
                public readonly Vector3 gradient;
                public Bristle(int index, float weight, Vector3 gradient)
                { this.index = index; this.weight = weight; this.gradient = gradient; }
            }

            readonly MeshFilter filter;
            readonly Mesh source;
            readonly Matrix4x4 penToMesh;
            readonly Vector3[] baseVertices, baseNormals, vertices, normals;
            readonly Vector4[] baseTangents, tangents;
            readonly Bristle[] bristles;
            readonly Bounds restBounds;
            Mesh mesh;

            public bool HasBristles => bristles.Length > 0;

            public MeshBuffer(Transform pen, MeshFilter filter)
            {
                this.filter = filter;
                source = filter.sharedMesh;
                restBounds = source.bounds;
                Matrix4x4 meshToPen = MeshToPen(pen, filter.transform);
                penToMesh = meshToPen.inverse;
                var penZ = new Vector3(meshToPen.m20, meshToPen.m21, meshToPen.m22);
                baseVertices = source.vertices;
                baseNormals = source.normals;
                baseTangents = source.tangents;
                vertices = (Vector3[])baseVertices.Clone();
                normals = (Vector3[])baseNormals.Clone();
                tangents = (Vector4[])baseTangents.Clone();
                var selected = new List<Bristle>();
                for (int i = 0; i < baseVertices.Length; i++)
                {
                    float z = meshToPen.MultiplyPoint3x4(baseVertices[i]).z;
                    // The authored bristle rings occupy this range in both LODs. Copper starts
                    // behind the root plane. The root ring and point of contact stay fixed.
                    if (z <= RootZ + 1e-7f || z >= TipZ - 1e-7f) continue;
                    float angle = Mathf.PI * ((z - RootZ) / (TipZ - RootZ));
                    float sine = Mathf.Sin(angle);
                    float derivative = Mathf.Sin(2 * angle) * (Mathf.PI / (TipZ - RootZ));
                    selected.Add(new Bristle(i, sine * sine, penZ * derivative));
                }
                bristles = selected.ToArray();
            }

            public void Bind()
            {
                mesh = Instantiate(source);
                mesh.name = source.name + " (Nib Drag)";
                mesh.MarkDynamic();
                filter.sharedMesh = mesh;
            }

            public void Apply(Vector3 penBend)
            {
                Vector3 displacement = penToMesh.MultiplyVector(penBend);
                bool neutral = penBend.Equals(Vector3.zero);
                foreach (var bristle in bristles)
                {
                    int i = bristle.index;
                    vertices[i] = baseVertices[i] + bristle.weight * displacement;
                    // The Jacobian is I + displacement * gradient^T. Transform imported
                    // normals by its inverse transpose and tangents by the forward Jacobian.
                    var g = bristle.gradient;
                    if (normals.Length != 0)
                    {
                        var n = baseNormals[i];
                        normals[i] = neutral ? n : (n - g * (Vector3.Dot(displacement, n) /
                            (1 + Vector3.Dot(g, displacement)))).normalized;
                    }
                    if (tangents.Length != 0)
                    {
                        var original = baseTangents[i];
                        var t = new Vector3(original.x, original.y, original.z);
                        if (!neutral) t = (t + displacement * Vector3.Dot(g, t)).normalized;
                        tangents[i] = new Vector4(t.x, t.y, t.z, original.w);
                    }
                }
                const MeshUpdateFlags flags = MeshUpdateFlags.DontRecalculateBounds;
                mesh.SetVertices(vertices, 0, vertices.Length, flags);
                if (normals.Length != 0) mesh.SetNormals(normals, 0, normals.Length, flags);
                if (tangents.Length != 0) mesh.SetTangents(tangents, 0, tangents.Length, flags);
                var bounds = restBounds;
                bounds.Expand(2 * new Vector3(Mathf.Abs(displacement.x), Mathf.Abs(displacement.y), Mathf.Abs(displacement.z)));
                mesh.bounds = bounds;
            }

            public void Dispose()
            {
                if (filter && filter.sharedMesh == mesh) filter.sharedMesh = source;
                if (Application.isPlaying) Destroy(mesh);
                else DestroyImmediate(mesh);
            }
        }
    }
}
