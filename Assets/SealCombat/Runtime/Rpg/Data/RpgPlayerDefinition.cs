using UnityEngine;

namespace NarutoVR.SealCombat.Rpg
{
    [CreateAssetMenu(menuName = "NarutoVR/线性RPG/玩家定义")]
    public sealed class RpgPlayerDefinition : ScriptableObject
    {
        [Header("玩家基础属性")]
        [InspectorName("生命上限"), Tooltip("运行态生命上限；不从存档读取上限。"), Range(1, 10000)]
        public float maxHealth = 100;
        [InspectorName("能量上限"), Tooltip("施法消耗的能量上限。"), Range(1, 10000)]
        public float maxMana = 100;
        [InspectorName("每秒恢复能量"), Tooltip("存活且恢复等待结束后，每个游戏秒恢复的能量。"), Range(0, 10000)]
        public float manaRegen = 5;
        [InspectorName("恢复等待秒数"), Tooltip("成功消耗能量后等待多久开始恢复；暂停不推进游戏时钟。"), Range(0, 86400)]
        public float regenDelay = 2;

        public RpgPlayerState CreateState() => new RpgPlayerState(maxHealth, maxMana, manaRegen, regenDelay);
    }
}
