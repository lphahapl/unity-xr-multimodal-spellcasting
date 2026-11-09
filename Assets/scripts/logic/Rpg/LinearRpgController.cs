using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NarutoVR.SealLab;
using NarutoVR.SealCombat.Enemies;
using Oculus.Interaction.Input;
using UnityEngine;

namespace NarutoVR.SealCombat.Rpg
{
    /// <summary>进度提交结果：拒绝、已写入磁盘，或本轮已生效但仍待保存重试。</summary>
    public enum ProgressCommitResult { Rejected, Saved, PendingSave }

    /// <summary>
    /// 单人 RPG 会话总控：连接技能、玩家、移动与章节，统一提交阶段奖励和检查点。
    /// 章节目标交给 RpgChapterFlow，识别、资源及效果分别由各自模块处理。
    /// </summary>
    [DefaultExecutionOrder(300), RequireComponent(typeof(PlayerObj))]
    public sealed class LinearRpgController : MonoBehaviour, ISkillActivationPolicy
    {
#if UNITY_EDITOR
        // Set only by the explicit Editor play-test harness, before the scene's Awake.
        public static string EditorTestSaveFile;
#endif
        [Header("一、场景与配置")]
        [InspectorName("技能输入控制器")] public SkillTestController skills;
        [InspectorName("导航与闪避")] public RpgLocomotionController locomotion;
        [InspectorName("腕饰菜单与教学卡")] public WristMenuController wristMenu;
        [InspectorName("玩家基础定义")] public RpgPlayerDefinition playerDefinition;
        [InspectorName("顺序关卡配置")] public RpgStageDefinition[] stages = Array.Empty<RpgStageDefinition>();
        [InspectorName("各阶段检查点")] public NavigationNode[] checkpointNodes = Array.Empty<NavigationNode>();
        [Header("二、操作阈值")]
        [InspectorName("放下双手：低于头部（米）"), Range(.3f, 1)] public float lowHandsBelowHead = .55f;
        [InspectorName("官方基础手形配置"), Tooltip("官方逐指状态引擎与阈值；原始Curl伸直约180度，不使用归零弯曲角。")]
        public BasicHandPoseSettings handPoseSettings;
        [Header("三、进度与存档")]
        [InspectorName("启动时读取存档")] public bool autoLoad = true;
        [InspectorName("存档文件名"), Tooltip("保存到Application.persistentDataPath；不接受目录或绝对路径。")]
        public string saveFileName = "linear-rpg-v2.json";
        [InspectorName("战斗由房间区域管理"), Tooltip("由房间管理战斗，不以练习靶推进关卡，禁用训练预警攻击；靶复位时间由训练设施自行配置。")]
        public bool roomControlsEncounter;
        /// <summary>必须显式绑定的章节实现；其 Owner 必须指回当前控制器。</summary>
        [InspectorName("章节流程（必填）")]
        public RpgChapterFlow chapter;

        PlayerObj playerObject;
        public PlayerObj Entity => playerObject;
        public RpgPlayerState Player => playerObject ? playerObject.State : null;
        /// <summary>当前会话进度；修改后需通过检查点入口持久化，不等于磁盘已经保存。</summary>
        public RpgProgressData Progress { get; private set; }
        /// <summary>技能知识、解锁或章节许可变化的版本号，供界面刷新缓存。</summary>
        public int ProgressRevision { get; private set; }
        /// <summary>最新进度已在会话生效，但仍等待磁盘保存。</summary>
        public bool CheckpointSavePending => pendingCheckpoint != null;
        RpgProgressData pendingCheckpoint;
        double nextSaveRetry;
        string[] StartingSkills => chapter.InitialSkills;
        /// <summary>是否已了解该技能；此状态不代表拥有永久施放资格。</summary>
        public bool IsSkillDiscovered(string id) => SkillProgression.IsDiscovered(Progress, id);
        /// <summary>是否永久解锁该技能。</summary>
        public bool IsSkillUnlocked(string id) => SkillProgression.IsUnlocked(Progress, id);
        /// <summary>综合永久解锁与章节授权；技能资源和冷却在施放入口另行判断。</summary>
        public bool IsSkillAvailable(string id) => Progress != null && chapter &&
            (chapter.AllowsTrialSkill(id) || (IsSkillUnlocked(id) && chapter.AllowsPermanentSkill(id)));
        /// <summary>技能或章节许可变化时使教学界面的缓存失效。</summary>
        internal void RefreshProgress() => ProgressRevision++;

        /// <summary>创建独立进度草稿，物品、商店等模块只能在草稿上计算变更。</summary>
        public RpgProgressData CreateProgressDraft() => CaptureSaveCandidate();

        /// <summary>校验完整草稿后统一应用及保存；草稿不得推进阶段或发阶段奖励。拒绝不改会话，写盘失败保留最新快照重试。</summary>
        public ProgressCommitResult CommitProgressChange(RpgProgressData candidate)
        {
            if (!CanAct || !store.Validate(candidate, out _) ||
                !candidate.completedStageIds.SequenceEqual(Progress.completedStageIds) ||
                !candidate.claimedRewardIds.SequenceEqual(Progress.claimedRewardIds) || !ValidateProgress(candidate))
            { Reject("进度变更无效，未应用"); return ProgressCommitResult.Rejected; }
            Progress = CloneProgress(candidate);
            ApplyUpgrades(); RefreshProgress();
            QueueCheckpoint(CloneProgress(candidate));
            return PersistCheckpoint(pendingCheckpoint, false) ? ProgressCommitResult.Saved : ProgressCommitResult.PendingSave;
        }
        public string Status { get; private set; } = "等待初始化";
        public string StageName => stages.Length > StageIndex && stages[StageIndex] ? stages[StageIndex].displayName : "未配置阶段";
        public string SavePath { get; private set; }
        public int StageIndex { get; private set; }
        public double GameTime { get; private set; }
        public bool HazardsEnabled { get; private set; }
        public bool CanAct => isActiveAndEnabled && Player != null && Player.Alive && !paused && Time.timeScale > 0;
        public bool LastCastCreatedProjectile { get; private set; }
        /// <summary>目标文字由当前章节解释，RPG 不区分首章或训练场。</summary>
        public string ObjectiveStatus => chapter ? chapter.ObjectiveText : "未配置章节流程";
        public bool CampaignComplete => Progress != null && stages.Length > 0 && stages.All(s => s && Progress.completedStageIds.Contains(s.stageId));

