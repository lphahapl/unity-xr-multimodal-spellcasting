using System.Collections.Generic;
using System.Linq;

namespace NarutoVR.SealCombat.Rpg
{
    /// <summary>将玩家领域状态映射到固定 v2 格式。返回的集合均独立于玩家。</summary>
    public static class RpgSaveMapper
    {
        internal static RpgProgressData Export(this PlayerModel model, RpgCheckpointData checkpoint, string checkpointId)
        {
            return new RpgProgressData
            {
                currency = model.Currency,
                discoveredSkillIds = new List<string>(model.DiscoveredSkills),
                unlockedSkillIds = new List<string>(model.UnlockedSkills),
                skillLevels = model.SkillLevels.Select(pair => new RpgSkillLevelData { skillId = pair.Key, level = pair.Value }).ToList(),
                items = model.Inventory.Select(pair => new RpgItemProgressData { itemId = pair.Key, activated = pair.Value }).ToList(),
                claimedRewardIds = new List<string>(model.ClaimedRewards),
                completedStageIds = new List<string>(model.Chapter.CompletedStages),
                checkpoint = checkpoint,
                checkpointId = checkpointId
            };
        }

        internal static void RestoreGrowth(this PlayerModel model, RpgProgressData data)
        {
            model.LoadGrowth(data.currency, data.discoveredSkillIds, data.unlockedSkillIds,
                data.skillLevels.Select(level => new KeyValuePair<string, int>(level.skillId, level.level)),
                data.items.Select(item => new KeyValuePair<string, bool>(item.itemId, item.activated)),
                data.claimedRewardIds, data.completedStageIds);
        }

        public static RpgCheckpointData CaptureCheckpoint(this RpgPlayerState player, double now)
        {
            var state = player.CaptureVitals(now);
            return new RpgCheckpointData
            {
                health = state.Health,
                mana = state.Mana,
                manaRegenDelayRemaining = state.RegenDelayRemaining,
                cooldowns = state.Cooldowns.Select(pair => new RpgCooldownData { group = pair.Key, remainingSeconds = pair.Value }).ToList()
            };
        }

        public static bool RestoreCheckpoint(this RpgPlayerState player, RpgCheckpointData data, double now, out string reason)
        {
            if (TryReadVitals(data, out var state)) return player.RestoreVitals(state, now, out reason);
            reason = "检查点生命、能量或冷却无效";
            return false;
        }

        internal static bool ValidateCheckpoint(RpgCheckpointData data, float maxHealth, float maxMana, out string reason)
        {
            bool valid = TryReadVitals(data, out var state) && state.IsValid(maxHealth, maxMana);
            reason = valid ? null : "检查点生命、能量或冷却无效";
            return valid;
        }

        static bool TryReadVitals(RpgCheckpointData data, out PlayerVitalsSnapshot state)
        {
            state = default;
            if (data?.cooldowns == null || data.cooldowns.Count > RpgDataRules.MaximumEntries) return false;
            var cooldowns = new List<KeyValuePair<string, float>>(data.cooldowns.Count);
            foreach (var group in data.cooldowns)
            {
                if (group == null) return false;
                cooldowns.Add(new KeyValuePair<string, float>(group.group, group.remainingSeconds));
            }
            state = new PlayerVitalsSnapshot(data.health, data.mana, data.manaRegenDelayRemaining, cooldowns);
            return true;
        }
    }
}
