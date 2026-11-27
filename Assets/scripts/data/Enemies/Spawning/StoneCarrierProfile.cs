using UnityEngine;

namespace NarutoVR.SealCombat.Enemies
{
    /// <summary>同一工奴定义共享镇石外形、背载位置和行动参数。</summary>
    [CreateAssetMenu(menuName = "考工律度/敌人/背石工奴配置")]
    public sealed class StoneCarrierProfile : ScriptableObject
    {
        public BallastStone stone;
        [InspectorName("搬运剪辑与石根逐帧姿态")] public StoneCarryPose carryPose;
        [InspectorName("未配置搬运曲线时的静态位置")] public Vector3 carryOffset = new(0, 1.1f, -.42f);
        [InspectorName("搬运剪辑原速对应的地速（米/秒）"), Min(.05f)] public float clipWalkSpeed = .85f;
        [Min(.05f)] public float patrolSpeed = .85f, retreatSpeed = 2.1f;
        [Min(.05f)] public float arrivalDistance = .45f, retreatDuration = 4.5f;
        [Min(0)] public float patrolPause = 1.2f;
        [InspectorName("未配巡逻点时的左右距离"), Min(.5f)] public float defaultRadius = 2.5f;
    }
}
