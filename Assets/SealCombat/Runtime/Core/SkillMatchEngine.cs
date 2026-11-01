using System;
using System.Collections.Generic;
using System.Text;
using NarutoVR.SealLab;
using UnityEngine;

namespace NarutoVR.SealCombat
{
    /// <summary>Single-thread input matcher. Call Tick once before the first input. No effect or Unity scene ownership.</summary>
    public sealed class SkillMatchEngine
    {
        public Func<SkillDefinition, bool> TryActivate;
        /// <summary>The RPG session owns group cooldowns; legacy scenes retain their existing local cooldowns.</summary>
        public bool ExternalCooldownAuthority { get; set; }
        public Action<string> Log;
        public string Status { get; private set; } = "等待技能输入";
        public string ObservationDiagnostics { get; private set; } = "等待骨骼输入";
        double nextObservationDiagnostic;
        public string PendingSkillName => pending < 0 ? "" : Name(recipes[pending].Definition);
        public string PendingSkillId => pending < 0 ? "" : recipes[pending].Id;
        public SkillDefinition PendingSkill => pending < 0 ? null : recipes[pending].Definition;
        public double HistoryLimitSeconds => HistorySeconds;
        public string CancelPhrase { get; }
        public double PendingDeadline => pending < 0 ? 0 : voiceEnd + recipes[pending].Window;
        public int BufferCount => bufferCount;
        public double BufferSeconds => bufferCount < 2 ? 0 : slots[(bufferHead + bufferCount - 1) % Capacity].Frame.time - slots[bufferHead].Frame.time;
        public long Epoch { get; private set; }
        public IReadOnlyList<SkillDefinition> Skills { get; }
        public IReadOnlyList<SkillRecipeInfo> ActiveRecipes { get; }

        private readonly int Capacity;
        private readonly double HistorySeconds, MaximumVoiceLatency, ExitSeconds, MaximumSampleGap;
        private const double Epsilon = 0.000001;
        private readonly float Margin;
        private readonly Recipe[] recipes;
        private readonly Dictionary<string, SealState> seals = new(StringComparer.Ordinal);
        private readonly List<Evidence> evidence = new();
        private readonly List<Candidate> candidates = new();
        private readonly Slot[] slots;
        private readonly Dictionary<string, double> cooldowns = new(StringComparer.Ordinal);
        private readonly HashSet<string> validationIds = new(StringComparer.Ordinal);
        private int bufferHead, bufferCount, pending = -1, expiredPending = -1, previousLeft, previousRight, previousFlags;
        private int lastAttemptFrame = -1;
        private long nextAction;
        private double now, voiceEnd, barrier = double.NegativeInfinity, lastVoiceEnd = double.NegativeInfinity;
        private double expiredVoiceEnd, expiredProcessingDeadline;
        private double lastSample = double.NegativeInfinity, lastAttemptTime = double.NegativeInfinity;
        private bool initialized, hasPrevious, committing;
        private string configurationError;

        public SkillMatchEngine(IReadOnlyList<SkillDefinition> skills, SkillMatchSettings settings = null, string cancelPhrase = SkillPhrase.DefaultCancel)
        {
            if (skills == null) throw new ArgumentNullException(nameof(skills));
            var config=(settings??new SkillMatchSettings()).Snapshot();
            if(config.Validate() is string settingsError)throw new ArgumentException(settingsError,nameof(settings));
            CancelPhrase=SkillPhrase.Normalize(cancelPhrase);
            if(CancelPhrase.Length==0)throw new ArgumentException("取消口令不能为空",nameof(cancelPhrase));
            Capacity=config.capacity;HistorySeconds=config.historySeconds;MaximumVoiceLatency=config.maximumVoiceLatency;
            ExitSeconds=config.exitSeconds;MaximumSampleGap=config.maximumSampleGap;Margin=config.ambiguityMargin;
            slots=new Slot[Capacity];
            var errors=SkillConfigurationValidator.GetErrors(skills,config,CancelPhrase);
            var definitions = new SkillDefinition[skills.Count];
            recipes = new Recipe[skills.Count];
            for (int i = 0; i < skills.Count; i++)
            {
                definitions[i] = skills[i]; recipes[i] = new Recipe(skills[i]);
                if (errors[i] != null) Disable(i, errors[i]);
            }
            Skills = Array.AsReadOnly(definitions);
            for (int i = 0; i < recipes.Length; i++)
            {
                var recipe = recipes[i];
                if (!recipe.Enabled || recipe.Mode == SkillTriggerMode.VoiceOnly) continue;
                if (!seals.TryGetValue(recipe.Seal, out var seal)) { seal = new SealState(recipe.Seal); seals.Add(recipe.Seal, seal); }
                seal.Trackers.Add(new Tracker(i));
            }
            if (configurationError != null) Status = configurationError;
            var recipeInfo=new List<SkillRecipeInfo>();
            foreach(var recipe in recipes)if(recipe.Enabled)recipeInfo.Add(new SkillRecipeInfo(recipe.Definition));
            ActiveRecipes=recipeInfo.AsReadOnly();
        }

