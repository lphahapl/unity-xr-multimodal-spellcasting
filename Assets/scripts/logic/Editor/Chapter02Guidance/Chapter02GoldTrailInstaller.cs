using System;
using System.Linq;
using NarutoVR.SealCombat.Rpg;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Leg = NarutoVR.SealCombat.Rpg.Chapter02GoldTrail.Leg;

namespace NarutoVR.SealCombat.Editor
{
    public static class Chapter02GoldTrailInstaller
    {
        const string ScenePath = "Assets/Scene/Chapter02_SuspendedPath.unity";
        const string GoldMaterial = "Assets/Materials/Chapter01/QuestTrailGold.mat";

        const string NearLanding = "通路符近岸中心";
        const string FarLanding = "通路符对岸中心";

        static bool MatchesBridgeRoute(QuestGuideRoute route, Transform[] original)
        {
            if (!route || route.points == null || original.Length != 4) return false;
            var near = route.transform.Find(NearLanding);
            var far = route.transform.Find(FarLanding);
            if (!near && !far) return route.points.SequenceEqual(original);
            return near && far && route.points.SequenceEqual(
                new[] { original[0], original[1], near, far, original[2], original[3] });
        }

        // Existing scenes can apply this change alone. Only the route and its two child anchors are written.
        public static bool ApplyPassageRoute(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded || scene.path != ScenePath || Application.isPlaying)
                throw new InvalidOperationException("请传入已加载的二章编辑场景。");
            var roots = scene.GetRootGameObjects();
            var guide = roots.SelectMany(root => root.GetComponentsInChildren<Chapter02GoldTrail>(true)).Single();
            var bridge = guide.bridge;
            if (!bridge || !(bridge.drawingStage is Chapter02PassageStage stage) || !bridge.repairedDeck ||
                !guide.player || guide.player.StageAt(1) != stage || guide.legs == null ||
                guide.legs.Length <= (int)Leg.BridgeToAltar)
                throw new InvalidOperationException("修桥后的浮光需要二章通路符阶段和对应的真实桥面。");
            var hints = guide.legs[(int)Leg.BridgeToAltar];
            var route = hints?.route;
            var encounter = guide.transform.Find("入祭坛守备区域");
            var altarLine = roots.SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Single(t => t.name == "首次接敌线");
            var original = new[] { guide.player.CheckpointAt(0).transform, guide.player.CheckpointAt(1).transform,
                altarLine, encounter };
            if (!encounter || !route || route.transform.parent != guide.transform ||
                route.name != nameof(Leg.BridgeToAltar) || hints.destination != encounter ||
                !MatchesBridgeRoute(route, original))
                throw new InvalidOperationException("修桥后路线并非预期的旧四点或已迁移的六点。");

            // PassageGlyphRoad uses these same deck-local positions as its walkable centerline.
            var deck = bridge.repairedDeck.transform;
            var nearPosition = deck.TransformPoint(stage.nearLanding);
            var farPosition = deck.TransformPoint(stage.farLanding);
            if ((nearPosition - farPosition).sqrMagnitude < .0001f)
                throw new InvalidOperationException("二章通路符两岸落点重合。");
            var near = route.transform.Find(NearLanding);
            var far = route.transform.Find(FarLanding);
            if (near && (near.position - nearPosition).sqrMagnitude < 1e-8f &&
                (far.position - farPosition).sqrMagnitude < 1e-8f) return false;
            if (!near)
            {
                near = ObjectFactory.CreateGameObject(scene, HideFlags.None, NearLanding).transform;
                near.SetParent(route.transform, false);
                far = ObjectFactory.CreateGameObject(scene, HideFlags.None, FarLanding).transform;
                far.SetParent(route.transform, false);
                route.points = new[] {original[0], original[1], near, far, original[2], original[3]};
                EditorUtility.SetDirty(route);
            }
            near.position = nearPosition;
            far.position = farPosition;
            EditorUtility.SetDirty(near);
            EditorUtility.SetDirty(far);
            EditorSceneManager.MarkSceneDirty(scene);
            return true;
        }

