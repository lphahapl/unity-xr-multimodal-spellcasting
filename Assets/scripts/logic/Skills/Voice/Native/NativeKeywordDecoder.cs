using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace NarutoVR.SealCombat
{
    /// <summary>Capture-clock times in seconds, already mapped to the caller's monotonic epoch.
    /// DetectedAt is the end of the audio frame that first produced the keyword, not wall time.</summary>
    public readonly struct NativeKeywordHit
    {
        public string Phrase { get; }
        public double EndedAt { get; }
        public double DetectedAt { get; }
        public NativeKeywordHit(string phrase, double endedAt, double detectedAt)
        { Phrase = phrase; EndedAt = endedAt; DetectedAt = detectedAt; }
    }

    /// <summary>Keyword finalization matching the desktop Python decoder. No Unity API or clock
    /// reads: all timing derives from captured sample positions. Events run on the owning worker.</summary>
    public sealed class NativeKeywordDecoder : IDisposable
    {
        public const float EnergyThreshold = .012f;
        public const double EndSilenceSeconds = .12;
        public const double MaximumPendingSeconds = .8;
        public const double DebounceSeconds = .6;
        readonly IKeywordEngine engine;
        readonly int ownerThread = Thread.CurrentThread.ManagedThreadId;
        readonly Dictionary<string, double> lastEmitted = new Dictionary<string, double>(StringComparer.Ordinal);
        string pendingPhrase;
        double pendingDetection, lastVoicedEnd = double.NaN, previousEnd = double.NaN;
        float[] frameBuffer;
        bool disposed, processing;
        int previousRate;

        public event Action<NativeKeywordHit> Hit;
        public string NativeVersion => engine.NativeVersion;

        /// <summary>Takes ownership; the engine must have been created on this same thread.</summary>
        public NativeKeywordDecoder(IKeywordEngine engine)
        { this.engine = engine ?? throw new ArgumentNullException(nameof(engine)); }

        /// <summary>Accept mono PCM. Large batches are divided into 20ms energy/decoder frames.
        /// firstSampleTime is capture time, never the time a worker dequeued the samples.</summary>
        public void Accept(float[] samples, int count, int sampleRate, double firstSampleTime)
        {
            CheckAccess();
            if (samples == null) throw new ArgumentNullException(nameof(samples));
            if (count < 0 || count > samples.Length) throw new ArgumentOutOfRangeException(nameof(count));
            if (sampleRate < 8000 || sampleRate > 192000) throw new ArgumentOutOfRangeException(nameof(sampleRate));
            if (!Finite(firstSampleTime)) throw new ArgumentOutOfRangeException(nameof(firstSampleTime));
            if (count == 0) return;
            double tolerance = Math.Max(2d / sampleRate, .002);
            if (Finite(previousEnd) && (previousRate != sampleRate || Math.Abs(firstSampleTime - previousEnd) > tolerance))
                throw new InvalidOperationException("Capture discontinuity: reset the decoder before accepting a new audio generation.");
            for (int i = 0; i < count; i++)
                if (float.IsNaN(samples[i]) || float.IsInfinity(samples[i])) throw new ArgumentException("PCM contains a non-finite sample.", nameof(samples));
            processing = true;
            try
            {
                int frameSize = sampleRate / 50;
                for (int offset = 0; offset < count; offset += frameSize)
                {
                    int size = Math.Min(frameSize, count - offset);
                    float[] frame = samples;
                    if (offset != 0 || count > frameSize)
                    {
                        if (frameBuffer == null || frameBuffer.Length != frameSize) frameBuffer = new float[frameSize];
                        Array.Copy(samples, offset, frameBuffer, 0, size);
                        frame = frameBuffer;
                    }
                    ProcessFrame(frame, size, sampleRate, firstSampleTime + (double)offset / sampleRate);
                }
                previousEnd = firstSampleTime + (double)count / sampleRate;
                previousRate = sampleRate;
            }
            finally { processing = false; }
        }

        void ProcessFrame(float[] samples, int count, int sampleRate, double firstSampleTime)
        {
            double frameEnd = firstSampleTime + (double)count / sampleRate;
            double squareSum = 0;
            for (int i = 0; i < count; i++) squareSum += (double)samples[i] * samples[i];
            if (Math.Sqrt(squareSum / count) >= EnergyThreshold) lastVoicedEnd = frameEnd;
            engine.Accept(samples, sampleRate, count);
            while (engine.IsReady)
            {
                string phrase = Normalize(engine.Decode());
                if (phrase.Length == 0) continue;
                // Finalize an older pending phrase before replacing it with a new detection.
                Emit(frameEnd, true);
                pendingPhrase = phrase;
                pendingDetection = frameEnd;
                engine.Reset();
            }
            Emit(frameEnd, false);
        }

        /// <summary>Explicit end-of-file only. Pause/stop must call Reset/Dispose, not Flush,
        /// because flushing would publish a pending word from the old generation.</summary>
        public void Flush(double frameEnd)
        {
            CheckAccess();
            if (!Finite(frameEnd) || Finite(previousEnd) && frameEnd < previousEnd) throw new ArgumentOutOfRangeException(nameof(frameEnd));
            processing = true;
            try { Emit(frameEnd, true); }
            finally { processing = false; }
        }

        void Emit(double frameEnd, bool force)
        {
            if (pendingPhrase == null) return;
            double voicedEnd = Finite(lastVoicedEnd) ? lastVoicedEnd : pendingDetection;
            if (!force && frameEnd - voicedEnd < EndSilenceSeconds && frameEnd - pendingDetection < MaximumPendingSeconds) return;
            string phrase = pendingPhrase;
            double detected = pendingDetection;
            pendingPhrase = null;
            if (lastEmitted.TryGetValue(phrase, out double previous) && frameEnd - previous < DebounceSeconds) return;
            lastEmitted[phrase] = frameEnd;
            Hit?.Invoke(new NativeKeywordHit(phrase, Math.Min(frameEnd, voicedEnd), detected));
        }

        /// <summary>Forget pending words, debounce and all acoustic history. Does not emit a hit.</summary>
        public void Reset()
        {
            CheckAccess();
            pendingPhrase = null;
            pendingDetection = 0;
            lastVoicedEnd = previousEnd = double.NaN;
            previousRate = 0;
            lastEmitted.Clear();
            engine.RestartStream();
        }

        public void Dispose()
        {
            if (disposed) return;
            CheckAccess();
            disposed = true;
            pendingPhrase = null;
            lastEmitted.Clear();
            Hit = null;
            engine.Dispose();
        }

        void CheckAccess()
        {
            if (Thread.CurrentThread.ManagedThreadId != ownerThread) throw new InvalidOperationException("Keyword decoder ownership belongs to its creating worker thread.");
            if (disposed) throw new ObjectDisposedException(nameof(NativeKeywordDecoder));
            if (processing) throw new InvalidOperationException("Do not re-enter the decoder from a keyword callback; enqueue the result for the main thread.");
        }
        static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "";
            var text = new StringBuilder(value.Length);
            foreach (char c in value) if (!char.IsWhiteSpace(c) && !char.IsPunctuation(c)) text.Append(c);
            return text.ToString();
        }
    }
}
