using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NarutoVR.SealLab;
using Oculus.Interaction.Input;
using UnityEngine;

namespace NarutoVR.SealCombat.Rpg
{
    /// <summary>Single-player session adapter. Recognition, resources, effects and locomotion retain separate owners.</summary>
    [DefaultExecutionOrder(300)]
    public sealed class LinearRpgController : MonoBehaviour
    {
#if UNITY_EDITOR
        // Set only by the explicit Editor play-test harness, before the scene's Awake.
        public static string EditorTestSaveFile;
#endif
        [Header("一、场景与配置")]
        [InspectorName("技能输入控制器")] public SkillTestController skills;
        [InspectorName("导航与闪避")] public RpgLocomotionController locomotion;
        [InspectorName("玩家基础定义")] public RpgPlayerDefinition playerDefinition;
        [InspectorName("顺序关卡配置")] public RpgStageDefinition[] stages = Array.Empty<RpgStageDefinition>();
        [InspectorName("阶段训练靶")] public SkillTrainingTarget[] targets = Array.Empty<SkillTrainingTarget>();
        [InspectorName("各阶段检查点")] public NavigationNode[] checkpointNodes = Array.Empty<NavigationNode>();
        [Header("二、操作阈值")]
        [InspectorName("放下双手：低于头部（米）"), Range(.3f, 1)] public float lowHandsBelowHead = .55f;
        [InspectorName("伸直手指最大弯曲角"), Range(0, 90)] public float straightCurl = 25;
        [InspectorName("收拢手指最小弯曲角"), Range(0, 180)] public float foldedCurl = 45;
        [Header("三、进度与存档")]
        [InspectorName("启动时读取存档")] public bool autoLoad = true;
        [InspectorName("存档文件名"), Tooltip("保存到Application.persistentDataPath；不接受目录或绝对路径。")]
        public string saveFileName = "linear-rpg-v1.json";
        [InspectorName("击破全部靶后自动推进")] public bool autoAdvanceStages = true;
        [InspectorName("推进等待秒数"), Min(.5f)] public float stageDelay = 3;
        [InspectorName("隐藏法术购买价格"), Min(0)] public int hiddenSkillPrice = 60;
        [InspectorName("每次火球升级价格"), Min(0)] public int fireballUpgradePrice = 30;

        public RpgPlayerState Player { get; private set; }
        public RpgProgressData Progress { get; private set; }
        public string Status { get; private set; } = "等待初始化";
        public string StageName => stages.Length > StageIndex && stages[StageIndex] ? stages[StageIndex].displayName : "未配置阶段";
        public string SavePath { get; private set; }
        public int StageIndex { get; private set; }
        public double GameTime { get; private set; }
        public bool HazardsEnabled { get; private set; }
        public bool CanAct => isActiveAndEnabled && Player != null && Player.Alive && !paused && Time.timeScale > 0;
        public string ObjectiveStatus => CampaignComplete ? "全部试炼已完成，可继续练习或读取检查点" :
            $"击破训练靶 {targets.Count(t => t && t.IsDefeated)}/{targets.Length} · {(stages.Length > StageIndex && stages[StageIndex] ? stages[StageIndex].instructions : "")}";
        public bool CampaignComplete => Progress != null && stages.Length > 0 && stages.All(s => s && Progress.completedStageIds.Contains(s.stageId));

        readonly List<UnityEngine.Object> ownedAssets = new();
        readonly HashSet<string> receivedDamage = new(StringComparer.Ordinal);
        readonly Dictionary<ProjectileEffectDefinition, float> baseDamage = new();
        RpgSaveStore store;
        RpgProgressData lastCheckpoint;
        SkillCatalog originalCatalog;
        bool paused, initialized, wasNavigation, saveBlocked;
        double completedSince = -1;
        long activationSequence, damageSequence;
        readonly string sessionId = Guid.NewGuid().ToString("N");

