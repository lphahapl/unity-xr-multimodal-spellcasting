using System;
using System.Collections.Generic;

namespace NarutoVR.SealCombat.Rpg
{
    [Serializable] public sealed class RpgCooldownData
    {
        public string group;
        public float remainingSeconds;
    }

    [Serializable] public sealed class RpgCheckpointData
    {
        public float health = 100;
        public float mana = 100;
        public float manaRegenDelayRemaining;
        public List<RpgCooldownData> cooldowns = new List<RpgCooldownData>();
    }

    [Serializable] public sealed class RpgSkillLevelData
    {
        public string skillId;
        public int level;
    }

    [Serializable] public sealed class SaveEnvelope
    {
        public int version = RpgSaveStore.CurrentVersion;
        public string profileId;
        public RpgProgressData progress;
    }

    /// <summary>Serializable value DTO. Loading must pass RpgSaveStore's catalogue validation before use.</summary>
    [Serializable] public sealed class RpgProgressData
    {
        public List<string> unlockedSkillIds = new List<string>();
        public List<RpgSkillLevelData> skillLevels = new List<RpgSkillLevelData>();
        public int currency;
        public List<string> completedStageIds = new List<string>();
        public List<string> claimedRewardIds = new List<string>();
        public string checkpointId;
        public RpgCheckpointData checkpoint = new RpgCheckpointData();

        public bool TryGrantReward(string rewardId, int currency, string skillId)
        {
            if (!RpgDataRules.ValidId(rewardId) || currency < 0 || currency > RpgDataRules.MaximumCurrency ||
                this.currency < 0 || this.currency > RpgDataRules.MaximumCurrency - currency ||
                (!string.IsNullOrEmpty(skillId) && !RpgDataRules.ValidId(skillId)) ||
                claimedRewardIds == null || unlockedSkillIds == null || claimedRewardIds.Contains(rewardId) ||
                claimedRewardIds.Count >= RpgDataRules.MaximumEntries ||
                (!string.IsNullOrEmpty(skillId) && !unlockedSkillIds.Contains(skillId) && unlockedSkillIds.Count >= RpgDataRules.MaximumEntries)) return false;
            claimedRewardIds.Add(rewardId);
            this.currency += currency;
            if (!string.IsNullOrEmpty(skillId) && !unlockedSkillIds.Contains(skillId)) unlockedSkillIds.Add(skillId);
            return true;
        }

        public bool TryPurchaseSkill(string skillId, int fixedCost)
        {
            if (!RpgDataRules.ValidId(skillId) || fixedCost < 0 || fixedCost > RpgDataRules.MaximumCurrency ||
                currency < fixedCost || unlockedSkillIds == null || unlockedSkillIds.Contains(skillId) ||
                unlockedSkillIds.Count >= RpgDataRules.MaximumEntries) return false;
            currency -= fixedCost; unlockedSkillIds.Add(skillId); return true;
        }

        public bool TryUpgradeSkill(string skillId, int fixedCost, int maximumLevel = 3)
        {
            if (!RpgDataRules.ValidId(skillId) || fixedCost < 0 || fixedCost > RpgDataRules.MaximumCurrency || currency < fixedCost ||
                maximumLevel < 1 || maximumLevel > RpgDataRules.MaximumSkillLevel || unlockedSkillIds == null ||
                !unlockedSkillIds.Contains(skillId) || skillLevels == null) return false;
            var item = skillLevels.Find(x => x != null && x.skillId == skillId);
            if (item != null && (item.level < 0 || item.level >= maximumLevel)) return false;
            if (item == null)
            {
                if (skillLevels.Count >= RpgDataRules.MaximumEntries) return false;
                item = new RpgSkillLevelData { skillId = skillId }; skillLevels.Add(item);
            }
            item.level++; currency -= fixedCost; return true;
        }
    }

    internal static class RpgDataRules
    {
        internal const float MaximumStat = 10000, MaximumDuration = 86400;
        internal const int MaximumEntries = 2048, MaximumCurrency = 1000000, MaximumSkillLevel = 10;
        internal static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        internal static bool InRange(float value, float min, float max) => Finite(value) && value >= min && value <= max;
        internal static bool ValidId(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || id.Length > 128 || id != id.Trim()) return false;
            foreach (char c in id) if (char.IsControl(c)) return false;
            return true;
        }
        internal static bool ValidateCheckpoint(RpgCheckpointData data, float maxHealth, float maxMana, out string reason)
        {
            reason = "检查点生命、能量或冷却无效";
            if (data == null || !InRange(data.health, 0, maxHealth) || !InRange(data.mana, 0, maxMana) ||
                !InRange(data.manaRegenDelayRemaining, 0, MaximumDuration) || data.cooldowns == null || data.cooldowns.Count > MaximumEntries) return false;
            var groups = new HashSet<string>(StringComparer.Ordinal);
            foreach (var cd in data.cooldowns)
                if (cd == null || !ValidId(cd.group) || !groups.Add(cd.group) || !InRange(cd.remainingSeconds, 0, MaximumDuration)) return false;
            reason = null; return true;
        }
    }
}
