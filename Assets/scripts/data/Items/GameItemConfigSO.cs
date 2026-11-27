using UnityEngine;

namespace NarutoVR.SealCombat.Items
{
    public enum GameItemKind
    {
        [InspectorName("通用")] Generic = 0,
        [InspectorName("墨规笔")] GlyphPen = 1,
        [InspectorName("行旅麦枣干粮包")] TravelRation = 2,
        [InspectorName("醒神松墨锭")] PineInk = 3,
        [InspectorName("冷铸青铜拓片")] BronzeRubbing = 4,
        [InspectorName("定律铜橛")] CooldownStake = 5
    }

    [CreateAssetMenu(menuName = "考工律度/道具/道具配置")]
    public class GameItemConfigSO : ScriptableObject
    {
        internal const float DefaultHeldScaleMultiplier = 1.4f;
        public GameItemKind kind;
        [Min(1f), InspectorName("取用尺寸倍率"), Tooltip("相对收纳尺寸。持有和落地均使用此倍率，归槽后恢复。")]
        public float heldScaleMultiplier = DefaultHeldScaleMultiplier;
    }
}
