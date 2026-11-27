using System;
using System.Collections;
using System.IO;
using System.Linq;
using NarutoVR.SealCombat.Items;
using NarutoVR.SealCombat.Rpg;
using NarutoVR.SealCombat.Editor;
using NarutoVR.SealCombat;
using NarutoVR.SealCombat.Enemies;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class CooldownStakePlayChecks
{
    const string Key="NarutoVR.CheckCooldownStake";
    const string Report="Logs/CooldownStake.txt";
    static IEnumerator run;
    static double deadline;
    static int frame=-1;
    static CooldownStakePlayChecks() { EditorApplication.update+=Tick; }
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Samples/ToolBeltItems/CooldownStakeLab.unity");
        File.WriteAllText(Report,DateTimeOffset.Now.ToString("o")+" Unity "+Application.unityVersion+" / desktop Play / synthetic right-hand observations\n");
        SessionState.SetBool(Key,true);
        EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!SessionState.GetBool(Key,false) || !Application.isPlaying || !EditorApplication.isPlaying ||
            Time.frameCount < 3 || frame==Time.frameCount) return;
        frame=Time.frameCount;
        try
        {
            if(run==null) { deadline=EditorApplication.timeSinceStartup+95;run=Trial(); }
            if(EditorApplication.timeSinceStartup>deadline) throw new TimeoutException("等待铜橛测试状态");
            if(run.MoveNext()) return;
            Log("STAKE_PLAY_PASS");Finish(0);
        }
        catch(Exception e) { Log("FAIL "+e);Finish(1); }
    }
    static void Finish(int code) { SessionState.EraseBool(Key);EditorApplication.Exit(code); }
    static void Log(string s) { File.AppendAllText(Report,s+"\n");Debug.Log("STAKE "+s); }
    static void Need(bool ok,string message) { if(!ok) throw new InvalidOperationException(message); }

    static IEnumerator Trial()
    {
        Time.captureDeltaTime=.02f;
        var lab=Object.FindFirstObjectByType<CooldownStakeLabControls>();
        Need(lab,"场景缺少灰盒控制器");
        var stake=lab.stake; var p=lab.player;
        var loadDeadline=EditorApplication.timeSinceStartup+18;
        while(!p.IsReady || !lab.Initialised || (stake.Dock && stake.Dock.IsSnapping))
        {
            if(EditorApplication.timeSinceStartup>=loadDeadline)
            {
                var pos=p.CheckpointAt(0).floorPosition;
                var found=Physics.OverlapCapsule(pos+Vector3.up*.25f,pos+Vector3.up*1.6f,.23f,
                    ~0,QueryTriggerInteraction.Ignore);
                throw new InvalidOperationException($"初始化失败：{p.Status}，点={pos}，附近={string.Join(",",found.Select(c=>c.name))}，腰带={lab.Initialised} 槽={stake.Dock}");
            }
            yield return null;
        }
        Need(stake.Dock==stake.home && ((ToolBeltSlot)stake.home).displayKind==GameItemKind.CooldownStake,"窄鞘首摆失败");
        var pocket=(ToolBeltSlot)stake.home;
        var at=stake.insertionTip.position;
        void PlantAt(Vector3 ground) {
            Need(lab.TryTake(stake,true),"从原套口或地面拾取铜橛");
            stake.transform.rotation=Quaternion.identity;
            stake.transform.position+=ground+Vector3.up*.012f-stake.insertionTip.position;
            lab.ReleaseAuxiliary(stake,true);
            Need(stake.FieldActive && stake.Planted && !stake.Dock && !stake.IsHeld,"松手将铜橛插地");
        }
        at=p.locomotion.PlayerFloorPosition+p.locomotion.rigRoot.forward*.45f;
        PlantAt(at);at=stake.insertionTip.position;
        var scope=stake.gameObject.scene;
        int passed=0;
        void Check(bool ok,string note){Need(ok,note);Log("PASS "+note);passed++;}
        bool Hit(Vector3 a,Vector3 b,out float hit){
            Vector3 delta=b-a;
            if (StakeScreen.TryEntry(scope,at+a,delta.normalized,delta.magnitude,out var e)){hit=e.Distance;return true;}
            hit=0;return false;
        }
        Check(stake.radius==1.5f && stake.duration==12 && stake.screenMaterial && stake.screenMaterial.shader.isSupported,"时长12s、半宽1.5m、纱材质受支持");
        Check(stake.GetComponentInChildren<StakeScreen>() && !stake.dial.gameObject.activeSelf,"素纱屏已启用");
        Check(Hit(new(-10,1,0),new(10,1,0),out float entry)&&Mathf.Abs(entry-8.5f)<.002f,"一帧横穿10米，最近的西屏在8.5米");
        Check(Hit(new(0,1,8),new(0,1,0),out _) && Hit(new(0,1,-8),new(0,1,0),out _) && Hit(new(8,1,0),new(0,1,0),out _),"东南北三面外入");
        Check(!Hit(new(0,1,0),new(8,1,0),out _),"内出外放行");
        Check(!Hit(new(-8,1,1.5f),new(8,1,1.5f),out _) && !Hit(new(-2,1,1),new(-1,1,2),out _),"沿边飞行、只触角点放行");
        Check(Hit(new(-2,1,-2),new(0,1,0),out _),"对角真正进入拦截");
        Check(!Hit(new(-8,4,0),new(8,4,0),out _) && !Hit(new(-8,-3,0),new(8,-3,0),out _),"另一层平射不干扰");
        Check(Hit(new(0,4,0),new(0,1,0),out _) && Hit(new(0,-4,0),new(0,1,0),out _),"上方或下方进入有效体积均拦截");
        Check(!Hit(new(1.5f,1,0),new(4,1,0),out _) && Hit(new(1.5f,1,0),new(0,1,0),out _),"边界内外方向正确");
        var other=Object.Instantiate(stake,stake.transform.position+Vector3.right*4,stake.transform.rotation);
        other.Plant(p,5);
        Check(Hit(new(0,1,0),new(8,1,0),out float second) && Mathf.Abs(second-2.5f)<.005f,"从本屏走出，下一面友方屏仍挡弹");
        Check(Hit(new(10,1,0),new(-10,1,0),out float first) && Mathf.Abs(first-4.5f)<.01f,"叠场按最近边界");
        other.gameObject.SetActive(false);Object.Destroy(other.gameObject);
        double planted=p.GameTime;
        var eyes=at+new Vector3(4.2f,2.4f,-4.7f);
        while(p.GameTime<planted+.13)yield return null;
        Capture(eyes,at+Vector3.up*.95f,45,"StakeGauze-unfold");
        while(p.GameTime<planted+.62)yield return null;
        Capture(eyes,at+Vector3.up*.95f,45,"StakeGauze-open");
        int layer=28; int mask=1<<layer;Vector3 center=at+new Vector3(0,1,.4f);
        var dummy=GameObject.CreatePrimitive(PrimitiveType.Cube);
        dummy.name="训练靶";dummy.transform.position=center;dummy.transform.localScale=Vector3.one*.32f;
        dummy.layer=layer;var target=dummy.AddComponent<SkillTrainingTarget>();target.health=100;target.ResetTarget();Physics.SyncTransforms();
        SkillProjectile Fire(Vector3 start,Vector3 end,bool wave=false){
            var ball=new GameObject("纱屏测试弹");ball.transform.position=start;
            var proj=ball.AddComponent<SkillProjectile>();proj.damage=10;proj.poiseDamage=0;proj.speed=20/Time.deltaTime;proj.radius=.02f;
            if(wave)proj.Strike=new SkillStrike(Guid.NewGuid().ToString(),10,0,p.locomotion.head);
            proj.Initialize((end-start).normalized,null,mask,0,null,null);proj.SendMessage("Update");return proj;
        }
        float hp=target.CurrentHealth;
        var outer=Fire(center-Vector3.right*5,center);
        Check(outer.IsFinished && Mathf.Abs(outer.transform.position.x-at.x+1.5f)<.005f && hp==target.CurrentHealth,"外侧火球在纱面终止，内侧靶不掉血");
        Fire(center-Vector3.right*5,center,true);Check(target.CurrentHealth==hp,"外侧雷剑剑波被拦");
        Fire(center-Vector3.right*.5f,center);Check(target.CurrentHealth==hp-10,"内部命中内部靶");
        dummy.transform.position=center+Vector3.right*4;Physics.SyncTransforms();
        Fire(center,dummy.transform.position);Check(target.CurrentHealth==hp-20,"内部火球射出命中外侧靶");
        dummy.transform.position=center-Vector3.right*3;Physics.SyncTransforms();
        Fire(center-Vector3.right*5,center);Check(target.CurrentHealth==hp-30,"纱屏之前的目标先命中");
        Object.Destroy(dummy);
        var shooterPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Enemies/ZhiHuoCanJi/ZhiHuoCanJi_Enemy.prefab");
        var shooter=Object.Instantiate(shooterPrefab,at+Vector3.forward*7,Quaternion.identity).GetComponent<EnemyController>();
        shooter.tickAutomatically=false;
        var receiver=p.GetComponent<RpgDamageReceiver>();
        Vector3 playerHead=receiver.HitPosition;
        var outside=new Vector3(at.x-4,playerHead.y,playerHead.z);
        EnemyFireCore Core(Vector3 origin,Vector3 end,float explosion=0){
            var go=new GameObject("屏幕实射火核");go.transform.SetPositionAndRotation(origin,Quaternion.LookRotation(end-origin));
            var core=go.AddComponent<EnemyFireCore>();core.collisionMask=0;
            core.Launch(shooter,Guid.NewGuid().ToString(),0,10,50,Vector3.Distance(origin,end),explosion);core.Advance(1f);return core;
        }
        float vitals=p.Player.Health+p.skills.effects.ShieldPoints;
        var hostile=Core(outside,playerHead);
        Check(!hostile.gameObject.activeInHierarchy || Mathf.Abs(hostile.transform.position.x-(at.x-1.5f))<.005f,"真实敌火核在纱面截停");
        Check(Mathf.Abs(p.Player.Health+p.skills.effects.ShieldPoints-vitals)<.001f,"敌弹未伤内部玩家");
        var blast=at+Vector3.left*1.6f+Vector3.up*.05f;
        Core(blast+Vector3.left*.2f,blast,4);
        Check(Mathf.Abs(p.Player.Health+p.skills.effects.ShieldPoints-vitals)<.001f,"屏外爆点不能向内造成溅射");
        var insideShot=Core(playerHead+Vector3.left*.6f,playerHead);
        Check(Mathf.Abs(p.Player.Health+p.skills.effects.ShieldPoints-(vitals-10))<.001f,"屏内真实敌弹仍能命中");
        var opponent=new EnemyAttackChecks.RpgFixture(at+Vector3.right*3.7f,"buff.zi","buff.voice","shield.left");
        try
        {
            Check(opponent.Controller.DebugCast("shield.left"),"真实屏外玩家开盾");
            var face=opponent.Receiver.HitPosition;
            shooter.transform.position=face-Vector3.right*4;
            Physics.SyncTransforms();
            var from=face-Vector3.right*3.5f;
            var ricochet=Core(from,face);
            Check(ricochet.transform.forward.x<-.8f && ricochet.transform.position.x>at.x+1.5f,"向外穿纱后由外侧玩家实际弹反");
            float beforeReturn=shooter.Entity.Combat.Health;
            ricochet.Advance(1);
            Check(Mathf.Abs(ricochet.transform.position.x-at.x-1.5f)<.01f && Mathf.Abs(shooter.Entity.Combat.Health-beforeReturn)<.001f,"返程外入命中纱屏，原主无伤");
        }
        finally { opponent.Dispose(); }
        // A melee volume can reach through a curtain; no physical wall or generic damage veto is installed.
        var originalFloor=p.locomotion.PlayerFloorPosition;
        var toEdge=at+Vector3.left*1.32f-originalFloor;
        p.locomotion.rigRoot.position+=Vector3.ProjectOnPlane(toEdge,Vector3.up);
        shooter.transform.SetPositionAndRotation(at+Vector3.left*1.9f,Quaternion.LookRotation(Vector3.right));
        Physics.SyncTransforms();
        var melee=new EnemyAttackExecution(shooter.Entity);
        var reach=new AttackData {radius=1.2f,angle=150,halfHeight=2,centerOffset=Vector3.up,activeStart=.3f,activeEnd=.7f,damage=5};
        float prior=p.Player.Health+p.skills.effects.ShieldPoints;
        melee.Begin(0,reach);melee.Evaluate(.5f,false,1.4f,~0);melee.End();
        Check(Mathf.Abs(p.Player.Health+p.skills.effects.ShieldPoints-prior+5)<.001f,"外侧近战打到屏内仍正常扣血");
        p.locomotion.rigRoot.position-=Vector3.ProjectOnPlane(toEdge,Vector3.up);
        Object.Destroy(shooter.gameObject);
        double before=p.GameTime;string group="qa.stake.rate";
        Need(p.Player.TryReserveCast(Guid.NewGuid().ToString(),group,group,0,8,before,out var cast,out _),"起冷却");cast.Commit();
        while(p.GameTime<before+.24)yield return null;
        Check(Mathf.Abs(p.Player.GetCooldown(group,p.GameTime)-(8-(float)(p.GameTime-before)))<.004f,"纱屏内技能冷却常速");
        Check(lab.TryTake(stake,true) && !stake.FieldActive && !Hit(new(-4,1,0),new(0,1,0),out _),"拔起立即撤防");
        stake.transform.position+=at+Vector3.up*.01f-stake.insertionTip.position;lab.ReleaseAuxiliary(stake,true);
        Check(stake.FieldActive,"重新插放再次展开");
        stake.Plant(p,.24f);double restored=p.GameTime;
        Check(stake.SecondsLeft>.22f && stake.SecondsLeft<.25f,"旧存档剩余秒数继续计时");
        while(p.GameTime<restored+.35)yield return null;
        Check(!stake.FieldActive && !Hit(new(-4,1,0),new(0,1,0),out _) && !stake.GetComponentInChildren<StakeScreen>(),"到期消隐，解除拦截");
        var clock=typeof(LinearRpgController).GetProperty(nameof(LinearRpgController.GameTime));
        clock.SetValue(p,59.85d);stake.Plant(p,12);
        clock.SetValue(p,60.15d);stake.SendMessage("LateUpdate");
        var props=new MaterialPropertyBlock();stake.GetComponentInChildren<StakeScreen>().GetComponent<MeshRenderer>().GetPropertyBlock(props);
        Check(Mathf.Abs(props.GetVector("_FieldPhase").z-.3f)<.001f,"纱屏跨60秒时刻，流光相位自插地连续至0.3秒");
        Log("联测通过 "+passed);
    }
    internal static void Capture(Vector3 eye,Vector3 at,float fov,string filename)
    {
        if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)return;
        var camera=new GameObject("灰盒实拍").AddComponent<Camera>();
        var rt=new RenderTexture(960,540,24);
        var image=new Texture2D(960,540,TextureFormat.RGB24,false);
        var previous=RenderTexture.active;
        try
        {
            camera.transform.position=eye;
            camera.transform.LookAt(at);camera.fieldOfView=fov;
            camera.targetTexture=rt;camera.Render(); RenderTexture.active=rt;
            image.ReadPixels(new Rect(0,0,960,540),0,0);image.Apply();
            File.WriteAllBytes("Logs/"+filename+".jpg",image.EncodeToJPG(94));
        }
        finally {RenderTexture.active=previous;camera.targetTexture=null;Object.Destroy(camera.gameObject);Object.Destroy(image);Object.Destroy(rt);}
    }
}
