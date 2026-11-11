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
        public Action<string> Log;
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
        private readonly double HistorySeconds, MaximumVoiceLatency, ExitSeconds, MaximumSampleGap, ComboHeldRearmSeconds;
        private const double Epsilon = 0.000001;
        private readonly float Margin;
        private readonly Recipe[] recipes;
        private readonly Dictionary<string, SealState> seals = new(StringComparer.Ordinal);
        private readonly List<Evidence> evidence = new();
        private readonly List<Candidate> candidates = new();
        private readonly Slot[] slots;
        private readonly HashSet<string> validationIds = new(StringComparer.Ordinal);
        private readonly HashSet<string> voicePhrasesAtLastEnd = new(StringComparer.Ordinal);
        private int bufferHead, bufferCount, pending = -1, expiredPending = -1, previousLeft, previousRight, previousFlags;
        private int lastAttemptFrame = -1;
        private long nextAction;
        private double now, voiceEnd, barrier = double.NegativeInfinity, lastVoiceEnd = double.NegativeInfinity;
        private double expiredVoiceEnd, expiredProcessingDeadline;
        private double lastSample = double.NegativeInfinity, lastAttemptTime = double.NegativeInfinity;
        private bool initialized, hasPrevious, committing;
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

        /// <summary>推进输入实时时钟并清理过期证据；时间回退时重置上下文。</summary>
        public void Tick(double time)
        {
            if (!Finite(time)) { SetStatus("输入时钟无效"); return; }
            if (initialized && time + Epsilon < now) { Reset(time); SetStatus("输入时钟回退，已清除匹配上下文"); return; }
            now = time; initialized = true; Prune();
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
        public void Observe(SealFrame frame, IReadOnlyList<SealScore> scores, bool suppressActivation = false, bool suppressHeldRearm = false)
        {
            if (!initialized || frame == null || frame.left == null || frame.right == null || scores == null || !Finite(frame.time)) return;
            if (frame.time <= barrier || frame.time + Epsilon < lastSample || frame.time > now + MaximumVoiceLatency) return;
            now = Math.Max(now, frame.time); Prune();
            int flags = Flags(frame.left) | (Flags(frame.right) << 3);
            if (hasPrevious && previousLeft == frame.left.version && previousRight == frame.right.version && previousFlags == flags) return;
            diagnosticGap=frame.time-lastSample;diagnosticSuppressed=suppressActivation;diagnosticHeld=suppressHeldRearm;
            bool contiguous = frame.time - lastSample <= MaximumSampleGap + Epsilon;
            previousLeft = frame.left.version; previousRight = frame.right.version; previousFlags = flags;
            hasPrevious = true; lastSample = frame.time;
            var slot = AddFrame(frame, scores);
            bool valid = BothHandsValid(frame);
            candidates.Clear();
            foreach (var state in seals.Values)
            {
                bool found = TryGetScore(slot, state.Seal, out var target);
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
                    if (double.IsNaN(tracker.Start)) tracker.Start = frame.time;
                    double requiredHold = state.Blocked ? Math.Max(ComboHeldRearmSeconds, target.Hold) : target.Hold;
                    if (frame.time - tracker.Start + Epsilon < requiredHold) continue;
                    bool firstCompletion = !tracker.Complete;
                    // 组合技能的证据尚未消费且手印持续合格时，可以滚动更新有效区间。
                    // 滚动时复用同一动作编号及证据对象；一旦质量不合格，
                    // 上方逻辑就会清除 Start，必须重新完成一次完整保持。
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

        public string LastVoiceRejection { get; private set; }
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
            pending = -1;
            if (recipe.Mode == SkillTriggerMode.VoiceOnly) { Attempt(selected, null); return; }
            voiceEnd = endedAt; pending = selected;
            if (FindEvidence(selected, endedAt, out var match)) { Attempt(selected, match); return; }
            if (now > PendingDeadline + Epsilon) { pending = -1; RejectVoice("口令已超过组合窗口，请重新输入"); return; }
            SetStatus(Name(recipe.Definition) + "：口令已确认，等待" + recipe.Seal + "印");
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

        /// <summary>推进 Epoch 并设置时间屏障，同时清空缓存和等待项，隔离清理前后的输入。</summary>
        private void ClearContext(double time, bool resetClock)
        {
            now = resetClock ? time : Math.Max(now, time); initialized = true; barrier = now; Epoch++;
            pending = expiredPending = -1;
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
                foreach (var item in candidates) Consume(item.Evidence);
                pending = expiredPending = -1; SetStatus("技能冲突，未释放；请重新输入"); return;
            }
            Attempt(selected, candidates[0].Evidence);
        }

        /// <summary>先消费本次输入证据，同帧至多提交一次施法请求。</summary>
        private void Attempt(int index, Evidence item)
        {
            pending = expiredPending = -1; if (item != null) Consume(item);
            var recipe = recipes[index];
            bool sameFrame = Application.isPlaying ? lastAttemptFrame == Time.frameCount : Math.Abs(now - lastAttemptTime) <= Epsilon;
            if (committing || sameFrame) { SetStatus("本帧已有激活尝试；本次输入已消费，请重试"); return; }
            lastAttemptTime = now; lastAttemptFrame = Application.isPlaying ? Time.frameCount : -1;
            bool success = false;
            committing = true;
            try { success = TryActivate != null && TryActivate(recipe.Definition); }
            catch (Exception ex) { SetStatus("激活异常：" + ex.Message + "；本次输入已消费"); return; }
            finally { committing = false; }
            if (success) SetStatus(Name(recipe.Definition) + "已激活");
            else SetStatus(Name(recipe.Definition) + "激活失败；本次输入已消费，请重新输入");
        }

        /// <summary>按物理动作编号消费全部关联证据，防止同一次手印被多个配方重复使用。</summary>
        private void Consume(Evidence item)
        {
            DiscardAction(item.Action);
            foreach (var state in seals.Values) if (state.Action == item.Action)
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

        // 采用与 SealProfile.ValidateRules 相同的校验规则，复用临时集合以减少逐帧分配。
        // 评分历史只存不可变的评分事实，临时集合和可编辑模板不会写入历史缓存。
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
                    double held=double.IsNaN(tracker.Start)?0:Math.Max(0,lastSample-tracker.Start);
                    text.Append($"\n{recipe.Id}/{state.Seal}：分数 {target.Score:P1} / {Threshold(recipe,target):P1}，质量通过={valid&&found&&target.Quality}，歧义通过={Unambiguous(slot,target)}；竞争印 {other} {best:P1}，要求领先 {Margin:P0}");
                    if(!state.Blocked)text.Append($"；保持 {held:F3}/{target.Hold:F3}s");
                    else if(recipe.Mode==SkillTriggerMode.GestureOnly)text.Append("；此动作已消耗，纯印须退出后重结");
                    else if(diagnosticSuppressed||diagnosticHeld)text.Append($"；组合重备暂停（{(diagnosticHeld?"手上有持有物":"其他交互占用输入")}），松开后重新计时");
                    else if(tracker.Current!=null && tracker.Current.Buffered)text.Append("；同印重备已就绪，需新的有效口令");
                    else text.Append($"；同印重备剩余 {Math.Max(0,Math.Max(ComboHeldRearmSeconds,target.Hold)-held):F2} 秒，需新的有效口令");
                }
            }
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
            public readonly float Window, Discount, Floor;
            public bool Enabled = true;
            public Recipe(SkillDefinition definition)
            {
                Definition = definition;
                if (!definition) { Id = Phrase = Seal = ""; return; }
                Id = definition.skillId; Phrase = SkillPhrase.Normalize(definition.phrase); Seal = definition.sealName;
                Mode = definition.triggerMode; Window = definition.matchWindow; Discount = definition.voiceDiscount;
                Floor = definition.thresholdFloor;
            }
        }
        private sealed class SealState
        {
            public readonly string Seal;
            public readonly List<Tracker> Trackers = new();
            public long Action;
            public bool Blocked;
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
        // 缓存评分所需的数值快照，避免持有调用方会继续修改的模板、规则和数组引用。
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
