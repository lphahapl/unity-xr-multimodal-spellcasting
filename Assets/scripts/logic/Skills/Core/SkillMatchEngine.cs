using System;
using System.Collections.Generic;
using System.Text;
using NarutoVR.SealLab;
using UnityEngine;

namespace NarutoVR.SealCombat
{
    /// <summary>单线程输入匹配器。首个输入前先调用 Tick 初始化时钟，匹配成功后通过回调申请激活。</summary>
    public sealed class SkillMatchEngine
    {
        public Func<SkillDefinition, bool> TryActivate;
        public Func<SkillDefinition, bool> IsCastConditionMet;
        internal Action<SkillDefinition> CastConditionWaitChanged;
        public Action<string> Log;
        /// <summary>每个稳定的物理结印只通知一次，包括尚未组成技能的印。</summary>
        public event Action<string, double> SealConfirmed;
        readonly List<SealHistory> histories = new();
        readonly SealHistory feedbackHistory;
        readonly SealHistory leftFeedback = new();
        double lastLeftObservation = double.NegativeInfinity;
        public string Status { get; private set; } = "等待技能输入";
        // 日志与界面按需获取诊断文字，
        // 避免在每次骨骼 Observe 调用中反复分配格式化字符串。
        public string ObservationDiagnostics
        {
            get
            {
                if (!hasPrevious || bufferCount == 0) return "等待骨骼输入";
                if (lastSample >= nextObservationDiagnostic)
                {
                    diagnosticText = DescribeObservation();
                    nextObservationDiagnostic = lastSample + .2;
                }
                return diagnosticText;
            }
        }
        private string diagnosticText = "等待骨骼输入";
        private double nextObservationDiagnostic = double.NegativeInfinity, diagnosticGap;
        private bool diagnosticSuppressed, diagnosticHeld;
        public string PendingSkillName => pending < 0 ? "" : Name(recipes[pending].Definition);
        public string NextSeal(string id)
        {
            if (waitingForCondition && recipes[pending].Id == id) return recipes[pending].Order[^1];
            foreach (var recipe in recipes)
                if (recipe.Id == id) return recipe.Sequence == null ? recipe.Seal : recipe.Order[recipe.Sequence.PrefixLength(recipe, now)];
            return "";
        }
        internal SkillDefinition PendingSequenceCharge => pending >= 0 &&
            recipes[pending].Sequence?.PrefixLength(recipes[pending], now) > 0 ? recipes[pending].Definition : null;
        public string PendingSkillId => pending < 0 ? "" : recipes[pending].Id;
        public bool HasPendingCast => pending >= 0 || heldShort != null;
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
        private readonly double HistorySeconds, MaximumVoiceLatency, ExitSeconds, MaximumSampleGap, ComboHeldRearmSeconds;
        private const double Epsilon = 0.000001;
        private readonly float Margin;
        private readonly Recipe[] recipes;
        private readonly SealNode root = new(0);
        private readonly double shortSealHold;
        private const double BranchTrackingGrace = .150, BranchPoseDebounce = .050;
        private readonly SealHistory branchHistory;
        private SealNode branchTerminal;
        private Evidence heldShort;
        private double branchObservedAt, lastBranchFrameAt, branchStartedAt, shortProgress;
        private double lastMatchingAt = double.NaN, departingSince = double.NaN;
        private readonly Dictionary<string, SealState> seals = new(StringComparer.Ordinal);
        private readonly List<Evidence> evidence = new();
        private readonly List<Candidate> candidates = new();
        private readonly Slot[] slots;
        private readonly Slot currentScores = new();
        private bool waitingForCondition;
        private readonly HashSet<string> validationIds = new(StringComparer.Ordinal);
        private readonly HashSet<string> voicePhrasesAtLastEnd = new(StringComparer.Ordinal);
        private int bufferHead, bufferCount, pending = -1, expiredPending = -1, previousLeft, previousRight, previousFlags;
        private int lastAttemptFrame = -1;
        private long nextAction;
        private double now, voiceEnd, barrier = double.NegativeInfinity, lastVoiceEnd = double.NegativeInfinity;
        private double expiredVoiceEnd, expiredProcessingDeadline;
        private double lastSample = double.NegativeInfinity, lastAttemptTime = double.NegativeInfinity;
        private bool initialized, hasPrevious, committing, previousLeftHighConfidence;
        private string configurationError;

        /// <summary>冻结配方与时序配置，为合法配方建立单印跟踪器；无效配方禁用并给出原因。</summary>
        public SkillMatchEngine(IReadOnlyList<SkillDefinition> skills, SkillMatchSettings settings = null, string cancelPhrase = SkillPhrase.DefaultCancel)
        {
            if (skills == null) throw new ArgumentNullException(nameof(skills));
            var config=(settings??new SkillMatchSettings()).Snapshot();
            if(config.Validate() is string settingsError)throw new ArgumentException(settingsError,nameof(settings));
            CancelPhrase=SkillPhrase.Normalize(cancelPhrase);
            if(CancelPhrase.Length==0)throw new ArgumentException("取消口令不能为空",nameof(cancelPhrase));
            Capacity=config.capacity;HistorySeconds=config.historySeconds;MaximumVoiceLatency=config.maximumVoiceLatency;
            ExitSeconds=config.exitSeconds;MaximumSampleGap=config.maximumSampleGap;Margin=config.ambiguityMargin;
            ComboHeldRearmSeconds=config.comboHeldRearmSeconds;
            shortSealHold = config.ShortSealHoldSeconds;
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
                var node = root;
                for (int step = 0; step < recipe.Order.Length; step++)
                {
                    string name = recipe.Order[step];
                    if (!node.Children.TryGetValue(name, out var child))
                        node.Children.Add(name, child = new SealNode(step + 1));
                    node = recipe.Path[step] = child;
                    if (recipe.Mode == SkillTriggerMode.GestureOnly)
                    {
                        node.PureWindow = Math.Max(node.PureWindow, recipe.Window);
                        if (step < recipe.Order.Length - 1)
                            node.ContinuationWindow = Math.Max(node.ContinuationWindow, recipe.Window);
                    }
                }
                node.Finishes.Add(i);
                if (recipe.Order.Length > 1) continue;
                if (!seals.TryGetValue(recipe.Seal, out var seal)) { seal = new SealState(recipe.Seal); seals.Add(recipe.Seal, seal); }
                recipe.SingleTracker = new Tracker(i);
                seal.Trackers.Add(recipe.SingleTracker);
            }
            var strict = new SealHistory();
            histories.Add(strict);
            feedbackHistory = strict;
            foreach (var recipe in recipes)
            {
                if (!recipe.Enabled || recipe.Mode != SkillTriggerMode.VoiceAndGesture) continue;
                if (recipe.Discount == 0 && recipe.Floor == 0) continue;
                if (feedbackHistory == strict) { feedbackHistory = new SealHistory(); histories.Add(feedbackHistory); }
                feedbackHistory.Include(recipe);
            }
            foreach (var recipe in recipes)
            {
                if (!recipe.Enabled || recipe.Order.Length < 2) continue;
                recipe.Sequence = strict;
                if (recipe.Mode == SkillTriggerMode.GestureOnly && Array.Exists(recipe.Path, node =>
                    node.ContinuationWindow > 0 && node.Finishes.Exists(i => recipes[i].Mode == SkillTriggerMode.GestureOnly)))
                {
                    if (branchHistory == null) { branchHistory = new SealHistory(); histories.Add(branchHistory); }
                    recipe.Sequence = branchHistory;
                }
                if (recipe.Mode == SkillTriggerMode.VoiceAndGesture && (recipe.Discount != 0 || recipe.Floor != 0))
                {
                    // A relaxed voice recipe may accept a pose that a pure recipe must reject.
                    recipe.Sequence = new SealHistory(recipe);
                    histories.Add(recipe.Sequence);
                }
            }
            if (configurationError != null) Status = configurationError;
            var recipeInfo=new List<SkillRecipeInfo>();
            foreach(var recipe in recipes)if(recipe.Enabled)recipeInfo.Add(new SkillRecipeInfo(recipe.Definition));
            ActiveRecipes=recipeInfo.AsReadOnly();
        }

