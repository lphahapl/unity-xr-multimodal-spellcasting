using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NarutoVR.SealLab;
using Oculus.Interaction.Input;
using UnityEditor;
using UnityEngine;
using Lab = NarutoVR.SealLab.SealLab;

namespace NarutoVR.Editor
{
    public sealed class SealLabWindow : EditorWindow
    {
        private Lab lab;
        private SealProfile profile;
        private Vector2 scroll;
        private List<SealFrame> replay;
        private int replayIndex;
        private bool playReplay;
        private double replayStarted;
        private double replayOffset;
        private string message = "", replayPath = "";
        private readonly SealFeatureExtractor extractor = new();
        private readonly List<SealFeature> features = new();
        private SealFrame displayed;
        private bool HasReplay => replay != null && replay.Count > 0;
        private string referenceLabel = "";
        private Vector2 testScroll;

        [MenuItem("Tools/NarutoVR/Seal Lab")]
        public static void Open() => GetWindow<SealLabWindow>("结印实验台").minSize = new Vector2(980, 600);
        [MenuItem("Tools/NarutoVR/结印实验台")]
        private static void OpenChinese() => Open();
        private void OnEnable() { titleContent = new GUIContent("结印实验台"); EditorApplication.update += Tick; SceneView.duringSceneGui += DrawReplay; }
        private void OnDisable() { EditorApplication.update -= Tick; SceneView.duringSceneGui -= DrawReplay; }
        private void Tick()
        {
            if (!lab) lab = FindFirstObjectByType<Lab>();
            if (playReplay && replay?.Count > 0)
            {
                double target = replay[0].time + replayOffset + EditorApplication.timeSinceStartup - replayStarted;
                while (replayIndex + 1 < replay.Count && replay[replayIndex + 1].time <= target) replayIndex++;
                if (replayIndex == replay.Count - 1) playReplay = false;
                SceneView.RepaintAll();
            }
            Repaint();
        }
        private void OnGUI()
        {
            EditorGUILayout.LabelField("结印实验台 · 实际追踪 → 特征 → 录制/回放 → 加权评分", EditorStyles.boldLabel);
            lab = (Lab)EditorGUILayout.ObjectField("实验台数据源", lab, typeof(Lab), true);
            if (lab) EditorGUILayout.LabelField("追踪来源", lab.TrackingSourceDisplay);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("采集 / 调参")) { replay = null; playReplay = false; if (lab && lab.IsTesting) lab.SetTestMode(false); }
                if (GUILayout.Button("实时测试（不录制）")) { replay = null; playReplay = false; if (lab) lab.SetTestMode(true); }
                if (GUILayout.Button("打开录制回放")) LoadReplay();
                if (GUILayout.Button("打开实验场景")) SealLabBuilder.OpenScene();
                if (GUILayout.Button("Steam Link 场景")) SealSteamLinkSetup.Open();
            }
            if (lab && lab.IsTesting)
            {
                DrawLiveTest();
                return;
            }
            if (HasReplay)
            {
                EditorGUILayout.LabelField(Path.GetFileName(replayPath));
                EditorGUI.BeginChangeCheck();
                replayIndex = EditorGUILayout.IntSlider("回放帧", replayIndex, 0, replay.Count - 1);
                if (EditorGUI.EndChangeCheck()) { playReplay = false; SceneView.RepaintAll(); }
                if (GUILayout.Button(playReplay ? "暂停回放" : "按录制时间播放"))
                { playReplay = !playReplay; replayStarted = EditorApplication.timeSinceStartup; replayOffset = replay[replayIndex].time - replay[0].time; }
                displayed = replay[replayIndex];
            }
            else displayed = Application.isPlaying && lab ? lab.Frame : null;

