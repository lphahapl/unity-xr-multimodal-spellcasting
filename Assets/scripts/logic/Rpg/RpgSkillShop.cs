using System;
using System.Linq;
using UnityEngine;

namespace NarutoVR.SealCombat.Rpg
{
    /// <summary>可选商店模块。将商品交易交给规则计算，再由会话统一保存。</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(LinearRpgController))]
    public sealed class RpgSkillShop : MonoBehaviour, IRpgProgressConstraint
    {
        /// <summary>当前场景可购买的商品；不需要商店的场景不挂本组件。</summary>
        public SkillShopCatalog catalog;
        /// <summary>最近交易的成功或拒绝说明。</summary>
        public string Status { get; private set; } = "商店就绪";
        /// <summary>同对象上的 RPG 会话。</summary>
        public LinearRpgController Owner => GetComponent<LinearRpgController>();

        /// <summary>购买指定商品；保存待重试也算本轮交易已生效，不能据此再次扣款。</summary>
        public bool TryPurchase(string offerId)
        {
            if (!isActiveAndEnabled || !Owner.CanAct || !catalog) { Status = "商店当前不可用"; return false; }
            var draft = Owner.CreateProgressDraft();
            if (!SkillShopTransactions.Apply(draft, catalog.Find(offerId), out string reason)) { Status = reason; return false; }
            var result = Owner.CommitProgressChange(draft);
            Status = result == ProgressCommitResult.Rejected ? Owner.Status : result == ProgressCommitResult.PendingSave ? "交易已生效，等待存档" : "交易已保存";
            return result != ProgressCommitResult.Rejected;
        }
        /// <inheritdoc/>
        public void ValidateConfiguration(LinearRpgController owner)
        {
            if (Owner != owner || !catalog) throw new InvalidOperationException("商店缺少目录或所属控制器错误");
            catalog.Validate(owner.skills.catalog.skills, owner.stages);
            var inventory = GetComponent<RpgItemInventory>();
            if (inventory && inventory.catalog)
            {
                var exclusive = inventory.catalog.items.SelectMany(i => i.unlockSkillIds).ToArray();
                if (catalog.offers.Any(o => o.kind == SkillOfferKind.Unlock && exclusive.Contains(o.skillId)))
                    throw new InvalidOperationException("商店不能出售由成长物品独占授予的技能");
            }
        }
        /// <summary>商店没有独立持久状态；余额和等级交给存档层校验，解锁来源交给物品模块校验。</summary>
        public bool ValidateProgress(RpgProgressData progress) => true;
    }
}
