using System.Collections.Generic;
using System.Text;
using Oculus.Interaction.Input;
using UnityEngine;
using UnityEngine.UI;

namespace NarutoVR.SealLab
{
    public sealed class SealLabView : MonoBehaviour
    {
        [Header("一、实验台与头显文字显示")]
        [InspectorName("实验台数据源")]
        [Tooltip("提供实时骨骼、录制状态及测试结果的SealLab组件。")]
        public SealLab lab;
        [InspectorName("头显内状态文字")]
        [Tooltip("显示准备倒计时、录制进度或实时测试结果的世界空间文字。")]
        public Text statusText;
        [Header("二、原始骨骼可视化")]
        [InspectorName("骨骼线条材质")]
        [Tooltip("用于绘制实际追踪到的手部骨骼线条。")]
        public Material boneMaterial;
        [Header("三、中文字体")]
        [InspectorName("头显中文字体（随包资源）")]
        [Tooltip("用于显示中文印名和提示。留空时加载随包的Noto中文黑体；电脑与Quest使用同一份字体。")]
        public Font chineseFont;
        private readonly List<LineRenderer> lines = new();
        private readonly List<int[]> chains = new();
        private readonly StringBuilder text = new();
        private double nextText;
        private AudioSource cues;
        private AudioClip tickCue, startCue, endCue;
        private int lastCountdown = -1;
        private bool wasRecording;
        private void Start()
        {
            if (!chineseFont) chineseFont = Resources.Load<Font>("Fonts/NotoSansCJKsc-Regular");
            if (statusText && chineseFont) statusText.font = chineseFont;
            cues = gameObject.AddComponent<AudioSource>(); cues.playOnAwake = false; cues.spatialBlend = 0;
            tickCue = MakeCue("Prepare", 660, 0.07f);
            startCue = MakeCue("Record", 990, 0.18f);
            endCue = MakeCue("Saved", 440, 0.3f);
            for (int side = 0; side < 2; ++side)
            for (int f = 0; f < 5; ++f)
            {
                var finger = HandJointUtils.FingerToJointList[f];
                var chain = new int[finger.Length + 1]; chain[0] = (int)HandJointId.HandWristRoot;
                for (int j = 0; j < finger.Length; ++j) chain[j + 1] = (int)finger[j];
                chains.Add(chain);
                var line = new GameObject($"{side}-{f} raw bones").AddComponent<LineRenderer>();
                line.transform.SetParent(transform, false); line.sharedMaterial = boneMaterial;
                line.widthMultiplier = 0.003f; line.positionCount = chain.Length;
                line.useWorldSpace = true; line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                line.receiveShadows = false; lines.Add(line);
            }
        }
        private void LateUpdate()
        {
            if (!lab) return;
            int countdown = lab.IsPreparing ? Mathf.CeilToInt((float)lab.RecordingSecondsRemaining) : -1;
            if (countdown >= 1 && countdown != lastCountdown) cues.PlayOneShot(tickCue, 0.3f);
            lastCountdown = countdown;
            if (lab.IsRecording && !wasRecording) cues.PlayOneShot(startCue, 0.4f);
            else if (!lab.IsRecording && wasRecording) cues.PlayOneShot(endCue, 0.4f);
            wasRecording = lab.IsRecording;
            for (int i = 0; i < lines.Count; ++i)
            {
                var hand = lab.Frame == null ? null : i < 5 ? lab.Frame.left : lab.Frame.right; var chain = chains[i];
                bool valid = hand != null && hand.connected && hand.tracked && hand.fresh;
                if (hand != null) foreach (int joint in chain) valid &= hand.jointValid[joint];
                lines[i].enabled = valid;
                if (valid) for (int j = 0; j < chain.Length; ++j)
                    lines[i].SetPosition(j, lab.recordingOrigin.TransformPoint(hand.joints[chain[j]].position));
            }
            if (!statusText || Time.realtimeSinceStartupAsDouble < nextText) return;
            nextText = Time.realtimeSinceStartupAsDouble + 0.1;
            text.Clear(); text.AppendLine(lab.IsTesting ? "结印实验台 / 实时识别测试" : "结印实验台 / 骨骼采集");
            text.AppendLine(lab.TrackingSourceDisplay);
            if (lab.xrHandsSource) text.AppendLine(lab.xrHandsSource.Status);
            if (lab.IsTesting)
            {
                text.AppendLine("<size=44>识别结果：" + SealRecognizer.HudName(lab.Recognized) + "</size>");
                text.AppendLine(SealRecognizer.StateText(lab.RecognitionState));
                if (lab.Candidate && !lab.Recognized) text.AppendLine("候选印：" + SealRecognizer.HudName(lab.Candidate) + $" / 保持进度 {lab.HoldProgress:P0}");
                if (lab.testExpectedSeal) text.AppendLine("预期印：" + SealRecognizer.HudName(lab.testExpectedSeal) + " / " +
                    (!lab.Recognized ? "等待识别" : lab.Recognized == lab.testExpectedSeal ? "符合预期" : "与预期不符"));
                for (int i = 0; i < Mathf.Min(3, lab.Scores.Count); ++i)
                {
                    var score = lab.Scores[i];
                    text.AppendLine(SealRecognizer.HudName(score.profile) + $" 得分 {score.score:P0} / 覆盖率 {score.coverage:P0}");
                }
                if (lab.Frame != null) { text.AppendLine(HandState("LEFT", lab.Frame.left)); text.AppendLine(HandState("RIGHT", lab.Frame.right)); }
                statusText.text = text.ToString();
                return;
            }
            text.AppendLine(lab.IsPreparing ? $"请摆好手印：{Mathf.CeilToInt((float)lab.RecordingSecondsRemaining)} 秒后开始录制" :
                lab.IsRecording ? $"正在录制：剩余 {lab.RecordingSecondsRemaining:F1} 秒 / 已采集 {lab.RecordedFrames} 帧" : lab.Status);
            if (lab.Frame != null) { text.AppendLine(HandState("LEFT", lab.Frame.left)); text.AppendLine(HandState("RIGHT", lab.Frame.right)); }
            text.AppendLine(lab.Recognized ? "当前印：" + lab.Recognized.sealName : "当前印：未识别");
            foreach (var value in lab.Features)
                if (value.id.StartsWith("Both.") || value.id.EndsWith(".Curl"))
                    text.AppendLine(SealChineseText.FeatureName(value.id) + "：" + (value.valid ? value.value.ToString("F2") + " " + SealChineseText.Unit(value.unit) + (value.highConfidence ? "" : "［低质量］") : "无有效数据"));
            statusText.text = text.ToString();
        }
        private static AudioClip MakeCue(string name, float frequency, float duration)
        {
            const int sampleRate = 16000;
            var samples = new float[Mathf.CeilToInt(sampleRate * duration)];
            for (int i = 0; i < samples.Length; ++i)
                samples[i] = Mathf.Sin(2 * Mathf.PI * frequency * i / sampleRate) * Mathf.Sin(Mathf.PI * i / samples.Length);
            var clip = AudioClip.Create(name, samples.Length, 1, sampleRate, false); clip.SetData(samples, 0); return clip;
        }
        private void OnDestroy() { if (tickCue) Destroy(tickCue); if (startCue) Destroy(startCue); if (endCue) Destroy(endCue); }
        public static string HandState(string side, HandSnapshot hand) =>
            $"{(side == "LEFT" ? "左手" : side == "RIGHT" ? "右手" : side)}：{(hand.connected && hand.tracked && hand.fresh ? "追踪有效" : "追踪不可用")} / 质量{(hand.highConfidence ? "高" : "低")} / 第{hand.version}帧" +
            (hand.confidenceSource != null && hand.confidenceSource.StartsWith("derived") ? "［关节有效性推导］" : "");
    }
}
