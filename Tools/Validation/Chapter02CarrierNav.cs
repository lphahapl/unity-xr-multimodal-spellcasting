// 仅在 D:/Temp/NarutoVR-CarrierQA-20261125 编译、烘焙和回读；不保存场景。
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
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class Chapter02CarrierNav
{
    public const string AssetPath="Assets/Art/Environment/SuspendedPath/Navigation/Chapter02AltarStoneCarrier_NavMesh.asset";
    const string Pending="Chapter02CarrierNav.Disk";
    static readonly List<string> notes=new();
    static Chapter02CarrierNav()=>EditorApplication.update+=OnPlay;
    static void Guard()
    {
        if(!Application.isBatchMode || !Path.GetFullPath(Application.dataPath).Replace('\\','/').Equals(
            "D:/Temp/NarutoVR-CarrierQA-20261125/Assets",StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("仅限指定的独立验证工程");
    }
    static Transform Find(string name)=>Chapter02CarrierNatural.Find(name);
    static void Record(string line){notes.Add(line);Debug.Log(line);}
    static void Check(bool ok,string text){if(!ok)throw new InvalidOperationException(text);Record("PASS "+text);}
    static void Finish(string sub,int code)
    {
        File.WriteAllLines(Path.Combine(Chapter02CarrierNatural.Report,"nav-"+sub+".txt"),notes);
        EditorApplication.Exit(code);
    }
    public static void Bake()
    {
        Guard();
        try
        {
            EditorSceneManager.OpenScene(Chapter02CarrierNatural.ScenePath);
            Check(!AssetDatabase.LoadAssetAtPath<NavMeshData>(AssetPath) && !File.Exists(AssetPath),"使用新的祭坛独立资产名");
            var start=Find("背石起始位");var drop=Find("示例落石");
            // 与已通过的运输用例相同，收集前移走后续用作同一石外观的子节点，关闭原工奴占位。
            Find("背载镇石 · 含铜箍").SetParent(null,true);
            Find("背石工奴 · 机体占位").gameObject.SetActive(false);
            Physics.SyncTransforms();
            var bounds=new Bounds((start.position+drop.position)*.5f,new Vector3(35,9,37));
            var src=new List<NavMeshBuildSource>();
            NavMeshBuilder.CollectSources(bounds,~0,NavMeshCollectGeometry.PhysicsColliders,0,new List<NavMeshBuildMarkup>(),src);
            var cfg=NavMesh.GetSettingsByIndex(0);
            Record($"scene={Chapter02CarrierNatural.ScenePath} sceneSHA256={HashScene()} boundsCenter={bounds.center:F3} boundsSize={bounds.size:F3} geometry=PhysicsColliders layerMask=-1 defaultArea=0 count={src.Count}");
            Record($"agentTypeID={cfg.agentTypeID} agentRadius={cfg.agentRadius} agentHeight={cfg.agentHeight} slope={cfg.agentSlope} climb={cfg.agentClimb} minRegionArea={cfg.minRegionArea} tileSize={cfg.tileSize} voxelSize={cfg.voxelSize}");
            string[] detailed=src.Select(s=>$"{(s.component?Chapter02CarrierNatural.Hierarchy(s.component.transform):"(no transform)")} | shape={s.shape} area={s.area} size={s.size:F5} src={(s.sourceObject?s.sourceObject.name:"primitive")} matrix={s.transform.ToString("F5").Replace("\n",";").Trim()}").OrderBy(x=>x,StringComparer.Ordinal).ToArray();
            File.WriteAllLines(Path.Combine(Chapter02CarrierNatural.Report,"nav-sources.txt"),detailed);
            Check(src.Count==27,"27 个现场来源，与已通过的运输样机数量相同");
            var data=NavMeshBuilder.BuildNavMeshData(cfg,src,bounds,Vector3.zero,Quaternion.identity);
            Check(data,"成功烘焙世界坐标导航数据");
            data.name="Chapter02AltarStoneCarrier_NavMesh";
            AssetDatabase.CreateAsset(data,AssetPath);AssetDatabase.SaveAssets();
            Check(AssetDatabase.LoadAssetAtPath<NavMeshData>(AssetPath) && File.Exists(AssetPath),"磁盘上实际创建 NavMeshData");
            Finish("bake",0);
        }
        catch(Exception e){Record("CH02_NAV_BAKE_FAIL "+e);Finish("bake",1);}
    }
    static string HashScene()
    {
        using var sha=System.Security.Cryptography.SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(Chapter02CarrierNatural.ScenePath))).Replace("-","");
    }
    public static void VerifyDisk()
    {
        Guard();EditorSceneManager.OpenScene(Chapter02CarrierNatural.ScenePath);
        SessionState.SetBool(Pending,true);EditorApplication.EnterPlaymode();
    }
    static async void OnPlay()
    {
        if(!Application.isPlaying || !SessionState.GetBool(Pending,false))return;
        SessionState.EraseBool(Pending);
        try{await CheckDisk();Finish("disk-play",0);}
        catch(Exception e){Record("CH02_NAV_DISK_FAIL "+e);Finish("disk-play",1);}
    }
    static async Task CheckDisk()
    {
        var start=Find("背石起始位");var drop=Find("示例落石");
        var data=AssetDatabase.LoadAssetAtPath<NavMeshData>(AssetPath);
        Check(data && EditorUtility.IsPersistent(data),"新 Unity 进程从磁盘加载持久资产");
        NavMesh.RemoveAllNavMeshData();
        Check(!NavMesh.SamplePosition(start.position,out _,1f,NavMesh.AllAreas),"移除全部临时数据后出生位置无法采样");
        var surface=NavMesh.AddNavMeshData(data,Vector3.zero,Quaternion.identity);
        Check(surface.valid,"导航单独挂载，世界原点、单位旋转、单位缩放");
        var settings=NavMesh.GetSettingsByIndex(0);
        var filter=new NavMeshQueryFilter{agentTypeID=settings.agentTypeID,areaMask=NavMesh.AllAreas};
        Check(NavMesh.SamplePosition(start.position,out var navStart,1f,filter) &&
            Vector3.Distance(navStart.position,start.position)<.2f,"正式出生点在持久导航上");
        Check(NavMesh.SamplePosition(drop.position,out var navDrop,1f,filter) &&
            Vector3.Distance(navDrop.position,drop.position)<.2f,"示例撤离点在持久导航上");
        var path=new NavMeshPath();
        Check(NavMesh.CalculatePath(navStart.position,navDrop.position,filter,path)&&path.status==NavMeshPathStatus.PathComplete,
            "持久导航在出生与撤离点间给出完整路径");
        float length=0;var corners=path.corners;for(int i=1;i<corners.Length;i++)length+=Vector3.Distance(corners[i-1],corners[i]);
        Record($"sceneSHA256={HashScene()} asset={AssetPath} guid={AssetDatabase.AssetPathToGUID(AssetPath)} registeredPos=(0,0,0) rot=identity; navStart={navStart.position:F4} navEnd={navDrop.position:F4}; corners={string.Join(";",corners.Select(c=>c.ToString("F4")))} length={length:F4}m");
        var manager=new GameObject("QA磁盘导航的正式敌人服务").AddComponent<EnemyManager>();
        manager.catalog=AssetDatabase.LoadAssetAtPath<EnemyCatalog>("Assets/ScriptableObjects/敌人目录配置/本关怪物目录.asset");
        var player=new GameObject("QA头部代表点").transform;
        player.position=start.position-(drop.position-start.position).normalized*2+Vector3.up*1.6f;
        var lease=await manager.SpawnEnemyAsync("stone-carrier",start.position,Quaternion.LookRotation(drop.position-start.position),player);
        Check(lease.Controller.Agent.isOnNavMesh && manager.IsCurrent(lease),"正式 stone-carrier 由 EnemyManager 在出生点落到磁盘导航");
        var post=start.gameObject.AddComponent<StoneCarrierPost>();
        var basket=Find("A · 镇石配重吊篮").gameObject.AddComponent<BallastBasket>();
        basket.passengerVolume=basket.gameObject.AddComponent<BoxCollider>();basket.passengerVolume.isTrigger=true;
        basket.slot=Find("示例槽心");post.basket=basket;post.patrolPoints=new[]{drop,start};
        Check(post.Bind(lease,player),"现场起始位配置本次租约的撤离点");
        var worker=lease.Controller.GetComponent<StoneCarrier>();
        Check(lease.Controller.Entity.ReceiveHit("持久祭坛导航普通实击",1,0,attacker:player)&&worker.Retreating,"实际伤害进入工奴撤离");
        var origin=lease.Controller.transform.position;float flat=100;int steps=0;
        while(steps++<360 && flat>.53f)
        {
            await Awaitable.FixedUpdateAsync();
            flat=Vector3.ProjectOnPlane(lease.Controller.transform.position-drop.position,Vector3.up).magnitude;
        }
        var end=lease.Controller.transform.position;
        Check(flat<.6f && Vector3.Distance(end,origin)>5f,"NavMeshAgent 实际沿持久导航撤离超过 5 米");
        Record($"birth={origin:F4} actualEnd={end:F4} distance={Vector3.Distance(origin,end):F4}m finalGap={flat:F4}m agentType={lease.Controller.Agent.agentTypeID} areaMask={lease.Controller.Agent.areaMask}");
        manager.Recycle(lease);surface.Remove();
        Check(!NavMesh.SamplePosition(start.position,out _,1f,filter),"卸载该持久数据即失去出生点导航覆盖");
        Record("CH02_NAV_DISK_PASS");
    }
}
