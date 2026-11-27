using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using NarutoVR.Editor;
using NarutoVR.SealCombat;
using NarutoVR.SealCombat.Editor;
using NarutoVR.SealCombat.Enemies;
using NarutoVR.SealCombat.Events;
using NarutoVR.SealCombat.Rpg;
using Unity.Collections;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Place inside the game editor assembly of a disposable project copy. Run RoofTrial.Run in batch mode.
[InitializeOnLoad]
public static class RoofTrial
{
    const string ScenePath = "Assets/Scene/Chapter02_SuspendedPath.unity", Key = "roof.trial.pending";
    // Use -roofMovingControl to run the moving-shooter contrast with the prefab's stock dwell time.
    static readonly bool installedPosts = Environment.GetCommandLineArgs().Contains("-installedRoofPosts");
    static readonly bool movingControl = installedPosts || Environment.GetCommandLineArgs().Contains("-roofMovingControl");
    static readonly string Output = Path.GetFullPath(installedPosts ? "Logs/RoofTrialInstalled" :
                                                     movingControl ? "Logs/RoofTrialMovingControl" : "Logs/RoofTrial");
    static readonly List<Roof> roofs = new();
    static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static IEnumerator routine;
    static int lastFrame;
    static double deadline;
    static StreamWriter trail, flight;
    static string phase;
    static RoofTrial()
    {
        EditorApplication.update += Tick;
    }
    public static void Run()
    {
        if (!Application.isBatchMode)
            throw new InvalidOperationException("在隔离工程的批处理模式中运行");
        var scene = EditorSceneManager.OpenScene(ScenePath);
        if (installedPosts)
        {
            var first = WorkshopRoofEncounterSetup.InstallPosts(scene);
            Require(first.SequenceEqual(WorkshopRoofEncounterSetup.InstallPosts(scene)), "重复装配改变了房间敌人引用");
            const string copy = "Assets/Scene/T16_InstalledRoofTrial.unity";
            Require(EditorSceneManager.SaveScene(scene, copy, true), "无法保存独立验证场景");
            EditorSceneManager.OpenScene(copy);
        }
        SessionState.SetBool(Key, true);
        EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !Application.isPlaying || Time.frameCount < 2 ||
            Time.frameCount == lastFrame)
            return;
        lastFrame = Time.frameCount;
        try
        {
            if (routine == null)
            {
                deadline = EditorApplication.timeSinceStartup + 240;
                routine = Play();
            }
            if (EditorApplication.timeSinceStartup > deadline)
                throw new TimeoutException(phase);
            if (!routine.MoveNext())
            {
                Log("ROOF_TRIAL_PASS");
                Finish(0);
            }
        }
        catch (Exception e)
        {
            Debug.LogError("ROOF_TRIAL_FAIL " + e);
            Finish(1);
        }
    }
    static void Finish(int code)
    {
        trail?.Dispose();
        flight?.Dispose();
        SessionState.EraseBool(Key);
        EditorApplication.Exit(code);
    }
    static void Log(string s)
    {
        Debug.Log("T16 " + s);
        File.AppendAllText(Path.Combine(Output, "report.txt"), s + "\n");
    }
    static void Require(bool ok, string s)
    {
        if (!ok)
            throw new InvalidOperationException(s);
    }
    static string V(Vector3 v) => v.ToString("F3", CultureInfo.InvariantCulture);
    static T Field<T>(EnemyFireCore core, string n) => (T) typeof(EnemyFireCore).GetField(n, Private).GetValue(core);
    sealed class Roof
    {
        public Transform building;
        public MeshCollider tiles;
        public EnemyController actor;
        public Vector3[] anchors;
        public int area, samples;
        public float travel, maxError;
        public Vector3 last;
        public bool[] visited = new bool[2];
        public Roof(Transform node, int index, List<NavMeshBuildSource> sources)
        {
            area = 3 + index;
            building = node;
            if (installedPosts)
            {
                var surface = node.GetComponentsInChildren<NavMeshSurface>().Single();
                tiles = surface.GetComponent<MeshCollider>();
                actor = surface.GetComponentsInChildren<EnemyController>().Single();
                area = NavMesh.GetAreaFromName(WorkshopRoofEncounterSetup.Posts[index].area);
                Require(surface.navMeshData && tiles && tiles.sharedMesh && area >= 0 && area == surface.defaultArea &&
                        actor.Agent.areaMask == (1 << area) && !actor.Agent.autoTraverseOffMeshLink,
                        "屋顶驻点导航配置未序列化 " + node.name);
                Require(actor.Agent.isOnNavMesh && Mathf.Approximately(actor.patrolWaitSeconds, 1.5f),
                        "驻点初始绑定或停留时间错误 " + node.name);
                anchors = actor.patrolPoints.Select(t => t.position).ToArray();
                Require(anchors.Length == 2 && actor.patrolPoints.All(t => t.IsChildOf(surface.transform)),
                        "屋顶巡逻点引用不完整");
                last = actor.transform.position;
                actor.Entity.Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                Log($"installed {node.name} spawn={V(last)} route={V(anchors[0])} -> {V(anchors[1])} " +
                    $"area={area} dwell={actor.patrolWaitSeconds} nav={AssetDatabase.GetAssetPath(surface.navMeshData)}");
                return;
            }
            var render = node.GetComponentsInChildren<LODGroup>().First().GetLODs()[0].renderers.First();
            var mf = render.GetComponent<MeshFilter>();
            int slot = Array.FindIndex(render.sharedMaterials, m => m && m.name == "Tile");
            Require(slot >= 0, node.name + " 缺少瓦面子网格");
            using var meshes = MeshUtility.AcquireReadOnlyMeshData(mf.sharedMesh);
            var data = meshes[0];
            using var vertices = new NativeArray<Vector3>(data.vertexCount, Allocator.Temp);
            using var indices = new NativeArray<int>(data.GetSubMesh(slot).indexCount, Allocator.Temp);
            data.GetVertices(vertices);
            data.GetIndices(indices, slot);
            var mesh = new Mesh { name = node.name + " 原瓦面" };
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = vertices.ToArray();
            mesh.SetTriangles(indices.ToArray(), 0);
            var tileObject = new GameObject(mesh.name);
            tileObject.transform.SetParent(mf.transform, false);
            tiles = tileObject.AddComponent<MeshCollider>();
            tiles.sharedMesh = mesh;
            sources.Add(new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Mesh, sourceObject = mesh,
                                                 transform = mf.transform.localToWorldMatrix, area = area });
            Log(node.name + " " + AssetDatabase.GetAssetPath(mf.sharedMesh) + " submesh=" + slot +
                " tileTriangles=" + indices.Length / 3 + " origin=" + V(node.position));
        }
        public Vector3 Sample(float x, float z)
        {
            var above = building.TransformPoint(new Vector3(x, 12, z));
            Require(tiles.Raycast(new Ray(above, Vector3.down), out var tile, 20), "瓦面射线未命中 " + V(above));
            Require(NavMesh.SamplePosition(tile.point, out var nav, .8f, 1 << area), "瓦面无法导航 " + V(tile.point));
            return nav.position;
        }
        public void Observe()
        {
            Vector3 p = actor.transform.position;
            Require(actor.Agent.isOnNavMesh && NavMesh.SamplePosition(p, out var nav, .22f, 1 << area),
                    "离开本栋屋顶 " + V(p));
            Require(tiles.Raycast(new Ray(p + Vector3.up, Vector3.down), out var hit, 1.4f), "脚下瓦面中断 " + V(p));
            float error = Mathf.Abs(p.y - hit.point.y);
            maxError = Mathf.Max(maxError, error);
            travel += Vector3.Distance(last, p);
            last = p;
            samples++;
            for (int j = 0; j < 2; j++)
                visited[j] |= Vector3.Distance(p, anchors[j]) < .5f;
            foreach (var c in actor.Agent.path.corners)
                Require(NavMesh.SamplePosition(c, out _, .22f, 1 << area), "路径拐点离开屋面 " + V(c));
            trail.WriteLine(FormattableString.Invariant(
                $"{Time.frameCount},{phase},{area},{p.x:F4},{p.y:F4},{p.z:F4},{error:F4},{actor.CurrentBehaviour}"));
        }
    }
    static IEnumerator Play()
    {
        Directory.CreateDirectory(Output);
        File.WriteAllText(Path.Combine(Output, "report.txt"), "");
        trail = new StreamWriter(Path.Combine(Output, "agents.csv"));
        trail.WriteLine("frame,phase,roof,x,y,z,tileError,behaviour");
        flight = new StreamWriter(Path.Combine(Output, "cores.csv"));
        flight.WriteLine("frame,id,source,returned,x,y,z");
        Time.captureFramerate = 60;
        UnityEngine.Random.InitState(1625);
        var objects = SceneManager.GetActiveScene()
                          .GetRootGameObjects()
                          .SelectMany(o => o.GetComponentsInChildren<Transform>(true))
                          .ToArray();
        var workshop = objects.Single(t => t.name.StartsWith("03 · 失衡工坊战区"));
        var yard = workshop.GetComponentsInChildren<BoxCollider>().Single(b => Mathf.Abs(b.size.x - 36) < .01f &&
                                                                               Mathf.Abs(b.size.z - 28) < .01f);
        int oldNav = NavMesh.CalculateTriangulation().indices.Length / 3;
        int oldEnemies = objects.Count(t => t.GetComponent<EnemyRangedAttack>());
        Log($"scene={SceneManager.GetActiveScene().path} originalNavTriangles={oldNav} originalRangedActors={oldEnemies} yard={V(yard.bounds.center)}");
        var sources = new List<NavMeshBuildSource>();
        foreach (string prefix in new[] { "F1 · 锻造房", "F2 · 炭棚" })
            roofs.Add(new Roof(objects.Single(t => t.name == prefix), roofs.Count, sources));
        sources.Add(new NavMeshBuildSource { area = 0, shape = NavMeshBuildSourceShape.Box, size = yard.size,
                                             transform = yard.transform.localToWorldMatrix *
                                                         Matrix4x4.Translate(yard.center) });
        var settings = NavMesh.GetSettingsByIndex(0);
        settings.overrideVoxelSize = true;
        settings.voxelSize = .09f;
        var bounds = yard.bounds;
        bounds.Expand(new Vector3(12, 28, 12));
        var baked = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
        Require(baked != null, "屋面烘焙失败");
        NavMesh.AddNavMeshData(baked);
        Physics.SyncTransforms();
        var triangles = NavMesh.CalculateTriangulation();
        Log("bakedAreas=" + string.Join(" ", triangles.areas.GroupBy(x => x).Select(g => g.Key + ":" + g.Count())));
        using (var geo = new StreamWriter(Path.Combine(Output, "navmesh.csv")))
        {
            geo.WriteLine("area,x1,y1,z1,x2,y2,z2,x3,y3,z3");
            for (int i = 0; i < triangles.areas.Length; i++)
            {
                var vs = Enumerable.Range(0, 3).Select(j => triangles.vertices[triangles.indices[3 * i + j]]);
                geo.WriteLine(
                    triangles.areas[i] + "," +
                    string.Join(",", vs.Select(v => FormattableString.Invariant($"{v.x:F4},{v.y:F4},{v.z:F4}"))));
            }
        }
        var prefab =
            AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Enemies/ZhiHuoCanJi/ZhiHuoCanJi_Enemy.prefab");
        for (int i = 0; i < 2; i++)
        {
            var r = roofs[i];
            if (installedPosts)
            {
                Require(!NavMesh.SamplePosition(roofs[1 - i].last, out _, .5f, r.actor.Agent.areaMask),
                        "屋顶掩码允许另一栋房顶");
                continue;
            }
            float half = i == 0 ? 3.4f : 1.4f;
            r.anchors = new[] { r.Sample(-half, -.9f), r.Sample(half, -.9f) };
            var start = r.Sample(0, -.9f);
            r.last = start;
            r.actor = Object.Instantiate(prefab, start, Quaternion.identity).GetComponent<EnemyController>();
            r.actor.Entity.Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            r.actor.patrolPoints = r.anchors
                                       .Select((p, j) =>
                                               {
                                                   var a = new GameObject("T16 " + i + "-" + j).transform;
                                                   a.position = p;
                                                   return a;
                                               })
                                       .ToArray();
            if (i == 0)
            {
                var so = new SerializedObject(r.actor.Entity.RangedAttack);
                so.FindProperty("positioning.mode").enumValueIndex = (int)FireThrowerPositioning.Mode.PatrolOnly;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            Log("route " + i + " " + V(r.anchors[0]) + " -> " + V(r.anchors[1]) + " spawn " + V(start));
        }
        phase = "patrol";
        for (int i = 0; i < 1800 && roofs.Any(r => !r.visited.All(v => v)); i++)
        {
            yield return null;
            foreach (var r in roofs)
                r.Observe();
        }
        foreach (var r in roofs)
        {
            Log("patrol.area=" + r.area + " visited=" + string.Join(",", r.visited) + " travel=" + r.travel);
            Require(r.visited.All(v => v), "巡逻端点未全部抵达 " + r.actor.Status);
        }
        var dynamic = roofs[1];
        var dummy = new GameObject("临时观测目标").transform;

        // Same-elevation control separates physical occlusion from the NavMesh island restriction.
        dummy.position = dynamic.Sample(0, -.9f) + Vector3.right * 9;
        dummy.forward =
            Vector3.ProjectOnPlane(dynamic.actor.transform.position - dummy.position, Vector3.up).normalized;
        dynamic.actor.SetTarget(dummy);
        dynamic.actor.NotifyAttackedBy(dummy);
        const int seed = 1625;
        var positioning = dynamic.actor.Entity.RangedAttack.Positioning;
        bool FindShot(float now, out Vector3 point)
        {
            positioning.Reset(dynamic.actor.transform);
            UnityEngine.Random.InitState(seed);
            return positioning.TryGetDestination(dynamic.actor, dummy.position, now, out point);
        }
        Require(FindShot(1, out var openPoint), "无遮挡同层对照未获得屋顶射位");
        Log("clear control target=" + V(dummy.position) + " valid firing point=" + V(openPoint));
        var obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
        obstacle.name = "隔离遮挡板";
        obstacle.transform.position =
            Vector3.Lerp(dummy.position, dynamic.actor.transform.position, .35f) + Vector3.up * 1.3f;
        obstacle.transform.rotation = Quaternion.LookRotation(dummy.forward);
        obstacle.transform.localScale = new Vector3(18, 8, .16f);
        Physics.SyncTransforms();
        Require(!dynamic.actor.Entity.HasClearSightFrom(dynamic.actor.transform.position +
                                                            Vector3.up * dynamic.actor.eyeHeight,
                                                        dummy, dummy.position, dynamic.actor.obstructionMask),
                "遮挡板未挡住视线");
        Require(!FindShot(1, out _), "同一种子和目标加遮挡后仍有合格射位");
        phase = "occlusion";
        bool sawFallback = false;
        for (int i = 0; i < 90; i++)
        {
            yield return null;
            foreach (var r in roofs)
                r.Observe();
            sawFallback |= dynamic.actor.HasTarget &&
                           dynamic.actor.CurrentBehaviour == EnemyController.Behaviour.Patrol &&
                           dynamic.anchors.Any(p => Vector3.Distance(p, dynamic.actor.Agent.destination) < .45f);
        }
        Require(sawFallback, "遮挡时未观察到已有目标回退到屋顶锚点");
        obstacle.SetActive(false);
        Physics.SyncTransforms();
        phase = "edge";
        dummy.position = workshop.TransformPoint(new Vector3(4, 0, 5));
        dummy.forward = (dynamic.building.position - dummy.position).normalized;
        var path = new NavMeshPath();
        Require(NavMesh.SamplePosition(dummy.position, out var lower, 2, 1) &&
                    (!dynamic.actor.Agent.CalculatePath(lower.position, path) ||
                     path.status != NavMeshPathStatus.PathComplete),
                "屋面意外连通下层");
        bool noDestination =
            !dynamic.actor.Entity.RangedAttack.Positioning.TryGetDestination(dynamic.actor, dummy.position, 200, out _);
        Require(noDestination, "院内射位应不可达");
        dynamic.actor.NotifyAttackedBy(dummy);
        sawFallback = false;
        for (int i = 0; i < 100; i++)
        {
            yield return null;
            foreach (var r in roofs)
                r.Observe();
            sawFallback |= dynamic.actor.HasTarget &&
                           dynamic.actor.CurrentBehaviour == EnemyController.Behaviour.Patrol &&
                           dynamic.anchors.Any(p => Vector3.Distance(p, dynamic.actor.Agent.destination) < .45f);
        }
        Require(sawFallback, "边缘无点未回退同栋巡逻");
        Log("fallback: dynamic射手有目标，遮挡确认、院落断路确认，两种情况均实际选择原屋顶巡逻点。");
        Object.Destroy(dummy.gameObject);
        if (!movingControl)
        {
            phase = "post";
            // Begin a new encounter from authored front-eave posts. The agents walk there themselves.
            foreach (var r in roofs)
            {
                r.actor.SetTarget(null);
                r.actor.ResetForSpawn();
                var post = r.area == 3 ? r.Sample(2.9f, -1.68f) : r.Sample(-.95f, -1.30f);
                r.actor.patrolPoints[0].position = post;
                r.actor.patrolWaitSeconds = 4f;
            }
            bool Ready(Roof r) => r.actor.CurrentBehaviour == EnemyController.Behaviour.Idle &&
                                  Vector3.Distance(r.actor.transform.position, r.actor.patrolPoints[0].position) < .50f;
            bool ready = false;
            for (int i = 0; i < 1500 && !ready; i++)
            {
                yield return null;
                foreach (var r in roofs)
                    r.Observe();
                ready = roofs.All(Ready);
            }
            Require(ready, "未同步抵达临檐射位");
            foreach (var r in roofs)
                Log("post " + r.area + " goal=" + V(r.actor.patrolPoints[0].position) +
                    " actual=" + V(r.actor.transform.position));
        }
        var left = roofs[0].actor.transform.position;
        var right = roofs[1].actor.transform.position;
        var axis = Vector3.ProjectOnPlane(right - left, Vector3.up);
        var normal = Vector3.Cross(Vector3.up, axis.normalized);
        if (Vector3.Dot(normal, yard.bounds.center - (left + right) * .5f) < 0)
            normal = -normal;
        var playerSite =
            Vector3.ProjectOnPlane((left + right) * .5f, Vector3.up) + normal * 6.2f +
            axis.normalized * ((Mathf.Pow(right.y + 1, 2) - Mathf.Pow(left.y + 1, 2)) / (2 * axis.magnitude));
        if (movingControl)
            playerSite = new Vector3(17.710f, 0, 51.700f);
        using var player = new EnemyAttackChecks.RpgFixture(playerSite, "shield.left", "buff.voice", "buff.zi");
        var head = player.Receiver.HitTransform;
        Log("combat playerFloor=" + V(player.Receiver.NavigationPosition) + " head=" + V(head.position));
        foreach (var r in roofs)
        {
            r.actor.SetTarget(head);
            r.actor.NotifyAttackedBy(head);
            r.actor.transform.rotation =
                Quaternion.LookRotation(Vector3.ProjectOnPlane(head.position - r.actor.transform.position, Vector3.up));
            Require(r.actor.Entity.HasClearSightFrom(r.actor.transform.position + Vector3.up * r.actor.eyeHeight, head,
                                                     head.position, r.actor.obstructionMask),
                    "起手视线受挡 " + r.building.name + " " + V(r.actor.transform.position));
            r.actor.tickAutomatically = true;
        }
        phase = "combat";
        var owners = new HashSet<EnemyController>();
        var reflects = new HashSet<EnemyController>();
        var hp = roofs.ToDictionary(r => r.actor, r => r.actor.Entity.Combat.Health);
        var attackOwners = new Dictionary<string, EnemyController>();
        var delivered = new HashSet<string>();
        using var vitals = EventCenter.Subscribe(
            EnemyEvents.VitalsChanged,
            hit =>
            {
                if (hit.HealthDamage <= 0 || !attackOwners.TryGetValue(hit.HitId, out var source))
                    return;
                Require(source.Entity == hit.Enemy && hit.HealthDamage == 80 && delivered.Add(hit.HitId),
                        "反伤与发射者不一致或重复");
                Log("vitals " + hit.HitId + " owner=" + source.GetInstanceID() + " hp=" + hit.Health +
                    " frame=" + Time.frameCount);
            });
        bool cast = false;
        bool firstShotsFinished = false;
        float maxDist = 0;
        int castFrame = -1;
        var firstShots = new Dictionary<EnemyController, int>();
        var seenReturns = new HashSet<int>();
        for (int i = 0; i < 600; i++)
        {
            yield return null;
            foreach (var r in roofs)
                r.Observe();
            var shots = Object.FindObjectsByType<EnemyFireCore>(FindObjectsSortMode.None);
            foreach (var core in shots)
            {
                var owner = Field<EnemyController>(core, "shooter");
                if (!hp.ContainsKey(owner))
                    continue;
                var pos = core.transform.position;
                var returned = Field<bool>(core, "returning");
                flight.WriteLine(FormattableString.Invariant(
                    $"{Time.frameCount},{core.GetInstanceID()},{roofs.FindIndex(r=>r.actor==owner)},{returned},{pos.x:F4},{pos.y:F4},{pos.z:F4}"));
                attackOwners[Field<string>(core, "attackId")] = owner;
                if (owners.Add(owner))
                {
                    firstShots.Add(owner, core.GetInstanceID());
                    Log("fire " + owner.GetInstanceID() + " pos=" + V(pos) + " frame=" + Time.frameCount);
                }
                if (returned && firstShots[owner] == core.GetInstanceID() && seenReturns.Add(core.GetInstanceID()))
                {
                    Require(castFrame >= 0 && (Time.frameCount - castFrame) / 60f <= .3f, "首发火核超出衡印弹反窗");
                    reflects.Add(owner);
                    Log("return " + owner.GetInstanceID() + " frame=" + Time.frameCount);
                }
            }
            if (!cast && firstShots.Count == 2 && shots.Count(c => firstShots.ContainsValue(c.GetInstanceID())) == 2)
            {
                var center = head.position - Vector3.up * .7f;
                var distances = shots.Where(c => firstShots.ContainsValue(c.GetInstanceID()))
                                    .Select(c => Vector3.Distance(c.transform.position, center))
                                    .ToArray();
                if (distances.Min() < 1.49f)
                {
                    castFrame = Time.frameCount;
                    maxDist = distances.Max();
                    cast = player.Controller.DebugCast("shield.left");
                    Require(cast, "衡印起手失败 " + player.Controller.Status);
                    Log("cast frame=" + Time.frameCount + " coreDistances=" + string.Join(",", distances));
                }
            }
            firstShotsFinished = seenReturns.Count == 2 && !shots.Any(c => firstShots.ContainsValue(c.GetInstanceID()));
            if (movingControl && firstShotsFinished)
                break;
            if (cast && hp.All(e => Mathf.Approximately(e.Key.Entity.Combat.Health, e.Value - 80)))
                break;
        }
        foreach (var r in roofs)
            Log("result " + r.building.name + " hp=" + r.actor.Entity.Combat.Health + " agentSamples=" + r.samples +
                " travel=" + r.travel + " maxTileHeightError=" + r.maxError);
        Require(cast && owners.Count == 2 && reflects.Count == 2, "首发双核未在同一盾窗回弹 lastMaxDist=" + maxDist);
        Require(roofs.All(r => r.maxError < .25f), "屋顶导航高度偏差过大");
        Require(Mathf.Approximately(player.Controller.Player.Health, 100) &&
                    Mathf.Approximately(player.Effects.ShieldPoints, 100),
                "气血或盾量异常");
        if (installedPosts)
        {
            Require(firstShotsFinished && hp.All(e => Mathf.Approximately(e.Key.Entity.Combat.Health, e.Value) ||
                                                     Mathf.Approximately(e.Key.Entity.Combat.Health, e.Value - 80)),
                    "正式驻点火核未消失或出现异常伤害");
            Log($"installed outcome: 双核均弹回，命中原主 {delivered.Count}/2，玩家生命100、盾量100。");
            CheckRoomRegistration(player);
        }
        else if (movingControl)
        {
            Require(firstShotsFinished && delivered.Count == 0 &&
                        hp.All(e => Mathf.Approximately(e.Key.Entity.Combat.Health, e.Value)),
                    "移动对照中出现原主受击");
            Log("moving control: 首发双核回弹后消失，两射手各150生命，玩家生命100、盾量100。");
        }
        else
        {
            Require(delivered.Count == 2 && hp.All(e => Mathf.Approximately(e.Key.Entity.Combat.Health, e.Value - 80)),
                    "未分别命中两名原主");
            Log("double return: 一次衡印，两名原主各损失80，玩家生命100、盾量100。");
        }
    }

    static void CheckRoomRegistration(EnemyAttackChecks.RpgFixture player)
    {
        var actors = roofs.Select(r => r.actor).ToArray();
        var root = new GameObject("测试房间");
        root.SetActive(false);
        try
        {
            var rpg = player.Controller;
            root.transform.position = rpg.locomotion.PlayerFloorPosition;
            var area = root.AddComponent<BoxCollider>();
            area.isTrigger = true;
            area.size = Vector3.one * 4;
            var room = root.AddComponent<CombatRoomEncounter>();
            room.rpg = rpg;
            room.area = area;
            room.enemies = actors;
            rpg.roomControlsEncounter = true;
            root.SetActive(true);
            room.TickEncounter();
            Require(room.PlayerInside && room.HasStarted && !room.UsesEnemyManager &&
                    actors.All(a => a.target == rpg.locomotion.head && a.CanReceiveHit && a.Agent.isOnNavMesh),
                    "房间未给两名驻点射手绑定玩家头部");
            rpg.locomotion.rigRoot.position += Vector3.right * 8;
            room.TickEncounter();
            Require(!room.PlayerInside && actors.All(a => !a.CanReceiveHit), "离房未暂停两射手");
            rpg.locomotion.rigRoot.position -= Vector3.right * 8;
            room.TickEncounter();
            Require(room.PlayerInside && actors.All(a => a.CanReceiveHit), "重入未唤醒两射手");
            foreach (var actor in actors)
                Require(actor.Entity.ReceiveHit("roof-room-" + actor.GetInstanceID(), 1000, 0), "测试清场伤害失败");
            room.TickEncounter();
            Require(room.IsComplete, "两驻点死亡后房间未清场");
            room.ResetEncounter();
            Require(!room.IsComplete && actors.All(a => !a.gameObject.activeSelf), "房间重置未收起两驻点");
            Log("room: 正式 CombatRoomEncounter.enemies 登记两名驻点，进入绑定 locomotion.head，退出暂停，重入恢复，双亡清场，重置通过。");
        }
        finally { Object.DestroyImmediate(root); }
    }
}
