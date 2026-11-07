#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.Threading;
using UnityEngine;

namespace NarutoVR.SealCombat.Rpg
{
    // Editor-only audio-thread capture. The callback never allocates or writes files.
    public sealed class EditorBgmAudioCapture : MonoBehaviour
    {
        [Serializable] public struct BufferStamp
        {
            public int index, sampleOffset, sampleCount, frameCount, channels;
            public long stopwatchTicks;
            public double dspTime, stopwatchSeconds, dspDelta, stopwatchDelta, rms, peak;
        }
        public sealed class Snapshot
        {
            public float[] samples;
            public BufferStamp[] buffers;
            public int sampleCount, bufferCount, channels, sampleRate, errorFlags;
            public long clockOriginTicks, clockFrequency;
        }
        float[] samples;
        BufferStamp[] buffers;
        int recording, publishedBuffers, expectedChannels, sampleRate, errorFlags;
        long origin;

        public int BufferCount => Volatile.Read(ref publishedBuffers);
        public int ErrorFlags => Volatile.Read(ref errorFlags);
        public long LastCallbackTicks => BufferCount > 0 ? buffers[BufferCount - 1].stopwatchTicks : 0;
        public int CapturedSamples => BufferCount > 0 ? buffers[BufferCount - 1].sampleOffset + buffers[BufferCount - 1].sampleCount : 0;

        public void Configure(int rate, int channels, double seconds, int dspBufferSize)
        {
            if (rate <= 0 || channels < 1 || channels > 8 || seconds <= 0)
                throw new ArgumentOutOfRangeException(nameof(seconds));
            long count = (long)Math.Ceiling(rate * seconds) * channels;
            if (count > 64L * 1024 * 1024) throw new InvalidOperationException("Capture exceeds the 256 MiB sample budget.");
            sampleRate = rate; expectedChannels = channels;
            samples = new float[(int)count];
            int minFrames = Math.Max(32, dspBufferSize / 4);
            buffers = new BufferStamp[(int)Math.Ceiling(rate * seconds / minFrames) + 256];
            publishedBuffers = errorFlags = 0;
            origin = Stopwatch.GetTimestamp();
            Volatile.Write(ref recording, 1);
        }
        public Snapshot Seal()
        {
            Volatile.Write(ref recording, 0);
            // Only the already-published prefix is read. An in-flight callback can
            // append another buffer, but cannot modify any part of this prefix.
            int count = BufferCount;
            return new Snapshot { samples = samples, buffers = buffers, bufferCount = count,
                sampleCount = count == 0 ? 0 : buffers[count - 1].sampleOffset + buffers[count - 1].sampleCount,
                channels = expectedChannels, sampleRate = sampleRate, errorFlags = ErrorFlags,
                clockOriginTicks = origin, clockFrequency = Stopwatch.Frequency };
        }
        void OnDisable() => Volatile.Write(ref recording, 0);
        void OnAudioFilterRead(float[] data, int channels)
        {
            if (Volatile.Read(ref recording) == 0 || samples == null) return;
            int index = Volatile.Read(ref publishedBuffers);
            if (channels != expectedChannels) { Volatile.Write(ref errorFlags, errorFlags | 1); return; }
            int offset = index == 0 ? 0 : buffers[index - 1].sampleOffset + buffers[index - 1].sampleCount;
            if (index >= buffers.Length || offset + data.Length > samples.Length)
            { Volatile.Write(ref errorFlags, errorFlags | 2); Volatile.Write(ref recording, 0); return; }
            long ticks = Stopwatch.GetTimestamp();
            double dsp = AudioSettings.dspTime, sum = 0, peak = 0;
            for (int i = 0; i < data.Length; i++)
            {
                float value = data[i]; samples[offset + i] = value;
                if (float.IsNaN(value) || float.IsInfinity(value)) Volatile.Write(ref errorFlags, errorFlags | 4);
                sum += (double)value * value; peak = Math.Max(peak, Math.Abs(value));
            }
            buffers[index] = new BufferStamp { index = index, sampleOffset = offset, sampleCount = data.Length,
                frameCount = data.Length / channels, channels = channels, stopwatchTicks = ticks,
                dspTime = dsp, stopwatchSeconds = (double)(ticks - origin) / Stopwatch.Frequency,
                dspDelta = index == 0 ? 0 : dsp - buffers[index - 1].dspTime,
                stopwatchDelta = index == 0 ? 0 : (double)(ticks - buffers[index - 1].stopwatchTicks) / Stopwatch.Frequency,
                rms = Math.Sqrt(sum / data.Length), peak = peak };
            Volatile.Write(ref publishedBuffers, index + 1);
        }
    }
}
#endif