        public void Tick(double time)
        {
            if (!Finite(time)) { SetStatus("输入时钟无效"); return; }
            if (initialized && time + Epsilon < now) { Reset(time); SetStatus("输入时钟回退，已清除匹配上下文"); return; }
            now = time; initialized = true; Prune();
            if (pending >= 0 && now > PendingDeadline + Epsilon)
            {
                // LateUpdate's Tick can be a few milliseconds later than the lab's sample timestamp.
                // Hide the expired intent immediately, but allow that already sampled frame to finish.
                expiredPending = pending; expiredVoiceEnd = voiceEnd;
                expiredProcessingDeadline = PendingDeadline + MaximumSampleGap;
                pending = -1; SetStatus("组合窗口超时，请重新输入口令与手印");
            }
            if (expiredPending >= 0 && now > expiredProcessingDeadline + Epsilon) expiredPending = -1;
        }

        public void Observe(SealFrame frame, IReadOnlyList<SealScore> scores, bool suppressActivation = false)
        {
            if (!initialized || frame == null || frame.left == null || frame.right == null || scores == null || !Finite(frame.time)) return;
            if (frame.time <= barrier || frame.time + Epsilon < lastSample || frame.time > now + MaximumVoiceLatency) return;
            now = Math.Max(now, frame.time); Prune();
            int flags = Flags(frame.left) | (Flags(frame.right) << 3);
            if (hasPrevious && previousLeft == frame.left.version && previousRight == frame.right.version && previousFlags == flags) return;
            bool diagnosticDue=frame.time>=nextObservationDiagnostic;
            if(diagnosticDue) { nextObservationDiagnostic=frame.time+.2; ObservationDiagnostics=$"骨骼间隔 {frame.time-lastSample:F3}s / 上限 {MaximumSampleGap:F3}s；输入占用={suppressActivation}；双手有效={BothHandsValid(frame)}"; }
            bool contiguous = frame.time - lastSample <= MaximumSampleGap + Epsilon;
            previousLeft = frame.left.version; previousRight = frame.right.version; previousFlags = flags;
            hasPrevious = true; lastSample = frame.time;
            var slot = AddFrame(frame, scores);
            bool valid = BothHandsValid(frame);
            candidates.Clear();
            foreach (var state in seals.Values)
            {
                ScoreFact target = default; bool found = false;
                for (int i = 0; i < slot.ScoreCount; i++)
                    if (slot.Scores[i].Seal == state.Seal)
                    { if (found) { found = false; break; } target = slot.Scores[i]; found = true; }
                bool quality = valid && found && target.Quality;
                float minimum = 1;
                foreach (var tracker in state.Trackers) minimum = Math.Min(minimum, Threshold(recipes[tracker.Recipe], target));
                bool shapePresent = quality && target.Score + Epsilon >= minimum;
                if (quality && !shapePresent)
                {
                    if (!contiguous || double.IsNaN(state.ExitSince)) state.ExitSince = frame.time;
                    if (frame.time - state.ExitSince + Epsilon >= ExitSeconds) state.Rearm();
                }
                else state.ExitSince = double.NaN;
                if (!contiguous || !quality || suppressActivation) foreach (var tracker in state.Trackers) tracker.Start = double.NaN;
                if (state.Blocked || suppressActivation) { if(diagnosticDue) ObservationDiagnostics+=$"\n{state.Seal}：动作已消耗={state.Blocked}，输入被占用={suppressActivation}"; continue; }
                foreach (var tracker in state.Trackers)
                {
                    var recipe = recipes[tracker.Recipe];
                    if (tracker.Complete && recipe.Mode == SkillTriggerMode.GestureOnly) continue;
                    bool qualifies = quality && target.Score + Epsilon >= Threshold(recipe, target) && Unambiguous(slot, target);
                    if(diagnosticDue) {
                        string other="无"; float best=-1;
                        for(int j=0;j<slot.ScoreCount;j++) if(slot.Scores[j].Seal!=target.Seal && slot.Scores[j].Quality && slot.Scores[j].Score>best) {best=slot.Scores[j].Score;other=slot.Scores[j].Seal;}
                        ObservationDiagnostics+=$"\n{recipe.Id}/{state.Seal}：分数 {target.Score:P1} / {Threshold(recipe,target):P1}，质量通过={quality}，歧义通过={Unambiguous(slot,target)}；竞争印 {other} {best:P1}，要求领先 {Margin:P0}；保持 {(double.IsNaN(tracker.Start)?0:frame.time-tracker.Start):F3}/{target.Hold:F3}s";
                    }
                    if (!qualifies) { tracker.Start = double.NaN; continue; }
                    if (state.Action == 0) state.Action = ++nextAction;
                    if (double.IsNaN(tracker.Start)) tracker.Start = frame.time;
                    if (frame.time - tracker.Start + Epsilon < target.Hold) continue;
                    bool firstCompletion = !tracker.Complete;
                    // A still-unconsumed combination is usable while the player keeps the seal.
                    // Roll its interval, retaining the same physical action and allocation. A loss
                    // of quality resets Start above, so a fresh full hold is needed before rolling.
                    double start = recipe.Mode == SkillTriggerMode.VoiceAndGesture ? Math.Max(tracker.Start, frame.time - target.Hold) : tracker.Start;
                    var item = tracker.Current;
                    if (item == null) tracker.Current = item = new Evidence(tracker.Recipe, state.Action, Epoch, start, frame.time, target.Score);
                    else { item.Start = start; item.End = frame.time; item.Score = target.Score; }
                    tracker.Complete = true;
                    if (!item.Buffered)
                    {
                        item.Buffered = true; evidence.Add(item);
                        if (evidence.Count > Capacity) { evidence[0].Buffered = false; evidence.RemoveAt(0); }
                    }
                    if (firstCompletion && recipe.Mode == SkillTriggerMode.GestureOnly) candidates.Add(new Candidate(tracker.Recipe, item));
                }
            }
            if (!suppressActivation && pending >= 0 && FindEvidence(pending, voiceEnd, out var match)) candidates.Add(new Candidate(pending, match));
            else if (!suppressActivation && expiredPending >= 0 && now <= expiredProcessingDeadline + Epsilon &&
                     frame.time <= expiredVoiceEnd + recipes[expiredPending].Window + Epsilon && FindEvidence(expiredPending, expiredVoiceEnd, out var lateMatch))
                candidates.Add(new Candidate(expiredPending, lateMatch));
            ResolveCandidates();
        }