        RuntimeSkillCatalog runtimeSkills;
        IRpgProgressConstraint[] progressConstraints = Array.Empty<IRpgProgressConstraint>();
        RpgSaveStore store;
        RpgProgressData lastCheckpoint;
        SkillCatalog originalCatalog;
        bool paused, initialized, wasNavigation, saveBlocked, wasUi;
        public bool UiInputOwned => wristMenu && wristMenu.OwnsInput;
        public string MovementBlockReason
        {
            get
            {
                if (!initialized || !isActiveAndEnabled || Player == null) return "关卡未就绪";
                if (paused || Time.timeScale <= 0) return "游戏已暂停，恢复播放后才能移动";
                if (!Player.Alive) return "生命为零，点击「恢复检查点」后可继续移动";
                return UiInputOwned ? "腕部界面占用输入，请先结束点击或拖动并关闭主菜单" : null;
            }
        }
        string lastMovementGate;
        public string LastDebugMove { get; private set; }
        long activationSequence, damageSequence;
        readonly string sessionId = Guid.NewGuid().ToString("N");
        OfficialHandPoseDetector handPoses;
        public string HandPoseStatus => handPoses?.Status ?? "官方手形：等待初始化";
        public event Action<RpgActionCue> Feedback;
        /// <summary>进度、角色和落点全部恢复后通知章节重建场景，不负责逐句台词续播。</summary>
        public event Action CheckpointRestored;

        /// <summary>校验接线、建立运行时技能副本和玩家，再创建新进度或读取磁盘检查点。</summary>
        void Awake()
        {
            try
            {
#if UNITY_EDITOR
                if (!string.IsNullOrEmpty(EditorTestSaveFile))
                { saveFileName = EditorTestSaveFile; autoLoad = false; }
#endif
                ValidateSetup();
                handPoses = new OfficialHandPoseDetector(handPoseSettings);
                playerObject = GetComponent<PlayerObj>();
                if (!playerObject) throw new InvalidOperationException("缺少玩家实体组件");
                playerObject.Initialize(playerDefinition);
                originalCatalog = skills.catalog;
                runtimeSkills = new RuntimeSkillCatalog(originalCatalog);
                var runtimeCatalog = runtimeSkills.Catalog;
                skills.catalog = runtimeCatalog;
                SavePath = Path.Combine(Application.persistentDataPath, saveFileName);
                store = new RpgSaveStore(SavePath, "linear-rpg-player", runtimeCatalog.skills.Select(s => s.skillId),
                    Player.MaxHealth, Player.MaxMana, runtimeCatalog.skills.Select(CooldownGroup).Distinct());
                Progress = new RpgProgressData { checkpoint = Player.CaptureCheckpoint(GameTime), checkpointId = checkpointNodes[0].nodeId };
                SkillProgression.Unlock(Progress, StartingSkills);
                SkillProgression.Discover(Progress, chapter.InitialDiscoveries);
                Status = chapter.InitialStatus;
                if (autoLoad && (File.Exists(SavePath) || File.Exists(SavePath + ".bak")))
                {
                    if (store.TryLoad(out var loaded, out string reason) && ValidateProgress(loaded))
                    { Progress = loaded; Status = reason ?? "已读取检查点"; }
                    else { saveBlocked = true; Status = "读取失败，当前使用临时新角色，已禁止覆盖原存档：" + (reason ?? "阶段、奖励或检查点不属于当前关卡"); }
                }
                lastCheckpoint = CloneProgress(Progress);
                RefreshProgress();
                SelectStageFromProgress();
                Player.RestoreCheckpoint(Progress.checkpoint, GameTime, out _);
                ApplyUpgrades();
                skills.enableOpenHandCancel = false;
                skills.ActivationOverride = null;
                skills.ActivationPolicy = this;
                skills.GestureInputBlocked = () => !CanAct || locomotion.NavigationActive || UiInputOwned;
                skills.VoiceInputBlocked = () => !CanAct || locomotion.NavigationActive || UiInputOwned;
                skills.ExclusiveInputBlocked = () => UiInputOwned;
                skills.VoiceOwnershipDetail = () => UiInputOwned ? "腕部界面：" + wristMenu.OwnershipReason :
                    !CanAct ? (Player != null && !Player.Alive ? "角色已倒下" : "角色当前不能行动") : "节点移动占用输入";
                skills.CancelOverride = CancelCasting;
                skills.DeferVoiceUntilLateUpdate = true;
                skills.effects.GameClock = () => GameTime;
                skills.effects.RightHandActionPolicy = () => CanAct && skills.isActiveAndEnabled && !UiInputOwned &&
                    !locomotion.NavigationActive && !locomotion.EncounterSlideActive;
                skills.effects.ThrowGripProvider = (left, settings) => ReadThrowGrip(left, settings, Time.realtimeSinceStartupAsDouble);
                locomotion.GameClock = () => GameTime;
                locomotion.encounterModesEnabled = true;
                skills.effects.ProjectileLaunched += OnProjectileLaunched;
                locomotion.CancelRequested += CancelCasting;
                locomotion.Moved += OnMoved;
                initialized = true;
                if (!GetComponent<RpgDamageReceiver>()) gameObject.AddComponent<RpgDamageReceiver>();
            }
            catch (Exception e)
            {
                Status = "线性RPG初始化失败：" + e.Message;
                Debug.LogError(Status, this);
                if (skills) skills.enabled = false;
                enabled = false;
            }
        }

