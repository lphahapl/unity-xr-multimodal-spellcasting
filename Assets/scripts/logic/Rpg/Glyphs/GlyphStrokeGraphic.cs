using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace NarutoVR.SealCombat.Rpg
{
    /// <summary>一个 UI 网格显示全部笔画；每笔的局部坐标（米）转成画布毫米像素。</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class GlyphStrokeGraphic : MaskableGraphic
    {
        const float BrushMargin = 1.32f;
        [System.NonSerialized] public List<List<Vector2>> lines;
        [System.NonSerialized] public float width = 3;
        VisibleInk visible;
        GlyphBrushProfile brush;
        bool useVisibleInk, useBrush;

        internal void UpdateInk(List<List<Vector2>> samples, IReadOnlyList<GlyphBoard.NibSample> nibSamples, Material brushMaterial)
        {
            visible ??= new VisibleInk();
            visible.Update(samples);
            useVisibleInk = true;
            useBrush = brushMaterial;
            material = brushMaterial;
            if (useBrush)
            {
                brush ??= new GlyphBrushProfile();
                brush.Update(nibSamples);
            }
            SetVerticesDirty();
        }

        // During the snap, movingInk has the same samples as the handwritten source.
        // Once fully aligned, the graphic uses the template's own stroke topology.
        internal void RebuildSuccessInk(bool fullyAligned)
        {
            useVisibleInk = !fullyAligned;
            useBrush = false;
            material = null;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (lines == null) return;
            float half = width * .5f;
            if (useVisibleInk && visible != null)
            {
                foreach (var span in visible.spans)
                {
                    var line = lines[span.stroke];
                    if (useBrush) BrushSpan(mesh, line, span, half);
                    else if (span.end == 0) Dot(mesh, line[0] * 1000, half);
                    else
                    {
                        Vector2 start = line[span.end - 1], edge = line[span.end] - start;
                        Segment(mesh, (start + span.from * edge) * 1000, (start + span.to * edge) * 1000, half);
                    }
                }
                return;
            }
            foreach (var line in lines)
            {
                if (line.Count == 0) continue;
                Vector2 a = line[0] * 1000;
                Dot(mesh, a, half);
                for (int i = 1; i < line.Count; ++i)
                {
                    Vector2 b = line[i] * 1000;
                    Segment(mesh, a, b, half);
                    a = b;
                }
            }
        }

        void BrushSpan(VertexHelper mesh, List<Vector2> line, VisibleInk.Span span, float half)
        {
            var end = brush.At(span.stroke, span.end, line.Count);
            int index = mesh.currentVertCount;
            if (span.end == 0)
            {
                Vector2 center = line[0] * 1000f;
                float r = half * end.width * (line.Count == 1 ? 1f : .78f) * BrushMargin;
                BrushVertex(mesh, center + new Vector2(-r, -r), end, -BrushMargin, -BrushMargin, true);
                BrushVertex(mesh, center + new Vector2(-r, r), end, -BrushMargin, BrushMargin, true);
                BrushVertex(mesh, center + new Vector2(r, r), end, BrushMargin, BrushMargin, true);
                BrushVertex(mesh, center + new Vector2(r, -r), end, BrushMargin, -BrushMargin, true);
            }
            else
            {
                Vector2 start = line[span.end - 1];
                Vector2 edge = line[span.end] - start;
                Vector2 direction = edge.normalized;
                var begin = brush.At(span.stroke, span.end - 1, line.Count);
                Vector2 beginNormal = JoinNormal(line, span.end - 1, direction);
                Vector2 endNormal = JoinNormal(line, span.end, direction);
                var from = GlyphBrushProfile.Tip.Lerp(begin, end, span.from);
                var to = GlyphBrushProfile.Tip.Lerp(begin, end, span.to);
                Vector2 a = (start + span.from * edge) * 1000f;
                Vector2 b = (start + span.to * edge) * 1000f;
                Vector2 aOffset = Vector2.Lerp(beginNormal, endNormal, span.from) * (half * from.width * BrushMargin);
                Vector2 bOffset = Vector2.Lerp(beginNormal, endNormal, span.to) * (half * to.width * BrushMargin);
                BrushVertex(mesh, a - aOffset, from, -BrushMargin, from.travelMm, false);
                BrushVertex(mesh, a + aOffset, from, BrushMargin, from.travelMm, false);
                BrushVertex(mesh, b + bOffset, to, BrushMargin, to.travelMm, false);
                BrushVertex(mesh, b - bOffset, to, -BrushMargin, to.travelMm, false);
            }
            AddQuad(mesh, index);
        }

        static Vector2 JoinNormal(List<Vector2> line, int point, Vector2 segment)
        {
            Vector2 normal = Vector2.Perpendicular(segment);
            if (point == 0 || point + 1 == line.Count) return normal;
            Vector2 before = (line[point] - line[point - 1]).normalized;
            Vector2 after = (line[point + 1] - line[point]).normalized;
            if (Vector2.Dot(before, after) < -.8f) return normal;
            Vector2 bisector = Vector2.Perpendicular((before + after).normalized);
            return bisector / Mathf.Max(.72f, Vector2.Dot(normal, bisector));
        }

        void BrushVertex(VertexHelper mesh, Vector2 position, GlyphBrushProfile.Tip tip,
            float across, float along, bool dot)
        {
            Color tint = color;
            tint.a *= tip.wetness;
            var vertex = UIVertex.simpleVert;
            vertex.position = position;
            vertex.color = tint;
            vertex.uv0 = new Vector2(across, along);
            vertex.uv1 = new Vector2(tip.taper, dot ? 1 : 0);
            mesh.AddVert(vertex);
        }

        void Dot(VertexHelper mesh, Vector2 a, float half) =>
            Quad(mesh, a + new Vector2(-half, -half), a + new Vector2(-half, half),
                a + new Vector2(half, half), a + new Vector2(half, -half));

        void Segment(VertexHelper mesh, Vector2 a, Vector2 b, float half)
        {
            Vector2 offset = Vector2.Perpendicular(b - a).normalized * half;
            Quad(mesh, a - offset, a + offset, b + offset, b - offset);
        }

        void Quad(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            int index = mesh.currentVertCount;
            Color32 tint = color;
            mesh.AddVert(a, tint, Vector2.zero);
            mesh.AddVert(b, tint, Vector2.zero);
            mesh.AddVert(c, tint, Vector2.zero);
            mesh.AddVert(d, tint, Vector2.zero);
            AddQuad(mesh, index);
        }

        static void AddQuad(VertexHelper mesh, int index)
        {
            mesh.AddTriangle(index, index + 1, index + 2);
            mesh.AddTriangle(index, index + 2, index + 3);
        }

        sealed class VisibleInk
        {
            // In board metres. Probe at half the 3.6 mm ink width so a long, sparse
            // hand sample can leave an existing line and add a genuinely new edge.
            const float Tolerance = .0036f, Step = Tolerance * .5f, Cell = .012f;
            const float MinAlignment = .96f;

            internal struct Span
            {
                public int stroke, end;
                public float from, to;
            }

            internal readonly List<Span> spans = new();
            readonly Dictionary<Vector2Int, List<int>> cells = new();
            List<List<Vector2>> raw;
            int strokeCount, pointCount;

            internal void Update(List<List<Vector2>> source)
            {
                if (raw != source || source.Count < strokeCount)
                {
                    spans.Clear();
                    foreach (var bucket in cells.Values) bucket.Clear();
                    strokeCount = pointCount = 0;
                    raw = source;
                }
                for (int s = Mathf.Max(0, strokeCount - 1); s < source.Count; ++s)
                {
                    var line = source[s];
                    int first = s == strokeCount - 1 ? pointCount : 0;
                    for (int end = first; end < line.Count; ++end)
                    {
                        if (end == 0)
                        {
                            if (!Covered(line[0], Vector2.zero, line[0], line[0])) Add(s, 0, 0, 0);
                        }
                        else AppendEdge(s, end);
                    }
                }
                strokeCount = source.Count;
                pointCount = strokeCount == 0 ? 0 : source[strokeCount - 1].Count;
            }

            void AppendEdge(int s, int end)
            {
                Vector2 a = raw[s][end - 1], edge = raw[s][end] - a;
                Vector2 direction = edge.normalized;
                int count = Mathf.Max(1, Mathf.CeilToInt(edge.magnitude / Step));
                int first = -1;
                for (int i = 0; i < count; ++i)
                {
                    if (!Covered(a + edge * ((i + .5f) / count), direction, a, raw[s][end]))
                    {
                        if (first < 0) first = i;
                    }
                    else if (first >= 0)
                    {
                        Add(s, end, first / (float)count, i / (float)count);
                        first = -1;
                    }
                }
                if (first >= 0) Add(s, end, first / (float)count, 1);
            }

            static Vector2Int CellAt(Vector2 p) =>
                new Vector2Int(Mathf.FloorToInt(p.x / Cell), Mathf.FloorToInt(p.y / Cell));

            void Add(int s, int end, float from, float to)
            {
                int index = spans.Count;
                spans.Add(new Span { stroke = s, end = end, from = from, to = to });
                Vector2 a = raw[s][Mathf.Max(0, end - 1)], edge = raw[s][end] - a;
                Vector2 start = a + edge * from, delta = edge * (to - from);
                int steps = Mathf.Max(1, Mathf.CeilToInt(delta.magnitude / Cell));
                for (int i = 0; i <= steps; ++i)
                {
                    var center = CellAt(start + delta * (i / (float)steps));
                    // Every point to be queried is within half a cell of a registration
                    // sample, plus the 3.6 mm radius. One neighbouring cell suffices.
                    for (int y = -1; y <= 1; ++y)
                        for (int x = -1; x <= 1; ++x)
                        {
                            var key = center + new Vector2Int(x, y);
                            if (!cells.TryGetValue(key, out var bucket)) cells.Add(key, bucket = new List<int>());
                            if (bucket.Count == 0 || bucket[bucket.Count - 1] != index) bucket.Add(index);
                        }
                }
            }

            bool Covered(Vector2 point, Vector2 direction, Vector2 edgeStart, Vector2 edgeEnd)
            {
                if (!cells.TryGetValue(CellAt(point), out var bucket)) return false;
                bool dot = direction == Vector2.zero;
                foreach (int index in bucket)
                {
                    var span = spans[index];
                    var line = raw[span.stroke];
                    Vector2 start = line[Mathf.Max(0, span.end - 1)];
                    Vector2 edge = line[span.end] - start;
                    float lengthSq = edge.sqrMagnitude;
                    float along = Vector2.Dot(direction, edge);
                    if (!dot && (lengthSq == 0 || along * along < MinAlignment * MinAlignment * lengthSq)) continue;
                    float t = lengthSq > 0 ? Vector2.Dot(point - start, edge) / lengthSq : 0;
                    // A nearby endpoint is not an overlap along the tangent. In
                    // particular, keep the first few millimetres of a new extension.
                    if (!dot && (t < span.from || t > span.to)) continue;
                    Vector2 closest = start + edge * Mathf.Clamp(t, span.from, span.to);
                    if ((point - closest).sqrMagnitude > Tolerance * Tolerance) continue;
                    // A shallow crossing can also have nearly parallel tangents. If both
                    // ends of the new edge clearly leave the old ink on opposite sides,
                    // its middle belongs to the crossing, not to a retraced line.
                    Vector2 normal = Vector2.Perpendicular(edge);
                    float sideA = Vector2.Dot(edgeStart - start, normal);
                    float sideB = Vector2.Dot(edgeEnd - start, normal);
                    if (sideA * sideB < 0 && Mathf.Min(sideA * sideA, sideB * sideB) >
                        Tolerance * Tolerance * lengthSq) continue;
                    return true;
                }
                return false;
            }
        }
    }
}
