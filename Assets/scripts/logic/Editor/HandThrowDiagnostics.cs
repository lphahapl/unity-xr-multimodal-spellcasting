#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace NarutoVR.SealCombat.Editor
{
    /// <summary>Opt-in bounded capture of the actual throw input; no microphones or camera frames.</summary>
    [InitializeOnLoad]
    public static class HandThrowDiagnostics
    {
        static StreamWriter writer;
        static SkillController target;
        static double until, last = double.NegativeInfinity;
        static int rows;
        public static string LastPath { get; private set; }
        static HandThrowDiagnostics()
        {
            EditorApplication.update += () => { if (writer != null && (!Application.isPlaying || !target || EditorApplication.timeSinceStartup >= until)) Stop(); };
            AssemblyReloadEvents.beforeAssemblyReload += Stop;
            EditorApplication.quitting += Stop;
        }
        [MenuItem("Tools/NarutoVR/线性RPG/录制45秒投掷诊断（播放模式）")]
        public static void Begin()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("投掷诊断需要播放模式");
            Stop();
            var rpg = UnityEngine.Object.FindFirstObjectByType<Rpg.LinearRpgController>();
            target = rpg && rpg.skills ? rpg.skills.effects : null;
            if (!target) throw new InvalidOperationException("缺少当前RPG投掷效果执行器");
            Directory.CreateDirectory("Logs/throw-captures");
            LastPath = "Logs/throw-captures/throw-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + ".jsonl";
            writer = new StreamWriter(LastPath, false, new System.Text.UTF8Encoding(false));
            until = EditorApplication.timeSinceStartup + 45; rows = 0; last = double.NegativeInfinity;
            target.ThrowObserved += Observe;
            Debug.Log("[投掷诊断] 已开始45秒记录，文件：" + LastPath);
        }
        static void Observe(HandThrowObservation observation)
        {
            if (writer == null || observation.observed == last) return;
            try
            {
                writer.WriteLine(JsonUtility.ToJson(observation)); rows++; last = observation.observed;
                if (rows % 120 == 0) writer.Flush();
                if (rows >= 15000 || EditorApplication.timeSinceStartup >= until) Stop();
            }
            catch (Exception error) { Debug.LogWarning("投掷诊断写入失败：" + error.Message); Stop(); }
        }
        [MenuItem("Tools/NarutoVR/线性RPG/停止投掷诊断")]
        public static void Stop()
        {
            if (target) target.ThrowObserved -= Observe;
            target = null;
            if (writer == null) return;
            try { writer.Dispose(); } finally { writer = null; }
            Debug.Log("[投掷诊断] 已保存" + rows + "个观测：" + LastPath);
        }
    }
}
#endif
