using UnityEngine;

namespace NarutoVR.SealCombat.Enemies
{
    /// <summary>精英的召唤规则。小怪引用目录里的定义，资产加载与池化由 EnemyManager 处理。</summary>
    [CreateAssetMenu(menuName = "NarutoVR/敌人/精英召唤配置")]
    public sealed class EliteSummonConfig : ScriptableObject
    {
        [InspectorName("召唤的小怪")] public EnemyDefinition minion;
        [Min(1), InspectorName("首波数量")] public int baseCount = 3;
        [Min(1), InspectorName("同时存活上限")] public int maxAlive = 4;
        [InspectorName("允许重复召唤")] public bool allowRepeat = true;
        [Min(.1f), InspectorName("波次冷却（游戏秒）")] public float cooldown = 14;
        [Min(.1f), InspectorName("出生环半径（米）")] public float radius = 2.6f;
        [Min(.1f), InspectorName("指挥前摇（游戏秒）")] public float commandDuration = 2;
        [Min(.1f), InspectorName("指挥打断后本组瘫痪（游戏秒）")] public float paralysisDuration = 4.5f;
        [InspectorName("指挥算阵共享材质（留空隐藏）")] public Material formationMaterial;

        /// <summary>按从 1 起算的波次做自然对数衰减，并扣除仍在场的召唤物。</summary>
        public int CountForWave(int wave, int alive)
        {
            if (wave < 1 || (!allowRepeat && wave > 1)) return 0;
            int size = Mathf.Max(1, Mathf.FloorToInt(baseCount / (1f + Mathf.Log(wave))));
            return Mathf.Clamp(maxAlive - Mathf.Max(0, alive), 0, size);
        }

        internal bool Valid => minion && !minion.summoning && baseCount > 0 && maxAlive > 0 &&
            float.IsFinite(cooldown) && cooldown > 0 && float.IsFinite(radius) && radius > 0 &&
            float.IsFinite(commandDuration) && commandDuration > 0 && float.IsFinite(paralysisDuration) && paralysisDuration > 0;
    }
}
