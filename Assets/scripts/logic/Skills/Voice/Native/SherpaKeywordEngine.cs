using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace NarutoVR.SealCombat
{
    /// <summary>A single-worker keyword stream. Reset preserves buffered acoustic context;
    /// RestartStream discards it at a capture/lifecycle boundary.</summary>
    public interface IKeywordEngine : IDisposable
    {
        string NativeVersion { get; }
        void Accept(float[] samples, int sampleRate, int count);
        bool IsReady { get; }
        string Decode();
        void Reset();
        void RestartStream();
    }

    /// <summary>Explicit UTF-8 bindings for the sherpa-onnx v2.0.1 C API.
    /// Construct, use, and dispose on one worker thread. No finalizer calls into the library.</summary>
    public sealed class SherpaKeywordEngine : IKeywordEngine
    {
        const string LibraryName = "sherpa-onnx-c-api";
        const string Stem = "epoch-13-avg-2-chunk-8-left-64";
        readonly int ownerThread = Thread.CurrentThread.ManagedThreadId;
        readonly List<IntPtr> strings = new List<IntPtr>();
        IntPtr spotter, stream;
        bool disposed;

        public string NativeVersion { get; private set; }

        public SherpaKeywordEngine(string modelDirectory, string keywordsFile)
        {
            if (string.IsNullOrWhiteSpace(modelDirectory)) throw new ArgumentException("A model directory is required.", nameof(modelDirectory));
            if (string.IsNullOrWhiteSpace(keywordsFile)) throw new ArgumentException("A keyword file is required.", nameof(keywordsFile));
            try
            {
                NativeVersion = ReadUtf8(Native.GetVersion());
                string version = NativeVersion.TrimStart('v').Split('-', '+', ' ')[0];
                if (version != "2.0.1") throw new NotSupportedException("These C API layouts require sherpa-onnx 2.0.1; loaded " + NativeVersion);
                var config = new KeywordSpotterConfig
                {
                    feature = new FeatureConfig { sampleRate = 16000, featureDimension = 80 },
                    model = new OnlineModelConfig
                    {
                        transducer = new TransducerConfig
                        {
                            encoder = FileString(Path.Combine(modelDirectory, "encoder-" + Stem + ".int8.onnx")),
                            decoder = FileString(Path.Combine(modelDirectory, "decoder-" + Stem + ".onnx")),
                            joiner = FileString(Path.Combine(modelDirectory, "joiner-" + Stem + ".int8.onnx"))
                        },
                        tokens = FileString(Path.Combine(modelDirectory, "tokens.txt")),
                        numThreads = 2,
                        provider = AllocateUtf8("cpu")
                    },
                    maxActivePaths = 4,
                    numTrailingBlanks = 1,
                    keywordsScore = 1f,
                    keywordsThreshold = .25f,
                    keywordsFile = FileString(keywordsFile)
                };
                spotter = Native.CreateSpotter(ref config);
                if (spotter == IntPtr.Zero) throw new InvalidOperationException("sherpa-onnx could not create the keyword spotter; verify model and vocabulary.");
                stream = Native.CreateStream(spotter);
                if (stream == IntPtr.Zero) throw new InvalidOperationException("sherpa-onnx could not create a keyword stream.");
            }
            catch
            {
                Release();
                disposed = true;
                throw;
            }
        }

        public bool IsReady
        {
            get { CheckAccess(); return Native.IsReady(spotter, stream) != 0; }
        }

        public void Accept(float[] samples, int sampleRate, int count)
        {
            CheckAccess();
            if (samples == null) throw new ArgumentNullException(nameof(samples));
            if (count < 0 || count > samples.Length) throw new ArgumentOutOfRangeException(nameof(count));
            if (sampleRate <= 0) throw new ArgumentOutOfRangeException(nameof(sampleRate));
            for (int i = 0; i < count; i++)
                if (float.IsNaN(samples[i]) || float.IsInfinity(samples[i])) throw new ArgumentException("PCM contains a non-finite sample.", nameof(samples));
            if (count != 0) Native.AcceptWaveform(stream, sampleRate, samples, count);
        }

        /// <summary>Decode one ready step; take exactly one result snapshot and always release it.</summary>
        public string Decode()
        {
            CheckAccess();
            if (Native.IsReady(spotter, stream) == 0) throw new InvalidOperationException("The keyword stream is not ready to decode.");
            Native.Decode(spotter, stream);
            IntPtr result = Native.GetResult(spotter, stream);
            if (result == IntPtr.Zero) throw new InvalidOperationException("sherpa-onnx returned no keyword result snapshot.");
            try
            {
                var value = Marshal.PtrToStructure<KeywordResult>(result);
                return ReadUtf8(value.keyword);
            }
            finally { Native.DestroyResult(result); }
        }

        /// <summary>After a detected keyword, reset search state without destroying its audio stream.</summary>
        public void Reset() { CheckAccess(); Native.ResetStream(spotter, stream); }

        /// <summary>Discard audio history after pause, microphone discontinuity, or generation change.</summary>
        public void RestartStream()
        {
            CheckAccess();
            IntPtr replacement = Native.CreateStream(spotter);
            if (replacement == IntPtr.Zero) throw new InvalidOperationException("sherpa-onnx could not restart the keyword stream.");
            IntPtr previous = stream;
            stream = replacement;
            Native.DestroyStream(previous);
        }

        public void Dispose()
        {
            if (disposed) return;
            CheckAccess();
            disposed = true;
            Release();
        }

        void CheckAccess()
        {
            if (Thread.CurrentThread.ManagedThreadId != ownerThread) throw new InvalidOperationException("Keyword engine ownership belongs to its creating worker thread.");
            if (disposed) throw new ObjectDisposedException(nameof(SherpaKeywordEngine));
        }

        void Release()
        {
            // Clear handles before releasing; nested finally blocks also free constructor allocations.
            IntPtr oldStream = stream, oldSpotter = spotter;
            stream = spotter = IntPtr.Zero;
            try { if (oldStream != IntPtr.Zero) Native.DestroyStream(oldStream); }
            finally
            {
                try { if (oldSpotter != IntPtr.Zero) Native.DestroySpotter(oldSpotter); }
                finally
                {
                    foreach (IntPtr item in strings) Marshal.FreeHGlobal(item);
                    strings.Clear();
                }
            }
        }

        IntPtr FileString(string path)
        {
            if (path.IndexOf('\0') >= 0) throw new ArgumentException("A native file path cannot contain NUL.", nameof(path));
            path = Path.GetFullPath(path);
            if (!File.Exists(path)) throw new FileNotFoundException("Required keyword model or vocabulary file is missing.", path);
            return AllocateUtf8(path);
        }

        IntPtr AllocateUtf8(string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text + "\0");
            IntPtr memory = Marshal.AllocHGlobal(bytes.Length);
            try { Marshal.Copy(bytes, 0, memory, bytes.Length); strings.Add(memory); return memory; }
            catch { Marshal.FreeHGlobal(memory); throw; }
        }

        static string ReadUtf8(IntPtr pointer)
        {
            if (pointer == IntPtr.Zero) return "";
            int length = 0;
            // Version/keyword strings are small; reject corrupt or unterminated output.
            while (length < 65536 && Marshal.ReadByte(pointer, length) != 0) length++;
            if (length == 65536) throw new InvalidOperationException("Native text exceeds the supported UTF-8 string length.");
            byte[] bytes = new byte[length];
            Marshal.Copy(pointer, bytes, 0, length);
            return new UTF8Encoding(false, true).GetString(bytes);
        }

        // Field order and types mirror v2.0.1 c-api.h; optional families still occupy ABI space.
        // Default native packing gives sizes 136/184/56 for Model/Spotter/Result on 64-bit ABIs.
        [StructLayout(LayoutKind.Sequential)] struct FeatureConfig { public int sampleRate, featureDimension; }
        [StructLayout(LayoutKind.Sequential)] struct TransducerConfig { public IntPtr encoder, decoder, joiner; }
        [StructLayout(LayoutKind.Sequential)] struct ParaformerConfig { public IntPtr encoder, decoder; }
        [StructLayout(LayoutKind.Sequential)] struct OneModelConfig { public IntPtr model; }
        [StructLayout(LayoutKind.Sequential)] struct OnlineModelConfig
        {
            public TransducerConfig transducer;
            public ParaformerConfig paraformer;
            public OneModelConfig zipformer2Ctc;
            public IntPtr tokens;
            public int numThreads;
            public IntPtr provider;
            public int debug;
            public IntPtr modelType, modelingUnit, bpeVocabulary, tokensBuffer;
            public int tokensBufferSize;
            public OneModelConfig nemoCtc, toneCtc;
        }
        [StructLayout(LayoutKind.Sequential)] struct KeywordSpotterConfig
        {
            public FeatureConfig feature;
            public OnlineModelConfig model;
            public int maxActivePaths, numTrailingBlanks;
            public float keywordsScore, keywordsThreshold;
            public IntPtr keywordsFile, keywordsBuffer;
            public int keywordsBufferSize;
        }
        [StructLayout(LayoutKind.Sequential)] struct KeywordResult
        {
            public IntPtr keyword, tokens, tokensArray;
            public int count;
            public IntPtr timestamps;
            public float startTime;
            public IntPtr json;
        }

        static class Native
        {
            [DllImport(LibraryName, EntryPoint = "SherpaOnnxGetVersionStr", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
            internal static extern IntPtr GetVersion();
            [DllImport(LibraryName, EntryPoint = "SherpaOnnxCreateKeywordSpotter", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
            internal static extern IntPtr CreateSpotter(ref KeywordSpotterConfig config);
            [DllImport(LibraryName, EntryPoint = "SherpaOnnxDestroyKeywordSpotter", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
            internal static extern void DestroySpotter(IntPtr spotter);
            [DllImport(LibraryName, EntryPoint = "SherpaOnnxCreateKeywordStream", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
            internal static extern IntPtr CreateStream(IntPtr spotter);
            [DllImport(LibraryName, EntryPoint = "SherpaOnnxDestroyOnlineStream", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
            internal static extern void DestroyStream(IntPtr stream);
            [DllImport(LibraryName, EntryPoint = "SherpaOnnxOnlineStreamAcceptWaveform", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
            internal static extern void AcceptWaveform(IntPtr stream, int sampleRate, [In] float[] samples, int count);
            [DllImport(LibraryName, EntryPoint = "SherpaOnnxIsKeywordStreamReady", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
            internal static extern int IsReady(IntPtr spotter, IntPtr stream);
            [DllImport(LibraryName, EntryPoint = "SherpaOnnxDecodeKeywordStream", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
            internal static extern void Decode(IntPtr spotter, IntPtr stream);
            [DllImport(LibraryName, EntryPoint = "SherpaOnnxResetKeywordStream", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
            internal static extern void ResetStream(IntPtr spotter, IntPtr stream);
            [DllImport(LibraryName, EntryPoint = "SherpaOnnxGetKeywordResult", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
            internal static extern IntPtr GetResult(IntPtr spotter, IntPtr stream);
            [DllImport(LibraryName, EntryPoint = "SherpaOnnxDestroyKeywordResult", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
            internal static extern void DestroyResult(IntPtr result);
        }
    }
}
