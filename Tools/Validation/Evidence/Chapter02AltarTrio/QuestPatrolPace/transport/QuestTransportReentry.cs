using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NarutoVR.SealCombat.Enemies;
using NarutoVR.SealCombat.Items;
using NarutoVR.SealCombat.Rpg;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NarutoVR.SealCombat.Editor
{
// The observer is confined to this Play run. Feed tracked hand deltas immediately before the game's own
// ScriptRunBehaviourFixedUpdate so the real GlyphPenController dispatches physical forces to the original stone.
sealed class QuestTransportHandInput : IDisposable
{
    readonly UnityEngine.LowLevel.PlayerLoopSystem original;
    public Action sample;
    public QuestTransportHandInput()
    {
        original=UnityEngine.LowLevel.PlayerLoop.GetCurrentPlayerLoop();
        var modified=original;
        modified.subSystemList=original.subSystemList.ToArray();
        int fixedIndex=Array.FindIndex(modified.subSystemList,e=>e.type==typeof(UnityEngine.PlayerLoop.FixedUpdate));
        if(fixedIndex<0)throw new InvalidOperationException("No physics loop");
        var fixedLoop=modified.subSystemList[fixedIndex];
        var subs=fixedLoop.subSystemList.ToList();
        int script=subs.FindIndex(e=>e.type==typeof(UnityEngine.PlayerLoop.FixedUpdate.ScriptRunBehaviourFixedUpdate));
        if(script<0)throw new InvalidOperationException("No production FixedUpdate");
        subs.Insert(script,new UnityEngine.LowLevel.PlayerLoopSystem{type=typeof(QuestTransportHandInput),updateDelegate=Tick});
        fixedLoop.subSystemList=subs.ToArray();
        modified.subSystemList[fixedIndex]=fixedLoop;
        UnityEngine.LowLevel.PlayerLoop.SetPlayerLoop(modified);
    }
    void Tick()=>sample?.Invoke();
    public void Dispose(){sample=null;UnityEngine.LowLevel.PlayerLoop.SetPlayerLoop(original);}
}

[InitializeOnLoad]
public static class QuestTransportReentry
{
    const string Key="Ch02.QuestPatrol.Transport";
    static string Out=>Environment.GetEnvironmentVariable("CH02_PACE_TRANSPORT_OUT");
    static bool running;
    static double timeout;
    static QuestTransportReentry()=>EditorApplication.update+=Update;
    static Transform[] All()=>SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
    static T One<T>() where T:Component=>All().Select(t=>t.GetComponent<T>()).Single(c=>c);
    static string P(Vector3 p)=>p.ToString("F4");
    static void Log(string s){File.AppendAllText(Path.Combine(Out,"trio-free-pen.txt"),s+Environment.NewLine);Debug.Log("CH02_TRIO_FREE_PEN "+s);}
    static void Check(bool ok,string s){Log((ok?"PASS ":"FAIL ")+s);if(!ok)throw new InvalidOperationException(s);}
    static async Task Fixed(int n=1){for(int i=0;i<n;i++)await Awaitable.FixedUpdateAsync();}
    static async Task Until(Func<bool> test,string label,double limit=32)
    {
        var until=EditorApplication.timeSinceStartup+limit;
        while(!test() && EditorApplication.timeSinceStartup<until)await Task.Delay(12);
        Check(test(),label);
    }
    public static void Run()
    {
        if(!Application.isBatchMode || string.IsNullOrWhiteSpace(Out))throw new InvalidOperationException("Set CH02_PACE_TRANSPORT_OUT in isolated batch project");
        Directory.CreateDirectory(Out);
        var xr = UnityEditor.XR.Management.XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Standalone);
        if (!xr || xr.InitManagerOnStart || (xr.Manager && xr.Manager.activeLoader))
            throw new InvalidOperationException("私有XR隔离未就绪");
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("XR_RUNTIME_JSON")))
            throw new InvalidOperationException("未屏蔽硬件XR运行时");
        EditorSceneManager.OpenScene("Assets/Scene/Chapter02_SuspendedPath.unity");
        var p=One<LinearRpgController>();p.autoLoad=true;p.saveFileName="ch02-quest-transport-"+Guid.NewGuid().ToString("N")+".json";
        var path=Path.Combine(Application.persistentDataPath,p.saveFileName);
        File.Copy(Path.Combine(Out,"postrepair.json"),path);
        SessionState.SetString(Key,path);EditorApplication.EnterPlaymode();
    }
    static async void Update()
    {
        if(running && EditorApplication.timeSinceStartup>timeout){Log("FAIL watchdog");End(1);return;}
        if(running || !Application.isPlaying || SessionState.GetString(Key,"")=="")return;
        running=true;timeout=EditorApplication.timeSinceStartup+205;
        try{await Test();Log("PASS END");End(0);}catch(Exception e){Log("FAIL "+e);End(1);}
    }
    static void End(int code)
    {
        var path=SessionState.GetString(Key,"");SessionState.EraseString(Key);
        if(Path.GetFileName(path).StartsWith("ch02-quest-transport-") && File.Exists(path))File.Delete(path);
        running=false;EditorApplication.Exit(code);
    }
    static string Contacts(RpgLocomotionController loc)
    {
        const System.Reflection.BindingFlags flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
        var query=new object[]{loc.PlayerFloorPosition,default(RaycastHit),null};
        var type=typeof(RpgLocomotionController);
        if(!(bool)type.GetMethod("SmoothFloor",flags).Invoke(loc,query)) return "无地板 "+query[2];
        var cap=new object[]{(RaycastHit)query[1],loc.PlayerFloorPosition,0f,Vector3.zero,Vector3.zero,0f};
        type.GetMethod("SmoothCapsule",flags).Invoke(loc,cap);
        var hits=Physics.OverlapCapsule((Vector3)cap[3],(Vector3)cap[4],(float)cap[5],loc.obstacleLayers,QueryTriggerInteraction.Ignore)
            .Where(c=>!c.transform.IsChildOf(loc.rigRoot));
        string PathOf(Transform tr)=>tr.parent?PathOf(tr.parent)+"/"+tr.name:tr.name;
        return "floor="+PathOf(((RaycastHit)query[1]).collider.transform)+" hits=["+string.Join("; ",hits.Select(c=>PathOf(c.transform)+"#"+c.GetInstanceID()+" bounds="+c.bounds)) +"]";
    }

    static async Task Walk(RpgLocomotionController loc, Vector3 target, string label, Func<bool> stop = null)
    {
        var walk=typeof(RpgLocomotionController).GetMethod("WalkStep",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
        var start=loc.PlayerFloorPosition;
        int slow=0;
        for(int i=0;i<1100;i++)
        {
            var feet=loc.PlayerFloorPosition;
            var dir=Vector3.ProjectOnPlane(target-feet,Vector3.up);
            if(dir.magnitude<.045f || stop?.Invoke()==true) break;
            bool moved=(bool)walk.Invoke(loc,new object[]{Vector3.ClampMagnitude(dir,.065f),label});
            if(!moved)throw new InvalidOperationException(label+" 受阻 "+P(feet)+" → "+P(target)+" "+loc.Status+" "+Contacts(loc));
            var step=loc.PlayerFloorPosition-feet;
            slow=Vector3.Dot(step,dir.normalized)<.001f?slow+1:0;
            if(slow>=5 || Mathf.Abs(start.y-loc.PlayerFloorPosition.y)>.28f)
                throw new InvalidOperationException(label+" 脱离步行面或无进展 "+P(loc.PlayerFloorPosition));
            Physics.SyncTransforms();
            if(i%6==5 || stop!=null)await Awaitable.NextFrameAsync();
        }
        Check(stop?.Invoke()==true || Vector3.ProjectOnPlane(target-loc.PlayerFloorPosition,Vector3.up).magnitude<.06f,
            label+" "+P(start)+" → "+P(loc.PlayerFloorPosition));
    }

    // Probe original floor and obstruction rules, then execute the resulting small legs using production WalkStep.
    static async Task WalkDeck(RpgLocomotionController loc,Vector3 target,string label)
    {
        const System.Reflection.BindingFlags flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
        var type=typeof(RpgLocomotionController);
        var probe=type.GetMethod("SmoothFloor",flags);var clear=type.GetMethod("SmoothClear",flags);
        var leg=type.GetMethod("TrySmoothLeg",flags);var walk=type.GetMethod("WalkStep",flags);
        var start=loc.PlayerFloorPosition;
        var origin=new object[]{start,default(RaycastHit),null};
        if(!(bool)probe.Invoke(loc,origin))throw new InvalidOperationException(label+" 起点无地板 "+P(start));
        var originFloor=(RaycastHit)origin[1];
        var occ=new object[]{originFloor,start,0f,null,null};
        if(!(bool)clear.Invoke(loc,occ))throw new InvalidOperationException(label+" 起点占位 "+P(start)+" "+Contacts(loc));
        var from=new Dictionary<Vector2Int,RaycastHit>{{Vector2Int.zero,originFloor}};
        var prev=new Dictionary<Vector2Int,Vector2Int>();
        var pending=new List<Vector2Int>{Vector2Int.zero};
        bool Leg(RaycastHit a,Vector3 b,out RaycastHit end)
        {
            end=a;
            var d=Vector3.ProjectOnPlane(b-a.point,Vector3.up);
            int parts=Mathf.Max(1,Mathf.CeilToInt(d.magnitude/.055f));
            for(int segment=1;segment<=parts;segment++)
            {
                var next=a.point+d*((float)segment/parts);
                var args=new object[]{end,start,Vector3.ProjectOnPlane(next-end.point,Vector3.up),0f,default(RaycastHit),default(RaycastHit),null};
                if(!(bool)leg.Invoke(loc,args))return false;
                end=(RaycastHit)args[4];
                if(Mathf.Abs(end.point.y-start.y)>=.22f)return false;
            }
            return true;
        }
        const float spacing=.09f;
        Vector2Int finish=default;bool found=false;
        while(pending.Count>0 && from.Count<16000)
        {
            int best=0;
            for(int i=1;i<pending.Count;i++)
                if((from[pending[i]].point-target).sqrMagnitude<(from[pending[best]].point-target).sqrMagnitude)best=i;
            var key=pending[best];pending[best]=pending[pending.Count-1];pending.RemoveAt(pending.Count-1);
            if((from[key].point-target).sqrMagnitude<.18f*.18f && Leg(from[key],target,out _)){found=true;finish=key;break;}
            for(int x=-1;x<=1;x++)for(int z=-1;z<=1;z++)
            {
                if(x==0 && z==0)continue;
                var next=key+new Vector2Int(x,z);
                if(from.ContainsKey(next))continue;
                var position=start+new Vector3(next.x*spacing,0,next.y*spacing);
                if(position.x<Mathf.Min(start.x,target.x)-3f || position.x>Mathf.Max(start.x,target.x)+3f ||
                    position.z<Mathf.Min(start.z,target.z)-1.8f || position.z>Mathf.Max(start.z,target.z)+1.8f)continue;
                if(!Leg(from[key],position,out var end))continue;
                from.Add(next,end);prev.Add(next,key);pending.Add(next);
            }
        }
        if(!found)throw new InvalidOperationException(label+" 原地板没有可通行路径 "+P(start)+" → "+P(target)+" explored="+from.Count);
        var path=new List<Vector3>{target};
        for(var k=finish;k!=Vector2Int.zero;k=prev[k])path.Add(from[k].point);
        path.Reverse();int steps=0;
        foreach(var point in path)
        {
            for(int i=0;i<8;i++)
            {
                var d=Vector3.ProjectOnPlane(point-loc.PlayerFloorPosition,Vector3.up);
                if(d.magnitude<.004f)break;
                if(!(bool)walk.Invoke(loc,new object[]{Vector3.ClampMagnitude(d,.055f),label}))
                    throw new InvalidOperationException(label+" 实步受阻 "+P(loc.PlayerFloorPosition)+" → "+P(point)+" "+Contacts(loc));
                if(++steps%12==0)await Awaitable.NextFrameAsync();
            }
        }
        Physics.SyncTransforms();
        Check(Vector3.Distance(loc.PlayerFloorPosition,target)<.075f,label+" "+P(start)+" → "+P(loc.PlayerFloorPosition)+" 实步 "+steps+
            "，落脚实际胶囊 "+Contacts(loc));
    }

    static async Task Test()
    {
        File.WriteAllText(Path.Combine(Out,"trio-free-pen.txt"),"");
        File.WriteAllText(Path.Combine(Out,"trio-free-pen.csv"),"cast,step,stoneID,stoneX,stoneY,stoneZ,distance,locked"+Environment.NewLine);
        var xr = UnityEngine.XR.Management.XRGeneralSettings.Instance;
        var displays = new List<UnityEngine.XR.XRDisplaySubsystem>();
        SubsystemManager.GetSubsystems(displays);
        Check(xr && !xr.InitManagerOnStart && (!xr.Manager || !xr.Manager.activeLoader) && displays.All(d => !d.running),
            "桌面 Play 使用私有 XR 配置，活动 Loader 和运行中的头显显示子系统均为零");
        var lesson=One<Chapter02BridgeEquipment>();var p=lesson.player;var loc=p.locomotion;var head=loc.head;
        var post=One<StoneCarrierPost>();var basket=post.basket;var slot=basket.slot;
        var manager=One<EnemyManager>();var room=All().SelectMany(t=>t.GetComponents<CombatRoomEncounter>()).Single(r=>
            r.spawnPoints.Any(s=>s.enemyId=="stone-carrier" && s.point==post.transform));
        var pen=lesson.equipment.pen;var belt=lesson.equipment.interaction;var glyph=lesson.glyph;
        var home=(ToolBeltSlot)pen.home;
        await Until(()=>p.CanAct && belt.Initialised && pen.Dock==home && !home.IsSnapping,"修桥后存档恢复实笔与原腰槽");
        Check(p.StageAt(p.StageIndex).stageId=="CH02_REACH_HUB" && !lesson.brokenBarrier.activeInHierarchy &&
            lesson.repairedDeck.activeInHierarchy && !lesson.panel.IsWaiting,"本次进入真实修桥后的阶段");
        var matches=glyph.activeTemplates.Where(t=>t && t.effect is QianjunEffectDefinition).ToArray();
        Check(matches.Length==1,"玩家自由画板实际绑定一个千钧模板；全部可用模板="+string.Join(",",glyph.activeTemplates.Select(t=>t?t.SymbolKey:"missing")));
        var template=matches[0];var settings=(QianjunEffectDefinition)template.effect;
        Log("配置原引用 template="+AssetDatabase.GetAssetPath(template)+" effect="+AssetDatabase.GetAssetPath(settings));
        var drop=post.patrolPoints.Single(t=>t.name=="示例落石").position;
        var axis=Vector3.ProjectOnPlane(drop-post.transform.position,Vector3.up).normalized;
        var motion=One<Chapter02BasketMotion>();
        string[] types={"zhihuocanji","stone-carrier","canjiou"};
        Check(room.spawnPoints.Length==3 && types.All(kind=>room.spawnPoints.Count(point=>point.enemyId==kind && point.point && !point.isSummoned)==1) &&
            !room.HasStarted && !room.PlayerInside && !motion.IsMoving && !room.IsComplete,"旧档恢复后尚未交战，三个不同的正式非召唤出生点均在原房间");
        await Until(()=>types.All(kind=>manager.ActiveEnemies.Values.Count(e=>e.EnemyId==kind && !e.Defeated)==1) && basket.Stone,
            "读档后接入既有守敌预显，三只正式租约及背载 A4 已就位");
        var preview=types.ToDictionary(kind=>kind,kind=>manager.ActiveEnemies.Values.Single(e=>e.EnemyId==kind));
        var previewStone=basket.Stone;
        Check(preview.Values.All(e=>!e.Controller.CanReceiveHit) && !room.HasStarted && !room.IsComplete && !basket.Boarded &&
            previewStone.State==BallastStone.Phase.Carried && !previewStone.GetComponent<GlyphHeavyBody>().CanManipulate,
            "尚未入场，三敌受击锁定、A4 仍背载，入槽与符力锁定");
        int clears=0;
        room.Cleared+=()=>clears++;
        // Walk the established repaired route from the actual checkpoint. Only combat/aim stances below use desktop poses.
        await Walk(loc,post.transform.position,"读修桥档后经实桥走入祭坛",()=>room.HasStarted);
        await Until(()=>room.PlayerInside && types.All(kind=>manager.ActiveEnemies.Values.Count(e=>e.EnemyId==kind && !e.Defeated)==1) && basket.Stone,
            "原房间全体正式租约到场：远程、搬运、近战");
        var fighters=types.ToDictionary(kind=>kind,kind=>manager.ActiveEnemies.Values.Single(e=>e.EnemyId==kind && !e.Defeated));
        Check(types.All(kind=>ReferenceEquals(fighters[kind],preview[kind])) && previewStone==basket.Stone,
            "首次步入原房间直接启用同一批守敌租约与原 A4");
        var inside=post.transform.position-axis*3;
        await Walk(loc,inside,"沿生产地面走到交战位置");
        var lease=fighters["stone-carrier"];
        var carrier=lease.Controller.GetComponent<StoneCarrier>();var stone=carrier.Stone;var rb=stone.GetComponent<Rigidbody>();
        var heavy=stone.GetComponent<GlyphHeavyBody>();int id=stone.GetInstanceID(),rigid=rb.GetInstanceID(),events=0;
        basket.onDocked.AddListener(()=>events++);
        var basketStart=slot.position;
        Check(fighters.Values.Select(e=>e.Id).Distinct().Count()==3 &&
            basket.Stone==stone && stone.State==BallastStone.Phase.Carried && !heavy.CanManipulate,
            "三只敌人使用不同租约，A4 由原工奴背载，刚体="+rigid+" 石="+id);
        const string a4Source="Assets/Art/Enemies/CanJiOu/BallastStoneA4/T25-A4-Unity.fbx";
        var a4=stone.GetComponentsInChildren<MeshFilter>().Where(f=>f.sharedMesh && f.GetComponent<Renderer>() &&
            f.GetComponent<Renderer>().enabled && AssetDatabase.GetAssetPath(f.sharedMesh)==a4Source).ToArray();
        Check(a4.Length>0,"原镇石活动渲染件来自定稿 A4 FBX，网格实例编号="+string.Join(",",a4.Select(f=>f.sharedMesh.GetInstanceID())));
        Log("出场租约 "+string.Join(", ",types.Select(k=>k+"="+fighters[k].Id)));

        bool LiveIsOriginal(string kind)
        {
            var fighter=fighters[kind];
            return !fighter.Defeated && manager.IsCurrent(fighter) && fighter.Controller.CanReceiveHit;
        }
        void Defeat(string kind)
        {
            var fighter=fighters[kind];
            var entity=fighter.Controller.Entity;
            Check(LiveIsOriginal(kind) && entity.ReceiveHit("trio-free-pen."+kind,entity.Combat.Health,0,attacker:head) &&
                fighter.Defeated,"真实受击结算 "+kind+" 租约="+fighter.Id);
        }
        void CheckProgressBeforeClear(int deaths)
        {
            Check(fighters.Values.Count(e=>!e.Defeated)==3-deaths && fighters.Values.Where(e=>!e.Defeated).All(manager.IsCurrent) &&
                clears==0 && !room.IsComplete && !basket.Boarded && !heavy.CanManipulate && !stone.MovedByGlyph && events==0 &&
                !motion.IsMoving && basket.Stone==stone && stone.GetInstanceID()==id && rb.GetInstanceID()==rigid,
                $"已败 {deaths}/3，玩家尚在战场；未清场时原石不可施力、A篮未启动，石/刚体 {id}/{rigid}");
        }

        async Task ExitAndReenter(int defeated)
        {
            var survivors=types.Where(k=>!fighters[k].Defeated).ToArray();
            var life=survivors.ToDictionary(k=>k,k=>fighters[k].Controller.Entity.Combat.Health);
            var outside=room.area.transform.TransformPoint(room.area.center-Vector3.right*(room.area.size.x*.5f+1.1f));
            outside.y=inside.y;
            await Walk(loc,outside,"沿实际地面后退至接敌线外 "+defeated+"/3");
            await Until(()=>!room.PlayerInside && !loc.EncounterCombat,"实际房间已退战 "+defeated+"/3");
            Check(survivors.Length==3-defeated && survivors.All(k=>manager.IsCurrent(fighters[k]) && !fighters[k].Controller.CanReceiveHit &&
                !fighters[k].Controller.Entity.ReceiveHit("线外试击."+defeated+"."+k,1,0,attacker:head)) &&
                !room.IsComplete && clears==0 && stone.GetInstanceID()==id && rb.GetInstanceID()==rigid,
                "退场保留幸存租约、拒绝线外普通攻击、同一镇石与刚体 "+defeated+"/3");
            await Walk(loc,inside,"沿实际地面重新越线 "+defeated+"/3",()=>room.PlayerInside);
            await Until(()=>loc.EncounterCombat && survivors.All(LiveIsOriginal),"原房间与幸存者重新激活 "+defeated+"/3");
            Check(survivors.All(k=>fighters[k].Controller.Entity.Combat.Health==life[k]) && basket.Stone==stone &&
                previewStone==stone && stone.GetInstanceID()==id && rb.GetInstanceID()==rigid && events==0 && clears==0,
                "重入保持逐只生命、预览原石与原刚体 "+defeated+"/3");
        }

        await Until(()=>types.All(LiveIsOriginal) && carrier.TryGetCarryPhase(out _),"三敌可受击，工奴持石姿态有效");
        await ExitAndReenter(0);
        Defeat("zhihuocanji");
        await ExitAndReenter(1);
        CheckProgressBeforeClear(1);
        Check(stone.State==BallastStone.Phase.Carried && carrier.Stone==stone,"远程先死，A4 仍在搬运者身上");
        Check(lease.Controller.Entity.ReceiveHit("trio-free-pen.retreat",1,0,attacker:head) && carrier.Retreating,"原工奴未死，轻击触发原撤退");
        Log("落石前真实导航 工奴="+P(lease.Controller.transform.position)+" 示例点="+P(drop)+" 玩家="+P(head.position)+" 目的点="+P(lease.Controller.Agent.destination));
        await Until(()=>Vector3.ProjectOnPlane(lease.Controller.transform.position-drop,Vector3.up).magnitude<.54f,"撤到正式掉落边缘");
        Defeat("stone-carrier");
        Check(stone.State==BallastStone.Phase.Dropped && !rb.isKinematic,"搬运第二死，同一 A4 和刚体实物落地");
        CheckProgressBeforeClear(2);
        Defeat("canjiou");
        await Until(()=>room.IsComplete && clears==1 && !loc.EncounterCombat,"近战最后死，原战区首次且仅一次清场");
        Check(!motion.PlayerInA && !basket.Boarded && !heavy.CanManipulate && !motion.IsMoving && !stone.MovedByGlyph &&
            rb.GetInstanceID()==rigid && events==0,"清场后人在篮外，需登篮并实笔施符才能入槽");
        await Fixed(25);
        var toward=Vector3.ProjectOnPlane(slot.position-stone.transform.position,Vector3.up).normalized;
        var floor=All().Single(t=>t.name=="示例槽心");var front=All().Single(t=>t.name=="A 上层篮口");
        float sideSign=stone.transform.position.x>=slot.position.x?-1f:1f;
        Vector3 sideOffset=Vector3.right*sideSign;
        Vector3 Approach(float x,float z)=>new Vector3(front.position.x+x,front.position.y,front.position.z+z);
        await WalkDeck(loc,Approach(sideSign*2.0f,2.1f),"清场后沿祭坛实体地面绕开落石至篮前");
        await WalkDeck(loc,Approach(0,.15f),"走到上层原篮门中央");
        var reachStance=Approach(sideSign*.98f,-.55f);
        var rearStance=Approach(sideSign*.98f,-1.94f);
        await WalkDeck(loc,reachStance,"从原篮门步入槽侧甲板的远距施符位");
        await Until(()=>motion.PlayerInA && basket.Boarded && heavy.CanManipulate,"实体登篮之后允许同石受力");
        using var handFeed=new QuestTransportHandInput();
        var nibOffset=Quaternion.Inverse(pen.transform.rotation)*(pen.insertionTip.position-pen.GripPosition);
        var nibRot=Quaternion.Inverse(pen.transform.rotation)*pen.insertionTip.rotation;
        int matched=0;
        var trace=new List<string>();
        template.onCompleted.AddListener(board=>{if(board==glyph.activeBoard && !board.IsOpen){matched++;Log("MATCH "+template.SymbolKey+" from actual free board count="+matched);}});
        double now=Time.realtimeSinceStartupAsDouble;
        void Hand(Vector3 grip,Quaternion rot,bool pinch,double dt)
        {
            now=Math.Max(now+dt,Time.realtimeSinceStartupAsDouble);
            var observation=new PlayerHandObservation{rightValid=true,rightObserved=now,gripPoseValid=true,pinchValid=true,
                systemPinch=true,systemPressed=pinch,rightWrist=grip,rightTip=grip,gripPosition=grip,gripRotation=rot,
                headPosition=head.position,headForward=head.forward};
            glyph.Observe(observation,now);glyph.Advance(now);
        }
        async Task WriteGlyph()
        {
            await Until(()=>pen.Dock==home && !home.IsSnapping && !belt.Held,"开始书符时同一原笔仍在自己的腰槽");
            var at=pen.GripPosition;var rot=pen.transform.rotation;
            Hand(at,rot,false,.02);Hand(at,rot,true,.02);
            Check(pen.IsHeld && belt.OwnsHeldItem(pen) && glyph.IsHeld && glyph.activeBoard && glyph.activeBoard.IsOpen,
                "实体笔捏合离槽，自由板由正式取笔入口展开");
            var board=glyph.activeBoard;int count=matched;
            void Draw(Vector3 pt,double dt=.02)
            {
                var rotation=board.transform.rotation*Quaternion.Inverse(nibRot);
                Hand(board.transform.TransformPoint(pt)-rotation*nibOffset,rotation,true,dt);
            }
            foreach(var stroke in template.Preview())
            {
                Vector3 first=stroke[0]*.115f;
                Draw(first+Vector3.back*.055f);Draw(first+Vector3.forward*.001f);
                for(int k=1;k<stroke.Length;k++)for(int t=1;t<=12;t++)
                    Draw((Vector3)Vector2.Lerp(stroke[k-1],stroke[k],t/12f)*.115f+Vector3.forward*.001f);
                Draw(first+Vector3.back*.055f);
            }
            Check(board.PointCount>=3 && board.StrokeCount>0 && matched==count,"实际笔尖落墨，抬笔等待提交；points="+board.PointCount+" strokes="+board.StrokeCount);
            Draw(new Vector3(0,0,-.065f),board.idleSeconds+.12);
            Check(matched==count+1 && !board.IsOpen && glyph.IsHeld && belt.OwnsHeldItem(pen),
                "停笔经正式匹配器确认本次自由符，收板并保留实笔；similarity="+board.LastScore+" "+glyph.Status);
        }
        async Task ReturnPen()
        {
            var rot=home.opening.rotation*Quaternion.Euler(pen.stowEuler);
            var g=home.opening.position+head.forward*.11f;
            Hand(g,rot,true,.02);Hand(g,rot,false,.02);
            await Until(()=>pen.Dock==home && !pen.IsHeld && !home.IsSnapping && !belt.Held,"玩家松手回同一笔套，释放前一轮持笔认领");
        }
        try
        {
            float previousTravel=float.PositiveInfinity;
            for(int cast=0;cast<15 && events==0;cast++)
            {
                var hull=stone.GetComponent<BoxCollider>().bounds;
                Vector3 offset=hull.center-slot.position;
                float lateral=offset.x;
                bool near=Vector3.ProjectOnPlane(offset,Vector3.up).magnitude<3.2f;
                bool beyond=near && offset.z<-.45f;
                float spareWidth=Mathf.Clamp(.76f-hull.extents.x-.065f,.035f,.18f);
                bool align=near && offset.z>.8f && Mathf.Abs(lateral)>spareWidth;
                bool lift=previousTravel<.28f;
                float activeSide=align?-Mathf.Sign(lateral):sideSign;
                var stance=align?Approach(activeSide*.98f,-.50f):beyond?reachStance:near?rearStance:reachStance;
                float fromSide=loc.PlayerFloorPosition.x-slot.position.x, toSide=stance.x-slot.position.x;
                if(Mathf.Abs(fromSide)>.35f && Mathf.Abs(toSide)>.35f && fromSide*toSide<0)
                {
                    float tailZ=front.position.z-2.32f;
                    await WalkDeck(loc,new Vector3(slot.position.x+Mathf.Sign(fromSide)*.98f,front.position.y,tailZ),"沿当前侧甲板回到篮尾净空");
                    await WalkDeck(loc,new Vector3(slot.position.x+Mathf.Sign(toSide)*.98f,front.position.y,tailZ),"从篮尾完整横板绕到对侧");
                }
                else if(near && !align && Mathf.Abs(toSide)<.15f && Mathf.Abs(fromSide)>.35f)
                {
                    float tailZ=front.position.z-2.32f;
                    await WalkDeck(loc,new Vector3(loc.PlayerFloorPosition.x,front.position.y,tailZ),"沿侧甲板先退至槽后");
                    await WalkDeck(loc,new Vector3(stance.x,front.position.y,tailZ),"从实体尾板对准槽后瞄准位");
                }
                if(Vector3.ProjectOnPlane(loc.PlayerFloorPosition-stance,Vector3.up).magnitude>.04f)
                    await WalkDeck(loc,stance,"沿槽侧甲板退到本次牵引位置");
                await Fixed(2);
                Log("施符脚位实际胶囊 "+Contacts(loc));
                Check(basket.passengerVolume.bounds.Contains(head.position),"施符站在原 A 乘员盒 "+P(head.position));
                await WriteGlyph();
                var body=new Vector3(head.position.x,loc.PlayerFloorPosition.y+1.6f,head.position.z);
                // The nib can reach across the side deck while the body stays outside the metal rim.
                Vector3 tip;
                if(align)
                    tip=body-Vector3.right*activeSide*.16f+Vector3.forward*.61f+Vector3.up*.06f;
                else if(near && !beyond && !lift)
                    tip=new Vector3(slot.position.x + activeSide*.03f,body.y-.60f,body.z+.72f);
                else if(beyond)
                    tip=body-sideOffset*.72f+Vector3.forward*.31f-Vector3.up*.25f;
                else
                    tip=body-sideOffset*.72f+Vector3.forward*(lift?.60f:.78f)-Vector3.up*(lift?.10f:.44f);
                string action=align?"从对侧抬笔横向导正门口":beyond?"从门侧反向回拉已越槽实石":lift?"停滞后抬笔牵过坎":"向槽牵引";
                var side=Vector3.Cross(toward,Vector3.up).normalized;
                var dir=Vector3.zero;
                foreach(float dx in new[]{0f,.38f,-.38f})
                {
                    foreach(float dy in new[]{0f,.40f,-.28f})
                    {
                        var origin=hull.center+side*dx+Vector3.up*dy+toward*.42f;
                        var ray=(origin-tip).normalized;
                        var hits=Physics.RaycastAll(tip,ray,settings.reach,settings.aimMask,QueryTriggerInteraction.Ignore)
                            .Where(h=>!h.collider.transform.IsChildOf(loc.rigRoot) && !h.collider.transform.IsChildOf(pen.transform))
                            .OrderBy(h=>h.distance).ToArray();
                        if(hits.Length>0 && hits[0].rigidbody==rb){dir=ray;break;}
                    }
                    if(dir!=Vector3.zero)break;
                }
                if(dir==Vector3.zero)
                {
                    Log("第 "+(cast+1)+" 次符已画出，但实际笔尖方向均被遮挡；等待后重新取笔");
                    await ReturnPen();await Fixed(20);continue;
                }
                var orient=Quaternion.LookRotation(dir,Vector3.up)*Quaternion.Inverse(nibRot);
                var grip=tip-orient*nibOffset;
                Hand(grip,orient,true,.02);
                var line=loc.rigRoot.GetComponentsInChildren<LineRenderer>(true).SingleOrDefault(r=>r.enabled && r.name=="千钧符·磁墨牵引线");
                Check(line && line.positionCount==2 && Vector3.Distance(glyph.nib.position,tip)<.003f && Vector3.Angle(glyph.nib.forward,dir)<.1f,
                    "同一支实笔的笔尖射向原石，产生正式千钧墨线；tip="+P(glyph.nib.position)+" hit="+P(line?line.GetPosition(1):Vector3.zero));
                var initial=stone.transform.position;
                Log($"ACTION {action} cast={cast+1} 脚={P(stance)} 石={P(hull.center)} 槽={P(slot.position)} 横偏={lateral:F3} 门余量={spareWidth:F3} 笔={P(tip)} 受力初向={P(-dir)}");
                Vector3 initialGrip=grip;
                float correction=0f;
                int n=0;
                handFeed.sample=()=>
                {
                    if(events>0 || n>=105)return;
                    float dt=Time.fixedDeltaTime;
                    // Two neighboring tracked poses describe one physical hand displacement. Engine FixedUpdate applies all forces.
                    Hand(grip,orient,true,dt*.1f);
                    if(align && n*dt<.70f)
                    {
                        // A pillar blocks straight hauling when the 1.05m stone is off centre in the 1.52m opening.
                        // Use observed rigidbody motion to slow the same hand stroke as the stone reaches the centreline.
                        float error=slot.position.x-rb.worldCenterOfMass.x;
                        float pullX=(grip-line.GetPosition(1)).normalized.x;
                        float wished=Mathf.Clamp(error*3.1f,-.75f,.75f);
                        float handSpeed=Mathf.Abs(pullX)>.12f?Mathf.Clamp(.58f*(wished-rb.linearVelocity.x)/pullX,-1.15f,1.15f):0f;
                        correction=Mathf.Clamp(correction+handSpeed*dt,-.14f,.32f);
                        grip=initialGrip-dir*correction;
                    }
                    else if(!align && n*dt<(beyond?.18f:lift?.27f:near?.23f:.38f))
                        grip-=dir*((beyond?1.05f:near?1.15f:1.5f)*dt);
                    Hand(grip,orient,true,dt*.9f);
                    if(n++%3==0)trace.Add(FormattableString.Invariant($"{cast+1},{n},{id},{stone.transform.position.x:F5},{stone.transform.position.y:F5},{stone.transform.position.z:F5},{Vector3.Distance(stone.transform.position,slot.position):F5},{line.enabled}"));
                };
                while(n<105 && events==0)await Fixed();
                handFeed.sample=null;
                previousTravel=Vector3.Distance(initial,stone.transform.position);
                Log("PULL cast="+(cast+1)+" from="+P(initial)+" to="+P(stone.transform.position)+" toSlot="+Vector3.Distance(stone.transform.position,slot.position).ToString("F5")+
                    " movedByGlyph="+stone.MovedByGlyph+" id="+stone.GetInstanceID());
                if(events==0)await ReturnPen();
            }
            Check(matched>0 && events==1 && basket.Stone==stone && stone.MovedByGlyph && stone.GetInstanceID()==id &&
                rb.GetInstanceID()==rigid && rb.isKinematic && stone.State==BallastStone.Phase.Docked &&
                stone.transform.parent==slot && stone.transform.localPosition.sqrMagnitude<1e-6f,
                "从自由画板经自然玩家施符和物理牵引至真实入槽仅一次，前后同一镇石/刚体；matched="+matched+" events="+events+" id="+id+"/"+rigid);
        }
        finally
        {
            handFeed.sample=null;
            File.AppendAllLines(Path.Combine(Out,"trio-free-pen.csv"),trace);
        }

        Check(clears==1 && events==1 && p.Player.Alive && motion.PlayerInA,"入槽时原乘员与同一 A4 都在篮，三敌清场只发过一次");
        await Until(()=>motion.IsMoving || motion.AtMiddle,"入槽事件经正式运输开始 A 降程");
        var rideOffset=loc.rigRoot.position-slot.position;
        var rigRotation=loc.rigRoot.rotation;
        bool midSmoke=false;
        var curtain=motion.comfortCurtain.GetComponent<CanvasGroup>();
        while(!motion.AtMiddle)
        {
            midSmoke |= motion.IsMoving && curtain.alpha>.95f;
            if(!motion.IsMoving || Vector3.Distance(loc.rigRoot.position-slot.position,rideOffset)>.035f ||
                Quaternion.Angle(loc.rigRoot.rotation,rigRotation)>.1f) throw new InvalidOperationException("A 中途脱离原篮或玩家被强制转向");
            await Fixed();
        }
        var mid=motion.middleCheckpoint.floorPosition;
        Check(midSmoke && !motion.IsMoving && !motion.comfortCurtain.activeSelf && motion.PlayerInA &&
            Mathf.Abs(loc.PlayerFloorPosition.y-mid.y)<.08f && Mathf.Abs(slot.position.y-basketStart.y+80f)<.03f &&
            stone.State==BallastStone.Phase.Docked && basket.Stone==stone && rb.GetInstanceID()==rigid &&
            stone.transform.parent==slot && events==1 && a4.All(f=>f && f.gameObject.activeInHierarchy && f.GetComponent<Renderer>().enabled && f.transform.IsChildOf(stone.transform)),"原乘员及同一 A4 完成 80m 落差，原篮停稳，定目帘与移动解锁");
        if(pen.IsHeld) await ReturnPen();
        await Until(()=>pen.Dock==home && !home.IsSnapping && !belt.Held,"中层已收回原笔，步行不占手");
        var sill=All().Single(t=>t.name=="A 中层篮口").position;
        Log("落稳起步实际胶囊 "+Contacts(loc));
        await WalkDeck(loc,motion.middleCheckpoint.floorPosition,"沿原侧甲板绕槽，经 A 中层篮口走上西栈道");
        Check(!motion.PlayerInA && motion.AtMiddle && p.CurrentStageCheckpoint==motion.middleCheckpoint &&
            room.IsComplete && clears==1 && events==1 && stone.GetInstanceID()==id && rb.GetInstanceID()==rigid &&
            Vector3.Distance(mid,loc.PlayerFloorPosition)<.09f,"脚下为静态中层存档点，人物已走出 A 篮，石和刚体仍是原物");
    }
}
}