        public void Voice(string phrase, double endedAt, double arrivedAt)
        {
            if (!initialized || !Finite(endedAt) || !Finite(arrivedAt)) return;
            if (endedAt > arrivedAt + Epsilon || arrivedAt - endedAt > MaximumVoiceLatency + Epsilon || arrivedAt > now + MaximumVoiceLatency + Epsilon)
            { SetStatus($"语音时钟无效或结果延迟超过{MaximumVoiceLatency:0.#}秒，请重试"); return; }
            now = Math.Max(now, arrivedAt); Prune();
            if (endedAt <= barrier || endedAt <= lastVoiceEnd + Epsilon) { SetStatus("已忽略重复、过期或取消前的口令"); return; }
            string normalized = SkillPhrase.Normalize(phrase);
            if (normalized == CancelPhrase) { lastVoiceEnd = endedAt; Cancel(now); return; }
            int selected = -1;
            for (int i = 0; i < recipes.Length; i++)
                if (recipes[i].Enabled && recipes[i].Mode != SkillTriggerMode.GestureOnly && recipes[i].Phrase == normalized) { selected = i; break; }
            if (selected < 0) { SetStatus("未匹配完整技能口令" + (configurationError == null ? "" : "；" + configurationError)); return; }
            lastVoiceEnd = endedAt;
            expiredPending = -1;
            if (pending >= 0 && pending != selected) ClearContext(now, false);
            var recipe = recipes[selected];
            pending = -1;
            if (recipe.Mode == SkillTriggerMode.VoiceOnly) { Attempt(selected, null); return; }
            voiceEnd = endedAt; pending = selected;
            if (FindEvidence(selected, endedAt, out var match)) { Attempt(selected, match); return; }
            if (now > PendingDeadline + Epsilon) { pending = -1; SetStatus("口令已超过组合窗口，请重新输入"); return; }
            SetStatus(Name(recipe.Definition) + "：口令已确认，等待" + recipe.Seal + "印");
        }

