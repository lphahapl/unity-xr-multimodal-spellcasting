namespace NarutoVR.SealCombat.Rpg
{
    /// <summary>一次性奖励结算：按奖励 ID 去重，同时检查货币上限与技能容量，不负责保存。</summary>
    public static class RpgRewards
    {
        /// <summary>在进度草稿上原子授予奖励；任何校验失败都不改变该草稿。</summary>
        public static bool Grant(RpgProgressData p, string rewardId, int currency, string[] skillIds)
        {
            if (p == null || !RpgDataRules.ValidId(rewardId) || currency < 0 || currency > RpgDataRules.MaximumCurrency ||
                p.currency < 0 || p.currency > RpgDataRules.MaximumCurrency - currency ||
                p.claimedRewardIds.Contains(rewardId) || p.claimedRewardIds.Count >= RpgDataRules.MaximumEntries) return false;
            if (!SkillProgression.Unlock(p, skillIds)) return false;
            p.claimedRewardIds.Add(rewardId); p.currency += currency; return true;
        }
    }
}
