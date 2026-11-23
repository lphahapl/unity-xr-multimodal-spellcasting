using UnityEngine;

namespace NarutoVR.SealCombat.Rpg
{
    [CreateAssetMenu(menuName = "NarutoVR/玩家手模/准望匣佩戴配置")]
    public sealed class WatchMountProfile : ScriptableObject
    {
        [Tooltip("静置掌面坐标，以米计。+Z 指向指根，+Y 与原准望匣佩戴基准一致。")]
        public Vector3 offsetMetres = new(-.0044f, -.0254f, -.040f);
        [Tooltip("X、Y 校正双道表带与小臂的轴线，Z 使左腕匣面向内侧倾 12 度。")]
        public Vector3 tiltDegrees = new(-5f, 5f, -12f);
    }
}
