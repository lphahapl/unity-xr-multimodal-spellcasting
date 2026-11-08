using UnityEngine;

namespace NarutoVR.SealCombat.Rpg
{
    /// <summary>新玩家配置入口；沿用旧玩家定义字段，旧资产和引用无需迁移。</summary>
    [CreateAssetMenu(menuName = "NarutoVR/线性RPG/玩家配置 PlayerSO", fileName = "玩家配置")]
    public sealed class PlayerSO : RpgPlayerDefinition { }
}
