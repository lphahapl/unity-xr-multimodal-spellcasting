using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

namespace NarutoVR.SealCombat
{
    /// <summary>Owns microphone capture on Unity's main thread and one offline KWS worker.
    /// Audio remains in bounded memory buffers; only the generated keyword list is written.</summary>
    [DisallowMultipleComponent]
    public sealed class NativeSkillVoiceSource : MonoBehaviour
    {
        const double MaximumCaptureGap = .25;
        const int QueueCapacity = 50, MaximumBlocksPerUpdate = 13, MaximumHitsPerUpdate = 16;
        static NativeSkillVoiceSource microphoneOwner;
        SkillVoiceReceiver receiver;
        SkillCatalog catalog;
        Coroutine startup;
        Worker worker;
        AudioClip microphoneClip;
        float[] interleaved;
        int attempt, readPosition, observedPosition, blockFrames, sampleRate, channels;
        long generation, frames, voicedFrames;
        double nextFrameTime, lastPoll, lastPositionChange;
        bool starting, paused, permissionAsked, requestingPermission, permissionBlocked, needsRetry, sessionOpen, capturing;
        string deviceLabel;
        static double Now => Time.realtimeSinceStartupAsDouble;
        public bool NeedsRetry => needsRetry;

        public void Bind(SkillVoiceReceiver owner)
        {
            if (!owner) throw new ArgumentNullException(nameof(owner));
            if (receiver && receiver != owner) StopRun("原生语音接收目标已更换", false);
            receiver = owner;
            BeginIfPossible();
        }

        public void Configure(SkillCatalog value)
        {
            if (catalog == value && (starting || capturing) && !needsRetry) return;
            bool changed = catalog != value;
            StopRun(value ? "正在更新原生语音词表" : "原生语音等待技能目录", false);
            catalog = value;
            // Rebinding the same catalog during enable/resume must not silently clear a
            // capture/native failure. A genuinely new configuration starts a new attempt.
            if (changed) needsRetry = false;
            BeginIfPossible();
        }

        /// <summary>Explicit user retry after denial, device failure or a capture discontinuity.</summary>
        public void Retry()
        {
            StopRun("正在重新启动原生语音", false);
            permissionAsked = false;
            permissionBlocked = false;
            needsRetry = false;
            BeginIfPossible();
        }

        void OnEnable() => BeginIfPossible();
        void OnDisable() => StopRun("原生语音已停止", false);
        void OnDestroy() => StopRun("原生语音已释放", false);

        void BeginIfPossible()
        {
            if (!Application.isPlaying || !isActiveAndEnabled || !receiver || !receiver.isActiveAndEnabled ||
                !catalog || paused || starting || capturing || needsRetry) return;
            starting = true;
            int current = ++attempt;
            generation = receiver.NativeBeginSession();
            sessionOpen = true;
            startup = StartCoroutine(GuardedStart(current));
        }

        // Flatten nested iterators so a model-copy/permission/startup exception becomes a
        // visible state instead of silently abandoning the coroutine with starting=true.
        IEnumerator GuardedStart(int current)
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(StartPipeline(current));
            try
            {
                while (stack.Count > 0 && current == attempt)
                {
                    object yielded = null;
                    bool moved = false;
                    Exception failure = null;
                    try { moved = stack.Peek().MoveNext(); if (moved) yielded = stack.Peek().Current; }
                    catch (Exception error) { failure = error; }
                    if (failure != null) { Fail(failure.Message, false); yield break; }
                    if (!moved) { (stack.Pop() as IDisposable)?.Dispose(); continue; }
                    if (yielded is IEnumerator child) stack.Push(child);
                    else yield return yielded;
                }
            }
            finally
            {
                while (stack.Count > 0) (stack.Pop() as IDisposable)?.Dispose();
                if (current == attempt) { startup = null; starting = false; }
            }
        }

