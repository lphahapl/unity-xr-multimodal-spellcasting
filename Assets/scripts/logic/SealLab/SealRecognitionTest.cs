using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace NarutoVR.SealLab
{
    [DefaultExecutionOrder(300)]
    public sealed class SealRecognitionTest : MonoBehaviour
    {
        [Header("一、测试来源与显示")]
        [InspectorName("结印实验台")] public SealLab lab;
        [InspectorName("识别结果面板")] public Text summaryText;
        [InspectorName("左侧实时特征")] public Text leftText;
        [InspectorName("右侧实时特征")] public Text rightText;
        [Header("二、起步权重配置")]
        [InspectorName("表格权重配置")] public TextAsset weights;
        [InspectorName("采用表格权重")] public bool applyWeights = true;
        [InspectorName("启用表格必需项（初期关闭）")] public bool enforceRequired;
        [Header("三、失败诊断")]
        [InspectorName("打印未通过原因")] public bool logFailures = true;
        [InspectorName("日志最短间隔（秒）"), Min(0.5f)] public float logInterval = 2;
        [InspectorName("未选预期印时诊断前几名"), Range(1, 12)] public int diagnosticCandidates = 3;
        private double nextUI, nextLog;
        private readonly List<SealProfile> copies = new();
        private List<SealProfile> originals;
        [Serializable] public sealed class WeightFile { public WeightProfile[] profiles; }
        [Serializable] public sealed class WeightProfile
        { public string sealName; public float threshold, minimumCoverage, holdSeconds; public FeatureRule[] rules; }

        private void Awake()
        {
            if (!lab) { enabled = false; return; }
            lab.recordOnStart = false; lab.SetTestMode(true);
            if (!applyWeights) return;
            if (!weights) { Debug.LogError("[结印测试] 权重配置未绑定，停止测试。", this); lab.enabled = false; enabled = false; return; }
            var config = JsonUtility.FromJson<WeightFile>(weights.text);
            originals = lab.profiles;
            foreach (var source in originals)
            {
                if (!source) { copies.Add(null); continue; }
                var copy = Instantiate(source); copy.name = source.name + "（测试副本）";
                var setting = config.profiles.FirstOrDefault(p => p.sealName == source.sealName);
                if (setting != null)
                {
                    copy.threshold = setting.threshold; copy.minimumCoverage = setting.minimumCoverage; copy.holdSeconds = setting.holdSeconds;
                    foreach (var rule in copy.rules)
                    {
                        var value = setting.rules.FirstOrDefault(r => r.featureId == rule.featureId.Replace(".Max.", ".Pinky."));
                        if (value == null) continue;
                        rule.weight = value.weight; rule.tolerance = value.tolerance; rule.required = enforceRequired && value.required;
                    }
                    int missing = setting.rules.Count(r => r.weight > 0 && !copy.rules.Any(x => x.featureId.Replace(".Max.", ".Pinky.") == r.featureId));
                    if (missing > 0) Debug.LogWarning($"[结印测试] {copy.sealName} 缺少 {missing} 项标定范围，只使用已有范围；请完成录制标定。", this);
                }
                copies.Add(copy);
            }
            lab.profiles = copies;
        }
        private void LateUpdate()
        {
            double now = Time.realtimeSinceStartupAsDouble;
            if (now >= nextUI) { nextUI = now + 0.1; RefreshUI(); }
            if (!logFailures || lab.Recognized || now < nextLog) return;
            nextLog = now + Math.Max(0.5, logInterval);
            var text = new StringBuilder("[结印测试] " + SealRecognizer.StateText(lab.RecognitionState));
            if (lab.RecognitionState == SealRecognitionState.Ambiguous)
                text.Append($"；要求领先 {lab.minimumScoreMargin * 100:F1} 分；" + string.Join("，", lab.Scores.Where(s => s.passes).Select(s => $"{s.profile.sealName}={s.score * 100:F1}")));
            else if (lab.RecognitionState == SealRecognitionState.Holding) text.Append($"；保持进度 {lab.HoldProgress:P0}");
            else if (lab.RecognitionState == SealRecognitionState.NoTracking) text.Append("；手部未连接、无有效追踪、数据过期或双手采样不同步。");
            if (lab.Frame != null) text.Append("\n" + SealLabView.HandState("LEFT", lab.Frame.left) + "；" + SealLabView.HandState("RIGHT", lab.Frame.right));
            if (lab.RecognitionState != SealRecognitionState.NoTracking && lab.RecognitionState != SealRecognitionState.Holding)
                foreach (var score in lab.testExpectedSeal ? lab.Scores : lab.Scores.Take(Mathf.Clamp(diagnosticCandidates, 1, 12)))
                    if (!lab.testExpectedSeal || score.profile && score.profile.sealName == lab.testExpectedSeal.sealName)
                        text.Append("\n" + SealTestDiagnostics.Explain(score));
            Debug.Log(text.ToString(), this);
        }
        private void RefreshUI()
        {
            var text = new StringBuilder("十二印 · 识别测试\n");
            text.AppendLine("识别结果：" + SealRecognizer.HudName(lab.Recognized));
            text.AppendLine(SealRecognizer.StateText(lab.RecognitionState));
            text.AppendLine("置信度显示为匹配分数，并非正确概率");
            if (lab.Candidate) text.AppendLine($"候选：{lab.Candidate.sealName} / 保持 {lab.HoldProgress:P0}");
            if (lab.testExpectedSeal) text.AppendLine("预期：" + lab.testExpectedSeal.sealName + " / " + (!lab.Recognized ? "等待识别" : lab.Recognized.sealName == lab.testExpectedSeal.sealName ? "符合" : "不符合"));
            text.AppendLine($"已启用 {lab.profiles.Count(p => p && p.ValidateRules() == null)} / {lab.profiles.Count} 个模板");
            foreach (var s in lab.Scores)
                if (s.profile) text.AppendLine($"{s.profile.sealName}  {s.score * 100:F1} 分 / 阈值 {s.profile.threshold * 100:F0} / 覆盖 {s.coverage:P0}" + (s.profile.calibrated ? "" : "（未标定）"));
            if (lab.Frame != null) { text.AppendLine(SealLabView.HandState("LEFT", lab.Frame.left)); text.AppendLine(SealLabView.HandState("RIGHT", lab.Frame.right)); }
            summaryText.text = text.ToString();
            var a = new StringBuilder("实时特征 · 左手\n"); var b = new StringBuilder("实时特征 · 右手\n");
            int pair = 0;
            foreach (var f in lab.Features)
            {
                var dest = f.id.StartsWith("L.") ? a : f.id.StartsWith("R.") ? b : pair++ < 5 ? a : b;
                dest.AppendLine(SealChineseText.FeatureName(f.id) + "：" + (f.valid ? $"{f.value:F3} {SealChineseText.Unit(f.unit)}" + (f.highConfidence ? "" : "［低质量］") : "无数据"));
            }
            leftText.text = a.ToString(); rightText.text = b.ToString();
        }
        private void OnDestroy()
        {
            if (originals != null && lab) lab.profiles = originals;
            foreach (var p in copies) if (p) Destroy(p);
        }
    }
}
