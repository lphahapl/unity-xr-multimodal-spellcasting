using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NarutoVR.SealCombat.Rpg
{
    /// <summary>商品行为：解锁技能，或提升已解锁技能的等级。</summary>
    public enum SkillOfferKind { Unlock, Upgrade }

    /// <summary>一个商品的固定规则；不包含玩家余额或购买次数。</summary>
    [Serializable]
    public sealed class SkillShopOffer
    {
        /// <summary>商品稳定 ID，按钮通过此 ID 请求购买。</summary>
        public string offerId;
        /// <summary>商品的显示名称。</summary>
        public string displayName;
        /// <summary>商品作用的技能 ID，不在代码中写死具体术式。</summary>
        public string skillId;
        /// <summary>解锁或升级。</summary>
        public SkillOfferKind kind;
        /// <summary>每次交易的固定价格。</summary>
        [Min(0)] public int cost;
        /// <summary>升级商品允许达到的最高等级。</summary>
        [Range(1, 10)] public int maximumLevel = 3;
        /// <summary>可选购买门槛；为空则不要求完成某个阶段。</summary>
        public RpgStageDefinition requiredStage;
    }

    /// <summary>商店商品目录，商品可独立于章节和教学内容配置。</summary>
    [CreateAssetMenu(menuName = "NarutoVR/成长/技能商店目录")]
    public sealed class SkillShopCatalog : ScriptableObject
    {
        /// <summary>按界面展示顺序排列的商品。</summary>
        public SkillShopOffer[] offers = Array.Empty<SkillShopOffer>();
        /// <summary>查找商品，未知 ID 不生成默认商品。</summary>
        public SkillShopOffer Find(string id) => offers?.FirstOrDefault(x => x != null && x.offerId == id);
        /// <summary>校验商品、技能引用、升级能力和可选阶段门槛。</summary>
        public void Validate(IEnumerable<SkillDefinition> skills, RpgStageDefinition[] stages)
        {
            var known = skills.ToDictionary(s => s.skillId, StringComparer.Ordinal);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            if (offers == null) throw new InvalidOperationException("商店商品列表缺失");
            foreach (var offer in offers)
            {
                if (offer == null || !RpgDataRules.ValidId(offer.offerId) || !ids.Add(offer.offerId) || string.IsNullOrWhiteSpace(offer.displayName) ||
                    !known.TryGetValue(offer.skillId ?? "", out var skill) || !Enum.IsDefined(typeof(SkillOfferKind), offer.kind) ||
                    offer.cost < 0 || offer.cost > RpgDataRules.MaximumCurrency || offer.maximumLevel < 1 || offer.maximumLevel > RpgDataRules.MaximumSkillLevel ||
                    (offer.requiredStage && !stages.Contains(offer.requiredStage))) throw new InvalidOperationException("技能商品配置无效");
                if (offer.kind == SkillOfferKind.Upgrade && (!skill.effectDefinition || !skill.effectDefinition.SupportsLevels))
                    throw new InvalidOperationException("商品对应效果未实现等级规则：" + offer.skillId);
            }
        }
    }
}