        public void Cancel(double time)
        {
            if (!Finite(time)) return;
            ClearContext(time, false); SetStatus("已取消施法；旧口令与手印已清除，请退出原手印后重试");
        }

        public void Reset(double time)
        {
            if (!Finite(time)) return;
            ClearContext(time, true); SetStatus(configurationError ?? "已重置输入，等待新的口令与手印");
        }

        private void ClearContext(double time, bool resetClock)
        {
            now = resetClock ? time : Math.Max(now, time); initialized = true; barrier = now; Epoch++;
            pending = expiredPending = -1;
            foreach (var item in evidence) item.Buffered = false;
            evidence.Clear(); bufferHead = bufferCount = 0; hasPrevious = false; lastSample = double.NegativeInfinity;
            foreach (var state in seals.Values)
            {
                state.Blocked |= state.Action != 0;
                state.ExitSince = double.NaN;
                foreach (var tracker in state.Trackers) { tracker.Start = double.NaN; tracker.Complete = false; tracker.Current = null; }
            }
            if (resetClock) { lastVoiceEnd = double.NegativeInfinity; lastAttemptTime = double.NegativeInfinity; }
        }

        private void ResolveCandidates()
        {
            if (candidates.Count == 0) return;
            int selected = candidates[0].Recipe;
            for (int i = 1; i < candidates.Count; i++) if (candidates[i].Recipe != selected)
            {
                foreach (var item in candidates) Consume(item.Evidence);
                pending = expiredPending = -1; SetStatus("技能冲突，未释放；请重新输入"); return;
            }
            Attempt(selected, candidates[0].Evidence);
        }

        private void Attempt(int index, Evidence item)
        {
            pending = expiredPending = -1; if (item != null) Consume(item);
            var recipe = recipes[index];
            bool sameFrame = Application.isPlaying ? lastAttemptFrame == Time.frameCount : Math.Abs(now - lastAttemptTime) <= Epsilon;
            if (committing || sameFrame) { SetStatus("本帧已有激活尝试；本次输入已消费，请重试"); return; }
            lastAttemptTime = now; lastAttemptFrame = Application.isPlaying ? Time.frameCount : -1;
            if (!ExternalCooldownAuthority && cooldowns.TryGetValue(recipe.Id, out double deadline) && now + Epsilon < deadline)
            { SetStatus(Name(recipe.Definition) + "仍在冷却；本次输入已消费"); return; }
            bool success = false;
            committing = true;
            try { success = TryActivate != null && TryActivate(recipe.Definition); }
            catch (Exception ex) { SetStatus("激活异常：" + ex.Message + "；本次输入已消费"); return; }
            finally { committing = false; }
            if (success) { if (!ExternalCooldownAuthority) cooldowns[recipe.Id] = now + recipe.Cooldown; SetStatus(Name(recipe.Definition) + "已激活"); }
            else SetStatus(Name(recipe.Definition) + "激活失败；本次输入已消费，请重新输入");
        }

        private void Consume(Evidence item)
        {
            for (int i = evidence.Count - 1; i >= 0; i--)
                if (evidence[i].Action == item.Action) { evidence[i].Buffered = false; evidence.RemoveAt(i); }
            foreach (var state in seals.Values) if (state.Action == item.Action) state.Blocked = true;
        }

        private bool FindEvidence(int recipe, double anchor, out Evidence found)
        {
            found = null; double bestDistance = double.PositiveInfinity;
            foreach (var item in evidence)
            {
                if (item.Recipe != recipe || item.Epoch != Epoch || Math.Max(anchor, item.End) - Math.Min(anchor, item.Start) > recipes[recipe].Window + Epsilon) continue;
                double distance = Math.Abs(anchor - item.End);
                if (distance < bestDistance) { found = item; bestDistance = distance; }
            }
            return found != null;
        }

