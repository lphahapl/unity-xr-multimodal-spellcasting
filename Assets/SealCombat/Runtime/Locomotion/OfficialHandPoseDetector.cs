using System;
using System.Collections.Generic;
using NarutoVR.SealLab;
using Oculus.Interaction.Input;
using Oculus.Interaction.PoseDetection;

namespace NarutoVR.SealCombat
{
    /// <summary>
    /// Uses the actual Meta FeatureStateProvider (also used by FingerFeatureStateProvider),
    /// fed by the existing SDK FingerShapes snapshot extractor. No copied threshold algorithm,
    /// render-hand poses, native runtime activation, or changes to recorded Curl units.
    /// </summary>
    public sealed class OfficialHandPoseDetector
    {
        public readonly struct Result
        {
            public readonly bool pointing, open, closed, valid, releaseOpen;
            public readonly string detail;
            public Result(bool pointing, bool open, string detail, bool closed = false, bool valid = false, bool releaseOpen = false)
            { this.pointing = pointing; this.open = open; this.detail = detail; this.closed = closed; this.valid = valid; this.releaseOpen = valid && (open || releaseOpen); }
        }

        sealed class HandState
        {
            readonly FeatureStateProvider<FingerFeature, string>[] providers = new FeatureStateProvider<FingerFeature, string>[4];
            readonly float[] curls = new float[4];
            readonly string[] states = new string[4];
            readonly string[] ids;
            readonly BasicHandPoseSettings settings;
            double last = double.NegativeInfinity, epoch;
            float clock;
            int sequence;
            bool prepared;
            Result cached;
            public HandState(string prefix, BasicHandPoseSettings settings)
            {
                this.settings = settings;
                // Match SealFeatureExtractor exactly. In SDK 207 Pinky and Max alias value 4;
                // Enum.ToString emits Max on this runtime. Never rename recorded feature IDs.
                ids = new string[4];
                for (int i = 0; i < 4; i++) ids[i] = $"{prefix}.{(HandFinger)(i + 1)}.Curl";
                for (int i = 0; i < 4; i++)
                {
                    int slot = i;
                    providers[i] = new FeatureStateProvider<FingerFeature, string>(f => f == FingerFeature.Curl ? curls[slot] : (float?)null, f => (int)f, () => clock);
                }
                Reset("等待有效骨骼");
            }
            public void Reset(string reason)
            {
                if (!prepared || sequence != 0)
                    for (int i = 0; i < 4; i++) { providers[i].InitializeThresholds(settings.ForFinger(i + 1)); providers[i].LastUpdatedFrameId = 0; states[i] = null; }
                prepared = true; last = double.NegativeInfinity; sequence = 0; clock = 0;
                cached = new Result(false, false, reason);
            }
            public Result Update(HandSnapshot hand, IReadOnlyList<SealFeature> features, double now, double maxAge, double maxGap)
            {
                if (hand == null || !hand.connected || !hand.tracked || !hand.fresh || !hand.highConfidence || !hand.wristPosesValid ||
                    !double.IsFinite(now) || !double.IsFinite(hand.observedAt) || now < hand.observedAt || now - hand.observedAt > maxAge)
                { Reset("追踪无效或过期"); return cached; }
                for (int i = 0; i < 4; i++)
                {
                    bool found = false;
                    if (features != null) foreach (var f in features)
                        if (f.id == ids[i])
                        {
                            found = f.valid && f.highConfidence && float.IsFinite(f.value) && f.value >= 0 && f.value <= 360;
                            curls[i] = f.value; break;
                        }
                    if (!found) { Reset("特征失效：" + FingerName(i)); return cached; }
                }
                if (hand.observedAt < last) { Reset("观测时间倒退，重新确认"); return cached; }
                if (hand.observedAt == last) return cached; // Polling/render frames are not new tracking evidence.
                if (hand.observedAt - last > maxGap && double.IsFinite(last)) Reset("数据间隔过长，重新确认");
                if (!double.IsFinite(last)) epoch = hand.observedAt;
                clock = (float)(hand.observedAt - epoch); last = hand.observedAt; sequence++;
                bool pointing = true, open = true, closed = true, valid = true;
                int opened=0, folded=0;
                for (int i = 0; i < 4; i++)
                {
                    providers[i].LastUpdatedFrameId = sequence;
                    states[i] = providers[i].GetCurrentFeatureState(FingerFeature.Curl);
                    open &= states[i] == "0";
                    closed &= states[i] == "2";
                    valid &= states[i] != null;
                    pointing &= states[i] == (i == 0 ? "0" : "2");
                    if(states[i]=="0") opened++;
                    if(states[i]=="2") folded++;
                }
                cached = new Result(pointing, open, $"食{StateName(states[0])} 中{StateName(states[1])} 无{StateName(states[2])} 小{StateName(states[3])}", closed, valid, opened>=3 && folded==0);
                return cached;
            }
        }

        readonly HandState left, right;
        string source;
        public Result Left { get; private set; }
        public Result Right { get; private set; }
        public string Status => $"左：{(Left.pointing ? "指向通过" : "指向未通过")} [{Left.detail}]\n右：{(Right.open ? "张掌通过" : "张掌未通过")} [{Right.detail}]";
        public OfficialHandPoseDetector(BasicHandPoseSettings settings)
        {
            if (!settings) throw new ArgumentNullException(nameof(settings));
            string error = settings.Validate(); if (error != null) throw new ArgumentException(error, nameof(settings));
            left = new HandState("L", settings); right = new HandState("R", settings); Reset();
        }
        public void Reset()
        {
            left.Reset("等待重新确认"); right.Reset("等待重新确认");
            Left = Right = new Result(false, false, "等待重新确认"); source = null;
        }
        public void Update(SealFrame frame, IReadOnlyList<SealFeature> features, double now, double maxAge, double maxGap)
        {
            if (frame == null || !double.IsFinite(maxAge) || !double.IsFinite(maxGap) || maxAge <= 0 || maxGap <= 0) { Reset(); return; }
            if (source != frame.source) { Reset(); source = frame.source; }
            Left = left.Update(frame.left, features, now, maxAge, maxGap);
            Right = right.Update(frame.right, features, now, maxAge, maxGap);
        }
        static string FingerName(int i) => i == 0 ? "食指" : i == 1 ? "中指" : i == 2 ? "无名指" : "小指";
        static string StateName(string state) => state == "0" ? "伸直" : state == "1" ? "半弯" : state == "2" ? "收拢" : "确认中";
    }
}
