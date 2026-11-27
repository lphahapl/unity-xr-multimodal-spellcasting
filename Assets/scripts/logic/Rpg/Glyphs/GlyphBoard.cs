using System;
using System.Collections.Generic;
using UnityEngine;

namespace NarutoVR.SealCombat.Rpg
{
    /// <summary>画板保持场景提供的位姿；以局部 XY 记录每笔，Z 只用于接触与离板。</summary>
    public sealed class GlyphBoard : MonoBehaviour
    {
        public readonly struct NibSample
        {
            public readonly Vector3 boardPosition;
            public readonly double time;
            public readonly int strokeIndex;

            internal NibSample(Vector3 boardPosition, double time, int strokeIndex)
            {
                this.boardPosition = boardPosition;
                this.time = time;
                this.strokeIndex = strokeIndex;
            }
        }

        public const float HalfWidth = .18f;
        public const float HalfHeight = .15f;
        internal const float ButtonY = -.179f;
        internal const float ButtonSpacing = .119f;
        internal const float ButtonHalfWidth = .051f;
        internal const float ButtonHalfHeight = .015f;

        [Min(.1f), InspectorName("停笔自动辨符（秒）")] public float idleSeconds = .9f;
        [Min(.1f), InspectorName("剧情吸附与金闪总时长（秒）")] public float successDuration = .65f;
        [Min(.001f), InspectorName("落笔距离（米）")] public float contactDistance = .009f;
        [Min(.002f), InspectorName("离板距离（米）")] public float releaseDistance = .019f;
        [Min(.0005f), InspectorName("最小采点间隔（米）")] public float pointSpacing = .0025f;
        [Range(0f, .3f), InspectorName("不同符形的最低得分差")] public float minimumMargin = .055f;
        const int MaxPoints = 3072;
        const int MaxStrokes = 64;
        const float NibHintDepth = .06f;

        readonly List<List<Vector2>> ink = new();
        readonly List<NibSample> samples = new();
        GlyphTemplate[] candidates = Array.Empty<GlyphTemplate>();
        GlyphBoardView view;
        List<Vector2> stroke;
        double lastInk, lostAt;
        int revision, evaluatedRevision;
        bool story, tracking, buttonPressed, atCapacity;
        double successStarted;
        GlyphTemplate completionAtSnap;
#if UNITY_EDITOR
        internal readonly GlyphRejectionCapture.ContactLog contactLog = new();
#endif
        public bool IsShowingSuccess { get; private set; }
        public bool IsOpen { get; private set; }
        public IReadOnlyList<NibSample> Samples => samples;
        public int PointCount => samples.Count;
        internal bool HasUnsubmittedInk => IsOpen && PointCount > 0 && revision != evaluatedRevision && !IsShowingSuccess;
        public int StrokeCount => ink.Count;
        public string Status { get; private set; }
        public float LastScore { get; private set; }
        internal bool ContainsSurface(Vector3 localTip) => view != null && view.ContainsBoardPoint(localTip);

        public void Open(GlyphTemplate[] candidates, bool story)
        {
            this.candidates = candidates ?? Array.Empty<GlyphTemplate>();
            this.story = story;
            IsShowingSuccess = false;
            completionAtSnap = null;
            IsOpen = true;
            tracking = false;
            lostAt = double.NaN;
            if (view == null) view = new GlyphBoardView(transform, ink, samples);
            view.Open(story && this.candidates.Length > 0 ? this.candidates[0] : null, story);
            ClearInk();
            SetStatus(view.StepFeedback ?? (story ? "照范本画符，停笔后辨符" : "笔尖触板书写，停笔后辨符"));
        }

        public void Close()
        {
            IsOpen = false;
            IsShowingSuccess = false;
            completionAtSnap = null;
            tracking = false;
            ClearInk();
            view?.Hide();
        }

        void OnDisable() => Close();

        /// <summary>now 使用单调时钟。无效观测应显式传 valid=false；失追不会自动提交或连成一笔。</summary>
        public void Sample(Vector3 worldTip, bool valid, double now) => Sample(worldTip, valid, now, true);