        /// <summary>初始化完成后通知章节进入阶段，并将玩家放到当前检查点。</summary>
        void Start()
        {
            if (!initialized) return;
            ApplyStage();
            var node = FindCheckpoint(Progress.checkpointId);
            if (node && !locomotion.TryRestoreCheckpoint(node)) Status = "检查点落地失败：" + locomotion.Status;
            // Do not overwrite the load diagnostic with the relocation's input reset.
        }

        void LateUpdate()
        {
            if (!initialized) return;
            if (CheckpointSavePending && !saveBlocked && Time.realtimeSinceStartupAsDouble >= nextSaveRetry)
            {
                nextSaveRetry = Time.realtimeSinceStartupAsDouble + 2;
                PersistCheckpoint(pendingCheckpoint, true);
            }
            if (!paused && Time.timeScale > 0) { GameTime += Time.deltaTime; Player.Tick(GameTime); }
            var frame = ReadNavigationFrame(Time.realtimeSinceStartupAsDouble);
            if (wristMenu && wristMenu.isActiveAndEnabled)
                UpdateWristUi(frame,ReadWristMenuFrame(frame),paused || Time.timeScale <= 0 || skills.effects.HasOccupiedHand || (!Player.Alive && !wristMenu.wornDevice),Time.realtimeSinceStartupAsDouble);
            else SyncWristOwnership(); // A menu disabled while active also returns gameplay ownership.
            var gate = MovementBlockReason;
            if (gate != lastMovementGate)
            {
                skills.TraceCast("movement_gate", gate ?? "移动输入已恢复");
                lastMovementGate = gate;
            }
            // Skip the whole locomotion recognizer: its low-hands cancellation runs before blockActions.
            if (!UiInputOwned) locomotion.ProcessInput(frame, skills.effects.HasRightHeldObject && !skills.effects.HasRightThrowObject, !CanAct, Time.realtimeSinceStartupAsDouble);
            bool navigating = locomotion.NavigationActive;
            if (navigating != wasNavigation) skills.CancelInputOnly();
            wasNavigation = navigating;
            // Suppress release on death/pause only: a held fireball still owns forward push during navigation.
            skills.effects.PushInputSuppressed = !CanAct || UiInputOwned;
            // A spawned projectile already owns the release gesture. Residual two-hand seal protection
            // must not clear its trajectory while hands separate (or the left hand goes out of view).
            skills.effects.ThrowInputSuppressed = !CanAct || UiInputOwned || (locomotion.SealProtectionActive && !skills.effects.HasHeldObject);

        }

        // Let a confirmed wrist press interrupt pending navigation, never a held spell.
        // The tap observer still receives approach samples so contact can be confirmed without
        // reserving input in advance; all resets belong to the actual ownership transition.
        public void UpdateWristUi(NavigationHandInput frame,WristMenuInput ui,bool hardBlocked,double now)
        {
            bool confirmed=wristMenu.PrepareWristTap(ui,hardBlocked,now);
            bool sealBusy=!confirmed && !UiInputOwned && !wristMenu.PanelOpen && !wristMenu.State.LauncherOpen && (locomotion.SealProtectionActive ||
                (frame.leftValid && frame.rightValid && Vector3.Distance(frame.leftWrist,frame.rightWrist)<locomotion.gestures.sealEnterDistance));
            string blocked = hardBlocked ? (skills && skills.effects && skills.effects.HasOccupiedHand ? "手上有持有物" : "角色当前不能行动") :
                !confirmed && locomotion.NavigationActive ? "导航操作中" : sealBusy ? "双手处于结印保护范围" : null;
            wristMenu.Tick(ui,this,blocked!=null,now,blocked);
            SyncWristOwnership();
        }

        void SyncWristOwnership()
        {
            bool owns=UiInputOwned;
            if(owns==wasUi)return;
            wasUi=owns;
            if(skills){skills.CancelInputOnly();skills.InvalidateEarlierVoice();}
            locomotion.ResetInput();
        }

        public bool DebugCast(string skillId) => TryActivate(skills ? skills.ConfiguredSkills.FirstOrDefault(s => s && s.skillId == skillId) : null);

        bool ISkillActivationPolicy.TryActivate(SkillDefinition skill) => TryActivate(skill);
        string ISkillActivationPolicy.ActivationStatus => Status;
        bool ISkillActivationPolicy.OwnsCooldowns => true;

        bool TryActivate(SkillDefinition skill)
        {
            if (!CanAct || !skill || !skills.ConfiguredSkills.Contains(skill)) return Reject("当前无法施法");
            if (UiInputOwned) return Reject("正在操作教学界面；手部恢复中立后再施法");
            if (locomotion.NavigationActive) return Reject("导航准备中：放下左手后再开始结印或吟唱");
            if (!IsSkillAvailable(skill.skillId))
                return Reject((IsSkillDiscovered(skill.skillId) ? "此阶段暂不能施放：" : "尚未解锁：") + skill.displayName);
            string request = sessionId + ":cast:" + (++activationSequence);
            int previousProjectile = skills.effects.HeldProjectileInstanceId;
            bool accepted = Player.TryActivate(request, skill.skillId, CooldownGroup(skill), skill.manaCost, skill.cooldownSeconds,
                GameTime, () => skills.effects.TryActivate(skill), out string reason);
            LastCastCreatedProjectile = accepted && skills.effects.HeldProjectileInstanceId != 0 &&
                skills.effects.HeldProjectileInstanceId != previousProjectile;
            Status = accepted ? $"{skill.displayName}已准备，消耗 {skill.manaCost:0} MP" : reason + "；" + skills.effects.Status;
            RpgFeedbackSignals.Raise(Feedback, accepted ? RpgActionCue.CastReady : RpgActionCue.Blocked, this);
            return accepted;
        }