        /// <summary>推进输入实时时钟并清理过期证据；时间回退时重置上下文。</summary>
        public void Tick(double time)
        {
            if (!Finite(time)) { SetStatus("输入时钟无效"); return; }
            if (initialized && time + Epsilon < now) { Reset(time); SetStatus("输入时钟回退，已清除匹配上下文"); return; }
            now = time; initialized = true; Prune();
            ExpireBranch(now);
            if (waitingForCondition && (now > PendingDeadline + Epsilon || now - lastSample > MaximumSampleGap + Epsilon))
                StopWaitingForCondition();
            if (pending >= 0 && now > PendingDeadline + Epsilon)
            {
                // LateUpdate 的 Tick 时间可能比本帧骨骼采样时间晚几毫秒。
                // 先关闭已过期的等待提示，再给已经采集的合法边界帧一次完成匹配的机会。
                expiredPending = pending; expiredVoiceEnd = voiceEnd;
                expiredProcessingDeadline = PendingDeadline + MaximumSampleGap;
                pending = -1; SetStatus("组合窗口超时，请重新输入口令与手印");
            }
            if (expiredPending >= 0 && now > expiredProcessingDeadline + Epsilon) expiredPending = -1;
        }

        /// <summary>接收新骨骼帧及评分，累计连续合格保持区间，并尝试解决当前候选。</summary>
        public void Observe(SealFrame frame, IReadOnlyList<SealScore> scores, bool suppressActivation = false, bool suppressHeldRearm = false,
            double observationsAfter = double.NegativeInfinity, bool suppressLeftOnly = false)
        {
            if (!initialized || frame?.left == null || scores == null || !Finite(frame.time)) return;
            if (frame.time <= barrier || frame.time + Epsilon < lastSample || frame.time > now + MaximumVoiceLatency) return;
            now = Math.Max(now, frame.time); Prune();
            int flags = Flags(frame.left) | (Flags(frame.right) << 3);
            if (suppressLeftOnly || suppressActivation)
            {
                leftFeedback.Interrupt();
                foreach (var state in seals.Values)
                    if (state.LeftOnly)
                    {
                        DiscardAction(state.Action);
                        state.ResetTrackers();
                        state.ExitSince = double.NaN;
                    }
            }
            if (suppressActivation || suppressHeldRearm)
            {
                EndBranch();
                if (waitingForCondition) StopWaitingForCondition();
                // Ownership can change between two observations of the same XR sample.
                foreach (var history in histories) history.Interrupt();
                for (int i = evidence.Count - 1; i >= 0; i--)
                    if (recipes[evidence[i].Recipe].Sequence != null) evidence.RemoveAt(i);
            }
            bool newLeft = (!hasPrevious || previousLeft != frame.left.version) && frame.left.observedAt > lastLeftObservation;
            int rightVersion = frame.right?.version ?? 0;
            // Confidence can drop on a repeated pose. Break only the holds that require it;
            // the repeated pose must not advance either the left or the two-hand timers.
            if (hasPrevious && previousLeftHighConfidence && !frame.left.highConfidence)
            {
                for (int i = 0; i < scores.Count; i++)
                {
                    var profile = scores[i]?.profile;
                    if (!profile || profile.handMode != SealHandMode.LeftOnly || profile.allowLowConfidence) continue;
                    string seal = profile.sealName;
                    if (string.IsNullOrEmpty(seal)) continue;
                    if (seals.TryGetValue(seal, out var held))
                    {
                        held.ExitSince = double.NaN;
                        foreach (var tracker in held.Trackers) tracker.Start = double.NaN;
                    }
                    leftFeedback.InterruptHold(seal);
                    foreach (var history in histories) history.InterruptHold(seal);
                    if (waitingForCondition && recipes[pending].Order[^1] == seal) StopWaitingForCondition();
                    if (heldShort != null && recipes[heldShort.Recipe].Order[^1] == seal)
                        lastMatchingAt = departingSince = double.NaN;
                }
            }
            previousLeftHighConfidence = frame.left.highConfidence;
            if (hasPrevious && previousLeft == frame.left.version && previousRight == rightVersion && previousFlags == flags) return;
            diagnosticGap=frame.time-lastSample;diagnosticSuppressed=suppressActivation;diagnosticHeld=suppressHeldRearm;
            bool contiguous = frame.time - lastSample <= MaximumSampleGap + Epsilon;
            previousLeft = frame.left.version; previousRight = rightVersion; previousFlags = flags;
            hasPrevious = true; lastSample = frame.time;
            bool valid = BothHandsValid(frame) && Math.Min(frame.left.observedAt, frame.right.observedAt) > observationsAfter;
            bool leftFresh = FreshLeft(frame, now);
            bool leftValid = !suppressLeftOnly && leftFresh && frame.left.observedAt > observationsAfter;
            bool leftContiguous = frame.left.observedAt - lastLeftObservation <= MaximumSampleGap + Epsilon;
            if (newLeft && leftFresh) lastLeftObservation = frame.left.observedAt;
            var slot = AddFrame(frame, scores, valid, leftValid);
            slot.LeftAdvanced = newLeft;
            slot.LeftContiguous = leftContiguous;
            bool fresh = valid && FreshHands(frame, now);
            double observed = Math.Min(frame.left.observedAt, frame.right?.observedAt ?? double.NegativeInfinity);
            ExpireBranch(now);
            bool branchSampleAdvanced = branchTerminal == null || observed > lastBranchFrameAt + Epsilon;
            bool branchGap = branchTerminal != null && observed - lastBranchFrameAt > BranchPoseDebounce + Epsilon;
            if (branchTerminal != null && fresh) lastBranchFrameAt = Math.Max(lastBranchFrameAt, observed);
            candidates.Clear();
            string reservedSeal = null;
            if (pending >= 0)
            {
                var selected = recipes[pending];
                if (selected.Definition.reserveFinalSeal && selected.Sequence != null &&
                    selected.Sequence.PrefixLength(selected, now) == selected.Order.Length - 1)
                    reservedSeal = selected.Order[^1];
            }
            foreach (var state in seals.Values)
            {
                bool found = TryGetScore(slot, state.Seal, out var target);
                state.LeftOnly = found && target.LeftOnly;
                if (state.LeftOnly && target.Quality && !newLeft && !suppressActivation) continue;
                double sampleAt = state.LeftOnly ? frame.left.observedAt : frame.time;
                bool continuous = state.LeftOnly ? leftContiguous : contiguous;
                bool quality = found && target.Quality;
                float minimum = 1;
                foreach (var tracker in state.Trackers) minimum = Math.Min(minimum, Threshold(recipes[tracker.Recipe], target));
                bool shapePresent = quality && target.Score + Epsilon >= minimum;
                if (quality && !shapePresent)
                {
                    if (!continuous || double.IsNaN(state.ExitSince)) state.ExitSince = sampleAt;
                    if (sampleAt - state.ExitSince + Epsilon >= ExitSeconds) state.Rearm();
                }
                else state.ExitSince = double.NaN;
                if (!continuous || !quality || suppressActivation) foreach (var tracker in state.Trackers) tracker.Start = double.NaN;
                if (state.Blocked && (suppressHeldRearm || suppressActivation))
                {
                    // 手部被占用时清除本轮持续保持产生的重复证据，
                    // 待占用结束后重新累计保持时间，避免一松手就补发。
                    DiscardAction(state.Action);
                    state.ResetTrackers();
                }
                if (suppressActivation || (state.Blocked && (suppressHeldRearm || frame.time < state.BlockedAt))) continue;
                foreach (var tracker in state.Trackers)
                {
                    var recipe = recipes[tracker.Recipe];
                    if (state.Blocked && recipe.Mode != SkillTriggerMode.VoiceAndGesture) continue;
                    if (tracker.Complete && recipe.Mode == SkillTriggerMode.GestureOnly) continue;
                    bool qualifies = quality && target.Score + Epsilon >= Threshold(recipe, target) && Unambiguous(slot, target);
                    if (!qualifies) { tracker.Start = double.NaN; continue; }
                    if (state.Action == 0) state.Action = ++nextAction;
                    if (double.IsNaN(tracker.Start)) tracker.Start = sampleAt;
                    double requiredHold = state.Blocked ? Math.Max(ComboHeldRearmSeconds, target.Hold) : target.Hold;
                    if (sampleAt - tracker.Start + Epsilon < requiredHold) continue;
                    bool firstCompletion = !tracker.Complete;
                    // 组合技能的证据尚未消费且手印持续合格时，可以滚动更新有效区间。
                    // 滚动时复用同一动作编号及证据对象；一旦质量不合格，
                    // 上方逻辑就会清除 Start，必须重新完成一次完整保持。
                    double start = recipe.Mode == SkillTriggerMode.VoiceAndGesture ? Math.Max(tracker.Start, sampleAt - target.Hold) : tracker.Start;
                    var item = tracker.Current;
                    if (item == null) tracker.Current = item = new Evidence(tracker.Recipe, state.Action, Epoch, tracker.Start, sampleAt, target.Score);
                    item.Start = start; item.End = sampleAt; item.Score = target.Score;
                    tracker.Complete = true;
                    if (!item.Buffered)
                    {
                        item.Buffered = true; evidence.Add(item);
                        if (evidence.Count > Capacity) { evidence[0].Buffered = false; evidence.RemoveAt(0); }
                    }
                    if (firstCompletion && recipe.Mode == SkillTriggerMode.GestureOnly && state.Seal != reservedSeal)
                        candidates.Add(new Candidate(tracker.Recipe, item));
                }
            }
            string confirmed = null;
            if (!suppressActivation && !suppressHeldRearm)
            {
                foreach (var history in histories)
                {
                    bool branch = history == branchHistory;
                    bool usable = branch ? fresh && branchSampleAdvanced : valid;
                    if (!usable || !contiguous || (branch && branchGap))
                        history.Interrupt(preserve: branch ? branchTerminal : null);
                    if (usable) history.Observe(this, slot, frame.time);
                }
                confirmed = feedbackHistory.Confirmed;
                if (confirmed != null && TryGetScore(slot, confirmed, out var feedbackScore) && feedbackScore.LeftOnly) confirmed = null;
            }
            // Independent single-hand feedback never advances the two-hand sequence tree.
            string leftConfirmed = null;
            if (!leftValid || !leftContiguous) leftFeedback.Interrupt();
            if (leftValid && newLeft && !suppressActivation)
            {
                leftFeedback.Observe(this, slot, frame.left.observedAt, leftFeedbackOnly: true);
                leftConfirmed = leftFeedback.Confirmed;
            }
            UpdateShortCast(frame, slot, observed, fresh && branchSampleAdvanced && !suppressActivation && !suppressHeldRearm);
            if (waitingForCondition)
            {
                var recipe = recipes[pending];
                if (!contiguous || !RecognizedSealStillHeld(recipe, slot, now))
                    StopWaitingForCondition();
                else if (IsCastConditionMet?.Invoke(recipe.Definition) == true)
                    candidates.Add(new Candidate(pending, null));
            }
            else if (!suppressActivation && pending >= 0 && FindEvidence(pending, voiceEnd, out var match)) candidates.Add(new Candidate(pending, match));
            else if (!suppressActivation && expiredPending >= 0 && now <= expiredProcessingDeadline + Epsilon &&
                     frame.time <= expiredVoiceEnd + recipes[expiredPending].Window + Epsilon && FindEvidence(expiredPending, expiredVoiceEnd, out var lateMatch))
                candidates.Add(new Candidate(expiredPending, lateMatch));
            ResolveCandidates();
            if (confirmed != null) SealConfirmed?.Invoke(confirmed, frame.time);
            if (leftConfirmed != null) SealConfirmed?.Invoke(leftConfirmed, frame.left.observedAt);
        }