        void Awake()
        {
            try
            {
#if UNITY_EDITOR
                if (!string.IsNullOrEmpty(EditorTestSaveFile))
                { saveFileName = EditorTestSaveFile; autoLoad = false; autoAdvanceStages = false; }
#endif
                ValidateSetup();
                Player = playerDefinition ? playerDefinition.CreateState() : new RpgPlayerState();
                Player.Tick(0);
                originalCatalog = skills.catalog;
                var runtimeCatalog = Instantiate(originalCatalog); ownedAssets.Add(runtimeCatalog);
                runtimeCatalog.skills = new List<SkillDefinition>();
                foreach (var asset in originalCatalog.skills)
                {
                    var copy = Instantiate(asset); ownedAssets.Add(copy);
                    if (asset.effectDefinition)
                    {
                        copy.effectDefinition = Instantiate(asset.effectDefinition); ownedAssets.Add(copy.effectDefinition);
                        if (copy.effectDefinition is ProjectileEffectDefinition projectile) baseDamage[projectile] = projectile.damage;
                    }
                    runtimeCatalog.skills.Add(copy);
                }
                skills.catalog = runtimeCatalog;
                SavePath = Path.Combine(Application.persistentDataPath, saveFileName);
                store = new RpgSaveStore(SavePath, "linear-rpg-player", runtimeCatalog.skills.Select(s => s.skillId),
                    Player.MaxHealth, Player.MaxMana, runtimeCatalog.skills.Select(CooldownGroup).Distinct());
                Progress = new RpgProgressData { checkpoint = Player.CaptureCheckpoint(GameTime), checkpointId = checkpointNodes[0].nodeId };
                Progress.unlockedSkillIds.Add("fireball");
                Status = "新角色：先练习火球与节点移动";
                if (autoLoad && (File.Exists(SavePath) || File.Exists(SavePath + ".bak")))
                {
                    if (store.TryLoad(out var loaded, out string reason) && ValidateProgress(loaded))
                    { Progress = loaded; Status = reason ?? "已读取检查点"; }
                    else { saveBlocked = true; Status = "读取失败，当前使用临时新角色，已禁止覆盖原存档：" + (reason ?? "阶段、奖励或检查点不属于当前关卡"); }
                }
                lastCheckpoint = CloneProgress(Progress);
                SelectStageFromProgress();
                Player.RestoreCheckpoint(Progress.checkpoint, GameTime, out _);
                ApplyUpgrades();
                skills.enableOpenHandCancel = false;
                skills.ActivationOverride = TryActivate;
                skills.GestureInputBlocked = () => !CanAct || locomotion.NavigationActive;
                skills.VoiceInputBlocked = () => !CanAct || locomotion.NavigationActive;
                skills.CancelOverride = CancelCasting;
                skills.DeferVoiceUntilLateUpdate = true;
                skills.effects.GameClock = () => GameTime;
                locomotion.GameClock = () => GameTime;
                locomotion.CancelRequested += CancelCasting;
                locomotion.Moved += OnMoved;
                initialized = true;
            }
            catch (Exception e)
            {
                Status = "线性RPG初始化失败：" + e.Message;
                Debug.LogError(Status, this);
                if (skills) skills.enabled = false;
                enabled = false;
            }
        }

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
            if (!paused && Time.timeScale > 0) { GameTime += Time.deltaTime; Player.Tick(GameTime); }
            var frame = ReadNavigationFrame(Time.realtimeSinceStartupAsDouble);
            locomotion.ProcessInput(frame, skills.effects.HasRightHeldObject, !CanAct, Time.realtimeSinceStartupAsDouble);
            bool navigating = locomotion.NavigationActive;
            if (navigating != wasNavigation) skills.CancelInputOnly();
            wasNavigation = navigating;
            // Suppress release on death/pause only: a held fireball still owns forward push during navigation.
            skills.effects.PushInputSuppressed = !CanAct;
            if (CanAct && !CampaignComplete && targets.Length > 0 && targets.All(t => t && t.IsDefeated))
            {
                if (completedSince < 0) completedSince = GameTime;
                if (autoAdvanceStages && GameTime - completedSince >= stageDelay) TryAdvanceStage();
            }
            else completedSince = -1;
        }

        public bool DebugCast(string skillId) => TryActivate(skills ? skills.ConfiguredSkills.FirstOrDefault(s => s && s.skillId == skillId) : null);

        bool TryActivate(SkillDefinition skill)
        {
            if (!CanAct || !skill || !skills.ConfiguredSkills.Contains(skill)) return Reject("当前无法施法");
            if (locomotion.NavigationActive) return Reject("导航准备中：放下左手后再开始结印或吟唱");
            if (!Progress.unlockedSkillIds.Contains(skill.skillId)) return Reject("尚未解锁：" + skill.displayName);
            string request = sessionId + ":cast:" + (++activationSequence);
            bool accepted = Player.TryActivate(request, skill.skillId, CooldownGroup(skill), skill.manaCost, skill.cooldownSeconds,
                GameTime, () => skills.effects.TryActivate(skill), out string reason);
            Status = accepted ? $"{skill.displayName}已准备，消耗 {skill.manaCost:0} MP" : reason + "；" + skills.effects.Status;
            return accepted;
        }