        internal void Sample(Vector3 worldTip, bool valid, double now, bool contactAllowed)
        {
            if (!IsOpen || IsShowingSuccess || !double.IsFinite(now)) return;
            if (!valid || !PlayerHandObservation.Finite(worldTip))
            {
                if (tracking)
                {
                    lostAt = now;
#if UNITY_EDITOR
                    contactLog.LostTracking(now);
#endif
                }
                tracking = false;
                EndStroke();
                buttonPressed = false;
                view.ClearPointer();
                return;
            }
            if (!tracking && double.IsFinite(lostAt) && PointCount > 0)
                lastInk += Math.Max(0, now - lostAt);
            tracking = true;
            lostAt = double.NaN;

            Vector3 local = transform.InverseTransformPoint(worldTip);
            float depth = Mathf.Abs(local.z);
            bool writingArea = Mathf.Abs(local.x) <= HalfWidth && Mathf.Abs(local.y) <= HalfHeight;
            int button = writingArea ? -1 : ButtonAt(local);
            if (writingArea && depth <= NibHintDepth)
                view.SetNib(local, Mathf.InverseLerp(NibHintDepth, contactDistance, depth), contactAllowed && depth <= contactDistance);
            else if (contactAllowed && button >= 0 && depth <= Mathf.Max(contactDistance, releaseDistance))
                view.ShowButton(button, depth <= contactDistance);
            else
                view.ClearPointer();
            if (!contactAllowed || depth > Mathf.Max(contactDistance, releaseDistance))
            {
                EndStroke();
                buttonPressed = false;
                return;
            }
            if (!writingArea)
            {
                EndStroke();
                if (button >= 0 && depth <= contactDistance)
                {
                    if (!buttonPressed)
                    {
                        if (button == 0) Undo(now);
                        else if (button == 1) { ClearInk(); SetStatus("已清空"); }
                    }
                    buttonPressed = true;
                }
                else if (button < 0) buttonPressed = false;
                return;
            }
            buttonPressed = false;
            if (stroke == null && depth > contactDistance) return;
            Vector2 point = new Vector2(local.x, local.y);
            if (stroke != null && (stroke[stroke.Count - 1] - point).sqrMagnitude < pointSpacing * pointSpacing)
                return;
            if (PointCount >= MaxPoints || (stroke == null && ink.Count >= MaxStrokes))
            {
                atCapacity = true;
                SetStatus("笔迹已满，请撤销或清空");
                return;
            }
            if (stroke == null)
            {
                stroke = new List<Vector2>();
                ink.Add(stroke);
            }
            stroke.Add(point);
            samples.Add(new NibSample(local, now, ink.Count - 1));
            revision++;
            lastInk = now;
            SetStatus(view.StepFeedback != null ? "笔尖已着墨，换笔时向身前抬离约两厘米" : story ? "书写中，停笔后辨符" : "书写中");
            view.InkChanged();
        }

        void EndStroke()
        {
            if (stroke == null) return;
            stroke = null;
            view.ReadSteps();
            if (view.StepFeedback != null && evaluatedRevision != revision) SetStatus(view.StepFeedback);
        }

