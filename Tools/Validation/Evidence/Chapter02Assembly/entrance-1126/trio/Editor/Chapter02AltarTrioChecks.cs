using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NarutoVR.SealCombat.Enemies;
using NarutoVR.SealCombat.Items;
using NarutoVR.SealCombat.Rpg;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace NarutoVR.SealCombat.Editor
{
[InitializeOnLoad]
public static class Chapter02AltarTrioChecks
{
    const string Key="Ch02.Entry.Trio.QA";
    static readonly List<string> notes=new(), problems=new();
    static readonly string[] Ids={"stone-carrier","canjiou","zhihuocanji"};
    static string Order=>SessionState.GetString(Key+".order","primary");
    static string Root=>Environment.GetEnvironmentVariable("CH02_TRIO_QA_OUT");
    static string Out=>Path.Combine(Root,Order);
    static int[] equipment;
    static Dictionary<string,EnemyLease> preview;
    static int previewStone;
    static int[] guard;
    static bool running;
    static double deadline;
    static readonly MethodInfo walk=typeof(RpgLocomotionController).GetMethod("WalkStep",BindingFlags.NonPublic|BindingFlags.Instance);
    static Chapter02AltarTrioChecks()=>EditorApplication.update+=Update;
    public static void Run()=>Begin("primary");
    public static void Alternate()=>Begin("alternate");
    static void Begin(string order)
    {
        if (!Application.isBatchMode || string.IsNullOrWhiteSpace(Root) || SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)
            throw new InvalidOperationException("Use isolated Unity -batchmode -force-d3d11 with CH02_TRIO_QA_OUT, without -nographics or -quit");
        SessionState.SetString(Key+".order",order);
        Directory.CreateDirectory(Out);
        EditorSceneManager.OpenScene("Assets/Scene/Chapter02_SuspendedPath.unity");
        var p=One<LinearRpgController>();
        p.autoLoad=true;
        p.saveFileName="ch02-trio-"+Guid.NewGuid().ToString("N")+".json";
        var path=Path.Combine(Application.persistentDataPath,p.saveFileName);
        if(File.Exists(path)) throw new IOException("Expected a wholly new save path");
        SessionState.SetString(Key,path);
        EditorApplication.EnterPlaymode();
    }
    static async void Update()
    {
        if(running && EditorApplication.timeSinceStartup>deadline){Log("FAIL watchdog"); End(1);return;}
        if(running || !Application.isPlaying || SessionState.GetString(Key,"")=="")return;
        running=true;deadline=EditorApplication.timeSinceStartup+255;
        try { await Test(); Log(problems.Count==0 ? "PASS END" : "FAIL END "+string.Join(" / ",problems)); End(problems.Count==0?0:1); }
        catch(Exception ex){Log("FAIL "+ex);End(1);}
    }
    static void End(int code)
    {
        var path=SessionState.GetString(Key,"");
        SessionState.EraseString(Key);
        if(Path.GetFileName(path).StartsWith("ch02-trio-") && File.Exists(path))File.Delete(path);
        running=false;
        EditorApplication.Exit(code);
    }
    static void Log(string s){notes.Add(s);File.AppendAllText(Path.Combine(Out,"trio.txt"),s+Environment.NewLine);Debug.Log("CH02_TRIO "+s);}
    static void Assert(bool ok,string s){Log((ok?"PASS ":"FAIL ")+s);if(!ok)throw new InvalidOperationException(s);}
    static void Observe(bool ok,string s){Log((ok?"PASS ":"FAIL ")+s);if(!ok)problems.Add(s);}
    static string P(Vector3 p)=>p.ToString("F4");
    static string PathOf(Transform t)=>t.parent ? PathOf(t.parent)+"/"+t.name : t.name;
    static T[] SceneObjects<T>() where T:Component=>SceneManager.GetActiveScene().GetRootGameObjects()
        .SelectMany(g=>g.GetComponentsInChildren<T>(true)).ToArray();
    static T One<T>() where T:Component=>SceneObjects<T>().Single();
    static async Task Frame()=>await Awaitable.NextFrameAsync();
    static async Task Until(Func<bool> condition,string name,double limit=20)
    {
        var stop=EditorApplication.timeSinceStartup+limit;
        while(!condition() && EditorApplication.timeSinceStartup<stop)await Frame();
        Assert(condition(),name);
    }
    static Renderer[] Active(Renderer[] list)=>list.Where(r=>r && r.enabled && !r.forceRenderingOff && r.gameObject.activeInHierarchy).ToArray();
    static Mesh MeshOf(Renderer r)=>r is SkinnedMeshRenderer skin ? skin.sharedMesh : r.TryGetComponent<MeshFilter>(out var f) ? f.sharedMesh : null;
    static string Asset(Object o)=>o?AssetDatabase.GetAssetPath(o):"missing";
    static async Task Test()
    {
        File.WriteAllText(Path.Combine(Out,"trio.txt"),"");
        var p=One<LinearRpgController>();var loc=p.locomotion;
        var lesson=One<Chapter02BridgeEquipment>();var panel=lesson.panel;var board=panel.board;
        var post=One<StoneCarrierPost>();var manager=One<EnemyManager>();
        var room=SceneObjects<CombatRoomEncounter>().Single(r=>r.spawnPoints.Any(s=>s.enemyId=="stone-carrier" && s.point==post.transform));
        var cam=loc.head.GetComponent<Camera>();
        Assert(cam && cam.isActiveAndEnabled && cam.gameObject==loc.head.gameObject,"真实玩家 Game 相机 "+PathOf(cam.transform));
        await Until(()=>p.IsReady && p.CanAct && lesson.equipment.interaction.Initialised,"全新玩家与原工具带初始化");
        await Frame();
        Assert(p.StageIndex==0 && p.SavePath==SessionState.GetString(Key,"") &&
            Vector3.Distance(loc.PlayerFloorPosition,p.CheckpointAt(0).floorPosition)<.08f,
            "从正式出生点开始，独立新档未复制存档；floor="+P(loc.PlayerFloorPosition));
        Assert(!room.HasStarted,"出生点未触发战斗");
        equipment=Equipment(p); guard=Workshop(p,manager);
        Assert(room.spawnPoints.Length==3 && Ids.All(id=>room.spawnPoints.Count(s=>s.enemyId==id && s.point)==1),"祭坛恰有三种真实生产出生点");
        Log("CAMERA eye="+P(cam.transform.position)+" forward="+P(cam.transform.forward)+" fov="+cam.fieldOfView+
            " mask="+cam.cullingMask+" GPU="+SystemInfo.graphicsDeviceName+" pipeline="+GraphicsSettings.currentRenderPipeline);
        var stand=p.CheckpointAt(1).floorPosition;
        var zone=SceneObjects<ReachAreaChapterObjective>().Single(r=>r.IsRunning);
        Log("APPROACH node="+P(stand)+" bounds="+zone.area.bounds+" room="+PathOf(room.transform)+" area="+room.area.bounds+" carrierPost="+P(post.transform.position));
        bool BoardVis()=>Active(board.GetComponentsInChildren<Renderer>(true)).Length>0;
        bool hiddenAtEntry=!board.IsOpen && !panel.IsWaiting && !BoardVis();
        Shot(cam,"01-fresh-entry");
        // Attempting the normal facility callback early must leave its visual and button unavailable.
        lesson.OpenPanel();
        Observe(hiddenAtEntry && !lesson.button.gameObject.activeInHierarchy && !board.IsOpen && !BoardVis(),
            "出生时画符面板实际渲染件隐藏，提前尝试调用不能展开");
        int steps=0;float walked=0;
        async Task Step(Vector3 target)
        {
            var from=loc.PlayerFloorPosition;
            var dir=Vector3.ClampMagnitude(Vector3.ProjectOnPlane(target-from,Vector3.up),.065f);
            if(!(bool)walk.Invoke(loc,new object[]{dir,"QA 入口步行"}))
                throw new InvalidOperationException("步行受阻 "+P(from)+" → "+P(target)+" "+loc.Status);
            var delta=Vector3.Distance(from,loc.PlayerFloorPosition);
            if(delta<.0005f)throw new InvalidOperationException("步行无位移 "+P(from));
            walked+=delta;steps++; Physics.SyncTransforms(); await Frame();
        }
        bool early=false;
        while(Vector3.ProjectOnPlane(loc.PlayerFloorPosition-stand,Vector3.up).magnitude>.035f && steps<360)
        {
            if(!ReachAreaChapterObjective.ContainsFloor(zone.area,loc.PlayerFloorPosition) && p.StageIndex==0)
                early|=board.IsOpen || BoardVis() || lesson.button.gameObject.activeInHierarchy;
            await Step(stand);
        }
        Observe(!early,"沿真实地板到达绘制区域前，面板及按钮均未显示");
        await Until(()=>p.StageIndex==1 && lesson.button.gameObject.activeInHierarchy &&
            lesson.equipment.pen.Dock && !lesson.equipment.pen.Dock.IsSnapping,"抵达绘制位开放取笔与铜扣");
        await Until(()=>Ids.All(id=>manager.ActiveEnemies.Values.Count(e=>e.EnemyId==id && !e.Defeated)==1),"绘符前桥头已能看到三名生产工奴/守备的实际实例",24);
        preview=Ids.ToDictionary(id=>id,id=>manager.ActiveEnemies.Values.Single(e=>e.EnemyId==id && !e.Defeated));
        var originalStone=preview["stone-carrier"].Controller.GetComponent<StoneCarrier>().Stone;
        Assert(originalStone,"桥头原工奴身上有真实镇石");
        previewStone=originalStone.GetInstanceID(); starts=contacts=0;
        preview["zhihuocanji"].Controller.onAttackStarted.AddListener(_=>starts++);
        preview["zhihuocanji"].Controller.onAttackContact.AddListener(_=>contacts++);
        Observe(!room.HasStarted && preview.Values.All(e=>!e.Controller.CanReceiveHit && e.Controller.CurrentBehaviour!=EnemyController.Behaviour.Attack) &&
            !SceneObjects<EnemyFireCore>().Any(f=>f.isActiveAndEnabled),"绘符位战斗未激活，三敌未攻击或发射火核");
        Log("PREVIEW stone="+previewStone+" ids="+string.Join(" | ",preview.Select(kv=>Describe(kv.Value))));
        if(Order=="primary") TrioPhoto(cam,"02-bridge-before-writing",preview,originalStone);
        Shot(cam,"02-at-broken-bridge");
        var glyph=lesson.glyph;var item=lesson.equipment.pen;var head=loc.head;
        var atButton=lesson.button.RestSurface;var axis=lesson.button.cap.forward;
        var clock=Time.realtimeSinceStartupAsDouble;
        void Hand(Vector3 grip,Quaternion rot,bool down,double add=.02)
        {
            clock=Math.Max(clock+add,Time.realtimeSinceStartupAsDouble);
            var obs=new PlayerHandObservation{rightValid=true,rightObserved=clock,gripPoseValid=true,pinchValid=true,
                systemPinch=true,systemPressed=down,headPosition=head.position,headForward=head.forward,
                rightTip=grip,rightWrist=grip,gripPosition=grip,gripRotation=rot};
            glyph.Observe(obs,clock);glyph.Advance(clock);
        }
        Hand(item.GripPosition,item.transform.rotation,false); Hand(item.GripPosition,item.transform.rotation,true);
        Assert(lesson.equipment.interaction.OwnsHeldItem(item) && glyph.IsHeld,"真实手势观测从腰槽拿取同一实笔");
        lesson.button.Sample(atButton-axis*.012f);lesson.button.Sample(atButton+axis*.003f);lesson.button.Sample(atButton-axis*.012f);
        Assert(board.IsOpen && panel.IsWaiting,"按实体铜扣才打开方直之矩；template="+Asset(panel.template)+" floor="+P(loc.PlayerFloorPosition));
        Shot(cam,"03-panel-open-player-direction");
        int completed=0;panel.onCompleted.AddListener(()=>completed++);
        void Draw(Vector3 pt,double delay=.02)
        {
            var rot=board.transform.rotation;
            Hand(board.transform.TransformPoint(pt)-rot*Vector3.forward*.108f,rot,true,delay);
        }
        foreach(var stroke in panel.template.Preview())
        {
            Vector3 start=stroke[0]*.115f;
            Draw(start+Vector3.back*.055f); Draw(start+Vector3.forward*.001f);
            for(int i=1;i<stroke.Length;i++) for(int n=1;n<=12;n++)
                Draw((Vector3)Vector2.Lerp(stroke[i-1],stroke[i],n/12f)*.115f+Vector3.forward*.001f);
            Draw(new Vector3(0,0,-.055f));
        }
        Draw(new Vector3(0,0,-.055f),board.idleSeconds+.1);
        Assert(panel.IsResolving && lesson.brokenBarrier.activeInHierarchy && !lesson.repairedDeck.activeInHierarchy,
            "原笔尖模板轨迹匹配成功，结算前仍为断桥");
        glyph.Advance(clock+board.successDuration+.15);
        await Until(()=>p.StageIndex==2 && !lesson.brokenBarrier.activeInHierarchy && lesson.repairedDeck.activeInHierarchy,"方直之矩结算，正式修复桥面开启");
        Observe(completed==1 && !board.IsOpen && !BoardVis(),"成功只结算一次并收起视图");
        Shot(cam,"04-bridge-repaired");
        var home=(ToolBeltSlot)item.home;
        var pos=home.opening.position+head.forward*.11f;var closeRot=home.opening.rotation*Quaternion.Euler(item.stowEuler);
        // Match the previous test's clock jump before issuing the first post-match open-hand observation.
        clock=Math.Max(clock+board.successDuration+.16,Time.realtimeSinceStartupAsDouble);
        Hand(pos,closeRot,true);Hand(pos,closeRot,false);
        var previous=loc.PlayerFloorPosition; bool deckContact=false,encounterBeforeDeck=false;
        var bridgeFoot=loc.PlayerFloorPosition;
        while(!room.HasStarted && steps<1100 && Vector3.ProjectOnPlane(loc.PlayerFloorPosition-post.transform.position,Vector3.up).magnitude>.5f)
        {
            previous=loc.PlayerFloorPosition;
            await Step(post.transform.position);
            if(Physics.Raycast(loc.PlayerFloorPosition+Vector3.up*.45f,Vector3.down,out var hit,.8f,~0,QueryTriggerInteraction.Ignore) &&
               hit.collider.transform.IsChildOf(lesson.repairedDeck.transform)) deckContact=true;
            if(!deckContact && room.HasStarted)encounterBeforeDeck=true;
        }
        Assert(deckContact && !encounterBeforeDeck,"真实玩家沿修复后的实体桥面跨过缺口");
        Assert(room.HasStarted,"实际进入祭坛首次启动房间，前一脚="+P(previous)+" 首次触发脚="+P(loc.PlayerFloorPosition)+
            " camera="+P(cam.transform.position)+" cameraForward="+P(cam.transform.forward)+" walked="+walked.ToString("F3")+"m steps="+steps);

        await Until(()=>Ids.All(id=>manager.ActiveEnemies.Values.Count(e=>e.EnemyId==id && e.Controller.CanReceiveHit)==1),"接敌后三个原租约被激活，三种实体可受击");
        var team=Ids.ToDictionary(id=>id,id=>manager.ActiveEnemies.Values.Single(e=>e.EnemyId==id));
        var carrier=team["stone-carrier"].Controller.GetComponent<StoneCarrier>();
        var stone=carrier.Stone;
        foreach(var id in Ids)
        {
            Assert(ReferenceEquals(preview[id],team[id]) && preview[id].Id==team[id].Id &&
                preview[id].Controller.GetInstanceID()==team[id].Controller.GetInstanceID() && manager.IsCurrent(team[id]),
                "桥头展示→开战沿用同一人 "+Describe(team[id]));
            var def=manager.catalog.enemies.Single(d=>d.enemyId==id);
            Log("ASSET "+id+" prefab="+AssetDatabase.GUIDToAssetPath(def.prefab.AssetGUID)+" combat="+Asset(def.combatDefinition)+
                " controller="+Asset(team[id].Controller.animator.runtimeAnimatorController)+" actualSpawn="+P(room.spawnPoints.Single(s=>s.enemyId==id).point.position));
        }
        Assert(manager.ActiveEnemies.Count==3 && stone.GetInstanceID()==previewStone && stone==post.basket.Stone &&
            stone.State==BallastStone.Phase.Carried,"首遇无重复角色，三名玩家先前所见的原租约及同一镇石 "+previewStone);
        if(Order=="primary")TrioPhoto(cam,"05-first-altar-trio",team,stone);
        var ordinary=team["canjiou"].Controller;var ranged=team["zhihuocanji"].Controller;
        var fire=ranged.GetComponent<EnemyRangedAttack>();
        Assert(fire && fire.IsConfigured && fire.projectile && fire.muzzle,"掷火敌使用原炮口与火核预置体 "+Asset(fire.projectile)+" muzzle="+PathOf(fire.muzzle));
        var ordinaryStart=ordinary.transform.position;
        bool chase=false, flight=false; EnemyFireCore core=null;Vector3 firstShot=default;
        float moved=0,progress=0,flightMeters=0;string source="";
        var stop=EditorApplication.timeSinceStartup+14;
        while(EditorApplication.timeSinceStartup<stop && (!chase || !flight))
        {
            await Frame();
            var move=Vector3.ProjectOnPlane(ordinary.transform.position-ordinaryStart,Vector3.up);
            var heading=Vector3.ProjectOnPlane(loc.head.position-ordinaryStart,Vector3.up).normalized;
            moved=Mathf.Max(moved,move.magnitude);progress=Mathf.Max(progress,Vector3.Dot(heading,move));
            chase|=ordinary.HasTarget && ordinary.HasEverDetectedPlayer && ordinary.CurrentBehaviour==EnemyController.Behaviour.Chase && progress>.13f;
            if(!core)
            {
                core=Object.FindObjectsByType<EnemyFireCore>(FindObjectsSortMode.None).FirstOrDefault(c=>c.isActiveAndEnabled &&
                    ReferenceEquals(Field<EnemyController>(c,"shooter"),ranged));
                if(core){firstShot=core.transform.position;source=Field<string>(core,"attackId");}
            }
            else
            {
                flightMeters=Mathf.Max(flightMeters,Vector3.Distance(firstShot,core.transform.position));
                flight|=starts>0 && contacts>0 && flightMeters>.06f && Field<float>(core,"speed")>0 &&
                    Field<float>(core,"damage")>0 && Field<float>(core,"remaining")>0 && !string.IsNullOrEmpty(source);
            }
        }
        Observe(chase,"普通残机偶正常检测、锁定并实际朝玩家追击；start="+P(ordinaryStart)+" offset="+moved.ToString("F3")+" forwardProgress="+progress.ToString("F3")+" state="+ordinary.CurrentBehaviour);
        Observe(flight,"掷火敌原动画开窗后由原炮口发出有效实弹，实弹引用本尊且向前飞行；starts="+starts+" windows="+contacts+
            " fireId="+source+" position="+P(firstShot)+" travel="+flightMeters.ToString("F3")+" playerHP="+p.Player.Health);
        Assert(p.CanAct && !room.IsComplete,"玩家仍可行动，三敌尚在房间中");
        var ctl=team["stone-carrier"].Controller;float hp=ctl.Entity.Combat.Health;
        Assert(ctl.Entity.ReceiveHit("QA 工奴负重撤离",1,0,attacker:cam.transform) && ctl.Entity.Combat.Health<hp && carrier.Retreating,
            "原工奴非致命受击，保留镇石并开始撤离");
        await Until(()=>carrier.TryGetCarryPhase(out _),"真实持石 Animator 已进入负重剪辑",8);
        carrier.TryGetCarryPhase(out var phaseA);
        var rootA=ctl.transform.position;var poseA=stone.transform.localPosition;var rotA=stone.transform.localRotation;
        var ctlAnim=ctl.animator;var clip=carrier.profile.carryPose.Clip;
        var body=Body(team["stone-carrier"],stone);
        if(Order=="primary")ClosePhoto(cam,"06-carrier-carry-early",body,stone);
        await Until(()=>carrier.TryGetCarryPhase(out var phaseB) && Mathf.Abs(Mathf.DeltaAngle(phaseA*360,phaseB*360))>12f &&
            Vector3.ProjectOnPlane(ctl.transform.position-rootA,Vector3.up).magnitude>.12f,"原工奴负重行走时动画相位和自身位置均前进",8);
        carrier.TryGetCarryPhase(out var phaseEnd);
        Observe(stone==carrier.Stone && stone.State==BallastStone.Phase.Carried && ctlAnim.GetCurrentAnimatorClipInfo(0).Any(c=>c.clip==clip),
            "负重实际播放 clip="+clip.name+" asset="+Asset(clip)+" poseProfile="+Asset(carrier.profile.carryPose)+" phase="+phaseA.ToString("F4")+"→"+phaseEnd.ToString("F4")+
            " rootTravel="+Vector3.Distance(ctl.transform.position,rootA).ToString("F3")+"m stoneLocal="+P(poseA)+"→"+P(stone.transform.localPosition)+
            " stoneTurn="+Quaternion.Angle(rotA,stone.transform.localRotation).ToString("F2")+"°");
        if(Order=="primary")ClosePhoto(cam,"06-carrier-carry-later",body,stone);
        var sequence=Order=="primary"?new[]{"stone-carrier","canjiou","zhihuocanji"}:new[]{"canjiou","zhihuocanji","stone-carrier"};
        var heavy=stone.GetComponent<GlyphHeavyBody>();
        var foot=loc.PlayerFloorPosition;
        var volume=post.basket.passengerVolume;
        int clearTransitions=0;room.CompletionChanged+=()=>{if(room.IsComplete)clearTransitions++;};
        for(int i=0;i<sequence.Length;i++)
        {
            var id=sequence[i];var target=team[id];
            await Until(()=>target.Controller.CanReceiveHit,"在战区内可对 "+id+" 产生有效受击",5);
            Assert(target.Controller.Entity.ReceiveHit("QA 正式三敌结算 "+id,target.Controller.Entity.Combat.Health,0,attacker:cam.transform) && target.Defeated,
                "对原实体造成真实致命伤 "+id+" lease="+target.Id);
            await Frame();
            Assert(team.Values.Count(e=>e.Defeated)==i+1 && sequence.Skip(i+1).All(s=>!team[s].Defeated),
                "当轮击破人数 "+(i+1)+"/3，其他租约未被误结算");
            if(i<2)Assert(!room.IsComplete && clearTransitions==0,"仍有原敌人存活时清场事件未发生");
            else await Until(()=>room.IsComplete && clearTransitions==1,"最后一人倒下后当轮清场只发布一次");
            if(stone.State!=BallastStone.Phase.Dropped){Assert(!heavy.CanManipulate,"工奴仍持石，无法受符力");continue;}
            // Only this release-gate probe places the tracked representative into the real box after the encounter has already started.
            // Leave/return the area between hits so the normal encounter resumes living guards before testing their death.
            var standIn=volume.transform.TransformPoint(new Vector3(1.92f,0f,.77f));
            MoveFloor(loc,standIn);await Awaitable.FixedUpdateAsync();await Awaitable.FixedUpdateAsync();
            Assert(volume.bounds.Contains(head.position) && post.basket.Stone==stone && stone.GetInstanceID()==previewStone,
                "资格探测使用真实乘员盒和桥头所见的原石 head="+P(head.position));
            if(i<2)Assert(!post.basket.Boarded && !heavy.CanManipulate && !room.IsComplete,
                "已杀 "+(i+1)+"，本人已进乘员盒亦无法施力取石");
            else Assert(post.basket.Boarded && heavy.CanManipulate && room.IsComplete,
                "已杀 3/3，真实入盒开放原石符力");
            MoveFloor(loc,foot);await Frame();
        }
        Assert(Equipment(p).SequenceEqual(equipment) && Workshop(p,manager).SequenceEqual(guard),"章节官方双手、实笔、左腕及③工坊两屋顶和精英原接线与首次进入前相同");
        Log("END order="+string.Join("→",sequence)+" floor="+P(loc.PlayerFloorPosition));
    }
    static int starts,contacts;
    static T Field<T>(object target,string name)=>(T)target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(target);
    static void MoveFloor(RpgLocomotionController loc,Vector3 p){loc.rigRoot.position+=p-loc.PlayerFloorPosition;Physics.SyncTransforms();}
    static string Describe(EnemyLease e)=>e.EnemyId+" lease="+e.Id+" instance="+e.Controller.GetInstanceID()+
        " ready="+e.Controller.CanReceiveHit+" beh="+e.Controller.CurrentBehaviour+" hasTarget="+e.Controller.HasTarget+" world="+P(e.Controller.transform.position);
    static int[] Equipment(LinearRpgController p)
    {
        var xr=p.handTracking.xrHandsSource;var vis=SceneObjects<Oculus.Interaction.HandVisual>();var lesson=One<Chapter02BridgeEquipment>();
        var pen=lesson.equipment.pen;
        Assert(xr && xr.IsConfigured && vis.Length==2 && p.wristMenu && p.wristMenu.wornDevice &&
            pen && lesson.glyph.penRoot==pen.transform && lesson.glyph.nib==pen.insertionTip && lesson.equipment.interaction.player==p,
            "原章节笔、腰带、左腕准望匣与双手输入齐备");
        var ids=new List<int>{pen.GetInstanceID(),lesson.equipment.interaction.GetInstanceID(),p.wristMenu.wornDevice.GetInstanceID()};
        foreach(bool left in new[]{true,false})
        {
            var src=left?xr.leftHand:xr.rightHand;
            var v=vis.Single(v=>ReferenceEquals(v.Hand,src));
            var mesh=(SkinnedMeshRenderer)new SerializedObject(v).FindProperty("_openXRSkinnedMeshRenderer").objectReferenceValue;
            Assert(v.isActiveAndEnabled && mesh && Asset(mesh.sharedMesh)=="Packages/com.meta.xr.sdk.interaction/Runtime/Meshes/OpenXR"+(left?"Left":"Right")+"Hand.fbx",
                "正式官方 "+(left?"左":"右")+" 手网格与输入源");
            ids.Add(v.GetInstanceID());
        }
        return ids.ToArray();
    }
    static int[] Workshop(LinearRpgController p,EnemyManager m)
    {
        var w=One<WorkshopTenonObjective>();
        Assert(w.player==p && w.roof && w.courtyard && w.roof.enemies.Length==2 && w.roof.enemies.All(e=>e) &&
            w.courtyard.enemyManager==m && w.courtyard.spawnPoints.Count(s=>s.point && s.enemyId=="elite-canjiou")==1 &&
            !w.courtyard.HasStarted && !w.roof.HasStarted,"③ 的两名屋顶敌人、独立院场和精英出生引用保留，均未提前进战");
        return new[]{w.roof.GetInstanceID(),w.roof.enemies[0].GetInstanceID(),w.roof.enemies[1].GetInstanceID(),
            w.courtyard.GetInstanceID(),w.courtyard.spawnPoints.Single(s=>s.enemyId=="elite-canjiou").point.GetInstanceID()};
    }
    static Renderer[] Body(EnemyLease lease,BallastStone stone)=>Active(lease.Controller.GetComponentsInChildren<Renderer>(true))
        .Where(r=>MeshOf(r) && Asset(MeshOf(r)).StartsWith("Assets/Art/Enemies/",StringComparison.Ordinal) &&
            !r.transform.IsChildOf(stone.transform)).ToArray();

    static RectInt RectOf(Camera c,IEnumerable<Renderer> renderers,int w,int h,int pad=0)
    {
        var viewport=new List<Vector3>();
        foreach(var r in renderers.Where(r=>LayerIn(c,r)))
            for(int i=0;i<8;i++)
            {
                var sign=new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1);
                var v=c.WorldToViewportPoint(r.bounds.center+Vector3.Scale(sign,r.bounds.extents));
                if(v.z>0)viewport.Add(v);
            }
        if(viewport.Count==0)return default;
        int x0=Mathf.Clamp(Mathf.FloorToInt(viewport.Min(v=>v.x)*w)-pad,0,w);
        int x1=Mathf.Clamp(Mathf.CeilToInt(viewport.Max(v=>v.x)*w)+pad,0,w);
        int y0=Mathf.Clamp(Mathf.FloorToInt(viewport.Min(v=>v.y)*h)-pad,0,h);
        int y1=Mathf.Clamp(Mathf.CeilToInt(viewport.Max(v=>v.y)*h)+pad,0,h);
        return new RectInt(x0,y0,Mathf.Max(0,x1-x0),Mathf.Max(0,y1-y0));
    }
    static void ClosePhoto(Camera cam,string name,Renderer[] body,BallastStone stone)
    {
        var shot=Shot(cam,name+"-full");
        var rect=RectOf(cam,body.Concat(Active(stone.GetComponentsInChildren<Renderer>(true))),shot.width,shot.height,16);
        if(rect.width<4 || rect.height<4){Observe(false,"负重近照框未落在当前玩家相机画面 "+rect);return;}
        var data=new Color32[rect.width*rect.height];
        for(int y=0;y<rect.height;y++)Array.Copy(shot.pixels,(rect.y+y)*shot.width+rect.x,data,y*rect.width,rect.width);
        WritePixels(name+"-crop",rect.width,rect.height,data);
        Log("CROP "+name+" same Game view native pixels="+rect+" cameraOriginalForward="+P(cam.transform.forward));
    }
    static void TrioPhoto(Camera cam,string stage,Dictionary<string,EnemyLease> team,BallastStone stone)
    {
        var full=Shot(cam,stage);var annotated=full.pixels.ToArray();
        var colors=new[]{new Color32(252,203,49,255),new Color32(87,213,255,255),new Color32(248,101,113,255)};
        var stamps=new[]{"010110010010111","111001111100111","111001111001111"};
        var stats=new List<string>();
        for(int j=0;j<Ids.Length;j++)
        {
            string id=Ids[j];
            var body=Body(team[id],stone);
            var rect=RectOf(cam,body,full.width,full.height);
            var materials=new List<string>();
            foreach(var r in body)
            {
                materials.Add(PathOf(r.transform)+" mesh="+MeshOf(r).name+" asset="+Asset(MeshOf(r))+" layer="+r.gameObject.layer+
                    " cameraIncludes="+LayerIn(cam,r)+" material="+string.Join("|",r.sharedMaterials.Select(m=>m?m.name+" ["+Asset(m)+"] "+m.shader?.name:"MISSING")));
            }
            File.WriteAllLines(Path.Combine(Out,stage+"-"+id+"-body.txt"),materials);
            var originalCast=body.Select(r=>r.shadowCastingMode).ToArray();
            FramePixels surface,removed,again;
            try
            {
                foreach(var r in body)r.shadowCastingMode=ShadowCastingMode.Off;
                // Settle the changed URP shadow map without advancing AI, Animator or the camera.
                surface=Shot(cam,stage+"-visible-no-shadow-"+id);
                foreach(var r in body)r.forceRenderingOff=true;
                removed=Shot(cam,stage+"-without-"+id);
                foreach(var r in body)r.forceRenderingOff=false;
                again=Shot(cam,stage+"-repeat-no-shadow-"+id);
            }
            finally
            {
                for(int k=0;k<body.Length;k++){body[k].forceRenderingOff=false;body[k].shadowCastingMode=originalCast[k];}
            }
            int count=0,xlo=full.width,ylo=full.height,xhi=-1,yhi=-1;
            for(int y=rect.yMin;y<rect.yMax;y++)for(int x=rect.xMin;x<rect.xMax;x++)
            {
                int k=y*full.width+x;
                int D(Color32 a,Color32 b)=>Math.Max(Math.Abs(a.r-b.r),Math.Max(Math.Abs(a.g-b.g),Math.Abs(a.b-b.b)));
                int noise=D(surface.pixels[k],again.pixels[k]);
                if(Math.Min(D(surface.pixels[k],removed.pixels[k]),D(removed.pixels[k],again.pixels[k]))<=Math.Max(10,noise*2+5))continue;
                count++;xlo=Math.Min(xlo,x);xhi=Math.Max(xhi,x);ylo=Math.Min(ylo,y);yhi=Math.Max(yhi,y);
            }
            bool supported=body.Length>0 && body.All(r=>r.sharedMaterials.All(m=>m && m.shader && m.shader.isSupported));
            Observe(count>=12 && supported,stage+" Game 相机同帧只隐藏 "+id+" 的身体，无该角色投影之配对画面内的可辨像素="+count+" rect="+rect+
                " visibleBottomLeft="+xlo+","+ylo+".."+xhi+","+yhi+" activeRenderers="+body.Length+" materialsSupported="+supported);
            if(stage=="05-first-altar-trio")
            {
                int margin=count>0 ? Math.Min(Math.Min(xlo,full.width-1-xhi),Math.Min(ylo,full.height-1-yhi)) : -1;
                Observe(margin>=8,"首遇原相机全景需容纳 "+id+" 的实测轮廓并至少留 8px 边距，实际="+margin);
            }
            stats.Add((j+1)+"="+Describe(team[id])+" pixels="+count+" (bottom-left x/y bounds) "+xlo+","+ylo+" to "+xhi+","+yhi);
            if(count==0)continue;
            void Dot(int x,int y,Color32 color)
            {if((uint)x<(uint)full.width && (uint)y<(uint)full.height)annotated[y*full.width+x]=color;}
            for(int x=xlo-2;x<=xhi+2;x++)for(int k=0;k<2;k++){Dot(x,ylo-2-k,colors[j]);Dot(x,yhi+2+k,colors[j]);}
            for(int y=ylo-2;y<=yhi+2;y++)for(int k=0;k<2;k++){Dot(xlo-2-k,y,colors[j]);Dot(xhi+2+k,y,colors[j]);}
            int labelX=Mathf.Clamp(xlo,0,full.width-20),labelY=Mathf.Clamp(yhi+2,0,full.height-26);
            for(int x=0;x<19;x++)for(int y=0;y<25;y++)Dot(labelX+x,labelY+y,colors[j]);
            for(int gy=0;gy<5;gy++)for(int gx=0;gx<3;gx++)if(stamps[j][gy*3+gx]=='1')
                for(int dx=0;dx<4;dx++)for(int dy=0;dy<4;dy++)Dot(labelX+3+gx*4+dx,labelY+3+(4-gy)*4+dy,new Color32(15,18,21,255));
        }
        WritePixels(stage+"-numbered",full.width,full.height,annotated);
        File.WriteAllLines(Path.Combine(Out,stage+"-legend.txt"),new[]{"1 背石工奴 / 2 普通残机偶 / 3 掷火残机；原图保留正式投影；框来自各角色关闭自身投影后身体的 A/B/A 差分。"}
            .Concat(stats));
    }
    static bool LayerIn(Camera c,Renderer r)=>(c.cullingMask & (1<<r.gameObject.layer))!=0;
    class FramePixels { public int width,height;public Color32[] pixels; }
    static void WritePixels(string name,int w,int h,Color32[] pixels)
    {
        var tex=new Texture2D(w,h,TextureFormat.RGBA32,false);
        try{tex.SetPixels32(pixels);tex.Apply();File.WriteAllBytes(Path.Combine(Out,name+".png"),tex.EncodeToPNG());}
        finally{Object.DestroyImmediate(tex);}
    }
    static FramePixels Shot(Camera camera,string name)
    {
        int w=1280,h=Mathf.Clamp(Mathf.RoundToInt(1280f/Mathf.Max(.1f,camera.aspect)),480,1440);
        var rt=RenderTexture.GetTemporary(w,h,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
        var active=RenderTexture.active;
        var tex=new Texture2D(w,h,TextureFormat.RGBA32,false);
        try
        {
            var request=new UniversalRenderPipeline.SingleCameraRequest{destination=rt};
            if(!RenderPipeline.SupportsRenderRequest(camera,request))throw new InvalidOperationException("当前玩家相机不支持 URP 捕获");
            RenderPipeline.SubmitRenderRequest(camera,request);
            RenderTexture.active=rt;
            tex.ReadPixels(new Rect(0,0,w,h),0,0);tex.Apply();
            File.WriteAllBytes(Path.Combine(Out,name+".png"),tex.EncodeToPNG());
            var pixels=tex.GetPixels32();
            Log($"SHOT {name} {w}x{h} eye={P(camera.transform.position)} forward={P(camera.transform.forward)}");
            return new FramePixels{width=w,height=h,pixels=pixels};
        }
        finally{RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(tex);}
    }
}
}





