using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

namespace NarutoVR.SealCombat
{
    /// <summary>统一口令门面：编辑器接收电脑服务，Quest包在设备内离线识别，主线程发布。</summary>
    [DisallowMultipleComponent]
    public sealed class SkillVoiceReceiver : MonoBehaviour
    {
        [Header("本机中文语音服务")]
        [InspectorName("编辑器播放时自动启动与重连")] public bool autoStartInEditor = true;
#if UNITY_EDITOR
        [InspectorName("编辑器显式测试原生麦克风")] public bool useNativeInEditor;
#endif
        public string LauncherStatus { get; set; } = "未请求自动启动";
        public bool UsesNativeBackend
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                return true;
#elif UNITY_EDITOR
                return useNativeInEditor;
#else
                return false;
#endif
            }
        }
        public bool HasServiceHeartbeat => UsesNativeBackend ? nativeReady && Now - nativeAudioAt < 1 : ready && Now-lastHeartbeat < heartbeatTimeout;
        public bool NativeNeedsRetry => UsesNativeBackend && nativeSource && nativeSource.NeedsRetry;
        [InspectorName("Unity 接收端口"), Range(1024, 65535)] public int receivePort = 47831;
        [InspectorName("Python 服务端口"), Range(1024, 65535)] public int servicePort = 47832;
        [InspectorName("心跳超时（秒）"), Min(1)] public float heartbeatTimeout = 3f;
        [InspectorName("最大消息年龄（秒）"), Min(.1f)] public float maximumEventAge = 3f;
        [InspectorName("允许 WAV 回放测试")] public bool allowWavReplay = true;
        [InspectorName("允许显式模拟接线测试")] public bool allowProtocolTest = true;
        [InspectorName("记录识别日志")] public bool logPhrases = true;

        /// <summary>事件第二个参数是映射到 Unity 实时时钟的口令结束时间。</summary>
        public event Action<string, double> PhraseReceived;
        public event Action<string,string> Diagnostic;
        void TracePacket(string reason,string phrase) {
            if(Diagnostic==null)return;
            foreach(Action<string,string> listener in Diagnostic.GetInvocationList())
                try { listener(reason,phrase); } catch { }
        }
        public string Status { get; private set; } = "语音未连接：请在电脑启动语音服务";
        public bool IsRunning { get; private set; }
        public double LastLatencyMilliseconds { get; private set; }
        public double ClockUncertaintyMilliseconds { get; private set; }
        public string LastCaptureBasis { get; private set; } = "尚无输入";
        public string LastPhrase { get; private set; } = "";
        private Packet audioTelemetry;
        private double audioTelemetryReceived;
        private NativeSkillVoiceSource nativeSource;
        private SkillCatalog nativeCatalog;
        private long nativeGeneration, nativeFrames, nativeVoicedFrames;
        private bool nativeReady;
        private double nativeAudioAt = double.NegativeInfinity, nativeVoiceAt = double.NegativeInfinity;
        private double nativeStartedAt, nativeLastEndedAt = double.NegativeInfinity;
        private readonly HashSet<string> nativePhrasesAtEnd = new(StringComparer.Ordinal);
        private float nativeRms, nativePeak;
        private string nativeDevice = "尚未启动";
        private int nativeSampleRate;

        /// <summary>根据采集端上报的音量及时间，判断所选麦克风最近是否出现过阈值以上的声音。</summary>
        public bool HasRecentMicrophoneSpeech(float holdSeconds = .24f)
        {
            if (UsesNativeBackend)
                return HasServiceHeartbeat && nativeVoicedFrames > 0 && Now >= nativeVoiceAt && Now - nativeVoiceAt <= Math.Max(.01, holdSeconds);
            var p = audioTelemetry;
            double now = Now;
            if (!HasServiceHeartbeat || mode != "microphone" || now - lastSync >= 10 || p == null ||
                p.audio_snapshot_at <= 0 || !Finite(p.audio_snapshot_at) || p.audio_frames < 1 ||
                p.audio_voiced_frames < 1 || p.audio_voiced_frames > p.audio_frames ||
                p.audio_age < 0 || !Finite(p.audio_voice_age) || p.audio_voice_age < 0) return false;
            // 用源时钟映射识别积压心跳，避免编辑器卡顿后把旧数据当作实时输入。
            double transit = now - (p.audio_snapshot_at + offset);
            if (transit < -.05 || transit > 1) return false;
            transit = Math.Max(0, transit);
            return transit + p.audio_age < 1 && transit + p.audio_voice_age <= Math.Max(.01, holdSeconds);
        }

        /// <summary>展示设备、音量和心跳诊断；选中设备本身不能证明已经采到声音。</summary>
        public string BuildDebugDetails()
        {
            if (UsesNativeBackend)
                return $"启动管理：{LauncherStatus}\n语音：{Status}\n运行模式：设备内离线KWS\n" +
                    $"选中输入：{nativeDevice} | {nativeSampleRate} Hz\n音频流：{(HasServiceHeartbeat ? "采样持续更新" : "尚无样本或输入已过期")}\n" +
                    $"输入能量：RMS {nativeRms:F4} / Peak {nativePeak:F4} | 门槛0.012\n有声帧/总帧：{nativeVoicedFrames}/{nativeFrames}\n" +
                    $"游戏已接收：{(string.IsNullOrEmpty(LastPhrase) ? "无" : LastPhrase)}\n词尾估计至接收：{LastLatencyMilliseconds:F1}ms\n" +
                    "短语匹配度：KWS API不提供概率；能量不是匹配分数；采集时间为Unity环形缓冲估计";
            var p = audioTelemetry;
            string prefix = $"启动管理：{LauncherStatus}\n语音：{Status}\n运行模式：{mode ?? "尚未连接"}\n";
            if (p == null) return prefix + "输入遥测：尚未收到（旧服务需重启）\n短语匹配度：当前 KWS API 不提供概率";
            double elapsed = Math.Max(0, Now - audioTelemetryReceived);
            double age = p.audio_age < 0 ? -1 : p.audio_age + elapsed;
            bool fresh = ready && mode == "microphone" && Now-lastHeartbeat < heartbeatTimeout && age >= 0 && age < 1;
            string stream = fresh ? "采样持续更新" : "尚无样本或输入已过期";
            string energy = !fresh ? "未知" : p.audio_rms >= p.audio_energy_threshold ? "有声能量（不代表口令匹配）" : "低能量/静音";
            return prefix + $"选中输入：{p.audio_device} [{p.audio_backend}]\n" +
                   $"设备索引：{p.audio_device_index} | 采样率：{p.audio_sample_rate} Hz\n" +
                   $"音频流：{stream} | 最近样本：{(age < 0 ? "无" : age.ToString("F2") + " 秒前")}\n" +
                   $"输入能量：{energy} | RMS {p.audio_rms:F4} / Peak {p.audio_peak:F4}\n" +
                   $"能量门槛：{p.audio_energy_threshold:F4} | 有声帧/总帧：{p.audio_voiced_frames}/{p.audio_frames}\n" +
                   $"服务最近命中：{(string.IsNullOrEmpty(p.audio_last_phrase) ? "未命中" : p.audio_last_phrase)} | 共 {p.audio_phrase_count} 次\n" +
                   $"游戏已接收：{(string.IsNullOrEmpty(LastPhrase) ? "无" : LastPhrase)}\n" +
                   "短语匹配度：当前 KWS API 不提供概率；能量不是匹配分数";
        }

        // 这些字段由 JsonUtility 按网络数据包字段名反射填充，修改名称会影响协议。