        public void CancelCasting()
        {
            if (!initialized) return;
            skills.CancelInputOnly(); skills.effects.CancelUnreleased(); locomotion.CancelPendingNavigation();
            Status = "已取消待施法输入与未发射火球；已生效护盾和飞行火球保留。已消耗MP和CD不返还。";
        }

        public bool DebugMove(bool dodge, Vector2 direction)
        {
            if (!CanAct) return Reject("死亡或暂停时不能移动");
            if (dodge && direction.y > 0 && skills.effects.HasRightHeldObject) return Reject("右手持球时前推属于发射；请实际前推或先取消火球");
            return dodge ? locomotion.TryDebugDodge(direction, Time.realtimeSinceStartupAsDouble) :
                locomotion.TryDebugStep(direction, Time.realtimeSinceStartupAsDouble);
        }

        public bool ReceiveDamage(string hitId, float amount)
        {
            // Deduplicate before touching the shield. Absorbed hits must not be replayable either.
            if (!CanAct || string.IsNullOrWhiteSpace(hitId) || !float.IsFinite(amount) || amount <= 0 || amount > 10000 ||
                receivedDamage.Count >= 65536 || !receivedDamage.Add(hitId)) return false;
            float remaining = skills.effects.AbsorbDamage(amount);
            float applied = 0;
            if (remaining > 0) Player.ApplyDamage(hitId, remaining, GameTime, out applied);
            Status = $"受到 {amount:0} 伤害，护盾吸收 {amount - remaining:0}，生命损失 {applied:0}";
            if (!Player.Alive)
            {
                skills.CancelInputOnly(); skills.effects.ClearAll(); locomotion.ResetInput();
                Status = "角色已倒下。恢复检查点后重试；头部追踪保持正常。";
            }
            return true;
        }
        public void DebugDamage(float amount) => ReceiveDamage(sessionId + ":debug-hit:" + (++damageSequence), amount);

        public bool SaveCheckpoint()
        {
            if (!CanAct) return Reject("仅存活且未暂停时可保存检查点");
            if (saveBlocked) return Reject("启动时存档验证失败；为保留原文件，本次临时会话禁止写入");
            var candidate = CloneProgress(Progress);
            candidate.checkpointId = checkpointNodes[StageIndex].nodeId;
            candidate.checkpoint = Player.CaptureCheckpoint(GameTime);
            if (!store.TrySave(candidate, out string reason)) return Reject(reason);
            Progress = candidate; lastCheckpoint = CloneProgress(candidate);
            Status = "检查点已保存（固定节点、角色进度、HP/MP和剩余CD）；临时效果与敌人血量不保存";
            return true;
        }

        public bool RestoreCheckpoint()
        {
            if (!initialized || paused || Time.timeScale <= 0) return Reject("暂停时不能恢复检查点");
            var restored = CloneProgress(lastCheckpoint);
            if (File.Exists(SavePath) || File.Exists(SavePath + ".bak"))
            {
                if (!store.TryLoad(out restored, out string reason)) return Reject(reason);
            }
            if (!ValidateProgress(restored) || restored.checkpoint.health <= 0) return Reject("检查点无效或生命为零，未改变当前状态");
            NavigationNode node = FindCheckpoint(restored.checkpointId);
            if (!node || !locomotion.TryRestoreCheckpoint(node)) return Reject("检查点落地失败：" + locomotion.Status);
            if (!Player.RestoreCheckpoint(restored.checkpoint, GameTime, out string error)) return Reject(error);
            Progress = restored; lastCheckpoint = CloneProgress(restored); SelectStageFromProgress(); ApplyUpgrades();
            skills.CancelInputOnly(); skills.effects.ClearAll(); locomotion.ResetInput();
            ApplyStage(); Status = "已恢复检查点；阶段目标重置，临时效果已清理";
            return true;
        }

