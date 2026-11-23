using System;
using System.Collections.Generic;
using UnityEngine;

namespace NarutoVR.SealCombat.Rpg
{
    /// <summary>在画板局部平面检查范本各条边是否有墨迹支持，并检查墨迹能否由范本解释。</summary>
    public static class GlyphRecognizer
    {
        const int Span = 48;
        const float CornerTolerance = .018f;
        const float MinDirectionDot = .82f; // 同向或逆向，相差约 35 度以内。
        const float MinGuideEdge = .12f;
        const float MinInkEdge = .30f;

        readonly struct Edge
        {
            public readonly Vector2 start, unit;
            public readonly float length;

            public Edge(Vector2 a, Vector2 b)
            {
                start = a;
                length = (b - a).magnitude;
                unit = (b - a) / length;
            }
        }

        public static float Similarity(IReadOnlyList<Vector2[]> candidate, IReadOnlyList<Vector2[]> template)
        {
            var ink = Edges(candidate, 0);
            if (ink == null) return 0;
            float ScoreAt(float angle)
            {
                var guide = Edges(template, angle);
                if (guide == null) return 0;
                float required = Coverage(guide, ink, MinGuideEdge, true);
                return required <= 0f ? 0f : Mathf.Min(required, Coverage(ink, guide, MinInkEdge, false));
            }
            float straight = ScoreAt(0);
            return straight >= 1f ? straight : Mathf.Max(straight, Mathf.Max(ScoreAt(-7), ScoreAt(7)));
        }

        static List<Edge> Edges(IReadOnlyList<Vector2[]> strokes, float degrees)
        {
            if (strokes == null) return null;
            float cos = Mathf.Cos(degrees * Mathf.Deg2Rad), sin = Mathf.Sin(degrees * Mathf.Deg2Rad);
            Vector2 Rotate(Vector2 p) => new(cos * p.x - sin * p.y, sin * p.x + cos * p.y);
            var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            foreach (var stroke in strokes)
            {
                if (stroke == null || stroke.Length == 0) return null;
                for (int i = 0; i < stroke.Length; i++)
                {
                    var p = stroke[i];
                    if (!float.IsFinite(p.x) || !float.IsFinite(p.y)) return null;
                    if (i == 0 || p.Equals(stroke[i - 1])) continue;
                    var a = Rotate(stroke[i - 1]);
                    var b = Rotate(p);
                    min = Vector2.Min(min, Vector2.Min(a, b));
                    max = Vector2.Max(max, Vector2.Max(a, b));
                }
            }
            var size = max - min;
            float span = Mathf.Max(size.x, size.y);
            if (!float.IsFinite(span) || span < 1e-6f) return null;
            var center = min + size * .5f;
            var scale = new Vector2(1f / Mathf.Max(size.x, span * .82f), 1f / Mathf.Max(size.y, span * .82f));
            var edges = new List<Edge>();
            foreach (var stroke in strokes)
            {
                if (stroke.Length < 2) continue;
                var points = new Vector2[stroke.Length];
                for (int i = 0; i < points.Length; ++i)
                    points[i] = Vector2.Scale(Rotate(stroke[i]) - center, scale);
                Simplify(points, edges);
            }
            // 长时间反复描同一处时，先合并起终点都落在同一对细格内的边。
            // 细格宽度约为符宽的 0.5%，远小于笔尖和匹配容差。
            if (edges.Count > 128) MergeRetracedEdges(edges);
            return edges.Count > 0 ? edges : null;
        }

        static void MergeRetracedEdges(List<Edge> edges)
        {
            const int precision = Span * 4;
            int Cell(Vector2 point) => Mathf.RoundToInt((point.y + .5f) * precision) * (precision + 1)
                + Mathf.RoundToInt((point.x + .5f) * precision);
            var seen = new HashSet<(int, int)>();
            int kept = 0;
            for (int i = 0; i < edges.Count; ++i)
            {
                var edge = edges[i];
                int a = Cell(edge.start), b = Cell(edge.start + edge.unit * edge.length);
                if (seen.Add((Math.Min(a, b), Math.Max(a, b)))) edges[kept++] = edge;
            }
            edges.RemoveRange(kept, edges.Count - kept);
        }

