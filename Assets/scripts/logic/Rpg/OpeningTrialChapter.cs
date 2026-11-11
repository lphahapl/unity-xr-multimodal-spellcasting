using System;
using System.Collections.Generic;
using System.IO;
using NeuGameMRTools.Runtime.Components;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace NarutoVR.SealCombat.Rpg
{
    /// <summary>
    /// 一条教学要求玩家完成的操作类型。到达、停稳、回蓝和校验在 Update 中检测；
    /// 其他操作主要由事件或训练回调确认，Listen 的推进由旁白配置决定。
    /// </summary>
    public enum OpeningAction { Listen, OpenWrist, HitTarget, Retreat, ClearRoom, ReachArea, InsertLeaf, CollectLeaf, RecoverMana, AbsorbBolt, VerifyWrist, StopAtMarker }

    /// <summary>
    /// 一条教学步骤，通过稳定的 lineId 连接所属阶段的旁白台词。
    /// 本类描述操作目标，音频播放及句间推进由旁白控制器负责。
    /// </summary>
    [Serializable]
    public sealed class OpeningLesson
    {
        /// <summary>作者指定的稳定台词 ID，与对话本句的 lineId 对应。</summary>
        public string id;
        /// <summary>旁白行正文为空时使用的备用字幕。</summary>
        [TextArea] public string subtitle;
        /// <summary>本句要求的玩家操作，决定采用哪一种成功判定。</summary>
        public OpeningAction action;
        /// <summary>到达或停稳教学的区域；通过玩家脚底坐标检测，不依赖触发器回调。</summary>
        public BoxCollider area;
        /// <summary>命中教学指定的训练靶；命中其他靶不计入本句。</summary>
        public SkillTrainingTarget target;
        [InspectorName("本句操作目标"), Tooltip("选填。绑定后按此单项或组合目标判断操作；清房仍由章节启用遭遇。")]
        public ChapterObjective operationTarget;
        [InspectorName("首次达成即记住"), Tooltip("到达一次即可完成的操作使用此项。持续条件在本句推进前仍会重新检查。")]
        public bool rememberTargetSuccess;
    }

    /// <summary>
    /// 玩家在一个阶段等待较久时播放的补充提示，不直接完成教学操作。
    /// </summary>
    [Serializable]
    public sealed class OpeningHint
    {
        /// <summary>相对 Resources/Narration 的音频路径；加载时移除文件扩展名。</summary>
        public string audioFile;
        /// <summary>提示的说话者；空白时使用当前引导台词的说话者。</summary>
        [Tooltip("留空时沿用本段当前台词的说话者。")] public string speaker;
        /// <summary>补充提示对应的字幕正文。</summary>
        [TextArea] public string subtitle;
    }

    /// <summary>
    /// 首章的一个阶段：关联 RPG 阶段、逐句教学、旁白、临时术式和场景表现。
    /// sections 的顺序必须与 LinearRpgController.stages 一致。
    /// </summary>
    [Serializable]
    public sealed class OpeningSection
    {
        /// <summary>
        /// 本段对应的 RPG 阶段资产，进入阶段时核对它与 RPG 同下标配置是否一致。
        /// </summary>
        public RpgStageDefinition stage;
        /// <summary>
        /// 本段的逐句旁白控制器，提供开始一句、结束一句和整段结束事件。
        /// </summary>
        public NarrationController narration;
        /// <summary>
        /// 教学步骤，按作者填写的稳定 ID 与 narration.lines 一一对应。
        /// </summary>
        public OpeningLesson[] lines;
        /// <summary>
        /// 仅在本阶段授权的练习技能 ID；临时授权本身不写入永久解锁存档。
        /// </summary>
        [Tooltip("此段可用的练习术，不写入角色存档。")] public string[] trialSkills = Array.Empty<string>();
        /// <summary>
        /// 阶段通过后关闭的门体或阻挡对象：当前及后续阶段保持激活，已完成阶段设为不激活。
        /// 这里通过 SetActive 实现放行，不负责门扇开启动画。
        /// </summary>
        public GameObject doorOpenedOnCompletion;
        [InspectorName("开始本段前的条件"), Tooltip("满足后才开始第一句话。已经开始的对话不会因玩家离开区域而重播。")]
        public ChapterObjective startCondition;
        /// <summary>
        /// 本阶段是否启用双盘解谜；开启后需要旁白结束且收到解谜成功事件才能通过。
        /// </summary>
        public bool alignDiscs;
        /// <summary>
        /// 本段按顺序播放的卡住提示；播放完数组后不再追加提示。
        /// </summary>
        public OpeningHint[] hints = Array.Empty<OpeningHint>();
    }

    /// <summary>
    /// 首章流程协调器：根据 RPG 当前阶段启动教学，等待旁白与操作条件完成，再请求推进。
    /// 具体施法、战斗、解谜、吸附和存档由对应组件执行，本类负责接线与结果确认。
    /// 阶段内的台词位置只保存在运行内存中；恢复阶段时重新启动该段对话。
    /// </summary>
    [DefaultExecutionOrder(320), DisallowMultipleComponent]
    public sealed class OpeningTrialChapter : RpgChapterFlow
    {
        /// <summary>
        /// 主 RPG 控制器，提供行动状态、当前阶段、玩家资源及进度保存入口。
        /// </summary>
        public LinearRpgController rpg;
        /// <summary>
        /// Awake 时应用到移动组件的探索步速；运行中修改本字段不会在每帧自动同步。
        /// </summary>
        [InspectorName("首章探索步速（米／秒）"), Min(.05f)] public float explorationWalkSpeed = 1.25f;
        /// <summary>
        /// 清房教学使用的遭遇控制器，在对应台词期间启用并监听清场结果。
        /// </summary>
        public CombatRoomEncounter encounter;
        /// <summary>
        /// 首章阶段列表，按 RPG 阶段下标读取；长度也用作章节结束的哨兵下标。
        /// </summary>
        public OpeningSection[] sections;
        /// <summary>
        /// 交给字幕呈现器管理的 UI 文本组件。
        /// </summary>
        public Text subtitle;
        /// <summary>
        /// 双盘机关、奖励铜叶、入匣状态及腕匣校验的交互组件。
        /// </summary>
        public OpeningLeafPuzzle puzzle;
        /// <summary>
        /// 成长物品模块；首章只报告真实拾取和入匣成功，不自行计算技能奖励。
        /// </summary>
        public RpgItemInventory inventory;
        /// <summary>教学使用的训练设施，生命与复位由该组件配置。</summary>
        public TrainingTargetGroup targetGroup;
        /// <summary>新角色了解的基础术式；临时施放由各教学阶段授权。</summary>
        public string[] initialDiscoveries = Array.Empty<string>();
        /// <summary>首章完成该安全校验阶段后才允许永久术式施放。</summary>
        public RpgStageDefinition permanentSkillGate;
        /// <summary>
        /// 左腕吸附区；只有收到吸附完成事件并验证铜叶身份，才确认入匣操作。
        /// </summary>
        public SnapZone wristSlot;
        /// <summary>
        /// 执行护盾承伤和后撤训练，并通过回调报告成功。
        /// </summary>
        public OpeningDefenseDrill defense;
        /// <summary>
        /// 首章完成后的检查点，供 RPG 控制器保存和恢复最终落点。
        /// </summary>
        public NavigationNode finalCheckpoint;
        /// <summary>
        /// 补充提示和手动重听使用的音源，与主线旁白控制器的音源分开。
        /// </summary>
        public AudioSource promptVoice;
        /// <summary>
        /// 恢复度力教学的完成比例，默认达到最大度力的 95%。
        /// </summary>
        [Range(.1f,1)] public float recoveredManaRatio = .95f;

        /// <summary>
        /// 当前激活的阶段；清理阶段或首章结束时为 null。
        /// </summary>
        OpeningSection section;
        /// <summary>
        /// 当前等待玩家完成的教学步骤；不是整段旁白的独立进度存档。
        /// </summary>
        OpeningLesson line;
        NarrationController.Step boundStep;
        long dialogueGeneration;
        Action onOperation, onCollected;
        Action<ChapterObjective> onObjectiveChanged, onEntryChanged;
        Action onPuzzleSolved;
        UnityAction onDialogueCompleted;
        object sectionBinding;
        Action<bool> onWrist;
        Action<GameObject> onInserted;
        Action<SkillTrainingTarget> onHit;
        bool refreshingBinding;
        // Only hits earned after this section starts may satisfy its later instruction.
        readonly HashSet<SkillTrainingTarget> earlyTargetHits = new();
        /// <summary>
        /// 负责字幕定位、显示时序和暂停处理的呈现器。
        /// </summary>
        OpeningSubtitlePresenter captions;
        /// <summary>
        /// 依次表示整段对话结束、阶段已启动、双盘已解开、需要从进度重建机关状态。
        /// </summary>
        bool dialogueFinished, started, puzzleAligned, restoreWorld = true;
        /// <summary>
        /// 停稳教学连续满足条件的时间；离开区域或恢复移动时清零。
        /// </summary>
        float stopped;
        /// <summary>
        /// 本阶段下一条补充提示的下标，离开阶段时归零。
        /// </summary>
        int hintIndex;
        /// <summary>
        /// 满足提示条件时累计的等待时间；条件不满足时暂停累计，并非每次都清零。
        /// </summary>
        float waiting;
        /// <summary>
        /// 是否正在播放补充提示，或主线当前句的音频尚未完成。
        /// </summary>
        public override bool Speaking => (promptVoice && promptVoice.isPlaying) ||
            (section?.narration && section.narration.CurrentStep != null && !section.narration.AudioCompleted);
        bool CanPlayPrompt => promptVoice && promptVoice.isActiveAndEnabled &&
            (!AudioListener.pause || promptVoice.ignoreListenerPause);
        /// <summary>
        /// 当前已接入的阶段下标；-1 表示尚未接入，sections.Length 表示首章结束。
        /// </summary>
        int index = -1;
        /// <summary>
        /// 本地阶段与 RPG 阶段一致、整段旁白结束，且所需双盘解谜完成时，允许请求过关。
        /// </summary>
        public override bool ObjectiveMet => section != null && index == rpg.StageIndex && dialogueFinished && (!section.alignDiscs || puzzleAligned);
        /// <summary>
        /// 供外部 UI 读取的当前目标文字：优先当前台词，最后回退到阶段说明。
        /// </summary>
        public override string ObjectiveText => rpg.CampaignComplete ? "已抵达测候台，首章完成" :
            line != null ? LineText(section?.narration?.CurrentLine, line) : section?.stage.instructions ?? "";
        /// <summary>
        /// 检查指定技能 ID 是否属于当前阶段的临时练习授权。
        /// </summary>
        public override bool AllowsTrialSkill(string id)
        {
            // 章节授权随已恢复的阶段立即生效。旁白与场景物件会在随后的 Update 接入。
            int currentStage = ActiveStageIndex;
            if (sections == null || currentStage < 0 || currentStage >= sections.Length) return false;
            var allowed = sections[currentStage]?.trialSkills;
            return allowed != null && Array.IndexOf(allowed, id) >= 0;
        }

        /// <inheritdoc/>
        public override LinearRpgController Owner => rpg;
        /// <inheritdoc/>
        public override string[] InitialDiscoveries => initialDiscoveries;
        /// <inheritdoc/>
        public override bool AllowsPermanentSkill(string id) => rpg.Model != null &&
            rpg.Model.Chapter.IsComplete(permanentSkillGate.stageId);
        /// <inheritdoc/>
        public override NavigationNode FinalCheckpoint => finalCheckpoint;
        /// <inheritdoc/>
        public override string InitialStatus => "试炼室 · 等待腕匣引导";
        /// <inheritdoc/>
        public override bool UsesWorldNavigationMarks => true;
        /// <inheritdoc/>
        public override bool UsesExplorationFootsteps => true;
        /// <inheritdoc/>
        public override int GlyphPreviewPages => 1;

        /// <summary>首章的双训练靶按本阶段参数重置；通用主控不再承担训练靶复位。</summary>
        public override void OnStageEntered()
        {
            targetGroup.ResetForStage(rpg.StageIndex);
        }

        /// <summary>首章专属接线检查留在首章，通用 RPG 无需知道铜叶机关的结构。</summary>
        public override void ValidateChapter(LinearRpgController owner)
        {
            base.ValidateChapter(owner);
            if (!targetGroup) throw new InvalidOperationException("首章教学训练靶组缺失");
            targetGroup.Validate(owner.stages.Length);
            if (!inventory || inventory.Owner != owner || !inventory.catalog || !puzzle || puzzle.inventory != inventory)
                throw new InvalidOperationException("首章物品模块或实物机关接线缺失");
            if (sections == null || sections.Length != owner.stages.Length)
                throw new InvalidOperationException("首章阶段数量与 RPG 配置不一致");
            for (int i = 0; i < sections.Length; i++)
                if (sections[i] == null || sections[i].stage != owner.stages[i] || !sections[i].narration ||
                    sections[i].lines == null || sections[i].lines.Length == 0 ||
                    sections[i].lines.Length != sections[i].narration.lines.Count)
                    throw new InvalidOperationException("首章步骤与旁白接线不一致：" + i);
            if (!permanentSkillGate || !Array.Exists(owner.stages, s => s == permanentSkillGate) || inventory.catalog.Find(puzzle.leafId) == null)
                throw new InvalidOperationException("首章校验阶段或奖励物品配置缺失");
            var usedObjectives = new HashSet<ChapterObjective>();
            var lineIds = new HashSet<string>(StringComparer.Ordinal);
            var doors = new HashSet<GameObject>();
            foreach (var authored in sections)
            {
                var door = authored.doorOpenedOnCompletion;
                if (door && (!doors.Add(door) || (sceneGuidance && sceneGuidance.ControlsVisibility(door))))
                    throw new InvalidOperationException($"门体 {door.name} 只能由一个阶段的完成条件控制显隐");
                if (authored.startCondition) ChapterObjective.ValidateTree(authored.startCondition, usedObjectives);
                foreach (var lesson in authored.lines)
                {
                    if (lesson == null || string.IsNullOrWhiteSpace(lesson.id) || !lineIds.Add(lesson.id))
                        throw new InvalidOperationException("教学操作缺少唯一的台词 ID");
                    if ((lesson.action == OpeningAction.ReachArea || lesson.rememberTargetSuccess) && !lesson.operationTarget)
                        throw new InvalidOperationException("本句需要配置操作目标：" + lesson.id);
                    if (lesson.operationTarget) ChapterObjective.ValidateTree(lesson.operationTarget, usedObjectives);
                }
                var paired = new HashSet<string>(StringComparer.Ordinal);
                foreach (var row in authored.narration.lines)
                {
                    if (row == null) throw new InvalidOperationException("教学旁白不能包含空行：" + authored.stage.stageId);
                    string id = string.IsNullOrEmpty(row.lineId)
                        ? Path.GetFileNameWithoutExtension((row.audioFileName ?? "").Replace('\\','/')) : row.lineId;
                    if (string.IsNullOrEmpty(id) || !Array.Exists(authored.lines, lesson => lesson.id == id) || !paired.Add(id))
                        throw new InvalidOperationException("旁白与教学的稳定台词 ID 不匹配：" + authored.stage.stageId);
                    // 旧场景只在音频名与本段唯一作者键精确匹配时获得运行时身份。
                    row.legacyLineId = string.IsNullOrEmpty(row.lineId) ? id : null;
                }
            }
            foreach (var section in sections)
                if (section.trialSkills == null || Array.Exists(section.trialSkills, id =>
                    !Array.Exists(initialDiscoveries, known => known == id) || !owner.skills.catalog.skills.Exists(skill => skill.skillId == id)))
                    throw new InvalidOperationException("首章试用技能必须属于初始了解的真实配方");
        }

        /// <summary>
        /// 应用探索步速，并在字幕、移动组件和头部引用齐全时创建字幕呈现器。
        /// </summary>
        void Awake()
        {
            if (!rpg || rpg.chapter != this) return;
            if (rpg && rpg.locomotion)
                rpg.locomotion.exploreSpeed = Mathf.Max(.05f, explorationWalkSpeed);
            if (subtitle && rpg && rpg.locomotion && rpg.locomotion.head)
                captions = new OpeningSubtitlePresenter(subtitle, rpg.locomotion.head);
        }
        /// <summary>
        /// 订阅检查点恢复事件，并在渲染前更新字幕位置以跟随最新头部姿态。
        /// </summary>
        protected override void OnEnable()
        {
            base.OnEnable();
            if (rpg) rpg.CheckpointRestored += Restore;
            if (captions != null) Application.onBeforeRender += captions.Place;
        }
        /// <summary>
        /// 解除生命周期事件并离开当前阶段；下次启用时重新接入阶段、重建机关状态。
        /// </summary>
        protected override void OnDisable()
        {
            if (captions != null) Application.onBeforeRender -= captions.Place;
            if (rpg) rpg.CheckpointRestored -= Restore;
            LeaveSection();
            index = -1;
            restoreWorld = true;
            base.OnDisable();
        }

        /// <summary>
        /// 每帧更新字幕位置与显示进度；不能行动或游戏暂停时通知呈现器暂停处理。
        /// </summary>
        void LateUpdate()
        {
            captions?.Place();
            captions?.Tick(!rpg || !rpg.CanAct || Time.timeScale <= 0);
        }

        /// <summary>
        /// 应用进入后台时立即通知字幕呈现器暂停处理。
        /// </summary>
        void OnApplicationPause(bool paused)
        {
            if (paused) captions?.Tick(true);
        }

        /// <summary>
        /// 同步 RPG 阶段，按入口区域启动旁白，并轮询需要连续检测的操作条件。
        /// 其他操作通过 BeginLine 注册的事件回报；最终过关仍由 RPG 控制器确认。
        /// </summary>
        void Update()
        {
            if (!rpg || rpg.chapter != this || !rpg.CanAct) return;
            int expected = rpg.CampaignComplete ? sections.Length : rpg.StageIndex;
            if (index != expected) EnterSection(expected);
            TryStartSection(section);
            if (line != null && !line.operationTarget && CanActOn(boundStep))
                switch (line.action)
                {
                    case OpeningAction.StopAtMarker:
                        // 持续发出行走指令却被墙挡住，不算完成“收手停稳”教学。
                        stopped = ReachAreaChapterObjective.ContainsFloor(line.area, rpg.locomotion.PlayerFloorPosition) && !rpg.locomotion.NavigationActive &&
                            !rpg.locomotion.ContinuousMotionActive ? stopped + Time.deltaTime : 0;
                        if (stopped >= .5f) Accept(boundStep);
                        break;
                    case OpeningAction.RecoverMana: if (rpg.Player.Mana >= recoveredManaRatio * rpg.Player.MaxMana) Accept(boundStep); break;
                    case OpeningAction.VerifyWrist: if (puzzle.VerifiesWrist) Accept(boundStep); break;
                }
            TickHints();
            if (ObjectiveMet && rpg.CompleteChapterObjective(this))
                EnterSection(rpg.CampaignComplete ? sections.Length : rpg.StageIndex);
        }

        /// <summary>
        /// 仅在玩家可交互、没有语音或 UI 占用、右手追踪新鲜等条件满足时累计等待时间。
        /// 首次累计 18 秒触发提示，后续每累计 25 秒播放下一条；切换阶段重置计数。
        /// </summary>
        void TickHints()
        {
            if (section == null || !started || section.hints == null || hintIndex >= section.hints.Length || !CanPlayPrompt ||
                rpg.UiInputOwned || Speaking || (!section.alignDiscs && (line == null || line.action == OpeningAction.Listen)) ||
                (section.startCondition && !section.startCondition.IsComplete)) return;
            var hand = rpg.wristMenu.LatestInput;
            if (!WristMenuState.Fresh(hand.rightValid, hand.rightObserved, Time.realtimeSinceStartupAsDouble, rpg.wristMenu.settings.maxAge)) return;
            waiting += Time.deltaTime;
            if (waiting < (hintIndex == 0 ? 18 : 25)) return;
            while (hintIndex < section.hints.Length)
            {
                var hint = section.hints[hintIndex++];
                var clip = NarrationLine.LoadClip(hint?.audioFile);
                if (!clip)
                {
                    Debug.LogWarning($"{section.stage.stageId} 的第 {hintIndex} 条卡住提示缺少可播放音频：{hint?.audioFile}", this);
                    continue;
                }
                string speaker = string.IsNullOrWhiteSpace(hint.speaker) ? GuidanceLine().speaker : hint.speaker;
                PlayPrompt(clip, speaker, hint.subtitle);
                waiting = 0;
                break;
            }
        }

        /// <summary>
        /// 重听当前引导；整段旁白已经结束时重听该段最后一句。
        /// 只播放提示音频和字幕，不重置旁白流程，也不重复提交教学操作。
        /// </summary>
        public override void ReplayGuidance()
        {
            if (section == null || !started || Speaking || !rpg.CanAct || !CanPlayPrompt) return;
            if (section.narration.CurrentLineIndex < 0 && !section.narration.DialogueCompleted) return;
            var row = GuidanceLine();
            var lesson = Array.Find(section.lines, item => item.id == row.EffectiveLineId);
            var clip = NarrationLine.LoadClip(row.audioFileName);
            if (!clip) return;
            if (section.narration.IsCurrentStep(boundStep))
            {
                refreshingBinding = true;
                try { if (!section.narration.RefreshOperationBinding()) return; }
                finally { refreshingBinding = false; }
            }
            PlayPrompt(clip, row.speaker, LineText(row, lesson));
        }

        /// <summary>
        /// 取当前旁白行；旁白结束后使用本段最后一行，供补充提示继承说话者。
        /// </summary>
        NarrationLine GuidanceLine()
        {
            var narration = section.narration;
            return narration.CurrentLine ?? narration.lines[narration.lines.Count - 1];
        }

        /// <summary>
        /// 优先使用旁白控制器中的正文；为空时回退到教学步骤的备用字幕。
        /// </summary>
        static string LineText(NarrationLine row, OpeningLesson lesson) =>
            string.IsNullOrWhiteSpace(row?.text) ? lesson?.subtitle ?? string.Empty : row.text;

        /// <summary>
        /// 从头播放已解析的提示音频，并同步字幕。
        /// </summary>
        void PlayPrompt(AudioClip clip, string speaker, string caption)
        {
            promptVoice.Stop();
            promptVoice.clip = clip;
            promptVoice.timeSamples = 0;
            promptVoice.Play();
            captions?.Begin(promptVoice, speaker, caption);
        }

        /// <summary>
        /// 响应 RPG 已恢复检查点的通知；根据已恢复的阶段重新接线并重建机关表现。
        /// 本方法不自行读磁盘，也不恢复退出前的台词下标或音频播放秒数。
        /// </summary>
        void Restore()
        {
            if (!rpg || rpg.chapter != this) return;
            restoreWorld = true;
            EnterSection(rpg.CampaignComplete ? sections.Length : rpg.StageIndex);
        }

        /// <summary>
        /// 清理旧阶段，按已完成的阶段放行门体，必要时从进度恢复机关。
        /// 核对旁白与教学数组接线后注册事件；真正开始对话仍需 Update 检查入口区域。
        /// </summary>
        void EnterSection(int next)
        {
            rpg.RefreshProgress();
            LeaveSection();
            index = next;
            for (int i = 0; i < sections.Length; i++)
                if (sections[i].doorOpenedOnCompletion) sections[i].doorOpenedOnCompletion.SetActive(i >= index);
            if (restoreWorld)
            {
                puzzle.Restore(rpg.Model.Chapter.CompletedStages);
                restoreWorld = false;
            }
            if (index == sections.Length)
            {
                return;
            }
            section = sections[index];
            if (!section.narration || section.stage != rpg.stages[index] || section.lines == null ||
                section.lines.Length != section.narration.lines.Count || section.lines.Length == 0)
            {
                Debug.LogError("首章步骤与旁白接线不一致：" + index, this);
                enabled = false;
                return;
            }
            section.narration.StepStarted += BeginLine;
            section.narration.StepEnded += EndStep;
            var binding = sectionBinding = new object();
            var expected = section;
            onDialogueCompleted = () => { if (sectionBinding == binding && expected.narration.DialogueCompleted) dialogueFinished = true; };
            section.narration.onDialogueCompleted.AddListener(onDialogueCompleted);
            onPuzzleSolved = () => { if (sectionBinding == binding && started) puzzleAligned = true; };
            if (section.startCondition)
            {
                onEntryChanged = source => { if (sectionBinding == binding && source == expected.startCondition) TryStartSection(expected); };
                expected.startCondition.StateChanged += onEntryChanged;
                expected.startCondition.BeginOwned(rpg, expected);
                TryStartSection(expected);
            }
        }

        /// <summary>
        /// 停止旧阶段的提示与旁白，解除操作和旁白事件，清空本阶段运行状态。
        /// 切换、禁用和恢复都会走此清理，避免旧阶段的回调误推进新阶段。
        /// </summary>
        void LeaveSection()
        {
            sectionBinding = null;
            captions?.Clear();
            EndLine();
            RemoveHitObserver();
            if (section?.startCondition)
            {
                section.startCondition.StateChanged -= onEntryChanged;
                section.startCondition.EndOwned(section);
                onEntryChanged = null;
            }
            earlyTargetHits.Clear();
            if (promptVoice) promptVoice.Stop();
            if (puzzle) puzzle.Solved -= onPuzzleSolved;
            if (section?.narration)
            {
                section.narration.StepStarted -= BeginLine;
                section.narration.StepEnded -= EndStep;
                if (onDialogueCompleted != null) section.narration.onDialogueCompleted.RemoveListener(onDialogueCompleted);
                section.narration.StopDialogue();
            }
            onDialogueCompleted = null;
            section = null;
            dialogueFinished = false;
            puzzleAligned = started = false;
            hintIndex = 0; waiting = 0;
        }

        /// <summary>
        /// 达到本段正式开始条件后，才启动第一句旁白和对应的机关。
        /// </summary>
        void TryStartSection(OpeningSection expected)
        {
            if (expected == null || section != expected || started || !rpg.CanAct || index != rpg.StageIndex) return;
            if (expected.startCondition && !expected.startCondition.IsComplete) return;
            started = true;
            if (expected.alignDiscs) { puzzle.Solved += onPuzzleSolved; puzzle.EnablePuzzle(); }
            expected.narration.StartDialogue();
        }

        bool CanActOn(NarrationController.Step step) =>
            step != null && ReferenceEquals(step, boundStep) && isActiveAndEnabled && rpg.CanAct &&
            section != null && section.narration == step.Target && index == rpg.StageIndex &&
            step.Target.CanCompleteOperation(step);

        /// <summary>按稳定 lineId 连接本轮台词与教学操作；索引仅用于播放顺序。</summary>
        void BeginLine(NarrationController.Step step)
        {
            if (section == null || section.narration != step.Target || !step.Target.IsCurrentStep(step)) return;
            EndLine();
            boundStep = step;
            line = Array.Find(section.lines, lesson => lesson.id == step.LineId);
            if (line == null) throw new InvalidOperationException("当前台词没有操作接线：" + step.LineId);
            if (dialogueGeneration != step.Generation || (!refreshingBinding && step.Index == 0))
            {
                earlyTargetHits.Clear();
                dialogueGeneration = step.Generation;
                dialogueFinished = false;
            }
            if (!refreshingBinding)
            {
                if (promptVoice) promptVoice.Stop();
                if (sceneGuidance)
                    sceneGuidance.SetFocus(line.action == OpeningAction.HitTarget && line.target ? line.target.gameObject : null);
                captions?.Begin(step.Target.GetComponent<AudioSource>(), step.Line.speaker, LineText(step.Line, line));
            }
            RemoveHitObserver();
            onHit = target => Hit(step, target);
            rpg.skills.effects.ProjectileHitTarget += onHit;
            if (line.operationTarget)
            {
                var objective = line.operationTarget;
                Func<bool> read = () => ReferenceEquals(boundStep, step) && isActiveAndEnabled && rpg.CanAct &&
                    step.Target.IsCurrentStep(step) && objective.IsRunning && objective.IsComplete;
                onObjectiveChanged = source =>
                {
                    if (source != objective || !ReferenceEquals(boundStep, step) || !step.Target.IsCurrentStep(step)) return;
                    if (!line.rememberTargetSuccess) step.Target.RefreshOperationState(step);
                    else if (read()) step.Target.CompleteOperation(step);
                };
                objective.StateChanged += onObjectiveChanged;
                objective.BeginOwned(rpg, step);
                if (!ReferenceEquals(boundStep, step)) return;
                if (line.rememberTargetSuccess) { if (read()) step.Target.CompleteOperation(step); }
                else if (!step.Target.BindOperationState(step, read)) return;
                if (line.action == OpeningAction.ClearRoom && !objective.IsComplete) encounter.enabled = true;
                return;
            }
            if (!step.Target.CanCompleteOperation(step)) return;
            onOperation = () => Accept(step);
            switch (line.action)
            {
                case OpeningAction.OpenWrist:
                    onWrist = open => { if (CanActOn(step) && open && rpg.wristMenu.OpenTeachingFromLauncher()) Accept(step); };
                    rpg.wristMenu.WristTapConfirmed += onWrist;
                    onWrist(rpg.wristMenu.State.LauncherOpen);
                    break;
                case OpeningAction.HitTarget:
                    if (earlyTargetHits.Remove(line.target)) Accept(step);
                    break;
                case OpeningAction.StopAtMarker:
                    if (!refreshingBinding) stopped = 0;
                    break;
                case OpeningAction.AbsorbBolt:
                    if (!refreshingBinding || !defense.ReplaceCompletion(onOperation)) defense.BeginShield(onOperation);
                    break;
                case OpeningAction.Retreat:
                    rpg.SetEncounterCombat(true);
                    if (!refreshingBinding || !defense.ReplaceCompletion(onOperation)) defense.BeginRetreat(onOperation);
                    break;
                case OpeningAction.ClearRoom:
                    encounter.Cleared += onOperation;
                    encounter.enabled = true;
                    if (encounter.IsComplete) Accept(step);
                    break;
                case OpeningAction.CollectLeaf:
                    onCollected = () => CollectedLeaf(step);
                    puzzle.Collected += onCollected;
                    puzzle.EnableReward();
                    if (puzzle.IsEarnedLeaf(puzzle.leaf)) onCollected();
                    break;
                case OpeningAction.InsertLeaf:
                    onInserted = item => Inserted(step, item);
                    wristSlot.OnSnapCompleted += onInserted;
                    wristSlot.enabled = true;
                    puzzle.EnableInsertion();
                    if (puzzle.IsSeated) onInserted(puzzle.leaf);
                    break;
            }
        }

        void EndStep(NarrationController.Step step)
        {
            if (ReferenceEquals(boundStep, step)) EndLine();
        }

        void EndLine()
        {
            if (line == null) return;
            var old = boundStep;
            boundStep = null;
            if (rpg && rpg.wristMenu) rpg.wristMenu.WristTapConfirmed -= onWrist;
            if (line.operationTarget && onObjectiveChanged != null)
            {
                line.operationTarget.StateChanged -= onObjectiveChanged;
                line.operationTarget.EndOwned(old);
            }
            if (!refreshingBinding && !line.operationTarget && defense && (line.action == OpeningAction.AbsorbBolt || line.action == OpeningAction.Retreat))
            {
                defense.StopDrill();
                if (line.action == OpeningAction.Retreat && rpg) rpg.SetEncounterCombat(false);
            }
            if (encounter && line.action == OpeningAction.ClearRoom)
            {
                encounter.Cleared -= onOperation;
                if (!refreshingBinding) encounter.enabled = false;
            }
            if (puzzle) puzzle.Collected -= onCollected;
            if (wristSlot) wristSlot.OnSnapCompleted -= onInserted;
            // 句内操作在此解绑。靶命中观察延续到本句的句间等待，直到下句、重播或离开本段。
            if (!old.Target.IsWaitingForNextLine) RemoveHitObserver();
            onWrist = null; onObjectiveChanged = null; onCollected = onOperation = null; onInserted = null;
            line = null;
        }

        void RemoveHitObserver()
        {
            if (rpg && rpg.skills && rpg.skills.effects) rpg.skills.effects.ProjectileHitTarget -= onHit;
            onHit = null;
        }

        // 段内允许提前命中下一句指定的靶，包括句间等待。旧轮回调不能替新轮积攒命中。
        void Hit(NarrationController.Step step, SkillTrainingTarget target)
        {
            if (!target || step.Target.CurrentStep != step || (!step.Target.IsCurrentStep(step) && !step.Target.IsWaitingForNextLine) ||
                !started || section == null || index != rpg.StageIndex || !rpg.CanAct) return;
            if (line?.action == OpeningAction.HitTarget && line.target == target) { Accept(step); return; }
            for (int i = step.Index + 1; i < step.Target.lines.Count; i++)
            {
                var later = Array.Find(section.lines, item => item.id == step.Target.lines[i].EffectiveLineId);
                if (later != null && later.action == OpeningAction.HitTarget && later.target == target)
                { earlyTargetHits.Add(target); return; }
            }
        }
        /// <summary>
        /// 验证玩家取得的是本机关奖励铜叶，并请求记录“已取得、未登记入匣”的成长状态。
        /// </summary>
        void CollectedLeaf(NarrationController.Step step)
        {
            if (CanActOn(step) && puzzle.IsEarnedLeaf(puzzle.leaf) && inventory.Collect(puzzle.leafId) != ProgressCommitResult.Rejected) Accept(step);
        }
        /// <summary>
        /// 吸附完成后再次验证铜叶身份，并请求登记入匣；未被拒绝才固定铜叶、完成教学。
        /// 返回值允许保存待重试的情况，不把物理入匣成功等同于磁盘写入已经成功。
        /// </summary>
        void Inserted(NarrationController.Step step, GameObject item)
        {
            // 即使存档正在等待重试，本次有效的物理入匣仍可在当前会话内继续使用。
            if (!CanActOn(step) || !puzzle.IsEarnedLeaf(item) || inventory.Activate(puzzle.leafId) == ProgressCommitResult.Rejected) return;
            if (!CanActOn(step)) return;
            puzzle.Seat(item);
            Accept(step);
        }
        /// <summary>
        /// 向旁白控制器报告当前句的操作完成，不直接切换阶段。
        /// 何时进入下一句由该句的旁白推进配置决定；动作句通常还需等音频播放结束。
        /// </summary>
        void Accept(NarrationController.Step step)
        {
            if (CanActOn(step) && (!line.operationTarget || line.operationTarget.IsComplete)) step.Target.CompleteOperation(step);
        }
    }
}
