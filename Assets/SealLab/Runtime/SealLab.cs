using System;
using System.Collections.Generic;
using System.IO;
using Oculus.Interaction.Input;
using UnityEngine;

namespace NarutoVR.SealLab
{
    [DefaultExecutionOrder(200)]
    public sealed class SealLab : MonoBehaviour
    {
        [Header("一、实际手部追踪来源")]
        [InspectorName("左手实际追踪源")]
        [Tooltip("绑定左手 FromOVRHandDataSource 直接输出的 Hand，避免读取保留上一姿态或合成的手。场景已自动连接。")]
        public MonoBehaviour leftTrackedHand;
        [InspectorName("右手实际追踪源")]
        [Tooltip("绑定右手 FromOVRHandDataSource 直接输出的 Hand。场景已自动连接。")]
        public MonoBehaviour rightTrackedHand;
        [InspectorName("骨骼录制坐标原点")]
        [Tooltip("通常为 Rig 的 TrackingSpace；缩放必须为1。保存的关节位置相对该坐标原点。")]
        public Transform recordingOrigin;
        [Header("一（补充）、Steam Link / 通用手追踪输入")]
        [InspectorName("XR Hands 追踪来源")]
        [Tooltip("独立Steam Link场景使用此来源；留空时保持原有Meta原生/Quest Link输入。质量标志由关节有效性推导，不等同于Meta原生置信度。")]
        public SealXRHandsSource xrHandsSource;
        public string TrackingSource => xrHandsSource ? "OpenXR / XR Hands / " + xrHandsSource.RuntimeName : "Meta OVR";
        public string TrackingSourceDisplay => xrHandsSource ? "通用手追踪 / 运行时：" + xrHandsSource.RuntimeName : "Meta 原生手追踪";
        [Header("二、参与识别的印模板")]
        [InspectorName("印模板列表")]
        [Tooltip("测试时同时比较此列表内所有已确认的模板，尚未确认的模板不识别。")]
        public List<SealProfile> profiles = new();
        [Header("三、采样与追踪有效性")]
        [InspectorName("最高采样频率（次/秒）"), Range(10, 120)]
        [Tooltip("这是采样上限，不保证设备提供等量的新追踪数据。")]
        public int maximumSampleRate = 60;
        [InspectorName("数据过期时间（秒）"), Range(0.04f, 0.5f)]
        [Tooltip("手部数据版本超过该时长未变化时，视为过期并停止用它识别。")]
        public float staleAfterSeconds = 0.12f;
        [Header("四、免手操作录制")]
        [InspectorName("录制样本标签")]
        [Tooltip("例如寅、未、none、transition。准备录制时锁定标签，与当前所选模板相互独立。")]
        public string sampleLabel = "unlabelled";
        [InspectorName("运行后自动准备录制")]
        [Tooltip("适合Quest独立包。启动后先倒计时，再自动录制和保存；测试模式下不录制。")]
        public bool recordOnStart;
        [InspectorName("摆印准备倒计时（秒）"), Min(0)]
        [Tooltip("点击准备录制后留给你摆手印的时间，可改成10至15秒。")]
        public float preparationSeconds = 5;
        [InspectorName("自动录制时长（秒）"), Min(0.1f)]
        [Tooltip("倒计时结束后采集这么久，到时自动停止并保存。")]
        public float captureSeconds = 5;
        [InspectorName("单次录制时长上限（秒）"), Min(1)]
        [Tooltip("实际录制时长取自动录制时长与本上限的较小值。")]
        public float recordingLimitSeconds = 60;
        [Header("五、实时识别与测试")]
        [InspectorName("两个达标印的最小分差"), Range(0, 1)]
        [Tooltip("默认0.05表示5个百分点。两个印都达标且分差不足时返回无法区分；0关闭此保护。")]
        public float minimumScoreMargin = 0.05f;
        [InspectorName("测试模式（不录制）"), SerializeField]
        [Tooltip("启动后实时识别并在头显显示结果。可在保存场景后用于Quest独立包测试。")]
        private bool testMode;
        [InspectorName("测试预期印（可留空）")]
        [Tooltip("用于显示结果是否与你预期一致，不参与评分、不提示识别器答案。")]
        public SealProfile testExpectedSeal;

