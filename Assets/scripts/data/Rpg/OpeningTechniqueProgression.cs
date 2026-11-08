using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NarutoVR.SealCombat.Rpg
{
    public enum TechniqueLearningState { Locked, Basic, LeafCollected, LeafRegistered, Learned }

    [Serializable]
    public sealed class TechniqueCourse
    {
        [InspectorName("课程标识")] public string courseId;
        [InspectorName("教学页名称")] public string displayName;
        [InspectorName("本课真实技能配方")] public string[] skillIds = Array.Empty<string>();
    }

    [Serializable]
    public sealed class CopperLeafLesson
    {
        [InspectorName("实物稳定标识")] public string leafId;
        [InspectorName("铜叶名称")] public string displayName;
        [InspectorName("取得完成阶段（兼容旧档）")] public RpgStageDefinition collectedStage;
        [InspectorName("入匣完成阶段（兼容旧档）")] public RpgStageDefinition seatedStage;
        [InspectorName("拾取后开放的课")] public TechniqueCourse course;
    }

    [CreateAssetMenu(menuName = "NarutoVR/线性RPG/铜叶成长配置")]
    public sealed class OpeningTechniqueProgression : ScriptableObject
    {
        [InspectorName("初始教学课")] public TechniqueCourse[] basicCourses = Array.Empty<TechniqueCourse>();
        [InspectorName("铜叶与后续教学")] public CopperLeafLesson[] leaves = Array.Empty<CopperLeafLesson>();
        [InspectorName("允许使用永久术式的校验阶段")] public RpgStageDefinition calibrationStage;
        public IEnumerable<TechniqueCourse> OrderedCourses => basicCourses.Concat(leaves.Select(leaf => leaf.course));

        public TechniqueLearningState State(RpgProgressData progress, string skillId)
        {
            if (progress == null) return TechniqueLearningState.Locked;
            if (basicCourses.Any(c => Array.IndexOf(c.skillIds, skillId) >= 0)) return TechniqueLearningState.Basic;
            var leaf = leaves.FirstOrDefault(l => Array.IndexOf(l.course.skillIds, skillId) >= 0);
            var acquired = leaf == null ? null : progress.copperLeaves.Find(l => l.leafId == leaf.leafId);
            if (acquired != null) return acquired.registered ? TechniqueLearningState.LeafRegistered : TechniqueLearningState.LeafCollected;
            // Retain a permanent skill purchased in an older campaign.
            return progress.unlockedSkillIds.Contains(skillId) ? TechniqueLearningState.Learned : TechniqueLearningState.Locked;
        }

        public bool Available(RpgProgressData progress, string skillId, bool stageAllowsTrial)
        {
            if (progress == null) return false;
            if (stageAllowsTrial && State(progress, skillId) == TechniqueLearningState.Basic) return true;
            return progress.unlockedSkillIds.Contains(skillId) &&
                (!calibrationStage || progress.completedStageIds.Contains(calibrationStage.stageId));
        }

        // A repeated signal is a successful no-op so restored, partially played narration can finish.
        public bool Record(RpgProgressData progress, string leafId, bool register, out bool changed)
        {
            changed = false;
            var lesson = Array.Find(leaves, l => l.leafId == leafId);
            if (progress == null || lesson == null) return false;
            var acquired = progress.copperLeaves.Find(l => l.leafId == leafId);
            if (!register)
            {
                if (acquired != null) return true;
                if (progress.copperLeaves.Count >= RpgDataRules.MaximumEntries) return false;
                progress.copperLeaves.Add(new RpgCopperLeafData { leafId = leafId });
                changed = true;
                return true;
            }
            if (acquired == null) return false;
            if (acquired.registered) return true;
            var additions = lesson.course.skillIds.Where(id => !progress.unlockedSkillIds.Contains(id)).ToArray();
            if (progress.unlockedSkillIds.Count + additions.Length > RpgDataRules.MaximumEntries) return false;
            progress.unlockedSkillIds.AddRange(additions);
            acquired.registered = true;
            changed = true;
            return true;
        }

        public bool Restore(RpgProgressData progress)
        {
            foreach (var acquired in progress.copperLeaves)
            {
                var lesson = Array.Find(leaves, l => l.leafId == acquired.leafId);
                if (lesson == null || (acquired.registered && lesson.course.skillIds.Any(id => !progress.unlockedSkillIds.Contains(id)))) return false;
            }
            // v1 saves from before leaf ownership was persisted only recorded completed stage IDs.
            foreach (var l in leaves)
            {
                bool seated = l.seatedStage && progress.completedStageIds.Contains(l.seatedStage.stageId);
                bool taken = l.collectedStage && progress.completedStageIds.Contains(l.collectedStage.stageId);
                if ((taken || seated) && !Record(progress, l.leafId, false, out _)) return false;
                if (seated && !Record(progress, l.leafId, true, out _)) return false;
            }
            return true;
        }

        public bool IncludesLeafSkill(string skillId) => leaves.Any(l => Array.IndexOf(l.course.skillIds, skillId) >= 0);

        public void ValidateSetup(IEnumerable<string> catalog, RpgStageDefinition[] stages, string firstLeafId)
        {
            var known = new HashSet<string>(catalog, StringComparer.Ordinal);
            var courseIds = new HashSet<string>(StringComparer.Ordinal);
            var skillIds = new HashSet<string>(StringComparer.Ordinal);
            var leafIds = new HashSet<string>(StringComparer.Ordinal);
            if (basicCourses == null || leaves == null || !calibrationStage || !stages.Contains(calibrationStage))
                throw new InvalidOperationException("铜叶课程或校验阶段未配置");
            foreach (var l in leaves)
                if (l == null || !RpgDataRules.ValidId(l.leafId) || !leafIds.Add(l.leafId) ||
                    (l.collectedStage && !stages.Contains(l.collectedStage)) || (l.seatedStage && !stages.Contains(l.seatedStage)))
                    throw new InvalidOperationException("铜叶标识重复或关联阶段不属于当前关卡");
            if (!leafIds.Contains(firstLeafId)) throw new InvalidOperationException("首枚实物铜叶没有对应课程");
            foreach (var c in OrderedCourses)
                if (c == null || string.IsNullOrWhiteSpace(c.displayName) || !RpgDataRules.ValidId(c.courseId) || !courseIds.Add(c.courseId) ||
                    c.skillIds == null || c.skillIds.Length == 0 || c.skillIds.Any(id => !known.Contains(id) || !skillIds.Add(id)))
                    throw new InvalidOperationException("课程身份、技能配方或分组无效");
            if (stages.Any(stage => stage && stage.unlockSkillIds != null && stage.unlockSkillIds.Any(IncludesLeafSkill)))
                throw new InvalidOperationException("铜叶配方的永久登记必须来自实物入匣，阶段奖励不能重复发放");
        }
    }
}
