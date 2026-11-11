using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
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
    public sealed partial class LinearRpgController : MonoBehaviour, ISkillActivationPolicy
    {
        [Header("一、场景与配置")]
        [InspectorName("技能输入控制器")] public SkillInputController skills;
        [InspectorName("异步技能资源清单"), Tooltip("设置时按技能 ID 异步装配；旧场景仍可使用技能控制器的直引目录。")]
        public SkillResourceManifest skillResources;
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
        public PlayerModel Model => playerObject ? playerObject.Model : null;
        public IPlayerReadModel PlayerView => Model;
        public ProgressionController Growth { get; private set; }
        /// <summary>导出最近一次检查点的独立副本。正在重试写盘时，返回更新的待保存检查点。</summary>
        public RpgProgressData CaptureProgressSnapshot() => checkpoints?.CaptureLatest();

        /// <summary>技能知识、解锁或章节许可变化的版本号，供界面刷新缓存。</summary>
        public int ProgressRevision { get; private set; }
        /// <summary>最新进度已在会话生效，但仍等待磁盘保存。</summary>
        public bool CheckpointSavePending => checkpoints?.Pending == true;
        string[] StartingSkills => chapter.InitialSkills;
        /// <summary>是否已了解该技能；此状态不代表拥有永久施放资格。</summary>
        public bool IsSkillDiscovered(string id) => Model?.IsDiscovered(id) == true;
        /// <summary>是否永久解锁该技能。</summary>
        public bool IsSkillUnlocked(string id) => Model?.IsUnlocked(id) == true;
        /// <summary>综合永久解锁与章节授权；技能资源和冷却在施放入口另行判断。</summary>
        public bool IsSkillAvailable(string id) => Model != null && chapter &&
            (chapter.AllowsTrialSkill(id) || (IsSkillUnlocked(id) && chapter.AllowsPermanentSkill(id)));
        /// <summary>技能或章节许可变化时使教学界面的缓存失效。</summary>
        internal void RefreshProgress() => ProgressRevision++;

        internal ProgressCommitResult ApplyGrowthChange(Func<PlayerModel, bool> change)
        {
            if (!CanAct || committingPlayerState || CastTransactionActive) return ProgressCommitResult.Rejected;
            committingPlayerState = true;
            try
            {
                var candidate = Model.ForkGrowth();
                if (!change(candidate)) return ProgressCommitResult.Rejected;
                var draft = candidate.Export(Player.CaptureCheckpoint(GameTime), SaveNode(StageIndex, CampaignComplete).nodeId);
                if (!checkpoints.Validate(draft, out _) || !ValidateProgress(draft)) return ProgressCommitResult.Rejected;
                Model.AdoptGrowth(candidate);
                ApplyUpgrades();
                QueueCheckpoint(draft);
                return PersistCheckpoint(false) ? ProgressCommitResult.Saved : ProgressCommitResult.PendingSave;
            }
            finally { committingPlayerState = false; }
        }
        bool committingPlayerState;
        internal bool StateCommitInProgress => committingPlayerState;
        bool publishingCastFeedback;
        bool PublishingCast => publishingCastFeedback || (skills && skills.effects && skills.effects.IsPublishingCast);
        bool CastTransactionActive => publishingCastFeedback || (skills && skills.effects && skills.effects.IsCastingTransaction) ||
            (Player?.CastInProgress ?? false);
        readonly Queue<Action> confirmedCastCommands = new();

        // 确认监听不直接嵌套结算。下一次命令阶段按顺序重验；读档、倒下和离开场景清除旧命令。
        void RunConfirmedCastCommands()
        {
            int count = confirmedCastCommands.Count;
            while (count-- > 0 && confirmedCastCommands.Count > 0)
                confirmedCastCommands.Dequeue().Invoke();
        }
        public string Status { get; private set; } = "等待初始化";
        public string StageName => stages.Length > StageIndex && stages[StageIndex] ? stages[StageIndex].displayName : "未配置阶段";
        public string SavePath { get; private set; }
        public int StageIndex => Model?.Chapter.StageIndex ?? 0;
        public double GameTime { get; private set; }
        public bool HazardsEnabled { get; private set; }
        public bool IsReady => isActiveAndEnabled && initialized && (!usesSkillResources || (skillCatalogReady && startCalled));
        public bool CanRetrySkillAssembly => isActiveAndEnabled && usesSkillResources && assemblyFailed && skillLoad == null;
        public bool CanAct => IsReady && Player != null && Player.Alive && !paused && Time.timeScale > 0;
        public bool LastCastCreatedProjectile { get; private set; }
        /// <summary>目标文字由当前章节解释，RPG 不区分首章或训练场。</summary>
        public string ObjectiveStatus => chapter ? chapter.ObjectiveText : "未配置章节流程";
        public bool CampaignComplete => Model?.Chapter.CampaignComplete == true;

        RuntimeSkillCatalog runtimeSkills;
        IRpgProgressConstraint[] progressConstraints = Array.Empty<IRpgProgressConstraint>();
        public RpgSkillShop SkillShop { get; private set; }
        internal RpgItemInventory ItemInventory { get; private set; }
        CheckpointSaveCoordinator checkpoints;
        CancellationTokenSource skillLoad;
        bool usesSkillResources, skillCatalogReady, assemblyFailed, startCalled, stageEntered;
        PlayerInputController input;
        public PlayerInputController Input => input ??= new PlayerInputController(this);
        internal bool InputPaused => paused;
        string suspendedStatus;
        SkillCatalog originalCatalog;
        bool paused, initialized, wasNavigation, wasUi;
        public bool UiInputOwned => Input.UiOwnsInput;
        public string MovementBlockReason => Input.MovementBlockReason;
        string lastMovementGate;
        public string LastDebugMove { get; private set; }
        long activationSequence, damageSequence;
        readonly string sessionId = Guid.NewGuid().ToString("N");
        public string HandPoseStatus => Input.HandPoseStatus;
        public event Action<RpgActionCue> Feedback;
        /// <summary>进度、角色和落点全部恢复后通知章节重建场景，不负责逐句台词续播。</summary>
        public event Action CheckpointRestored;

        /// <summary>校验接线、建立运行时技能副本和玩家，再创建新进度或读取磁盘检查点。</summary>
        void Awake()
        {
            originalCatalog = skills ? skills.catalog : null;
            usesSkillResources = skillResources;
            if (usesSkillResources) { SuspendSkillCatalog(); return; }
            try
            {
                ValidateSetup();
                runtimeSkills = new RuntimeSkillCatalog(originalCatalog);
                InitializeSession(ReadStartupCheckpoint(originalCatalog.skills.Select(s => s.skillId),
                    originalCatalog.skills.Select(CooldownGroup)));
            }
            catch (Exception e)
            {
                Status = "线性RPG初始化失败：" + e.Message;
                Debug.LogError(Status, this);
                if (skills) skills.enabled = false;
                enabled = false;
            }
        }

        /// <summary>资源失败后显式重试；禁用、已就绪或正在进行的会话不再发起第二次加载。</summary>
        public bool RetrySkillAssembly()
        {
            if (!usesSkillResources || !isActiveAndEnabled || skillCatalogReady || skillLoad != null) return false;
            var attempt = new CancellationTokenSource();
            skillLoad = attempt;
            assemblyFailed = false;
            Status = "正在按 ID 装配玩家技能";
            AssembleSkills(attempt);
            return true;
        }

        async void AssembleSkills(CancellationTokenSource attempt)
        {
            RuntimeSkillCatalog prepared = null;
            try
            {
                var requestedManifest = skillResources;
                // v2 只依赖业务 ID、冷却组及数值上限。先读快照，装配完成后才提交到当前玩家。
                (RpgProgressData progress, string diagnostic) checkpoint = default;
                if (!initialized)
                {
                    if (!requestedManifest) throw new InvalidOperationException("缺少技能资源清单");
                    if (requestedManifest.Validate() is string error) throw new InvalidOperationException(error);
                    checkpoint = ReadStartupCheckpoint(requestedManifest.entries.Select(entry => entry.id),
                        requestedManifest.entries.Select(entry => entry.cooldownGroup));
                }
                prepared = await RuntimeSkillCatalog.LoadAsync(requestedManifest, attempt.Token);
                if (!this || !isActiveAndEnabled || !ReferenceEquals(skillLoad, attempt)) return;
                if (!requestedManifest || requestedManifest != skillResources)
                    throw new InvalidOperationException("装配期间技能资源清单已更换");
                if (!skills || !skills.effects) throw new InvalidOperationException("缺少技能输入或效果组件");
                runtimeSkills = prepared;
                prepared = null;
                skills.catalog = runtimeSkills.Catalog;
                ValidateSetup();
                if (!initialized) { InitializeSession(checkpoint); suspendedStatus = Status; }
                else { ApplyUpgrades(); RefreshProgress(); Status = suspendedStatus ?? chapter.InitialStatus; }
                skills.effects.enabled = true;
                skills.enabled = true;
                skills.InitializeMatcher();
                skills.BeginFreshPlayerInput(paused);
                Input.Invalidate();
                // 首次进入前恢复阶段与位置。启停重装只替换资源，保留当前玩家进度与位置。
                skillCatalogReady = true;
                if (startCalled) EnterInitialStage();
            }
            catch (Exception e)
            {
                if (this && ReferenceEquals(skillLoad, attempt))
                {
                    SuspendSkillCatalog();
                    assemblyFailed = true;
                    Status = "技能装配失败，可重试：" + e.Message;
                    Debug.LogError(Status, this);
                }
            }
            finally
            {
                prepared?.Dispose();
                if (ReferenceEquals(skillLoad, attempt)) skillLoad = null;
                attempt.Dispose();
            }
        }

        void SuspendSkillCatalog()
        {
            if (skillCatalogReady) suspendedStatus = Status;
            skillCatalogReady = false;
            if (initialized && wristMenu) wristMenu.ResetForWorldChange();
            if (skills)
            {
                skills.enabled = false;
                skills.DetachPlayerMatcher();
                if (skills.effects) { skills.effects.ClearAll(); skills.effects.enabled = false; }
                skills.catalog = null;
            }
            runtimeSkills?.Dispose();
            runtimeSkills = null;
        }

        (RpgProgressData progress, string diagnostic) ReadStartupCheckpoint(IEnumerable<string> skillIds, IEnumerable<string> cooldownGroups)
        {
            ValidateSaveFileName();
            SavePath = Path.Combine(Application.persistentDataPath, saveFileName);
            var store = new RpgSaveStore(UnityRpgSaveJson.Instance, SavePath, "linear-rpg-player", skillIds,
                playerDefinition ? playerDefinition.maxHealth : 100, playerDefinition ? playerDefinition.maxMana : 100, cooldownGroups);
            checkpoints = new CheckpointSaveCoordinator(store, UnityRpgSaveJson.Instance);
            return checkpoints.ReadStartup(autoLoad);
        }

        void InitializeSession((RpgProgressData progress, string diagnostic) checkpoint)
        {
            playerObject = GetComponent<PlayerObj>();
            if (!playerObject) throw new InvalidOperationException("缺少玩家实体组件");
            playerObject.Initialize(playerDefinition);
            Model.Chapter.Configure(stages.Select(stage => stage.stageId));
            var runtimeCatalog = runtimeSkills.Catalog;
            skills.catalog = runtimeCatalog;
            if (!Model.Unlock(StartingSkills) || !Model.Discover(chapter.InitialDiscoveries))
                throw new InvalidOperationException("角色初始技能 ID 无效或超出上限");
            var initial = Model.Export(Player.CaptureCheckpoint(GameTime), checkpointNodes[0].nodeId);
            Status = chapter.InitialStatus;
            if (checkpoint.progress != null || checkpoint.diagnostic != null)
            {
                if (ValidateProgress(checkpoint.progress))
                { initial = checkpoint.progress; Status = checkpoint.diagnostic ?? "已读取检查点"; }
                else { checkpoints.BlockWrites(); Status = "读取失败，当前使用临时新角色，已禁止覆盖原存档：" + (checkpoint.diagnostic ?? "阶段、奖励或检查点不属于当前关卡"); }
            }
            Model.RestoreGrowth(initial);
            Growth = new ProgressionController(this);
            checkpoints.SetRestored(initial);
            RefreshProgress();
            Player.RestoreCheckpoint(initial.checkpoint, GameTime, out _);
            ApplyUpgrades();
            skills.enableOpenHandCancel = false;
            skills.effects.BindVitals(Player);
            skills.ActivationPolicy = this;
            skills.GestureInputBlocked = () => !Input.CanCast(out _);
            skills.VoiceInputBlocked = () => !Input.CanCast(out _);
            skills.InputGeneration = () => Input.Generation;
            skills.ExclusiveInputBlocked = () => UiInputOwned;
            skills.VoiceOwnershipDetail = () => UiInputOwned ? "腕部界面：" + wristMenu.OwnershipReason :
                Input.CanCast(out var reason) ? "输入已恢复" : reason;
            skills.CancelOverride = CancelCasting;
            skills.DeferVoiceUntilLateUpdate = true;
            skills.effects.GameClock = () => GameTime;
            skills.effects.RightHandActionPolicy = () => CanAct && skills.isActiveAndEnabled && !UiInputOwned && !Input.InteractionOwnsHand &&
                !locomotion.NavigationActive && !locomotion.EncounterSlideActive;
            skills.effects.ThrowGripProvider = (left, settings) => Input.ReadThrowGrip(left, settings, Time.realtimeSinceStartupAsDouble);
            locomotion.GameClock = () => GameTime;
            locomotion.encounterModesEnabled = true;
            skills.effects.ProjectileLaunched += OnProjectileLaunched;
            locomotion.CancelRequested += CancelCasting;
            locomotion.Moved += OnMoved;
            if (!GetComponent<RpgDamageReceiver>()) gameObject.AddComponent<RpgDamageReceiver>();
            initialized = true;
        }

        void Start()
        {
            startCalled = true;
            if (IsReady) EnterInitialStage();
        }

        void EnterInitialStage()
        {
            if (stageEntered) return;
            ApplyStage();
            var node = FindCheckpoint(checkpoints.CheckpointId);
            if (node && !locomotion.TryRestoreCheckpoint(node)) Status = "检查点落地失败：" + locomotion.Status;
            stageEntered = true;
            // Do not overwrite the load diagnostic with the relocation's input reset.
        }

        void LateUpdate()
        {
            if (!IsReady) return;
            RunConfirmedCastCommands();
            if (checkpoints.RetryDue(Time.realtimeSinceStartupAsDouble)) RunCheckpointCommand(() => PersistCheckpoint(true));
            if (!paused && Time.timeScale > 0) { GameTime += Time.deltaTime; Player.Tick(GameTime); }
            var frame = Input.ReadNavigationFrame(Time.realtimeSinceStartupAsDouble);
            if (wristMenu && wristMenu.isActiveAndEnabled)
                UpdateWristUi(frame,Input.ReadWristMenuFrame(frame),paused || Time.timeScale <= 0 || Input.InteractionOwnsHand || skills.effects.HasOccupiedHand || (!Player.Alive && !wristMenu.wornDevice),Time.realtimeSinceStartupAsDouble);
            else SyncWristOwnership(); // A menu disabled while active also returns gameplay ownership.
            var gate = MovementBlockReason;
            if (gate != lastMovementGate)
            {
                skills.TraceCast("movement_gate", gate ?? "移动输入已恢复");
                lastMovementGate = gate;
            }
            // Skip the whole locomotion recognizer: its low-hands cancellation runs before blockActions.
            if (!UiInputOwned) locomotion.ProcessInput(frame, Input.InteractionOwnsHand || (skills.effects.HasRightHeldObject && !skills.effects.HasRightThrowObject), !CanAct, Time.realtimeSinceStartupAsDouble);
            bool navigating = locomotion.NavigationActive;
            if (navigating != wasNavigation) { Input.InvalidateRequests(); skills.CancelInputOnly(); }
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
            if (!IsReady) return;
            bool confirmed=wristMenu.PrepareWristTap(ui,hardBlocked,now);
            bool sealBusy=!confirmed && !UiInputOwned && !wristMenu.PanelOpen && !wristMenu.State.LauncherOpen && (locomotion.SealProtectionActive ||
                (frame.leftValid && frame.rightValid && Vector3.Distance(frame.leftWrist,frame.rightWrist)<locomotion.gestures.sealEnterDistance));
            string blocked = hardBlocked ? (Input.InteractionOwnsHand || (skills && skills.effects && skills.effects.HasOccupiedHand) ? "手上有持有物" : "角色当前不能行动") :
                !confirmed && locomotion.NavigationActive ? "导航操作中" : sealBusy ? "双手处于结印保护范围" : null;
            wristMenu.Tick(ui,this,blocked!=null,now,blocked);
            SyncWristOwnership();
        }

        void SyncWristOwnership()
        {
            bool owns=UiInputOwned;
            if(owns==wasUi)return;
            Input.InvalidateRequests();
            wasUi=owns;
            if(skills){skills.CancelInputOnly();skills.InvalidateEarlierVoice();}
            locomotion.ResetInput();
        }

        public bool DebugCast(string skillId) => IsReady && runtimeSkills.TryGet(skillId, out var skill) && skills.effects.TryCast(skill);

        SkillCastResult ISkillActivationPolicy.TryActivate(SkillDefinition skill) => TryActivate(skill);
        string ISkillActivationPolicy.ActivationStatus => Status;

        SkillCastResult TryActivate(SkillDefinition skill)
        {
            if (PublishingCast) { confirmedCastCommands.Enqueue(() => TryActivate(skill)); return SkillCastResult.Rejected(SkillCastFailure.Deferred, "已排到下个命令阶段"); }
            if (CastTransactionActive || committingPlayerState) return SkillCastResult.Rejected(SkillCastFailure.Busy, "上一个玩家命令尚未结束");
            if (!IsReady || !CanAct) return Deny(Player != null && !Player.Alive ? SkillCastFailure.Dead : SkillCastFailure.NotReady, "当前无法施法");
            if (!skill || !skills.ConfiguredSkills.Contains(skill)) return Deny(SkillCastFailure.InvalidRequest, "技能不属于当前玩家");
            if (!Input.CanCast(out var blocked)) return Deny(SkillCastFailure.InputBlocked, blocked);
            if (!IsSkillAvailable(skill.skillId))
                return Deny(SkillCastFailure.NotPermitted, (IsSkillDiscovered(skill.skillId) ? "此阶段暂不能施放：" : "尚未解锁：") + skill.displayName);
            string request = sessionId + ":cast:" + (++activationSequence);
            int previousProjectile = skills.effects.HeldProjectileInstanceId;
            var result = skills.effects.CommitCast(skill, Player, request, skill.manaCost, GameTime,
                () => CanAct && Input.CanCast(out _) && IsSkillAvailable(skill.skillId));
            bool accepted = result.Confirmed;
            LastCastCreatedProjectile = accepted && skills.effects.HeldProjectileInstanceId != 0 &&
                skills.effects.HeldProjectileInstanceId != previousProjectile;
            Status = accepted ? $"{skill.displayName}已准备，消耗 {skill.manaCost:0} MP" : result.Detail;
            publishingCastFeedback = accepted;
            try { RpgFeedbackSignals.Raise(Feedback, accepted ? RpgActionCue.CastReady : RpgActionCue.Blocked, this); }
            finally { publishingCastFeedback = false; }
            return result;

            SkillCastResult Deny(SkillCastFailure failure, string detail)
            {
                Reject(detail);
                return SkillCastResult.Rejected(failure,detail);
            }
        }

        public void CancelCasting()
        {
            if (committingPlayerState) return;
            if (PublishingCast) { confirmedCastCommands.Enqueue(CancelCasting); return; }
            if (!Input.CanCancel(out _)) return;
            Input.Invalidate();
            skills.CancelInputOnly(); skills.effects.CancelUnreleased(); locomotion.CancelPendingNavigation();
            Status = "已取消待施法输入并收起手中法宝。已消耗MP和CD不返还。";
            RpgFeedbackSignals.Raise(Feedback, RpgActionCue.Cancelled, this);
        }

        public bool DebugMove(bool dodge, Vector2 direction)
        {
            if (committingPlayerState) return false;
            bool moved = !Input.CanMove(dodge, out string reason) ? Reject(reason) : dodge
                ? locomotion.TryDebugDodge(direction, Time.realtimeSinceStartupAsDouble)
                : locomotion.TryDebugStep(direction, Time.realtimeSinceStartupAsDouble);
            LastDebugMove = reason ?? locomotion.Status;
            skills?.TraceCast("movement_debug", LastDebugMove);
            return moved;
        }

        public bool ReceiveDamage(string hitId, float amount) => ReceiveDamage(hitId, amount, null);

        internal bool ReceiveDamage(string hitId, float amount, EnemyAttackHit? attack)
        {
            if (PublishingCast)
            {
                confirmedCastCommands.Enqueue(() => ReceiveDamage(hitId, amount, attack));
                return false;
            }
            bool canAct = CanAct && !committingPlayerState && !CastTransactionActive;
            PlayerDamageResult damage = default;
            bool accepted = canAct && playerObject.TryReceiveDamage(hitId, amount, GameTime, out damage);
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
            if (damage.Absorbed > 0) skills.effects.PresentShieldHit(damage.Absorbed);
            Status = $"受到 {damage.Requested:0} 伤害，护盾吸收 {damage.Absorbed:0}，生命损失 {damage.HealthLost:0}";
            RpgFeedbackSignals.Raise(Feedback, RpgActionCue.Damage, this);
            if (!Player.Alive)
            {
                confirmedCastCommands.Clear();
                skills.CancelInputOnly(); skills.effects.ClearOnPlayerDeath(); locomotion.ResetInput();
                Status = "角色已倒下。恢复检查点后重试；头部追踪保持正常。";
            }
            return true;
        }
        public void DebugDamage(float amount) => ReceiveDamage(sessionId + ":debug-hit:" + (++damageSequence), amount);

        /// <summary>捕获当前阶段、技能和角色资源；正式写盘成功后才更新最后成功快照。</summary>
        public bool SaveCheckpoint()
        {
            if (PublishingCast) { confirmedCastCommands.Enqueue(() => SaveCheckpoint()); return false; }
            return RunCheckpointCommand(SaveCurrentState);
        }

        bool SaveCurrentState()
        {
            if (!CanAct) return Reject("仅存活且未暂停时可保存检查点");
            var candidate = CaptureSaveCandidate();
            QueueCheckpoint(candidate);
            return PersistCheckpoint(false);
        }

        /// <summary>复制当前进度，补入章节检查点与资源，避免保存快照继续跟随会话变化。</summary>
        RpgProgressData CaptureSaveCandidate()
        {
            return Model.Export(Player.CaptureCheckpoint(GameTime), SaveNode(StageIndex, CampaignComplete).nodeId);
        }

        /// <summary>排队保存最新快照；后来的有效进度覆盖更早快照，避免重试把玩家回滚。</summary>
        void QueueCheckpoint(RpgProgressData candidate)
        {
            checkpoints.Queue(candidate, Time.realtimeSinceStartupAsDouble);
            RefreshProgress();
        }

        /// <summary>校验并持久化快照；失败保留诊断与待重试状态，不假报保存成功。</summary>
        bool PersistCheckpoint(bool retry)
        {
            if (!CheckpointSavePending) return true;
            if (!checkpoints.TryWrite(Time.realtimeSinceStartupAsDouble, out string reason))
            {
                if (retry) return false;
                return Reject(checkpoints.WritesBlocked ? reason : "进度在本次游戏中已生效，等待存档：" + reason);
            }
            RefreshProgress();
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
            if (PublishingCast) { confirmedCastCommands.Enqueue(() => RestoreCheckpoint()); return false; }
            return RunCheckpointCommand(RestoreSavedState);
        }

        bool RunCheckpointCommand(Func<bool> command)
        {
            if (!IsReady || committingPlayerState || CastTransactionActive) return false;
            committingPlayerState = true;
            try { return command(); }
            finally { committingPlayerState = false; }
        }

        bool RestoreSavedState()
        {
            if (paused || Time.timeScale <= 0) return Reject("暂停时不能恢复检查点");
            if (!checkpoints.TryReadForRestore(out var restored, out string reason)) return Reject(reason);
            if (!ValidateProgress(restored) || restored.checkpoint.health <= 0) return Reject("检查点无效或生命为零，未改变当前状态");
            NavigationNode node = FindCheckpoint(restored.checkpointId);
            if (!node || !locomotion.TryRestoreCheckpoint(node)) return Reject("检查点落地失败：" + locomotion.Status);
            if (!Player.RestoreCheckpoint(restored.checkpoint, GameTime, out string error)) return Reject(error);
            confirmedCastCommands.Clear();
            Input.Invalidate();
            Model.RestoreGrowth(restored); checkpoints.SetRestored(restored); ApplyUpgrades();
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
            if (!IsReady) return false;
            if (PublishingCast)
            {
                int requestedStage = StageIndex;
                confirmedCastCommands.Enqueue(() => { if (requestedStage == StageIndex) TryAdvanceStage(); });
                return false;
            }
            if (CastTransactionActive || committingPlayerState) return false;
            if (!CanAct || !chapter || !chapter.isActiveAndEnabled) return Reject("章节当前不能推进");
            return chapter.TryAdvanceStage(out string reason) || Reject(reason);
        }

        /// <summary>只接受当前章节的有效完成请求，统一结算阶段奖励并保存检查点。</summary>
        public bool CompleteChapterObjective(RpgChapterFlow source) => CompleteChapterObjective(source, null);

        internal bool CompleteChapterObjective(RpgChapterFlow source, NavigationNode travelTo)
        {
            if (source != chapter || !source || !source.isActiveAndEnabled || source.Owner != this || !CanAct || committingPlayerState || CastTransactionActive || CampaignComplete || !source.ObjectiveMet) return false;
            committingPlayerState = true;
            try { return CommitStage(stages[StageIndex], travelTo); }
            finally { committingPlayerState = false; }
        }

        /// <summary>统一发放一次阶段奖励、更新技能和阶段，然后保存；章节不得自行重复结算。</summary>
        bool CommitStage(RpgStageDefinition stage, NavigationNode travelTo)
        {
            var next = Model.ForkGrowth();
            if (!next.ClaimStage(stage.stageId, stage.rewardCurrency, stage.unlockSkillIds)) return Reject("此阶段奖励已领取或进度已达上限");
            var checkpointNode = SaveNode(next.Chapter.StageIndex, next.Chapter.CampaignComplete);
            var checkpoint = next.Export(Player.CaptureCheckpoint(GameTime), checkpointNode.nodeId);
            if (!checkpoints.Validate(checkpoint, out string error) || !ValidateProgress(checkpoint))
                return Reject(error ?? "阶段奖励或检查点不符合当前章节");
            if (travelTo && !locomotion.TryRestoreCheckpoint(travelTo))
                return Reject("下一检查点被阻挡，阶段与奖励保持原样：" + locomotion.Status);
            Model.AdoptGrowth(next);
            confirmedCastCommands.Clear();
            skills.CancelInputOnly(); skills.effects.ClearAll(); locomotion.ResetInput();
            if (chapter.RestoreVitalsOnStageComplete)
            {
                Player.Respawn(GameTime);
                checkpoint.checkpoint = Player.CaptureCheckpoint(GameTime);
            }
            QueueCheckpoint(checkpoint);
            if (!CampaignComplete)
            {
                ApplyStage();
            }
            else
            {
                HazardsEnabled = false;
                SetEncounterCombat(false);
                chapter.RefreshStageGuidance();
            }
            PersistCheckpoint(false);
            Status = (CampaignComplete ? "全部线性试炼完成" : "进入「" + StageName + "」") + $"；获得 {stage.rewardCurrency} 货币" +
                (Status.StartsWith("检查点已保存", StringComparison.Ordinal) ? "；进度已保存" : "；" + Status);
            return true;
        }

        // Encounter transitions are explicit; the hazard training switch is independent.
        public void SetEncounterCombat(bool combat)
        {
            if (!initialized || (combat && !IsReady) || !locomotion || locomotion.EncounterCombat == combat) return;
            locomotion.SetEncounterCombat(combat);
            skills.TraceCast("locomotion_mode",combat ? "进入战斗" : "进入探索");
        }
        void OnProjectileLaunched() => locomotion.NotifyProjectileLaunched(Time.realtimeSinceStartupAsDouble);
        public void ToggleHazards()
        {
            if (!IsReady) return;
            if (roomControlsEncounter) { HazardsEnabled = false; Status = "战斗房间模式不启用练习区预警攻击"; return; }
            HazardsEnabled = !HazardsEnabled; Status = HazardsEnabled ? "攻击预警已开启" : "攻击预警已关闭";
        }

        /// <summary>通知当前章节重建阶段目标，再设置通用战斗模式及训练预警开关。</summary>
        void ApplyStage()
        {
            Input.Invalidate();
            chapter.OnStageEntered();
            chapter.RefreshStageGuidance();
            if (!roomControlsEncounter) SetEncounterCombat(!CampaignComplete && stages[StageIndex].startsInCombat);
            HazardsEnabled = !roomControlsEncounter && !CampaignComplete && stages[StageIndex].enableHazards;
        }
        /// <summary>由技能副本管理器按各自 ID 重建等级效果，不在会话中识别具体术式。</summary>
        void ApplyUpgrades() => runtimeSkills?.ApplyLevels(Model.SkillLevel);
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
            progressConstraints = GetComponentsInChildren<IRpgProgressConstraint>(true)
                .Where(module => ((Component)module).GetComponentInParent<LinearRpgController>(true) == this)
                .ToArray();
            SkillShop = progressConstraints.OfType<RpgSkillShop>().SingleOrDefault();
            ItemInventory = progressConstraints.OfType<RpgItemInventory>().SingleOrDefault();
            foreach (var module in progressConstraints) module.ValidateConfiguration(this);
            ValidateSaveFileName();
        }

        void ValidateSaveFileName()
        {
            if (string.IsNullOrWhiteSpace(saveFileName) || Path.GetFileName(saveFileName) != saveFileName || Path.IsPathRooted(saveFileName) || saveFileName is "." or "..") throw new InvalidOperationException("存档只能指定文件名");
        }

        public static bool PointerPoseNearHand(Pose aim,Vector3 wrist) => NarutoVR.SealLab.SealLab.FinitePose(aim) &&
            RpgGestureState.Finite(wrist) && Vector3.Distance(aim.position,wrist)<=.3f;
        void OnApplicationPause(bool value)
        {
            confirmedCastCommands.Clear();
            Input.Invalidate();
            if (wristMenu) wristMenu.ResetForWorldChange();
            paused = value;
            if (!initialized) return;
            if (skills) { skills.CancelInputOnly(); if (skills.effects) skills.effects.ClearAll(); }
            if (locomotion) locomotion.ResetInput();
        }
        void OnDisable()
        {
            confirmedCastCommands.Clear();
            Input.Invalidate();
            if (usesSkillResources)
            {
                var loading = skillLoad;
                skillLoad = null;
                loading?.Cancel();
                SuspendSkillCatalog();
                if (locomotion) locomotion.ResetInput();
                return;
            }
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
            Input.Invalidate();
            if (usesSkillResources) { RetrySkillAssembly(); return; }
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
                skills.GestureInputBlocked = null; skills.VoiceInputBlocked = null; skills.CancelOverride = null;
                if (ReferenceEquals(skills.ActivationPolicy,this)) skills.ActivationPolicy = null;
                skills.ExclusiveInputBlocked = null; skills.VoiceOwnershipDetail = null;
                skills.DeferVoiceUntilLateUpdate = false;
                skills.InputGeneration = null;
                skills.catalog = originalCatalog;
                if (skills.effects) { skills.effects.ProjectileLaunched -= OnProjectileLaunched; skills.effects.ClearAll(); skills.effects.enabled = false; skills.effects.GameClock = null; skills.effects.ThrowGripProvider = null; skills.effects.RightHandActionPolicy = null; }
            }
            runtimeSkills?.Dispose();
        }
    }
}
