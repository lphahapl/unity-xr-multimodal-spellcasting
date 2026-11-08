using UnityEngine;

namespace NarutoVR.SealCombat.Enemies
{
    /// <summary>共享静态配置；每只敌人拥有独立运行状态，绝不在资产上扣血或削韧。</summary>
    [CreateAssetMenu(menuName = "NarutoVR/敌人/生命与韧性配置")]
    public sealed class EnemyCombatDefinition : ScriptableObject
    {
        [Header("生命与韧性")]
        [InspectorName("最大生命"), Min(1)] public float maxHealth = 150;
        [InspectorName("最大韧性"), Min(1)] public float maxPoise = 100;
        [Header("自然恢复（从最后一次有效削韧开始等待）")]
        [InspectorName("恢复等待秒数"), Min(0)] public float recoveryDelay = 4;
        [InspectorName("每秒恢复韧性（0 为不自然恢复）"), Min(0)] public float recoveryPerSecond = 15;
        [Header("瘫痪恢复")]
        [InspectorName("Seated 动画结束后恢复比例"), Range(.01f, 1)] public float recoveryAfterSeated = 1;
        public bool Valid => float.IsFinite(maxHealth) && maxHealth >= 1 && float.IsFinite(maxPoise) && maxPoise >= 1 &&
            float.IsFinite(recoveryDelay) && recoveryDelay >= 0 && float.IsFinite(recoveryPerSecond) && recoveryPerSecond >= 0 &&
            float.IsFinite(recoveryAfterSeated) && recoveryAfterSeated > 0 && recoveryAfterSeated <= 1;
    }
}
