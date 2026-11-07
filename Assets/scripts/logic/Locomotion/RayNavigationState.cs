using System;
using UnityEngine;

namespace NarutoVR.SealCombat.Rpg
{
    [Serializable]
    public sealed class RayNavigationSettings
    {
        [Header("左手射线选点（无需移动食指）")]
        [InspectorName("有效指向准备时间（秒）"), Min(.02f)] public float readySeconds = .12f;
        [InspectorName("同目标驻留时间（秒）"), Min(.05f)] public float dwellSeconds = .32f;
        [InspectorName("移开或放松后重备时间（秒）"), Min(.05f)] public float releaseSeconds = .12f;
        [InspectorName("节点射线命中半径（米）"), Min(.05f)] public float hitRadius = .35f;
        [InspectorName("换点评分滞后"), Range(0,.2f)] public float switchAdvantage = .04f;
        [Header("战斗选点（头显主选，手指微调）")]
        [InspectorName("头显视线权重"), Min(0)] public float combatGazeWeight = .60f;
        [InspectorName("左食指方向权重"), Min(0)] public float combatHandWeight = .30f;
        [InspectorName("近距离权重"), Min(0)] public float combatDistanceWeight = .10f;
        [InspectorName("候选半角（度）"), Range(5,85), Tooltip("节点在头显或手指任一有效方向的半角内即可入围。")]
        public float combatCandidateAngle = 50;
        public bool CombatScoringValid => Nonnegative(combatGazeWeight) && Nonnegative(combatHandWeight) &&
            Nonnegative(combatDistanceWeight) && Positive(combatGazeWeight + combatHandWeight) &&
            float.IsFinite(combatGazeWeight + combatHandWeight + combatDistanceWeight) &&
            float.IsFinite(combatCandidateAngle) && combatCandidateAngle >= 5 && combatCandidateAngle <= 85;
        public bool Valid => Positive(readySeconds) && Positive(dwellSeconds) && Positive(releaseSeconds) &&
            Positive(hitRadius) && float.IsFinite(switchAdvantage) && switchAdvantage >= 0 && switchAdvantage <= .2f;
        static bool Positive(float value) => float.IsFinite(value) && value > 0;
        static bool Nonnegative(float value) => float.IsFinite(value) && value >= 0;
    }

    /// <summary>Only distinct fresh hand samples advance dwell. Loss cancels progress, never rearms a spent gesture.</summary>
    public sealed class RayNavigationState
    {
        object target;
        double lastObserved = double.NegativeInfinity, readySince = double.NaN, dwellSince = double.NaN, releaseSince = double.NaN;
        public bool Active { get; private set; }
        public bool NeedsRelease { get; private set; }
        public float Progress { get; private set; }
        public string Status { get; private set; } = "等待左手指向落点";
        public void Reset()
        {
            Suspend(); lastObserved = double.NegativeInfinity; NeedsRelease = false;
        }
        public void Suspend()
        {
            target = null; readySince = dwellSince = releaseSince = double.NaN;
            Active = false; Progress = 0;
        }
        public bool Process(object candidate, bool observationValid, bool release, double stamp, double now,
            RayNavigationSettings config, RpgGestureSettings timing)
        {
            if (config == null || !config.Valid || timing == null || !observationValid || !double.IsFinite(stamp) ||
                !double.IsFinite(now) || stamp > now + .01 || now - stamp > timing.sampleMaxAge)
            { Suspend(); Status = "输入失效或被占用：驻留清空"; return false; }
            if (stamp < lastObserved) { Suspend(); Status = "样本时间倒退：等待新观测"; return false; }
            if (stamp == lastObserved)
            {
                if (!ReferenceEquals(candidate, target)) Suspend();
                return false;
            }
            if (stamp - lastObserved > timing.sampleMaxGap) Suspend();
            lastObserved = stamp;
            if (release)
            {
                target = null; Active = false; Progress = 0; readySince = dwellSince = double.NaN;
                if (double.IsNaN(releaseSince)) releaseSince = stamp;
                if (stamp - releaseSince >= config.releaseSeconds) NeedsRelease = false;
                Status = NeedsRelease ? "移开或放松左手以重新准备" : "等待指向有效落点";
                return false;
            }
            releaseSince = double.NaN;
            if (NeedsRelease) { Suspend(); Status = "本次已执行：请移开射线或放松左手"; return false; }
            if (candidate == null) { Suspend(); Status = "射线未命中可达落点"; return false; }
            if (!ReferenceEquals(candidate, target))
            {
                target = candidate; readySince = stamp; dwellSince = double.NaN; Progress = 0; Active = false;
            }
            if (stamp - readySince < config.readySeconds) { Status = "指向准备中"; return false; }
            Active = true;
            if (double.IsNaN(dwellSince)) dwellSince = stamp;
            Progress = Mathf.Clamp01((float)((stamp - dwellSince) / config.dwellSeconds));
            Status = "同目标驻留确认";
            if (Progress < 1) return false;
            NeedsRelease = true; Active = false; Progress = 0; target = null;
            Status = "已确认：等待移开射线或放松左手";
            return true;
        }
    }
}
