using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using NarutoVR.SealCombat.Enemies;
using NarutoVR.SealCombat.Rpg;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>在二章的独立可写副本检验安装、保存回读、撤销重做及冲突保护。</summary>
public static class Chapter02AltarTrioEditChecks
{
    const string ScenePath = "Assets/Scene/Chapter02_SuspendedPath.unity";
    static readonly List<string> Notes = new();
    static string Arg(string name)
    {
        var args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, name);
        return index < 0 ? null : args[index + 1];
    }
    static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        Notes.Add("PASS " + message);
    }
    static string Id(UnityEngine.Object item) => item ? GlobalObjectId.GetGlobalObjectIdSlow(item).ToString() : "null";
    static CombatRoomEncounter[] Rooms(Scene scene) => scene.GetRootGameObjects()
        .SelectMany(o => o.GetComponentsInChildren<CombatRoomEncounter>(true)).ToArray();
    static CombatRoomEncounter Room(Scene scene) => Rooms(scene).Single(r => r.spawnPoints != null &&
        r.spawnPoints.Any(s => s?.point && s.point.TryGetComponent<StoneCarrierPost>(out _)));
    static string Entry(CombatRoomEncounter.SpawnPoint s) => s == null ? "null" : $"{s.enemyId}/{Id(s.point)}/{s.isSummoned}";
    static string RoomState(CombatRoomEncounter r) =>
        $"{Id(r)}|{Id(r.rpg)}|{Id(r.area)}|{Id(r.enemyManager)}|{r.exitHysteresis}|" +
        string.Join(",",r.enemies.Select(e => Id(e))) + "|" + string.Join(",",r.spawnPoints.Select(Entry));
    static string Others(Scene s, CombatRoomEncounter altar) => string.Join("\n",
        Rooms(s).Where(r => r != altar).Select(RoomState).OrderBy(v => v, StringComparer.Ordinal));
    static string AltarState(CombatRoomEncounter r)
    {
        var root = r.transform.root;
        var tree = root.GetComponentsInChildren<Transform>(true).Select(t =>
            $"{t.GetInstanceID()}|{t.GetSiblingIndex()}|{t.name}|{t.localPosition:F6}|{t.localRotation:F6}|{t.localScale:F6}|{t.gameObject.activeSelf}");
        return RoomState(r) + "\n" + string.Join("\n", tree);
    }
    static void Reject(Scene scene, CombatRoomEncounter room, string test)
    {
        string before = AltarState(room), elsewhere = Others(scene, room);
        bool dirty = scene.isDirty, rejected = false;
        try { Chapter02AltarTrioInstaller.Apply(scene); }
        catch (InvalidOperationException) { rejected = true; }
        Check(rejected && before == AltarState(room) && elsewhere == Others(scene, room) && scene.isDirty == dirty, test);
    }
    static string Hash(string file)
    {
        using var hash = SHA256.Create();
        return BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(file))).Replace("-", "");
    }

    public static void Run()
    {
        string output = Arg("-altarResults");
        try
        {
            var root = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/');
            if (!Application.isBatchMode || root != Arg("-altarDisposable")?.Replace('\\', '/') ||
                !root.StartsWith("D:/Temp/NarutoVR-", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("请显式指定独立可写工程，验证将保存该副本的场景。");
            Notes.Add("Unity " + Application.unityVersion);
            Notes.Add("sourceSceneSHA256=" + Hash(ScenePath));
            var s = EditorSceneManager.OpenScene(ScenePath);
            var room = Room(s);
            string other = Others(s, room);
            Check(room.spawnPoints.Length == 1 && room.spawnPoints[0].enemyId == "stone-carrier", "输入是祭坛单工奴版本");
            string original = Entry(room.spawnPoints[0]);
            var post = room.spawnPoints[0].point.GetComponent<StoneCarrierPost>();
            string postState = Id(post) + Id(post.basket) + string.Join("/", post.patrolPoints.Select(t => Id(t)));
            Undo.ClearAll();
            Check(Chapter02AltarTrioInstaller.Apply(s), "在原登记后补齐缺少的两只");
            Undo.FlushUndoRecordObjects();
            Check(Entry(room.spawnPoints[0]) == original && room.spawnPoints.Select(e => e.enemyId)
                .SequenceEqual(new[] {"stone-carrier", "canjiou", "zhihuocanji"}), "原工奴出生登记不变，新登记各一只");
            var pts = room.spawnPoints.Skip(1).Select(e => (e.point.position, e.point.rotation)).ToArray();
            foreach (var e in room.spawnPoints)
                Notes.Add($"{e.enemyId} world_m={e.point.position:F6} yaw_deg={e.point.eulerAngles.y:F3} marker={e.point.name}");
            Undo.PerformUndo();
            Check(room.spawnPoints.Length == 1 && Entry(room.spawnPoints[0]) == original && !room.transform.root.Find("普通近战出生点") &&
                !room.transform.root.Find("掷火残机出生点"), "一次 Undo 还原单工奴与原层级");
            Undo.PerformRedo();
            Check(room.spawnPoints.Length == 3 && pts.Select((p, i) => Vector3.Distance(room.spawnPoints[i + 1].point.position,p.position)<.0001f &&
                Quaternion.Angle(room.spawnPoints[i + 1].point.rotation,p.rotation)<.01f).All(x => x), "一次 Redo 恢复登记、点位和朝向");
            Check(EditorSceneManager.SaveScene(s), "仅保存独立工程内的测试场景");
            s = EditorSceneManager.OpenScene(ScenePath);
            room = Room(s);
            var saved = Hash(ScenePath);
            Check(!s.isDirty && !Chapter02AltarTrioInstaller.Apply(s) && !s.isDirty && saved == Hash(ScenePath), "重开后重复安装返回 false，场景和磁盘均未改变");
            post = room.spawnPoints[0].point.GetComponent<StoneCarrierPost>();
            Check(postState == Id(post) + Id(post.basket) + string.Join("/",post.patrolPoints.Select(t=>Id(t))) && other == Others(s,room),
                "原工奴的 post、原篮、巡逻点和祭坛以外所有房间的范围、服务、敌人与出生引用完全保留");
            var carrier = room.spawnPoints[0]; var melee = room.spawnPoints[1]; var ranged = room.spawnPoints[2];
            melee.point.name = "场景作者自定 · 近战";
            ranged.point.name = "场景作者自定 · 远程";
            melee.point.position += Vector3.right * .15f;
            ranged.point.Rotate(0, 23, 0);
            room.spawnPoints = new[] {ranged, carrier, melee};
            var artistState = AltarState(room);
            Check(!Chapter02AltarTrioInstaller.Apply(s) && AltarState(room) == artistState, "既有三只可自定名字、排列、地面位置和朝向，保持逐项原值");
            room.spawnPoints = new[] {carrier, melee, melee};
            Reject(s,room,"重复 ID/锚点拒绝，祭坛和其他房间零改动");
            room.spawnPoints = new[] {carrier, new CombatRoomEncounter.SpawnPoint {enemyId="canjiou",point=ranged.point,isSummoned=true}};
            Reject(s,room,"误配召唤标志拒绝，祭坛和其他房间零改动");
            var outside = Rooms(s).First(r=>r!=room).transform;
            room.spawnPoints = new[] {carrier, new CombatRoomEncounter.SpawnPoint {enemyId="zhihuocanji",point=outside}};
            Reject(s,room,"跨房间锚点拒绝，工坊和祭坛零改动");
            ranged.point.name = "掷火残机出生点";
            room.spawnPoints = new[] {melee, carrier};
            Check(Chapter02AltarTrioInstaller.Apply(s) && room.spawnPoints[0]==melee && room.spawnPoints[1]==carrier &&
                room.spawnPoints[2].point==ranged.point, "部分已有登记只补缺失项，同时复用预布置的远程锚点");
            Notes.Add("ALTAR_TRIO_EDIT_PASS");
            File.WriteAllLines(output,Notes);
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            Notes.Add("FAIL " + e.Message);
            File.WriteAllLines(output,Notes);
            Debug.LogException(e);
            EditorApplication.Exit(1);
        }
    }
}