        public string LastVoiceRejection { get; private set; }

        /// <summary>读取前三个可用配方的当前进度。不会推进时钟、确认手印或消费输入。</summary>
        public void GetSealHints(List<SkillSealHint> result)
        {
            result.Clear();
            if (!hasPrevious || waitingForCondition) return;
            bool sampleExpired = now - lastSample > MaximumSampleGap + Epsilon;
            for (int i = 0; i < recipes.Length; i++)
            {
                var recipe = recipes[i];
                if (!recipe.Enabled || recipe.Order.Length == 0) continue;
                bool holding = heldShort != null && heldShort.Recipe == i;
                if (sampleExpired && !holding && (branchTerminal == null || recipe.Sequence != branchHistory ||
                    recipe.Path.Length <= branchTerminal.Depth || recipe.Path[branchTerminal.Depth - 1] != branchTerminal)) continue;
                var history = recipe.Sequence ?? feedbackHistory;
                int matched = holding ? recipe.Order.Length : history.PrefixLength(recipe, now, true);
                if (matched == 0) continue;
                bool complete = matched == recipe.Order.Length;
                if (complete && !holding && (recipe.Mode == SkillTriggerMode.GestureOnly ||
                    (recipe.SingleTracker != null && recipe.SingleTracker.Current?.Buffered != true))) continue;
                var hint = new SkillSealHint(recipe.Definition, matched, recipe.Order.Length,
                    complete ? null : recipe.Order[matched],
                    holding ? Mathf.Clamp01((float)(shortProgress / shortSealHold)) : -1f);
                // Prefer a pending short cast, then the longest matching prefix and fewest remaining seals.
                // Equal candidates keep the order authored in the catalog.
                int rank = 0;
                while (rank < result.Count)
                {
                    var other = result[rank];
                    if (hint.Holding != other.Holding ? hint.Holding : hint.Matched > other.Matched ||
                        (hint.Matched == other.Matched && hint.Total < other.Total)) break;
                    rank++;
                }
                if (rank == 3) continue;
                if (result.Count == 3) result.RemoveAt(2);
                result.Insert(rank, hint);
            }
        }

