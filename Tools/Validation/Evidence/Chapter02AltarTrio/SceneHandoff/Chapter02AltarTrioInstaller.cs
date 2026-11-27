using System;
using System.Linq;
using NarutoVR.SealCombat.Enemies;
using NarutoVR.SealCombat.Rpg;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>供二章场景负责人在 Editor 手动运行，在既有祭坛房间登记缺少的两种普通敌人。</summary>
public static class Chapter02AltarTrioInstaller
{
    const string ScenePath = "Assets/Scene/Chapter02_SuspendedPath.unity";
    const string NavAsset = "Assets/Art/Environment/SuspendedPath/Navigation/Chapter02AltarStoneCarrier_NavMesh.asset";
    const string Carrier = "stone-carrier", Melee = "canjiou", Ranged = "zhihuocanji";

    [MenuItem("Tools/NarutoVR/第二章断衡悬道/补齐祭坛三敌")]
    static void Install()
    {
        bool added = Apply(SceneManager.GetSceneByPath(ScenePath));
        Debug.Log(added ? "祭坛登记已更新，请审阅后保存场景。" : "祭坛三敌已就绪。");
    }

    static void Require(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException("祭坛三敌接线停止。" + message);
    }

    /// <summary>返回是否新增登记。保留既有点、顺序和朝向；遇到编成冲突时不修改场景。</summary>
    public static bool Apply(Scene scene)
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode && scene.IsValid() && scene.isLoaded && scene.path == ScenePath,
            "请在编辑模式打开二章场景。");
        var roots = scene.GetRootGameObjects();
        var altar = roots.Where(o => o.name.StartsWith("02 · 祭坛战场", StringComparison.Ordinal)).Select(o => o.transform).SingleOrDefault();
        Require(altar && altar.gameObject.activeInHierarchy, "找不到现行祭坛根。");
        var line = altar.Find("首次接敌线");
        var post = altar.Find("背石起始位")?.GetComponent<StoneCarrierPost>();
        Require(line && post && post.isActiveAndEnabled && post.basket && post.basket.slot, "接敌线、原背石出生点或篮槽引用缺失。");

        var rooms = roots.SelectMany(o => o.GetComponentsInChildren<CombatRoomEncounter>(true)).ToArray();
        var room = rooms.Where(r => r.transform.IsChildOf(altar) || r.area && r.area.transform.IsChildOf(altar) ||
            (r.spawnPoints ?? Array.Empty<CombatRoomEncounter.SpawnPoint>()).Any(s => s != null && s.point && s.point.IsChildOf(altar))).SingleOrDefault();
        Require(room && room.isActiveAndEnabled && room.rpg && room.rpg.roomControlsEncounter && room.area && room.area.enabled &&
            room.area.isTrigger && room.area.gameObject.activeInHierarchy && room.area.transform.IsChildOf(altar) &&
            room.enemyManager && room.enemyManager.isActiveAndEnabled && room.enemyManager.catalog &&
            (room.enemies == null || room.enemies.Length == 0), "请先确认祭坛唯一的租约房间、触发盒与敌人服务。");
        var entries = room.spawnPoints ?? Array.Empty<CombatRoomEncounter.SpawnPoint>();
        Require(entries.All(s => s != null && s.point && s.point.IsChildOf(altar) && s.point.gameObject.activeInHierarchy &&
            !s.isSummoned && (s.enemyId == Carrier || s.enemyId == Melee || s.enemyId == Ranged)) &&
            entries.Select(s => s.enemyId).Distinct().Count() == entries.Length &&
            entries.Select(s => s.point).Distinct().Count() == entries.Length, "已有缺失、重复、跨区、召唤或其它敌人登记。");
        Require(entries.Any(s => s.enemyId == Carrier && s.point == post.transform), "原工奴与背石点的登记必须保留。");
        foreach (string id in new[] { Carrier, Melee, Ranged })
        {
            Require(room.enemyManager.catalog.TryGet(id, out var definition) && definition && !definition.summoning &&
                (id == Carrier ? definition.stoneCarrier && definition.stoneCarrier.stone : !definition.stoneCarrier),
                "当前敌人目录缺少对应的角色或石块配置：" + id);
        }
        var nav = roots.SelectMany(o => o.GetComponentsInChildren<NavMeshSurface>(true))
            .Where(s => s.isActiveAndEnabled && s.navMeshData && AssetDatabase.GetAssetPath(s.navMeshData) == NavAsset).SingleOrDefault();
        Require(nav, "场景缺少启用的原祭坛地面导航。");
        var filter = new NavMeshQueryFilter { agentTypeID = nav.agentTypeID, areaMask = NavMesh.AllAreas };
        var melee = entries.FirstOrDefault(s => s.enemyId == Melee);
        var ranged = entries.FirstOrDefault(s => s.enemyId == Ranged);
        const string meleeName = "普通近战出生点";
        const string rangedName = "掷火残机出生点";
        // 已登记的位置优先。无登记时可复用场景作者预先摆放的同名空节点。
        Transform Locate(CombatRoomEncounter.SpawnPoint entry, string marker)
        {
            if (entry != null) return entry.point;
            return altar.Cast<Transform>().Where(t => t.name == marker).SingleOrDefault();
        }
        var meleePoint = Locate(melee, meleeName);
        var rangedPoint = Locate(ranged, rangedName);
        Vector3 Plan(Transform point, Vector3 relativeToLine)
        {
            if (point) Require(point.gameObject.activeInHierarchy && point != post.transform && !point.GetComponent<StoneCarrierPost>(),
                "战斗敌人不能占用工奴点或已停用的点。");
            var candidate = point ? point.position : line.TransformPoint(relativeToLine);
            var inBox = room.area.transform.InverseTransformPoint(candidate) - room.area.center;
            var half = room.area.size * .5f;
            Require(Mathf.Abs(inBox.x) <= half.x && Mathf.Abs(inBox.y) <= half.y && Mathf.Abs(inBox.z) <= half.z,
                "出生点不在祭坛接敌范围内。");
            Require(NavMesh.SamplePosition(candidate, out var hit, .45f, filter), "出生点离开了原祭坛地面。");
            Require(NavMesh.SamplePosition(post.transform.position, out var start, .45f, filter), "原背石出生点离开了祭坛地面。");
            var path = new NavMeshPath();
            Require(NavMesh.CalculatePath(start.position, hit.position, filter, path) && path.status == NavMeshPathStatus.PathComplete,
                "战斗出生点与工奴所在的祭坛地面不连通。");
            return point ? point.position : hit.position;
        }
        Vector3 meleePosition = Plan(meleePoint, new Vector3(3, 0, -3));
        Vector3 rangedPosition = Plan(rangedPoint, new Vector3(8, 0, 4));
        Require(Vector3.Distance(meleePosition, rangedPosition) > .05f &&
            Vector3.Distance(meleePosition, post.transform.position) > .05f && Vector3.Distance(rangedPosition, post.transform.position) > .05f,
            "两名战斗敌人与工奴必须有各自的出生位置。");
        if (melee != null && ranged != null) return false;

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        const string operation = "补齐二章祭坛三敌出生登记";
        Undo.SetCurrentGroupName(operation);
        try
        {
            var result = entries.ToList();
            void Add(string id, string marker, Transform point, Vector3 position)
            {
                if (!point)
                {
                    var node = new GameObject(marker);
                    Undo.RegisterCreatedObjectUndo(node, operation);
                    Undo.SetTransformParent(node.transform, altar, operation);
                    point = node.transform;
                    var direction = Vector3.ProjectOnPlane(line.position - position, Vector3.up);
                    Undo.RecordObject(point, operation);
                    point.SetPositionAndRotation(position, Quaternion.LookRotation(direction, Vector3.up));
                }
                result.Add(new CombatRoomEncounter.SpawnPoint { enemyId = id, point = point, isSummoned = false });
            }
            if (melee == null) Add(Melee, meleeName, meleePoint, meleePosition);
            if (ranged == null) Add(Ranged, rangedName, rangedPoint, rangedPosition);
            Undo.RecordObject(room, operation);
            room.spawnPoints = result.ToArray();
            PrefabUtility.RecordPrefabInstancePropertyModifications(room);
            EditorUtility.SetDirty(room);
            EditorSceneManager.MarkSceneDirty(scene);
            Undo.CollapseUndoOperations(group);
            return true;
        }
        catch
        {
            Undo.RevertAllDownToGroup(group);
            throw;
        }
    }
}