        public static void Install()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                if (scene.isDirty) throw new InvalidOperationException("安装前请保存当前场景。");
                scene = EditorSceneManager.OpenScene(ScenePath);
            }
            if (Apply(scene) && !EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("二章浮光场景保存失败。");
        }

        public static bool Apply(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded || scene.path != ScenePath || Application.isPlaying)
                throw new InvalidOperationException("请传入已加载的二章编辑场景。");
            void Require(bool condition, string label)
            {
                if (!condition) throw new InvalidOperationException("二章浮光接线与预期不符：" + label);
            }
            T[] All<T>() where T : Component => scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
            Transform Named(string name) => All<Transform>().Single(t => t.name == name);
            var previous = All<Chapter02GoldTrail>().SingleOrDefault();
            var player = All<LinearRpgController>().Single();
            var flow = player.chapter as ObjectiveChapterFlow;
            var bridge = All<Chapter02BridgeEquipment>().Single();
            var workshop = All<WorkshopTenonObjective>().Single();
            var baskets = All<Chapter02BasketMotion>().Single();
            var lever = All<Chapter02BasketBControl>().Single();
            var altar = All<CombatRoomEncounter>().Single(r => r.spawnPoints.Any(p => p != null && p.enemyId == "stone-carrier"));
            var material = AssetDatabase.LoadAssetAtPath<Material>(GoldMaterial);
            if (!flow || !material || !lever.leverGrip || player.StageAt(0) != bridge.approachStage ||
                player.StageAt(1) != bridge.drawingStage) throw new InvalidOperationException("章节阶段、实物分控杆或首章金色材质不匹配。");

            var entry = player.CheckpointAt(0).transform;
            var stand = player.CheckpointAt(1).transform;
            var altarLine = Named("首次接敌线");
            var topDoor = Named("A 上层篮口");
            var middleDoor = Named("A 中层篮口");
            var aLanding = baskets.middleCheckpoint.transform;
            var aPier = Named("A 栈道中控端");
            var aTurn = Named("A 岸转向位");
            var hub = Named("中控操作位");
            var leftTurn = Named("左行转向位");
            var leftPier = Named("左栈桥中控端");
            var workshopDoor = Named("工坊院口");
            var pickup = Named("主榫取用站位");
            var bTurn = Named("B 岸转向位");
            var bPier = Named("B 栈道中控端");
            var bDoor = Named("B 中层篮口");
            var bridgeStrip = Named("桥后导引嵌条").GetComponent<MeshRenderer>();
            var workshopStrip = Named("铜边嵌尺 · 中控至工坊").gameObject;
            if (!bridgeStrip || workshopStrip.GetComponentsInChildren<Collider>(true).Length != 0 ||
                workshopStrip.GetComponentsInChildren<MonoBehaviour>(true).Length != 0)
                throw new InvalidOperationException("旧导引嵌条的用途已变化，请复核其组件。");

            const string geometryPack = "Assets/Art/Environment/SuspendedPath/SuspendedPathGeometry.asset";
            var sourceMeshes = AssetDatabase.LoadAllAssetsAtPath(geometryPack).OfType<Mesh>().ToArray();
            var banks = new[] { "A", "B" }.Select(bank =>
            {
                var bankNode = Named(bank + " 岸接驳栈道");
                var filter = bankNode.GetComponent<MeshFilter>();
                var original = sourceMeshes.Single(mesh => mesh.name == bankNode.name);
                var visual = AssetDatabase.LoadAssetAtPath<Mesh>(
                    "Assets/Art/Environment/SuspendedPath/CH02Bank" + bank + "WithoutFloorGuide.asset");
                var materials = bankNode.GetComponent<MeshRenderer>().sharedMaterials;
                Require(filter && visual && (filter.sharedMesh == original || filter.sharedMesh == visual) &&
                    visual.vertexCount == original.vertexCount && visual.subMeshCount == 6 && original.subMeshCount == 6 &&
                    original.GetIndexCount(2) == 540 && visual.GetIndexCount(2) == 0 && materials.Length == 6 &&
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(materials[2], out string guid, out long id) &&
                    guid == "9269cec67ee957643b90e6ec3eb180ee" && id == 1518654377004692586L &&
                    Enumerable.Range(0, 6).All(i => i == 2 || visual.GetIndexCount(i) == original.GetIndexCount(i)),
                    bankNode.name + " 旧青色指路刻线");
                return (filter, visual);
            }).ToArray();
            bool RemoveBankFloorGuides()
            {
                bool changed = false;
                foreach (var (filter, visual) in banks)
                {
                    if (filter.sharedMesh == visual) continue;
                    filter.sharedMesh = visual;
                    EditorUtility.SetDirty(filter);
                    if (PrefabUtility.IsPartOfPrefabInstance(filter))
                        PrefabUtility.RecordPrefabInstancePropertyModifications(filter);
                    changed = true;
                }
                return changed;
            }

            bool verify = previous != null;
            var root = verify ? previous.gameObject : new GameObject("二章 · 金色浮光指路");
            if (verify) Require(root.transform.parent == flow.transform && root.activeInHierarchy, "场景节点");
            else root.transform.SetParent(flow.transform, false);

            Transform Point(string label, Vector3 world)
            {
                if (verify)
                {
                    var existing = root.transform.Find(label);
                    Require(existing && (existing.position - world).sqrMagnitude < .0001f, label);
                    return existing;
                }
                var point = new GameObject(label).transform;
                point.SetParent(root.transform, false);
                point.position = world;
                return point;
            }
            Transform Offset(string label, Transform origin, float dx, float dz) =>
                Point(label, origin.position + new Vector3(dx, 0f, dz));
            var guide = verify ? previous : root.AddComponent<Chapter02GoldTrail>();
            if (verify)
            {
                Require(guide.enabled && guide.player == player && guide.bridge == bridge && guide.workshop == workshop &&
                    guide.baskets == baskets && guide.lever == lever && guide.altar == altar &&
                    guide.legs != null && guide.legs.Length == (int)Leg.BoardB + 1, "玩家、机关、房间及路线数量");
            }
            else
            {
                guide.player = player;
                guide.bridge = bridge;
                guide.workshop = workshop;
                guide.baskets = baskets;
                guide.lever = lever;
                guide.altar = altar;
                guide.legs = new QuestVisualHints[(int)Leg.BoardB + 1];
            }
            void SetHints(Leg leg, QuestGuideRoute route, Transform destination)
            {
                if (verify)
                {
                    var hints = guide.legs[(int)leg];
                    Require(hints != null && hints.route == route && hints.destination == destination &&
                        hints.trailDelay == 0f, leg.ToString());
                }
                else guide.legs[(int)leg] = new QuestVisualHints {route = route, destination = destination};
            }
            QuestGuideRoute Route(Leg leg, params Transform[] points)
            {
                var node = Point(leg.ToString(), root.transform.position).gameObject;
                var route = verify ? node.GetComponent<QuestGuideRoute>() : node.AddComponent<QuestGuideRoute>();
                if (verify)
                {
                    bool same = route && route.isActiveAndEnabled && (leg == Leg.BridgeToAltar ?
                        MatchesBridgeRoute(route, points) : route.points.SequenceEqual(points));
                    Require(same, leg.ToString());
                }
                else route.points = points;
                SetHints(leg, route, points[points.Length - 1]);
                return route;
            }
            var encounter = Offset("入祭坛守备区域", altarLine, 0f, -.85f);
            Route(Leg.BridgeApproach, entry, stand);
            Route(Leg.BridgeToAltar, entry, stand, altarLine, encounter);
            var altarShoulder = Offset("祭坛侧行", altarLine, 1.55f, -4f);
            var topApproach = Offset("A 篮门前", topDoor, 0f, .30f);
            Route(Leg.AltarToA, encounter, altarShoulder, Offset("镇石侧行", topDoor, 1.55f, 2.30f),
                topApproach, Offset("上层乘员侧板", topDoor, .83f, -.49f));
            var eastSide = Offset("中层右侧板", middleDoor, -.52f, 0f);
            var westFront = Offset("中层左侧板", middleDoor, -2.48f, 1.05f);
            var eastFront = Offset("中层右侧板前段", middleDoor, -.52f, 1.05f);
            var westBack = Offset("中层左侧板后段", middleDoor, -2.48f, -.72f);
            var eastBack = Offset("中层右侧板后段", middleDoor, -.52f, -.72f);
            Route(Leg.AExitWest, westFront, westBack, eastBack, eastSide, middleDoor, aLanding);
            Route(Leg.AExitEast, eastFront, eastSide, middleDoor, aLanding);
            Route(Leg.AToHub, middleDoor, aLanding, aPier, aTurn, hub);
            var inside = Offset("工坊院门内侧", workshopDoor, 0f, .9f);
            var corridor = Route(Leg.PickupTenon, hub, leftTurn, leftPier, workshopDoor, inside, pickup);
            SetHints(Leg.HubToWorkshop, corridor, inside);
            SetHints(Leg.ReturnTenon, corridor, hub);
            var droppedFloor = Point("掉落主榫所在路面", pickup.position);
            var droppedJoin = Point("拾回主榫转向点", pickup.position);
            if (verify) Require(guide.droppedFloor == droppedFloor && guide.droppedJoin == droppedJoin, "掉落主榫");
            else
            {
                guide.droppedFloor = droppedFloor;
                guide.droppedJoin = droppedJoin;
            }
            Route(Leg.DroppedTenon, guide.droppedJoin, guide.droppedFloor);
            var leverStand = lever.leverGrip.position + Vector3.forward * .57f;
            leverStand.y = bDoor.position.y;
            var leverFloor = Point("分控杆站位", leverStand);
            var bMouth = Offset("B 篮接驳口", bDoor, -.55f, 0f);
            Route(Leg.HubToLeverB, hub, bTurn, bPier, bMouth, leverFloor);
            Route(Leg.BoardB, leverFloor, bMouth, bDoor, Offset("B 篮乘员区", bDoor, .64f, 0f));

            if (verify)
            {
                var current = root.GetComponent<CourtyardTaskGuide>();
                Require(current && current.isActiveAndEnabled && current.chapter == flow && current.guideMaterial == material &&
                    current.showTrail && !current.showGestures && Mathf.Approximately(current.trailHeight, .17f), "首章浮光样式");
                Require(!bridgeStrip.enabled && !workshopStrip.activeSelf, "重复静态导航嵌条");
                bool centered = ApplyPassageRoute(scene);
                if (!RemoveBankFloorGuides()) return centered;
                EditorSceneManager.MarkSceneDirty(scene);
                return true;
            }

            var display = root.AddComponent<CourtyardTaskGuide>();
            display.chapter = flow;
            display.guideMaterial = material;
            display.trailHeight = .17f;
            display.showTrail = true;
            display.showGestures = false;
            ApplyPassageRoute(scene);
            RemoveBankFloorGuides();
            bridgeStrip.enabled = false;
            workshopStrip.SetActive(false);
            EditorUtility.SetDirty(bridgeStrip);
            EditorUtility.SetDirty(workshopStrip);
            if (PrefabUtility.IsPartOfPrefabInstance(workshopStrip))
                PrefabUtility.RecordPrefabInstancePropertyModifications(workshopStrip);
            EditorSceneManager.MarkSceneDirty(scene);
            return true;
        }
    }
}