        public void CancelCasting()
        {
            if (!initialized) return;
            skills.CancelInputOnly(); skills.effects.CancelUnreleased(); locomotion.CancelPendingNavigation();
            Status = "已取消待施法输入并收起手中法宝。已消耗MP和CD不返还。";
            RpgFeedbackSignals.Raise(Feedback, RpgActionCue.Cancelled, this);
        }

        public bool DebugMove(bool dodge, Vector2 direction)
        {
            string reason = MovementBlockReason;
            if (reason == null && dodge && skills.effects.HasRightHeldObject)
                reason = "右手正在持有法宝，先释放或收起再闪避";
            bool moved = reason != null ? Reject(reason) : dodge
                ? locomotion.TryDebugDodge(direction, Time.realtimeSinceStartupAsDouble)
                : locomotion.TryDebugStep(direction, Time.realtimeSinceStartupAsDouble);
            LastDebugMove = reason ?? locomotion.Status;
            skills?.TraceCast("movement_debug", LastDebugMove);
            return moved;
        }

        public bool ReceiveDamage(string hitId, float amount) => ReceiveDamage(hitId, amount, null);

        internal bool ReceiveDamage(string hitId, float amount, EnemyAttackHit? attack)
        {
            bool canAct = CanAct;
            PlayerDamageResult damage = default;
            bool accepted = canAct && playerObject.TryReceiveDamage(hitId, amount, GameTime, skills.effects, out damage);
            if (skills && skills.enableCastTrace)
            {
                float after = Player?.Health ?? 0;
                string source = "直接伤害";
                if (attack is EnemyAttackHit hit && hit.Source)
                {
                    var attacks = hit.Source.Attacks;
                    string move = attacks != null && hit.AttackIndex >= 0 && hit.AttackIndex < attacks.Length
                        ? attacks[hit.AttackIndex]?.displayName : null;
                    source = $"{hit.Source.name}/{move ?? hit.AttackIndex.ToString()}";
                }
                string outcome = accepted ? "已结算" : playerObject && playerObject.HasReceivedDamage(hitId) ? "重复命中" :
                    canAct ? "未接受" : "玩家不可行动";
                skills.TraceCast(accepted ? "player_damage" : "player_damage_rejected",
                    $"来源={source} 结果={outcome} 原始={amount:0.##} 护盾吸收={damage.Absorbed:0.##} " +
                    $"HP={after + damage.HealthLost:0.##}→{after:0.##} hitId={hitId}");
            }
            if (!accepted) return false;
            Status = $"受到 {damage.Requested:0} 伤害，护盾吸收 {damage.Absorbed:0}，生命损失 {damage.HealthLost:0}";
            RpgFeedbackSignals.Raise(Feedback, RpgActionCue.Damage, this);
            if (!Player.Alive)
            {
                skills.CancelInputOnly(); skills.effects.ClearOnPlayerDeath(); locomotion.ResetInput();
                Status = "角色已倒下。恢复检查点后重试；头部追踪保持正常。";
            }
            return true;
        }
        public void DebugDamage(float amount) => ReceiveDamage(sessionId + ":debug-hit:" + (++damageSequence), amount);

        /// <summary>捕获当前阶段、技能和角色资源；正式写盘成功后才更新最后成功快照。</summary>
        public bool SaveCheckpoint()
        {
            if (!CanAct) return Reject("仅存活且未暂停时可保存检查点");
            var candidate = CaptureSaveCandidate();
            QueueCheckpoint(candidate);
            return PersistCheckpoint(candidate, false);
        }

        /// <summary>复制当前进度，补入章节检查点与资源，避免保存快照继续跟随会话变化。</summary>
        RpgProgressData CaptureSaveCandidate()
        {
            var candidate = CloneProgress(Progress);
            candidate.checkpointId = SaveNode(StageIndex, CampaignComplete).nodeId;
            candidate.checkpoint = Player.CaptureCheckpoint(GameTime);
            return candidate;
        }

        /// <summary>排队保存最新快照；后来的有效进度覆盖更早快照，避免重试把玩家回滚。</summary>
        void QueueCheckpoint(RpgProgressData candidate)
        {
            pendingCheckpoint = candidate;
            nextSaveRetry = Time.realtimeSinceStartupAsDouble + 2;
            RefreshProgress();
        }

        /// <summary>校验并持久化快照；失败保留诊断与待重试状态，不假报保存成功。</summary>
        bool PersistCheckpoint(RpgProgressData candidate, bool retry)
        {
            if (saveBlocked) return Reject("启动时存档验证失败；为保留原文件，本次临时会话禁止写入");
            if (!store.TrySave(candidate, out string reason))
            {
                if (retry) return false;
                return Reject(CheckpointSavePending ? "进度在本次游戏中已生效，等待存档：" + reason : reason);
            }
            // A delayed write checkpoints the snapshot without reverting changes made since the failed save.
            if (!retry) Progress = candidate;
            lastCheckpoint = CloneProgress(candidate);
            if (CheckpointSavePending) { pendingCheckpoint = null; RefreshProgress(); }
            Status = "检查点已保存（固定节点、角色进度、HP/MP和剩余CD）；临时效果与敌人血量不保存";
            RpgFeedbackSignals.Raise(Feedback, RpgActionCue.Progress, this);
            return true;
        }

