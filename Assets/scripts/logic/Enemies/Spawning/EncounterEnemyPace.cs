using UnityEngine;

namespace NarutoVR.SealCombat.Enemies
{
    /// <summary>挂在房间出生标记上，只调节该标记本次租约的移动。工奴仍由原工作点决定路线。</summary>
    [DisallowMultipleComponent]
    public sealed class EncounterEnemyPace : MonoBehaviour
    {
        [Min(.01f), InspectorName("巡逻速度（米/秒）")] public float patrolSpeed = .9f;
        [Min(.01f), InspectorName("追击/射位/工奴撤离速度（米/秒）")] public float engagedSpeed = 2f;
        [Min(0f), InspectorName("巡逻到点停留（秒）")] public float patrolWait = 1.5f;
        [Min(.1f), InspectorName("工奴撤离时限（秒）")] public float retreatSeconds = 8.5f;
        [InspectorName("普通敌人巡逻点（空时保留原配置）")] public Transform[] patrolPoints = System.Array.Empty<Transform>();
    }
}
