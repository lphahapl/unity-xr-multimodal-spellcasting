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
using UnityEngine.SceneManagement;

/// <summary>新档首次接敌，让三个原控制器自由运行。记录交易事件、实际火核及每两人的地面距离。</summary>
[InitializeOnLoad]
public static class Chapter02AltarLiveCheck
{
    const string Key="Chapter02AltarTrio.Live";
    static string Dir=>Environment.GetEnvironmentVariable("ALTAR_TRIO_EVIDENCE");
    static readonly List<string> notes=new(), rows=new();
    static bool running;
    static double deadline;
    static IEnumerable<T> All<T>() where T:Component=>SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<T>(true));
    static Chapter02AltarLiveCheck()=>EditorApplication.update+=Update;
    static void Need(bool yes,string label){if(!yes)throw new InvalidOperationException(label);}
    static void Note(string s){notes.Add(s);Debug.Log("ALTAR_LIVE "+s);}
    static float Flat(Vector3 a, Vector3 b)=>Vector3.ProjectOnPlane(a-b,Vector3.up).magnitude;
    static async Task Wait(Func<bool> predicate,string what)
    {
        double until=EditorApplication.timeSinceStartup+65;
        while(!predicate() && EditorApplication.timeSinceStartup<until)await Awaitable.NextFrameAsync();
        Need(predicate(),what);
    }
    public static void Run()
    {
        Need(Application.isBatchMode && Application.dataPath.Replace('\\','/').EndsWith("/NarutoVR-CarrierQA-20261125/Assets",StringComparison.OrdinalIgnoreCase),"专用副本");
        Directory.CreateDirectory(Dir);
        var scene=EditorSceneManager.OpenScene("Assets/Scene/Chapter02_SuspendedPath.unity");
        var room=Chapter02AltarPreviewWitness.ReadScene(scene,out bool changed);
        var p=room.rpg;
        p.autoLoad=false; p.saveFileName="qa-altar-live-"+Guid.NewGuid().ToString("N")+".json";
        File.WriteAllLines(Path.Combine(Dir,"setup.txt"),new[]{"scene="+scene.path,"patchModified="+changed,"newSave="+p.saveFileName});
        SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
    }
    static async void Update()
    {
        if(running && EditorApplication.timeSinceStartup>deadline){Note("FAIL 全程超时"); End(1);return;}
        if(!Application.isPlaying || !SessionState.GetBool(Key,false))return;
        SessionState.EraseBool(Key);running=true;deadline=EditorApplication.timeSinceStartup+180;
        try{await Test();Note("PASS 三敌同时实战");End(0);}
        catch(Exception e){Note("FAIL "+e);End(1);}
    }
    static void End(int code){running=false;File.WriteAllLines(Path.Combine(Dir,"assertions.txt"),notes);File.WriteAllLines(Path.Combine(Dir,"states.csv"),rows);EditorApplication.Exit(code);}
    sealed class Counts{public int started,window,hits;public float drift;public readonly HashSet<string> states=new();}
    static async Task Test()
    {
        var bridge=All<Chapter02BridgeEquipment>().Single();var p=bridge.player;
        var loc=p.locomotion;var post=All<StoneCarrierPost>().Single();
        var room=All<CombatRoomEncounter>().Single(r=>r.spawnPoints.Any(s=>s.point==post.transform));
        var line=post.transform.parent.Find("首次接敌线");
        var counts=new Dictionary<EnemyController,Counts>();
        Counts Get(EnemyController c){if(!counts.TryGetValue(c,out var q))counts.Add(c,q=new Counts());return q;}
        var damage=new List<EnemyAttackHit>();
        var receiver=p.GetComponent<RpgDamageReceiver>();
        using var begin=EventCenter.Subscribe(EnemyEvents.AttackStarted,n=>{if(n.Enemy)Get(n.Enemy).started++;});
        using var contact=EventCenter.Subscribe(EnemyEvents.AttackWindowOpened,n=>{if(n.Enemy)Get(n.Enemy).window++;});
        using var hit=EventCenter.Subscribe(EnemyEvents.DamageAccepted,n=>{if(n.Source && n.Receiver==receiver){damage.Add(n);Get(n.Source.GetComponent<EnemyController>()).hits++;}});
        await Wait(()=>p.CanAct,"新档玩家未就绪");
        Need(p.StageAt(p.StageIndex)==bridge.approachStage && !room.HasStarted,"进入顺序");
        var originalRig=new Chapter02AltarPreviewWitness.Rig(p);Note(originalRig.Ids);
        void To(Vector3 f){loc.rigRoot.position+=f-loc.PlayerFloorPosition;Physics.SyncTransforms();}
        To(p.CheckpointAt(1).floorPosition);
        await Wait(()=>p.StageAt(p.StageIndex)==bridge.drawingStage && bridge.drawingObjective.IsRunning,"石案阶段");
        bridge.CompleteDrawing();
        await Wait(()=>p.StageAt(p.StageIndex)==bridge.originalFirstStage && bridge.repairedDeck.activeInHierarchy,"上层祭坛阶段");
        Vector3 heading=Vector3.ProjectOnPlane(post.transform.position-line.position,Vector3.up).normalized;
        loc.rigRoot.RotateAround(loc.head.position,Vector3.up,Vector3.SignedAngle(Vector3.ProjectOnPlane(loc.head.forward,Vector3.up),heading,Vector3.up));
        Chapter02AltarPreviewWitness outsideBatch=null;
        if(room.showEnemiesBeforeEngagement)
        {
            To(line.position-heading*2);
            await Wait(()=>room.enemies.Length==3 && post.basket.Stone,"线外守敌就位");
            outsideBatch=new Chapter02AltarPreviewWitness(room,post);
            Note("线外守敌 "+outsideBatch.Ids);
        }
        To(line.position+heading*3);
        await Wait(()=>room.HasStarted && room.enemies.Length==3 && post.basket.Stone,"祭坛三敌出生");
        if(outsideBatch!=null){outsideBatch.CheckEngaged();Note("过线同批 "+outsideBatch.Ids);}
        var party=room.enemyManager.ActiveEnemies.Values.Where(l=>room.enemies.Contains(l.Controller)).ToDictionary(l=>l.EnemyId);
        string[] keys={"stone-carrier","canjiou","zhihuocanji"};
        Need(party.Count==3 && keys.All(party.ContainsKey),"三种普通租约身份");
        var start=keys.ToDictionary(k=>k,k=>party[k].Controller.transform.position);
        var head0=loc.head.position;var forward0=loc.head.forward;
        Need(Vector3.Angle(heading,Vector3.ProjectOnPlane(forward0,Vector3.up))<1f,"固定真实入口朝向");
        var stone=post.basket.Stone;float hp0=p.Player.Health, nearCM=float.PositiveInfinity,nearCR=nearCM,nearMR=nearCM;
        var shooter=typeof(EnemyFireCore).GetField("shooter",BindingFlags.Instance|BindingFlags.NonPublic);
        Need(shooter!=null,"无法读回真实火核发射者");
        var cores=new HashSet<int>();
        float t0=Time.time,next=t0;bool interrupted=false;
        rows.Add("time,playerHP,enemy,lease,x,y,z,navSpeed,behaviour,attack,starts,windows,damageAccepted,fireCores,carrierMelee_m,carrierRanged_m,meleeRanged_m");
        while(Time.time-t0<12f && p.Player.Alive)
        {
            foreach(var core in UnityEngine.Object.FindObjectsByType<EnemyFireCore>(FindObjectsSortMode.None))
                if((EnemyController)shooter.GetValue(core)==party["zhihuocanji"].Controller)cores.Add(core.GetInstanceID());
            var a=party[keys[0]].Controller.transform.position;var b=party[keys[1]].Controller.transform.position;var c=party[keys[2]].Controller.transform.position;
            var cm=Flat(a,b);var cr=Flat(a,c);var mr=Flat(b,c);nearCM=Mathf.Min(nearCM,cm);nearCR=Mathf.Min(nearCR,cr);nearMR=Mathf.Min(nearMR,mr);
            foreach(string id in keys)
            {
                var l=party[id];var actor=l.Controller;var q=Get(actor);var pos=actor.transform.position;
                q.drift=Mathf.Max(q.drift,Flat(start[id],pos));q.states.Add(actor.CurrentBehaviour.ToString());
                if(actor.CurrentBehaviour==EnemyController.Behaviour.Suspended)interrupted=true;
                if(Time.time>=next)rows.Add(FormattableString.Invariant($"{Time.time-t0:F3},{p.Player.Health},{id},{l.Id},{pos.x:F4},{pos.y:F4},{pos.z:F4},{actor.Agent.velocity.magnitude:F3},{actor.CurrentBehaviour},{actor.CurrentAttack},{q.started},{q.window},{q.hits},{cores.Count},{cm:F3},{cr:F3},{mr:F3}"));
            }
            if(Time.time>=next)next=Time.time+.1f;
            await Awaitable.NextFrameAsync();
        }
        Note(FormattableString.Invariant($"时长={Time.time-t0:F3}s 玩家HP={hp0}→{p.Player.Health} 头世界={head0:F3} 最终={loc.head.position:F3} 转角={Vector3.Angle(forward0,loc.head.forward):F3}° 朝向={forward0:F3} 进房状态={room.Status}"));
        foreach(string id in keys){var q=Get(party[id].Controller); Note(FormattableString.Invariant($"{id} lease={party[id].Id} 最大位移={q.drift:F3}m 行为={string.Join("/",q.states)} 启动={q.started} 生效窗={q.window} 玩家接受命中={q.hits}"));}
        foreach(var h in damage)Note($"玩家伤害事件 source={keys.Single(k=>party[k].Controller.Entity==h.Source)} id={h.AttackId} damage={h.Damage}");
        Note(FormattableString.Invariant($"掷火源确认的独立在飞火核={cores.Count} ids={string.Join(",",cores)} 三对最小水平距离 工奴近战={nearCM:F3} m 工奴掷火={nearCR:F3} m 近战掷火={nearMR:F3} m"));
        Need(!interrupted && party.Values.All(l=>!l.Defeated) && !room.IsComplete && post.basket.Stone==stone &&
             stone.State==BallastStone.Phase.Carried && (Time.time-t0>=12f || !p.Player.Alive),"有暂停、非主动改变或观察窗口过短");
        Need(Flat(head0,loc.head.position)<.05f && Vector3.Angle(forward0,loc.head.forward)<1f,"观察阶段玩家离开位置或朝向改变");
        originalRig.Check();Note("结束 "+originalRig.Ids);
    }
}
