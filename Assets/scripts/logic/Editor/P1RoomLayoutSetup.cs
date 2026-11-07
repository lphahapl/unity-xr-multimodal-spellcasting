#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using NarutoVR.SealCombat.Rpg;
using NarutoVR.SealCombat.Enemies;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.AI;
using Unity.AI.Navigation;
using System.Collections.Generic;
using Object = UnityEngine.Object;

namespace NarutoVR.SealCombat.Editor
{
    public static class P1RoomLayoutSetup
    {
        public const string LogFolder = "Logs/p1-room-20261104";
        const string RootName = "试炼扩建 · 通廊与战斗房间";
        const string NavPath = "Assets/Configuration/Enemies/P1RoomNavMesh.asset";
        static Scene RequireScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (Application.isPlaying || scene.path != LinearRpgValidation.P1ScenePath)
                throw new InvalidOperationException("请在编辑模式打开 KaogongP1Trial。");
            return scene;
        }
        [MenuItem("Tools/NarutoVR/P1试炼/扩建通廊与战斗房间（一次性）")]
        public static void ApplyMenu() => Debug.Log(Apply());
        public static string Apply()
        {
            var scene = RequireScene();
            if (scene.GetRootGameObjects().Any(g => g.name == RootName)) throw new InvalidOperationException("房间已经建立；使用检查命令，不重复生成。");
            Directory.CreateDirectory(LogFolder);
            // Save a copy of the live scene too: the user's unsaved layout is part of the backup.
            string backup = LogFolder + "/KaogongP1Trial_beforeRoom_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".unity";
            if (!EditorSceneManager.SaveScene(scene, backup, true)) throw new IOException("场景备份失败，未修改布局");
            var rpg = Components<LinearRpgController>(scene).Single();
            var targets = Components<SkillTrainingTarget>(scene);
            var kept = targets.OrderBy(t => (t.transform.position - new Vector3(0, 1.6f, 5)).sqrMagnitude).First();
            var yard = kept.transform.parent;
            var stone = Components<Renderer>(scene).First(r => r.name == "庭院石台").sharedMaterial;
            var dark = Components<Renderer>(scene).First(r => r.name == "背墙").sharedMaterial;
            var glow = Components<Renderer>(scene).First(r => r.name == "靶心").sharedMaterial;
            var marker = Components<NavigationNode>(scene).First().GetComponent<LineRenderer>().sharedMaterial;
            var labelTemplate = Components<UnityEngine.UI.Text>(scene).First(t => t.name.Contains("标牌") || t.transform.parent.name.Contains("标牌"));
            var font = labelTemplate.font;
            foreach (var t in targets.Where(t => t != kept)) Object.DestroyImmediate(t.gameObject);
            foreach (Transform child in yard.Cast<Transform>().ToArray())
            {
                bool targetDecoration = child.name == "靶心" || child.name.Contains("标牌") ||
                    (child.name == "能量环" && child.position.y > .8f && child.position.y < 2.2f);
                if (targetDecoration && Vector3.Distance(new Vector3(child.position.x, 0, child.position.z), new Vector3(0, 0, 5)) > .3f)
                    Object.DestroyImmediate(child.gameObject);
                else if (child.name == "背墙" || child.name.Contains("后庭台阶")) Object.DestroyImmediate(child.gameObject);
            }
            kept.name = "练习靶 · 5米 · 可重复练习"; kept.resetAfterSeconds = 3;
            rpg.targets = new[] { kept }; rpg.autoAdvanceStages = false; rpg.roomControlsEncounter = true;
            var root = new GameObject(RootName).transform;
            var hall = new GameObject("连接通廊 · 8米").transform; hall.SetParent(root, false);
            var room = new GameObject("战斗房间 · 14×16米").transform; room.SetParent(root, false);
            Box(hall, "入口左墙", new Vector3(-4.65f, 1.6f, 10), new Vector3(3.2f, 3.2f, .35f), dark);
            Box(hall, "入口右墙", new Vector3(4.65f, 1.6f, 10), new Vector3(3.2f, 3.2f, .35f), dark);
            Box(hall, "通廊地面", new Vector3(0, -.28f, 14), new Vector3(6.4f, .5f, 8.4f), stone);
            foreach (float side in new[] { -1f, 1f })
            {
                Box(hall, "通廊侧墙", new Vector3(side * 3.2f, 1.5f, 14), new Vector3(.35f, 3, 8), dark);
                Box(room, "房间侧墙", new Vector3(side * 7.2f, 2, 26), new Vector3(.4f, 4, 16.8f), dark);
                Box(room, "入口两侧墙", new Vector3(side * 4.95f, 2, 18), new Vector3(4.5f, 4, .4f), dark);
                Box(hall, "通廊边缘导光", new Vector3(side * 2.9f, .025f, 14), new Vector3(.045f, .025f, 7.8f), glow, false);
                Box(room, "房间边缘导光", new Vector3(side * 6.8f, .025f, 26), new Vector3(.06f, .025f, 15.6f), glow, false);
                for (int z = 20; z <= 32; z += 4)
                {
                    Box(room, "石柱", new Vector3(side * 6.9f, 1.5f, z), new Vector3(.5f, 3, .6f), stone);
                    Box(room, "柱上导光", new Vector3(side * 6.6f, 2, z), new Vector3(.07f, 1.3f, .12f), glow, false);
                }
            }
            Box(room, "房间地面", new Vector3(0, -.28f, 26), new Vector3(14.8f, .5f, 16.8f), stone);
            Box(room, "房间后墙", new Vector3(0, 2, 34.2f), new Vector3(14.8f, 4, .4f), dark);
            Box(room, "门楣", new Vector3(0, 3.7f, 18), new Vector3(5.4f, .6f, .5f), stone);
            Box(room, "进入战斗边界线", new Vector3(0, .035f, 19), new Vector3(13.5f, .025f, .07f), glow, false);
            for (int z = 20; z <= 32; z += 2)
                Box(room, "地面分缝", new Vector3(0, -.015f, z), new Vector3(13.6f, .015f, .025f), dark, false);
            Sign(hall, font, "前方：战斗试炼室\n越过室内光线后进入战斗", new Vector3(0, 3.1f, 17.7f));
            Sign(room, font, "考 工 试 炼\n击败残机偶 · 退出房间暂停战斗", new Vector3(0, 3.1f, 33.95f));
            // Only room geometry is collected. The corridor is deliberately not enemy walkable space.
            var surface = room.gameObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children; surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();
            if (!surface.navMeshData) throw new Exception("战斗房间导航烘焙失败");
            if (AssetDatabase.LoadAssetAtPath<NavMeshData>(NavPath)) throw new Exception("导航资产已存在，拒绝覆盖");
            AssetDatabase.CreateAsset(surface.navMeshData, NavPath);
            var nodeRoot = new GameObject("扩建导航点").transform; nodeRoot.SetParent(root, false);
            foreach (float z in new[] { 7f, 9.5f, 12f, 14.5f, 17f })
                foreach (float x in new[] { -1.75f, 0, 1.75f }) Node(nodeRoot, $"approach.{x}.{z}", new Vector3(x, -.03f, z), marker);
            for (int row = 0; row < 6; row++) for (int col = 0; col < 5; col++)
                Node(nodeRoot, $"room.{col}.{row}", new Vector3(-5 + col * 2.5f, -.03f, 20 + row * 2.5f), marker);
            Physics.SyncTransforms();
            var nodes = Components<NavigationNode>(scene);
            foreach (var node in nodes)
            {
                node.neighbors.Clear();
                foreach (var other in nodes)
                    if (node != other && Vector3.Distance(node.floorPosition, other.floorPosition) <= 3.65f && ClearPath(node.floorPosition, other.floorPosition, rpg)) node.neighbors.Add(other);
                EditorUtility.SetDirty(node);
            }
            var enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Enemies/CanJiOu/CanJiOu_Enemy.prefab");
            var enemyGo = (GameObject)PrefabUtility.InstantiatePrefab(enemyPrefab, root);
            enemyGo.name = "房间敌人 · 进入后唤醒"; enemyGo.transform.SetPositionAndRotation(new Vector3(0, -.03f, 29), Quaternion.Euler(0, 180, 0));
            var enemy = enemyGo.GetComponent<EnemyController>(); enemy.target = rpg.locomotion.head;
            enemy.detectionDistance = 15; enemy.loseDistance = 20;
            enemyGo.SetActive(false);
            var encounter = root.gameObject.AddComponent<CombatRoomEncounter>(); encounter.rpg = rpg; encounter.enemies = new[] { enemy };
            var volume = new GameObject("战斗区域 · 仅检测玩家脚下位置").AddComponent<BoxCollider>(); volume.transform.SetParent(root, false);
            volume.isTrigger = true; volume.center = new Vector3(0, 1, 26.4f); volume.size = new Vector3(13.6f, 4, 14.8f);
            encounter.area = volume;
            EditorUtility.SetDirty(rpg);
            var detail = Check();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("扩建场景保存失败");
            AssetDatabase.SaveAssets();
            File.WriteAllText(LogFolder + "/apply.txt", detail + "\nbackup=" + backup);
            return detail + "\n备份：" + backup;
        }
        static GameObject Box(Transform parent, string name, Vector3 position, Vector3 scale, Material material, bool solid = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.SetParent(parent, false);
            go.transform.position = position; go.transform.localScale = scale; go.GetComponent<Renderer>().sharedMaterial = material;
            if (!solid) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }
        static void Sign(Transform parent, Font font, string message, Vector3 position)
        {
            var go = new GameObject("房间指示牌", typeof(RectTransform), typeof(Canvas)); go.transform.SetParent(parent, false);
            go.transform.position = position; go.transform.localScale = Vector3.one * .003f;
            go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace; go.GetComponent<RectTransform>().sizeDelta = new Vector2(1400, 260);
            var label = new GameObject("说明", typeof(RectTransform), typeof(UnityEngine.UI.Text)).GetComponent<UnityEngine.UI.Text>();
            label.transform.SetParent(go.transform, false); label.rectTransform.sizeDelta = new Vector2(1400, 260);
            label.font = font; label.fontSize = 52; label.alignment = TextAnchor.MiddleCenter; label.text = message; label.color = new Color(.8f, .95f, 1); label.raycastTarget = false;
        }
        static void Node(Transform parent, string id, Vector3 at, Material marker)
        {
            var go = new GameObject("传送点 · " + id); go.transform.SetParent(parent, false); go.transform.position = at;
            go.AddComponent<NavigationNode>().nodeId = id;
            var line = go.AddComponent<LineRenderer>(); line.sharedMaterial = marker; line.useWorldSpace = false; line.loop = true;
            line.startWidth = line.endWidth = .018f; line.positionCount = 24;
            for (int i = 0; i < 24; i++) { float a = i * Mathf.PI / 12; line.SetPosition(i, new Vector3(Mathf.Sin(a) * .18f, .04f, Mathf.Cos(a) * .18f)); }
        }
        static bool ClearPath(Vector3 a, Vector3 b, LinearRpgController rpg)
        {
            float radius = rpg.locomotion.bodyRadius;
            var low = a + Vector3.up * (radius + .07f); var high = a + Vector3.up * (1.7f - radius);
            return !Physics.CapsuleCastAll(low, high, radius, (b - a).normalized, Vector3.Distance(a, b), rpg.locomotion.obstacleLayers, QueryTriggerInteraction.Ignore)
                .Any(h => !h.collider.transform.IsChildOf(rpg.locomotion.rigRoot));
        }
        public static string Check()
        {
            var scene = RequireScene(); var rpg = Components<LinearRpgController>(scene).Single();
            var nodes = Components<NavigationNode>(scene); var encounter = Components<CombatRoomEncounter>(scene).Single();
            void Assert(bool ok, string why) { if (!ok) throw new Exception(why); }
            Assert(Components<SkillTrainingTarget>(scene).Length == 1 && rpg.targets.Length == 1, "练习靶必须仅一个");
            Assert(rpg.roomControlsEncounter && !rpg.autoAdvanceStages, "靶场阶段仍在控制战斗");
            Assert(nodes.Length >= 65 && nodes.Select(n => n.nodeId).Distinct().Count() == nodes.Length, "导航点不足或编号重复");
            Assert(encounter.UsesEnemyManager
                ? encounter.spawnPoints.Length > 0 && encounter.spawnPoints.All(p => p != null && p.point) && Components<EnemyController>(scene).Length == 0
                : encounter.enemies.Length > 0 && encounter.enemies.All(e => e && !e.gameObject.activeSelf), "敌人必须等待房间触发");
            Assert(!encounter.area.bounds.Contains(new Vector3(0, 0, 5)), "练习区落入战斗区域");
            var reachable = new HashSet<NavigationNode> { nodes[0] }; var queue = new Queue<NavigationNode>(); queue.Enqueue(nodes[0]);
            Physics.SyncTransforms();
            while (queue.Count > 0) foreach (var other in queue.Dequeue().neighbors) if (reachable.Add(other)) queue.Enqueue(other);
            Assert(reachable.Count == nodes.Length, $"节点图不连通 {reachable.Count}/{nodes.Length}");
            foreach (var node in nodes)
            {
                Assert(Physics.Raycast(node.floorPosition + Vector3.up * .15f, Vector3.down, out var floor, .3f, rpg.locomotion.floorLayers, QueryTriggerInteraction.Ignore), "节点无地面：" + node.nodeId);
                foreach (var other in node.neighbors) Assert(other.neighbors.Contains(node) && ClearPath(node.floorPosition, other.floorPosition, rpg), "边穿墙或非双向：" + node.nodeId);
            }
            var surface = Components<NavMeshSurface>(scene).Single();
            Assert(surface.navMeshData && NavMesh.SamplePosition(new Vector3(0, 0, 29), out _, 1, NavMesh.AllAreas), "房间无可用导航面");
            Assert(!NavMesh.SamplePosition(new Vector3(0, 0, 12), out _, 1, NavMesh.AllAreas), "敌人导航面泄漏到通廊");
            string detail = $"通过：单靶；走廊8米；房间14×16米；{nodes.Length}个导航点全部连通且路径无障碍；敌人仅房间内导航；练习区不触发战斗。";
            Directory.CreateDirectory(LogFolder); File.WriteAllText(LogFolder + "/layout-check.txt", detail); return detail;
        }
        public static string Preview()
        {
            RequireScene(); Directory.CreateDirectory(LogFolder);
            var go = new GameObject("临时场景预览相机") { hideFlags = HideFlags.HideAndDontSave };
            var camera = go.AddComponent<Camera>(); camera.enabled = false; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.08f, .1f, .14f); camera.farClipPlane = 130;
            var rt = new RenderTexture(1600, 1000, 24); camera.targetTexture = rt;
            var previous = RenderTexture.active; var texture = new Texture2D(1600, 1000, TextureFormat.RGB24, false);
            try
            {
                void Capture(string name, Vector3 position, Vector3 target)
                {
                    go.transform.position = position; go.transform.LookAt(target); camera.Render(); RenderTexture.active = rt;
                    texture.ReadPixels(new Rect(0, 0, 1600, 1000), 0, 0); texture.Apply(); File.WriteAllBytes(LogFolder + "/" + name + ".png", texture.EncodeToPNG());
                }
                camera.orthographic = true; camera.orthographicSize = 22;
                Capture("layout-overview", new Vector3(23, 34, -6), new Vector3(0, 0, 15));
                camera.orthographic = false; camera.fieldOfView = 75;
                Capture("room-entry", new Vector3(0, 1.7f, 19.3f), new Vector3(0, 1.7f, 29));
            }
            finally { RenderTexture.active = previous; camera.targetTexture = null; rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(texture); Object.DestroyImmediate(go); }
            return LogFolder + "/layout-overview.png; room-entry.png";
        }
        static T[] Components<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<T>(true)).ToArray();
        public static string Inspect()
        {
            var scene = SceneManager.GetActiveScene();
            var text = new StringBuilder($"scene={scene.path} dirty={scene.isDirty} playing={Application.isPlaying}\n");
            foreach (var root in scene.GetRootGameObjects()) text.AppendLine($"ROOT {root.name} active={root.activeSelf} pos={root.transform.position}");
            foreach (var group in Components<Renderer>(scene).Where(r => !(r is ParticleSystemRenderer)).GroupBy(r => r.name))
            {
                Bounds bounds = group.First().bounds; foreach (var r in group.Skip(1)) bounds.Encapsulate(r.bounds);
                text.AppendLine($"GEOMETRY {group.Key} count={group.Count()} min={bounds.min} max={bounds.max}");
            }
            foreach (var node in Components<NavigationNode>(scene)) text.AppendLine($"NODE {node.nodeId} pos={node.transform.position} neighbors={node.neighbors.Count} active={node.gameObject.activeInHierarchy}");
            foreach (var target in Components<SkillTrainingTarget>(scene)) text.AppendLine($"TARGET {target.name} pos={target.transform.position} parent={target.transform.parent?.name} active={target.gameObject.activeInHierarchy}");
            foreach (var enemy in Components<EnemyController>(scene)) text.AppendLine($"ENEMY {enemy.name} pos={enemy.transform.position} active={enemy.gameObject.activeInHierarchy} prefab={PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(enemy)}");
            foreach (var rpg in Components<LinearRpgController>(scene))
            {
                text.AppendLine($"RPG {rpg.name} targets={rpg.targets.Length} autoAdvance={rpg.autoAdvanceStages} head={rpg.locomotion.head.position} rig={rpg.locomotion.rigRoot.position}");
                foreach (var stage in rpg.stages) text.AppendLine($"STAGE {stage.stageId} combat={stage.startsInCombat} hazards={stage.enableHazards} path={AssetDatabase.GetAssetPath(stage)}");
            }
            Directory.CreateDirectory(LogFolder); File.WriteAllText(LogFolder + "/inspect.txt", text.ToString());
            return text.ToString();
        }
    }
}
#endif
