using System;
using System.Collections.Generic;
using UnityEngine;

namespace NarutoVR.SealCombat.Rpg
{
    /// <summary>
    /// 章节与通用 RPG 的协作契约。章节负责解释目标和交互；RPG 统一负责阶段奖励与存档。
    /// 首章教学和普通目标关卡均可接入，不向调用方暴露双盘、靶或吸附区。
    /// </summary>
    public abstract class RpgChapterFlow : MonoBehaviour
    {
        [UnityEngine.Serialization.FormerlySerializedAs("interiorGuidance")]
        [InspectorName("章节场景提示（可选）")] public ChapterSceneGuidance sceneGuidance;

        /// <summary>章节所属的 RPG；完成请求必须来自该控制器绑定的章节。</summary>
        public abstract LinearRpgController Owner { get; }
        /// <summary>当前运行阶段的全部完成条件是否满足，不在此属性内发奖励或写存档。</summary>
        public abstract bool ObjectiveMet { get; }
        /// <summary>供腕匣或 HUD 展示的当前目标说明。</summary>
        public abstract string ObjectiveText { get; }
        public virtual float? AttackWarningSeconds => null;
        /// <summary>可选通关落点；未配置时使用最后阶段检查点。</summary>
        public virtual NavigationNode FinalCheckpoint => null;
        /// <summary>新角色初始了解的技能；只开放知识，不授予永久施放资格。</summary>
        public virtual string[] InitialDiscoveries => Array.Empty<string>();
        /// <summary>新角色初始化完成后的章节提示。</summary>
        public virtual string InitialStatus => "章节已就绪";
        /// <summary>阶段完成时是否恢复角色生命、度力等检查点资源。</summary>
        public virtual bool RestoreVitalsOnStageComplete => false;
        /// <summary>章节语音是否正在播放，供音乐系统决定是否压低背景音。</summary>
        public virtual bool Speaking => false;
        internal virtual QuestVisualHints TrailHints => null;
        internal virtual QuestGestureHint[] GestureHints => null;
        /// <summary>是否使用世界空间导航刻线表现。</summary>
        public virtual bool UsesWorldNavigationMarks => false;
        /// <summary>是否根据探索实际移动距离播放脚步。</summary>
        public virtual bool UsesExplorationFootsteps => false;
        /// <summary>当前章节允许展示的画符预览页数。</summary>
        public virtual int GlyphPreviewPages => 2;

        /// <summary>进入指定阶段时追加给玩家的试用技能，离开时由 RPG 自动收回。</summary>
        public virtual string[] StageSkillGrants(int index) => Array.Empty<string>();
        /// <summary>请求重听当前引导；没有语音的章节可保持为空。</summary>
        public virtual void ReplayGuidance() { }

        /// <summary>RPG 已选择阶段后通知章节重建阶段内目标；首次启动与读档都会调用。</summary>
        public virtual void OnStageEntered() { }

        protected virtual GameObject GetStageGuidanceTarget(int stageIndex) => null;

        /// <summary>已提交的有效阶段下标。当前章节未绑定、禁用或通关时返回 -1。</summary>
        protected int ActiveStageIndex
        {
            get
            {
                var owner = Owner;
                return isActiveAndEnabled && owner && owner.chapter == this && owner.Model != null && !owner.CampaignComplete
                    ? owner.StageIndex : -1;
            }
        }

        internal static Dictionary<string, T> BindStages<T>(
            LinearRpgController owner, T[] rows, Func<T, RpgStageDefinition> stageOf, string label)
        {
            var bindings = new Dictionary<string, T>(StringComparer.Ordinal);
            var allowed = new HashSet<string>(Array.ConvertAll(owner.stages, s => s.stageId));
            foreach (var row in rows ?? Array.Empty<T>())
            {
                var stage = stageOf(row);
                string id = stage ? stage.stageId : null;
                if (string.IsNullOrEmpty(id) || !allowed.Contains(id) || !bindings.TryAdd(id, row))
                    throw new InvalidOperationException($"{label}阶段缺失、重复或不属于路线：{id ?? "空"}");
            }
            foreach (string id in allowed)
                if (!bindings.ContainsKey(id)) throw new InvalidOperationException($"{label}缺少阶段：{id}");
            return bindings;
        }

        internal void RefreshStageGuidance()
        {
            var owner = Owner;
            if (!sceneGuidance || !owner || owner.chapter != this) return;
            int index = ActiveStageIndex;
            if (index < 0) sceneGuidance.Show(null);
            else sceneGuidance.Show(owner.StageAt(index).stageId, GetStageGuidanceTarget(index));
        }

        protected virtual void OnEnable()
        {
            if (Application.isPlaying && Owner && Owner.IsReady && Owner.chapter == this)
            {
                Owner.SyncStageSkills();
                RefreshStageGuidance();
            }
        }

        protected virtual void OnDisable()
        {
            if (!Application.isPlaying) return;
            if (Owner && Owner.chapter == this) Owner.SyncStageSkills();
            if (!sceneGuidance) return;
            var current = Owner ? Owner.chapter : null;
            if (!current || current == this || current.sceneGuidance != sceneGuidance) sceneGuidance.Show(null);
        }

        /// <summary>请求手动推进。默认由目标自动控制，因此拒绝外部按钮跳过目标。</summary>
        public virtual bool TryAdvanceStage(out string reason)
        {
            reason = "当前阶段由章节目标推进，不能手动跳过目标";
            return false;
        }

        /// <summary>在 RPG 初始化前校验章节专属接线；不得在这里启动旁白或访问玩家运行状态。</summary>
        public virtual void ValidateChapter(LinearRpgController owner)
        {
            if (Owner != owner) throw new InvalidOperationException("章节绑定的 RPG 与调用方不一致");
            if (sceneGuidance) sceneGuidance.ValidateConfiguration(this, owner.stages);
        }
    }
}
