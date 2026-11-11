namespace NarutoVR.SealCombat.Rpg
{
    /// <summary>当前玩家的成长命令。每条命令只提交一次完整变更；保存待重试也代表业务已经生效。</summary>
    public sealed class ProgressionController
    {
        readonly LinearRpgController session;
        internal ProgressionController(LinearRpgController session) => this.session = session;

        public ProgressCommitResult CreditCurrency(int amount) => session.ApplyGrowthChange(candidate => candidate.Credit(amount));
        public ProgressCommitResult GrantSkill(string id) => session.ApplyGrowthChange(candidate => candidate.Unlock(new[] { id }));

        internal ProgressCommitResult Purchase(SkillShopOffer offer, out string reason)
        {
            if (offer == null) { reason = "商品不存在"; return ProgressCommitResult.Rejected; }
            string resultReason = null;
            string requiredStage = offer.requiredStage ? offer.requiredStage.stageId : null;
            var result = session.ApplyGrowthChange(candidate =>
                candidate.Purchase(offer.skillId, offer.kind, offer.cost, offer.maximumLevel, requiredStage, out resultReason));
            reason = resultReason ?? session.Status;
            return result;
        }

        internal ProgressCommitResult ChangeItem(ProgressionItemDefinition definition, bool activate)
        {
            var state = session.Model.ItemState(definition.itemId);
            if (activate && state == ProgressionItemState.Missing) return ProgressCommitResult.Rejected;
            if (state == ProgressionItemState.Activated || (!activate && state == ProgressionItemState.Collected))
                return session.CheckpointSavePending ? ProgressCommitResult.PendingSave : ProgressCommitResult.Saved;
            return session.ApplyGrowthChange(candidate =>
                candidate.ChangeItem(definition.itemId, definition.discoverSkillIds, definition.unlockSkillIds, activate));
        }
    }
}
