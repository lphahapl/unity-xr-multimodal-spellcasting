using System.Collections.Generic;
using UnityEngine;

namespace NarutoVR.SealCombat.Rpg
{
    // The overlay measures ink on the displayed paper. Only GlyphTemplate.Score decides a match.
    internal sealed class GlyphStoryStrokeGuide
    {
        readonly GlyphTemplate.StrokeHint[] hints;
        readonly List<List<Vector2>> printed;
        public int Current { get; private set; }
        public List<Vector2> CurrentLine => Current < printed.Count ? printed[Current] : null;
        public string Instruction { get; private set; }
        public string Feedback { get; private set; }
        public string Rejection => Current == printed.Count ? "两笔已留，请撤销多余线条或对齐范本" : Feedback;

        public GlyphStoryStrokeGuide(GlyphTemplate.StrokeHint[] hints, List<List<Vector2>> printed)
        {
            this.hints = hints;
            this.printed = printed;
        }

        public void Read(List<List<Vector2>> ink)
        {
            int completed = 0, last = -1;
            Current = printed.Count;
            for (int i = 0; i < printed.Count; i++)
            {
                var line = printed[i];
                bool traced = line.Count >= 2;
                for (int j = 1; traced && j < line.Count; j++)
                    traced = CoversEdge(ink, line[j - 1], line[j]);
                if (traced) { completed++; last = i; }
                else if (Current == printed.Count) Current = i;
            }
            if (Current == printed.Count)
            {
                Instruction = "退笔，停稳后辨符";
                Feedback = "两笔已留，停稳后辨符";
                return;
            }
            var hint = hints[Current];
            Instruction = $"第{Current + 1}/{printed.Count}笔  {hint.name} · {hint.direction}";
            if (completed == 0)
                Feedback = ink.Count == 0 ? "沿亮线从“起”画到“止”" : $"{hint.name}未描全，沿亮线补画";
            else
                Feedback = $"{hints[last].name}已留，抬笔再补{hint.name}";
        }

        // Sample along each printed edge. Tiny jitter and strokes drawn in reverse or in pieces count.
        static bool CoversEdge(List<List<Vector2>> ink, Vector2 a, Vector2 b)
        {
            const int bins = 12;
            const float tolerance = .022f;
            var axis = b - a;
            float length = axis.magnitude;
            if (length < .001f) return true;
            axis /= length;
            int mask = 0, hits = 0;
            foreach (var stroke in ink)
            {
                for (int i = 1; i < stroke.Count; ++i)
                {
                    var start = stroke[i - 1];
                    var delta = stroke[i] - start;
                    float along = Vector2.Dot(axis, delta);
                    if (Mathf.Abs(along) < .0001f || along * along < .45f * delta.sqrMagnitude) continue;
                    float s = Vector2.Dot(start - a, axis);
                    int first = Mathf.Max(0, Mathf.CeilToInt(Mathf.Min(s, s + along) / length * bins - .5f));
                    int last = Mathf.Min(bins - 1, Mathf.FloorToInt(Mathf.Max(s, s + along) / length * bins - .5f));
                    for (int j = first; j <= last; j++)
                    {
                        if ((mask & (1 << j)) != 0) continue;
                        float d = (j + .5f) * length / bins;
                        if ((start + delta * ((d - s) / along) - (a + axis * d)).sqrMagnitude > tolerance * tolerance) continue;
                        mask |= 1 << j;
                        if (++hits >= 9) return true;
                    }
                }
            }
            return false;
        }
    }
}
