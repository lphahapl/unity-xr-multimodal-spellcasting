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

        public void Reset()
        {
            Candidate = Recognized = null; HoldProgress = 0; State = SealRecognitionState.NoTracking;
            Scores.Clear();
        }
        public void Update(IReadOnlyList<SealProfile> profiles, IReadOnlyList<SealFeature> features,
            double now, bool bothHandsAvailable, float minimumMargin)
        {
            Scores.Clear(); int enabledCount = 0;
            foreach (var profile in profiles)
            {
                Scores.Add(SealScorer.Evaluate(profile, features));
                if (profile && profile.ValidateRules() == null) enabledCount++;
            }
            Scores.Sort((a, b) => b.score.CompareTo(a.score));
            if (!bothHandsAvailable) { Reject(SealRecognitionState.NoTracking); return; }
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
            if (Candidate != best.profile || now < candidateSince)
            { Candidate = best.profile; candidateSince = now; Recognized = null; }
            HoldProgress = Candidate.holdSeconds <= 0 ? 1 : Mathf.Clamp01((float)((now - candidateSince) / Candidate.holdSeconds));
            if (HoldProgress >= 1) { Recognized = Candidate; State = SealRecognitionState.Recognized; }
            else { Recognized = null; State = SealRecognitionState.Holding; }
        }
        private void Reject(SealRecognitionState state)
        { Candidate = Recognized = null; HoldProgress = 0; State = state; }

        public static string StateText(SealRecognitionState state) => state switch
        {
            SealRecognitionState.NoTracking => "等待双手有效追踪",
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
