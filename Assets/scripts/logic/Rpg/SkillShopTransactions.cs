namespace NarutoVR.SealCombat.Rpg
{
    /// <summary>技能交易规则。只在草稿上扣费与授予，提交失败时不改变正式进度。</summary>
    public static class SkillShopTransactions
    {
        /// <summary>执行一次配置商品交易，拒绝重复购买、超等级、门槛未达和余额不足。</summary>
        public static bool Apply(RpgProgressData p, SkillShopOffer offer, out string reason)
        {
            reason = "商品不存在、门槛未达或余额不足";
            if (p == null || offer == null || !RpgDataRules.ValidId(offer.skillId) || offer.cost < 0 || offer.cost > RpgDataRules.MaximumCurrency ||
                p.currency < offer.cost || (offer.requiredStage && !p.completedStageIds.Contains(offer.requiredStage.stageId))) return false;
            bool granted = offer.kind == SkillOfferKind.Unlock
                ? !SkillProgression.IsUnlocked(p, offer.skillId) && SkillProgression.Unlock(p, new[] { offer.skillId })
                : offer.kind == SkillOfferKind.Upgrade && SkillProgression.Upgrade(p, offer.skillId, offer.maximumLevel);
            if (!granted) { reason = "已经解锁，或技能未解锁／已达到等级上限"; return false; }
            p.currency -= offer.cost; reason = null; return true;
        }
    }

}