        IEnumerator StartPipeline(int current)
        {
            // Stop does not dispose a native object from another thread. Wait for its owner
            // to finish, without blocking a frame or initializing a second model in parallel.
            double waitUntil = Now + 10;
            while (worker != null && worker.IsAlive)
            {
                SetStatus("等待上一次识别线程释放", false);
                if (Now > waitUntil) throw new InvalidOperationException("旧识别线程尚未退出，请稍后重试");
                yield return null;
            }
            worker = null;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
            {
                if (permissionAsked) { permissionBlocked = true; throw new InvalidOperationException("未获麦克风权限；请在系统设置允许录音后返回，或主动重试"); }
                permissionAsked = requestingPermission = true;
                bool answered = false;
                var callbacks = new PermissionCallbacks();
                Action<string> answer = _ => { if (current == attempt) answered = true; };
                callbacks.PermissionGranted += answer;
                callbacks.PermissionDenied += answer;
                callbacks.PermissionDeniedAndDontAskAgain += answer;
                SetStatus("语音口令需要头显麦克风；音频仅在设备内处理，请选择是否允许", false);
                Permission.RequestUserPermission(Permission.Microphone, callbacks);
                try
                {
                    // Android suspends the app while the system permission dialog is shown.
                    double activeWait = 0, lastWait = Now;
                    while (!answered && !Permission.HasUserAuthorizedPermission(Permission.Microphone))
                    {
                        double now = Now;
                        if (!paused) activeWait += Math.Max(0, Math.Min(.25, now - lastWait));
                        lastWait = now;
                        if (activeWait > 15) { permissionBlocked = true; throw new InvalidOperationException("未收到麦克风权限结果，请检查系统权限后重试"); }
                        yield return null;
                    }
                    while (paused) yield return null;
                }
                finally
                {
                    callbacks.PermissionGranted -= answer;
                    callbacks.PermissionDenied -= answer;
                    callbacks.PermissionDeniedAndDontAskAgain -= answer;
                    requestingPermission = false;
                }
                if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
                { permissionBlocked = true; throw new InvalidOperationException("麦克风权限未允许；语音未启动，可在系统权限设置允许后返回"); }
            }
            permissionBlocked = false;
#endif
            SetStatus("正在校验随包离线语音模型", false);
            string directory = null, modelError = null;
            yield return NativeVoiceModelFiles.Prepare(path => directory = path, error => modelError = error);
            if (modelError != null || string.IsNullOrEmpty(directory)) throw new InvalidOperationException(modelError ?? "模型目录未就绪");
            string keywords = NativeVoiceConfiguration.BuildKeywordText(catalog, Path.Combine(directory, "tokens.txt"));
            string keywordFile = Path.Combine(directory, "keywords-" + Guid.NewGuid().ToString("N") + ".txt");
            File.WriteAllText(keywordFile, keywords, new UTF8Encoding(false));
            SetStatus("正在启动设备内中文口令识别", false);
            worker = new Worker(directory, keywordFile, generation);
            worker.Start();
            waitUntil = Now + 30;
            while (!worker.Ready)
            {
                if (worker.Error != null || worker.Finished) throw new InvalidOperationException(worker.Error ?? "识别线程启动失败");
                if (Now > waitUntil) throw new InvalidOperationException("离线识别初始化超时，请重试");
                yield return null;
            }
            if (microphoneOwner && microphoneOwner != this) throw new InvalidOperationException("另一个原生语音组件正在使用麦克风");
            microphoneOwner = this;
            string[] devices = Microphone.devices;
            deviceLabel = devices.Length > 0 ? devices[0] : "系统默认头显麦克风";
            microphoneClip = Microphone.Start(null, true, 2, 16000);
            if (!microphoneClip) throw new InvalidOperationException("无法启动头显麦克风，请核查权限和音频设备");
            sampleRate = microphoneClip.frequency;
            channels = microphoneClip.channels;
            if (sampleRate < 8000 || sampleRate > 96000 || channels < 1 || channels > 8)
                throw new InvalidOperationException("麦克风返回了不支持的采样格式");
            blockFrames = Math.Max(1, sampleRate / 50);
            interleaved = new float[blockFrames * channels];
            worker.AllocateFrames(blockFrames);
            waitUntil = Now + 5;
            int position;
            while ((position = Microphone.GetPosition(null)) <= 0)
            {
                if (Now > waitUntil) throw new InvalidOperationException("麦克风已请求但持续没有采样，请检查设备");
                yield return null;
            }
            // Discard initialization audio. Thereafter use one continuous sample clock,
            // anchored to this ring position; never retimestamp late blocks at dequeue time.
            readPosition = observedPosition = position;
            nextFrameTime = lastPoll = lastPositionChange = Now;
            frames = voicedFrames = 0;
            capturing = true;
            SetStatus("头显麦克风已开启，等待采样", false);
        }

