using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Unity.AI.Navigation;
using NarutoVR.SealCombat.Enemies;
using NarutoVR.SealCombat.Rpg;
using NarutoVR.SealCombat.Events;
using UO=UnityEngine.Object;

[InitializeOnLoad]
public static class AltarTrioActions
{
    const string ScenePath="Assets/Scene/Chapter02_SuspendedPath.unity", Key="TrioActions.Arm";
    static string Dir => Environment.GetEnvironmentVariable("ALTAR_TRIO_ACTIONS");
    static string Hash(string path) { using var h=SHA256.Create(); return BitConverter.ToString(h.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant(); }
    static void Log(string value){File.AppendAllText(Path.Combine(Dir,"actions.txt"),value+"\n");Debug.Log("TRIO-ACTIONS "+value);}
    static int n;
    static bool Check(bool ok,string msg){Log((ok?"PASS ":"FAIL ")+msg); if(!ok)throw new Exception(msg);n++;return ok;}
    static string V(Vector3 v)=>v.ToString("F4");
    static float Flat(Vector3 a,Vector3 b)=>Vector3.ProjectOnPlane(a-b,Vector3.up).magnitude;
    static T Read<T>(object obj,string field)=>(T)obj.GetType().GetField(field,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(obj);
    static double stop;
    static bool running;
    static AltarTrioActions(){EditorApplication.update+=Entry;}
    public static void Run()
    {
        if(!Application.isBatchMode || !Application.dataPath.Contains("NarutoVR-A4ProductionArt-20261125"))throw new InvalidOperationException("Independent QA project only");
        Directory.CreateDirectory(Dir);File.WriteAllText(Path.Combine(Dir,"actions.txt"),DateTimeOffset.Now.ToString("o")+" Unity "+Application.unityVersion+"\n");
        var scene=EditorSceneManager.OpenScene(ScenePath);
        Log("scene before install="+Hash(ScenePath));
        bool added=Chapter02AltarTrioInstaller.Apply(scene);
        var room=GetRoom();
        room.rpg.autoLoad=false; room.rpg.saveFileName="qa-t25-altrio-actions-"+Guid.NewGuid().ToString("N")+".json";
        if(room.rpg.skills && room.rpg.skills.voice)room.rpg.skills.voice.autoStartInEditor=false;
        EditorUtility.SetDirty(room.rpg); EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Log("QA scene after install + isolated save="+Hash(ScenePath)+" installerAdded="+added);
        EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
        SessionState.SetBool(Key,true); EditorApplication.EnterPlaymode();
    }
    static CombatRoomEncounter GetRoom()=>UO.FindObjectsByType<CombatRoomEncounter>(FindObjectsInactive.Include,FindObjectsSortMode.None)
        .Single(x=>x.spawnPoints!=null&&x.spawnPoints.Any(p=>p!=null&&p.enemyId=="stone-carrier"));
    static async void Entry()
    {
        if(running && EditorApplication.timeSinceStartup>stop){Log("TIMEOUT");EditorApplication.Exit(2);return;}
        if(!Application.isPlaying || !SessionState.GetBool(Key,false))return;
        SessionState.EraseBool(Key);running=true;stop=EditorApplication.timeSinceStartup+180;
        try {await Verify();Log("TRIO_ACTIONS_PASS "+n);running=false;EditorApplication.Exit(0);}
        catch(Exception ex){Log("TRIO_ACTIONS_FAIL "+ex); running=false; EditorApplication.Exit(1);}
    }
    static async Task Frame(){await Awaitable.NextFrameAsync();}
    static void MovePlayer(LinearRpgController p,Vector3 floor,Vector3 face)
    {
        var loc=p.locomotion; loc.rigRoot.position+=floor-loc.PlayerFloorPosition;
        if(face.sqrMagnitude>.01f)loc.head.rotation=Quaternion.LookRotation(face,Vector3.up);
        Physics.SyncTransforms();
        Log("QA placed real rig floor="+V(loc.PlayerFloorPosition)+" head="+V(loc.head.position)+" forward="+V(loc.head.forward));
    }
    static void Capture(string name,Vector3 eye,Vector3 at)
    {
        if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)return;
        var go=new GameObject("QA action camera");var cam=go.AddComponent<Camera>();cam.enabled=false;
        cam.transform.position=eye;cam.transform.rotation=Quaternion.LookRotation(at-eye,Vector3.up);cam.fieldOfView=48;
        cam.clearFlags=CameraClearFlags.Skybox;cam.nearClipPlane=.04f;cam.farClipPlane=600;cam.stereoTargetEye=StereoTargetEyeMask.None;
        var old=RenderTexture.active;var rt=RenderTexture.GetTemporary(1152,720,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
        var tex=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);
        try {cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);tex.Apply();
            File.WriteAllBytes(Path.Combine(Dir,name+".png"),tex.EncodeToPNG());Log("camera "+name+" eye="+V(eye)+" center="+V(at));}
        finally{RenderTexture.active=old;cam.targetTexture=null;RenderTexture.ReleaseTemporary(rt);UO.Destroy(tex);UO.Destroy(go);}
    }
    static void CharacterView(string n,Transform t)
    { var c=t.position+Vector3.up*1.15f;Capture(n,c+t.right*3.2f+t.forward*2.1f+Vector3.up*.75f,c); }
    static void RangedView(string n,Vector3 source,Vector3 mark)
    {var mid=(source+mark)*.5f+Vector3.up*.1f;var side=Vector3.Cross(Vector3.up,mark-source).normalized;Capture(n,mid+side*8.8f+Vector3.up*3.4f,mid);}