        private void EndBranch()
        {
            branchTerminal = null;
            heldShort = null;
            lastMatchingAt = departingSince = double.NaN;
        }

        private void ExpireBranch(double time)
        {
            if (branchTerminal == null || (time - branchObservedAt <= BranchTrackingGrace + Epsilon &&
                (heldShort != null || time - branchStartedAt <= branchTerminal.ContinuationWindow + Epsilon))) return;
            branchHistory.ForgetPath(branchTerminal);
            EndBranch();
        }

        private void ObserveBranchPose(Slot slot, double observed, bool fresh)
        {
            if (!fresh || observed <= branchObservedAt + Epsilon)
            {
                lastMatchingAt = departingSince = double.NaN;
                return;
            }
            if (heldShort == null) { branchObservedAt = observed; return; }
            var recipe = recipes[heldShort.Recipe];
            if (FinalSealMatches(recipe, slot))
            {
                double elapsed = observed - lastMatchingAt;
                if (elapsed <= BranchPoseDebounce + Epsilon) shortProgress += elapsed;
                lastMatchingAt = branchObservedAt = observed;
                departingSince = double.NaN;
                return;
            }
            lastMatchingAt = double.NaN;
            // Missing features or a near tie are uncertain. A clearly weak old seal or
            // a distinctly stronger alternative is evidence of an intentional transition.
            if (!TryGetScore(slot, recipe.Order[^1], out var target) || !target.Quality ||
                (target.Score + Margin >= target.Threshold && !HasStrongerSeal(slot, target)))
            {
                departingSince = double.NaN;
                return;
            }
            if (double.IsNaN(departingSince) || observed - branchObservedAt > BranchPoseDebounce + Epsilon)
                departingSince = observed;
            branchObservedAt = observed;
            if (observed - departingSince + Epsilon >= BranchPoseDebounce) heldShort = null;
        }

        private bool HasStrongerSeal(Slot slot, ScoreFact target)
        {
            for (int i = 0; i < slot.ScoreCount; i++)
            {
                var other = slot.Scores[i];
                if (other.Seal != target.Seal && other.Quality && other.Score >= other.Threshold &&
                    other.Score > target.Score && other.Score - target.Score + Epsilon >= Margin) return true;
            }
            return false;
        }

        private void UpdateShortCast(SealFrame frame, Slot slot, double observed, bool usable)
        {
            if (branchTerminal != null)
            {
                if (branchHistory.Confirmed != null) EndBranch();
                else ObserveBranchPose(slot, observed, usable);
            }

            for (int i = candidates.Count - 1; i >= 0; i--)
            {
                var candidate = candidates[i];
                var recipe = recipes[candidate.Recipe];
                var terminal = recipe.Path[^1];
                if (terminal.ContinuationWindow <= 0) continue;
                if (!usable && TryGetScore(slot, recipe.Order[^1], out var single) && single.LeftOnly) continue;
                // A repeated starting seal can also be the second or third step of the same combo.
                if (branchHistory.HasDeeperPurePath(terminal.Depth, frame.time))
                {
                    candidates.RemoveAt(i);
                    continue;
                }
                if (frame.time - candidate.Evidence.ActionStart > terminal.ContinuationWindow + Epsilon) continue;
                candidates.RemoveAt(i);
                if (!usable) continue;
                heldShort = candidate.Evidence;
                branchTerminal = terminal;
                branchStartedAt = heldShort.ActionStart;
                lastMatchingAt = branchObservedAt = lastBranchFrameAt = observed;
                departingSince = double.NaN;
                shortProgress = 0;
            }

            if (heldShort == null || shortProgress + Epsilon < shortSealHold) return;
            heldShort.End = frame.time;
            candidates.Add(new Candidate(heldShort.Recipe, heldShort));
            EndBranch();
        }

        private void RecordSequence(SealHistory history, SealNode node, double start, double time)
        {
            foreach (int index in node.Finishes)
            {
                var recipe = recipes[index];
                // Single seals have a separate hold/rearm tracker. Discounted recipes own their threshold history.
                if (recipe.Sequence != history || time - start > recipe.Window + Epsilon) continue;
                var complete = new Evidence(index, ++nextAction, Epoch, start, time, 1) { Buffered = true };
                evidence.Add(complete);
                if (evidence.Count > Capacity) { evidence[0].Buffered = false; evidence.RemoveAt(0); }
                if (recipe.Mode == SkillTriggerMode.GestureOnly) candidates.Add(new Candidate(index, complete));
            }
        }

        void RejectVoice(string reason) { LastVoiceRejection = reason; SetStatus(reason); }

        /// <summary>校验完整口令及结束、到达时间，先查历史手印，再建立等待后续手印的窗口。</summary>
        public void Voice(string phrase, double endedAt, double arrivedAt)
        {
            LastVoiceRejection = null;
            if (!initialized) { RejectVoice("匹配器尚未初始化"); return; }
            if (!Finite(endedAt) || !Finite(arrivedAt)) { RejectVoice("口令时间戳无效"); return; }
            if (endedAt > arrivedAt + Epsilon) { RejectVoice("口令结束时间晚于接收时间"); return; }
            if (arrivedAt - endedAt > MaximumVoiceLatency + Epsilon) { RejectVoice($"口令延迟 {arrivedAt-endedAt:F2} 秒，超过上限 {MaximumVoiceLatency:F2} 秒"); return; }
            if (arrivedAt > now + MaximumVoiceLatency + Epsilon) { RejectVoice("匹配器时钟落后于口令接收时间"); return; }
            now = Math.Max(now, arrivedAt); Prune();
            if (endedAt <= barrier) { RejectVoice("口令结束于取消或重置之前"); return; }
            if (endedAt < lastVoiceEnd) { RejectVoice("口令时间戳早于已处理口令"); return; }
            string normalized = SkillPhrase.Normalize(phrase);
            // Energy segmentation may give different recognized words the same end time.
            // Preserve capture time and reject only a replay of the same normalized word.
            if (endedAt == lastVoiceEnd && voicePhrasesAtLastEnd.Contains(normalized))
            { RejectVoice("相同口令时间戳重复"); return; }
            if (normalized == CancelPhrase) { RecordVoiceEnd(normalized, endedAt); Cancel(now); return; }
            int selected = -1;
            for (int i = 0; i < recipes.Length; i++)
                if (recipes[i].Enabled && recipes[i].Mode != SkillTriggerMode.GestureOnly && recipes[i].Phrase == normalized) { selected = i; break; }
            if (selected < 0) { RejectVoice("未匹配完整技能口令" + (configurationError == null ? "" : "；" + configurationError)); return; }
            RecordVoiceEnd(normalized, endedAt);
            expiredPending = -1;
            if (pending >= 0 && pending != selected) ClearContext(now, false);
            var recipe = recipes[selected];
            if (waitingForCondition && pending == selected) return; // Repeating the word does not extend its window.
            pending = -1;
            if (recipe.Mode == SkillTriggerMode.VoiceOnly) { Attempt(selected, null); return; }
            voiceEnd = endedAt; pending = selected;
            if (FindEvidence(selected, endedAt, out var match)) { Match(selected, match, false); return; }
            if (now > PendingDeadline + Epsilon) { pending = -1; RejectVoice("口令已超过组合窗口，请重新输入"); return; }
            SetStatus(Name(recipe.Definition) + "：口令已确认，等待" + recipe.Seal);
        }

