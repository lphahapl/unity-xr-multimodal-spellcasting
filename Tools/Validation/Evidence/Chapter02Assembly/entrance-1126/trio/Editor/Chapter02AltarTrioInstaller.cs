using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using NarutoVR.SealCombat.Enemies;
using NarutoVR.SealCombat.Rpg;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace NarutoVR.SealCombat.Editor
{
    public static class Chapter02AltarTrioInstaller
    {
        const string ScenePath = "Assets/Scene/Chapter02_SuspendedPath.unity";
        const string SourceHash = "a5842a97dadeed8482750c2a95e8b810d39f42848f2bc7212e24a94952a4f13c";
        const string NavPath = "Assets/Art/Environment/SuspendedPath/Navigation/Chapter02AltarStoneCarrier_NavMesh.asset";
        const string Melee = "canjiou", Ranged = "zhihuocanji", Carrier = "stone-carrier";
        const string MeleeMark = "近战护卫 · 地面出生位", RangedMark = "掷火残机 · 地面射位";
        static readonly Vector3 MeleeLocal = new Vector3(15.5f, 0f, 8.2f);
        static readonly Vector3 RangedLocal = new Vector3(18f, 0f, 15.1f);

        public static void Install() => RunBatch(InstallCandidate);
        public static void Verify() => RunBatch(() => Verify(OpenIsolatedScene()));

        static void RunBatch(Action action)
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("请从隔离 Unity 批处理运行。");
            try
            {
                action();
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                EditorApplication.Exit(1);
            }
        }

        static void InstallCandidate()
        {
            var scene = OpenIsolatedScene();
            var room = FindRoom(scene, out var altar, out var carrier);
            if (room.spawnPoints.Length == 3)
            {
                Verify(scene);
                return;
            }
            Require(SceneHash() == SourceHash, "原始场景不是已验证的自由画板、腕部 UI 及修桥候选。");
            Require(room.spawnPoints.Length == 1 && room.spawnPoints[0].enemyId == Carrier &&
                room.spawnPoints[0].point == carrier.transform && !room.spawnPoints[0].isSummoned, "原祭坛工奴出生点已变更。");
            Require(!altar.Find(MeleeMark) && !altar.Find(RangedMark), "祭坛已包含未登记的护卫出生点。");
            CheckSites(scene, altar, carrier, room);
            var melee = AddMark(altar, MeleeMark, MeleeLocal, room.transform.position);
            var ranged = AddMark(altar, RangedMark, RangedLocal, room.transform.position);
            room.spawnPoints = new[]
            {
                room.spawnPoints[0],
                new CombatRoomEncounter.SpawnPoint { enemyId = Melee, point = melee, isSummoned = false },
                new CombatRoomEncounter.SpawnPoint { enemyId = Ranged, point = ranged, isSummoned = false }
            };
            SetQuietPreview(room);
            EditorUtility.SetDirty(room);
            EditorSceneManager.MarkSceneDirty(scene);
            Require(EditorSceneManager.SaveScene(scene), "无法保存隔离场景。");
            Verify(OpenIsolatedScene());
        }

        static void Verify(Scene scene)
        {
            var room = FindRoom(scene, out var altar, out var carrier);
            var preview = new SerializedObject(room).FindProperty("showEnemiesBeforeEngagement");
            Require(preview != null && preview.boolValue, "读回后缺少桥前静默展示。");
            var rows = room.spawnPoints;
            Require(rows.Length == 3 && rows.All(row => row != null && row.point && !row.isSummoned) &&
                rows[0].enemyId == Carrier && rows[0].point == carrier.transform &&
                rows[1].enemyId == Melee && rows[1].point == altar.Find(MeleeMark) &&
                rows[2].enemyId == Ranged && rows[2].point == altar.Find(RangedMark),
                "读回后的三敌 ID、场景引用或租约类别不符。");
            Require(Vector3.Distance(rows[1].point.localPosition, MeleeLocal) < .001f &&
                Vector3.Distance(rows[2].point.localPosition, RangedLocal) < .001f, "护卫地面位置已变更。");
            for (int i = 1; i < rows.Length; i++)
            {
                var mark = rows[i].point;
                var toEntry = Vector3.ProjectOnPlane(room.transform.position - mark.position, Vector3.up);
                Require(mark.GetComponents<Component>().Length == 1 && mark.childCount == 0 &&
                    Vector3.Angle(mark.forward, toEntry) < .1f, "出生点须为空间标记并朝向接敌线。");
            }
            CheckSites(scene, altar, carrier, room);
            Debug.Log("CH02_ALTAR_TRIO_INSTALLED 原祭坛共三名非召唤敌人，桥前静默，原区域接敌。" +
                $" 工奴={carrier.transform.position:F3} 近战={rows[1].point.position:F3} 掷火={rows[2].point.position:F3}");
        }

        static void SetQuietPreview(CombatRoomEncounter room)
        {
            var serialized = new SerializedObject(room);
            var prop = serialized.FindProperty("showEnemiesBeforeEngagement");
            Require(prop != null && prop.propertyType == SerializedPropertyType.Boolean, "先同步房间的桥前静默展示运行时。");
            prop.boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static Scene OpenIsolatedScene()
        {
            string root = Path.GetFullPath(Application.dataPath).Replace('\\', '/');
            Require(Application.isBatchMode && root.StartsWith("D:/Temp/", StringComparison.OrdinalIgnoreCase),
                "本安装器只写入 D:/Temp 内的隔离工程。");
            return EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        static CombatRoomEncounter FindRoom(Scene scene, out Transform altar, out StoneCarrierPost carrier)
        {
            altar = scene.GetRootGameObjects().Select(go => go.transform)
                .Single(t => t.name.StartsWith("02 · 祭坛战场", StringComparison.Ordinal));
            var line = altar.Find("首次接敌线");
            carrier = altar.Find("背石起始位").GetComponent<StoneCarrierPost>();
            var room = line.GetComponentInChildren<CombatRoomEncounter>(true);
            var target = altar.Find("示例落石");
            Require(room && room.enabled && room.gameObject.activeInHierarchy && room.area && room.area.enabled &&
                room.area.isTrigger && room.rpg && room.rpg.roomControlsEncounter && room.enemyManager && room.enemyManager.catalog &&
                room.transform.parent == line && room.area.transform == room.transform &&
                Vector3.Distance(room.area.center, new Vector3(9.5f, 1f, 0f)) < .001f &&
                Vector3.Distance(room.area.size, new Vector3(19f, 3f, 22f)) < .001f &&
                Vector3.Distance(line.localPosition, new Vector3(9f, 0f, 11f)) < .001f, "原接敌区域或玩家引用不符。");
            Require(carrier && carrier.basket && carrier.basket.slot && carrier.basket.passengerVolume && target &&
                carrier.patrolPoints.Length == 2 && carrier.patrolPoints[0] == target && carrier.patrolPoints[1] == carrier.transform &&
                Vector3.Distance(carrier.transform.localPosition, new Vector3(15f, 0f, 11f)) < .001f,
                "原工奴起始位、撤离点或镇石槽接线已变更。");
            return room;
        }

        static Transform AddMark(Transform altar, string name, Vector3 position, Vector3 face)
        {
            var mark = new GameObject(name).transform;
            mark.SetParent(altar, false);
            mark.localPosition = position;
            mark.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(face - mark.position, Vector3.up));
            return mark;
        }

        static void CheckSites(Scene scene, Transform altar, StoneCarrierPost carrier, CombatRoomEncounter room)
        {
            Physics.SyncTransforms();
            var nav = AssetDatabase.LoadAssetAtPath<NavMeshData>(NavPath);
            Require(nav && room.rpg.locomotion && room.rpg.locomotion.head, "缺少祭坛导航或玩家头部。");
            var near = altar.TransformPoint(MeleeLocal);
            var far = altar.TransformPoint(RangedLocal);
            var entry = altar.TransformPoint(new Vector3(9.25f, 0f, 11f));
            var board = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<Transform>(true))
                .Single(t => t.name == "画符站位");
            var camera = room.rpg.locomotion.head.GetComponent<Camera>();
            Require(camera, "原玩家观察点缺少 Camera。");
            foreach (var point in new[] { near, carrier.transform.position, far })
            {
                var sight = point + Vector3.up * 1.2f - (board.position + Vector3.up * 1.2f);
                Require(Vector3.Angle(camera.transform.forward, sight) < camera.fieldOfView * .5f,
                    "出生点在默认桥头视场以外。");
            }

            NavMeshDataInstance temporary = default;
            try
            {
                if (!NavMesh.SamplePosition(carrier.transform.position, out _, .4f, NavMesh.AllAreas))
                    temporary = NavMesh.AddNavMeshData(nav);
                ValidateSite(room, Melee, near, entry);
                ValidateSite(room, Ranged, far, entry);
            }
            finally
            {
                if (temporary.valid) temporary.Remove();
            }
        }

        static void ValidateSite(CombatRoomEncounter room, string id, Vector3 point, Vector3 entry)
        {
            Require(room.enemyManager.catalog.TryGet(id, out var definition) && definition && definition.prefab != null &&
                !definition.stoneCarrier && !definition.summoning, $"目录缺少正式普通敌人 {id}。");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(definition.prefab.AssetGUID));
            Require(prefab, $"无法读取 {id} 的正式 Prefab。");
            var controller = prefab.GetComponent<EnemyController>();
            var agent = prefab.GetComponent<NavMeshAgent>();
            Require(controller && agent, $"{id} 缺少生产控制器或导航代理。");
            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            Require(NavMesh.SamplePosition(point, out var ground, .4f, filter) &&
                Vector3.Distance(point, ground.position) < .2f, $"{id} 脚下缺少同代理类型的地面导航。");
            Require(NavMesh.SamplePosition(entry, out var nearLine, .4f, filter), "接敌线地面导航缺失。");
            var path = new NavMeshPath();
            Require(NavMesh.CalculatePath(ground.position, nearLine.position, filter, path) &&
                path.status == NavMeshPathStatus.PathComplete, $"{id} 与祭坛接敌区不连通。");
            Require(Physics.Raycast(point + Vector3.up * .5f, Vector3.down, out var floor, .8f, ~0, QueryTriggerInteraction.Ignore) &&
                Mathf.Abs(floor.point.y - point.y) < .12f && floor.normal.y > .9f, $"{id} 缺少实体站立面。");
            Require(!Physics.CheckCapsule(floor.point + Vector3.up * (agent.radius + .07f),
                floor.point + Vector3.up * Mathf.Max(agent.radius + .07f, agent.height - agent.radius),
                agent.radius, ~0, QueryTriggerInteraction.Ignore), $"{id} 出生位置有实体占位。");
            Require(room.area.bounds.Contains(point + Vector3.up * .1f), $"{id} 出生点离开祭坛战区。");
            if (id != Ranged) return;
            var attack = prefab.GetComponent<EnemyRangedAttack>();
            float range = Vector3.Distance(point, entry);
            Require(attack && attack.IsConfigured && range < Mathf.Min(controller.attackDistance, controller.detectionDistance),
                "正式掷火手或射程未覆盖正面入场口。");
            var eye = ground.position + Vector3.up * controller.eyeHeight;
            var head = entry + Vector3.up * 1.2f;
            var aim = entry + Vector3.up * .1f;
            Require(!Physics.Linecast(eye, head, controller.obstructionMask, QueryTriggerInteraction.Ignore) &&
                !Physics.Linecast(eye, aim, controller.obstructionMask, QueryTriggerInteraction.Ignore),
                "掷火手观察线或至接敌地面的射线受到静态遮挡。");
            Debug.Log($"CH02_ALTAR_FIRE_LANE {id} point={point:F3} nav={ground.position:F3} floor={floor.collider.name} range={range:F2}m");
        }

        static string SceneHash()
        {
            using var hash = SHA256.Create();
            using var source = File.OpenRead(ScenePath);
            return BitConverter.ToString(hash.ComputeHash(source)).Replace("-", "").ToLowerInvariant();
        }

        static void Require(bool success, string message)
        {
            if (!success) throw new InvalidOperationException(message);
        }
    }
}
