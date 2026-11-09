using System;
using System.Linq;
using UnityEngine;

namespace NarutoVR.SealCombat.Rpg
{
    /// <summary>物品交互方可查询的只读状态，不暴露可变存档记录。</summary>
    public enum ProgressionItemState { Missing, Collected, Activated }
    /// <summary>成长物品运行逻辑：收集开放知识，激活授予技能；使用 RPG 草稿提交保证失败不半改进度。</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(LinearRpgController))]
    public sealed class RpgItemInventory : MonoBehaviour, IRpgProgressConstraint
    {
        /// <summary>本战役物品奖励目录。</summary>
        public ProgressionItemCatalog catalog;
        /// <summary>同对象上的会话控制器，负责保存与进度广播。</summary>
        public LinearRpgController Owner => GetComponent<LinearRpgController>();
        /// <summary>查询实际物品状态；没有记录即未收集，不根据阶段猜测。</summary>
        public ProgressionItemState GetState(string id)
        {
            var record = Owner.Progress?.items.Find(x => x.itemId == id);
            return record == null ? ProgressionItemState.Missing : record.activated ? ProgressionItemState.Activated : ProgressionItemState.Collected;
        }

        /// <summary>收集真实物品，重复调用成功但不重复奖励。</summary>
        public ProgressCommitResult Collect(string id) => Change(id, false);
        /// <summary>激活已收集物品；铜叶入匣、秘籍研读等具体行为由调用方确认。</summary>
        public ProgressCommitResult Activate(string id) => Change(id, true);

        /// <summary>只操作草稿，完整校验及写盘由会话负责。</summary>
        ProgressCommitResult Change(string id, bool activate)
        {
            var owner = Owner;
            if (!isActiveAndEnabled || !owner.CanAct || !catalog) return ProgressCommitResult.Rejected;
            var definition = catalog.Find(id);
            if (definition == null) return ProgressCommitResult.Rejected;
            var existing = owner.Progress.items.Find(x => x.itemId == id);
            if (activate && existing == null) return ProgressCommitResult.Rejected;
            if (existing != null && (!activate || existing.activated))
                return owner.CheckpointSavePending ? ProgressCommitResult.PendingSave : ProgressCommitResult.Saved;
            var draft = owner.CreateProgressDraft();
            var item = draft.items.Find(x => x.itemId == id);
            if (item == null)
            {
                if (draft.items.Count >= RpgDataRules.MaximumEntries) return ProgressCommitResult.Rejected;
                item = new RpgItemProgressData { itemId = id }; draft.items.Add(item);
            }
            if (!SkillProgression.Discover(draft, definition.discoverSkillIds)) return ProgressCommitResult.Rejected;
            if (activate)
            {
                if (!SkillProgression.Unlock(draft, definition.unlockSkillIds)) return ProgressCommitResult.Rejected;
                item.activated = true;
            }
            return owner.CommitProgressChange(draft);
        }

        /// <inheritdoc/>
        public void ValidateConfiguration(LinearRpgController owner)
        {
            if (Owner != owner || !catalog) throw new InvalidOperationException("物品模块未绑定目录或所属控制器错误");
            catalog.Validate(owner.skills.catalog.skills.Select(s => s.skillId));
            var exclusive = catalog.items.SelectMany(x => x.unlockSkillIds).ToArray();
            if (owner.chapter.InitialSkills.Any(exclusive.Contains) || owner.stages.Any(s => s.unlockSkillIds.Any(exclusive.Contains)))
                throw new InvalidOperationException("物品独占解锁不能同时作为初始或阶段奖励");
        }
        /// <inheritdoc/>
        public bool ValidateProgress(RpgProgressData p)
        {
            foreach (var item in p.items)
            {
                var definition = catalog.Find(item.itemId);
                if (definition == null || definition.discoverSkillIds.Any(id => !SkillProgression.IsDiscovered(p, id)) ||
                    (item.activated && definition.unlockSkillIds.Any(id => !SkillProgression.IsUnlocked(p, id)))) return false;
            }
            // 双向约束：不能仅写一个技能 ID 绕过其物品激活来源。
            foreach (var definition in catalog.items)
                if (definition.unlockSkillIds.Any(id => SkillProgression.IsUnlocked(p, id)) &&
                    !p.items.Any(x => x.itemId == definition.itemId && x.activated)) return false;
            return true;
        }
    }
}
