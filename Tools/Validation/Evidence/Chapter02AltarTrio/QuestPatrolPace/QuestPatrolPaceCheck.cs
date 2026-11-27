using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NarutoVR.SealCombat.Enemies;
using NarutoVR.SealCombat.Events;
using NarutoVR.SealCombat.Rpg;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;
using UnityEngine.SceneManagement;

/// <summary>在真实章节的原角色上观察预巡逻、追击、工奴受击逃离和首死；动画和导航始终由游戏更新。</summary>
[InitializeOnLoad]
public static class QuestPatrolPaceCheck
{
    const string Key="QuestPatrolPaceCheck.Run";
    static readonly List<string> result=new(), failures=new();
    static string Folder=>Environment.GetEnvironmentVariable("QUEST_PATROL_REPORT");
    static bool running;
    static double deadline;
    static string F(float x)=>x.ToString("F5",System.Globalization.CultureInfo.InvariantCulture);
    static string P(Vector3 p)=>p.ToString("F4");
    static float Flat(Vector3 a,Vector3 b)=>Vector3.ProjectOnPlane(a-b,Vector3.up).magnitude;
    static void Log(string s){result.Add(s);Debug.Log("QUEST_PATROL "+s);}
    static void Check(bool ok,string label){Log((ok?"PASS ":"FAIL ")+label);if(!ok)failures.Add(label);}
    static void Need(bool ok,string why){if(!ok)throw new InvalidOperationException(why);}
    static T One<T>() where T:Component=>SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<T>(true)).Single();
    static async Task Until(Func<bool> condition,string label,double seconds=65)
    {double stop=EditorApplication.timeSinceStartup+seconds;while(!condition() && EditorApplication.timeSinceStartup<stop)await Awaitable.NextFrameAsync();Need(condition(),label);}
    static QuestPatrolPaceCheck()=>EditorApplication.update+=Update;
    public static void Run()
    {
        Need(Application.isBatchMode && Application.dataPath.Replace('\\','/').EndsWith("/NarutoVR-CarrierQA-20261125/Assets",StringComparison.OrdinalIgnoreCase),"要求工奴独立副本");
        Directory.CreateDirectory(Folder);
        var scene=EditorSceneManager.OpenScene("Assets/Scene/Chapter02_SuspendedPath.unity");
        var xr=UnityEditor.XR.Management.XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Standalone);
        Need(xr && !xr.InitManagerOnStart && (!xr.Manager || !xr.Manager.activeLoader),"须先关闭本副本硬件XR自动加载");
        var room=Chapter02AltarPreviewWitness.ReadScene(scene,out var patched);Need(!patched,"须原章直接登记");
        room.rpg.autoLoad=false;room.rpg.saveFileName="qa-quest-patrol-"+Guid.NewGuid().ToString("N")+".json";
        File.WriteAllText(Path.Combine(Folder,"setup.txt"),"scene="+scene.path+"\npatchModified="+patched+"\nsave="+room.rpg.saveFileName+"\n");
        SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
    }
    static async void Update()
    {
        if(running && EditorApplication.timeSinceStartup>deadline){Log("FAIL watchdog");End(2);return;}
        if(!Application.isPlaying || !SessionState.GetBool(Key,false))return;
        SessionState.EraseBool(Key);running=true;deadline=EditorApplication.timeSinceStartup+320;
        try{await RunScenario();Log(failures.Count==0?"PASS TOTAL":"FAIL TOTAL "+failures.Count);End(failures.Count==0?0:1);}
        catch(Exception e){Log("FAIL EXCEPTION "+e);End(2);}
    }
    static void End(int code){running=false;File.WriteAllLines(Path.Combine(Folder,"assertions.txt"),result);EditorApplication.Exit(code);}
    sealed class Track : IDisposable
    {
        internal readonly EnemyLease l;
        internal readonly QuestActorGeometry geometry;
        internal int starts,hits,windows,visible,frames;
        internal float distance,furthest,peak;
        internal Vector3 origin,last;
        internal Track(EnemyLease l,BallastStone stone){this.l=l;geometry=new QuestActorGeometry(l.Controller,stone,Log);Reset();}
        internal void Reset(){origin=last=l.Controller.transform.position;distance=furthest=peak=0;visible=frames=0;}
        public void Dispose()=>geometry.Dispose();
    }
    sealed class LateProbe
    {
        internal static Action sample;
        internal static void AfterAnimation()=>sample?.Invoke();
        internal static PlayerLoopSystem Add()
        {
            var old=PlayerLoop.GetCurrentPlayerLoop();var changed=old;
            var top=(PlayerLoopSystem[])old.subSystemList.Clone();
            int i=Array.FindIndex(top,x=>x.type==typeof(PostLateUpdate));Need(i>=0,"无 PostLateUpdate");
            var tail=top[i];tail.subSystemList=tail.subSystemList.Concat(new[]{new PlayerLoopSystem{type=typeof(LateProbe),updateDelegate=AfterAnimation}}).ToArray();
            top[i]=tail;changed.subSystemList=top;PlayerLoop.SetPlayerLoop(changed);return old;
        }
    }
    static float Ground(EnemyController c)
    {
        var p=c.transform.position;
        var hits=Physics.RaycastAll(p+Vector3.up*.5f,Vector3.down,1.5f,~0,QueryTriggerInteraction.Ignore);
        var floor=hits.Where(h=>h.normal.y>.7f && !h.collider.GetComponentInParent<EnemyController>() && h.point.y<=p.y+.10f).OrderByDescending(h=>h.point.y).ToArray();
        return floor.Length>0?floor[0].point.y:float.NaN;
    }
    static void Shot(Camera camera,string name)
    {
        var prev=camera.targetTexture;var active=RenderTexture.active;var rt=RenderTexture.GetTemporary(800,450,24);
        var image=new Texture2D(800,450,TextureFormat.RGB24,false);
        try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,800,450),0,0);image.Apply();
            File.WriteAllBytes(Path.Combine(Folder,name+".png"),image.EncodeToPNG());}
        finally{camera.targetTexture=prev;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);UnityEngine.Object.Destroy(image);}
    }
    static async Task RunScenario()
    {
        Time.captureFramerate=30;
        var xr=UnityEngine.XR.Management.XRGeneralSettings.Instance;
        var displays=new List<UnityEngine.XR.XRDisplaySubsystem>();SubsystemManager.GetSubsystems(displays);
        Need(xr && !xr.InitManagerOnStart && (!xr.Manager || !xr.Manager.activeLoader) && displays.All(d=>!d.running),"发现活动硬件XR");
        Log("隔离Play硬件XR自动初始化=False，活动显示子系统=0，原XR玩家和双手保留。");
        var bridge=One<Chapter02BridgeEquipment>();var player=bridge.player;var post=One<StoneCarrierPost>();
        var room=SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<CombatRoomEncounter>(true)).Single(r=>r.spawnPoints.Any(s=>s.point==post.transform));
        var loc=player.locomotion;var head=loc.head;var camera=head.GetComponent<Camera>();Need(camera,"原头节点无 Camera");
        var rig=new Chapter02AltarPreviewWitness.Rig(player);var basket=post.basket;
        await Until(()=>player.CanAct,"真实新档未就绪");
        void Move(Vector3 f){loc.rigRoot.position+=f-loc.PlayerFloorPosition;Physics.SyncTransforms();}
        Move(player.CheckpointAt(1).floorPosition);
        await Until(()=>player.StageAt(player.StageIndex)==bridge.drawingStage && bridge.drawingObjective.IsRunning,"石案阶段");
        bridge.drawingObjective.Complete();await Until(()=>player.StageAt(player.StageIndex)==bridge.originalFirstStage && bridge.repairedDeck.activeInHierarchy,"桥教学目标回调未完成");
        Log("祭坛独立测试用正式绘桥目标的完成回调进入对应阶段；未模拟书写和指岸。");
        var line=post.transform.parent.Find("首次接敌线");var forward=Vector3.ProjectOnPlane(post.transform.position-line.position,Vector3.up).normalized;
        loc.rigRoot.RotateAround(head.position,Vector3.up,Vector3.SignedAngle(Vector3.ProjectOnPlane(head.forward,Vector3.up),forward,Vector3.up));
        Move(line.position-forward*2);
        await Until(()=>room.enemies.Length==3 && basket.Stone,"未生成预显三人");
        var original=new Chapter02AltarPreviewWitness(room,post);var stone=basket.Stone;var body=stone.GetComponent<Rigidbody>();
        var leases=room.enemyManager.ActiveEnemies.Values.Where(l=>room.enemies.Contains(l.Controller)).OrderBy(l=>l.Id).ToArray();
        var carrier=leases.Single(l=>l.EnemyId=="stone-carrier").Controller.GetComponent<StoneCarrier>();int clears=0;room.Cleared+=()=>clears++;
        Log("新档="+player.saveFileName+" 站点="+P(loc.PlayerFloorPosition)+" 模拟眼高="+F(head.position.y-loc.PlayerFloorPosition.y)+" 原摄像机="+camera.name+" 有效="+camera.isActiveAndEnabled);
        Log("线外 "+original.Ids);
        foreach(var l in leases)Log("配置 "+l.EnemyId+" patrol="+F(l.Controller.patrolSpeed)+" chase="+F(l.Controller.chaseSpeed)+" scale="+P(l.Controller.transform.lossyScale)+" nav-height="+F(l.Controller.Agent.height));
        var tracks=leases.Select(l=>new Track(l,l.EnemyId=="stone-carrier"?stone:null)).ToArray();
        var ranged=tracks.Single(t=>t.l.EnemyId=="zhihuocanji");
        var liveCores=new HashSet<int>();
        var coreOwner=typeof(EnemyFireCore).GetField("shooter",BindingFlags.NonPublic|BindingFlags.Instance);
        Need(coreOwner!=null,"火核源字段缺失");
        var receiver=player.GetComponent<RpgDamageReceiver>();Need(receiver,"玩家接收器缺失");
        using var starts=EventCenter.Subscribe(EnemyEvents.AttackStarted,n=>{var t=tracks.FirstOrDefault(x=>x.l.Controller==n.Enemy);if(t!=null)t.starts++;});
        using var windows=EventCenter.Subscribe(EnemyEvents.AttackWindowOpened,n=>{var t=tracks.FirstOrDefault(x=>x.l.Controller==n.Enemy);if(t!=null)t.windows++;});
        using var hits=EventCenter.Subscribe(EnemyEvents.DamageAccepted,n=>{
            var t=tracks.FirstOrDefault(x=>x.l.Controller.Entity==n.Source);
            if(t!=null && n.Receiver==receiver){t.hits++;Log($"实伤 frame={Time.frameCount} {t.l.EnemyId} {n.AttackId} target={n.Receiver.GetInstanceID()} amount={F(n.Damage)} point={P(n.Position)}");}
        });
        using var rows=new StreamWriter(Path.Combine(Folder,"frames.csv"));
        rows.WriteLine("phase,frame,time,enemy,lease,x,y,z,realspeed,navvelocity,status,attack,canHit,floorY,bodyMinY,bodyTopY,footSoleY,leftFootJointY,rightFootJointY,headBoneY,auxMinY,auxTopY,stoneMinY,stoneTopY,bodyEnabled,frustum,clearRay,playerEyeY,playerFloorY,playerHP,agentConfiguredSpeed,baseOffset,handDrivenTopY,withoutHandDrivenTopY,capsuleMinY,capsuleTopY");
        var oldLoop=LateProbe.Add();string phase="";float phaseStart=0;Exception failure=null;int lastFrame=-1;
        bool unexpectedCombat=false;
        LateProbe.sample=()=>{
            if(phase.Length==0 || lastFrame==Time.frameCount || failure!=null)return;
            lastFrame=Time.frameCount;
            try
            {
                foreach(var k in UnityEngine.Object.FindObjectsByType<EnemyFireCore>(FindObjectsSortMode.None))
                    if((EnemyController)coreOwner.GetValue(k)==ranged.l.Controller && liveCores.Add(k.GetInstanceID()))
                        Log($"新火核 frame={Time.frameCount} phase={phase} id={k.GetInstanceID()} shooter={ranged.l.Controller.GetInstanceID()} from={P(k.transform.position)}");
                foreach(var t in tracks)
                {
                    if(t.l.Defeated)continue;var c=t.l.Controller;var p=c.transform.position;var sample=t.geometry.Read(camera,player.transform);
                    var delta=Flat(t.last,p);t.last=p;t.distance+=delta;t.furthest=Mathf.Max(t.furthest,Flat(t.origin,p));float speed=delta/Time.deltaTime;t.peak=Mathf.Max(t.peak,speed);
                    t.frames++;if(sample.visible&&sample.frustum&&sample.unblocked)t.visible++;
                    if(phase=="preview" && (c.CanReceiveHit||c.CurrentAttack>=0||t.starts>0||t.windows>0||t.hits>0||player.locomotion.EncounterCombat))unexpectedCombat=true;
                    string H(QuestActorGeometry.Extent e,bool upper)=>e.count==0?"":F(upper?e.high.y:e.low.y);
                    rows.WriteLine(string.Join(",",phase,Time.frameCount,F(Time.time-phaseStart),t.l.EnemyId,t.l.Id,F(p.x),F(p.y),F(p.z),F(speed),F(c.Agent.velocity.magnitude),
                        c.CurrentBehaviour,c.CurrentAttack,c.CanReceiveHit,F(Ground(c)),H(sample.body,false),H(sample.body,true),H(sample.soles,false),F(sample.leftFoot),F(sample.rightFoot),F(sample.head),
                        H(sample.props,false),H(sample.props,true),H(sample.stone,false),H(sample.stone,true),sample.visible,sample.frustum,sample.unblocked,F(head.position.y),F(loc.PlayerFloorPosition.y),F(player.Player.Health),F(c.Agent.speed),F(c.Agent.baseOffset),
                        H(sample.handDriven,true),H(sample.withoutHandDriven,true),H(sample.capsule,false),H(sample.capsule,true)));
                }
            }catch(Exception e){failure=e;}
        };
        async Task Observe(string name,float seconds)
        {
            foreach(var t in tracks)t.Reset();phaseStart=Time.time;phase=name;bool imageSaved=false;
            while(Time.time-phaseStart<seconds && failure==null)
            {
                await Awaitable.NextFrameAsync();
                if(name=="preview" && !imageSaved && Time.time-phaseStart>=4)
                {
                    Shot(camera,"preview-camera");
                    // 同一游戏帧从同一水平位置单独渲染假设站姿；恢复后再推进时间，不修改玩家碰撞或寻路。
                    var eye=head.position;var floor=loc.PlayerFloorPosition;
                    try{
                        head.position=new Vector3(eye.x,floor.y+1.68f,eye.z);
                        int count=0;foreach(var t in tracks){var s=t.geometry.Read(camera,player.transform);if(s.visible&&s.frustum&&s.unblocked)count++;}
                        Shot(camera,"preview-alt-eye-168");Check(count>=2,$"1.68m对照眼位可见且无遮挡 {count}/3，点位={P(head.position)}");
                    }finally{head.position=eye;}
                    imageSaved=true;
                }
                Need(player.Player.Alive,"观察阶段玩家倒地："+name);
            }
            phase="";if(failure!=null)throw failure;
            foreach(var t in tracks.Where(t=>!t.l.Defeated))Log($"{name} {t.l.EnemyId} n={t.frames} path_m={F(t.distance)} away_m={F(t.furthest)} mean_mps={F(t.distance/seconds)} max_mps={F(t.peak)} visible={t.visible}/{t.frames} state={t.l.Controller.CurrentBehaviour}");
            rows.Flush();
        }
        try
        {
            var hp=player.Player.Health;
            await Observe("preview",8);
            foreach(var t in tracks)
            {
                var c=t.l.Controller;float enemyHp=c.Entity.Combat.Health;
                Check(!c.Entity.ReceiveHit("预显验证-"+t.l.Id,1,0,attacker:head) && c.Entity.Combat.Health==enemyHp,"线外拒绝实际受击 "+t.l.EnemyId);
                Check(t.visible>0,"线外启用身体网格、原摄像机视锥内、到躯干无遮挡 "+t.l.EnemyId);
                if(t.l.EnemyId!="zhihuocanji")
                {
                    Check(t.visible>=t.frames/2,"两名巡逻守敌各自至少半程可见 "+t.l.EnemyId+" "+t.visible+"/"+t.frames);
                    Check(t.furthest>=.7f,"8秒预巡逻地面净位移≥0.7m "+t.l.EnemyId+" 实际="+F(t.furthest));
                }
            }
            Check(!unexpectedCombat && liveCores.Count==0 && player.Player.Health==hp && !room.HasStarted,"进入前无人攻击、无火核、无伤害、未开战");
            Move(line.position+forward*3);
            await Until(()=>room.HasStarted && room.PlayerInside,"未接敌");original.CheckEngaged();Log("过线同批 "+original.Ids);
            await Observe("engaged",3.0f);
            Check(carrier.Stone==stone && carrier.GetComponent<EnemyController>().Entity.ReceiveHit("工奴实击",1,0,attacker:head) && carrier.Retreating,"非致命实际受击改为撤离");
            var worker=carrier.GetComponent<EnemyController>();
            var target=(Vector3)typeof(StoneCarrier).GetField("escape",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(carrier);
            var filter=new NavMeshQueryFilter{agentTypeID=worker.Agent.agentTypeID,areaMask=worker.Agent.areaMask};
            var marker=post.patrolPoints.FirstOrDefault(p=>p && NavMesh.SamplePosition(p.position,out var h,worker.navSampleRadius,filter) && Vector3.Distance(h.position,target)<.10f);
            var route=new NavMeshPath();float from=Flat(worker.transform.position,target);
            bool path=worker.Agent.CalculatePath(target,route) && route.status==NavMeshPathStatus.PathComplete;
            Check(marker && path,$"撤离目标与原巡逻点一致且完整可达 marker={(marker?marker.name:"缺失")} target={P(target)} path={route.status} corners={string.Join("/",route.corners.Select(P))} from={F(from)}");
            await Observe("retreat",2.6f);
            var w=tracks.Single(t=>t.l.EnemyId=="stone-carrier");
            Check(w.furthest>.2f,"工奴受击实际撤离 "+F(w.furthest)+"m");
            float after=Flat(worker.transform.position,target);Check(from-after>.18f,$"撤离实际接近该巡逻点 from={F(from)} to={F(after)}");
            if(liveCores.Count==0||ranged.hits==0)await Observe("attack-window",2);
            Check(liveCores.Count>0 && ranged.hits>0 && leases.All(l=>!l.Defeated),$"三人都在场时当前掷火实例新火核={liveCores.Count} 玩家实伤={ranged.hits} 攻击开始={ranged.starts}");
            foreach(var id in new[]{"stone-carrier","canjiou","zhihuocanji"})
            {
                var l=leases.Single(x=>x.EnemyId==id);Check(l.Controller.Entity.ReceiveHit("顺序击杀-"+id,l.Controller.Entity.Combat.Health,0,attacker:head)&&l.Defeated,"实际击杀 "+id);
                for(int i=0;i<4;i++)await Awaitable.NextFrameAsync();
                int count=leases.Count(x=>x.Defeated);
                Check(basket.Stone==stone && stone.GetComponent<Rigidbody>()==body && stone.State==BallastStone.Phase.Dropped && room.IsComplete==(count==3) && clears==(count==3?1:0),
                    $"第{count}亡 原落石={stone.GetInstanceID()}/{body.GetInstanceID()} 清场={room.IsComplete} 事件={clears}");
            }
            rig.Check();
        }finally{LateProbe.sample=null;PlayerLoop.SetPlayerLoop(oldLoop);foreach(var t in tracks)t.Dispose();}
    }
}
