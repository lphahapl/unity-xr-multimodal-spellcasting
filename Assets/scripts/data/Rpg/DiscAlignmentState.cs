using System;

namespace NarutoVR.SealCombat.Rpg
{
    /// <summary>方圆仪外盘带动内盘，内盘可独立调整。双盘对齐并在松手后保持稳定才结算。</summary>
    public sealed class DiscAlignmentState
    {
        const float OuterPeriod = 180f;
        const float InnerPeriod = 90f;
        public float OuterDegrees { get; private set; }
        public float InnerDegrees { get; private set; }
        public bool Solved { get; private set; }
        bool released;
        float stableSeconds;

        public void Restore(bool solved, float outerStart, float innerStart)
        {
            Solved = solved;
            OuterDegrees = solved ? 0 : outerStart;
            InnerDegrees = solved ? 0 : innerStart;
            BeginTurn();
        }

        public void BeginTurn() { released = false; stableSeconds = 0; }
        public void Release() => released = true;

        public void Turn(bool outer, float degrees)
        {
            if (Solved) return;
            if (outer) OuterDegrees += degrees;
            InnerDegrees += degrees;
        }

        public bool TrySettle(bool handsFree, float tolerance, float duration, float dt)
        {
            if (Solved || !released) return false;
            bool aligned = handsFree && Math.Abs(OuterDegrees - Snap(OuterDegrees, OuterPeriod)) <= tolerance &&
                Math.Abs(InnerDegrees - Snap(InnerDegrees, InnerPeriod)) <= tolerance;
            stableSeconds = aligned ? stableSeconds + dt : 0;
            if (!aligned || stableSeconds < duration) return false;
            OuterDegrees = Snap(OuterDegrees, OuterPeriod);
            InnerDegrees = Snap(InnerDegrees, InnerPeriod);
            Solved = true;
            return true;
        }

        static float Snap(float degrees, float period) => (float)Math.Round(degrees / period) * period;
    }
}