        private void Prune()
        {
            while (bufferCount > 0 && slots[bufferHead].Frame.time < now - HistorySeconds - Epsilon)
            { bufferHead = (bufferHead + 1) % Capacity; bufferCount--; }
            for (int i = evidence.Count - 1; i >= 0; i--)
                if (evidence[i].Start < now - HistorySeconds - Epsilon || evidence[i].Epoch != Epoch)
                { evidence[i].Buffered = false; evidence.RemoveAt(i); }
        }

        private Slot AddFrame(SealFrame frame, IReadOnlyList<SealScore> scores)
        {
            if (bufferCount == Capacity) { bufferHead = (bufferHead + 1) % Capacity; bufferCount--; }
            int index = (bufferHead + bufferCount++) % Capacity;
            var slot = slots[index] ?? (slots[index] = new Slot());
            CopyFrame(frame, slot.Frame);
            if (slot.Scores.Length < scores.Count) slot.Scores = new ScoreFact[scores.Count];
            slot.ScoreCount = scores.Count;
            for (int i = 0; i < scores.Count; i++) slot.Scores[i] = new ScoreFact(scores[i], ValidateProfile(scores[i]?.profile));
            return slot;
        }

        // Mirror SealProfile.ValidateRules without allocating its temporary HashSet every sample.
        // Only this scratch set is reused; score history never retains mutable profile/rule objects.
        private bool ValidateProfile(SealProfile profile)
        {
            if (!profile || !profile.calibrated || !float.IsFinite(profile.threshold) || profile.threshold <= 0 || profile.threshold > 1 ||
                !float.IsFinite(profile.minimumCoverage) || profile.minimumCoverage <= 0 || profile.minimumCoverage > 1 ||
                !float.IsFinite(profile.holdSeconds) || profile.holdSeconds < 0 || profile.rules == null || profile.rules.Count == 0) return false;
            validationIds.Clear(); float total = 0;
            foreach (var rule in profile.rules)
            {
                if (rule == null || string.IsNullOrWhiteSpace(rule.featureId) || !validationIds.Add(rule.featureId) ||
                    !float.IsFinite(rule.weight) || rule.weight < 0 || !float.IsFinite(rule.min) || !float.IsFinite(rule.max) ||
                    !float.IsFinite(rule.tolerance) || rule.min > rule.max || rule.tolerance <= 0) return false;
                total += rule.weight;
            }
            return total > 0 && float.IsFinite(total);
        }

        private bool Unambiguous(Slot slot, ScoreFact target)
        {
            for (int i = 0; i < slot.ScoreCount; i++)
            {
                var competitor = slot.Scores[i];
                if (competitor.Seal == target.Seal || !competitor.Quality) continue;
                if (target.Score - competitor.Score + Epsilon < Margin) return false;
            }
            return true;
        }
        private static float Threshold(Recipe recipe, ScoreFact score) => recipe.Mode == SkillTriggerMode.VoiceAndGesture ? Math.Max(recipe.Floor, score.Threshold - recipe.Discount) : score.Threshold;
        private void Disable(int index, string reason)
        {
            recipes[index].Enabled = false;
            string message = "配置冲突或无效：" + Name(recipes[index].Definition) + "（" + reason + "）";
            configurationError = configurationError == null ? message : configurationError + "；" + message;
        }
        private void SetStatus(string status) { if (Status == status) return; Status = status; Log?.Invoke(status); }
        private static string Name(SkillDefinition definition) => definition ? (string.IsNullOrWhiteSpace(definition.displayName) ? definition.skillId : definition.displayName) : "空技能";
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        private static int Flags(HandSnapshot hand) => (hand.connected ? 1 : 0) | (hand.tracked ? 2 : 0) | (hand.fresh ? 4 : 0);
        private static bool BothHandsValid(SealFrame frame) => Flags(frame.left) == 7 && Flags(frame.right) == 7 &&
            Finite(frame.left.observedAt) && Finite(frame.right.observedAt) && Math.Abs(frame.left.observedAt - frame.right.observedAt) <= SealFeatureExtractor.MaximumHandSkew;

