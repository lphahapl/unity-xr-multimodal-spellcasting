using System;
using System.Collections.Generic;
using System.Linq;

namespace NarutoVR.SealCombat.Rpg
{
    /// <summary>技能进度规则：了解、永久解锁与等级。没有课程、铜叶、章节或磁盘依赖。</summary>
    public static class SkillProgression
    {
        /// <summary>已了解可查看教学；永久解锁也自然包含了解资格。</summary>
        public static bool IsDiscovered(RpgProgressData p, string id) => p != null &&
            (p.discoveredSkillIds.Contains(id) || p.unlockedSkillIds.Contains(id));
        /// <summary>仅查询永久解锁，临时施放许可由章节判断。</summary>
        public static bool IsUnlocked(RpgProgressData p, string id) => p != null && p.unlockedSkillIds.Contains(id);
        /// <summary>查询技能当前升级等级，未升级为零。</summary>
        public static int Level(RpgProgressData p, string id) => p?.skillLevels.Find(x => x.skillId == id)?.level ?? 0;

        /// <summary>批量记录了解资格，重复记录成功且不追加；失败不修改列表。</summary>
        public static bool Discover(RpgProgressData p, IEnumerable<string> ids) => p != null && Add(p.discoveredSkillIds, ids);
        /// <summary>批量永久解锁，重复解锁成功且不追加；调用方负责校验当前技能目录。</summary>
        public static bool Unlock(RpgProgressData p, IEnumerable<string> ids) => p != null && Add(p.unlockedSkillIds, ids);

        /// <summary>先验证全部输入及容量，再一次性加入新标识。</summary>
        static bool Add(List<string> target, IEnumerable<string> ids)
        {
            if (target == null || ids == null) return false;
            var incoming = ids.Distinct(StringComparer.Ordinal).ToArray();
            if (incoming.Any(id => !RpgDataRules.ValidId(id))) return false;
            var additions = incoming.Where(id => !target.Contains(id)).ToArray();
            if (target.Count + additions.Length > RpgDataRules.MaximumEntries) return false;
            target.AddRange(additions); return true;
        }

        /// <summary>提升已解锁技能一级；不扣货币，不保存，也不修改其他技能。</summary>
        public static bool Upgrade(RpgProgressData p, string id, int maximumLevel)
        {
            if (!IsUnlocked(p, id) || maximumLevel < 1 || maximumLevel > RpgDataRules.MaximumSkillLevel) return false;
            var item = p.skillLevels.Find(x => x.skillId == id);
            if (item != null && (item.level < 0 || item.level >= maximumLevel)) return false;
            if (item == null)
            {
                if (p.skillLevels.Count >= RpgDataRules.MaximumEntries) return false;
                item = new RpgSkillLevelData { skillId = id }; p.skillLevels.Add(item);
            }
            item.level++; return true;
        }
    }
}
