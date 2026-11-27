using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using NarutoVR.SealCombat.Items;
using NarutoVR.SealCombat.Rpg;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace NarutoVR.SealCombat.Editor
{
    public static class Chapter02BridgeEquipmentInstaller
    {
        public const string ScenePath = "Assets/Scene/Chapter02_SuspendedPath.unity";
        public const string ApproachId = "CH02_BRIDGE_APPROACH";
        public const string DrawId = "CH02_BRIDGE_DRAW";
        const string SourceSha256 = "e82f24bdef5532e0a39fc9551860a00f7de6aa9c7a15277f842b1d1c8a33681f";
        const string StageFolder = "Assets/ScriptableObjects/Chapter02Workshop";
        const string BeltPath = "Assets/Art/Props/ShenShangToolBelt/ChapterToolBelt.prefab";
        const string PassagePath = "Assets/Art/Glyphs/Passage.asset";
        const string BronzePath = "Assets/Art/Environment/Opening/ConcentricGauge/Bronze/CG_A1_Bronze.mat";
        const string StandName = "画符站位";
        const string TableName = "墨规画符石案";
        const string BarrierName = "断裂态 · 跨岸阻挡";
        const string DeckName = "修复态 · 桥面与碰撞";
        const string FixtureName = "断桥通路符";
        const string FallBoundaryName = "墨规笔防坠下界";
        static readonly string[] OriginalIds = { "CH02_REACH_HUB", "CH02_WORKSHOP_TENON", "CH02_B_DESCENT" };
        static readonly Vector3 DrawOffset = new Vector3(0f, 0f, .85f);
        static readonly Vector3 ApproachSize = new Vector3(1.4f, .7f, .85f);

        [MenuItem("Tools/NarutoVR/第二章断衡悬道/安装石案画符与工具带")]
        public static void Run()
        {
            RequireEditMode();
            var scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.isLoaded)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            if (scene.isDirty)
                throw new InvalidOperationException("二章有未保存的改动。请先处理现有改动，再执行设备教学安装。");
            if (!Apply(scene))
            {
                Debug.Log("[Chapter02BridgeEquipment] 已安装，接线验证通过。");
                return;
            }
            if (!EditorSceneManager.SaveScene(scene))
                throw new IOException("保存二章设备教学场景失败：" + scene.path);
            Debug.Log("[Chapter02BridgeEquipment] 已安装并保存 " + scene.path);
        }

        // The caller owns scene opening and saving. A byte-identical review copy is also accepted.
        public static bool Apply(Scene scene)
        {
            RequireLoadedScene(scene);
            var player = Single<LinearRpgController>(scene);
            if (Components<Chapter02BridgeEquipment>(scene).Any())
            {
                VerifyInstalled(scene);
                return false;
            }
            VerifySourceHash(scene);

            var flow = player.chapter as ObjectiveChapterFlow;
            Require(flow && flow.Owner == player && flow.finalCheckpoint && player.handTracking && player.locomotion,
                "二章原流程、最终检查点、手部输入或移动接线缺失。");
            CheckRoute(player, flow, OriginalIds);
            Require(player.CheckpointAt(0).nodeId == "CH02_ENTRANCE",
                "二章入口检查点 ID 已变更。");
            Require(!Components<GlyphPenController>(scene).Any() && !Components<ChapterToolBelt>(scene).Any(),
                "场景已有未登记的工具带或画符接线，请先核对。");

            var stand = Named(scene, StandName);
            var table = Named(scene, TableName);
            var slab = table.GetComponent<BoxCollider>();
            var broken = Named(scene, BarrierName).gameObject;
            var repaired = Named(scene, DeckName).gameObject;
            var valley = Components<Transform>(scene).Single(t =>
                !t.parent && t.name.StartsWith("05 · 谷底平地", StringComparison.Ordinal));
            Require(slab && slab.enabled && !slab.isTrigger, "画符石案缺少现有实体碰撞。");
            Require(Vector3.Distance(stand.position, new Vector3(-9.121364f, 80f, 57f)) < .02f &&
                Vector3.Distance(table.position, new Vector3(-7.921364f, 80f, 58.85f)) < .02f,
                "画符站位或石案与已核对的坐标不符。");
            CheckBridge(broken, repaired);
            Require(!Components<NavigationNode>(scene).Any(node => node.nodeId == DrawId),
                "场景已有未登记的画符导航节点。");

            var beltPrefab = RequiredAsset<GameObject>(BeltPath).GetComponent<ChapterToolBelt>();
            CheckPen(beltPrefab);
            var template = RequiredAsset<GlyphTemplate>(PassagePath);
            Require(template.effect is PassageGlyphEffect, "断桥剧情画板需要通路符。");
            var bronze = RequiredAsset<Material>(BronzePath);
            Require(AssetDatabase.IsValidFolder(StageFolder), "二章阶段资源目录缺失。");
            var approach = Stage(ApproachId, "前往断桥石案", "走到断桥旁的墨规画符石案。");
            var draw = RequiredAsset<Chapter02PassageStage>($"{StageFolder}/{DrawId}.asset");

            var originalRows = flow.stages;
            var originalNodes = player.checkpointNodes;
            var originalStages = player.stages;
            var previousFinal = flow.finalCheckpoint;
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("安装二章断桥设备教学");
            try
            {
                var checkpoint = AddDrawCheckpoint(stand);
                var reach = AddReachArea(checkpoint.gameObject);
                var (panel, button, signal) = AddDrawingStation(table, slab, checkpoint.floorPosition, player, template, bronze);
                var equipment = AddBelt(player, beltPrefab, valley, out var glyph);

                var bridge = Undo.AddComponent<Chapter02BridgeEquipment>(player.gameObject);
                bridge.player = player;
                bridge.equipment = equipment;
                bridge.glyph = glyph;
                bridge.panel = panel;
                bridge.button = button;
                bridge.drawingObjective = signal;
                bridge.approachStage = approach;
                bridge.drawingStage = draw;
                bridge.originalFirstStage = originalStages[0];
                bridge.brokenBarrier = broken;
                bridge.repairedDeck = repaired;
                UnityEventTools.AddPersistentListener(button.onPressed, bridge.OpenPanel);
                UnityEventTools.AddPersistentListener(panel.onCompleted, bridge.CompleteDrawing);

                Undo.RecordObjects(new UnityEngine.Object[] { player, flow }, "接入断桥教学阶段");
                flow.stages = new[]
                {
                    new ChapterObjectiveStage { stage = approach, objectives = new ChapterObjective[] { reach } },
                    new ChapterObjectiveStage { stage = draw, objectives = new ChapterObjective[] { signal } }
                }.Concat(originalRows).ToArray();
                player.ConfigureRoute(new[] { approach, draw }.Concat(originalStages).ToArray(),
                    new[] { originalNodes[0], checkpoint }.Concat(originalNodes).ToArray());
                PrefabUtility.RecordPrefabInstancePropertyModifications(player);
                PrefabUtility.RecordPrefabInstancePropertyModifications(flow);
                VerifyInstalled(scene);
                Require(previousFinal == flow.finalCheckpoint && flow.stages.Skip(2).SequenceEqual(originalRows) &&
                    player.checkpointNodes.Skip(2).SequenceEqual(originalNodes),
                    "装配改变了二章原阶段或检查点。");
                Undo.CollapseUndoOperations(undoGroup);
                EditorSceneManager.MarkSceneDirty(scene);
                return true;
            }
            catch
            {
                Undo.RevertAllDownToGroup(undoGroup);
                throw;
            }
        }

        public static void VerifyInstalled(Scene scene)
        {
            RequireLoadedScene(scene);
            var player = Single<LinearRpgController>(scene);
            var flow = player.chapter as ObjectiveChapterFlow;
            var bridge = Single<Chapter02BridgeEquipment>(scene);
            Require(flow && flow.Owner == player && flow.finalCheckpoint && bridge.gameObject == player.gameObject &&
                bridge.player == player, "二章设备教学未接到原玩家流程。");
            CheckRoute(player, flow, new[] { ApproachId, DrawId }.Concat(OriginalIds).ToArray());
            CheckTutorialStage(bridge.approachStage, ApproachId);
            CheckTutorialStage(bridge.drawingStage, DrawId);
            Require(player.StageAt(0) == bridge.approachStage && player.StageAt(1) == bridge.drawingStage &&
                player.StageAt(2) == bridge.originalFirstStage &&
                player.CheckpointAt(0) == player.CheckpointAt(2) && player.CheckpointAt(0).nodeId == "CH02_ENTRANCE" &&
                player.CheckpointAt(3).nodeId == "CH02_HUB" && player.CheckpointAt(4) == player.CheckpointAt(3) &&
                flow.finalCheckpoint == player.CheckpointAt(4), "阶段或原检查点引用不一致。");

            var draw = player.CheckpointAt(1);
            var stand = Named(scene, StandName);
            var reach = flow.stages[0].objectives.SingleOrDefault() as ReachAreaChapterObjective;
            Require(draw.nodeId == DrawId && draw.transform.parent == stand &&
                Vector3.Distance(draw.floorPosition, stand.position + DrawOffset) < .01f &&
                reach && reach.gameObject == draw.gameObject && reach.area && reach.area.isTrigger &&
                reach.area.enabled && ReachAreaChapterObjective.ContainsFloor(reach.area, draw.floorPosition) &&
                Components<NavigationNode>(scene).Count(node => node.nodeId == DrawId) == 1,
                "画符检查点或站位区域接线错误。");
            Require(flow.stages.Take(2).All(stage => stage.trialSkills is { Length: 0 }) &&
                flow.stages[1].objectives.SingleOrDefault() == bridge.drawingObjective && bridge.drawingObjective,
                "画符教学阶段只能由到位和剧情板成功推进。");

            var pen = bridge.glyph;
            var belt = bridge.equipment;
            CheckPen(belt);
            var fall = belt.pen.worldFallBoundary;
            Require(belt.transform.parent == player.transform &&
                PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(belt.gameObject) == BeltPath &&
                belt.interaction.player == player && fall && fall.name == FallBoundaryName && fall.position.y < -80f &&
                fall.parent && !fall.parent.parent && fall.parent.name.StartsWith("05 · 谷底平地", StringComparison.Ordinal) &&
                !belt.pen.gameObject.activeSelf && pen && pen.gameObject == player.gameObject &&
                !pen.enabled && !pen.activeBoard &&
                pen.penRoot == belt.pen.transform && pen.nib == belt.pen.insertionTip && pen.holster == belt.pen.home.transform,
                "正式工具带、墨规笔或初始书写权限接线错误。");

            var panel = bridge.panel;
            var button = bridge.button;
            var table = Named(scene, TableName);
            Require(panel && panel.board && panel.board.transform == panel.transform &&
                bridge.drawingObjective.gameObject == panel.gameObject &&
                bridge.drawingStage is Chapter02PassageStage lesson && lesson.passage == RequiredAsset<GlyphTemplate>(PassagePath) && lesson.socketPrefab &&
                panel.transform.parent && panel.transform.parent.parent == table &&
                button && button.transform.parent == panel.transform.parent && button.cap && button.enabled &&
                !button.gameObject.activeSelf &&
                button.playerHandInput == player &&
                CallsOnly(button.onPressed, bridge, nameof(Chapter02BridgeEquipment.OpenPanel)) &&
                CallsOnly(panel.onCompleted, bridge, nameof(Chapter02BridgeEquipment.CompleteDrawing)),
                "剧情画板、铜扣或持久事件接线错误。");
            Require(bridge.brokenBarrier == Named(scene, BarrierName).gameObject &&
                bridge.repairedDeck == Named(scene, DeckName).gameObject, "断桥状态引用错误。");
            CheckBridge(bridge.brokenBarrier, bridge.repairedDeck);
            CheckStationClearance(draw, reach.area, table.GetComponent<BoxCollider>(), panel, button, bridge.brokenBarrier);
        }

        static NavigationNode AddDrawCheckpoint(Transform stand)
        {
            var obj = Child(stand, DrawId);
            obj.transform.position = stand.position + DrawOffset;
            var node = Undo.AddComponent<NavigationNode>(obj);
            node.nodeId = DrawId;
            return node;
        }

        static ReachAreaChapterObjective AddReachArea(GameObject checkpoint)
        {
            var area = Undo.AddComponent<BoxCollider>(checkpoint);
            area.center = Vector3.up * .10f;
            area.size = ApproachSize;
            area.isTrigger = true;
            var reach = Undo.AddComponent<ReachAreaChapterObjective>(checkpoint);
            reach.area = area;
            return reach;
        }

        static (GlyphStoryPanel panel, ZhunwangPressButton button, SignalChapterObjective signal)
            AddDrawingStation(Transform table, BoxCollider slab, Vector3 standing, LinearRpgController player,
                GlyphTemplate template, Material bronze)
        {
            var bounds = slab.bounds;
            var direction = Vector3.ProjectOnPlane(standing - bounds.center, Vector3.up).normalized;
            // The board faces its approach and stays just beyond the player-side face of the real stone.
            float tableEdge = Mathf.Min(bounds.extents.x / Mathf.Max(Mathf.Abs(direction.x), .001f),
                bounds.extents.z / Mathf.Max(Mathf.Abs(direction.z), .001f));
            var fixture = Child(table, FixtureName).transform;
            var facingEdge = bounds.center + direction * (tableEdge + .12f);
            fixture.SetPositionAndRotation(new Vector3(facingEdge.x, bounds.max.y + .28f, facingEdge.z),
                Quaternion.LookRotation(-direction, Vector3.up));

            var board = Child(fixture, "方矩定点画板");
            var panel = Undo.AddComponent<GlyphStoryPanel>(board);
            panel.board = Undo.AddComponent<GlyphBoard>(board);
            panel.template = template;
            var signal = Undo.AddComponent<SignalChapterObjective>(board);

            var buttonObject = Child(fixture, "展开画板铜扣");
            buttonObject.transform.localPosition = new Vector3(-.235f, -.12f, -.035f);
            var cap = GameObject.CreatePrimitive(PrimitiveType.Cube);
            SceneManager.MoveGameObjectToScene(cap, table.gameObject.scene);
            Undo.RegisterCreatedObjectUndo(cap, "新增固定画板铜扣");
            cap.name = "铜扣";
            cap.transform.SetParent(buttonObject.transform, false);
            cap.transform.localScale = new Vector3(.071f, .071f, .022f);
            Undo.DestroyObjectImmediate(cap.GetComponent<Collider>());
            cap.GetComponent<Renderer>().sharedMaterial = bronze;
            var button = Undo.AddComponent<ZhunwangPressButton>(buttonObject);
            button.cap = cap.transform;
            button.playerHandInput = player;
            button.touchRadius = .042f;
            buttonObject.SetActive(false);
            return (panel, button, signal);
        }

        static void CheckStationClearance(NavigationNode draw, BoxCollider approach, BoxCollider table,
            GlyphStoryPanel panel, ZhunwangPressButton button, GameObject barrier)
        {
            var floor = draw.floorPosition;
            var aim = Vector3.ProjectOnPlane(floor - panel.transform.position, Vector3.up).normalized;
            Require(Vector3.Dot(-panel.transform.forward, aim) > .95f, "画板书写面朝向偏离画符站位。");
            Require(table && panel.transform.position.y - GlyphBoard.HalfHeight > table.bounds.max.y &&
                button.cap.position.y > table.bounds.max.y, "画板或铜扣进入石案碰撞。");
            var contact = button.cap.position;
            contact.y = approach.bounds.center.y;
            Require(Vector3.Distance(contact, approach.ClosestPoint(contact)) < .65f,
                "从画符到达区域无法够到铜扣。");
            var landing = new Bounds(floor + Vector3.up * .8f, new Vector3(.65f, 1.6f, .65f));
            Require(!barrier.GetComponents<BoxCollider>().Any(c => c.bounds.Intersects(landing)),
                "画符读档检查点与断裂阻挡相交。");
        }

        static ChapterToolBelt AddBelt(LinearRpgController player, ChapterToolBelt prefab,
            Transform valley, out GlyphPenController glyph)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab.gameObject, player.transform);
            Undo.RegisterCreatedObjectUndo(instance, "接入正式章节工具带");
            var belt = instance.GetComponent<ChapterToolBelt>();
            var fall = Child(valley, FallBoundaryName).transform;
            fall.localPosition = Vector3.down * 12f;
            belt.interaction.player = player;
            belt.pen.worldFallBoundary = fall;
            belt.pen.gameObject.SetActive(false);
            PrefabUtility.RecordPrefabInstancePropertyModifications(belt.interaction);
            PrefabUtility.RecordPrefabInstancePropertyModifications(belt.pen);
            PrefabUtility.RecordPrefabInstancePropertyModifications(belt.pen.gameObject);

            glyph = Undo.AddComponent<GlyphPenController>(player.gameObject);
            glyph.enabled = false;
            glyph.penRoot = belt.pen.transform;
            glyph.nib = belt.pen.insertionTip;
            glyph.holster = belt.pen.home.transform;
            return belt;
        }

        static RpgStageDefinition Stage(string id, string title, string description)
        {
            string path = $"{StageFolder}/{id}.asset";
            var stage = AssetDatabase.LoadAssetAtPath<RpgStageDefinition>(path);
            if (!stage)
            {
                Require(!File.Exists(path) && !AssetDatabase.LoadMainAssetAtPath(path), "阶段资源路径已被占用：" + path);
                stage = ScriptableObject.CreateInstance<RpgStageDefinition>();
                stage.stageId = id;
                stage.displayName = title;
                stage.instructions = description;
                stage.startsInCombat = false;
                stage.rewardCurrency = 0;
                AssetDatabase.CreateAsset(stage, path);
            }
            CheckTutorialStage(stage, id);
            return stage;
        }

        static void CheckTutorialStage(RpgStageDefinition stage, string id) =>
            Require(stage && stage.stageId == id && AssetDatabase.GetAssetPath(stage) == $"{StageFolder}/{id}.asset" &&
                !string.IsNullOrEmpty(stage.displayName) && !string.IsNullOrEmpty(stage.instructions) &&
                !stage.startsInCombat && !stage.enableHazards && stage.rewardCurrency == 0 &&
                stage.unlockSkillIds is { Length: 0 }, "新增教学阶段奖惩或路径错误：" + id);

        static void CheckRoute(LinearRpgController player, ObjectiveChapterFlow flow, string[] ids)
        {
            Require(player.StageCount == ids.Length && flow.stages.Length == ids.Length,
                "二章阶段数量不符合装配基线。");
            for (int i = 0; i < ids.Length; i++)
            {
                var stage = player.StageAt(i);
                Require(stage && stage.stageId == ids[i] && player.CheckpointAt(i) &&
                    flow.stages[i]?.stage == stage && flow.stages[i].objectives is { Length: > 0 },
                    "二章路线或阶段目标不符：" + ids[i]);
            }
        }

        static void CheckPen(ChapterToolBelt belt) =>
            Require(belt && belt.interaction && belt.pen && belt.pen.Kind == GameItemKind.GlyphPen &&
                belt.pen.home && belt.pen.keyItem && belt.pen.grip && belt.pen.insertionTip,
                "正式工具带缺少可归槽的实体墨规笔或握点、笔尖。");

        static void CheckBridge(GameObject broken, GameObject repaired) =>
            Require(broken.activeSelf && !repaired.activeSelf &&
                broken.GetComponents<BoxCollider>().Any(c => c.enabled && !c.isTrigger) &&
                repaired.GetComponents<BoxCollider>().Any(c => c.enabled && !c.isTrigger && c.size.x > 3f && c.size.z > 3f) &&
                repaired.TryGetComponent<MeshFilter>(out var mesh) && mesh.sharedMesh,
                "断桥必须使用已有的断裂阻挡和带碰撞、模型的修复桥面。");

        static bool CallsOnly(UnityEvent call, MonoBehaviour target, string method) =>
            call.GetPersistentEventCount() == 1 && call.GetPersistentTarget(0) == target &&
            call.GetPersistentMethodName(0) == method && call.GetPersistentListenerState(0) != UnityEventCallState.Off;

        static GameObject Child(Transform parent, string name)
        {
            var obj = new GameObject(name);
            SceneManager.MoveGameObjectToScene(obj, parent.gameObject.scene);
            Undo.RegisterCreatedObjectUndo(obj, "创建 " + name);
            obj.transform.SetParent(parent, false);
            return obj;
        }

        static T[] Components<T>(Scene scene) where T : Component =>
            scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();

        static T Single<T>(Scene scene) where T : Component => Components<T>(scene).Single();
        static Transform Named(Scene scene, string name) => Components<Transform>(scene).Single(t => t.name == name);
        static T RequiredAsset<T>(string path) where T : UnityEngine.Object =>
            AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new InvalidOperationException("缺少正式资源：" + path);

        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        static void RequireEditMode()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("请退出 Play 后安装二章设备教学。");
        }

        static void RequireLoadedScene(Scene scene)
        {
            RequireEditMode();
            Require(scene.IsValid() && scene.isLoaded && !string.IsNullOrEmpty(scene.path), "请先打开已保存的二章场景。");
        }

        static void VerifySourceHash(Scene scene)
        {
            Require(!scene.isDirty, "要按源场景哈希装配，请传入重新载入的干净二章场景。");
            using var sha256 = SHA256.Create();
            using var file = File.OpenRead(scene.path);
            var actual = BitConverter.ToString(sha256.ComputeHash(file)).Replace("-", "");
            Require(string.Equals(actual, SourceSha256, StringComparison.OrdinalIgnoreCase),
                $"二章场景基线 SHA-256 不符：{scene.path} ({actual})");
        }
    }
}