        /// <summary>
        /// 从磁盘或本轮待保存状态恢复；先验证进度和落点，再替换角色状态并通知章节。
        /// 恢复的是阶段检查点，阶段内未保存的目标与临时效果会重置。
        /// </summary>
        public bool RestoreCheckpoint()
        {
            if (!initialized || paused || Time.timeScale <= 0) return Reject("暂停时不能恢复检查点");
            var restored = CloneProgress(pendingCheckpoint ?? lastCheckpoint);
            if (!CheckpointSavePending && (File.Exists(SavePath) || File.Exists(SavePath + ".bak")))
            {
                if (!store.TryLoad(out restored, out string reason)) return Reject(reason);
            }
            if (!ValidateProgress(restored) || restored.checkpoint.health <= 0) return Reject("检查点无效或生命为零，未改变当前状态");
            NavigationNode node = FindCheckpoint(restored.checkpointId);
            if (!node || !locomotion.TryRestoreCheckpoint(node)) return Reject("检查点落地失败：" + locomotion.Status);
            if (!Player.RestoreCheckpoint(restored.checkpoint, GameTime, out string error)) return Reject(error);
            Progress = restored; lastCheckpoint = CloneProgress(restored); SelectStageFromProgress(); ApplyUpgrades();
            RefreshProgress();
            skills.CancelInputOnly(); skills.effects.ClearAll(); locomotion.ResetInput();
            ApplyStage(); Status = "已恢复检查点；阶段目标重置，临时效果已清理";
            RpgFeedbackSignals.Raise(Feedback, RpgActionCue.Progress, this);
            if (CheckpointRestored != null)
                foreach (Action listener in CheckpointRestored.GetInvocationList())
                    try { listener(); } catch (Exception listenerError) { Debug.LogWarning("检查点恢复监听异常：" + listenerError, this); }
            return true;
        }

        /// <summary>把手动推进请求交给已绑定章节；RPG 不自行判定训练靶或教学条件。</summary>
        public bool TryAdvanceStage()
        {
            if (!CanAct || !chapter || !chapter.isActiveAndEnabled) return Reject("章节当前不能推进");
            return chapter.TryAdvanceStage(out string reason) || Reject(reason);
        }

        /// <summary>只接受当前章节的有效完成请求，统一结算阶段奖励并保存检查点。</summary>
        public bool CompleteChapterObjective(RpgChapterFlow source)
        {
            if (source != chapter || !source || !source.isActiveAndEnabled || source.Owner != this || !CanAct || CampaignComplete || !source.ObjectiveMet) return false;
            return CommitStage(stages[StageIndex]);
        }

        /// <summary>统一发放一次阶段奖励、更新技能和阶段，然后保存；章节不得自行重复结算。</summary>
        bool CommitStage(RpgStageDefinition stage)
        {
            if (!RpgRewards.Grant(Progress, "reward." + stage.stageId, stage.rewardCurrency, stage.unlockSkillIds)) return Reject("此阶段奖励已领取或进度已达上限");
            Progress.completedStageIds.Add(stage.stageId);
            RefreshProgress();
            skills.CancelInputOnly(); skills.effects.ClearAll(); locomotion.ResetInput();
            if (chapter.RestoreVitalsOnStageComplete) Player.Respawn(GameTime);
            if (!CampaignComplete)
            {
                StageIndex++; ApplyStage();
            }
            else { HazardsEnabled = false; SetEncounterCombat(false); }
            SaveCheckpoint();
            Status = (CampaignComplete ? "全部线性试炼完成" : "进入「" + StageName + "」") + $"；获得 {stage.rewardCurrency} 货币" +
                (Status.StartsWith("检查点已保存", StringComparison.Ordinal) ? "；进度已保存" : "；" + Status);
            return true;
        }

        // Encounter transitions are explicit; the hazard training switch is independent.
        public void SetEncounterCombat(bool combat)
        {
            if (!initialized || !locomotion || locomotion.EncounterCombat == combat) return;
            locomotion.SetEncounterCombat(combat);
            skills.TraceCast("locomotion_mode",combat ? "进入战斗" : "进入探索");
        }
        void OnProjectileLaunched() => locomotion.NotifyProjectileLaunched(Time.realtimeSinceStartupAsDouble);
        public void ToggleHazards()
        {
            if (roomControlsEncounter) { HazardsEnabled = false; Status = "战斗房间模式不启用练习区预警攻击"; return; }
            if (initialized) { HazardsEnabled = !HazardsEnabled; Status = HazardsEnabled ? "攻击预警已开启" : "攻击预警已关闭"; }
        }