        public bool TryAdvanceStage()
        {
            if (!CanAct || CampaignComplete || targets.Length == 0 || targets.Any(t => !t || !t.IsDefeated)) return Reject("先击破本阶段全部训练靶");
            var stage = stages[StageIndex];
            if (StageIndex + 1 < stages.Length && !locomotion.TryRestoreCheckpoint(checkpointNodes[StageIndex + 1]))
                return Reject("下一检查点被阻挡，阶段与奖励保持原样：" + locomotion.Status);
            if (!Progress.TryGrantReward("reward." + stage.stageId, stage.rewardCurrency, null)) return Reject("此阶段奖励已领取或进度已达上限");
            Progress.completedStageIds.Add(stage.stageId);
            foreach (string id in stage.unlockSkillIds) if (!Progress.unlockedSkillIds.Contains(id)) Progress.unlockedSkillIds.Add(id);
            skills.CancelInputOnly(); skills.effects.ClearAll(); locomotion.ResetInput();
            Player.Respawn(GameTime); // A completed encounter is a rest checkpoint; cooldowns still persist.
            if (!CampaignComplete)
            {
                StageIndex++; ApplyStage();
            }
            else HazardsEnabled = false;
            SaveCheckpoint();
            Status = (CampaignComplete ? "全部线性试炼完成" : "进入「" + StageName + "」") + $"；获得 {stage.rewardCurrency} 货币" +
                (Status.StartsWith("检查点已保存", StringComparison.Ordinal) ? "；进度已保存" : "；" + Status);
            return true;
        }

        public void ToggleHazards() { if (initialized) { HazardsEnabled = !HazardsEnabled; Status = HazardsEnabled ? "攻击预警已开启" : "攻击预警已关闭"; } }
        public bool PurchaseHiddenSkill()
        {
            if (!CanAct || !Progress.completedStageIds.Contains(stages[0].stageId)) return Reject("先完成第一阶段，再购买隐藏法术");
            if (!Progress.TryPurchaseSkill("buff.hidden", hiddenSkillPrice)) return Reject($"购买失败：需要 {hiddenSkillPrice} 货币，且不能重复购买");
            SaveCheckpoint(); return true;
        }
        public bool UpgradeFireball()
        {
            if (!CanAct || !Progress.TryUpgradeSkill("fireball", fireballUpgradePrice, 3)) return Reject($"升级失败：需要 {fireballUpgradePrice} 货币，最多升级3次");
            ApplyUpgrades(); SaveCheckpoint(); return true;
        }
        public void DebugDefeatTargets() { if (CanAct) foreach (var target in targets) if (target) target.ApplyHit(10000); }

        void ApplyStage()
        {
            foreach (var target in targets) { target.health = stages[StageIndex].targetHealth; target.resetAfterSeconds = 0; target.ResetTarget(); }
            HazardsEnabled = !CampaignComplete && stages[StageIndex].enableHazards; completedSince = -1;
        }
        void SelectStageFromProgress() { StageIndex = Array.FindIndex(stages, s => !Progress.completedStageIds.Contains(s.stageId)); if (StageIndex < 0) StageIndex = stages.Length - 1; }
        void ApplyUpgrades()
        {
            int level = Progress.skillLevels.FirstOrDefault(l => l.skillId == "fireball")?.level ?? 0;
            foreach (var pair in baseDamage) pair.Key.damage = pair.Value * (1 + level * .1f);
        }
        void OnMoved(Vector3 delta) { skills.CancelInputOnly(); skills.effects.ResetPushInput(); }
        static string CooldownGroup(SkillDefinition s) => string.IsNullOrEmpty(s.cooldownGroup) ? s.skillId : s.cooldownGroup;
        NavigationNode FindCheckpoint(string id) => checkpointNodes.FirstOrDefault(n => n && n.nodeId == id);
        bool Reject(string reason) { Status = reason; return false; }
        static RpgProgressData CloneProgress(RpgProgressData p) => JsonUtility.FromJson<RpgProgressData>(JsonUtility.ToJson(p));
        bool ValidateProgress(RpgProgressData p)
        {
            if (p == null || !FindCheckpoint(p.checkpointId) || p.checkpoint.health <= 0 || !p.unlockedSkillIds.Contains("fireball") ||
                !p.completedStageIds.SequenceEqual(stages.Take(p.completedStageIds.Count).Select(s => s.stageId))) return false;
            int stageIndex = Math.Min(p.completedStageIds.Count, stages.Length - 1);
            if (p.checkpointId != checkpointNodes[stageIndex].nodeId) return false;
            var rewards = p.completedStageIds.Select(id => "reward." + id).ToArray();
            if (p.claimedRewardIds.Count != rewards.Length || rewards.Any(id => !p.claimedRewardIds.Contains(id))) return false;
            foreach (var stage in stages.Take(p.completedStageIds.Count))
                if (stage.unlockSkillIds.Any(id => !p.unlockedSkillIds.Contains(id))) return false;
            return true;
        }

