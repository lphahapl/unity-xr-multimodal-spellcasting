using System;
using System.Collections.Generic;
using UnityEngine;

namespace NarutoVR.SealLab
{
    public enum SealRecognitionState { NoTracking, NoTemplates, Unknown, Ambiguous, Holding, Recognized }

    public sealed class SealRecognizer
    {
        public readonly List<SealScore> Scores = new();
        public SealProfile Candidate { get; private set; }
        public SealProfile Recognized { get; private set; }
        public SealRecognitionState State { get; private set; } = SealRecognitionState.NoTracking;
        public float HoldProgress { get; private set; }
        private double candidateSince;
        private double lastLeftTime = double.NegativeInfinity;
        private int lastLeftVersion;

        public void Reset()
        {
            Candidate = Recognized = null; HoldProgress = 0; State = SealRecognitionState.NoTracking;
            lastLeftTime = double.NegativeInfinity;
            Scores.Clear();
        }
        public void Update(IReadOnlyList<SealProfile> profiles, IReadOnlyList<SealFeature> features,
            double now, bool bothHandsAvailable, float minimumMargin, HandSnapshot left = null, double staleAfterSeconds = SealLab.DefaultStaleAfterSeconds)
        {
            bool leftReady = SealLab.IsCurrentHand(left, now, staleAfterSeconds);
            bool newLeft = leftReady && left.observedAt > lastLeftTime && (double.IsNegativeInfinity(lastLeftTime) || left.version != lastLeftVersion);
            if (newLeft)
            {
                if (Candidate && Candidate.handMode == SealHandMode.LeftOnly && left.observedAt - lastLeftTime > staleAfterSeconds)
                    Reject(SealRecognitionState.NoTracking);
                lastLeftTime = left.observedAt;
                lastLeftVersion = left.version;
            }
            else if (leftReady)
                leftReady = left.version == lastLeftVersion && left.observedAt == lastLeftTime;

            Scores.Clear(); int enabledCount = 0;
            bool trackingAvailable = bothHandsAvailable;
            foreach (var profile in profiles)
            {
                var score = SealScorer.Evaluate(profile, features);
                Scores.Add(score);
                if (!profile || profile.ValidateRules() != null) continue;
                enabledCount++;
                bool available = profile.handMode == SealHandMode.LeftOnly
                    ? leftReady && (left.highConfidence || profile.allowLowConfidence) : bothHandsAvailable;
                trackingAvailable |= available;
                if (!available)
                {
                    score.passes = false;
                    score.reason = profile.handMode == SealHandMode.LeftOnly ? "等待有效的左手观测" : "等待同步的双手观测";
                }
            }
            Scores.Sort((a, b) => b.score.CompareTo(a.score));
            if (!trackingAvailable) { Reject(SealRecognitionState.NoTracking); return; }
            if (enabledCount == 0) { Reject(SealRecognitionState.NoTemplates); return; }
            SealScore best = null, second = null;
            foreach (var score in Scores)
            {
                if (!score.passes) continue;
                if (best == null) best = score;
                else { second = score; break; }
            }
            if (best == null) { Reject(SealRecognitionState.Unknown); return; }
            if (second != null && best.score - second.score < Mathf.Clamp01(minimumMargin))
            { Reject(SealRecognitionState.Ambiguous); return; }
            if (best.profile.handMode == SealHandMode.LeftOnly && !newLeft && Candidate != best.profile)
            { Reject(SealRecognitionState.Unknown); return; }
            double observed = best.profile.handMode == SealHandMode.LeftOnly ? lastLeftTime : now;
            if (Candidate != best.profile || observed < candidateSince)
            { Candidate = best.profile; candidateSince = observed; Recognized = null; }
            HoldProgress = Candidate.holdSeconds <= 0 ? 1 : Mathf.Clamp01((float)((observed - candidateSince) / Candidate.holdSeconds));
            if (HoldProgress >= 1) { Recognized = Candidate; State = SealRecognitionState.Recognized; }
            else { Recognized = null; State = SealRecognitionState.Holding; }
        }
        private void Reject(SealRecognitionState state)
        { Candidate = Recognized = null; HoldProgress = 0; State = state; }

        public static string StateText(SealRecognitionState state) => state switch
        {
            SealRecognitionState.NoTracking => "等待所需手部的有效追踪",
            SealRecognitionState.NoTemplates => "没有启用的模板，请先生成并确认模板",
            SealRecognitionState.Unknown => "未知手势：没有模板达到判定条件",
            SealRecognitionState.Ambiguous => "无法区分：两个达标印的得分过于接近",
            SealRecognitionState.Holding => "候选达标，等待保持时间",
            SealRecognitionState.Recognized => "已识别",
            _ => "未知状态"
        };

        public static string HudName(SealProfile profile) => profile ? profile.sealName : "未识别";
    }
}