        /// <summary>通知当前章节重建阶段目标，再设置通用战斗模式及训练预警开关。</summary>
        void ApplyStage()
        {
            chapter.OnStageEntered();
            if (!roomControlsEncounter) SetEncounterCombat(!CampaignComplete && stages[StageIndex].startsInCombat);
            HazardsEnabled = !roomControlsEncounter && !CampaignComplete && stages[StageIndex].enableHazards;
        }
        /// <summary>选择第一个未完成阶段；全通关时下标停在最后一段，以 CampaignComplete 区分完成态。</summary>
        void SelectStageFromProgress() { StageIndex = Array.FindIndex(stages, s => !Progress.completedStageIds.Contains(s.stageId)); if (StageIndex < 0) StageIndex = stages.Length - 1; }
        /// <summary>由技能副本管理器按各自 ID 重建等级效果，不在会话中识别具体术式。</summary>
        void ApplyUpgrades() => runtimeSkills?.ApplyLevels(Progress);
        void OnMoved(Vector3 delta)
        {
            if (!skills) return;
            if (wristMenu && !locomotion.ContinuousMotionActive) wristMenu.ResetForWorldChange();
            skills.CancelInputOnly();
            if (skills.effects) skills.effects.ResetPushInput();
        }
        static string CooldownGroup(SkillDefinition s) => string.IsNullOrEmpty(s.cooldownGroup) ? s.skillId : s.cooldownGroup;
        /// <summary>通关优先使用章节专属终点，其余情况使用对应阶段的检查点。</summary>
        NavigationNode SaveNode(int stage, bool complete) => complete && chapter && chapter.FinalCheckpoint
            ? chapter.FinalCheckpoint : checkpointNodes[stage];
        NavigationNode FindCheckpoint(string id) => chapter && chapter.FinalCheckpoint && chapter.FinalCheckpoint.nodeId == id
            ? chapter.FinalCheckpoint : checkpointNodes.FirstOrDefault(n => n && n.nodeId == id);
        bool Reject(string reason) { Status = reason; RpgFeedbackSignals.Raise(Feedback, RpgActionCue.Blocked, this); return false; }
        static RpgProgressData CloneProgress(RpgProgressData p) => JsonUtility.FromJson<RpgProgressData>(JsonUtility.ToJson(p));
        /// <summary>验证顺序阶段、奖励、技能和落点，再调用扩展模块校验，不自行解释物品或商店规则。</summary>
        bool ValidateProgress(RpgProgressData p)
        {
            if (p == null || !FindCheckpoint(p.checkpointId) || p.checkpoint.health <= 0 || StartingSkills.Any(id => !p.unlockedSkillIds.Contains(id)) ||
                !p.completedStageIds.SequenceEqual(stages.Take(p.completedStageIds.Count).Select(s => s.stageId))) return false;
            int stageIndex = Math.Min(p.completedStageIds.Count, stages.Length - 1);
            if (p.checkpointId != SaveNode(stageIndex, p.completedStageIds.Count == stages.Length).nodeId) return false;
            var rewards = p.completedStageIds.Select(id => "reward." + id).ToArray();
            if (p.claimedRewardIds.Count != rewards.Length || rewards.Any(id => !p.claimedRewardIds.Contains(id))) return false;
            foreach (var stage in stages.Take(p.completedStageIds.Count))
                if (stage.unlockSkillIds.Any(id => !p.unlockedSkillIds.Contains(id))) return false;
            return progressConstraints.All(module => module.ValidateProgress(p));
        }

        /// <summary>检查通用必需接线，再调用章节专属校验；缺少章节时直接拒绝初始化。</summary>
        void ValidateSetup()
        {
            if (!handPoseSettings || handPoseSettings.Validate() is string) throw new InvalidOperationException("缺少或无效的官方基础手形配置");
            if (!skills || !skills.catalog || !skills.effects || !skills.lab || !locomotion || !locomotion.head) throw new InvalidOperationException("缺少技能、效果、骨骼或导航接线");
            var errors = skills.catalog.Validate(); if (errors.Count != 0) throw new InvalidOperationException(string.Join("；", errors));
            if (!chapter) throw new InvalidOperationException("必须配置章节流程");
            if (stages.Length == 0 || checkpointNodes.Length != stages.Length) throw new InvalidOperationException("阶段或检查点配置不完整");
            var ids = new HashSet<string>();
            foreach (var stage in stages)
                if (!stage || string.IsNullOrWhiteSpace(stage.stageId) || !ids.Add(stage.stageId) ||
                    stage.rewardCurrency < 0 || stage.unlockSkillIds == null || stage.unlockSkillIds.Any(id => !skills.catalog.skills.Any(s => s.skillId == id)))
                    throw new InvalidOperationException("阶段配置无效或含未知技能");
            if (checkpointNodes.Any(n => !n)) throw new InvalidOperationException("检查点节点缺失");
            if (StartingSkills == null || StartingSkills.Distinct().Count() != StartingSkills.Length ||
                StartingSkills.Any(id => !skills.catalog.skills.Any(skill => skill.skillId == id)))
                throw new InvalidOperationException("章节初始技能包含未知或重复 ID");
            if (chapter.InitialDiscoveries == null || chapter.InitialDiscoveries.Distinct().Count() != chapter.InitialDiscoveries.Length ||
                chapter.InitialDiscoveries.Any(id => !skills.catalog.skills.Any(skill => skill.skillId == id)))
                throw new InvalidOperationException("章节初始知识包含未知或重复技能 ID");
            chapter.ValidateChapter(this);
            if (wristMenu && wristMenu.teachingCatalog) wristMenu.teachingCatalog.Validate(skills.catalog.skills.Select(skill => skill.skillId));
            progressConstraints = GetComponents<IRpgProgressConstraint>();
            foreach (var module in progressConstraints) module.ValidateConfiguration(this);
            if (string.IsNullOrWhiteSpace(saveFileName) || Path.GetFileName(saveFileName) != saveFileName || Path.IsPathRooted(saveFileName) || saveFileName is "." or "..") throw new InvalidOperationException("存档只能指定文件名");
        }

