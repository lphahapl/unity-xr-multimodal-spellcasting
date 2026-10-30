using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

namespace NarutoVR.SealCombat
{
    /// <summary>Receives local sherpa-onnx events. All Unity API calls and events run on the main thread.</summary>
    [DisallowMultipleComponent]
    public sealed class SkillVoiceReceiver : MonoBehaviour
    {
        [Header("本机中文语音服务")]
        [InspectorName("Unity 接收端口"), Range(1024, 65535)] public int receivePort = 47831;
        [InspectorName("Python 服务端口"), Range(1024, 65535)] public int servicePort = 47832;
        [InspectorName("心跳超时（秒）"), Min(1)] public float heartbeatTimeout = 3f;
        [InspectorName("最大消息年龄（秒）"), Min(.1f)] public float maximumEventAge = 3f;
        [InspectorName("允许 WAV 回放测试")] public bool allowWavReplay = true;
        [InspectorName("允许显式模拟接线测试")] public bool allowProtocolTest = true;
        [InspectorName("记录识别日志")] public bool logPhrases = true;

        /// <summary>Second argument is estimated voice end mapped to Time.realtimeSinceStartupAsDouble.</summary>
        public event Action<string, double> PhraseReceived;
        public string Status { get; private set; } = "语音未连接：请在电脑启动语音服务";
        public bool IsRunning { get; private set; }
        public double LastLatencyMilliseconds { get; private set; }
        public double ClockUncertaintyMilliseconds { get; private set; }
        public string LastCaptureBasis { get; private set; } = "尚无输入";
        public string LastPhrase { get; private set; } = "";

        // JsonUtility populates these wire-format fields by reflection.
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
        }
#pragma warning restore 0649

        private UdpClient socket;
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

        /// <summary>Copies the active catalog's labels. Unconfigured receivers reject every phrase.</summary>
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

        private static double Now => Time.realtimeSinceStartupAsDouble;
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        private void OnEnable()
        {
            session = null;
            ready = IsRunning = false;
            lastHeartbeat = lastSync = double.NegativeInfinity;
            nextPing = 0;
            bestRtt = double.PositiveInfinity;
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
            if (socket == null) return;
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
            // Bounded polling prevents a noisy local process from blocking a VR frame.
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
                    sequence = 0;
                    bestRtt = double.PositiveInfinity;
                }
                // Refresh after long pauses; prefer the lowest-jitter sample within a window.
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
            // A restarted server must complete a new handshake before its events are accepted.
            if (packet.session != session) return;
            if (packet.kind == "heartbeat") { ReadStatus(packet, now); return; }
            if (packet.kind != "phrase" || !ready || now - lastHeartbeat > heartbeatTimeout ||
                now - lastSync > 10 || packet.seq <= sequence ||
                (packet.mode != "microphone" && packet.mode != "wav-replay" && packet.mode != "protocol-test") ||
                (packet.mode == "protocol-test" && !allowProtocolTest) ||
                (packet.mode == "wav-replay" && !allowWavReplay)) return;
            string phrase=SkillPhrase.Normalize(packet.phrase);
            if (!allowedPhrases.Contains(phrase)) return;
            if (!Finite(packet.ended_at) || !Finite(packet.sent_at) || packet.ended_at <= 0 ||
                packet.sent_at < packet.ended_at ||
                (packet.end_method != "rms-frame-end-estimate" && !(packet.mode == "protocol-test" && packet.end_method == "synthetic-test"))) return;
            double endedAt = packet.ended_at + offset;
            double age = now - endedAt;
            if (age < -.05 || age > maximumEventAge) return;
            sequence = packet.seq;
            LastPhrase = phrase;
            LastLatencyMilliseconds = age * 1000;
            LastCaptureBasis = packet.capture_basis ?? "unknown";
            if (logPhrases)
                Debug.Log($"[中文语音] {packet.phrase} | 词尾能量估计至接收 {LastLatencyMilliseconds:F1} ms | " +
                          $"时钟 RTT/2 {ClockUncertaintyMilliseconds:F1} ms | {LastCaptureBasis} | {packet.mode}", this);
            PhraseReceived?.Invoke(phrase, endedAt);
        }

        private void ReadStatus(Packet packet, double now)
        {
            lastHeartbeat = now;
            ready = packet.ready;
            mode = packet.mode;
            serviceStatus = packet.status;
        }

        private void OnDisable()
        {
            socket?.Close();
            socket = null;
            ready = IsRunning = false;
            Status = "语音接收已关闭";
        }
    }
}
