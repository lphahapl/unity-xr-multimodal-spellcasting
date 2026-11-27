using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NarutoVR.SealCombat.Enemies;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>只在二章独立副本启动。原场景实测后，根据正式工坊碰撞体临时烘焙，并调用实际精英和随从逻辑。</summary>
[InitializeOnLoad]
public static class Chapter02EliteNavigation
{
    const string ScenePath = "Assets/Scene/Chapter02_SuspendedPath.unity";
    const string CatalogPath = "Assets/ScriptableObjects/敌人目录配置/本关怪物目录.asset";
    const string Pending = "Chapter02EliteNavigation.Pending";
    const string Persisted = "Chapter02EliteNavigation.Persisted";
    const string GroundAsset = "Assets/Art/Environment/SuspendedPath/Navigation/WorkshopCourtyardGround.asset";
    static Chapter02EliteNavigation() => EditorApplication.update += WhenPlaying;

    public static void Run() => StartCheck(false);
    public static void RunPersisted() => StartCheck(true);

    static void StartCheck(bool loadGround)
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("仅用于隔离批处理");
        EditorSceneManager.OpenScene(ScenePath);
        SessionState.SetBool(Persisted, loadGround);
        SessionState.SetBool(Pending, true);
        EditorApplication.EnterPlaymode();
    }

    static async void WhenPlaying()
    {
        if (!Application.isPlaying || EditorApplication.isCompiling || !SessionState.GetBool(Pending, false)) return;
        SessionState.EraseBool(Pending);
        bool loadGround = SessionState.GetBool(Persisted, false);
        SessionState.EraseBool(Persisted);
        try { await Verify(loadGround); Log("PASS"); EditorApplication.Exit(0); }
        catch (Exception error) { Debug.LogError("CH02_ELITE_FAIL " + error); EditorApplication.Exit(1); }
    }

    static void Log(string value) => Debug.Log("CH02_ELITE " + value);
    static void Require(bool value, string message)
    {
        if (!value) throw new Exception(message);
        Log(message);
    }
    static string P(Vector3 p) => p.ToString("F3");

    static Transform Ground(Transform yard) => yard.Cast<Transform>().Single(t => t.name.StartsWith("工坊院场 · 地面与崖基"));

    static NavMeshData BuildGround(Transform ground)
    {
        Physics.SyncTransforms();
        var sources = new List<NavMeshBuildSource>();
        var yardBounds = ground.GetComponent<BoxCollider>().bounds;
        var region = new Bounds(yardBounds.center, new Vector3(yardBounds.size.x + 1, 5, yardBounds.size.z + 1));
        NavMeshBuilder.CollectSources(region, ~0, NavMeshCollectGeometry.PhysicsColliders, 0, new List<NavMeshBuildMarkup>(), sources);
        int floors = 0;
        for (int i = 0; i < sources.Count; ++i)
        {
            var s = sources[i];
            if (s.component && s.component.transform == ground) floors++;
            else { s.area = 1; sources[i] = s; }
        }
        Require(floors == 1 && sources.Count == 38, $"取原实测 PhysicsColliders：院场地面 {floors}，其他障碍 {sources.Count - floors}，体积 {region}");
        var settings = NavMesh.GetSettingsByIndex(0);
        var mesh = NavMeshBuilder.BuildNavMeshData(settings, sources, region, Vector3.zero, Quaternion.identity);
        Require(mesh, "烘焙地面，agentType=" + settings.agentTypeID + " radius=" + settings.agentRadius);
        return mesh;
    }

    /// <summary>在包含上次实测几何的独立工程创建一次。由另一 Unity 进程在新版二章场景读取资产并验证。</summary>
    public static void BakeAsset()
    {
        if (!Application.isBatchMode || AssetDatabase.LoadMainAssetAtPath(GroundAsset))
            throw new InvalidOperationException("仅在隔离工程创建新的工坊导航资产：" + GroundAsset);
        var scene = EditorSceneManager.OpenScene(ScenePath);
        var yard = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .Single(t => t.name.StartsWith("03 · 失衡工坊战区"));
        var mesh = BuildGround(Ground(yard));
        mesh.name = "WorkshopCourtyardGround";
        AssetDatabase.CreateAsset(mesh, GroundAsset);
        AssetDatabase.SaveAssets();
        Require(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mesh, out string guid, out long id),
            "已持久化 " + GroundAsset);
        Log($"saved guid={guid} fileID={id} bounds={mesh.sourceBounds} position={P(mesh.position)} rotation={mesh.rotation}");
    }

    static async Task Verify(bool loadGround)
    {
        var scene = SceneManager.GetActiveScene();
        Require(scene.path == ScenePath, "active scene=" + scene.path);
        var all = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<Transform>(true)).ToArray();
        var yard = all.Single(t => t.name.StartsWith("03 · 失衡工坊战区"));
        var center = yard.Find("空地中央");
        var stand = yard.Find("主榫取用站位");
        var ground = Ground(yard);
        Require(center && stand && ground.GetComponent<BoxCollider>(), "原场景锚点与院场实体 center=" + P(center.position) +
            " stand=" + P(stand.position) + " floor=" + ground.name);
        int liveManagers = all.Count(t => t.GetComponent<EnemyManager>()), nativeEnemies = all.Count(t => t.GetComponent<EnemyController>());
        var markers = all.Where(t => t.name.Contains("精英") || t.name.Contains("召唤") || t.name.Contains("随从")).ToArray();
        Log($"原状 managers={liveManagers} sceneEnemies={nativeEnemies} namedEliteOrMinionMarkers={markers.Length} " +
            "markers=" + string.Join("|", markers.Select(t => t.name)) + " triangles=" + NavMesh.CalculateTriangulation().indices.Length / 3);
        Vector3 spawn = center.position;
        Vector3 direction = Vector3.ProjectOnPlane(stand.position - spawn, Vector3.up).normalized;
        Vector3 playerFloor = spawn + direction * 7f;
        var rotation = Quaternion.LookRotation(direction);
        var target = new GameObject("QA 工坊玩家替身，仅提供目标").transform;
        target.SetPositionAndRotation(playerFloor + Vector3.up * .22f, Quaternion.LookRotation(-direction));
        var host = new GameObject("QA 二章工坊生成入口");
        var manager = host.AddComponent<EnemyManager>();
        manager.catalog = AssetDatabase.LoadAssetAtPath<EnemyCatalog>(CatalogPath);
        Require(manager.catalog && manager.catalog.Validate(out _), "QA 管理器使用正式敌人目录，玩家脚下=" + P(playerFloor));
        Physics.SyncTransforms();
        NavMeshData mesh = null;
        NavMeshDataInstance data = default;
        GameObject mount = null;
        EnemyLease leader = null;
        try
        {
            bool originalSample = NavMesh.SamplePosition(spawn, out var original, manager.sampleRadius, NavMesh.AllAreas);
            Log("原状中央点采样=" + originalSample + (originalSample ? " " + P(original.position) : ""));
            try { leader = await manager.SpawnEnemyAsync("elite-canjiou", spawn, rotation, target, false); }
            catch (InvalidOperationException error) { Log("原状正式生成拒绝 " + error.Message); }
            if (loadGround) Require(leader == null && !originalSample, "读盘前检验：现场尚无工坊地面导航");
            if (leader == null)
            {
                Require(!originalSample, "原状中央缺导航，实际精英未生成");
                if (loadGround)
                {
                    var asset = AssetDatabase.LoadAssetAtPath<NavMeshData>(GroundAsset);
                    Require(asset && AssetDatabase.Contains(asset), "读盘取得持久 NavMeshData，GUID=" + AssetDatabase.AssetPathToGUID(GroundAsset));
                    int before = NavMesh.CalculateTriangulation().indices.Length / 3;
                    mount = new GameObject("QA 工坊地面导航挂点");
                    mount.SetActive(false);
                    var surface = mount.AddComponent<NavMeshSurface>();
                    surface.agentTypeID = NavMesh.GetSettingsByIndex(0).agentTypeID;
                    surface.navMeshData = asset;
                    mount.SetActive(true);
                    int added = NavMesh.CalculateTriangulation().indices.Length / 3 - before;
                    Require(added == 131 && NavMesh.SamplePosition(spawn, out var centerHit, .6f, 1) &&
                        Vector3.Distance(centerHit.position, spawn) < .2f, "挂载资产新增地面三角形=" + added);
                    surface.enabled = false;
                    Require(!NavMesh.SamplePosition(spawn, out _, .6f, 1), "禁用挂点后中央地面采样消失");
                    surface.enabled = true;
                    Require(NavMesh.SamplePosition(spawn, out var restored, .6f, 1), "重启相同挂点后采样=" + P(restored.position));
                    Log("mount world position=" + P(mount.transform.position) + " rotation=" + mount.transform.rotation + " scale=" + P(mount.transform.lossyScale));
                }
                else
                {
                    mesh = BuildGround(ground);
                    data = NavMesh.AddNavMeshData(mesh);
                    Require(data.valid && NavMesh.SamplePosition(spawn, out _, .6f, NavMesh.AllAreas),
                        "临时导航生效 triangles=" + NavMesh.CalculateTriangulation().indices.Length / 3);
                }
                leader = await manager.SpawnEnemyAsync("elite-canjiou", spawn, rotation, target, false);
            }
            Require(manager.IsCurrent(leader) && leader.Controller.Agent.isOnNavMesh, "正式普通感知入口生成精英 lease=" + leader.Id +
                " at=" + P(leader.Controller.transform.position) + " isSpawnedEnemy=" + leader.Controller.isSpawnedEnemy);
            var agent = leader.Controller.Agent;
            var filter = new NavMeshQueryFilter {agentTypeID = agent.agentTypeID, areaMask = agent.areaMask};
            var path = new NavMeshPath();
            Require(NavMesh.SamplePosition(playerFloor, out var playerGround, .6f, filter) && agent.CalculatePath(playerGround.position, path) &&
                path.status == NavMeshPathStatus.PathComplete, "精英至玩家站位有完整路径，玩家投影=" + P(playerGround.position));
            var outer = new HashSet<long>();
            var full = new HashSet<long>();
            var attack = new HashSet<long>();
            var starts = new Dictionary<long, Vector3>();
            var maximum = new Dictionary<long, float>();
            bool command = false;
            double began = EditorApplication.timeSinceStartup, nextReport = 0;
            float startTime = Time.time;
            while (EditorApplication.timeSinceStartup - began < 45 && Time.time - startTime < 17f)
            {
                command |= leader.Controller.IsCommanding;
                var children = manager.ActiveEnemies.Values.Where(e => e.EnemyId == "canjiou" && !e.Defeated).OrderBy(e => e.Id).Take(3).ToArray();
                foreach (var member in children)
                {
                    var actor = member.Controller;
                    var navigator = actor.Agent;
                    if (!starts.TryGetValue(member.Id, out var from))
                    {
                        starts.Add(member.Id, from = actor.transform.position);
                        maximum[member.Id] = 0;
                        Log("首波 lease=" + member.Id + " at=" + P(from));
                    }
                    maximum[member.Id] = Mathf.Max(maximum[member.Id], Vector3.Distance(from, actor.transform.position));
                    if (actor.Status.Contains("外圈") && navigator.isOnNavMesh && navigator.hasPath &&
                        navigator.pathStatus == NavMeshPathStatus.PathComplete)
                    {
                        if (outer.Add(member.Id)) Log("外圈完整导航 lease=" + member.Id + " point=" + P(navigator.destination));
                    }
                    if (navigator.isOnNavMesh && navigator.CalculatePath(playerGround.position, path) && path.status == NavMeshPathStatus.PathComplete)
                        full.Add(member.Id);
                    if (actor.CurrentAttack >= 0) attack.Add(member.Id);
                }
                if (EditorApplication.timeSinceStartup >= nextReport)
                {
                    nextReport = EditorApplication.timeSinceStartup + 1.5;
                    Log($"实际追击 t={Time.time - startTime:F2} leader={leader.Controller.Status} wave={children.Length} " +
                        string.Join(" | ", children.Select(c => c.Id + ":" + c.Controller.Status + "@" + P(c.Controller.transform.position))));
                }
                if (starts.Count == 3 && outer.Count == 3 && full.Count == 3 && attack.Count > 0 && maximum.Values.All(v => v > 1)) break;
                await Task.Delay(35);
            }
            Log("实测汇总 command=" + command + " full=" + string.Join(",", full) + " outer=" + string.Join(",", outer) +
                " attacks=" + string.Join(",", attack) + " maxDisplacement=" + string.Join(",", maximum.Select(pair => pair.Key + "=" + pair.Value.ToString("F2"))));
            Require(command && starts.Count == 3 && full.Count == 3, "普通感知进入指挥，首波三只均有正式租约及通往玩家的完整路径");
            Require(outer.Count == 3 && maximum.Values.All(v => v > 1), "三只随从按真实 Update 走向本组外圈，实移超过一米");
            Require(attack.Count > 0, "至少一只随从自行抵达近战并进入攻击动作");
        }
        finally
        {
            if (leader != null) manager.Recycle(leader);
            Object.Destroy(host); Object.Destroy(target.gameObject);
            if (data.valid) data.Remove();
            if (mesh) Object.Destroy(mesh);
            if (mount) { mount.SetActive(false); Object.Destroy(mount); }
        }
    }
}