        void Update()
        {
            if (!capturing) return;
            try
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
                { permissionBlocked = true; throw new InvalidOperationException("麦克风权限已被撤回"); }
#endif
                if (worker == null || worker.Error != null || worker.Finished) throw new InvalidOperationException(worker?.Error ?? "识别线程意外停止");
                double now = Now;
                if (now - lastPoll > MaximumCaptureGap) throw new InvalidOperationException("音频读取停顿超过250毫秒；旧口令已丢弃，请重试");
                lastPoll = now;
                if (!Microphone.IsRecording(null)) throw new InvalidOperationException("麦克风录音已中断，请重试");
                int position = Microphone.GetPosition(null);
                if (position < 0 || position >= microphoneClip.samples) throw new InvalidOperationException("麦克风采样位置无效");
                if (position != observedPosition) { observedPosition = position; lastPositionChange = now; }
                if (now - lastPositionChange > MaximumCaptureGap) throw new InvalidOperationException("麦克风超过250毫秒没有新采样，请重试");
                int available = (position - readPosition + microphoneClip.samples) % microphoneClip.samples;
                if ((double)available / sampleRate > MaximumCaptureGap || Math.Abs(nextFrameTime + (double)available / sampleRate - now) > MaximumCaptureGap)
                    throw new InvalidOperationException("采集时间轴不连续；旧口令已丢弃，请重试");
                int processed = 0;
                while (available >= blockFrames && processed++ < MaximumBlocksPerUpdate)
                {
                    if (!worker.TryRent(out float[] mono)) throw new InvalidOperationException("语音处理积压超过缓冲上限，请重试");
                    if (!microphoneClip.GetData(interleaved, readPosition))
                    { worker.Return(mono); throw new InvalidOperationException("读取麦克风音频失败"); }
                    double squares = 0;
                    float peak = 0;
                    for (int i = 0; i < blockFrames; ++i)
                    {
                        float value = 0;
                        for (int c = 0; c < channels; ++c) value += interleaved[i * channels + c];
                        value /= channels;
                        if (float.IsNaN(value) || float.IsInfinity(value)) { worker.Return(mono); throw new InvalidOperationException("麦克风返回了无效采样"); }
                        mono[i] = value;
                        squares += (double)value * value;
                        peak = Math.Max(peak, Math.Abs(value));
                    }
                    float rms = (float)Math.Sqrt(squares / blockFrames);
                    double end = nextFrameTime + (double)blockFrames / sampleRate;
                    if (!worker.Enqueue(mono, sampleRate, nextFrameTime))
                    { worker.Return(mono); throw new InvalidOperationException("语音解码队列已满；旧上下文已失效，请重试"); }
                    ++frames;
                    if (rms >= NativeKeywordDecoder.EnergyThreshold) ++voicedFrames;
                    SetStatus("中文口令识别中（头显内离线）", true);
                    receiver.NativeAudio(end, rms, peak, frames, voicedFrames, deviceLabel, sampleRate, generation);
                    nextFrameTime = end;
                    readPosition = (readPosition + blockFrames) % microphoneClip.samples;
                    available -= blockFrames;
                }
                for (int i = 0; i < MaximumHitsPerUpdate && worker.TryGetHit(out NativeKeywordHit hit); ++i)
                    receiver.NativePhrase(hit, worker.Generation);
            }
            catch (Exception error) { Fail(error.Message); }
        }

        void SetStatus(string message, bool running)
        { if (receiver && sessionOpen) receiver.NativeStatus(message, running, generation); }

        void Fail(string message, bool cancelStartup = true) => StopRun("原生语音：" + message, true, cancelStartup);

        void StopRun(string message, bool fault, bool cancelStartup = true)
        {
            ++attempt;
            capturing = starting = false;
            if (cancelStartup && startup != null) StopCoroutine(startup);
            startup = null;
            requestingPermission = false;
            worker?.Cancel();
            if (microphoneOwner == this)
            {
                try { Microphone.End(null); }
                catch (Exception) { /* Release the logical owner even if the device vanished. */ }
                microphoneOwner = null;
            }
            if (microphoneClip) Destroy(microphoneClip);
            microphoneClip = null;
            interleaved = null;
            // Normal pause/disable is not an acknowledgment of an earlier fault. Only
            // Retry, a new catalog, or observing a restored permission may clear it.
            if (fault) needsRetry = true;
            if (receiver && sessionOpen) receiver.NativeEndSession(message);
            sessionOpen = false;
        }

        void OnApplicationPause(bool value)
        {
            paused = value;
            // Keep the initial system permission callback alive across its own suspension.
            if (value) { if (!requestingPermission) StopRun("应用已暂停，麦克风已释放", false); }
            else ResumeAfterPermission();
        }

        void OnApplicationFocus(bool value)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!value && !requestingPermission && (starting || capturing)) StopRun("应用失去焦点，麦克风已释放", false);
