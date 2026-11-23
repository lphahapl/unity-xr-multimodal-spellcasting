using System;
using UnityEngine;

namespace NarutoVR.SealCombat.Rpg
{
    [AddComponentMenu("任务引导/光点路线")]
    public sealed class QuestGuideRoute : MonoBehaviour
    {
        [InspectorName("依次经过的地面点"), Tooltip("沿可行走路径排列场景 Transform。普通空物体和已有导航点都可以。")]
        public Transform[] points = Array.Empty<Transform>();

        internal int EndIndex(Transform destination)
        {
            if (points == null || points.Length == 0 || Array.IndexOf(points, null) >= 0) return -1;
            return destination ? Array.IndexOf(points, destination) : points.Length - 1;
        }

        void OnDrawGizmosSelected()
        {
            if (points == null) return;
            Gizmos.color = new Color(1f, .74f, .25f);
            for (int i = 0; i < points.Length; i++)
            {
                if (!points[i]) continue;
                Gizmos.DrawWireSphere(points[i].position, .06f);
                if (i > 0 && points[i-1]) Gizmos.DrawLine(points[i-1].position, points[i].position);
            }
        }
    }

    [Serializable]
    public sealed class QuestVisualHints
    {
        [InspectorName("光点路线")] public QuestGuideRoute route;
        [InspectorName("路线终点"), Tooltip("从路线里拖一个点；留空使用最后一个点。")] public Transform destination;
        [InspectorName("开始指路前等待（秒）"), Min(0)] public float trailDelay;
        [InspectorName("手势提示"), Tooltip("依次选择第一个在范围内且满足条件的提示。")]
        public QuestGestureHint[] gestures = Array.Empty<QuestGestureHint>();
    }
}
