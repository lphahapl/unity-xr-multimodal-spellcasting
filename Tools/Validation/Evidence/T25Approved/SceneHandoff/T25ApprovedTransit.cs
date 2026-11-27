using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NarutoVR.SealCombat.Enemies;
using NarutoVR.SealCombat.Rpg;
using Unity.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>在隔离副本保存并重开安装补丁；真实房间、角色、符力、原篮全程共用生产引用。</summary>
[InitializeOnLoad]
public static class T25ApprovedTransit
{
    const string Key="T25ApprovedTransit.Run";
    static string Dir=>Environment.GetEnvironmentVariable("T25_TRANSIT_REPORT");
    static readonly List<string> notes=new(), trail=new();
    static bool running;
    static double timeout;
    static T25ApprovedTransit()=>EditorApplication.update+=Update;
    static Transform[] All()=>SceneManager.GetActiveScene().GetRootGameObjects()
        .SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
    static T One<T>() where T:Component=>All().Select(t=>t.GetComponent<T>()).Single(c=>c);
    static string P(Vector3 p)=>p.ToString("F6");
    static string PathOf(Transform t)=>t.parent ? PathOf(t.parent)+"/"+t.name : t.name;
    static void Check(bool result,string label){if(!result)throw new Exception(label);Log(label);}
    static void Log(string line){notes.Add(line);Debug.Log("T25_APPROVED "+line);}
    static void Snapshot(string stage)
    {
        var scene=SceneManager.GetActiveScene();
        var all=All();
        var snap=new List<string>();
        foreach(var room in all.SelectMany(t=>t.GetComponents<CombatRoomEncounter>()))
        {
            var pts=room.spawnPoints??Array.Empty<CombatRoomEncounter.SpawnPoint>();
            snap.Add("room "+PathOf(room.transform)+" active="+room.isActiveAndEnabled+" manager="+(room.enemyManager?room.enemyManager.name:"null")+
                " area="+(room.area?PathOf(room.area.transform):"null")+" spawns="+string.Join(";",pts.Select(s=>s.enemyId+"@"+(s.point?PathOf(s.point):"null"))));
        }
        var post=One<StoneCarrierPost>();
        var basket=post.basket;
        var old=all.SingleOrDefault(t=>t.name=="背石工奴 · 机体占位");
        snap.Add("placeholder="+(old?old.gameObject.activeInHierarchy.ToString():"absent"));
        snap.Add("old stone="+all.Where(t=>t.name=="背载镇石 · 含铜箍").Select(t=>PathOf(t)+" active="+t.gameObject.activeInHierarchy).SingleOrDefault());
        snap.Add("post="+PathOf(post.transform)+" patrol="+string.Join(";",post.patrolPoints.Select(t=>PathOf(t))));
        snap.Add("basket="+PathOf(basket.transform)+" passenger="+PathOf(basket.passengerVolume.transform)+" center="+P(basket.passengerVolume.center)+
            " size="+P(basket.passengerVolume.size)+" slot="+PathOf(basket.slot)+" world="+P(basket.slot.position)+" rotation="+P(basket.slot.eulerAngles)+
            " onDocked.persistent="+basket.onDocked.GetPersistentEventCount());
        snap.Add("scene="+scene.path+" rpg="+PathOf(One<LinearRpgController>().transform));
        var flow=One<LinearRpgController>().chapter;
        var stages=flow ? new SerializedObject(flow).FindProperty("stages") : null;
        if(stages!=null)
            for(int i=0;i<stages.arraySize;i++)
            {
                var entry=stages.GetArrayElementAtIndex(i); var stageAsset=entry.FindPropertyRelative("stage").objectReferenceValue;
                var objectives=entry.FindPropertyRelative("objectives");
                var names=Enumerable.Range(0,objectives.arraySize).Select(j=>objectives.GetArrayElementAtIndex(j).objectReferenceValue)
                    .Select(o=>o?o.GetType().Name:"missing");
                snap.Add("chapter stage="+(stageAsset?stageAsset.name:"missing")+" objectives="+string.Join(";",names));
            }
        foreach(var o in all.SelectMany(t=>t.GetComponents<MonoBehaviour>()).Where(c=>c&&c.GetType().Name=="WorkshopTenonObjective"))
        {
            var props=new SerializedObject(o);
            foreach(var key in new[]{"roof","courtyard"})
            {
                var owner=props.FindProperty(key).objectReferenceValue as CombatRoomEncounter;
                snap.Add("WorkshopTenonObjective."+key+"="+(owner?PathOf(owner.transform):"missing"));
            }
        }
        File.WriteAllLines(Path.Combine(Dir,stage+".txt"),snap);
    }

