using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NarutoVR.SealCombat.Rpg
{
    /// <summary>一种成长物品的奖励配置；铜叶、秘籍等可使用相同结构，激活动作由交互方解释。</summary>
    [Serializable]
    public sealed class ProgressionItemDefinition
    {
        /// <summary>物品稳定 ID，用于收集和激活记录。</summary>
        public string itemId;
        /// <summary>供 Inspector 和物品界面显示的名称。</summary>
        public string displayName;
        /// <summary>收集后了解的技能，只开放知识，不授予永久施放资格。</summary>
        public string[] discoverSkillIds = Array.Empty<string>();
        /// <summary>成功激活后永久解锁的技能；默认由此物品独占授予。</summary>
        public string[] unlockSkillIds = Array.Empty<string>();
    }

    /// <summary>成长物品目录，只声明物品身份和奖励，不包含教学页面或章节校验门槛。</summary>
    [CreateAssetMenu(menuName = "NarutoVR/成长/物品奖励目录")]
    public sealed class ProgressionItemCatalog : ScriptableObject
    {
        /// <summary>当前战役支持的成长物品。</summary>
        public ProgressionItemDefinition[] items = Array.Empty<ProgressionItemDefinition>();
        /// <summary>按稳定 ID 查找物品配置。</summary>
        public ProgressionItemDefinition Find(string id) => items?.FirstOrDefault(x => x != null && x.itemId == id);
        /// <summary>检查物品身份、奖励技能和奖励独占性。</summary>
        public void Validate(IEnumerable<string> skillIds)
        {
            if (items == null) throw new InvalidOperationException("物品目录缺失");
            var known = new HashSet<string>(skillIds, StringComparer.Ordinal);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var exclusive = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in items)
                if (item == null || !RpgDataRules.ValidId(item.itemId) || !ids.Add(item.itemId) || string.IsNullOrWhiteSpace(item.displayName) ||
                    item.discoverSkillIds == null || item.unlockSkillIds == null ||
                    item.discoverSkillIds.Any(id => !known.Contains(id)) || item.discoverSkillIds.Distinct().Count() != item.discoverSkillIds.Length ||
                    item.unlockSkillIds.Any(id => !known.Contains(id) || !exclusive.Add(id)))
                    throw new InvalidOperationException("成长物品身份或奖励配置无效");
        }
    }
}
