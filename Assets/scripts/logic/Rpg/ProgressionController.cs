using System;

namespace NarutoVR.SealCombat.Rpg
{
    /// <summary>当前玩家的成长命令。每条命令只提交一次完整变更；保存待重试也代表业务已经生效。</summary>
    public sealed class ProgressionController
    {
        internal delegate ProgressCommitResult CommitGrowth(Func<PlayerModel, bool> change, Func<PlayerModel, bool> alreadyApplied = null);
        readonly CommitGrowth commit;

        internal ProgressionController(CommitGrowth commit) => this.commit = commit;

        public ProgressCommitResult CreditCurrency(int amount) => commit(candidate => candidate.Credit(amount));
        public ProgressCommitResult GrantSkill(string id) => commit(candidate => candidate.Unlock(new[] { id }));

        internal ProgressCommitResult Purchase(SkillShopOffer offer, out string reason)
        {
            if (offer == null) { reason = "商品不存在"; return ProgressCommitResult.Rejected; }
            string resultReason = null;
            string requiredStage = offer.requiredStage ? offer.requiredStage.stageId : null;
            var result = commit(candidate =>
                candidate.Purchase(offer.skillId, offer.kind, offer.cost, offer.maximumLevel, requiredStage, out resultReason));
            reason = result == ProgressCommitResult.Rejected ? resultReason ?? "当前无法提交交易" : null;
            return result;
        }

        internal ProgressCommitResult ChangeItem(ProgressionItemDefinition definition, bool activate)
        {
            bool AlreadyApplied(PlayerModel player)
            {
                var state = player.ItemState(definition.itemId);
                return state == ProgressionItemState.Activated || (!activate && state == ProgressionItemState.Collected);
            }
            return commit(candidate => candidate.ChangeItem(definition.itemId, definition.discoverSkillIds,
                definition.unlockSkillIds, activate), AlreadyApplied);
        }
    }
}
