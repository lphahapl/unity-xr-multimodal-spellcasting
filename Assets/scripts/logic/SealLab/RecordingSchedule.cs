using System;

namespace NarutoVR.SealLab
{
    public enum RecordingPhase { Idle, Preparing, Capturing }

    // Uses a monotonic clock; recording deadlines do not depend on new hand frames.
    public sealed class RecordingSchedule
    {
        public RecordingPhase Phase { get; private set; }
        public double StartsAt { get; private set; }
        public double EndsAt { get; private set; }
        public void Arm(double now, double preparation, double duration)
        {
            if (!double.IsFinite(now) || !double.IsFinite(preparation) || preparation < 0 ||
                !double.IsFinite(duration) || duration <= 0) throw new ArgumentOutOfRangeException(nameof(duration), "录制时间必须为有限数值；准备时间不能为负，录制时长必须大于零。");
            StartsAt = now + preparation; EndsAt = StartsAt + duration;
            Phase = RecordingPhase.Preparing;
        }
        public RecordingPhase Advance(double now)
        {
            if (Phase == RecordingPhase.Idle) return Phase;
            // If the app stalls through the entire window, do not start a surprise recording.
            if (now >= EndsAt) Phase = RecordingPhase.Idle;
            else if (now >= StartsAt) Phase = RecordingPhase.Capturing;
            return Phase;
        }
        public double Remaining(double now) => Phase == RecordingPhase.Idle ? 0 :
            Math.Max(0, (Phase == RecordingPhase.Preparing ? StartsAt : EndsAt) - now);
        public void Cancel() => Phase = RecordingPhase.Idle;
    }
}