        private void RecordVoiceEnd(string phrase, double endedAt)
        {
            if (endedAt > lastVoiceEnd) voicePhrasesAtLastEnd.Clear();
            lastVoiceEnd = endedAt;
            // Only enabled catalog phrases and the configured cancel phrase reach this point.
            voicePhrasesAtLastEnd.Add(phrase);
        }

        /// <summary>设置取消时间屏障并清除证据，使旧口令或旧手印无法在取消后补发。</summary>
        public void Cancel(double time)
        {
            if (!Finite(time)) return;
            ClearContext(time, false); SetStatus("已取消施法；旧输入已清除。可退出重结，组合技也可持续保持同印并重新念咒");
        }

        /// <summary>清除上下文并允许时钟重新起算，供启停、暂停恢复及时间回退使用。</summary>
        public void Reset(double time)
        {
            if (!Finite(time)) return;
            ClearContext(time, true); SetStatus(configurationError ?? "已重置输入，等待新的口令与手印");
        }

        // Start a new permission epoch without replaying a spell or reporting the same held pose twice.
        internal void BeginAfterPermissionChange(SkillMatchEngine previous, double boundary)
        {
            Epoch = previous.Epoch;
            Reset(boundary);
            lastAttemptFrame = previous.lastAttemptFrame;
            lastAttemptTime = previous.lastAttemptTime;
            feedbackHistory.KeepHeldPose(previous.feedbackHistory);
            leftFeedback.KeepHeldPose(previous.leftFeedback);
            branchHistory?.KeepHeldPose(previous.branchHistory ?? previous.histories[0]);
            string deferredSeal = previous.heldShort == null ? null : previous.recipes[previous.heldShort.Recipe].Order[^1];
            foreach (var state in seals.Values)
                if (state.Seal == deferredSeal || (previous.seals.TryGetValue(state.Seal, out var old) && old.Blocked))
                {
                    state.Blocked = true;
                    state.BlockedAt = boundary;
                }
        }

        /// <summary>推进 Epoch 并设置时间屏障，同时清空缓存和等待项，隔离清理前后的输入。</summary>
        private void ClearContext(double time, bool resetClock)
        {
            EndBranch();
            now = resetClock ? time : Math.Max(now, time); initialized = true; barrier = now; Epoch++;
            pending = expiredPending = -1;
            SetCastConditionWait(null);
            foreach (var history in histories) history.Interrupt(resetClock);
            leftFeedback.Interrupt(resetClock);
            lastLeftObservation = double.NegativeInfinity;
            foreach (var item in evidence) item.Buffered = false;
            evidence.Clear(); bufferHead = bufferCount = 0; hasPrevious = false; lastSample = double.NegativeInfinity;
            nextObservationDiagnostic = double.NegativeInfinity;
            foreach (var state in seals.Values)
            {
                state.Blocked |= state.Action != 0;
                state.BlockedAt = now; state.ExitSince = double.NaN; state.ResetTrackers();
            }
            if (resetClock) { lastVoiceEnd = double.NegativeInfinity; voicePhrasesAtLastEnd.Clear(); lastAttemptTime = double.NegativeInfinity; }
        }

        /// <summary>多个不同技能同时满足时拒绝本轮输入；唯一技能才进入激活尝试。</summary>
        private void ResolveCandidates()
        {
            if (candidates.Count == 0) return;
            int selected = candidates[0].Recipe;
            for (int i = 1; i < candidates.Count; i++) if (candidates[i].Recipe != selected)
            {
                foreach (var item in candidates) if (item.Evidence != null) Consume(item.Evidence);
                SetCastConditionWait(null);
                pending = expiredPending = -1; SetStatus("技能冲突，未释放；请重新输入"); return;
            }
            Match(selected, candidates[0].Evidence, true);
        }

        private void Match(int index, Evidence item, bool observedThisFrame)
        {
            var recipe = recipes[index];
            if (recipe.Definition.castCondition == SkillCastCondition.None || item == null)
            {
                Attempt(index, item);
                return;
            }
            // Claim this physical input once, while leaving resource and cooldown settlement to Attempt.
            // A delayed word may refer to a pose which has already been released.
            Consume(item);
            if (recipe.Mode == SkillTriggerMode.GestureOnly) voiceEnd = item.End;
            pending = index;
            expiredPending = -1;
            if (now > PendingDeadline + Epsilon || !HeldSince(recipe, item.End))
            {
                StopWaitingForCondition();
                return;
            }
            SetCastConditionWait(recipe.Definition);
            if (observedThisFrame && IsCastConditionMet?.Invoke(recipe.Definition) == true)
                Attempt(index, null);
            else
                SetStatus(Name(recipe.Definition) + (recipe.Definition.sealReleaseWristDistance > 0
                    ? "：已认印，双手保持合拢并移到嘴前" : "：保持手印并移到嘴前"));
        }

        private bool HeldSince(Recipe recipe, double matchedAt)
        {
            double next = now;
            for (int i = bufferCount - 1; i >= 0; i--)
            {
                var slot = slots[(bufferHead + i) % Capacity];
                if (next - slot.Frame.time > MaximumSampleGap + Epsilon ||
                    !RecognizedSealStillHeld(recipe, slot, next)) return false;
                if (slot.Frame.time <= matchedAt + Epsilon) return true;
                next = slot.Frame.time;
            }
            return false;
        }

        // Only used after the recipe has already earned its full gesture evidence.
        private bool RecognizedSealStillHeld(Recipe recipe, Slot slot, double time)
        {
            float releaseDistance = recipe.Definition.sealReleaseWristDistance;
            if (releaseDistance > 0)
                return FreshHands(slot.Frame, time) && WristsTogether(slot.Frame, releaseDistance);
            return FinalHandsFresh(recipe, slot, time) && FinalSealMatches(recipe, slot);
        }