    static StreamWriter trace;
    static float clock0;
    static void Sample(string phase,EnemyController c,LinearRpgController p,string extra="")
    {
        var pos=c.transform.position;var f=p.locomotion.PlayerFloorPosition;
        trace.WriteLine(FormattableString.Invariant($"{Time.frameCount},{Time.time-clock0:F5},{phase},{c.CurrentBehaviour},{pos.x:F5},{pos.y:F5},{pos.z:F5},{c.transform.eulerAngles.y:F4},{f.x:F5},{f.y:F5},{f.z:F5},{p.Player.Health:F2},{c.HasTarget},{extra}"));
    }
    static void Asset(string what,UO o){var path=AssetDatabase.GetAssetPath(o);Log(what+" "+path+" sha256="+Hash(path));}
    static async Task Verify()
    {
        Time.timeScale=1;Time.captureDeltaTime=1f/60f;
        var room=GetRoom();var rpg=room.rpg;
        for(int frame=0;!rpg.CanAct && frame<1500;frame++)await Frame();
        Check(rpg.CanAct && rpg.GetComponent<RpgDamageReceiver>().CanReceiveDamage,"real RPG damage receiver ready hp="+rpg.Player.Health);
        var entries=room.spawnPoints;Check(entries.Length==3,"three registered starts "+string.Join(", ",entries.Select(e=>e.enemyId+"="+V(e.point.position))));
        var nav=UO.FindObjectsByType<NavMeshSurface>(FindObjectsSortMode.None).First(s=>s.navMeshData&&s.navMeshData.name.Contains("AltarStoneCarrier"));
        Asset("altar navigation",nav.navMeshData);
        foreach(var e in entries)Check(NavMesh.SamplePosition(e.point.position,out var hit,.6f,NavMesh.AllAreas),"spawn samples "+e.enemyId+" "+V(hit.position));
        var post=entries.Single(e=>e.enemyId=="stone-carrier").point.GetComponent<StoneCarrierPost>();
        var player=new Vector3(-9.121365f,80,40.0f);
        MovePlayer(rpg,player,Vector3.back);
        for(int i=0;i<1200 && (!room.HasStarted || room.enemies.Length!=3);i++)await Frame();
        Check(room.HasStarted && room.enemies.Length==3 && room.PlayerInside,"floor entered actual managed room; active="+room.enemies.Length+" status="+room.Status);
        var map=entries.Zip(room.enemies,(e,c)=>(e,c)).ToDictionary(pair=>pair.e.enemyId,pair=>pair.c);
        var w=map["stone-carrier"];var m=map["canjiou"];var r=map["zhihuocanji"];
        Check(map.Values.All(c=>c && c.Agent && c.Agent.isOnNavMesh && !c.isSpawnedEnemy && c.target==rpg.locomotion.head),"all current actors use authored ordinary AI and the live player's head on NavMesh");
        var w0=w.transform.position;var m0=m.transform.position;var r0=r.transform.position;
        Log("spawn observed carrier="+V(w0)+" melee="+V(m0)+" ranged="+V(r0)+" nav="+V(nav.transform.position));
        m.SetSuspended(true); r.SetSuspended(true);
        using var outCsv=new StreamWriter(Path.Combine(Dir,"actions.csv"));trace=outCsv;clock0=Time.time;
        trace.WriteLine("frame,time,phase,action,x,y,z,yaw,playerX,playerY,playerZ,playerHP,hasTarget,measurement");
        var worker=w.GetComponent<StoneCarrier>(); var stone=worker.Stone;
        var meshes=stone.GetComponentsInChildren<MeshFilter>();
        Check(stone.State==BallastStone.Phase.Carried && stone.transform.parent==w.transform && meshes.Length==8 && meshes.All(x=>x.GetComponent<MeshRenderer>().enabled),"same carried physical stone id="+stone.GetInstanceID()+" eight active mesh filters "+string.Join("|",meshes.Select(x=>x.name)));
        Asset("carry clip",worker.profile.carryPose.Clip); Asset("pose samples",worker.profile.carryPose);Asset("carrier profile",worker.profile);
        Log("carry clip length="+worker.profile.carryPose.Clip.length+" samples="+worker.profile.carryPose.FrameCount+" animator="+w.animator.runtimeAnimatorController.name+" patrol="+worker.profile.patrolSpeed+" retreat="+worker.profile.retreatSpeed+" route="+string.Join(",",post.patrolPoints.Select(x=>V(x.position))));
        float min=1,max=0,travel=0,maxSpeed=0;var last=w0;int phases=0,correctPose=0;
        for(int i=0;i<330 && (travel<.7f || phases<25 || max-min<.15f);i++){
            await Frame();var pos=w.transform.position;travel+=Flat(last,pos);last=pos;maxSpeed=Mathf.Max(maxSpeed,w.Agent.velocity.magnitude);
            if(worker.TryGetCarryPhase(out float phase)){
                min=Mathf.Min(min,phase);max=Mathf.Max(max,phase);phases++;
                if(worker.profile.carryPose.TryEvaluate(phase,out var pose)&&Vector3.Distance(stone.transform.localPosition,pose.position)<.025f&&Quaternion.Angle(stone.transform.localRotation,pose.rotation)<2.5f)correctPose++;
                Sample("carrier-patrol",w,rpg,phase.ToString("F6"));
            }
        }
        Check(travel>.35f&&max-min>.10f&&phases>=12&&correctPose>=phases*.75f,"real patrol distance="+travel.ToString("F3")+" speedMax="+maxSpeed.ToString("F3")+" clipPhase="+min.ToString("F4")+".."+max.ToString("F4")+" matching stone poses="+correctPose+"/"+phases);
        CharacterView("01-carry-patrol",w.transform);
        var before=w.transform.position;var attacker=rpg.locomotion.head;var oldHp=w.Entity.Combat.Health;
        bool hit1=w.Entity.ReceiveHit("qa-trio-carrier-"+Guid.NewGuid().ToString("N"),1,0,null,attacker);
        Check(hit1 && worker.Retreating && w.Entity.Combat.Health==oldHp-1,"nonfatal real hit triggered retreat hp="+oldHp+"->"+w.Entity.Combat.Health+" origin="+V(before));
        var distBefore=Flat(before,attacker.position); float peak=distBefore;
        for(int i=0;i<150&&peak<distBefore+.35f;i++){await Frame();peak=Mathf.Max(peak,Flat(w.transform.position,attacker.position));Sample("carrier-retreat",w,rpg,worker.Status);}
        Check(peak>distBefore+.15f && worker.Stone==stone && stone.State==BallastStone.Phase.Carried,"retreated from attacker distance "+distBefore.ToString("F3")+"->"+peak.ToString("F3")+"m, endpoint="+V(w.transform.position));
        w.SetSuspended(true);m.SetSuspended(false);
        var start=m.transform.position;float mhp=rpg.Player.Health;int opened=0,started=0,ack=0;string damageId=null;float accepted=0;
        m.onAttackStarted.AddListener(ix=>{started++;Log("melee start index="+ix+" at distance="+Flat(m.transform.position,rpg.locomotion.PlayerFloorPosition)+" health="+rpg.Player.Health);});
        m.onAttackContact.AddListener(ix=>{opened++;var d=m.Entity.Attacks[ix];Log("melee contact index="+ix+" range="+d.radius+" window="+d.activeStart+".."+d.activeEnd);});
        using var observed=EventCenter.Subscribe(EnemyEvents.DamageAccepted,h=>{if(h.Source==m.Entity && h.Receiver==rpg.GetComponent<RpgDamageReceiver>()){ack++;accepted=h.Damage;damageId=h.AttackId;}});
        float md=Flat(start,player);Log("melee release from isolation "+V(start)+" player distance="+md+" attackDistance="+m.attackDistance);
        for(int i=0;i<1100&&(ack==0||opened==0);i++){await Frame();Sample("melee",m,rpg,m.Status);}
        Check(m.HasEverDetectedPlayer&&Flat(start,m.transform.position)>.35f,"melee discovered via "+m.DetectionReason+" and moved "+Flat(start,m.transform.position).ToString("F3")+"m to "+V(m.transform.position)+" status="+m.Status);
        Check(started>0&&opened>0&&ack>0&&rpg.Player.Health<mhp,"melee reached actual damage window, received attackId="+damageId+" raw damage="+accepted+" HP "+mhp+"->"+rpg.Player.Health);
        CharacterView("02-melee-contact",m.transform);
        m.SetSuspended(true);r.SetSuspended(false);
        var ranged=r.GetComponent<EnemyRangedAttack>();Asset("prefab fire core",ranged.projectile);
        Check(ranged.IsConfigured && ranged.projectile && ranged.warningRing,"ranged live muzzle, warning ring and projectile assigned");
        var rp=r.transform.position;var rotation=r.transform.rotation;float yaw=0,rtravel=0;last=rp;
        bool locked=false,ringHeld=false,warning=false,shot=false,flying=false,movedTarget=false;
        Vector3 aim=default,launch=default,lockFloor=default,lastShot=default;float aimAngle=180;int flightFrames=0;var shotID=0;
        int rstart=0;r.onAttackStarted.AddListener(ix=>{rstart++;Log("ranged start index="+ix+", dist="+Flat(r.transform.position,rpg.locomotion.PlayerFloorPosition).ToString("F3"));});
        var coreName=ranged.projectile.name;
        for(int i=0;i<1200&&!(shot&&flying&&ringHeld&&rtravel>.35f&&yaw>4);i++){
            await Frame(); var pos=r.transform.position;rtravel+=Flat(last,pos);last=pos;yaw=Mathf.Max(yaw,Quaternion.Angle(rotation,r.transform.rotation));
            bool observedLock=Read<bool>(ranged,"locked");float prog=Read<float>(ranged,"progress");var capturedAim=Read<Vector3>(ranged,"aimPoint");
            warning|=ranged.warningRing.enabled;
            if(!locked && observedLock && ranged.warningRing.enabled){
                locked=true;aim=capturedAim;lockFloor=rpg.locomotion.PlayerFloorPosition;
                Log("aim locked clipP="+prog+" configured="+ranged.lockProgress+" aim="+V(aim)+" ring="+V(ranged.warningRing.transform.position)+" muzzle="+V(ranged.muzzle.position));
                RangedView("03-warning-lock",ranged.muzzle.position,aim);
                MovePlayer(rpg,lockFloor+Vector3.left*1.45f,Vector3.ProjectOnPlane(r.transform.position-lockFloor,Vector3.up));
                movedTarget=true;
            }else if(locked&&!shot && observedLock && ranged.warningRing.enabled && Flat(lockFloor,rpg.locomotion.PlayerFloorPosition)>1)
                ringHeld|=Vector3.Distance(aim,capturedAim)<.002f && Flat(aim,ranged.warningRing.transform.position)<.002f;
            var core=UO.FindObjectsByType<EnemyFireCore>(FindObjectsSortMode.None).FirstOrDefault(c=>Read<EnemyController>(c,"shooter")==r);
            if(core && !shot){
                shot=true;shotID=core.GetInstanceID();lastShot=launch=core.transform.position;
                aimAngle=Vector3.Angle(core.transform.forward,aim-launch);
                Log("real projectile "+core.name+" id="+shotID+" launch observed="+V(launch)+" muzzleDist="+Vector3.Distance(launch,ranged.muzzle.position)+" aimAngle="+aimAngle+" speed="+Read<float>(core,"speed")+" attackId="+Read<string>(core,"attackId")+" progress="+prog);
                Check(core.name.StartsWith(coreName) && Read<string>(core,"attackId")!=null && Read<float>(core,"speed")==ranged.speed,"real spawned EnemyFireCore owned by ranged actor and configured speed");
            }else if(core && core.GetInstanceID()==shotID){
                flightFrames++;lastShot=core.transform.position;if(flightFrames==24) RangedView("04-fire-core-flight",ranged.muzzle.position,aim);if(Vector3.Distance(launch,lastShot)>.3f)flying=true;
            }
            Sample("ranged",r,rpg,FormattableString.Invariant($"{prog:F5}|{observedLock}|{ranged.warningRing.enabled}|{(core?core.GetInstanceID():0)}"));
        }
        Check(warning&&locked&&movedTarget&&ringHeld,"actual pre-release aim remains at original point after moving player 1.45m");
        Check(shot&&flying&&aimAngle<8f,"same projectile actual flight "+Vector3.Distance(launch,lastShot).ToString("F3")+"m across "+flightFrames+" frames, aiming at pre-dodge mark angle="+aimAngle.ToString("F3"));
        Check(rstart>0 && r.HasEverDetectedPlayer && rtravel>.35f && yaw>4,"ranged discovered via "+r.DetectionReason+"; travel="+rtravel.ToString("F3")+"m yaw from start="+yaw.ToString("F2")+" degrees final="+V(r.transform.position)+" status="+r.Status);
    }
}
