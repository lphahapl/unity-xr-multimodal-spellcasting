using System.Collections.Generic;
using UnityEngine;

namespace NarutoVR.SealCombat.Rpg
{
    [DisallowMultipleComponent]
    public sealed class NavigationNode : MonoBehaviour
    {
        static readonly List<NavigationNode> activeNodes = new List<NavigationNode>();
        public static IReadOnlyList<NavigationNode> ActiveNodes => activeNodes;

        [Header("预设导航点")]
        [InspectorName("稳定节点编号"), Tooltip("场景内唯一；不要用位置或实例编号作为存档ID。")]
        public string nodeId = "node";
        [InspectorName("相邻可达点"), Tooltip("在此点时只从此列表选点。需要双向通行时请双向配置。")]
        public List<NavigationNode> neighbors = new List<NavigationNode>();
        [InspectorName("覆盖碰撞层设置")] public bool overrideLayers;
        [InspectorName("地面层")] public LayerMask floorLayers = ~0;
        [InspectorName("障碍层")] public LayerMask obstacleLayers = ~0;
        public Vector3 floorPosition => transform.position;

        void OnEnable() { if (!activeNodes.Contains(this)) activeNodes.Add(this); }
        void OnDisable() { activeNodes.Remove(this); }

        void OnDrawGizmos()
        {
#if UNITY_EDITOR
            if (gameObject.scene.path == "Assets/Scene/Chapter01_Awakening.unity" &&
                !UnityEditor.Selection.Contains(gameObject)) return;
#endif
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(floorPosition + Vector3.up * 0.03f, 0.15f);
            if (neighbors == null) return;
            foreach (NavigationNode neighbor in neighbors)
                if (neighbor != null) Gizmos.DrawLine(floorPosition + Vector3.up * 0.03f,
                    neighbor.floorPosition + Vector3.up * 0.03f);
        }
    }
}
