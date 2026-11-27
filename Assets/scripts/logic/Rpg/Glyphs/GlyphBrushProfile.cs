using System.Collections.Generic;
using UnityEngine;

namespace NarutoVR.SealCombat.Rpg
{
    // Drawing-only profile. Distances are local to the board and times come from the accepted nib samples.
    internal sealed class GlyphBrushProfile
    {
        internal readonly struct Tip
        {
            public readonly float travelMm, width, wetness, taper;

            public Tip(float travelMm, float width, float wetness, float taper)
            {
                this.travelMm = travelMm;
                this.width = width;
                this.wetness = wetness;
                this.taper = taper;
            }

            public static Tip Lerp(Tip from, Tip to, float t) => new(
                Mathf.Lerp(from.travelMm, to.travelMm, t), Mathf.Lerp(from.width, to.width, t),
                Mathf.Lerp(from.wetness, to.wetness, t), Mathf.Lerp(from.taper, to.taper, t));
        }

        readonly struct Motion
        {
            public readonly float travelMm, speed, braking, turn;
            public readonly Vector2 direction;

            public Motion(float travelMm, float speed, float braking, float turn, Vector2 direction)
            {
                this.travelMm = travelMm;
                this.speed = speed;
                this.braking = braking;
                this.turn = turn;
                this.direction = direction;
            }
        }

        readonly List<int> starts = new();
        readonly List<Motion> motion = new();

        public void Update(IReadOnlyList<GlyphBoard.NibSample> samples)
        {
            if (motion.Count > samples.Count) motion.RemoveRange(samples.Count, motion.Count - samples.Count);
            while (starts.Count > 0 && starts[^1] >= samples.Count) starts.RemoveAt(starts.Count - 1);

            for (int i = motion.Count; i < samples.Count; i++)
            {
                var sample = samples[i];
                if (sample.strokeIndex == starts.Count)
                {
                    starts.Add(i);
                    motion.Add(new Motion(0, 0, 0, 0, Vector2.zero));
                    continue;
                }

                var previous = motion[i - 1];
                Vector2 delta = sample.boardPosition - samples[i - 1].boardPosition;
                float distance = delta.magnitude;
                Vector2 direction = delta.normalized;
                float dt = Mathf.Clamp((float)(sample.time - samples[i - 1].time), .008f, .35f);
                float measuredSpeed = Mathf.Min(distance / dt, 1.25f);
                float response = 1f - Mathf.Exp(-dt / .045f);
                bool firstEdge = previous.travelMm == 0;
                float speed = firstEdge ? measuredSpeed : Mathf.Lerp(previous.speed, measuredSpeed, response);
                float braking = firstEdge ? 0 : Mathf.Lerp(previous.braking,
                    Mathf.Clamp((previous.speed - speed) / (dt * 3f), -1, 1), response);
                float turn = firstEdge ? 0 : Mathf.InverseLerp(.08f, 1.2f,
                    (direction - previous.direction).magnitude);
                motion.Add(new Motion(previous.travelMm + distance * 1000f, speed, braking, turn, direction));
            }
        }

        public Tip At(int stroke, int point, int length)
        {
            int start = starts[stroke];
            var end = motion[start + length - 1];
            var current = motion[start + point];
            // A corner belongs to the vertex shared by its incoming and outgoing edges.
            var outgoing = point + 1 < length ? motion[start + point + 1] : current;
            float fast = Fast(point == 0 ? outgoing.speed : current.speed);
            float cornerPress = outgoing.turn * (1f - Fast(outgoing.speed)) *
                Mathf.Lerp(.45f, 1f, Mathf.Clamp01(outgoing.braking));
            if (point == 0 || point + 1 == length) cornerPress = 0;
            float width = Mathf.Lerp(1.42f, .74f, fast) + .25f * cornerPress;
            float wetness = Mathf.Clamp01(Mathf.Lerp(.98f, .48f, fast) + .22f * cornerPress -
                .08f * Mathf.Clamp01(current.travelMm / 190f));
            if (point == 0) width *= .86f;

            float taperMm = Mathf.Min(end.travelMm * .55f,
                Mathf.Lerp(4f, 13f, Fast(end.speed)) - 3f * end.braking);
            float taper = taperMm > 0 ? (end.travelMm - current.travelMm) / taperMm : 1f;
            return new Tip(current.travelMm, width, wetness, taper);
        }

        static float Fast(float speed) => Mathf.InverseLerp(.024f, .38f, speed);
    }
}
