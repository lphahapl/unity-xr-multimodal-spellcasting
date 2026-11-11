using System;
using System.Linq;
using UnityEngine;

namespace NarutoVR.SealCombat.Rpg
{
    /// <summary>将一个 RPG 阶段与需要完成的目标和临时技能绑定。</summary>
    [Serializable]
    public sealed class ChapterObjectiveStage
    {
        /// <summary>本组目标对应的阶段资产，必须与 RPG 同下标阶段一致。</summary>
        public RpgStageDefinition stage;
        /// <summary>本阶段目标集合，采用全部满足的 AND 关系。</summary>
        [Tooltip("本阶段所有目标都满足后才推进。每个阶段至少配置一个目标。")]
        public ChapterObjective[] objectives = Array.Empty<ChapterObjective>();
        /// <summary>本阶段可临时使用的技能 ID，切换阶段后不继续授权。</summary>
        public string[] trialSkills = Array.Empty<string>();
        [InspectorName("本阶段高亮目标（可选）")] public GameObject guidanceTarget;
    }

    /// <summary>
    /// 普通章节流程：按 RPG 阶段启用目标，以 AND 关系汇总结果，统一请求 RPG 保存和推进。
    /// 不要求首章教学、训练靶、铜叶机关或旁白。需要逐句教学的章节可另行实现 RpgChapterFlow。
    /// </summary>
    [DefaultExecutionOrder(320), DisallowMultipleComponent]
    public sealed class ObjectiveChapterFlow : RpgChapterFlow
    {
        /// <summary>当前流程绑定的 RPG。</summary>
        public LinearRpgController rpg;
        /// <summary>按顺序配置的阶段及其目标。</summary>
        public ChapterObjectiveStage[] stages = Array.Empty<ChapterObjectiveStage>();
        /// <summary>可选通关检查点。</summary>
        public NavigationNode finalCheckpoint;
        /// <summary>新档初始永久技能。</summary>
        public string[] initialSkills = Array.Empty<string>();
        /// <summary>新档初始了解的技能，独立于教学目录。</summary>
        public string[] initialDiscoveries = Array.Empty<string>();
        /// <summary>章节完成后的目标文字。</summary>
        [TextArea] public string completedText = "本章完成";
        /// <summary>每阶段过关是否恢复角色资源。</summary>
        public bool restoreVitalsOnStageComplete;
        /// <summary>是否启用世界导航刻线。</summary>
        public bool worldNavigationMarks;
        /// <summary>是否为实际探索移动播放脚步。</summary>
        public bool explorationFootsteps = true;
        /// <summary>已接入的阶段下标，负数表示需要在下一帧接入。</summary>
        int index = -1;
        /// <summary>当前运行的目标组；离开时逐个 End。</summary>
        ChapterObjectiveStage active;

        /// <inheritdoc/>
        public override LinearRpgController Owner => rpg;
        /// <inheritdoc/>
        public override NavigationNode FinalCheckpoint => finalCheckpoint;
        /// <inheritdoc/>
        public override string[] InitialSkills => initialSkills;
        /// <inheritdoc/>
        public override string[] InitialDiscoveries => initialDiscoveries;
        /// <inheritdoc/>
        public override bool RestoreVitalsOnStageComplete => restoreVitalsOnStageComplete;
        /// <inheritdoc/>
        public override bool UsesWorldNavigationMarks => worldNavigationMarks;
        /// <inheritdoc/>
        public override bool UsesExplorationFootsteps => explorationFootsteps;
        /// <inheritdoc/>
        public override string ObjectiveText => rpg && rpg.CampaignComplete ? completedText : CurrentStage?.stage.instructions ?? "";
        /// <inheritdoc/>
        public override bool ObjectiveMet => isActiveAndEnabled && rpg && !rpg.CampaignComplete &&
            active != null && index == rpg.StageIndex && active.objectives.Length > 0 &&
            active.objectives.All(o => o && o.IsRunning && o.IsComplete);
        /// <inheritdoc/>
        public override bool AllowsTrialSkill(string id) => CurrentStage?.trialSkills is string[] permitted && Array.IndexOf(permitted, id) >= 0;

        ChapterObjectiveStage CurrentStage
        {
            get
            {
                int index = ActiveStageIndex;
                return index >= 0 && stages != null && index < stages.Length ? stages[index] : null;
            }
        }

        /// <summary>订阅检查点恢复，以便读档时重置未保存的目标状态。</summary>
        protected override void OnEnable() { base.OnEnable(); if (rpg) rpg.CheckpointRestored += Restore; }
        /// <summary>解除恢复事件并清理运行中的目标。</summary>
        protected override void OnDisable() { if (rpg) rpg.CheckpointRestored -= Restore; Leave(); base.OnDisable(); }

        protected override GameObject GetStageGuidanceTarget(int stageIndex) => stages[stageIndex].guidanceTarget;
        /// <summary>恢复阶段时先结束旧目标，下一帧按 RPG 当前阶段重新接入。</summary>
        void Restore() => Leave();
        /// <summary>跟随 RPG 阶段启停目标；一帧最多提交一次过关。</summary>
        void Update()
        {
            if (!rpg || rpg.chapter != this || !rpg.CanAct) return;
            int next = rpg.CampaignComplete ? stages.Length : rpg.StageIndex;
            if (index != next)
            {
                Leave(); index = next;
                if (next < stages.Length)
                {
                    active = stages[next];
                    try
                    {
                        foreach (var objective in active.objectives) objective.BeginOwned(rpg, this);
                    }
                    catch
                    {
                        Leave();
                        enabled = false;
                        throw;
                    }
                }
                rpg.RefreshProgress();
            }
            // 一帧至多提交一个阶段；旧目标完成状态不会连带跳过下一阶段。
            if (ObjectiveMet && rpg.CompleteChapterObjective(this)) Leave();
        }
        /// <summary>清理当前目标组和阶段下标，可由禁用、恢复及过关重复调用。</summary>
        void Leave()
        {
            if (active != null) foreach (var objective in active.objectives) if (objective) objective.EndOwned(this);
            active = null; index = -1;
        }
        /// <summary>核对阶段顺序、目标唯一性、临时技能和各目标自己的接线。</summary>
        public override void ValidateChapter(LinearRpgController owner)
        {
            base.ValidateChapter(owner);
            if (stages == null || stages.Length != owner.stages.Length)
                throw new InvalidOperationException("目标章节与 RPG 阶段数量不一致");
            var used = new System.Collections.Generic.HashSet<ChapterObjective>();
            var known = owner.skills.catalog.skills.Select(s => s.skillId).ToArray();
            for (int i = 0; i < stages.Length; i++)
            {
                var row = stages[i];
                if (row == null || row.stage != owner.stages[i] || row.objectives == null || row.objectives.Length == 0 ||
                    row.trialSkills == null || row.trialSkills.Any(id => !known.Contains(id)))
                    throw new InvalidOperationException("目标章节配置无效：" + i);
                if (row.guidanceTarget && (!sceneGuidance || !sceneGuidance.targetHighlights.Any(cue => cue.target == row.guidanceTarget)))
                    throw new InvalidOperationException($"阶段 {row.stage.stageId} 的高亮目标 {row.guidanceTarget.name} 未绑定到章节场景提示");
                foreach (var objective in row.objectives)
                {
                    ChapterObjective.ValidateTree(objective, used);
                }
            }
        }
    }
}