#endif
            if (value) ResumeAfterPermission();
        }

        void ResumeAfterPermission()
        {
            if (paused || requestingPermission) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            bool authorized = Permission.HasUserAuthorizedPermission(Permission.Microphone);
            if (permissionAsked && !authorized) return;
            if (permissionBlocked && authorized)
            { permissionBlocked = false; needsRetry = false; }
#endif
            // A lifecycle return is a fresh stream. Runtime faults otherwise wait for Retry.
            BeginIfPossible();
        }

        sealed class Worker
        {
            readonly string directory, keywordFile;
            readonly Thread thread;
            readonly AutoResetEvent signal = new AutoResetEvent(false);
            readonly ConcurrentQueue<Frame> input = new ConcurrentQueue<Frame>();
            readonly ConcurrentQueue<float[]> free = new ConcurrentQueue<float[]>();
            readonly ConcurrentQueue<NativeKeywordHit> output = new ConcurrentQueue<NativeKeywordHit>();
            int queuedFrames, queuedHits;
            volatile bool cancelled;
            public volatile bool Ready, Finished;
            public volatile string Error;
            public long Generation { get; }
            public bool IsAlive => thread.IsAlive;
            readonly struct Frame
            {
                public readonly float[] Samples;
                public readonly int Rate;
                public readonly double Start;
                public Frame(float[] samples, int rate, double start) { Samples = samples; Rate = rate; Start = start; }
            }
            public Worker(string modelDirectory, string keywords, long generation)
            {
                directory = modelDirectory; keywordFile = keywords; Generation = generation;
                thread = new Thread(Run) { IsBackground = true, Name = "Quest offline KWS" };
            }
            public void Start() => thread.Start();
            public void AllocateFrames(int count) { for (int i = 0; i < QueueCapacity + 2; ++i) free.Enqueue(new float[count]); }
            public bool TryRent(out float[] data) => free.TryDequeue(out data);
            public void Return(float[] data) => free.Enqueue(data);
            public bool Enqueue(float[] data, int rate, double start)
            {
                if (cancelled || Finished) return false;
                if (Interlocked.Increment(ref queuedFrames) > QueueCapacity)
                { Interlocked.Decrement(ref queuedFrames); return false; }
                input.Enqueue(new Frame(data, rate, start));
                // Ownership transferred to the queue. If shutdown won this race, the old
                // worker/queue releases the buffer; never return it to the pool twice.
                try { signal.Set(); } catch (ObjectDisposedException) { }
                return true;
            }
            public bool TryGetHit(out NativeKeywordHit hit)
            {
                if (!output.TryDequeue(out hit)) return false;
                Interlocked.Decrement(ref queuedHits);
                return true;
            }
            public void Cancel()
            {
                cancelled = true;
                try { signal.Set(); } catch (ObjectDisposedException) { }
            }
            void Run()
            {
                NativeKeywordDecoder decoder = null;
                try
                {
                    if (cancelled) return;
                    decoder = new NativeKeywordDecoder(new SherpaKeywordEngine(directory, keywordFile));
                    decoder.Hit += hit =>
                    {
                        if (cancelled) return;
                        if (Interlocked.Increment(ref queuedHits) > 32) throw new InvalidOperationException("识别结果队列已满");
                        output.Enqueue(hit);
                    };
                    Ready = true;
                    while (!cancelled)
                    {
                        if (!input.TryDequeue(out Frame frame)) { signal.WaitOne(20); continue; }
                        Interlocked.Decrement(ref queuedFrames);
                        try { if (!cancelled) decoder.Accept(frame.Samples, frame.Samples.Length, frame.Rate, frame.Start); }
                        finally { Return(frame.Samples); }
                    }
                }
                catch (Exception error) { if (!cancelled) Error = error.GetType().Name + ": " + error.Message; }
                finally
                {
                    Ready = false;
                    try { decoder?.Dispose(); } catch (Exception error) { if (Error == null) Error = "识别资源释放失败：" + error.Message; }
                    while (input.TryDequeue(out Frame frame)) Return(frame.Samples);
                    while (output.TryDequeue(out _)) { }
                    try { File.Delete(keywordFile); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                    signal.Dispose();
                    Finished = true;
                }
            }
        }
    }
}