        private static void CopyFrame(SealFrame source, SealFrame destination)
        {
            destination.schema = source.schema; destination.time = source.time; destination.label = source.label;
            destination.skeleton = source.skeleton; destination.source = source.source;
            CopyHand(source.left, destination.left); CopyHand(source.right, destination.right);
        }
        private static void CopyHand(HandSnapshot source, HandSnapshot destination)
        {
            destination.connected = source.connected; destination.tracked = source.tracked; destination.highConfidence = source.highConfidence;
            destination.fresh = source.fresh; destination.wristPosesValid = source.wristPosesValid; destination.version = source.version;
            destination.observedAt = source.observedAt; destination.scale = source.scale; destination.confidenceSource = source.confidenceSource;
            CopyArray(source.joints, ref destination.joints); CopyArray(source.jointValid, ref destination.jointValid);
            CopyArray(source.wristPoses, ref destination.wristPoses); CopyArray(source.fingerConfidence, ref destination.fingerConfidence);
        }
        private static void CopyArray<T>(T[] source, ref T[] destination)
        {
            int length = source?.Length ?? 0;
            if (destination == null || destination.Length != length) destination = new T[length];
            if (length > 0) Array.Copy(source, destination, length);
        }

        private sealed class Recipe
        {
            public readonly SkillDefinition Definition;
            public readonly string Id, Phrase, Seal;
            public readonly SkillTriggerMode Mode;
            public readonly float Window, Discount, Floor, Cooldown;
            public bool Enabled = true;
            public Recipe(SkillDefinition definition)
            {
                Definition = definition;
                if (!definition) { Id = Phrase = Seal = ""; return; }
                Id = definition.skillId; Phrase = SkillPhrase.Normalize(definition.phrase); Seal = definition.sealName;
                Mode = definition.triggerMode; Window = definition.matchWindow; Discount = definition.voiceDiscount;
                Floor = definition.thresholdFloor; Cooldown = definition.cooldownSeconds;
            }
        }
        private sealed class SealState
        {
            public readonly string Seal;
            public readonly List<Tracker> Trackers = new();
            public long Action;
            public bool Blocked;
            public double ExitSince = double.NaN;
            public SealState(string seal) { Seal = seal; }
            public void Rearm() { Action = 0; Blocked = false; foreach (var item in Trackers) { item.Start = double.NaN; item.Complete = false; item.Current = null; } }
        }
        private sealed class Tracker
        {
            public readonly int Recipe;
            public double Start = double.NaN;
            public bool Complete;
            public Evidence Current;
            public Tracker(int recipe) { Recipe = recipe; }
        }
        private sealed class Evidence
        {
            public readonly int Recipe;
            public readonly long Action, Epoch;
            public double Start, End;
            public float Score;
            public bool Buffered;
            public Evidence(int recipe, long action, long epoch, double start, double end, float score)
            { Recipe = recipe; Action = action; Epoch = epoch; Start = start; End = end; Score = score; }
        }
        private readonly struct Candidate
        {
            public readonly int Recipe;
            public readonly Evidence Evidence;
            public Candidate(int recipe, Evidence evidence) { Recipe = recipe; Evidence = evidence; }
        }
        private sealed class Slot
        {
            public readonly SealFrame Frame = new();
            public ScoreFact[] Scores = Array.Empty<ScoreFact>();
            public int ScoreCount;
        }
        // Immutable facts only: no SealProfile, FeatureRule, RuleScore or caller-owned array references.
        private readonly struct ScoreFact
        {
            public readonly string Seal;
            public readonly float Score, Coverage, Threshold, MinimumCoverage, Hold;
            public readonly bool ValidProfile, MissingRequired;
            public bool Quality => ValidProfile && !MissingRequired && float.IsFinite(Score) && float.IsFinite(Coverage) && Coverage + Epsilon >= MinimumCoverage;
            public ScoreFact(SealScore source, bool validProfile)
            {
                var profile = source?.profile;
                Seal = profile ? profile.sealName : ""; Score = source?.score ?? 0; Coverage = source?.coverage ?? 0;
                Threshold = profile ? profile.threshold : 1; MinimumCoverage = profile ? profile.minimumCoverage : 1; Hold = profile ? profile.holdSeconds : 0;
                ValidProfile = validProfile;
                bool missing = source?.reason == "Required feature missing";
                if (source != null) foreach (var detail in source.details)
                    if (detail?.rule != null && detail.rule.weight > 0 && detail.rule.required && !detail.available) missing = true;
                MissingRequired = missing;
            }
        }
    }
}
