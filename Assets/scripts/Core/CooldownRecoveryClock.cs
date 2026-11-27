using System;
using System.Collections.Generic;

namespace NarutoVR.SealCombat.Rpg
{
    // A cooldown deadline on this clock is measured in seconds of recovery work.
    // Overlapping fields use the strongest active rate; each expires on game time.
    internal sealed class CooldownRecoveryClock
    {
        readonly Dictionary<object, (double end, float rate)> fields = new();
        double settledAt, extra;

        public double Read(double now)
        {
            double bonus = extra;
            for (double start = settledAt; start < now;)
            {
                double next = now;
                float strongest = 1f;
                foreach (var field in fields.Values)
                {
                    if (field.end <= start) continue;
                    next = Math.Min(next, field.end);
                    strongest = Math.Max(strongest, field.rate);
                }
                bonus += (next - start) * (strongest - 1);
                start = next;
            }
            return now + bonus;
        }

        public void Set(object source, double now, double end, float rate)
        {
            extra = Read(now) - now;
            settledAt = now;
            if (end > now && rate > 1) fields[source] = (end, rate);
            else fields.Remove(source);
        }
    }
}