        void ValidateSetup()
        {
            if (!skills || !skills.catalog || !skills.effects || !skills.lab || !locomotion || !locomotion.head) throw new InvalidOperationException("缺少技能、效果、骨骼或导航接线");
            var errors = skills.catalog.Validate(); if (errors.Count != 0) throw new InvalidOperationException(string.Join("；", errors));
            if (!skills.catalog.skills.Any(s => s.skillId == "fireball") || !skills.catalog.skills.Any(s => s.skillId == "buff.hidden")) throw new InvalidOperationException("缺少火球或隐藏法术配置");
            if (stages.Length == 0 || checkpointNodes.Length != stages.Length || targets.Length == 0 || targets.Any(t => !t)) throw new InvalidOperationException("阶段、检查点或训练靶配置不完整");
            var ids = new HashSet<string>();
            foreach (var stage in stages)
                if (!stage || string.IsNullOrWhiteSpace(stage.stageId) || !ids.Add(stage.stageId) || !float.IsFinite(stage.targetHealth) || stage.targetHealth <= 0 ||
                    stage.rewardCurrency < 0 || stage.unlockSkillIds == null || stage.unlockSkillIds.Any(id => !skills.catalog.skills.Any(s => s.skillId == id)))
                    throw new InvalidOperationException("阶段配置无效或含未知技能");
            if (checkpointNodes.Any(n => !n)) throw new InvalidOperationException("检查点节点缺失");
            if (string.IsNullOrWhiteSpace(saveFileName) || Path.GetFileName(saveFileName) != saveFileName || Path.IsPathRooted(saveFileName) || saveFileName is "." or "..") throw new InvalidOperationException("存档只能指定文件名");
        }

        NavigationHandInput ReadNavigationFrame(double now)
        {
            var result = new NavigationHandInput(); var frame = skills.lab.Frame;
            if (frame == null) return result;
            result.leftObserved = frame.left.observedAt; result.rightObserved = frame.right.observedAt;
            result.leftValid = Wrist(frame.left, now, out result.leftWrist);
            result.rightValid = Wrist(frame.right, now, out result.rightWrist);
            int index = (int)HandJointId.HandIndexTip;
            if (result.leftValid && frame.left.jointValid[index]) result.leftIndex = World(frame.left.joints[index].position);
            else result.leftValid = false;
            result.leftPointing = result.leftValid && Curl("L.Index.Curl", false) && Curl("L.Middle.Curl", true) && Curl("L.Ring.Curl", true) && Curl("L.Pinky.Curl", true);
            result.rightOpen = result.rightValid && Curl("R.Index.Curl", false) && Curl("R.Middle.Curl", false) && Curl("R.Ring.Curl", false) && Curl("R.Pinky.Curl", false);
            result.bothHandsLow = result.leftValid && result.rightValid && Math.Abs(result.leftObserved - result.rightObserved) <= SealFeatureExtractor.MaximumHandSkew &&
                result.leftWrist.y < locomotion.head.position.y - lowHandsBelowHead && result.rightWrist.y < locomotion.head.position.y - lowHandsBelowHead;
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
        bool Curl(string id, bool folded)
        {
            foreach (var f in skills.lab.Features) if (f.id == id) return f.valid && f.highConfidence && (folded ? f.value >= foldedCurl : f.value <= straightCurl);
            return false;
        }
        void OnApplicationPause(bool value)
        {
            paused = value;
            if (!initialized) return;
            skills.CancelInputOnly(); skills.effects.ClearAll(); locomotion.ResetInput();
        }
        void OnDisable()
        {
            if (!initialized) return;
            skills.CancelInputOnly(); skills.effects.ClearAll(); locomotion.ResetInput();
            skills.enabled = false; skills.effects.enabled = false;
        }
        void OnEnable()
        {
            if (!initialized) return;
            skills.effects.enabled = true; skills.enabled = true;
            locomotion.ResetInput();
        }
        void OnDestroy()
        {
            if (locomotion) { locomotion.CancelRequested -= CancelCasting; locomotion.Moved -= OnMoved; locomotion.GameClock = null; }
            if (skills)
            {
                skills.enabled = false;
                skills.ActivationOverride = null; skills.GestureInputBlocked = null; skills.VoiceInputBlocked = null; skills.CancelOverride = null;
                skills.DeferVoiceUntilLateUpdate = false;
                if (originalCatalog) skills.catalog = originalCatalog;
                if (skills.effects) { skills.effects.ClearAll(); skills.effects.enabled = false; skills.effects.GameClock = null; }
            }
            foreach (var asset in ownedAssets) if (asset) Destroy(asset);
        }
    }
}