        private static bool WristsTogether(SealFrame frame, float releaseDistance)
        {
            int wrist = (int)Oculus.Interaction.Input.HandJointId.HandWristRoot;
            bool HasWrist(HandSnapshot hand) => hand.jointValid != null && hand.joints != null &&
                wrist < hand.jointValid.Length && wrist < hand.joints.Length && hand.jointValid[wrist];
            if (!HasWrist(frame.left) || !HasWrist(frame.right)) return false;
            Vector3 separation = frame.left.joints[wrist].position - frame.right.joints[wrist].position;
            return separation.sqrMagnitude < releaseDistance * releaseDistance;
        }

        private void SetCastConditionWait(SkillDefinition skill)
        {
            waitingForCondition = skill != null;
            CastConditionWaitChanged?.Invoke(skill);
        }

        private void StopWaitingForCondition()
        {
            pending = expiredPending = -1;
            SetCastConditionWait(null);
            SetStatus("施法条件等待结束，请重新输入");
        }

        /// <summary>先消费本次输入证据，同帧至多提交一次施法请求。</summary>
        private void Attempt(int index, Evidence item)
        {
            EndBranch();
            // Stop matching before activation, but keep the prepared pose available until it is consumed.
            waitingForCondition = false;
            pending = expiredPending = -1; if (item != null) Consume(item);
            var recipe = recipes[index];
            bool sameFrame = Application.isPlaying ? lastAttemptFrame == Time.frameCount : Math.Abs(now - lastAttemptTime) <= Epsilon;
            if (committing || sameFrame)
            {
                CastConditionWaitChanged?.Invoke(null);
                SetStatus("本帧已有激活尝试；本次输入已消费，请重试"); return;
            }
            lastAttemptTime = now; lastAttemptFrame = Application.isPlaying ? Time.frameCount : -1;
            bool success = false;
            committing = true;
            try { success = TryActivate != null && TryActivate(recipe.Definition); }
            catch (Exception ex) { SetStatus("激活异常：" + ex.Message + "；本次输入已消费"); return; }
            finally { committing = false; CastConditionWaitChanged?.Invoke(null); }
            if (success) SetStatus(Name(recipe.Definition) + "已激活");
            else SetStatus(Name(recipe.Definition) + "激活失败；本次输入已消费，请重新输入");
        }

        /// <summary>按物理动作编号消费全部关联证据，防止同一次手印被多个配方重复使用。</summary>
        private void Consume(Evidence item)
        {
            EndBranch();
            // Each path owns its hold interval. Invalidate shared physical input even
            // when another recipe applied different thresholds to those same samples.
            foreach (var history in histories) history.ConsumeThrough(item.End);
            for (int i = evidence.Count - 1; i >= 0; i--)
            {
                var other = evidence[i];
                if ((recipes[item.Recipe].Sequence != null || recipes[other.Recipe].Sequence != null) &&
                    other.ActionStart <= item.End && other.End >= item.ActionStart)
                { other.Buffered = false; evidence.RemoveAt(i); }
            }
            DiscardAction(item.Action);
            foreach (var state in seals.Values) if (state.Action == item.Action ||
                (recipes[item.Recipe].Sequence != null && state.Trackers.Exists(t =>
                    t.Current != null && t.Current.ActionStart <= item.End && t.Current.End >= item.ActionStart)))
            {
                state.Blocked = true; state.BlockedAt = now; state.ExitSince = double.NaN;
                // 只有消费当下仍然合格的新鲜采样，才可作为下一段保持区间的起点。
                // 迟到口令匹配历史证据时，不能把新一轮保持的起点倒推到过去。
                state.ResetTrackers(Math.Abs(lastSample - now) <= Epsilon ? now : double.NaN);
            }
            nextObservationDiagnostic = double.NegativeInfinity;
        }

        private void DiscardAction(long action)
        {
            if (action == 0) return;
            for (int i = evidence.Count - 1; i >= 0; i--)
                if (evidence[i].Action == action) { evidence[i].Buffered = false; evidence.RemoveAt(i); }
        }

        /// <summary>在同一 Epoch 内查找完整保持区间落入组合窗口的证据，并选择时间最近的一份。</summary>
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

        private Slot AddFrame(SealFrame frame, IReadOnlyList<SealScore> scores, bool bothValid, bool leftValid)
        {
            if (bufferCount == Capacity) { bufferHead = (bufferHead + 1) % Capacity; bufferCount--; }
            int index = (bufferHead + bufferCount++) % Capacity;
            var slot = slots[index] ?? (slots[index] = new Slot());
            CopyFrame(frame, slot.Frame);
            CopyScores(scores, slot, bothValid, leftValid, frame.left.highConfidence);
            return slot;
        }

        private void CopyScores(IReadOnlyList<SealScore> scores, Slot slot, bool bothValid, bool leftValid, bool leftHighConfidence)
        {
            if (slot.Scores.Length < scores.Count) slot.Scores = new ScoreFact[scores.Count];
            slot.ScoreCount = scores.Count;
            for (int i = 0; i < scores.Count; i++)
            {
                var profile = scores[i]?.profile;
                bool eligible = profile && profile.handMode == SealHandMode.LeftOnly
                    ? leftValid && (leftHighConfidence || profile.allowLowConfidence) : bothValid;
                slot.Scores[i] = new ScoreFact(scores[i], eligible && ValidateProfile(profile));
            }
        }

        // Effects can tick before the input controller processes this frame. Use the latest
        // live scores without inserting them twice into the discrete matching history.
        public bool IsCurrentSealHeld(SkillDefinition skill, SealFrame frame, IReadOnlyList<SealScore> scores, double time,
            double observationsAfter = double.NegativeInfinity, bool suppressLeftOnly = false, bool afterRecognition = false)
        {
            if (!skill || scores == null || frame?.left == null) return false;
            foreach (var recipe in recipes)
                if (recipe.Enabled && recipe.Id == skill.skillId && recipe.Order.Length > 0)
                {
                    bool bothValid = FreshHands(frame, time) && Math.Min(frame.left.observedAt, frame.right.observedAt) > observationsAfter;
                    if (afterRecognition && recipe.Definition.sealReleaseWristDistance > 0)
                        return bothValid && WristsTogether(frame, recipe.Definition.sealReleaseWristDistance);
                    bool leftValid = !suppressLeftOnly && FreshLeft(frame, time) && frame.left.observedAt > observationsAfter;
                    if (!bothValid && (recipe.Sequence != null || !leftValid)) return false;
                    CopyScores(scores, currentScores, bothValid, leftValid, frame.left.highConfidence);
                    return FinalSealMatches(recipe, currentScores);
                }
            return false;
        }

        private bool FreshHands(SealFrame frame, double time) => BothHandsValid(frame) &&
            Math.Max(frame.left.observedAt, frame.right.observedAt) <= time + Epsilon &&
            time - Math.Min(frame.left.observedAt, frame.right.observedAt) <= MaximumSampleGap + Epsilon;

