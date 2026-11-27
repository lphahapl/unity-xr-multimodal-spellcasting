// 只在独立验证项目 Assets/Editor 下运行，不写回正式场景。
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NarutoVR.SealCombat.Enemies;
using NarutoVR.SealCombat.Rpg;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class Chapter02CarrierNatural
{
    public const string ScenePath = "Assets/Scene/Chapter02_SuspendedPath.unity";
    const string Key = "Chapter02Carrier.Natural";
    static Chapter02CarrierNatural() => EditorApplication.update += InspectPlay;
    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("仅限隔离批处理");
        EditorSceneManager.OpenScene(ScenePath);
        Directory.CreateDirectory(Report);
        File.WriteAllLines(Path.Combine(Report,"unity-dependencies.txt"), AssetDatabase.GetDependencies(ScenePath,true).OrderBy(p=>p).ToArray());
        SessionState.SetBool(Key,true); EditorApplication.EnterPlaymode();
    }
    public static string Report => Environment.GetEnvironmentVariable("CARRIER_REPORT");
    public static Transform[] All() => SceneManager.GetActiveScene().GetRootGameObjects()
        .SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
    public static Transform Find(string label) => All().Single(t=>t.name == label);
    public static string Hierarchy(Transform t) => t.parent ? Hierarchy(t.parent)+"/"+t.name : t.name;
    static string Describe(Transform t)
    {
        var cols=t.GetComponents<Collider>().Select(c=>$"{c.GetType().Name}:{c.enabled}/{c.isTrigger} center={c.bounds.center:F2} size={c.bounds.size:F2}");
        var ren=t.GetComponent<Renderer>();
        return $"{Hierarchy(t)} | pos={t.position:F3} rot={t.eulerAngles:F1} active={t.gameObject.activeInHierarchy} | {string.Join(";",cols)} | " +
            (ren ? $"mesh center={ren.bounds.center:F2} size={ren.bounds.size:F2}" : "");
    }
    static async void InspectPlay()
    {
        if(!Application.isPlaying || !SessionState.GetBool(Key,false))return;
        SessionState.EraseBool(Key);
        try
        {
            var all=All();
            var a=Find("A · 镇石配重吊篮"); var b=Find("B · 谷底吊篮");
            var rock=Find("背载镇石 · 含铜箍"); var start=Find("背石起始位"); var drop=Find("示例落石");
            var a0=a.position;var b0=b.position;var s0=rock.position;
            var notes=new List<string>();
            foreach(var g in new[]{typeof(EnemyManager),typeof(EnemyController),typeof(StoneCarrier),typeof(BallastStone),
                typeof(StoneCarrierPost),typeof(BallastBasket),typeof(GlyphHeavyBody),typeof(CombatRoomEncounter),typeof(NavMeshAgent),typeof(Rigidbody)})
                notes.Add($"{g.Name} {all.Sum(t=>t.GetComponents(g).Length)}");
            var components=all.SelectMany(t=>t.GetComponents<MonoBehaviour>()).ToArray();
            notes.Add("Scene MonoBehaviours: "+string.Join(", ",components.Where(x=>x).GroupBy(x=>x.GetType().FullName).Select(g=>$"{g.Key}={g.Count()}")));
            notes.Add("Missing scripts: "+components.Count(x=>!x));
            notes.Add("Renderer missing mesh refs: "+all.Sum(t=>t.GetComponents<MeshFilter>().Count(m=>!m.sharedMesh)));
            notes.Add($"Spawn NavMesh {NavMesh.SamplePosition(start.position,out _,2,NavMesh.AllAreas)}");
            int ticks=0;
            while(ticks++<100) await Awaitable.FixedUpdateAsync();
            notes.Add($"100 fixed steps ({100*Time.fixedDeltaTime:F2}s) deltaA={(a.position-a0).magnitude:F5}, deltaB={(b.position-b0).magnitude:F5}, deltaStone={(rock.position-s0).magnitude:F5}");
            notes.AddRange(all.Where(t=> t.IsChildOf(a)||t.IsChildOf(b)||t.IsChildOf(rock)||
                t.name.Contains("梁")||t.name.Contains("吊")||t.name.Contains("石")||t.name.Contains("接敌")||
                t.name.Contains("篮口")||t.name.Contains("吊索")||
                (t.GetComponent<Collider>() && Vector3.Distance(t.GetComponent<Collider>().bounds.ClosestPoint(drop.position),drop.position)<12)).Select(Describe));
            File.WriteAllLines(Path.Combine(Report,"natural.txt"),notes);
            Debug.Log("CH02_CARRIER_NATURAL_PASS");EditorApplication.Exit(0);
        }
        catch(Exception e){Debug.LogError("CH02_CARRIER_NATURAL_FAIL "+e);EditorApplication.Exit(1);}
    }
}