        /// <summary>返回 true 表示这一版墨迹已评判。拒识时 match 为空，可撤销或清空后重画。</summary>
        public bool TrySubmit(double now, out GlyphTemplate match)
        {
            match = null;
            if (!IsOpen || IsShowingSuccess || !tracking || !double.IsFinite(now) || atCapacity || PointCount == 0 || evaluatedRevision == revision)
                return false;
            if (now - lastInk < Mathf.Max(.1f, idleSeconds)) return false;
            evaluatedRevision = revision;
            view.ReadSteps();

            var lines = new List<Vector2[]>(ink.Count);
            float length = 0;
            foreach (var line in ink)
            {
                if (line.Count < 2) continue;
                lines.Add(line.ToArray());
                for (int i = 1; i < line.Count; i++) length += Vector2.Distance(line[i - 1], line[i]);
            }
            LastScore = 0;
            if (PointCount < 3 || length < .035f)
            {
                SetStatus("笔迹太短，请继续画");
#if UNITY_EDITOR
                if (Application.isPlaying) GlyphRejectionCapture.Write(this, story, now, candidates, null);
#endif
                return true;
            }

            GlyphTemplate best = null;
            float runnerUp = -1;
            // 多个样本或多个同 ID 资源均只占一个候选类别。
            var ranked = new Dictionary<string, (float score, GlyphTemplate shape)>();
#if UNITY_EDITOR
            float[] scores = Application.isPlaying ? new float[candidates.Length] : null;
#endif
            for (int i = 0; i < candidates.Length; ++i)
            {
                var candidate = candidates[i];
                if (!candidate) continue;
                float score = candidate.Score(lines);
#if UNITY_EDITOR
                if (scores != null) scores[i] = score;
#endif
                if (!ranked.TryGetValue(candidate.SymbolKey, out var prior) || score > prior.score)
                    ranked[candidate.SymbolKey] = (score, candidate);
            }
            foreach (var entry in ranked.Values)
            {
                if (!best || entry.score > LastScore)
                {
                    if (best) runnerUp = LastScore;
                    best = entry.shape;
                    LastScore = entry.score;
                }
                else runnerUp = Mathf.Max(runnerUp, entry.score);
            }
            if (!best || LastScore < best.threshold) SetStatus(view.StepRejection ?? (story ? "符形不符，请照范本重画" : "符形未匹配，请修改或清空"));
            else if (!story && runnerUp >= 0 && LastScore - runnerUp < minimumMargin)
                SetStatus("两种符形相近，请重画");
            else { match = best; SetStatus("已识别：" + best.Title); }
#if UNITY_EDITOR
            if (!match && scores != null) GlyphRejectionCapture.Write(this, story, now, candidates, scores);
#endif
            return true;
        }

        internal void BeginStorySuccess(double now, GlyphTemplate matched = null)
        {
            if (!story || !IsOpen || IsShowingSuccess || !double.IsFinite(now)) return;
            IsShowingSuccess = true;
            view.ClearPointer();
            successStarted = now;
            completionAtSnap = matched;
            SetStatus("符成");
            view.SetStorySuccessProgress(0f, 0f);
        }

        internal bool AdvanceStorySuccess(double now)
        {
            if (!IsShowingSuccess || !double.IsFinite(now)) return false;
            float progress = Mathf.Clamp01((float)((now - successStarted) / Mathf.Max(.1f, successDuration)));
            float snap = Mathf.SmoothStep(0f, 1f, progress * 2f);
            float flash = Mathf.Sin(Mathf.PI * Mathf.Clamp01(progress * 2f - 1f));
            view.SetStorySuccessProgress(snap, Mathf.Max(0f, flash));
            if (progress >= .5f && completionAtSnap)
            {
                var completed = completionAtSnap;
                completionAtSnap = null;
                completed.RaiseCompleted(this);
            }
            return IsShowingSuccess && progress >= 1f;
        }

        static int ButtonAt(Vector3 local)
        {
            if (Mathf.Abs(local.y - ButtonY) > ButtonHalfHeight) return -1;
            for (int i = 0; i < 2; i++)
                if (Mathf.Abs(local.x - (i - 1) * ButtonSpacing) <= ButtonHalfWidth) return i;
            return -1;
        }

        void Undo(double now)
        {
            if (ink.Count == 0) return;
            var line = ink[ink.Count - 1];
            samples.RemoveRange(samples.Count - line.Count, line.Count);
            ink.RemoveAt(ink.Count - 1);
            stroke = null;
            atCapacity = false;
            revision++;
            lastInk = now;
            view.ReadSteps();
            SetStatus("已撤销上一笔");
            view.InkChanged();
        }

        void ClearInk()
        {
            stroke = null;
            ink.Clear();
#if UNITY_EDITOR
            contactLog.Clear();
#endif
            samples.Clear();
            revision = evaluatedRevision = 0;
            buttonPressed = atCapacity = false;
            LastScore = 0;
            view?.InkChanged();
            view?.ReadSteps();
        }

        void SetStatus(string value)
        {
            if (value == Status) return;
            Status = value;
            view?.SetStatus(value);
        }
    }
}