    public static void Run()
    {
        if(!Application.isBatchMode || !Path.GetFullPath(Application.dataPath).Replace('\\','/').EndsWith("/NarutoVR-CarrierQA-20261125/Assets",StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("只可在指定 CarrierQA 副本批处理执行");
        Directory.CreateDirectory(Dir);
        var scene=EditorSceneManager.OpenScene(T25AltarRoomInstaller.ScenePath);
        Snapshot("as-found");
        bool first=T25AltarRoomInstaller.Apply(scene);
        if(T25AltarRoomInstaller.Apply(scene)) throw new Exception("重复安装改写场景");
        var post=One<StoneCarrierPost>();
        var room=All().SelectMany(t=>t.GetComponents<CombatRoomEncounter>()).Single(r=>r.spawnPoints.Any(s=>s.point==post.transform));
        var area=room.area;int previousRooms=All().Sum(t=>t.GetComponents<CombatRoomEncounter>().Length);
        room.area=null;
        if(!T25AltarRoomInstaller.Apply(scene) || room.area!=area || All().Sum(t=>t.GetComponents<CombatRoomEncounter>().Length)!=previousRooms)
            throw new Exception("修复缺失范围时应复用既有房间与原范围");
        if(first && room.gameObject==area.gameObject)
        {
            // 模拟其他负责人把房间组件放在独立对象上，原具名范围仍然存在但引用遗失。
            var separate=new GameObject("QA 临时 · 既有独立房间");
            separate.transform.SetParent(post.transform.parent,false);
            var existing=separate.AddComponent<CombatRoomEncounter>();
            existing.rpg=room.rpg;existing.enemyManager=room.enemyManager;existing.spawnPoints=room.spawnPoints;
            UnityEngine.Object.DestroyImmediate(room);
            if(!T25AltarRoomInstaller.Apply(scene) || existing.area!=area || All().Sum(t=>t.GetComponents<CombatRoomEncounter>().Length)!=previousRooms)
                throw new Exception("独立房间应找回现有具名范围且保持房间总数");
            UnityEngine.Object.DestroyImmediate(separate);
            if(!T25AltarRoomInstaller.Apply(scene) || area.GetComponent<CombatRoomEncounter>()==null)
                throw new Exception("只有原范围时应只补房间组件");
        }
        var conflict=new GameObject("QA 重复祭坛房间：结束前移除");
        conflict.transform.SetParent(post.transform.parent,false);conflict.AddComponent<CombatRoomEncounter>();
        bool denied=false;
        try{T25AltarRoomInstaller.Apply(scene);}catch(InvalidOperationException){denied=true;}
        finally{UnityEngine.Object.DestroyImmediate(conflict);}
        if(!denied || T25AltarRoomInstaller.Apply(scene))throw new Exception("遇重复房间应拒绝，清除对照后应幂等");
        if(first && !EditorSceneManager.SaveScene(scene))throw new IOException("独立副本场景保存失败");
        scene=EditorSceneManager.OpenScene(T25AltarRoomInstaller.ScenePath);
        if(T25AltarRoomInstaller.Apply(scene))throw new Exception("保存重开后补丁不幂等");
        Snapshot("installed-reopened");
        File.WriteAllLines(Path.Combine(Dir,"installer-assertions.txt"),new[]{
            "first apply modified="+first,"reapply modified=False","missing area repaired on same room + original box",
            first?"separate room reused named volume and missing room reused original collider":"preinstalled layout retained",
            "conflicting second altar-room rejected without mutation","reopen saved scene and reapply modified=False"
        });
        var rpg=One<LinearRpgController>(); rpg.autoLoad=false;
        rpg.saveFileName="t25-approved-qa-"+Guid.NewGuid().ToString("N")+".json";
        SessionState.SetBool(Key,true); EditorApplication.EnterPlaymode();
    }

    static async void Update()
    {
        if(running && EditorApplication.timeSinceStartup>timeout){Log("FAIL watchdog timeout");End(1);return;}
        if(!Application.isPlaying || !SessionState.GetBool(Key,false)) return;
        SessionState.EraseBool(Key);running=true;timeout=EditorApplication.timeSinceStartup+210;
        try{await Test();Log("PASS");End(0);}catch(Exception e){Log("FAIL "+e);End(1);}
    }
    static void End(int code)
    {
        running=false;
        File.WriteAllLines(Path.Combine(Dir,"play-assertions.txt"),notes);
        File.WriteAllLines(Path.Combine(Dir,"transit.csv"),trail);
        EditorApplication.Exit(code);
    }
    static async Task Fixed(int steps=1){for(int i=0;i<steps;i++)await Awaitable.FixedUpdateAsync();}
    static async Task Until(Func<bool> condition,string label,double seconds=35)
    {
        var stop=EditorApplication.timeSinceStartup+seconds;
        while(!condition() && EditorApplication.timeSinceStartup<stop) await Task.Delay(25);
        Check(condition(),label);
    }
    static void MoveRig(RpgLocomotionController loc,Vector3 floor)
    {
        loc.rigRoot.position+=floor-loc.PlayerFloorPosition;
        Physics.SyncTransforms();
    }
    static async Task Test()
    {
        var rpg=One<LinearRpgController>();var loc=rpg.locomotion;var post=One<StoneCarrierPost>();
        var manager=One<EnemyManager>();var basket=post.basket;var slot=basket.slot;
        var head=loc.head;var glyph=AssetDatabase.LoadAssetAtPath<QianjunEffectDefinition>("Assets/Art/Glyphs/QianjunTether.asset");
        var room=All().SelectMany(t=>t.GetComponents<CombatRoomEncounter>()).Single(r=>r.spawnPoints.Any(s=>s.point==post.transform && s.enemyId=="stone-carrier"));
        int slotId=slot.GetInstanceID(),basketId=basket.GetInstanceID(),volumeId=basket.passengerVolume.GetInstanceID();
        Vector3 anchor=slot.position;
        int count=0;basket.onDocked.AddListener(()=>{count++; Log("onDocked count="+count+" stone="+basket.Stone.GetInstanceID()+" at="+P(basket.Stone.transform.position));});
        var old=All().SingleOrDefault(t=>t.name=="背石工奴 · 机体占位");
        Check(!old || !old.gameObject.activeInHierarchy,"现场旧工奴根及其子外观均未启用");
        Check(room.rpg==rpg && room.enemyManager==manager && room.isActiveAndEnabled && loc.rigRoot && head && rpg.GetComponent<RpgDamageReceiver>(),
            "已安装的祭坛房间使用现场 RPG、头部/移动根和 EnemyManager");
        await Until(()=>rpg.CanAct,"现场 RPG 初始化完毕，可进入真实房间，存档隔离："+rpg.saveFileName);
        var start=post.transform.position;
        var drop=post.patrolPoints.Single(t=>t.name=="示例落石").position;
        var axis=Vector3.ProjectOnPlane(drop-start,Vector3.up).normalized;
        MoveRig(loc,start-axis*3);
        Log("桌面模拟进入战区：head="+P(head.position)+" floor="+P(loc.PlayerFloorPosition)+" rig="+PathOf(loc.rigRoot));
        await Until(()=>room.HasStarted && basket.Stone && manager.ActiveEnemies.Values.Any(e=>e.EnemyId=="stone-carrier"),
            "玩家脚底进入祭坛后由 CombatRoomEncounter 自动出生、绑定现有 post 和 A 篮");
        var lease=manager.ActiveEnemies.Values.Single(e=>e.EnemyId=="stone-carrier");
        var carrier=lease.Controller.GetComponent<StoneCarrier>();var stone=carrier.Stone;
        var heavy=stone.GetComponent<GlyphHeavyBody>();var rb=stone.GetComponent<Rigidbody>();
        var filters=stone.GetComponentsInChildren<MeshFilter>().Where(f=>f.sharedMesh && f.TryGetComponent<Renderer>(out var renderer) && renderer.enabled).ToArray();
        int sid=stone.GetInstanceID(),bid=rb.GetInstanceID();
        var meshIds=filters.Select(f=>f.sharedMesh.GetInstanceID()).ToArray();
        Log("出生时 A4 活动渲染网格 ID="+string.Join(",",meshIds)+" 搬运者根="+P(lease.Controller.transform.position));
        var source=filters.Select(f=>AssetDatabase.GetAssetPath(f.sharedMesh)).Distinct().ToArray();
        Check(stone==basket.Stone && source.Any(path=>path.Contains("BallastStoneA4/") && path.EndsWith(".fbx",StringComparison.OrdinalIgnoreCase)),
            "正式 A4 外观随房间出生 lease="+lease.Id+" stoneID="+sid+" rigidbodyID="+bid+" source="+string.Join(";",source));
        await Until(()=>carrier.TryGetCarryPhase(out _),"工奴进入生产搬运 Animator 状态");
        await Awaitable.NextFrameAsync();carrier.TryGetCarryPhase(out var phase);
        Log("持姿 phase="+phase.ToString("F6")+" local="+P(stone.transform.localPosition)+" rotation="+stone.transform.localRotation+
            " profile="+AssetDatabase.GetAssetPath(carrier.profile)+" curve="+AssetDatabase.GetAssetPath(carrier.profile.carryPose));
        Check(stone.State==BallastStone.Phase.Carried && !heavy.CanManipulate && !basket.Boarded,
            "背载状态不能施力");
        Check(lease.Controller.Entity.ReceiveHit("A4普通受击",1,0,attacker:head) && carrier.Retreating,"正式非致命受击进入撤离");
        for(int i=0;i<400 && Vector3.ProjectOnPlane(lease.Controller.transform.position-drop,Vector3.up).magnitude>.54f;i++)await Fixed();
        var death=lease.Controller.transform.position;
        Check(Vector3.ProjectOnPlane(death-drop,Vector3.up).magnitude<.65f,"实际撤离至示例落石边缘 "+P(death));
        Check(lease.Controller.Entity.ReceiveHit("A4致命受击",lease.Controller.Entity.Combat.Health,0,attacker:head) && lease.Defeated &&
            stone.State==BallastStone.Phase.Dropped && stone.GetInstanceID()==sid && rb.GetInstanceID()==bid,"生产受击结算掉下原石/原刚体 ID="+sid+"/"+bid);
        await Until(()=>room.IsComplete,"原房间确认当轮清场");
        await Fixed(25);
        var from=stone.transform.position;
        Check(!basket.Boarded && !heavy.CanManipulate,"已清场但玩家尚未登篮，符力继续关闭，石心="+P(from));
        Vector3 toward=Vector3.ProjectOnPlane(slot.position-from,Vector3.up).normalized;
        var floorMarker=All().Single(t=>t.name=="示例槽心");var front=All().Single(t=>t.name=="A 上层篮口");
        MoveRig(loc,floorMarker.position+toward*.65f);await Fixed(3);
        Check(basket.Boarded && heavy.CanManipulate,"真实玩家代表点进入原乘员盒，符力开放；head="+P(head.position));
        trail.Add("sweep,step,stoneID,rigidbodyID,stoneX,stoneY,stoneZ,toSlot_m");
        for(int sweep=0;sweep<6 && count==0;sweep++)
        {
            bool lift=sweep==2 || sweep>=4;
            MoveRig(loc,lift?front.position+toward*.75f:floorMarker.position+toward*.65f);
            // 只合成笔尖、手与身体输入，所有目标锁定、力、碰撞、入槽和事件使用生产对象。
            var body=new Vector3(head.position.x,loc.PlayerFloorPosition.y+1.6f,head.position.z);
            var grip=body-toward*(lift?.22f:.38f)-Vector3.up*(lift?.02f:.58f);
            var tip=lift ? grip-toward*.34f-Vector3.up*.34f : body-toward*.9f-Vector3.up*.55f;
            var ctx=new GlyphEffectContext{owner=head,tracked=true,bodyTracked=true,hand=grip,tip=tip,body=body,
                forward=(stone.transform.position-tip).normalized,observed=Time.fixedTimeAsDouble};
            var action=glyph.Begin(ctx);
            try
            {
                action.Tick(ctx,Time.fixedDeltaTime);
                Log("Qianjun sweep="+(sweep+1)+" lock="+action.IsTethered+" tip="+P(tip)+" from="+P(stone.transform.position));
                if(!action.IsTethered)continue;
                for(int i=0;i<105 && count==0;i++)
                {
                    float dt=Time.fixedDeltaTime;
                    if(i*dt<(lift?.16f:.38f)) ctx.hand-=ctx.forward*(1.5f*dt);
                    ctx.observed+=dt;action.Tick(ctx,dt);action.FixedTick(dt);await Fixed();
                    var p=stone.transform.position;
                    if(i%3==0 || count>0)trail.Add(FormattableString.Invariant($"{sweep+1},{i},{sid},{bid},{p.x:F6},{p.y:F6},{p.z:F6},{Vector3.Distance(p,slot.position):F6}"));
                }
            }
            finally{action.End();}
        }
        await Fixed(10);
        Check(count==1 && stone.MovedByGlyph && stone.State==BallastStone.Phase.Docked && basket.Stone==stone &&
            stone.GetInstanceID()==sid && rb.GetInstanceID()==bid && stone.transform.parent==slot && rb.isKinematic &&
            meshIds.SequenceEqual(filters.Select(f=>f.sharedMesh.GetInstanceID())), "同一 A4 实例、刚体和渲染网格锁定，onDocked 恰好一次");
        Check(slot.GetInstanceID()==slotId && basket.GetInstanceID()==basketId && basket.passengerVolume.GetInstanceID()==volumeId &&
            Vector3.Distance(anchor,slot.position)<.0001f,"原篮、原乘员盒、原锁槽未替换；slot="+PathOf(slot)+" "+P(slot.position));
        MeasureClearance(stone,slot,filters);
    }

    static void MeasureClearance(BallastStone stone,Transform slot,MeshFilter[] filters)
    {
        Vector3 lo=Vector3.positiveInfinity,hi=Vector3.negativeInfinity;
        foreach(var f in filters)
        {
            using var read=UnityEditor.MeshUtility.AcquireReadOnlyMeshData(f.sharedMesh);
            using var vertices=new NativeArray<Vector3>(read[0].vertexCount,Allocator.Temp);read[0].GetVertices(vertices);
            var m=slot.worldToLocalMatrix*f.transform.localToWorldMatrix;
            foreach(var p in vertices){var q=m.MultiplyPoint3x4(p);lo=Vector3.Min(lo,q);hi=Vector3.Max(hi,q);}
        }
        var frame=slot.GetComponentInParent<Transform>();
        while(frame && !frame.name.StartsWith("A 配重槽"))frame=frame.parent;
        Check(frame,"锁槽由原 A 配重槽实体围合");
        float x0=float.NaN,x1=float.NaN,z0=float.NaN,z1=float.NaN,floor=float.NaN;
        foreach(var col in frame.GetComponents<BoxCollider>().Where(c=>c.enabled && !c.isTrigger))
        {
            var m=slot.worldToLocalMatrix*col.transform.localToWorldMatrix;
            var ctr=m.MultiplyPoint3x4(col.center); var min=Vector3.positiveInfinity;var max=Vector3.negativeInfinity;
            for(int i=0;i<8;i++)
            {
                var sign=new Vector3((i&1)==0?-.5f:.5f,(i&2)==0?-.5f:.5f,(i&4)==0?-.5f:.5f);
                var p=m.MultiplyPoint3x4(col.center+Vector3.Scale(sign,col.size));min=Vector3.Min(min,p);max=Vector3.Max(max,p);
            }
            if(Mathf.Abs(ctr.x)>.55f && Mathf.Abs(ctr.z)<.2f){if(ctr.x<0)x0=max.x;else x1=min.x;}
            else if(Mathf.Abs(ctr.z)>.55f && Mathf.Abs(ctr.x)<.2f){if(ctr.z<0)z0=max.z;else z1=min.z;}
            else if(Mathf.Abs(ctr.x)<.1f && Mathf.Abs(ctr.z)<.1f)floor=max.y;
        }
        var gaps=new[]{lo.x-x0,x1-hi.x,lo.z-z0,z1-hi.z};
        Check(gaps.All(d=>float.IsFinite(d) && d>.10f) && float.IsFinite(floor) && Mathf.Abs(lo.y-floor)<.0005f,
            "锁定态真实网格对四内壁净空 m="+string.Join(",",gaps.Select(v=>v.ToString("F7")))+" 底面离原槽底 m="+(lo.y-floor).ToString("F7")+
            " 石本地边界="+P(lo)+" / "+P(hi));
    }
}