            if (lab && !HasReplay)
            {
                using (new EditorGUI.DisabledScope(lab.IsPreparing || lab.IsRecording))
                {
                    lab.sampleLabel = EditorGUILayout.TextField("录制样本标签", lab.sampleLabel);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        lab.preparationSeconds = EditorGUILayout.FloatField("准备倒计时（秒）", lab.preparationSeconds);
                        lab.captureSeconds = EditorGUILayout.FloatField("自动录制（秒）", lab.captureSeconds);
                    }
                }
                EditorGUILayout.LabelField(lab.IsPreparing ? $"请摆好手印：{lab.RecordingSecondsRemaining:F1} 秒后自动开始" :
                    lab.IsRecording ? $"正在录制：剩余 {lab.RecordingSecondsRemaining:F1} 秒，结束后自动保存" : lab.Status);
                using (new EditorGUI.DisabledScope(!Application.isPlaying || !lab.enabled))
                    if (GUILayout.Button(lab.IsPreparing ? "取消准备" : lab.IsRecording ? $"提前停止并保存 ({lab.RecordedFrames} 帧)" : "准备录制（倒计时后自动开始、自动停止）"))
                    { if (lab.IsPreparing || lab.IsRecording) lab.StopRecording(); else lab.StartRecording(); }
                EditorGUILayout.SelectableLabel(lab.RecordingPath ?? lab.RecordingDirectory, GUILayout.Height(18));
            }
            profile = (SealProfile)EditorGUILayout.ObjectField("当前印模板", profile, typeof(SealProfile), false);
            if (!profile && lab && lab.profiles.Count > 0) profile = lab.profiles[0];
            if (HasReplay && profile)
            {
                string[] labels = replay.Select(f => f.label ?? "").Distinct().ToArray();
                int current = Array.IndexOf(labels, referenceLabel); if (current < 0) current = 0;
                referenceLabel = labels[EditorGUILayout.Popup("用于标定的录制标签", current, labels)];
                if (GUILayout.Button($"使用该标签整段录制，为“{profile.sealName}”生成模板草稿")) CaptureRecordingDraft();
            }
            if (!string.IsNullOrEmpty(message)) EditorGUILayout.HelpBox(message, MessageType.Info);
            if (displayed == null)
            {
                EditorGUILayout.HelpBox("打开 SealLab 场景并进入 Play，或载入原始录制。未连接 Quest 时显示无数据，不会生成假手势。", MessageType.Info);
                return;
            }
            extractor.Extract(displayed, features);
            EditorGUILayout.LabelField($"采样时间：{displayed.time:F3} 秒   标签：{SealChineseText.SampleLabel(displayed.label)}   每手 {displayed.left.joints.Length} 个关节");
            EditorGUILayout.LabelField(SealLabView.HandState("LEFT", displayed.left) + "    " + SealLabView.HandState("RIGHT", displayed.right));
            if (GUILayout.Button("从当前有效特征生成模板草稿（需人工校准后启用）")) CaptureDraft();
            if (profile)
            {
                var score = SealScorer.Evaluate(profile, features);
                EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(false, 22), score.score,
                    $"{profile.sealName}：得分 {score.score:P0}  覆盖率 {score.coverage:P0}  {SealChineseText.Reason(score.reason)}");
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUI.BeginChangeCheck();
                    bool calibrated = EditorGUILayout.ToggleLeft("已确认模板", profile.calibrated, GUILayout.Width(115));
                    bool low = EditorGUILayout.ToggleLeft("允许低置信特征", profile.allowLowConfidence, GUILayout.Width(150));
                    float threshold = EditorGUILayout.Slider("通过阈值", profile.threshold, 0.01f, 1);
                    if (EditorGUI.EndChangeCheck()) { Undo.RecordObject(profile, "Tune seal"); profile.calibrated = calibrated; profile.allowLowConfidence = low; profile.threshold = threshold; EditorUtility.SetDirty(profile); }
                }
            }
            EditorGUILayout.LabelField("特征名称（悬停查看固定标识）         原始值 / 单位           质量      权重        最小 / 最大目标值       容差       必需");
            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (var feature in features) DrawFeature(feature);
            EditorGUILayout.EndScrollView();
            if (profile && GUILayout.Button("保存模板修改")) AssetDatabase.SaveAssetIfDirty(profile);
        }
        private void DrawLiveTest()
        {
            EditorGUILayout.HelpBox("测试所有已确认的模板，不录制、不改参数。可先选预期印再摆动作；预期值只用于对比，不参与识别。", MessageType.Info);
            if (GUILayout.Button("退出测试，返回采集 / 调参")) { lab.SetTestMode(false); return; }
            lab.testExpectedSeal = (SealProfile)EditorGUILayout.ObjectField("预期印（可留空）", lab.testExpectedSeal, typeof(SealProfile), false);
            if (!string.IsNullOrEmpty(message)) EditorGUILayout.HelpBox(message, MessageType.Info);
            if (!Application.isPlaying)
            { EditorGUILayout.HelpBox("点击 Unity Play 并连接 Quest，随后直接摆手印。无需中途点击任何按钮。", MessageType.Info); return; }
            if (lab.Frame == null)
            { EditorGUILayout.HelpBox(lab.Status, MessageType.Info); return; }
            string result = lab.Recognized ? lab.Recognized.sealName : "未识别";
            EditorGUILayout.LabelField("识别结果：" + result, new GUIStyle(EditorStyles.boldLabel) { fontSize = 26 }, GUILayout.Height(40));
            EditorGUILayout.LabelField(SealRecognizer.StateText(lab.RecognitionState));
            if (lab.Candidate && !lab.Recognized)
                EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(false, 22), lab.HoldProgress, "候选：" + lab.Candidate.sealName + " / 保持进度");
            if (lab.testExpectedSeal)
                EditorGUILayout.LabelField("预期：" + lab.testExpectedSeal.sealName + "    对比：" +
                    (!lab.Recognized ? "等待结果" : lab.Recognized == lab.testExpectedSeal ? "正确" : "不一致"));
            EditorGUILayout.LabelField(SealLabView.HandState("LEFT", lab.Frame.left) + "    " + SealLabView.HandState("RIGHT", lab.Frame.right));
            EditorGUILayout.LabelField($"歧义保护：两个达标模板分差至少 {lab.minimumScoreMargin:P0}");
            testScroll = EditorGUILayout.BeginScrollView(testScroll);
            foreach (var score in lab.Scores)
            {
                if (!score.profile) continue;
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(score.profile.sealName, GUILayout.Width(55));
                    EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(false, 21, GUILayout.MinWidth(180)), score.score, $"得分 {score.score:P1}");
                    EditorGUILayout.LabelField($"覆盖率 {score.coverage:P0}", GUILayout.Width(110));
                    EditorGUILayout.LabelField(score.profile == lab.Recognized ? "已确认" : ReasonChinese(score.reason), GUILayout.MinWidth(220));
                }
                if (score.details.Count > 0)
                {
                    var failures = score.details.Where(d => !d.available || d.match < 0.7f).OrderBy(d => d.match).Take(3);
                    foreach (var detail in failures)
                        EditorGUILayout.LabelField("    " + SealChineseText.FeatureName(detail.rule.featureId) + "：" +
                            (!detail.available ? "缺失 / 低质量" : $"{detail.feature.value:F3} {SealChineseText.Unit(detail.feature.unit)}，目标[{detail.rule.min:F3}, {detail.rule.max:F3}]，匹配 {detail.match:P0}"), EditorStyles.miniLabel);
                }
            }
            EditorGUILayout.EndScrollView();
        }
        private static string ReasonChinese(string reason) => SealChineseText.Reason(reason);
        private void CaptureRecordingDraft()
        {
            var draft = SealTemplateFromRecording.Build(replay, referenceLabel);
            if (draft.error != null) { message = draft.error; return; }
            Undo.RecordObject(profile, "Build template from recording");
            profile.rules = draft.rules; profile.calibrated = false;
            EditorUtility.SetDirty(profile);
            message = $"使用 {draft.acceptedFrames}/{draft.matchingFrames} 帧生成 {draft.rules.Count} 条规则，目标取10%～90%分位区间。请检查并勾选“已确认模板”，保存后进入实时测试。草稿替换可撤销。";
        }
        private void DrawFeature(SealFeature feature)
        {
            var rule = profile ? profile.rules.Find(r => r != null && r.featureId == feature.id) : null;
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(new GUIContent(SealChineseText.FeatureName(feature.id), "固定标识：" + feature.id), GUILayout.Width(220));
                EditorGUILayout.LabelField(feature.valid ? $"{feature.value:F3} {SealChineseText.Unit(feature.unit)}" : "无有效数据", GUILayout.Width(145));
                EditorGUILayout.LabelField(feature.valid ? feature.highConfidence ? "高" : "低" : "—", GUILayout.Width(45));
                if (rule == null)
                {
                    if (profile && GUILayout.Button("添加规则", GUILayout.Width(90)))
                    { Undo.RecordObject(profile, "Add feature"); profile.rules.Add(NewRule(feature)); EditorUtility.SetDirty(profile); }
                    return;
                }
                EditorGUI.BeginChangeCheck();
                float weight = EditorGUILayout.FloatField(rule.weight, GUILayout.Width(55));
                float min = EditorGUILayout.FloatField(rule.min, GUILayout.Width(70));
                float max = EditorGUILayout.FloatField(rule.max, GUILayout.Width(70));
                float tolerance = EditorGUILayout.FloatField(rule.tolerance, GUILayout.Width(65));
                bool required = EditorGUILayout.Toggle(rule.required, GUILayout.Width(25));
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(profile, "Tune feature"); rule.weight = weight; rule.min = min; rule.max = max;
                    rule.tolerance = tolerance; rule.required = required; EditorUtility.SetDirty(profile);
                }
                float match = feature.valid && (profile.allowLowConfidence || feature.highConfidence)
                    ? Mathf.Clamp01(1 - Mathf.Max(0, Mathf.Max(rule.min - feature.value, feature.value - rule.max)) / Mathf.Max(0.00001f, rule.tolerance)) : 0;
                EditorGUILayout.LabelField($"匹配 {match:P0} / 贡献 {rule.weight * match:F2}", GUILayout.Width(155));
            }
        }
        private static FeatureRule NewRule(SealFeature f) => new() { featureId = f.id, min = f.value, max = f.value,
            tolerance = f.unit.StartsWith("m") ? 0.015f : 15, weight = f.valid && f.highConfidence ? 1 : 0 };
        private void CaptureDraft()
        {
            if (!profile) { message = "请先创建或选择结印模板。"; return; }
            if (!displayed.left.fresh || !displayed.right.fresh || !displayed.left.tracked || !displayed.right.tracked)
            { message = "需要双手有效的原始快照。"; return; }
            var rules = new List<FeatureRule>();
            foreach (var feature in features) if (feature.valid && feature.highConfidence) rules.Add(NewRule(feature));
            if (rules.Count == 0) { message = "没有高置信的特征可用于草稿。"; return; }
            Undo.RecordObject(profile, "Capture draft"); profile.rules = rules; profile.calibrated = false; EditorUtility.SetDirty(profile);
            message = "已生成草稿。请对照多次正样本、相近印及过渡动作调整范围/权重，再勾选已确认模板。";
        }
        private void LoadReplay()
        {
            string path = EditorUtility.OpenFilePanel("打开原始骨骼录制", lab ? lab.RecordingDirectory : "", "jsonl");
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                var loaded = SealRecording.Read(path);
                if (lab && lab.IsTesting) lab.SetTestMode(false);
                replay = loaded; replayPath = path; replayIndex = 0; playReplay = false;
                referenceLabel = profile && replay.Any(f => f.label == profile.sealName) ? profile.sealName : replay[0].label;
                message = "回放骨骼重新提取特征；可用整段录制生成模板范围，再进入实时测试。";
            }
            catch (Exception ex) { message = ex.Message; }
        }
        private void DrawReplay(SceneView view)
        {
            if (!HasReplay || displayed == null) return;
            DrawHand(displayed.left, Color.cyan); DrawHand(displayed.right, Color.magenta);
        }
        private static void DrawHand(HandSnapshot hand, Color color)
        {
            if (!hand.connected || !hand.tracked || !hand.fresh) return;
            Handles.color = color;
            foreach (var chain in HandJointUtils.FingerToJointList)
            {
                int previous = (int)HandJointId.HandWristRoot;
                foreach (var joint in chain)
                {
                    int next = (int)joint;
                    if (hand.jointValid[previous] && hand.jointValid[next]) Handles.DrawLine(hand.joints[previous].position, hand.joints[next].position, 3);
                    previous = next;
                }
            }
        }
    }
}