#pragma warning disable 0649
        [Serializable]
        private sealed class Packet
        {
            public int v;
            public string kind;
            public string session;
            public bool ready;
            public string status;
            public string mode;
            public int nonce;
            public double client_send;
            public double server_recv;
            public double server_send;
            public int seq;
            public string phrase;
            public double ended_at;
            public double detected_at;
            public double sent_at;
            public string capture_basis;
            public string end_method;
            public int audio_telemetry_version;
            public string audio_device, audio_backend, audio_last_phrase;
            public int audio_device_index, audio_sample_rate, audio_phrase_count;
            public long audio_frames, audio_voiced_frames;
            public double audio_snapshot_at, audio_age, audio_voice_age, audio_rms, audio_peak, audio_energy_threshold;
        }
#pragma warning restore 0649

        private UdpClient socket;
        private double nextBindRetry;
        private IPEndPoint service;
        private string session;
        private bool ready;
        private string mode;
        private string serviceStatus;
        private int sequence;
        private int nonce;
        private double pingSent;
        private double nextPing;
        private double lastHeartbeat = double.NegativeInfinity;
        private double lastSync = double.NegativeInfinity;
        private double offset;
        private double bestRtt = double.PositiveInfinity;
        private readonly HashSet<string> allowedPhrases = new(StringComparer.Ordinal);

        /// <summary>复制当前技能目录的口令白名单；完成配置前拒绝所有口令。</summary>
        public void ConfigurePhrases(IEnumerable<string> phrases)
        {
            allowedPhrases.Clear();
            if(phrases==null)return;
            foreach(var phrase in phrases)
            {
                string normalized=SkillPhrase.Normalize(phrase);
                if(normalized.Length>0)allowedPhrases.Add(normalized);
            }
        }
        public bool AcceptsPhrase(string phrase)=>allowedPhrases.Contains(SkillPhrase.Normalize(phrase));

        /// <summary>与白名单同时提供当前目录的声学拼音；不会使用电脑上的旧JSON配置。</summary>
        public void ConfigureNativeCatalog(SkillCatalog catalog)
        {
            nativeCatalog = catalog;
            if (UsesNativeBackend && Application.isPlaying && isActiveAndEnabled)
            {
                EnsureNativeSource();
                nativeSource.Configure(catalog);
            }
        }
        void EnsureNativeSource()
        {
            if (!nativeSource) nativeSource = GetComponent<NativeSkillVoiceSource>();
            if (!nativeSource) nativeSource = gameObject.AddComponent<NativeSkillVoiceSource>();
            nativeSource.enabled = true;
            nativeSource.Bind(this);
        }
        public void RetryNativeMicrophone()
        {
            if (!UsesNativeBackend || !Application.isPlaying || !isActiveAndEnabled) return;
            EnsureNativeSource(); nativeSource.Retry();
        }
        internal long NativeBeginSession()
        {
            ++nativeGeneration;
            nativeReady = IsRunning = false;
            nativeAudioAt = nativeVoiceAt = nativeLastEndedAt = double.NegativeInfinity;
            nativeFrames = nativeVoicedFrames = 0;
            nativePhrasesAtEnd.Clear();
            nativeRms = nativePeak = 0;
            nativeStartedAt = Now;
            LastPhrase = "";
            ClockUncertaintyMilliseconds = 0;
            LastCaptureBasis = "unity-microphone-ring-position-estimate";
            return nativeGeneration;
        }
        internal void NativeStatus(string status, bool running, long generation)
        {
            if (generation != nativeGeneration || !isActiveAndEnabled) return;
            nativeReady = IsRunning = running;
            Status = status;
            LauncherStatus = "设备内 sherpa-onnx；无需电脑Python服务";
        }
        internal void NativeAudio(double endedAt, float rms, float peak, long frames, long voicedFrames, string device, int rate, long generation)
        {
            if (generation != nativeGeneration || !isActiveAndEnabled || !Finite(endedAt) || endedAt < nativeStartedAt ||
                endedAt > Now + .05 || !Finite(rms) || !Finite(peak) || rms < 0 || rms > 1 || peak < 0 || peak > 1 ||
                frames < nativeFrames || voicedFrames < 0 || voicedFrames > frames || rate <= 0) return;
            nativeAudioAt = endedAt;
            if (rms >= .012f) nativeVoiceAt = endedAt;
            nativeFrames = frames; nativeVoicedFrames = voicedFrames;
            nativeRms = rms; nativePeak = peak; nativeDevice = device; nativeSampleRate = rate;
        }
        internal void NativePhrase(NativeKeywordHit hit, long generation)
        {
            if (!UsesNativeBackend || generation != nativeGeneration || !isActiveAndEnabled || !HasServiceHeartbeat) return;
            string phrase = SkillPhrase.Normalize(hit.Phrase);
            double age = Now - hit.EndedAt;
            if (!allowedPhrases.Contains(phrase)) { TracePacket("拒收：口令不在当前目录", phrase); return; }
            if (!Finite(hit.EndedAt) || !Finite(hit.DetectedAt) || hit.EndedAt < nativeStartedAt ||
                hit.EndedAt < nativeLastEndedAt ||
                (hit.EndedAt == nativeLastEndedAt && nativePhrasesAtEnd.Contains(phrase)) ||
                age < -.05 || age > maximumEventAge)
            { TracePacket("拒收：原生口令过期、重复或属于旧采集", phrase); return; }
            // Energy segmentation can give adjacent, distinct words the same end estimate.
            // Keep that capture time intact; deduplicate the phrase within this time bucket.
            if (hit.EndedAt > nativeLastEndedAt) nativePhrasesAtEnd.Clear();
            nativePhrasesAtEnd.Add(phrase);
            nativeLastEndedAt = hit.EndedAt;
            LastPhrase = phrase;
            LastLatencyMilliseconds = age * 1000;
            if (logPhrases) Debug.Log($"[原生语音] {phrase} | 词尾能量估计至接收 {LastLatencyMilliseconds:F1} ms | {LastCaptureBasis}", this);
            TracePacket($"接收：原生口令年龄{age:F3}s", phrase);
            PhraseReceived?.Invoke(phrase, hit.EndedAt);
        }
        internal void NativeEndSession(string status)
        {
            ++nativeGeneration;
            nativeReady = IsRunning = false;
            nativeAudioAt = nativeVoiceAt = double.NegativeInfinity;
            Status = status;
        }

        private static double Now => Time.realtimeSinceStartupAsDouble;
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        private void OnEnable()
        {
            session = null;
            audioTelemetry = null;
            LastPhrase = "";
            ready = IsRunning = false;
            lastHeartbeat = lastSync = double.NegativeInfinity;
            nextPing = 0;
            bestRtt = double.PositiveInfinity;
            if (UsesNativeBackend)
            {
                if (Application.isPlaying) { EnsureNativeSource(); nativeSource.Configure(nativeCatalog); }
                return;
            }
            OpenSocket();
        }
        void OpenSocket()
        {
            socket?.Close(); socket=null;
            nextBindRetry=Now+2;
            try
            {
                service = new IPEndPoint(IPAddress.Loopback, servicePort);
                socket = new UdpClient(AddressFamily.InterNetwork);
                socket.ExclusiveAddressUse = true;
                socket.Client.Bind(new IPEndPoint(IPAddress.Loopback, receivePort));
                socket.Client.Blocking = false;
                Status = "语音未启动：等待本机 KWS 服务";
            }
            catch (Exception error)
            {
                socket?.Close();
                socket = null;
                Status = "语音接收失败：" + error.Message;
                Debug.LogWarning(Status, this);
            }
        }

        private void Update()
        {
            if (UsesNativeBackend) return;
            if (socket == null) { if(Now>=nextBindRetry) OpenSocket(); if(socket==null)return; }
            double now = Now;
            if (now >= nextPing)
            {
                nextPing = now + 1;
                pingSent = now;
                var ping = new Packet { v = 1, kind = "ping", nonce = ++nonce, client_send = now };
                byte[] data = Encoding.UTF8.GetBytes(JsonUtility.ToJson(ping));
                try { socket.Send(data, data.Length, service); }
                catch (SocketException) { /* Unstarted local service is an expected state. */ }
            }
            // 限制每帧轮询数量，防止本机进程大量发送数据拖慢 VR 帧。
            for (int i = 0; i < 64; i++)
            {
                try
                {
                    if (socket.Available == 0) break;
                    var from = new IPEndPoint(IPAddress.Any, 0);
                    byte[] data = socket.Receive(ref from);
                    if (!IPAddress.IsLoopback(from.Address) || from.Port != servicePort || data.Length > 4096) continue;
                    var packet = JsonUtility.FromJson<Packet>(Encoding.UTF8.GetString(data));
                    if (packet == null || packet.v != 1 || string.IsNullOrEmpty(packet.session)) continue;
                    Process(packet, Now);
                }
                catch (SocketException) { break; }
                catch (ArgumentException) { /* Ignore malformed local packets. */ }
            }
            now = Now;
            IsRunning = ready && now - lastHeartbeat < heartbeatTimeout && now - lastSync < 10;
            if (now - lastHeartbeat >= heartbeatTimeout)
                Status = "语音未连接：请在电脑启动语音服务";
            else if (!ready)
                Status = string.IsNullOrEmpty(serviceStatus) ? "语音尚未就绪" : serviceStatus;
            else if (now - lastSync >= 10)
                Status = "语音服务已连接，等待时钟同步";
            else if (mode == "wav-replay")
                Status = allowWavReplay ? "WAV 回放测试中（非麦克风）" : "WAV 回放被禁用";
            else if (mode == "protocol-test")
                Status = allowProtocolTest ? "模拟接线测试（未运行语音识别）" : "模拟接线测试被禁用";
            else
                Status = serviceStatus ?? "中文口令识别中（本机）";
            if (mode == "wav-replay" && !allowWavReplay) IsRunning = false;
            if (mode == "protocol-test" && !allowProtocolTest) IsRunning = false;
        }

        /// <summary>检查会话握手、源时钟和递增序号，将通过白名单的口令映射为 Unity 输入时间。</summary>
        private void Process(Packet packet, double now)
        {
            if (packet.kind == "pong")
            {
                if (packet.nonce != nonce || Math.Abs(packet.client_send - pingSent) > .000001 ||
                    !Finite(packet.server_recv) || !Finite(packet.server_send)) return;
                double rtt = (now - pingSent) - (packet.server_send - packet.server_recv);
                if (rtt < 0 || rtt > .25 || packet.server_send < packet.server_recv) return;
                if (session != packet.session)
                {
                    session = packet.session;
                    audioTelemetry = null;
                    LastPhrase = "";
                    sequence = 0;
                    bestRtt = double.PositiveInfinity;
                }
                // 长暂停后重新校准时钟，在观测窗口内优先采用抖动最小的样本。
                if (rtt <= bestRtt * 1.5 || now - lastSync > 5)
                {
                    offset = ((pingSent + now) - (packet.server_recv + packet.server_send)) * .5;
                    bestRtt = rtt;
                    lastSync = now;
                    ClockUncertaintyMilliseconds = rtt * 500;
                }
                ReadStatus(packet, now);
                return;
            }
            // 服务端重启后必须重新握手，才能接收新会话的口令事件。
            if (packet.session != session) { if(packet.kind=="phrase")TracePacket("拒收：服务会话未同步",packet.phrase); return; }
            if (packet.kind == "heartbeat") { ReadStatus(packet, now); return; }
            if (packet.kind != "phrase" || !ready || now - lastHeartbeat > heartbeatTimeout ||
                now - lastSync > 10 || packet.seq <= sequence ||
                (packet.mode != "microphone" && packet.mode != "wav-replay" && packet.mode != "protocol-test") ||
                (packet.mode == "protocol-test" && !allowProtocolTest) ||
                (packet.mode == "wav-replay" && !allowWavReplay)) { if(packet.kind=="phrase")TracePacket($"拒收：ready={ready}, heartbeatAge={now-lastHeartbeat:F3}, syncAge={now-lastSync:F3}, seq={packet.seq}/{sequence}, mode={packet.mode}",packet.phrase); return; }
            string phrase=SkillPhrase.Normalize(packet.phrase);
            if (!allowedPhrases.Contains(phrase)) { TracePacket("拒收：口令不在当前目录",phrase); return; }
            if (!Finite(packet.ended_at) || !Finite(packet.sent_at) || packet.ended_at <= 0 ||
                packet.sent_at < packet.ended_at ||
                (packet.end_method != "rms-frame-end-estimate" && !(packet.mode == "protocol-test" && packet.end_method == "synthetic-test"))) { TracePacket("拒收：时间戳或词尾估计格式无效",phrase); return; }
            double endedAt = packet.ended_at + offset;
            double age = now - endedAt;
            if (age < -.05 || age > maximumEventAge) { TracePacket($"拒收：口令年龄{age:F3}s，上限{maximumEventAge:F3}s",phrase); return; }
            sequence = packet.seq;
            LastPhrase = phrase;
            LastLatencyMilliseconds = age * 1000;
            LastCaptureBasis = packet.capture_basis ?? "unknown";
            if (logPhrases)
                Debug.Log($"[中文语音] {packet.phrase} | 词尾能量估计至接收 {LastLatencyMilliseconds:F1} ms | " +
                          $"时钟 RTT/2 {ClockUncertaintyMilliseconds:F1} ms | {LastCaptureBasis} | {packet.mode}", this);
            TracePacket($"接收：口令年龄{age:F3}s",phrase);
            PhraseReceived?.Invoke(phrase, endedAt);
        }

        private void ReadStatus(Packet packet, double now)
        {
            lastHeartbeat = now;
            ready = packet.ready;
            mode = packet.mode;
            serviceStatus = packet.status;
            if (packet.audio_telemetry_version == 1 && packet.mode == "microphone" &&
                Finite(packet.audio_rms) && packet.audio_rms >= 0 && packet.audio_rms <= 1 &&
                Finite(packet.audio_peak) && packet.audio_peak >= 0 && packet.audio_peak <= 1 &&
                Finite(packet.audio_age) && packet.audio_age >= -1 &&
                Finite(packet.audio_energy_threshold) && packet.audio_energy_threshold > 0 &&
                packet.audio_frames >= 0 && packet.audio_voiced_frames >= 0)
            {
                audioTelemetry = packet;
                audioTelemetryReceived = now;
            }
            else audioTelemetry = null;
        }

        private void OnDestroy() => OnDisable();
        private void OnDisable()
        {
            if (nativeSource) nativeSource.enabled = false;
            NativeEndSession("语音接收已关闭");
            socket?.Close();
            socket = null;
            ready = IsRunning = false;
            Status = "语音接收已关闭";
        }
    }
}
