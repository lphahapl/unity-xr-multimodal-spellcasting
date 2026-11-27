using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using NarutoVR.SealCombat.Editor;
using NarutoVR.SealCombat.Items;
using NarutoVR.SealCombat.Rpg;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class ChapterStakeChecks
{
    const string Key="Ch02.PrivateStake";
    const string SaveName="Logs/StakeSaveName.txt";
    const string Report="Logs/ChapterStakeChecks.txt";
    static IEnumerator test;
    static int frame=-1;
    static double deadline;
    static ChapterStakeChecks(){EditorApplication.update+=Tick;}
    public static void Fresh()=>Start("fresh");
    public static void FreshInstalled()=>Start("installed");
    public static void Cold()=>Start("cold");
    public static void ColdField()=>Start("field");
    static void Start(string mode)
    {
        bool cold=mode=="cold" || mode=="field";
        if(!Application.isBatchMode)throw new InvalidOperationException("只在隔离副本运行");
        var path=ChapterStakeInstaller.ScenePath;
        if(!cold)
        {
            using var sha=SHA256.Create();
            var hash=BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant();
            if(mode=="fresh" && hash!="5fde465963731f04617d87206389f89f14391f10b2455e8aaf5fbd00b1e94e0f")
                throw new InvalidOperationException("Unexpected QA baseline: "+hash);
            File.WriteAllText(Report,(mode=="fresh"?"baseline ":"installed candidate ")+hash+"\n");
            File.WriteAllText(SaveName,"stake-qa-"+Guid.NewGuid().ToString("N")+".json");
        }
        var scene=EditorSceneManager.OpenScene(path);
        if(mode!="fresh" && !Object.FindFirstObjectByType<ChapterStakeSupply>())
            throw new InvalidOperationException("独立进程必须从磁盘读到已装配的领取组件。");
        var supply=ChapterStakeInstaller.Configure(scene);
        if(mode=="fresh")
        {
            Need(supply==ChapterStakeInstaller.Configure(scene),"安装入口可重复调用");
            if(!EditorSceneManager.SaveScene(scene))throw new IOException(path);
        }
        var p=supply.GetComponent<LinearRpgController>();
        p.saveFileName=File.ReadAllText(SaveName);p.defaultSaveSource=CampaignSaveSource.CurrentScene;p.autoLoad=cold;
        if(mode=="field")p.saveFileName="field-"+p.saveFileName;
        SessionState.SetString(Key,mode);
        Log(DateTimeOffset.Now.ToString("o")+" "+mode+" Unity "+Application.unityVersion);
        EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        string mode=SessionState.GetString(Key,"");
        if(mode=="" || !Application.isPlaying || frame==Time.frameCount || Time.frameCount<3)return;
        frame=Time.frameCount;
        try
        {
            if(test==null){deadline=EditorApplication.timeSinceStartup+65;test=Trial(mode);}
            if(EditorApplication.timeSinceStartup>deadline)
                throw new TimeoutException(Object.FindFirstObjectByType<LinearRpgController>()?.Status);
            if(test.MoveNext())return;
            Log("PASS "+mode);SessionState.EraseString(Key);EditorApplication.Exit(0);
        }
        catch(Exception e){Log("FAIL "+e);SessionState.EraseString(Key);EditorApplication.Exit(1);}
    }
    static void Log(string s){File.AppendAllText(Report,s+"\n");Debug.Log("CH02_STAKE "+s);}
    static void Need(bool b,string why){if(!b)throw new InvalidOperationException(why);Log("OK "+why);}
    static IEnumerator Trial(string mode)
    {
        Time.captureDeltaTime=.02f;
        var supply=Object.FindFirstObjectByType<ChapterStakeSupply>();
        var ration=supply.GetComponent<ChapterRationSupply>();
        var belt=supply.belt;var p=supply.GetComponent<LinearRpgController>();var home=supply.pocket;
        while(!p.CanAct || !belt.Initialised)yield return null;
        Need(ration && ration.interaction.sharedBelt==belt && belt.player==p && belt.slots.Length==5 &&
            Object.FindObjectsByType<ToolBeltInteraction>(FindObjectsSortMode.None).Length==1,"同一章内玩家、同一五槽带");
        if(mode=="field")
        {
            while(!supply.Item || !supply.Item.FieldActive || !ration.HasPouch)yield return null;
            var cp=JsonUtility.FromJson<SaveEnvelope>(File.ReadAllText(p.SavePath)).progress.checkpoint;
            Need(supply.Item.InRange && supply.Item.Planted && !supply.Item.Dock && !supply.TryGrant() &&
                Vector3.Distance(supply.Item.transform.position+(supply.Item.insertionTip.position-supply.Item.transform.position)*(1f-1f/supply.Item.CurrentSize),new Vector3(cp.stake.x,cp.stake.y,cp.stake.z))<.005f &&
                Math.Abs(supply.Item.SecondsLeft-cp.stake.fieldSeconds)<.16,"冷进程恢复原地力场与剩余时长");
            foreach(var cd in cp.cooldowns)
                Need(Math.Abs(p.Player.GetCooldown(cd.group,p.GameTime)-cd.remainingSeconds)<.25,"冷读场内剩余 "+cd.group);
            yield break;
        }
        if(mode=="cold")
        {
            while(!supply.Item || supply.Item.Dock!=home || home.IsSnapping || !ration.HasPouch)yield return null;
            Need(!supply.Item.FieldActive && !supply.TryGrant() && ration.interaction.pouch.InPouch==4 &&
                belt.items.Count(i=>i is CooldownStake)==1,"冷进程恢复窄鞘铜橛和中央干粮，禁止重领");
            yield break;
        }
        while(!supply.Item || home.IsSnapping) yield return null;
        Need(supply.Item.Dock==home && !ration.HasPouch && !supply.TryGrant(),"新局只有一根初始铜橛，默认窄鞘");
        supply.RestoreEquipment(new RpgCheckpointData());
        var previous=Object.Instantiate(supply.prefab,home.opening.position,home.opening.rotation);
        Need(previous.TryDock(home,Quaternion.Euler(previous.stowEuler)),"旧档已占窄鞘的实物");
        for(int i=0;i<8;i++)yield return null;
        Need(home.SnappedObject==previous.gameObject && (!supply.Item || (supply.Item.Dock && supply.Item.Dock!=home)),
             "旧档窄鞘占用时保留原实物，另选合尺寸的通用口或候补");
        if(supply.Item) Log("候补槽："+supply.Item.Dock.name);
        previous.gameObject.SetActive(false);Object.Destroy(previous.gameObject);
        supply.RestoreEquipment(JsonUtility.FromJson<RpgCheckpointData>("{\"mana\":99}"));
        while(!supply.Item || home.IsSnapping) yield return null;
        Need(supply.Item.Dock==home && !supply.TryGrant() && ration.TryGrant(),"缺少铜橛的旧存档补发一次，干粮仍独立领取");
        while(home.IsSnapping || ration.centralPocket.IsSnapping)yield return null;
        var item=supply.Item;
        var body=p.locomotion.rigRoot;var initialPos=body.position;
        var direction=Vector3.ProjectOnPlane(p.locomotion.head.forward,Vector3.up).normalized;
        var at=p.locomotion.PlayerFloorPosition+direction*.55f;
        Need(Physics.Raycast(at+Vector3.up*.35f,Vector3.down,out var floor,.7f,p.locomotion.floorLayers,QueryTriggerInteraction.Ignore),"正式石地可达");
        var grip=floor.point+Vector3.up*(.025f+item.grip.localPosition.y-item.insertionTip.localPosition.y);
        void Hand(Vector3 pos,bool closed,Quaternion rotation)
        {
            var now=Time.realtimeSinceStartupAsDouble;
            var hand=new PlayerHandObservation{rightValid=true,gripPoseValid=true,pinchValid=true,
                headPosition=p.locomotion.head.position,headForward=p.locomotion.head.forward,
                rightObserved=now,rightTip=pos,gripPosition=pos,gripRotation=rotation,systemPinch=true,systemPressed=closed};
            if(belt.DrivenByGlyph)p.GlyphPen.Observe(hand,now);
            else belt.Step(hand,now,.02f);
        }
        void Take(Vector3 atGrip){Hand(atGrip,false,Quaternion.identity);Hand(atGrip,true,Quaternion.identity);Need(belt.Held==supply.Item,"右手领取铜橛");}
        void Plant(){Hand(grip,true,Quaternion.identity);Hand(grip,false,Quaternion.identity);Need(item.Planted && item.FieldActive && item.InRange,"插放开启区域");}
        Need(home.opening.name=="TallSleeve" && item.Dock==home && item.MeasureInSlot(home.opening,home.opening.rotation,out var size)
            && size.size.x<=home.width && size.size.z<=home.thickness && !item.MeasureInSlot(home.opening,home.opening.rotation*Quaternion.Euler(180,0,0),out _),
            $"窄鞘 {home.width:F3}×{home.thickness:F3}×{home.depth:F3} m，入鞘段朝向与尺寸通过");
        var upper=item.GetComponent<BoxCollider>();
        var shoulder=home.opening.InverseTransformPoint(item.transform.TransformPoint(upper.center-Vector3.up*upper.size.y*.5f));
        Need(shoulder.y>0 && item.stowEnvelope.size.x<=home.width &&
            home.opening.InverseTransformPoint(item.insertionTip.position).y<0,"物理锥尖进入鞘口，宽杆碰撞体留在鞘口上方");
        var bag=ration.interaction.pouch;
        Need(belt.TryTake(bag,true) && !belt.TryTake(item,false),"持干粮时铜橛不能抢手");
        belt.ReleaseAuxiliary(bag,true);
        while(ration.centralPocket.IsSnapping)yield return null;
        Take(home.opening.position);
        Need(item.stowEnvelope.enabled && upper.enabled,"出鞘恢复实体碰撞");
        Need(!belt.TryTake(bag,true),"持铜橛时干粮不能抢手");
        Plant();
        var eye=floor.point-direction*4.8f+p.locomotion.head.right*2.9f+Vector3.up*2.1f;
        var camAt=floor.point+Vector3.up;
        var artAt=p.GameTime;
        Directory.CreateDirectory("Logs/StakeScreenFilm");
        for(int frame=0;frame<61;frame++)
        {
            while(p.GameTime<artAt+frame*.05)yield return null;
            if(frame==33)
            {
                Vector3 start=floor.point+Vector3.up*1.2f-direction*2.8f;
                if(StakeScreen.TryEntry(item.gameObject.scene,start,direction,4,out var impact)) impact.Ripple();
            }
            Capture(eye,camAt,46,"StakeScreenFilm/"+frame.ToString("D3"));
        }
        File.Copy("Logs/StakeScreenFilm/003.jpg","Logs/StakeChapter-unfold.jpg",true);
        File.Copy("Logs/StakeScreenFilm/021.jpg","Logs/StakeChapter-open.jpg",true);
        const string testGroup="fireball";
        Need(p.Player.TryReserveCast(Guid.NewGuid().ToString("N"),testGroup,testGroup,0,8,p.GameTime,out var cast,out _),"技能冷却事务");
        cast.Commit();
        float[] Read()=>new[]{p.Player.GetCooldown(testGroup,p.GameTime)};
        void Rate(string step,double start,float[] before,int rate)
        {
            float actual=Read()[0];
            Need(Math.Abs(actual-Math.Max(0,before[0]-(p.GameTime-start)*rate))<.005,step+"实际冷却 "+actual);
        }
        var t=p.GameTime;var before=Read();
        while(p.GameTime<t+.16)yield return null;
        Rate("场内",t,before,1);
        body.position+=p.locomotion.head.right*2.5f;
        while(item.InRange)yield return null;
        t=p.GameTime;before=Read();
        while(p.GameTime<t+.14)yield return null;
        Rate("离场",t,before,1);
        body.position=initialPos;
        while(!item.InRange)yield return null;
        Need(p.SaveCheckpoint(),"场内检查点写盘");
        File.Copy(p.SavePath,Path.Combine(Path.GetDirectoryName(p.SavePath),"field-"+Path.GetFileName(p.SavePath)),true);
        SaveEnvelope Disk()=>JsonUtility.FromJson<SaveEnvelope>(File.ReadAllText(p.SavePath));
        var saved=Disk().progress.checkpoint;
        Need(saved.stake.owned && saved.stake.planted && saved.stake.fieldSeconds>0 &&
            saved.ration.owned && saved.cooldowns.Any(c=>c.group==testGroup),"同档含屏风剩余时长、技能冷却及干粮");
        Need(p.RestoreCheckpoint(),"玩家原入口读回插地快照");
        while(!supply.Item || !supply.Item.FieldActive)yield return null;
        item=supply.Item;
        Need(item.InRange && Math.Abs(item.SecondsLeft-saved.stake.fieldSeconds)<.12 &&
            Vector3.Distance(item.insertionTip.position,floor.point)<.01,"恢复世界锥尖和剩余有效期");
        foreach(var cd in saved.cooldowns)
            Need(Math.Abs(p.Player.GetCooldown(cd.group,p.GameTime)-cd.remainingSeconds)<.13,"真实剩余读回 "+cd.group);
        Take(item.GripPosition);
        Need(!item.FieldActive && !item.Planted,"拔起撤场");
        Hand(home.opening.position,false,home.opening.rotation);
        while(home.IsSnapping)yield return null;
        Need(item.Dock==home && !belt.Held && p.SaveCheckpoint(),"归鞘并写冷启动档");
        saved=Disk().progress.checkpoint;
        Need(saved.stake.pocket==Array.IndexOf(belt.slots,home)+1 && saved.stake.fieldSeconds==0 && saved.ration.owned,"回收档两件均保留");
    }
    static void Capture(Vector3 eye,Vector3 at,float fov,string filename)
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