        private bool FreshLeft(SealFrame frame, double time) => Flags(frame.left) == 7 && Finite(frame.left.observedAt) &&
            frame.left.observedAt <= time + Epsilon && time - frame.left.observedAt <= MaximumSampleGap + Epsilon;

        private bool FinalHandsFresh(Recipe recipe, Slot slot, double time) =>
            recipe.Sequence == null && TryGetScore(slot, recipe.Seal, out var target) && target.LeftOnly
                ? FreshLeft(slot.Frame, time) : FreshHands(slot.Frame, time);

        private bool FinalSealMatches(Recipe recipe, Slot scores) =>
            TryGetScore(scores, recipe.Order[^1], out var target) && target.Quality &&
            target.Score + Epsilon >= Threshold(recipe, target) && Unambiguous(scores, target);

        // 采用与 SealProfile.ValidateRules 相同的校验规则，复用临时集合以减少逐帧分配。
        // 评分历史只存不可变的评分事实，临时集合和可编辑模板不会写入历史缓存。
        private bool ValidateProfile(SealProfile profile)
        {
            if (!profile || !profile.calibrated || (profile.handMode != SealHandMode.BothHands && profile.handMode != SealHandMode.LeftOnly) ||
                !float.IsFinite(profile.threshold) || profile.threshold <= 0 || profile.threshold > 1 ||
                !float.IsFinite(profile.minimumCoverage) || profile.minimumCoverage <= 0 || profile.minimumCoverage > 1 ||
                !float.IsFinite(profile.holdSeconds) || profile.holdSeconds < 0 || profile.rules == null || profile.rules.Count == 0) return false;
            validationIds.Clear(); float total = 0;
            foreach (var rule in profile.rules)
            {
                if (rule == null || string.IsNullOrWhiteSpace(rule.featureId) || !validationIds.Add(rule.featureId) ||
                    (rule.weight > 0 && !SealProfile.AcceptsFeature(profile.handMode, rule.featureId)) ||
                    !float.IsFinite(rule.weight) || rule.weight < 0 || !float.IsFinite(rule.min) || !float.IsFinite(rule.max) ||
                    !float.IsFinite(rule.tolerance) || rule.min > rule.max || rule.tolerance <= 0) return false;
                total += rule.weight;
            }
            return total > 0 && float.IsFinite(total);
        }

        private static bool TryGetScore(Slot slot, string seal, out ScoreFact target)
        {
            target=default;bool found=false;
            for(int i=0;i<slot.ScoreCount;i++) if(slot.Scores[i].Seal==seal)
            { if(found)return false; target=slot.Scores[i];found=true; }
            return found;
        }

        private string DescribeObservation()
        {
            var slot=slots[(bufferHead+bufferCount-1)%Capacity];
            bool valid=BothHandsValid(slot.Frame);
            var text=new StringBuilder($"骨骼间隔 {diagnosticGap:F3}s / 上限 {MaximumSampleGap:F3}s；输入占用={diagnosticSuppressed}；双手有效={valid}");
            foreach(var state in seals.Values)
            {
                bool found=TryGetScore(slot,state.Seal,out var target);
                float best=-1;string other="无";
                for(int i=0;i<slot.ScoreCount;i++) if(slot.Scores[i].Seal!=target.Seal && slot.Scores[i].Quality && slot.Scores[i].Score>best)
                { best=slot.Scores[i].Score;other=slot.Scores[i].Seal; }
                foreach(var tracker in state.Trackers)
                {
                    var recipe=recipes[tracker.Recipe];
                    double observed = target.LeftOnly ? lastLeftObservation : lastSample;
                    double held=double.IsNaN(tracker.Start)?0:Math.Max(0,observed-tracker.Start);
                    text.Append($"\n{recipe.Id}/{state.Seal}：分数 {target.Score:P1} / {Threshold(recipe,target):P1}，质量通过={found&&target.Quality}，歧义通过={Unambiguous(slot,target)}；竞争印 {other} {best:P1}，要求领先 {Margin:P0}");
                    if(!state.Blocked)text.Append($"；保持 {held:F3}/{target.Hold:F3}s");
                    else if(recipe.Mode==SkillTriggerMode.GestureOnly)text.Append("；此动作已消耗，纯印须退出后重结");
                    else if(diagnosticSuppressed||diagnosticHeld)text.Append($"；组合重备暂停（{(diagnosticHeld?"手上有持有物":"其他交互占用输入")}），松开后重新计时");
                    else if(tracker.Current!=null && tracker.Current.Buffered)text.Append("；同印重备已就绪，需新的有效口令");
                    else text.Append($"；同印重备剩余 {Math.Max(0,Math.Max(ComboHeldRearmSeconds,target.Hold)-held):F2} 秒，需新的有效口令");
                }
            }
            foreach (var recipe in recipes)
                if (recipe.Enabled && recipe.Sequence != null)
                    text.Append($"\n{recipe.Id}：{recipe.Seal}；下一印 {recipe.Order[recipe.Sequence.PrefixLength(recipe, now)]}");
            return text.ToString();
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
        private static int Flags(HandSnapshot hand) => hand == null ? 0 : (hand.connected ? 1 : 0) | (hand.tracked ? 2 : 0) | (hand.fresh ? 4 : 0);
        private static bool BothHandsValid(SealFrame frame) => Flags(frame.left) == 7 && Flags(frame.right) == 7 &&
            Finite(frame.left.observedAt) && Finite(frame.right.observedAt) && Math.Abs(frame.left.observedAt - frame.right.observedAt) <= SealFeatureExtractor.MaximumHandSkew;

        private static void CopyFrame(SealFrame source, SealFrame destination)
        {
            destination.schema = source.schema; destination.time = source.time; destination.label = source.label;
            destination.skeleton = source.skeleton; destination.source = source.source;
            CopyHand(source.left, destination.left); CopyHand(source.right ?? MissingHand, destination.right);
        }
        static readonly HandSnapshot MissingHand = new();
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
            public readonly string[] Order = Array.Empty<string>();
            public readonly SealNode[] Path;
            public SealHistory Sequence;
            public Tracker SingleTracker;
            public readonly SkillTriggerMode Mode;
            public readonly float Window, Discount, Floor;
            public bool Enabled = true;
            public Recipe(SkillDefinition definition)
            {
                Definition = definition;
                if (!definition) { Id = Phrase = Seal = ""; return; }
                Id = definition.skillId; Phrase = SkillPhrase.Normalize(definition.phrase);
                Order = definition.GetSealSequence();
                Path = new SealNode[Order.Length];
                Seal = string.Join(" → ", Order);
                Mode = definition.triggerMode; Window = definition.matchWindow; Discount = definition.voiceDiscount;
                Floor = definition.thresholdFloor;
            }
        }
        private sealed class SealNode
        {
            public readonly int Depth;
            public double PureWindow, ContinuationWindow;
            public readonly Dictionary<string, SealNode> Children = new(StringComparer.Ordinal);
            public readonly List<int> Finishes = new();
            public SealNode(int depth) { Depth = depth; }
        }

