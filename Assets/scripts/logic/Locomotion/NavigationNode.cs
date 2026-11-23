using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NarutoVR.SealCombat.Rpg
{
    [DisallowMultipleComponent]
    public sealed class NavigationNode : MonoBehaviour
    {
        static readonly List<NavigationNode> activeNodes = new List<NavigationNode>();
        static readonly HashSet<NavigationNode> registered = new();
        static readonly HashSet<Scene> exploringScenes = new();
        LineRenderer suppressedMarker;
        public static IReadOnlyList<NavigationNode> ActiveNodes => activeNodes;
        internal static bool IsRegistered(NavigationNode node) => registered.Contains(node);

        [Header("预设导航点")]
        [InspectorName("稳定节点编号"), Tooltip("场景内唯一；不要用位置或实例编号作为存档ID。")]
        public string nodeId = "node";
        [InspectorName("相邻可达点"), Tooltip("直接相邻的有向连接。战斗可沿最多四段连接选点，探索按手指所指的地面选点。双向通行请双向配置。")]
        public List<NavigationNode> neighbors = new List<NavigationNode>();
        [InspectorName("覆盖碰撞层设置")] public bool overrideLayers;
        [InspectorName("地面层")] public LayerMask floorLayers = ~0;
        [InspectorName("障碍层")] public LayerMask obstacleLayers = ~0;
        public Vector3 floorPosition => transform.position;

        internal static void SetExploring(Scene scene, bool exploring)
        {
            bool changed = exploring ? exploringScenes.Add(scene) : exploringScenes.Remove(scene);
            if (!changed) return;
            foreach (var node in activeNodes)
                if (node && node.gameObject.scene == scene) node.HideAuthoredMarker(exploring);
        }

        void HideAuthoredMarker(bool hide)
        {
            if (hide)
            {
                if (!suppressedMarker && TryGetComponent<LineRenderer>(out var line) && !line.forceRenderingOff)
                {
                    suppressedMarker = line;
                    line.forceRenderingOff = true;
                }
            }
            else if (suppressedMarker)
            {
                suppressedMarker.forceRenderingOff = false;
                suppressedMarker = null;
            }
        }

        void OnEnable()
        {
            if (registered.Add(this)) activeNodes.Add(this);
            if (exploringScenes.Contains(gameObject.scene)) HideAuthoredMarker(true);
        }

        void OnDisable()
        {
            HideAuthoredMarker(false);
            if (registered.Remove(this)) activeNodes.Remove(this);
        }

        void OnDrawGizmos()
        {
#if UNITY_EDITOR
            if (Application.isPlaying && exploringScenes.Contains(gameObject.scene)) return;
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
