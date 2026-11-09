using System;
using System.Linq;
using UnityEngine;

namespace NarutoVR.SealCombat.Rpg
{
    /// <summary>
    /// 训练场章节：汇总训练靶的击破状态、控制自动推进等待、确认下一检查点可以落地。
    /// 房间遭遇模式只提供训练说明，不使用练习靶完成阶段。奖励和存档统一交由 RPG 提交。
    /// </summary>
    [DefaultExecutionOrder(320), DisallowMultipleComponent, RequireComponent(typeof(TrainingTargetGroup))]
    public sealed class TrainingChapterFlow : RpgChapterFlow
    {
        /// <summary>本训练场所属的 RPG 控制器，必须与其 chapter 引用互相对应。</summary>
        public LinearRpgController rpg;
        /// <summary>新角色拥有的技能，由场景显式配置。</summary>
        public string[] initialSkills = Array.Empty<string>();
        /// <summary>此训练场持续开放的练习配方，不要求旧存档已有永久解锁记录。</summary>
        [InspectorName("训练场练习授权")]
        public string[] trialSkills = Array.Empty<string>();
        /// <summary>本对象上的训练设施，管理靶而不管理关卡奖励。</summary>
        public TrainingTargetGroup TargetGroup => GetComponent<TrainingTargetGroup>();
        /// <summary>全部击破后是否自动推进；关闭时等待显式推进请求。</summary>
        public bool autoAdvance = true;
        /// <summary>全靶击破后的游戏时间等待秒数。</summary>
        [Min(.5f)] public float advanceDelay = 3;
        /// <summary>所有靶首次击破时的游戏时钟；负数代表尚未满足或已重置。</summary>
        double completedSince = -1;
        /// <summary>仅在下一检查点确认可达之后，允许 RPG 提交本阶段。</summary>
        bool transitionReady;

        /// <inheritdoc/>
        public override LinearRpgController Owner => rpg;
        /// <inheritdoc/>
        public override string[] InitialSkills => initialSkills;
        /// <inheritdoc/>
        public override string[] InitialDiscoveries => trialSkills;
        /// <inheritdoc/>
        public override bool AllowsTrialSkill(string id) => Array.IndexOf(trialSkills, id) >= 0;
        /// <inheritdoc/>
        public override string InitialStatus => "训练场已就绪：练习已开放的术式与节点移动";
        /// <inheritdoc/>
        public override bool RestoreVitalsOnStageComplete => true;
        /// <summary>训练靶全部击破且章节未完成，才具备推进的基本条件。</summary>
        bool TargetsDefeated => rpg && rpg.CanAct && !rpg.CampaignComplete && !rpg.roomControlsEncounter &&
            TargetGroup.AllDefeated;
        /// <inheritdoc/>
        public override bool ObjectiveMet => transitionReady && TargetsDefeated;
        /// <inheritdoc/>
        public override string ObjectiveText => !rpg || rpg.stages.Length == 0 || !rpg.stages[rpg.StageIndex] ? "训练场尚未配置阶段" : rpg.roomControlsEncounter
            ? "练习靶自动恢复；进入尽头战斗房间迎敌，击败房间全部敌人后返回探索"
            : rpg.CampaignComplete ? "全部试炼已完成，可继续练习或读取检查点"
            : $"击破训练靶 {TargetGroup.DefeatedCount}/{TargetGroup.targets.Length} · {rpg.stages[rpg.StageIndex].instructions}";

        /// <summary>编辑器显式回归入口可关闭自动推进；正常运行只使用场景配置。</summary>
        void Awake()
        {
#if UNITY_EDITOR
            if (!string.IsNullOrEmpty(LinearRpgController.EditorTestSaveFile)) autoAdvance = false;
#endif
        }

        /// <summary>只在绑定本流程时累计自动推进等待；暂停使用 RPG 游戏时钟，不增加等待进度。</summary>
        void Update()
        {
            if (!rpg || rpg.chapter != this) return;
            if (!TargetsDefeated) { completedSince = -1; return; }
            if (completedSince < 0) { completedSince = rpg.GameTime; rpg.SetEncounterCombat(false); }
            if (autoAdvance && rpg.GameTime - completedSince >= advanceDelay) rpg.TryAdvanceStage();
        }

        /// <summary>首次进入、过关或读档后重置等待和训练靶，不继承上一阶段的击破状态。</summary>
        public override void OnStageEntered()
        {
            completedSince = -1;
            transitionReady = false;
            TargetGroup.ResetForStage(rpg.StageIndex);
        }

        /// <summary>手动与自动推进共用此入口；落点被阻挡时不发奖，也不改变阶段。</summary>
        public override bool TryAdvanceStage(out string reason)
        {
            reason = "先击破本阶段全部训练靶";
            if (!isActiveAndEnabled || rpg.chapter != this || !TargetsDefeated) return false;
            if (rpg.StageIndex + 1 < rpg.stages.Length && !rpg.locomotion.TryRestoreCheckpoint(rpg.checkpointNodes[rpg.StageIndex + 1]))
            { reason = "下一检查点被阻挡，阶段与奖励保持原样：" + rpg.locomotion.Status; return false; }
            transitionReady = true;
            try { return rpg.CompleteChapterObjective(this); }
            finally { transitionReady = false; }
        }

        /// <summary>训练场需要训练靶；普通目标章节不受这项要求约束。</summary>
        public override void ValidateChapter(LinearRpgController owner)
        {
            base.ValidateChapter(owner);
            if (!TargetGroup || !float.IsFinite(advanceDelay) || advanceDelay < .5f)
                throw new InvalidOperationException("训练场靶组或推进计时无效");
            TargetGroup.Validate(owner.stages.Length);
            if (trialSkills == null || trialSkills.Distinct().Count() != trialSkills.Length ||
                trialSkills.Any(id => string.IsNullOrWhiteSpace(id) || !owner.skills.catalog.skills.Any(skill => skill && skill.skillId == id)))
                throw new InvalidOperationException("训练场练习授权必须引用目录中的唯一技能 ID");
        }
    }
}