        public SealFrame Frame { get; private set; }
        public readonly List<SealFeature> Features = new();
        public List<SealScore> Scores => recognition.Scores;
        public SealProfile Recognized => recognition.Recognized;
        public SealProfile Candidate => recognition.Candidate;
        public SealRecognitionState RecognitionState => recognition.State;
        public float HoldProgress => recognition.HoldProgress;
        public bool IsTesting => testMode;
        public string Status { get; private set; } = "等待进入播放模式";
        public bool IsRecording => recorder.IsOpen;
        public bool IsPreparing => schedule.Phase == RecordingPhase.Preparing;
        public double RecordingSecondsRemaining => schedule.Remaining(Time.realtimeSinceStartupAsDouble);
        public string RecordingPath => recorder.Path;
        public int RecordedFrames => recorder.FrameCount;
        public string RecordingDirectory => Path.Combine(Application.persistentDataPath, "SealRecordings");
        private IHand left, right;
        private int leftVersion = int.MinValue, rightVersion = int.MinValue;
        private double leftObserved, rightObserved, nextSample;
        private string recordingLabel;
        private bool forceEvaluation;
        private readonly SealFeatureExtractor extractor = new();
        private readonly SealRecognizer recognition = new();
        private readonly SealRecording recorder = new();
        private readonly RecordingSchedule schedule = new();
        public event Action<SealProfile> WhenSealRecognized;

        private void Start()
        {
            // A HandRef may point to a synthetic hand. Require the concrete, unmodified tracking Hand.
            if (xrHandsSource)
            {
                if (!xrHandsSource.IsConfigured) { Status = "通用手追踪来源未配置，请绑定直接追踪的左右手组件"; enabled = false; return; }
            }
            else if (!RawTrackingHand(leftTrackedHand) || !RawTrackingHand(rightTrackedHand))
            { Status = "请绑定左右手实际追踪组件，不要绑定合成手或手部引用组件"; enabled = false; return; }
            if (!xrHandsSource) { left = (IHand)leftTrackedHand; right = (IHand)rightTrackedHand; }
            if (!xrHandsSource && (left.Handedness != Handedness.Left || right.Handedness != Handedness.Right))
            { Status = "左右手追踪来源绑定反了"; enabled = false; return; }
            if (!recordingOrigin) recordingOrigin = transform;
            if ((recordingOrigin.lossyScale - Vector3.one).sqrMagnitude > 0.00001f)
            { Status = "骨骼录制原点的缩放必须为（1，1，1）"; enabled = false; return; }
            Status = "实时追踪已启动，可直接录制；识别需要已确认的模板";
            if (recordOnStart) StartRecording();
        }
        private static bool RawTrackingHand(MonoBehaviour source) => source && source.GetType() == typeof(Hand) &&
            source.GetComponent<FromOVRHandDataSource>() != null;