        NavigationHandInput ReadNavigationFrame(double now)
        {
            var result = new NavigationHandInput(); var frame = skills.lab.Frame;
            if (frame == null) { handPoses?.Reset(); return result; }
            if (handPoses == null) handPoses = new OfficialHandPoseDetector(handPoseSettings);
            handPoses.Update(frame, skills.lab.Features, now, locomotion.gestures.sampleMaxAge, locomotion.gestures.sampleMaxGap);
            result.leftObserved = frame.left.observedAt; result.rightObserved = frame.right.observedAt;
            result.leftValid = Wrist(frame.left, now, out result.leftWrist);
            result.rightValid = Wrist(frame.right, now, out result.rightWrist);
            int index = (int)HandJointId.HandIndexTip;
            if (result.leftValid && frame.left.jointValid[index]) result.leftIndex = World(frame.left.joints[index].position);
            result.leftPointing = result.leftValid && frame.left.jointValid[index] && handPoses.Left.pointing;
            int indexBase = (int)HandJointId.HandIndex1;
            if (result.leftPointing && frame.left.jointValid[indexBase])
            {
                Vector3 direction = result.leftIndex - World(frame.left.joints[indexBase].position);
                float length = direction.magnitude;
                result.leftRayValid = RpgGestureState.Finite(direction) && length >= .02f && length <= .16f;
                if (result.leftRayValid)
                {
                    result.leftRayOrigin = result.leftIndex;
                    result.leftRayDirection = direction / length;
                }
            }
            result.rightOpen = result.rightValid && handPoses.Right.open;
            result.rightMotionOwnedBySpell = skills.effects && skills.effects.RightMotionOwnedBySpell;
            result.bothHandsLow = result.leftValid && result.rightValid && Math.Abs(result.leftObserved - result.rightObserved) <= SealFeatureExtractor.MaximumHandSkew &&
                result.leftWrist.y < locomotion.head.position.y - lowHandsBelowHead && result.rightWrist.y < locomotion.head.position.y - lowHandsBelowHead;
            return result;
        }
        public static bool PointerPoseNearHand(Pose aim,Vector3 wrist) => NarutoVR.SealLab.SealLab.FinitePose(aim) &&
            RpgGestureState.Finite(wrist) && Vector3.Distance(aim.position,wrist)<=.3f;
        WristMenuInput ReadWristMenuFrame(NavigationHandInput navigation)
        {
            var f = new WristMenuInput { leftValid = navigation.leftValid, rightValid = navigation.rightValid,
                leftObserved = navigation.leftObserved, rightObserved = navigation.rightObserved,
                leftWrist = navigation.leftWrist, leftClosed = handPoses != null && handPoses.Left.valid && handPoses.Left.closed,
                leftPointing = navigation.leftPointing, rightOpen = navigation.rightOpen,
                rightPointing = handPoses != null && handPoses.Right.valid && handPoses.Right.pointing,
                rightClosed = handPoses != null && handPoses.Right.valid && handPoses.Right.closed,
                headPosition = locomotion.head.position, headForward = locomotion.head.forward };
            var frame = skills.lab.Frame;
            if (frame == null) return f;
            f.source = frame.source;
            int wrist = (int)HandJointId.HandWristRoot, index = (int)HandJointId.HandIndex1,
                pinky = (int)HandJointId.HandPinky1, tip = (int)HandJointId.HandIndexTip, thumb = (int)HandJointId.HandThumbTip;
            Quaternion origin = skills.lab.recordingOrigin ? skills.lab.recordingOrigin.rotation : Quaternion.identity;
            double menuNow=Time.realtimeSinceStartupAsDouble;
            var leftHand=frame.left;
            f.leftValid=leftHand.connected && leftHand.tracked && leftHand.jointValid[wrist] &&
                WristMenuState.Fresh(leftHand.fresh,leftHand.observedAt,menuNow,wristMenu.settings.maxAge);
            if(f.leftValid){
                f.leftWrist=World(leftHand.joints[wrist].position);
                f.leftValid=WristMenuState.Finite(f.leftWrist);
            }
            if (f.leftValid)
            {
                f.leftRotation = origin*frame.left.joints[wrist].rotation;
                if (frame.left.jointValid[index] && frame.left.jointValid[pinky])
                {
                    Vector3 normal = Vector3.Cross(World(frame.left.joints[index].position)-f.leftWrist, World(frame.left.joints[pinky].position)-f.leftWrist);
                    Vector3 fingers = (World(frame.left.joints[index].position) + World(frame.left.joints[pinky].position)) * .5f - f.leftWrist;
                    f.mountValid = normal.sqrMagnitude > .0000001f && fingers.sqrMagnitude > .0001f;
                    if (f.mountValid) f.mountRotation = Quaternion.LookRotation(fingers, -normal);
                    // Both dorsal/palmar conventions are accepted; vendor-specific wrist axes stay in this adapter.
                    f.wristFacing = normal.sqrMagnitude > .0000001f && Mathf.Abs(Vector3.Dot(normal.normalized,(f.headPosition-f.leftWrist).normalized)) >= .2f;
                }
            }
            // Menu input needs only the joints used by each channel. Navigation's confidence
            // includes every finger and must not suppress a valid wrist when one tip is occluded.
            var hand=frame.right;
            f.rightValid=hand.connected && hand.tracked && hand.fresh && hand.jointValid[wrist] &&
                WristMenuState.Fresh(true,hand.observedAt,menuNow,wristMenu.settings.maxAge);
            f.tipUnavailable=true;
            if(!f.rightValid)return f;
            Vector3 wristWorld=World(hand.joints[wrist].position);
            if(!WristMenuState.Finite(wristWorld)){f.rightValid=false;return f;}
            if(hand.jointValid[tip]){
                f.rightTip=World(hand.joints[tip].position);
                f.tipUnavailable=!WristMenuState.Finite(f.rightTip);
            }
            int middle=(int)HandJointId.HandMiddle1;
            bool hasIndex=hand.jointValid[index],hasMiddle=hand.jointValid[middle];
            if(hasIndex || hasMiddle){
                Vector3 basePoint=hasIndex && hasMiddle ? (hand.joints[index].position+hand.joints[middle].position)*.5f
                    : hand.joints[hasIndex?index:middle].position;
                Vector3 axis=World(basePoint)-wristWorld;
                if(WristMenuState.Finite(axis) && axis.magnitude>=.02f && axis.magnitude<=.16f){
                    f.rayOrigin=wristWorld;f.rayDirection=axis.normalized;f.rayValid=true;f.raySource=hasIndex && hasMiddle?"骨骼·腕到双指根":hasIndex?"骨骼·腕到食指根":"骨骼·腕到中指根";
                }
            }
            if(!f.tipUnavailable && hand.jointValid[thumb]){
                f.pinchDistance=Vector3.Distance(f.rightTip,World(hand.joints[thumb].position));
                f.pinchValid=float.IsFinite(f.pinchDistance);
                if(f.pinchValid)f.pinchSource="骨骼·拇食指距离";
            }
            WristHandChannels.Apply(ref f,skills.lab.recordingOrigin,wristWorld,wristMenu.settings,
                skills.lab.xrHandsSource,skills.lab.rightTrackedHand as IHand);
            f.rayAnchorValid=f.rayValid;
            return f;
        }
        ThrowGripSample ReadThrowGrip(bool left, HandThrowSettings settings, double now)
        {
            var result = new ThrowGripSample(); var frame = skills.lab.Frame;
            if (frame == null || handPoses == null) return result;
            HandSnapshot hand = left ? frame.left : frame.right;
            if (!Wrist(hand, now, out var wrist)) return result;
            int indexBase=(int)HandJointId.HandIndex1, middleBase=(int)HandJointId.HandMiddle1;
            if(hand.jointValid[indexBase] && hand.jointValid[middleBase]) {
                var midpoint=(hand.joints[indexBase].position+hand.joints[middleBase].position)*.5f;
                var axis=World(midpoint)-wrist;
                if(axis.sqrMagnitude>=.0004f && axis.sqrMagnitude<=.0256f) result.wristToKnucklesWorld=axis.normalized;
            }
            var shape = left ? handPoses.Left : handPoses.Right;
            result.observed = hand.observedAt;
            result.fistValid = shape.valid; result.fistClosed = shape.closed;
            result.fistOpen = settings.allowPartialFistRelease ? shape.releaseOpen : shape.open;
            int thumb = (int)HandJointId.HandThumbTip, index = (int)HandJointId.HandIndexTip;
            if (hand.jointValid[thumb] && hand.jointValid[index])
            {
                float distance = Vector3.Distance(World(hand.joints[thumb].position), World(hand.joints[index].position));
                result.pinchValid = float.IsFinite(distance);
                result.pinchClosed = distance <= settings.pinchCloseDistance;
                result.pinchOpen = distance >= settings.pinchOpenDistance;
            }
            return result;
        }
        bool Wrist(HandSnapshot h, double now, out Vector3 position)
        {
            position = default; int wrist = (int)HandJointId.HandWristRoot;
            if (h == null || !h.connected || !h.tracked || !h.fresh || !h.highConfidence || !h.jointValid[wrist] ||
                now - h.observedAt < 0 || now - h.observedAt > locomotion.gestures.sampleMaxAge) return false;
            position = World(h.joints[wrist].position); return float.IsFinite(position.x) && float.IsFinite(position.y) && float.IsFinite(position.z);
        }
        Vector3 World(Vector3 position) => skills.lab.recordingOrigin ? skills.lab.recordingOrigin.TransformPoint(position) : position;
        void OnApplicationPause(bool value)
        {
            handPoses?.Reset();
            if (wristMenu) wristMenu.ResetForWorldChange();
            paused = value;
            if (!initialized) return;
            if (skills) { skills.CancelInputOnly(); if (skills.effects) skills.effects.ClearAll(); }
            if (locomotion) locomotion.ResetInput();
        }
        void OnDisable()
        {
            handPoses?.Reset();
            if (!initialized) return;
            // Scene shutdown does not guarantee sibling component destruction order.
            // Use Unity's alive-object checks before touching native Behaviour properties.
            if (skills)
            {
                skills.CancelInputOnly();
                if (skills.effects) { skills.effects.ClearAll(); skills.effects.enabled = false; }
                skills.enabled = false;
            }
            if (locomotion) locomotion.ResetInput();
        }
        void OnEnable()
        {
            handPoses?.Reset();
            if (!initialized) return;
            if (skills) { if (skills.effects) skills.effects.enabled = true; skills.enabled = true; }
            if (locomotion) locomotion.ResetInput();
        }
        void OnDestroy()
        {
            if (locomotion) { locomotion.CancelRequested -= CancelCasting; locomotion.Moved -= OnMoved; locomotion.GameClock = null; }
            if (skills)
            {
                skills.enabled = false;
                skills.ActivationOverride = null; skills.GestureInputBlocked = null; skills.VoiceInputBlocked = null; skills.CancelOverride = null;
                if (ReferenceEquals(skills.ActivationPolicy,this)) skills.ActivationPolicy = null;
                skills.ExclusiveInputBlocked = null; skills.VoiceOwnershipDetail = null;
                skills.DeferVoiceUntilLateUpdate = false;
                if (originalCatalog) skills.catalog = originalCatalog;
                if (skills.effects) { skills.effects.ProjectileLaunched -= OnProjectileLaunched; skills.effects.ClearAll(); skills.effects.enabled = false; skills.effects.GameClock = null; skills.effects.ThrowGripProvider = null; skills.effects.RightHandActionPolicy = null; }
            }
            runtimeSkills?.Dispose();
        }
    }
}
