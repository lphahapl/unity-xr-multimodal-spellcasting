#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEngine;

namespace NarutoVR.SealCombat.Editor
{
    /// <summary>No microphone or UDP binding: exercises diagnostics on an inactive object.</summary>
    public static class VoiceDiagnosticsChecks
    {
        public static int Run()
        {
            var go = new GameObject("语音遥测回归（临时）");
            go.SetActive(false);
            try
            {
                var receiver = go.AddComponent<SkillVoiceReceiver>();
                Require(receiver.BuildDebugDetails().Contains("尚未收到"), "旧服务不可显示为已有声音");
                var packetType = typeof(SkillVoiceReceiver).GetNestedType("Packet", BindingFlags.NonPublic);
                var read = typeof(SkillVoiceReceiver).GetMethod("ReadStatus", BindingFlags.Instance | BindingFlags.NonPublic);
                string json = "{\"mode\":\"microphone\",\"ready\":true,\"audio_telemetry_version\":1," +
                    "\"audio_device\":\"测试麦克风\",\"audio_backend\":\"测试后端\",\"audio_sample_rate\":16000," +
                    "\"audio_age\":0,\"audio_rms\":0,\"audio_peak\":0,\"audio_energy_threshold\":0.012," +
                    "\"audio_frames\":1}";
                read.Invoke(receiver, new[] { JsonUtility.FromJson(json, packetType), (object)Time.realtimeSinceStartupAsDouble });
                string silent = receiver.BuildDebugDetails();
                Require(silent.Contains("低能量/静音") && silent.Contains("采样持续更新"), "静音流与断流应区分");
                Require(silent.Contains("不提供概率") && silent.Contains("未命中"), "不能以音量冒充匹配度");
                read.Invoke(receiver, new[] { JsonUtility.FromJson(json.Replace("\"audio_age\":0", "\"audio_age\":2"), packetType),
                    (object)Time.realtimeSinceStartupAsDouble });
                Require(receiver.BuildDebugDetails().Contains("输入已过期"), "过期样本不能显示正在有声");
                // Repeated enable must replace, not leak, this receiver's own UDP socket.
                using(var probe=new System.Net.Sockets.UdpClient(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback,0)))
                    receiver.receivePort=((System.Net.IPEndPoint)probe.Client.LocalEndPoint).Port;
                var enable=typeof(SkillVoiceReceiver).GetMethod("OnEnable",BindingFlags.Instance|BindingFlags.NonPublic);
                var disable=typeof(SkillVoiceReceiver).GetMethod("OnDisable",BindingFlags.Instance|BindingFlags.NonPublic);
                enable.Invoke(receiver,null); enable.Invoke(receiver,null);
                var socket=(System.Net.Sockets.UdpClient)typeof(SkillVoiceReceiver).GetField("socket",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(receiver);
                Require(socket!=null && socket.Client.IsBound,"重复启用应释放旧套接字，不能与自己冲突");
                disable.Invoke(receiver,null);
                using(var released=new System.Net.Sockets.UdpClient(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback,receiver.receivePort)))
                    Require(released.Client.IsBound,"停用后必须释放端口");
                return 6;
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        private static void Require(bool valid, string message)
        {
            if (!valid) throw new Exception("语音遥测回归：" + message);
        }
    }
}
#endif
