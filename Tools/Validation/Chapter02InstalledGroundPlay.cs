// Copy to an Editor assembly in the isolated desktop QA project. Never saves the scene.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NarutoVR.SealCombat.Enemies;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class Chapter02InstalledGroundPlay
{
    const string Pending = "Chapter02InstalledGroundPlay.Pending";
    const string ScenePath = "Assets/Scene/Chapter02_SuspendedPath.unity";
    const string Catalog = "Assets/ScriptableObjects/敌人目录配置/本关怪物目录.asset";
    static readonly List<string> lines = new();

    static Chapter02InstalledGroundPlay() => EditorApplication.update += CheckPlay;

    public static void Run()
    {
        if (!Application.isBatchMode || string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CH2_ASSEMBLY_OUTPUT")))
            throw new InvalidOperationException("Use a desktop validation copy and set CH2_ASSEMBLY_OUTPUT.");
        EditorSceneManager.OpenScene(ScenePath);
        SessionState.SetBool(Pending, true);
        EditorApplication.EnterPlaymode();
    }

    static async void CheckPlay()
    {
        if (!Application.isPlaying || Time.frameCount < 3 || !SessionState.GetBool(Pending, false)) return;
        SessionState.EraseBool(Pending);
        int exitCode = 0;
        try
        {
            var scene = SceneManager.GetActiveScene();
            var nodes = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToArray();
            var post = nodes.Single(t => t.name == "背石起始位").GetComponent<StoneCarrierPost>();
            var elite = nodes.Single(t => t.name == "精英出生位");
            var manager = new GameObject("QA temporary production enemy manager").AddComponent<EnemyManager>();
            manager.catalog = AssetDatabase.LoadAssetAtPath<EnemyCatalog>(Catalog);
            var target = new GameObject("QA temporary target transform").transform;
            Require(scene.path == ScenePath && post && post.basket && post.basket.slot, "Saved carrier post and A basket loaded");
            lines.Add("Scene-registered navigation triangles: " + NavMesh.CalculateTriangulation().indices.Length / 3);
            await CheckCarrier(manager, target, post);
            await CheckElite(manager, target, elite, nodes.Single(t => t.name == "主榫取用站位"));
            lines.Add("PASS. Navigation, posts and basket came from the saved scene. The manager and target were desktop fixtures.");
        }
        catch (Exception error)
        {
            lines.Add("FAIL " + error);
            exitCode = 1;
        }
        File.WriteAllLines(Path.Combine(Environment.GetEnvironmentVariable("CH2_ASSEMBLY_OUTPUT"), "ground-play.txt"), lines);
        EditorApplication.Exit(exitCode);
    }

    static async Task CheckCarrier(EnemyManager manager, Transform target, StoneCarrierPost post)
    {
        var start = post.transform.position;
        var drop = post.patrolPoints[0].position;
        var retreat = (drop - start).normalized;
        target.position = start - retreat * 2 + Vector3.up * 1.6f;
        var lease = await manager.SpawnEnemyAsync("stone-carrier", start, Quaternion.LookRotation(retreat), target);
        var worker = lease.Controller.GetComponent<StoneCarrier>();
        Require(post.Bind(lease, target) && post.basket.Stone == worker.Stone, "Lease and stone bound by saved post");
        Require(lease.Controller.Entity.ReceiveHit("Desktop carrier regression", 1, 0, attacker: target) && worker.Retreating, "Hit started retreat");
        var actor = lease.Controller.transform;
        var begin = actor.position;
        for (int i = 0; i < 360 && Vector3.ProjectOnPlane(actor.position - drop, Vector3.up).magnitude > .53f; i++)
            await Awaitable.FixedUpdateAsync();
        float distance = Vector3.Distance(actor.position, begin);
        Require(lease.Controller.Agent.isOnNavMesh && distance > 5 && Vector3.Distance(actor.position, drop) < .65f,
            $"Carrier moved {distance:F3} m from {begin:F3} to {actor.position:F3}");
        post.ResetPost();
        manager.Recycle(lease);
        Require(!post.basket.Stone && !manager.IsCurrent(lease), "Basket binding and lease reset");
    }

    static async Task CheckElite(EnemyManager manager, Transform target, Transform spawn, Transform pickup)
    {
        var forward = Vector3.ProjectOnPlane(pickup.position - spawn.position, Vector3.up).normalized;
        var floor = spawn.position + forward * 7f;
        target.SetPositionAndRotation(floor + Vector3.up * .22f, Quaternion.LookRotation(-forward));
        var elite = await manager.SpawnEnemyAsync("elite-canjiou", spawn.position, spawn.rotation, target);
        var origin = new Dictionary<long, Vector3>();
        var travel = new Dictionary<long, float>();
        var connected = new HashSet<long>();
        bool attack = false;
        bool command = false;
        var path = new NavMeshPath();
        for (int i = 0; i < 850; i++)
        {
            await Awaitable.FixedUpdateAsync();
            command |= elite.Controller.IsCommanding;
            foreach (var member in manager.ActiveEnemies.Values.Where(e => e.EnemyId == "canjiou" && !e.Defeated).OrderBy(e => e.Id).Take(3))
            {
                var actor = member.Controller;
                var pos = actor.transform.position;
                if (!origin.ContainsKey(member.Id)) { origin.Add(member.Id, pos); travel.Add(member.Id, 0); }
                travel[member.Id] = Mathf.Max(travel[member.Id], Vector3.Distance(pos, origin[member.Id]));
                if (actor.Agent.isOnNavMesh && actor.Agent.CalculatePath(floor, path) && path.status == NavMeshPathStatus.PathComplete)
                    connected.Add(member.Id);
                attack |= actor.CurrentAttack >= 0;
            }
            if (origin.Count == 3 && connected.Count == 3 && travel.Values.All(v => v > 1f) && attack && command) break;
        }
        Require(command && connected.Count == 3 && travel.Values.All(v => v > 1f) && attack,
            "Elite commanded, summons with paths " + connected.Count + ", displacement (m) " +
            string.Join(", ", travel.Values.Select(v => v.ToString("F2"))) + ", melee " + attack);
        manager.Recycle(elite);
    }

    static void Require(bool value, string description)
    {
        if (!value) throw new InvalidOperationException(description);
        lines.Add(description);
    }
}