        // Standard recipes read the same bounded player history. Voice discounts get their
        // own qualification timeline so a lower threshold cannot activate a stricter recipe.
        private sealed class SealHistory
        {
            readonly List<(SealNode node, double start)> paths = new();
            readonly Dictionary<string, List<Recipe>> thresholds = new(StringComparer.Ordinal);
            readonly bool recipeSpecific;
            string held, candidate;
            double since, exitSince = double.NaN;
            public string Confirmed { get; private set; }

            public SealHistory(Recipe recipe = null)
            {
                recipeSpecific = recipe != null;
                if (recipe != null) Include(recipe);
            }

            public void Include(Recipe recipe)
            {
                foreach (string seal in recipe.Order)
                {
                    if (!thresholds.TryGetValue(seal, out var rules))
                        thresholds.Add(seal, rules = new List<Recipe>());
                    if (!rules.Contains(recipe)) rules.Add(recipe);
                }
            }

            float Required(ScoreFact score)
            {
                if (!thresholds.TryGetValue(score.Seal, out var rules)) return score.Threshold;
                float required = recipeSpecific ? 1 : score.Threshold;
                foreach (var rule in rules) required = Math.Min(required, Threshold(rule,score));
                return required;
            }

            public bool HasDeeperPurePath(int depth, double time)
            {
                foreach (var (node, start) in paths)
                    if (node.Depth > depth && node.PureWindow > 0 && time - start <= node.PureWindow + Epsilon) return true;
                return false;
            }

            public int PrefixLength(Recipe recipe, double time, bool includeComplete = false)
            {
                // Paths are ordered oldest first, so the first matching suffix is the longest.
                foreach (var (node, start) in paths)
                {
                    if (node.Depth <= recipe.Path.Length && (includeComplete || node.Depth < recipe.Path.Length) && node == recipe.Path[node.Depth - 1] &&
                        time - start <= recipe.Window + Epsilon) return node.Depth;
                }
                return 0;
            }

            public void ForgetPath(SealNode node)
            {
                for (int i = paths.Count - 1; i >= 0; i--)
                    if (paths[i].node == node) paths.RemoveAt(i);
            }

            public void Interrupt(bool release = false, SealNode preserve = null)
            {
                if (preserve == null) paths.Clear();
                else for (int i = paths.Count - 1; i >= 0; i--)
                    if (paths[i].node != preserve) paths.RemoveAt(i);
                Confirmed = candidate = null;
                exitSince = double.NaN;
                if (release) held = null;
            }

            public void KeepHeldPose(SealHistory previous) => held = previous.held;

            public void InterruptHold(string seal)
            {
                if (candidate == seal) candidate = null;
                if (held == seal) exitSince = double.NaN;
            }

            public void ConsumeThrough(double end)
            {
                int count = 0;
                while (count < paths.Count && paths[count].start <= end) count++;
                if (count > 0) paths.RemoveRange(0,count);
                if (since <= end) candidate = null;
            }

            public void Observe(SkillMatchEngine engine, Slot slot, double time, bool leftFeedbackOnly = false)
            {
                Confirmed = null;
                if (held != null)
                {
                    bool released = TryGetScore(slot, held, out var prior) && prior.Quality &&
                        prior.Score + Epsilon < Required(prior);
                    double releasedAt = prior.LeftOnly ? slot.Frame.left.observedAt : time;
                    if (!released) exitSince = double.NaN;
                    else if (!prior.LeftOnly || slot.LeftAdvanced)
                    {
                        if (double.IsNaN(exitSince) || (prior.LeftOnly && !slot.LeftContiguous)) exitSince = releasedAt;
                        if (releasedAt - exitSince + Epsilon >= engine.ExitSeconds) held = null;
                    }
                }
                ScoreFact best = default;
                for (int i = 0; i < slot.ScoreCount; i++)
                {
                    var score = slot.Scores[i];
                    if (leftFeedbackOnly && !score.LeftOnly) continue;
                    if (score.Quality && score.Score + Epsilon >= Required(score) && (!best.Quality || score.Score > best.Score))
                        best = score;
                }
                if (!best.Quality || best.Seal == held || !TryGetScore(slot, best.Seal, out _) || !engine.Unambiguous(slot,best))
                { candidate = null; return; }
                if (best.LeftOnly && !slot.LeftAdvanced) return;
                double sampleAt = best.LeftOnly ? slot.Frame.left.observedAt : time;
                if (candidate != best.Seal || (best.LeftOnly && !slot.LeftContiguous)) { candidate = best.Seal; since = sampleAt; }
                if (sampleAt - since + Epsilon < best.Hold) return;
                Confirmed = held = best.Seal;
                candidate = null;
                exitSince = double.NaN;
                if (leftFeedbackOnly) return;
                // Advance every still-legal suffix and also allow this seal to start a new sequence.
                for (int i = paths.Count - 1; i >= 0; i--)
                {
                    var (node, start) = paths[i];
                    if (time - start > engine.HistorySeconds + Epsilon || !node.Children.TryGetValue(held, out var child))
                    { paths.RemoveAt(i); continue; }
                    paths[i] = (child, start);
                    engine.RecordSequence(this, child, start, time);
                }
                if (engine.root.Children.TryGetValue(held, out var first))
                    paths.Add((first, since));
            }
        }

        private sealed class SealState
        {
            public readonly string Seal;
            public readonly List<Tracker> Trackers = new();
            public long Action;
            public bool Blocked;
            public bool LeftOnly;
            public double BlockedAt;
            public double ExitSince = double.NaN;
            public SealState(string seal) { Seal = seal; }
            public void ResetTrackers(double seed = double.NaN)
            {
                Action=0;
                foreach(var item in Trackers){item.Start=double.IsNaN(item.Start)?double.NaN:seed;item.Complete=false;item.Current=null;}
            }
            public void Rearm() { Blocked=false;ResetTrackers(); }
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
            public readonly double ActionStart;
            public double Start, End;
            public float Score;
            public bool Buffered;
            public Evidence(int recipe, long action, long epoch, double start, double end, float score)
            { Recipe = recipe; Action = action; Epoch = epoch; ActionStart = Start = start; End = end; Score = score; }
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
            public bool LeftAdvanced, LeftContiguous;
            public ScoreFact[] Scores = Array.Empty<ScoreFact>();
            public int ScoreCount;
        }
        // 缓存评分所需的数值快照，避免持有调用方会继续修改的模板、规则和数组引用。
        private readonly struct ScoreFact
        {
            public readonly string Seal;
            public readonly float Score, Coverage, Threshold, MinimumCoverage, Hold;
            public readonly bool ValidProfile, MissingRequired, LeftOnly;
            public bool Quality => ValidProfile && !MissingRequired && float.IsFinite(Score) && float.IsFinite(Coverage) && Coverage + Epsilon >= MinimumCoverage;
            public ScoreFact(SealScore source, bool validProfile)
            {
                var profile = source?.profile;
                LeftOnly = profile && profile.handMode == SealHandMode.LeftOnly;
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
