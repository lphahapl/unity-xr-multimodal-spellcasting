using System;
using System.Collections.Generic;
using UnityEngine;

namespace NarutoVR.SealCombat.Rpg
{
    /// <summary>一条教学内容的展示配置。同一术式的手印与口令配方可合并展示，不参与解锁。</summary>
    [Serializable]
    public sealed class TeachingEntry
    {
        /// <summary>教学条目的稳定标识，与技能或物品 ID 分开。</summary>
        public string entryId;
        /// <summary>教学页面标题。</summary>
        public string displayName;
        /// <summary>本条目介绍的技能配方；顺序决定首选手印和口令展示。</summary>
        public string[] skillIds = Array.Empty<string>();
        /// <summary>施放后的操作提示，由内容配置，不让 UI 猜测效果类型。</summary>
        [TextArea] public string actionHint;
    }

    /// <summary>纯教学目录：只描述条目、配方分组和文案，不授予技能，也不读取章节进度。</summary>
    [CreateAssetMenu(menuName = "NarutoVR/教学/教学目录")]
    public sealed class TeachingCatalog : ScriptableObject
    {
        /// <summary>腕匣中的展示顺序；是否可见由玩家已了解的技能决定。</summary>
        public TeachingEntry[] entries = Array.Empty<TeachingEntry>();

        /// <summary>检查内容标识和技能引用，避免重复分组或缺失配方。</summary>
        public void Validate(IEnumerable<string> skills)
        {
            var known = new HashSet<string>(skills, StringComparer.Ordinal);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var grouped = new HashSet<string>(StringComparer.Ordinal);
            if (entries == null) throw new InvalidOperationException("教学目录条目为空");
            foreach (var entry in entries)
            {
                if (entry == null || !RpgDataRules.ValidId(entry.entryId) || !ids.Add(entry.entryId) ||
                    string.IsNullOrWhiteSpace(entry.displayName) || entry.skillIds == null || entry.skillIds.Length == 0)
                    throw new InvalidOperationException("教学条目身份、名称或配方列表无效");
                foreach (var id in entry.skillIds)
                    if (!known.Contains(id) || !grouped.Add(id)) throw new InvalidOperationException("教学目录包含未知或重复配方：" + id);
            }
        }
    }
}
