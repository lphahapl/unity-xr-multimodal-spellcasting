using System;
using System.Collections.Generic;
using UnityEngine;

namespace NarutoVR.SealCombat.Rpg
{
    /// <summary>一条已解析的腕匣教学内容，可同时提供手印和口令两种施放方式。</summary>
    internal sealed class WristTeachingLesson
    {
        /// <summary>当前条目的展示标题。</summary>
        public readonly string Name;
        /// <summary>同一条目的两种可选配方，任一项可以为空。</summary>
        public readonly SkillDefinition Gesture, Voice;
        /// <summary>手印页需要展示的手印，不把备选配方拼成连续序列。</summary>
        public readonly IReadOnlyList<string> Seals;
        /// <summary>内容配置提供的施放后操作提示。</summary>
        public readonly string ActionHint;
        /// <summary>建立只读展示数据，不修改技能或玩家进度。</summary>
        public WristTeachingLesson(string name, SkillDefinition gesture, SkillDefinition voice, IReadOnlyList<string> seals, string actionHint)
        {
            Name = name; Gesture = gesture; Voice = voice; Seals = seals ?? Array.Empty<string>(); ActionHint = actionHint;
        }
        /// <summary>两种输入是否分别对应不同配方。</summary>
        public bool HasAlternative => Gesture && Voice && Gesture != Voice;
        /// <summary>仅供页面读取公共文案的首选配方，不能替代当前所选配方的权限检查。</summary>
        public SkillDefinition Reference => Gesture ? Gesture : Voice;
    }

    /// <summary>将纯教学目录与玩家技能知识组合成可见页面；不读取物品、章节类型或解锁来源。</summary>
    internal sealed class WristTeachingLibrary
    {
        readonly List<WristTeachingLesson> entries = new();
        /// <summary>当前可见的教学条目数量，不等于永久解锁技能数量。</summary>
        public int EntryCount => entries.Count;
        LinearRpgController source;
        int revision = -1;
        IReadOnlyList<SkillDefinition> recipes;
        TeachingCatalog sourceCatalog;
        bool sourceShowUndiscovered;

        /// <summary>按配置顺序解析教学内容；无独立目录时按每个技能生成一个基础条目。</summary>
        public void Sync(LinearRpgController owner)
        {
            var definitions = owner && owner.skills ? owner.skills.ConfiguredSkills : null;
            int nextRevision = owner ? owner.ProgressRevision : -1;
            var catalog = owner && owner.wristMenu ? owner.wristMenu.teachingCatalog : null;
            bool showUndiscovered = owner && owner.wristMenu && owner.wristMenu.showUndiscoveredEntries;
            if (source == owner && revision == nextRevision && ReferenceEquals(recipes, definitions) && sourceCatalog == catalog && sourceShowUndiscovered == showUndiscovered) return;
            source = owner; revision = nextRevision; recipes = definitions; entries.Clear();
            sourceCatalog = catalog; sourceShowUndiscovered = showUndiscovered;
            if (recipes == null) return;
            if (catalog)
            {
                foreach (var entry in catalog.entries)
                {
                    if (entry?.skillIds == null) continue;
                    SkillDefinition gesture = null, voice = null;
                    foreach (var id in entry.skillIds)
                    {
                        if (!showUndiscovered && !owner.IsSkillDiscovered(id)) continue;
                        foreach (var skill in recipes)
                        {
                            if (!skill || skill.skillId != id) continue;
                            if (skill.triggerMode != SkillTriggerMode.VoiceOnly && !gesture) gesture = skill;
                            if (skill.triggerMode != SkillTriggerMode.GestureOnly && !voice) voice = skill;
                            break;
                        }
                    }
                    Add(entry.displayName, gesture, voice, entry.actionHint);
                }
                return;
            }
            foreach (var skill in recipes)
            {
                if (!skill || (!showUndiscovered && !owner.IsSkillDiscovered(skill.skillId))) continue;
                Add(skill.displayName, skill.triggerMode == SkillTriggerMode.VoiceOnly ? null : skill,
                    skill.triggerMode == SkillTriggerMode.GestureOnly ? null : skill, "");
            }
        }

        /// <summary>生成一条显示数据；多种施放方式不被误当成连续手印序列。</summary>
        void Add(string name, SkillDefinition gesture, SkillDefinition voice, string actionHint)
        {
            if (!gesture && !voice) return;
            // 当前配方只匹配一个手印，备选施放方式不能被拼成连续手印序列。
            var order = gesture && !string.IsNullOrWhiteSpace(gesture.sealName) ? new[] { gesture.sealName } : Array.Empty<string>();
            entries.Add(new WristTeachingLesson(name, gesture, voice, order, actionHint));
        }

        /// <summary>统计手印或口令索引中的页面数，不改变教学可见性。</summary>
        public int PageCount(bool voiceIndex)
        {
            int count = 0;
            foreach (var entry in entries)
                count += voiceIndex ? (entry.Voice ? 1 : 0) : Math.Max(1, entry.Seals.Count);
            return count;
        }

        /// <summary>将索引页码映射到教学条目及其手印位置，越界时返回空。</summary>
        public WristTeachingLesson PageAt(int page, bool voiceIndex, out int sealIndex)
        {
            sealIndex = 0;
            if (page < 0) return null;
            foreach (var entry in entries)
            {
                int size = voiceIndex ? (entry.Voice ? 1 : 0) : Math.Max(1, entry.Seals.Count);
                if (page < size) { sealIndex = page; return entry; }
                page -= size;
            }
            return null;
        }
    }
}