        // Douglas–Peucker 在每一笔内找拐点，抬笔处永远不会补出连线。
        static void Simplify(Vector2[] points, List<Edge> edges)
        {
            var work = new Stack<(int first, int last)>();
            work.Push((0, points.Length - 1));
            while (work.Count > 0)
            {
                var (first, last) = work.Pop();
                Vector2 chord = points[last] - points[first];
                float squared = chord.sqrMagnitude;
                float largest = CornerTolerance * CornerTolerance;
                int corner = -1, innerCorner = -1;
                int margin = (last - first) / 3;
                float innerError = largest;
                for (int i = first + 1; i < last; ++i)
                {
                    Vector2 offset = points[i] - points[first];
                    float along = squared > 0 ? Mathf.Clamp01(Vector2.Dot(offset, chord) / squared) : 0;
                    float error = (offset - chord * along).sqrMagnitude;
                    if (i >= first + margin && i <= last - margin && error > innerError)
                    {
                        innerError = error;
                        innerCorner = i;
                    }
                    if (error <= largest) continue;
                    largest = error;
                    corner = i;
                }
                if (corner >= 0)
                {
                    // 在相近的误差峰中选中段，避免一长串锯齿被逐个剥离造成二次耗时。
                    if (innerCorner >= 0 && innerError >= largest * .7f) corner = innerCorner;
                    work.Push((corner, last));
                    work.Push((first, corner));
                }
                else if (squared > 1e-10f)
                    edges.Add(new Edge(points[first], points[last]));
            }
        }

        static float Coverage(List<Edge> query, List<Edge> reference, float requiredEdgeLength, bool checkGaps)
        {
            int side = Span + 1;
            var misses = new byte[side * side];
            var aligned = new List<(Edge edge, float angleMiss)>(reference.Count);
            float weakest = 1f;
            foreach (var edge in query)
            {
                aligned.Clear();
                foreach (var target in reference)
                {
                    float dot = Mathf.Abs(Vector2.Dot(edge.unit, target.unit));
                    if (dot >= MinDirectionDot)
                        aligned.Add((target, Mathf.Max(0f, (.967f - dot) / .25f)));
                }
                if (aligned.Count == 0 && edge.length >= requiredEdgeLength) return 0f;
                int count = Mathf.Max(2, Mathf.CeilToInt(edge.length * Span * (checkGaps ? 2 : 1)));
                float sum = 0;
                int firstHit = -1, lastHit = -1, hits = 0;
                for (int i = 0; i < count; ++i)
                {
                    var p = edge.start + edge.unit * (edge.length * ((i + .5f) / count));
                    float miss = Miss(p, aligned, out bool covered);
                    if (covered)
                    {
                        if (firstHit < 0) firstHit = i;
                        lastHit = i;
                        hits++;
                    }
                    sum += miss;
                    int x = Mathf.Clamp(Mathf.RoundToInt((p.x + .5f) * Span), 0, Span);
                    int y = Mathf.Clamp(Mathf.RoundToInt((p.y + .5f) * Span), 0, Span);
                    int cell = y * side + x;
                    // 0 为空，1 为吻合，255 为多余；重描时每格仍只算一次。
                    misses[cell] = Math.Max(misses[cell], (byte)(1 + Mathf.RoundToInt(254 * miss)));
                }
                if (edge.length >= requiredEdgeLength)
                {
                    weakest = Mathf.Min(weakest, 1f - sum / count);
                    // 两端可以因手写比例而缩短。两个实画部分之间的大段空白不能靠端点容差填满。
                    if (checkGaps && hits > 0)
                    {
                        float filled = hits / (float)(lastHit - firstHit + 1);
                        weakest = Mathf.Min(weakest, Mathf.Clamp01((filled - .35f) / .45f));
                    }
                    if (weakest == 0f) return 0f;
                }
            }
            float total = 0;
            int occupied = 0;
            foreach (byte value in misses)
            {
                if (value == 0) continue;
                total += (value - 1) / 254f;
                ++occupied;
            }
            return occupied == 0 ? 0f : Mathf.Min(weakest, 1f - total / occupied);
        }

        static float Miss(Vector2 p, List<(Edge edge, float angleMiss)> aligned, out bool covered)
        {
            float best = 1f;
            covered = false;
            foreach (var (edge, angleMiss) in aligned)
            {
                Vector2 offset = p - edge.start;
                float projection = Vector2.Dot(offset, edge.unit);
                float endGap = Mathf.Max(-projection, projection - edge.length);
                float sideGap = Mathf.Abs(offset.x * edge.unit.y - offset.y * edge.unit.x);
                // 手绘边可以平移；沿边只宽容短缺口，防止断掉半根边也被认全。
                if (endGap <= .003f && sideGap <= .18f) covered = true;
                float distanceMiss = Mathf.Max((sideGap - .135f) / .13f, (endGap - .045f) / .15f);
                best = Mathf.Min(best, Mathf.Max(angleMiss, distanceMiss));
                if (best == 0f && covered) return 0f;
            }
            return best;
        }
    }
}
