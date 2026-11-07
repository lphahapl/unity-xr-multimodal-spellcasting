using System;
using System.IO;
using System.Linq;
using NarutoVR.SealLab;
using UnityEditor;
using UnityEngine;
using Lab = NarutoVR.SealLab.SealLab;

namespace NarutoVR.Editor
{
    [InitializeOnLoad]
    public static class SealGuidedCapture
    {
        private const string RequestPath = "Logs/guided-capture.request.json";
        private const string ResultPath = "Logs/guided-capture-result.json";
        private static double nextPoll;
        private static Lab active;
        private static string seal;
        private static bool pending;
        [Serializable] private sealed class Request { public string action, label, path, sourceLabel; }
        [Serializable] private sealed class Result
        {
            public string state, label, path, error;
            public int frames, acceptedFrames, rules;
            public float wristMin, wristMax, indexMin, indexMax;
            public bool applied;
        }
        static SealGuidedCapture() => EditorApplication.update += Poll;
        private static void Poll()
        {
            if (EditorApplication.timeSinceStartup < nextPoll || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            nextPoll = EditorApplication.timeSinceStartup + 0.5;
            try
            {
                if (File.Exists(RequestPath))
                {
                    var request = JsonUtility.FromJson<Request>(File.ReadAllText(RequestPath)); File.Delete(RequestPath);
                    if (!new[] { "子", "丑", "寅" }.Contains(request.label)) throw new InvalidOperationException("本轮仅允许子、丑、寅。");
                    if (request.action == "capture")
                    {
                        if (!EditorApplication.isPlaying) throw new InvalidOperationException("请先进入采集场景播放模式。");
                        active = UnityEngine.Object.FindFirstObjectByType<Lab>();
                        if (!active || !active.enabled || active.GetComponent<SealRecognitionTest>()) throw new InvalidOperationException("需要普通采集场景。");
                        if (pending || active.IsRecording || active.IsPreparing) throw new InvalidOperationException("已有采集，不能覆盖。");
                        active.SetTestMode(false); active.sampleLabel = request.label;
                        active.preparationSeconds = 10; active.captureSeconds = 8; active.recordingLimitSeconds = 60;
                        seal = request.label; active.StartRecording();
                        if (!active.IsPreparing) throw new InvalidOperationException("录制未能启动：" + active.Status);
                        pending = true; Write(new Result { state = "准备10秒，录制8秒", label = seal });
                    }
                    else if (request.action == "apply" || request.action == "analyse")
                    {
                        var savedProfile = AssetDatabase.LoadAssetAtPath<SealProfile>("Assets/ScriptableObjects/手印识别配置/" + request.label + ".asset");
                        if (savedProfile) AssetDatabase.SaveAssetIfDirty(savedProfile);
                        Analyse(request.path, request.label, request.action == "apply", request.sourceLabel);
                    }
                    else throw new InvalidOperationException("不支持的采集操作。");
                }
                if (!pending) return;
                if (!active || !EditorApplication.isPlaying) { pending = false; throw new InvalidOperationException("采集被播放状态变化中断。"); }
                if (active.IsPreparing) return;
                if (active.IsRecording) { Write(new Result { state = "录制中", label = seal, frames = active.RecordedFrames, path = active.RecordingPath }); return; }
                pending = false; Analyse(active.RecordingPath, seal, false);
            }
            catch (Exception e) { Write(new Result { state = "失败", label = seal, error = e.ToString() }); Debug.LogException(e); }
        }
        private static void Analyse(string path, string label, bool apply, string sourceLabel = null)
        {
            var frames = SealRecording.Read(path);
            if (frames.Any(f => f.label != (sourceLabel ?? label))) throw new InvalidOperationException("录制标签不一致。");
            // Explicit mapping applies only to this loaded copy; preserve the original recording.
            foreach (var frame in frames) frame.label = label;
            var draft = SealTemplateFromRecording.Build(frames, label);
            if (draft.error != null) throw new InvalidOperationException(draft.error);
            var wrist = draft.rules.FirstOrDefault(r => r.featureId == "Both.WristDistance");
            var index = draft.rules.FirstOrDefault(r => r.featureId == "Both.IndexTipDistance");
            var report = new Result { state = "录制完成，待确认范围", label = label, path = path, frames = frames.Count,
                acceptedFrames = draft.acceptedFrames, rules = draft.rules.Count,
                wristMin = wrist?.min ?? -1, wristMax = wrist?.max ?? -1, indexMin = index?.min ?? -1, indexMax = index?.max ?? -1 };
            if (apply)
            {
                if (draft.acceptedFrames < frames.Count * 0.8f) throw new InvalidOperationException("有效帧不足80%，请重新录制。");
                var profilePath = "Assets/ScriptableObjects/手印识别配置/" + label + ".asset";
                var profile = AssetDatabase.LoadAssetAtPath<SealProfile>(profilePath);
                var config = JsonUtility.FromJson<SealRecognitionTest.WeightFile>(File.ReadAllText("Assets/SealLab/Resources/SealTestWeights.json")).profiles.Single(p => p.sealName == label);
                var missing = config.rules.Where(r => r.weight > 0 && !draft.rules.Any(d => d.featureId.Replace(".Max.", ".Pinky.") == r.featureId)).ToArray();
                if (missing.Length > 0) throw new InvalidOperationException("重要特征不稳定：" + string.Join(",", missing.Select(r => r.featureId)));
                var backup = "Logs/BeforeGuidedCapture-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"); Directory.CreateDirectory(backup);
                File.Copy(profilePath, backup + "/" + label + ".asset");
                foreach (var rule in draft.rules)
                {
                    var setting = config.rules.Single(r => r.featureId == rule.featureId.Replace(".Max.", ".Pinky."));
                    rule.weight = setting.weight; rule.tolerance = setting.tolerance; rule.required = false;
                }
                Undo.RecordObject(profile, "使用确认录制标定结印"); profile.rules = draft.rules; profile.calibrated = true;
                EditorUtility.SetDirty(profile); AssetDatabase.SaveAssetIfDirty(profile);
                report.state = "整段标定已保存SO"; report.applied = true;
            }
            Write(report);
        }
        private static void Write(Result result) => File.WriteAllText(ResultPath, JsonUtility.ToJson(result, true));
    }
}
