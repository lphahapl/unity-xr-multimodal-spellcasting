#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NarutoVR.SealCombat.Rpg;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NarutoVR.SealCombat.Editor
{
    /// <summary>Explicit, additive migration: preserves existing node IDs, positions, checkpoints and custom edges.</summary>
    public static class RpgNavigationScoringSetup
    {
        [MenuItem("Tools/NarutoVR/线性RPG/扩展导航点并启用融合评分")]
        public static void Menu() => Debug.Log("[导航配置] " + Configure());

        public static string Configure()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请先退出播放模式");
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != LinearRpgSceneSetup.ScenePath || scene.isDirty)
                throw new InvalidOperationException("仅配置已保存且当前打开的 LinearRpgTest 场景");
            var controllers = Components<RpgLocomotionController>(scene);
            if (controllers.Length != 1) throw new InvalidOperationException("场景须有且只有一个导航控制器");
            var controller = controllers[0];
            var settings = controller.scoring ?? new NavigationScoringSettings();
            if (!settings.Valid) throw new InvalidOperationException("现有评分参数无效，未修改场景或覆盖用户参数");
            var nodes = Components<NavigationNode>(scene);
            if (nodes.GroupBy(n => n.nodeId).Any(g => g.Count() != 1)) throw new InvalidOperationException("节点编号重复，拒绝推测扩展");
            var byId = nodes.ToDictionary(n => n.nodeId);
            NavigationNode Existing(string id) => byId.TryGetValue(id, out var value) ? value :
                throw new InvalidOperationException("缺少原网格锚点：" + id);
            var center = Existing("courtyard.1.1");
            Vector3 origin = center.floorPosition;
            Vector3 xStep = Existing("courtyard.2.1").floorPosition - origin;
            Vector3 zStep = Existing("courtyard.1.2").floorPosition - origin;
            if (Mathf.Abs(xStep.magnitude - 1.5f) > .05f || Mathf.Abs(zStep.magnitude - 1.5f) > .05f ||
                Mathf.Abs(xStep.y) > .05f || Mathf.Abs(zStep.y) > .05f || Mathf.Abs(Vector3.Dot(xStep.normalized, zStep.normalized)) > .05f)
                throw new InvalidOperationException("原网格已变形，拒绝覆盖用户布置；请手动扩点");
            Collider floor = Components<Collider>(scene).SingleOrDefault(c => c.enabled && !c.isTrigger && c.name == "庭院石台");
            if (!floor) throw new InvalidOperationException("缺少真实庭院地面碰撞体");
            Physics.SyncTransforms();
            var additions = new Dictionary<string, Vector3>();
            int skipped = 0;
            for (int z = 0; z <= 4; z++) for (int x = -1; x <= 3; x++)
            {
                string id = "courtyard." + x + "." + z;
                if (byId.ContainsKey(id)) continue;
                Vector3 expected = origin + (x - 1) * xStep + (z - 1) * zStep;
                if (!floor.Raycast(new Ray(expected + Vector3.up * controller.floorSearchUp, Vector3.down), out RaycastHit hit,
                    controller.floorSearchUp + controller.floorSearchDown) ||
                    Vector3.Angle(hit.normal, Vector3.up) > controller.maximumSlope ||
                    Mathf.Abs(hit.point.y - origin.y) > controller.maximumFloorStep)
                { skipped++; continue; }
                additions.Add(id, hit.point);
            }
            string backupFolder = Path.GetFullPath(Path.Combine(Application.dataPath, "../../NarutoVR_Backups"));
            Directory.CreateDirectory(backupFolder);
            string backup = Path.Combine(backupFolder, "LinearRpgTest-before-node-scoring-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + ".unity");
            File.Copy(Path.GetFullPath(scene.path), backup, false);
            var templateLine = center.GetComponent<LineRenderer>();
            foreach (var item in additions)
            {
                var value = new GameObject("导航点 " + item.Key.Substring("courtyard.".Length));
                value.transform.SetParent(center.transform.parent, false);
                value.transform.position = item.Value;
                var node = value.AddComponent<NavigationNode>();
                node.nodeId = item.Key;
                node.overrideLayers = center.overrideLayers;
                node.floorLayers = center.floorLayers;
                node.obstacleLayers = center.obstacleLayers;
                if (templateLine)
                {
                    var line = value.AddComponent<LineRenderer>();
                    EditorUtility.CopySerialized(templateLine, line);
                }
                byId.Add(item.Key, node);
            }
            // Only generated courtyard nodes receive extra edges; existing custom links are never removed.
            var grid = byId.Values.Where(n => IsGridId(n.nodeId)).ToArray();
            int addedLinks = 0;
            foreach (var from in grid) foreach (var to in grid)
            {
                Vector3 delta = to.floorPosition - from.floorPosition;
                if (from == to || Mathf.Abs(delta.y) > controller.nodeHeightTolerance || delta.magnitude > 3.1f) continue;
                from.neighbors ??= new List<NavigationNode>();
                if (from.neighbors.Contains(to)) continue;
                from.neighbors.Add(to); addedLinks++;
            }
            controller.scoring = settings;
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, LinearRpgSceneSetup.ScenePath)) throw new IOException("导航配置后保存失败");
            return $"网格 {grid.Length} 点，新增 {additions.Count} 点、{addedLinks} 条单向邻接；无地面跳过 {skipped} 点。旧编号/位置/检查点保留，备份：{backup}";
        }

        static bool IsGridId(string id)
        {
            string[] parts = id.Split('.');
            return parts.Length == 3 && parts[0] == "courtyard" && int.TryParse(parts[1], out int x) &&
                int.TryParse(parts[2], out int z) && x >= -1 && x <= 3 && z >= 0 && z <= 4;
        }

        static T[] Components<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
    }
}
#endif