        private void LateUpdate()
        {
            double now = Time.realtimeSinceStartupAsDouble;
            bool justStarted = AdvanceRecording(now);
            bool forced = forceEvaluation;
            if (now < nextSample && !justStarted && !forced) return;
            forceEvaluation = false;
            nextSample = now + 1.0 / maximumSampleRate;
            HandSnapshot xrLeft = null, xrRight = null;
            bool changedLeft, changedRight;
            if (xrHandsSource)
            {
                xrLeft = xrHandsSource.Capture(true, recordingOrigin, now, staleAfterSeconds);
                xrRight = xrHandsSource.Capture(false, recordingOrigin, now, staleAfterSeconds);
                changedLeft = xrLeft.version != leftVersion; changedRight = xrRight.version != rightVersion;
                leftVersion = xrLeft.version; rightVersion = xrRight.version;
            }
            else
            {
                changedLeft = Observe(left, ref leftVersion, ref leftObserved, now);
                changedRight = Observe(right, ref rightVersion, ref rightObserved, now);
            }
            // State transitions and staleness still produce snapshots; duplicates are explicitly versioned.
            bool staleTransition = xrHandsSource ? Frame != null &&
                (Frame.left.fresh != xrLeft.fresh || Frame.right.fresh != xrRight.fresh ||
                 Frame.left.tracked != xrLeft.tracked || Frame.right.tracked != xrRight.tracked ||
                 Frame.left.connected != xrLeft.connected || Frame.right.connected != xrRight.connected) :
                Frame != null && (Frame.left.fresh != (now - leftObserved <= staleAfterSeconds) ||
                Frame.right.fresh != (now - rightObserved <= staleAfterSeconds) || Frame.left.tracked != left.IsTrackedDataValid ||
                Frame.right.tracked != right.IsTrackedDataValid || Frame.left.connected != left.IsConnected || Frame.right.connected != right.IsConnected);
            if (!changedLeft && !changedRight && !staleTransition && !justStarted && !forced && Frame != null) return;
            Frame = new SealFrame { time = now, label = IsRecording ? recordingLabel : sampleLabel, source = TrackingSource,
                left = xrLeft ?? CaptureHand(left, recordingOrigin, leftObserved, now, staleAfterSeconds),
                right = xrRight ?? CaptureHand(right, recordingOrigin, rightObserved, now, staleAfterSeconds) };
            extractor.Extract(Frame, Features);
            Evaluate(now);
            if (IsRecording)
            {
                try { recorder.Write(Frame); }
                catch (Exception ex) { schedule.Cancel(); recorder.Dispose(); Status = "录制失败：" + ex.Message; Debug.LogException(ex, this); }
            }
        }
        private static bool Observe(IHand hand, ref int previous, ref double observed, double now)
        {
            int version = hand.CurrentDataVersion;
            if (version == previous) return false;
            previous = version; observed = now; return true;
        }
        public static HandSnapshot CaptureHand(IHand hand, Transform recordingOrigin, double observed, double now, double staleAfterSeconds)
        {
            var snapshot = new HandSnapshot { connected = hand.IsConnected, tracked = hand.IsTrackedDataValid,
                highConfidence = hand.IsHighConfidence, version = hand.CurrentDataVersion, observedAt = observed,
                fresh = now - observed <= staleAfterSeconds, scale = hand.Scale };
            if (!float.IsFinite(snapshot.scale) || snapshot.scale <= 0) snapshot.scale = 1;
            if (!snapshot.connected || !snapshot.tracked || !snapshot.fresh) return snapshot;
            Quaternion inverse = Quaternion.Inverse(recordingOrigin.rotation);
            for (int i = 0; i < snapshot.joints.Length; ++i)
            {
                if (!hand.GetJointPose((HandJointId)i, out var pose) || !FinitePose(pose)) continue;
                snapshot.jointValid[i] = true;
                snapshot.joints[i] = new Pose(recordingOrigin.InverseTransformPoint(pose.position), inverse * pose.rotation);
            }
            snapshot.wristPosesValid = hand.GetJointPosesFromWrist(out var poses);
            if (snapshot.wristPosesValid)
                for (int i = 0; i < snapshot.wristPoses.Length; ++i)
                {
                    snapshot.wristPoses[i] = poses[i];
                    snapshot.wristPosesValid &= FinitePose(poses[i]);
                }
            for (int f = 0; f < 5; ++f) snapshot.fingerConfidence[f] = hand.GetFingerIsHighConfidence((HandFinger)f);
            return snapshot;
        }
        public static bool FinitePose(Pose pose)
        {
            Vector3 p = pose.position; Quaternion q = pose.rotation;
            return float.IsFinite(p.x) && float.IsFinite(p.y) && float.IsFinite(p.z) &&
                   float.IsFinite(q.x) && float.IsFinite(q.y) && float.IsFinite(q.z) && float.IsFinite(q.w) &&
                   q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w > 0.0001f;
        }
        private void Evaluate(double now)
        {
            var previous = Recognized;
            bool bothHands = Frame.left.connected && Frame.left.tracked && Frame.left.fresh &&
                Frame.right.connected && Frame.right.tracked && Frame.right.fresh &&
                Math.Abs(Frame.left.observedAt - Frame.right.observedAt) <= SealFeatureExtractor.MaximumHandSkew;
            recognition.Update(profiles, Features, now, bothHands, minimumScoreMargin);
            if (Recognized && Recognized != previous) WhenSealRecognized?.Invoke(Recognized);
        }
        public void SetTestMode(bool active)
        {
            StopRecording(); testMode = active; recognition.Reset();
            // Re-evaluate the existing data without resetting its observed time/freshness.
            forceEvaluation = true;
        }
        public void StartRecording()
        {
            if (!Application.isPlaying || !enabled || (!xrHandsSource && left == null) || IsRecording || IsPreparing || IsTesting) return;
            try
            {
                schedule.Arm(Time.realtimeSinceStartupAsDouble, preparationSeconds, Math.Min(captureSeconds, recordingLimitSeconds));
                recordingLabel = sampleLabel;
                Status = "请摆好手印，倒计时结束后自动开始、自动停止录制";
            }
            catch (ArgumentException ex) { Status = "录制设置无效：" + ex.Message; }
        }
        private bool AdvanceRecording(double now)
        {
            var previous = schedule.Phase; var current = schedule.Advance(now);
            if (previous == RecordingPhase.Preparing && current == RecordingPhase.Capturing)
            {
                try { recorder.Start(RecordingDirectory); Status = "正在录制，请保持手印"; return true; }
                catch (Exception ex) { schedule.Cancel(); Status = "无法开始录制：" + ex.Message; Debug.LogException(ex, this); }
            }
            else if (previous == RecordingPhase.Preparing && current == RecordingPhase.Idle)
                Status = "已跳过录制：应用未能在预定时间开始采集";
            else if (previous == RecordingPhase.Capturing && current == RecordingPhase.Idle) StopRecording();
            return false;
        }
        public void StopRecording()
        {
            bool preparing = IsPreparing, recording = IsRecording;
            schedule.Cancel(); recorder.Dispose();
            if (preparing) Status = "已取消准备，未创建录制文件";
            else if (recording) Status = $"录制完成，已保存 {RecordedFrames} 帧原始骨骼快照";
        }
        private void OnDisable() { StopRecording(); recognition.Reset(); }
        private void OnApplicationPause(bool paused) { if (paused) { StopRecording(); recognition.Reset(); Status = "应用已暂停，录制已取消或停止保存"; } }
    }
}
