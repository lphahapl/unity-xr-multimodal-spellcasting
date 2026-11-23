using UnityEngine;

namespace NarutoVR.SealCombat
{
    /// <summary>一种表面痕迹的投影外观、接收层与寿命。尺寸以米为单位，Y 是剑痕长轴。</summary>
    [CreateAssetMenu(menuName = "NarutoVR/技能效果/场景痕迹样式")]
    public sealed class SurfaceMarkStyle : ScriptableObject
    {
        [InspectorName("URP 贴花材质")] public Material material;
        [InspectorName("宽、长、投射深度（米）")] public Vector3 size = new(.3f, .3f, .04f);
        [InspectorName("允许留下痕迹的碰撞层")] public LayerMask receivers = ~0;
        [InspectorName("停留秒数"), Min(0)] public float holdSeconds = 4;
        [InspectorName("淡出秒数"), Min(0)] public float fadeSeconds = 2;
        [InspectorName("绕法线随机旋转")] public bool randomRoll;
        [InspectorName("连续剑痕最长距离（米）"), Min(.01f)] public float maxStrokeLength = .65f;
        [InspectorName("持续火焰重复盖印距离（米）"), Min(.01f)] public float repeatSpacing = .12f;
        [InspectorName("连续痕迹允许的表面夹角（度）"), Range(0, 89)] public float joinAngle = 30;

        internal bool Accepts(Collider hit) => hit && !hit.isTrigger && material &&
            (receivers.value & (1 << hit.gameObject.layer)) != 0 &&
            size.x > 0 && size.y > 0 && size.z > 0 && holdSeconds + fadeSeconds > 0;
    }
}
