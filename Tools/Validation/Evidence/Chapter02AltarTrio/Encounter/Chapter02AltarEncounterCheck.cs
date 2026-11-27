using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NarutoVR.SealCombat.Enemies;
using NarutoVR.SealCombat.Rpg;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>使用正式玩家、房间、租约、受击与乘员门控。每个击杀顺序独立启动 Unity 和新档。</summary>
[InitializeOnLoad]
public static class Chapter02AltarEncounterCheck
{
    const string SessionKey = "AltarTrio.Encounter";
    const string ScenePath = "Assets/Scene/Chapter02_SuspendedPath.unity";
    const string Carrier = "stone-carrier", Melee = "canjiou", Ranged = "zhihuocanji";
    static readonly string[] Types = {Carrier, Melee, Ranged};
    static readonly List<string> notes = new();
    static readonly List<string> tracks = new();
    static bool running;
    static double timeout;
    static string Output => Environment.GetEnvironmentVariable("ALTAR_TRIO_EVIDENCE");
    static string Order => Environment.GetEnvironmentVariable("ALTAR_TRIO_ORDER");

    static Chapter02AltarEncounterCheck() => EditorApplication.update += Update;
    static IEnumerable<T> All<T>() where T : Component => SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<T>(true));
    static void Record(string s) { notes.Add(s); Debug.Log("ALTAR_TRIO " + s); }
    static void Require(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
    static Vector3 Pos(Transform t) => t.position;
    static string P(Vector3 p) => p.ToString("F4");
    static async Task Frames(int count) { for (int i = 0; i < count; i++) await Awaitable.NextFrameAsync(); }
    static async Task Fixed(int count) { for (int i = 0; i < count; i++) await Awaitable.FixedUpdateAsync(); }
    static async Task Wait(Func<bool> condition, string name, Func<string> detail = null, double seconds = 22)
    {
        double end = EditorApplication.timeSinceStartup + seconds;
        while (!condition() && EditorApplication.timeSinceStartup < end) await Awaitable.NextFrameAsync();
        Require(condition(), "超时 "+name+" "+detail?.Invoke());
    }

    public static void Run()
    {
        if (!Application.isBatchMode || !Application.dataPath.Replace('\\','/').EndsWith("/NarutoVR-CarrierQA-20261125/Assets",StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("必须使用专属 CarrierQA 验证副本");
        if (Order != "carrier-first" && Order != "carrier-last") throw new ArgumentException("顺序必须为 carrier-first 或 carrier-last");
        Directory.CreateDirectory(Output);
        var scene = EditorSceneManager.OpenScene(ScenePath);
        var room = Chapter02AltarPreviewWitness.ReadScene(scene,out bool changed);
        var player=room.rpg;
        player.autoLoad=false; player.saveFileName="qa-altar-trio-"+Guid.NewGuid().ToString("N")+".json";
        File.WriteAllLines(Path.Combine(Output,"setup.txt"),new[]{"scene="+scene.path, "patchModified="+changed, "freshSave="+player.saveFileName}
            .Concat(room.spawnPoints.Select(s=>s.enemyId+" marker="+s.point.name+" "+P(s.point.position)+" yaw="+s.point.eulerAngles.y+" summoned="+s.isSummoned)));
        SessionState.SetBool(SessionKey,true);
        EditorApplication.EnterPlaymode();
    }
    static async void Update()
    {
        if (running && EditorApplication.timeSinceStartup>timeout) { Record("FAIL 全程超时"); End(1); return; }
        if (!Application.isPlaying || !SessionState.GetBool(SessionKey,false)) return;
        SessionState.EraseBool(SessionKey); running=true; timeout=EditorApplication.timeSinceStartup+210;
        try { await new Scenario().Play(); Record("PASS "+Order); End(0); }
        catch (Exception error) { Record("FAIL "+error); End(1); }
    }
    static void End(int code)
    {
        running=false;
        File.WriteAllLines(Path.Combine(Output,"assertions.txt"),notes);
        File.WriteAllLines(Path.Combine(Output,"states.csv"),tracks);
        EditorApplication.Exit(code);
    }

    sealed class Scenario
    {
        readonly StoneCarrierPost post=All<StoneCarrierPost>().Single();
        readonly Dictionary<string,EnemyLease> leases=new();
        readonly Dictionary<string,int> starts=new(), contacts=new();
        readonly Dictionary<string,Vector3> initial=new();
        readonly Dictionary<string,float> maxDrift=new();
        readonly HashSet<long> seenIds=new();
        CombatRoomEncounter room;
        LinearRpgController player;
        BallastBasket basket;
        BallastStone rock;
        Rigidbody body;
        GlyphHeavyBody heavy;
        Chapter02AltarPreviewWitness.Rig originalRig;
        int stoneId, bodyId, cleared, baselineCreated, baselineReused;
        Vector3 inside, outside;
        Transform head => player.locomotion.head;
        IEnumerable<EnemyLease> Live => leases.Values.Where(l=>!l.Defeated);
        void MoveFloor(Vector3 floor)
        {
            player.locomotion.rigRoot.position+=floor-player.locomotion.PlayerFloorPosition;
            Physics.SyncTransforms();
        }
        void MoveHead(Vector3 point) { player.locomotion.rigRoot.position+=point-head.position; Physics.SyncTransforms(); }
        void ToBasket() => MoveHead(basket.passengerVolume.transform.TransformPoint(basket.passengerVolume.center));
        bool HeadInBasket()
        {
            var box=basket.passengerVolume;
            var delta=box.transform.InverseTransformPoint(head.position)-box.center;
            var half=box.size*.5f;
            return Mathf.Abs(delta.x)<=half.x && Mathf.Abs(delta.y)<=half.y && Mathf.Abs(delta.z)<=half.z;
        }
        void Identity(int deaths)
        {
            originalRig.Check();
            var active=room.enemyManager.ActiveEnemies.Values.Where(l=>Types.Contains(l.EnemyId)).ToArray();
            foreach (var l in active) seenIds.Add(l.Id);
            Require(seenIds.SetEquals(leases.Values.Select(l=>l.Id)), "出现新的房间租约");
            Require(active.Count(l=>!l.Defeated)==3-deaths && Live.Count()==3-deaths &&
                Live.All(l=>room.enemyManager.IsCurrent(l) && active.Contains(l)), "存活租约身份或数量改变");
            Require(room.enemyManager.CreatedCount==baselineCreated && room.enemyManager.ReusedCount==baselineReused, "退出或重入触发创建或从池再次取出");
            Require(basket.Stone==rock && rock.GetInstanceID()==stoneId && body && body.GetInstanceID()==bodyId, "镇石或刚体身份改变");
            Require(player.Player.Alive && player.CanAct, "测试中玩家倒地或失去行动权");
        }
        void Sample(string step)
        {
            foreach (var l in leases.Values.OrderBy(x=>x.Id))
            {
                if(l.Defeated || !room.enemyManager.IsCurrent(l))continue;
                var c=l.Controller;var p=c.transform.position; string id=l.EnemyId;
                maxDrift[id]=Mathf.Max(maxDrift[id],Vector3.Distance(initial[id],p));
                tracks.Add(FormattableString.Invariant($"{Time.frameCount},{Time.time:F4},{step},{id},{l.Id},{c.GetInstanceID()},{p.x:F4},{p.y:F4},{p.z:F4},{c.CurrentBehaviour},{c.CurrentAttack},{starts[id]},{contacts[id]},{c.Entity.Combat.Health},{rock.GetInstanceID()},{(int)rock.State},{basket.Boarded},{room.IsComplete}"));
            }
        }
        async Task RoundTrip(int deaths)
        {
            var before=Live.ToDictionary(l=>l.Id,l=>l.Controller.Entity.Combat.Health);
            MoveFloor(outside);
            await Wait(()=>!room.PlayerInside && Live.All(l=>!l.Controller.CanReceiveHit),"出界并暂停",()=>room.Status);
            Vector3 outFloor=player.locomotion.PlayerFloorPosition;
            await Fixed(5);
            Identity(deaths); Sample("out-"+deaths);
            Require(!room.IsComplete && cleared==0 && !basket.Boarded, "退场造成清场或篮提前开放");
            MoveFloor(inside);
            await Wait(()=>room.PlayerInside && Live.All(l=>l.Controller.CanReceiveHit),"重进并唤醒",()=>room.Status);
            Identity(deaths); Sample("in-"+deaths);
            Require(Live.All(l=>Mathf.Approximately(l.Controller.Entity.Combat.Health,before[l.Id])), "退场重入重置残血");
            Record($"退场重入 deaths={deaths} 出界脚底={P(outFloor)} 存活={string.Join(",",Live.Select(l=>l.Id))} 同石={stoneId}/{bodyId} 新建累计={baselineCreated} 复用累计={baselineReused} 清场事件={cleared}");
        }
        async Task CheckBasketClosed(int deaths)
        {
            ToBasket(); await Fixed(5);
            Require(HeadInBasket() && !basket.Boarded && !heavy.CanManipulate && !room.IsComplete && cleared==0,"身处原乘员盒且仍有敌人时失去门控");
            Identity(deaths); Sample("basket-closed-"+deaths);
            Record($"提前登篮 deaths={deaths} 玩家头在原盒={HeadInBasket()} 石状态={rock.State} 可牵引={heavy.CanManipulate} 就位许可={basket.Boarded} 已清场={room.IsComplete}");
            MoveFloor(inside);
            await Wait(()=>room.PlayerInside && Live.All(l=>l.Controller.CanReceiveHit),"退回祭坛，敌人恢复");
        }
        public async Task Play()
        {
            room=All<CombatRoomEncounter>().Single(x=>x.spawnPoints.Any(s=>s.point==post.transform));
            player=room.rpg; basket=post.basket;
            var bridge=All<Chapter02BridgeEquipment>().Single();
            await Wait(()=>player.CanAct,"正式 RPG 新档",()=>player.Status,60);
            originalRig=new Chapter02AltarPreviewWitness.Rig(player);
            Record(originalRig.Ids);
            var settings = new SerializedObject(room);
            bool preview = settings.FindProperty("showEnemiesBeforeEngagement")?.boolValue ?? false;
            float previewMetres = settings.FindProperty("previewDistance")?.floatValue ?? 0f;
            Require(player.StageAt(player.StageIndex)==bridge.approachStage && !bridge.repairedDeck.activeInHierarchy &&
                !player.autoLoad && player.saveFileName.StartsWith("qa-altar-trio-")
                && !room.HasStarted && (preview || !basket.Stone),"新档出生阶段/桥/遭遇原状不符");
            Record("新档入口 "+player.StageAt(player.StageIndex).stageId+" save="+player.SavePath+" 桥闭、未接敌");
            MoveFloor(player.CheckpointAt(1).floorPosition);
            await Wait(()=>player.StageAt(player.StageIndex)==bridge.drawingStage && bridge.drawingObjective.IsRunning,"进入桥教学",()=>player.Status);
            bridge.CompleteDrawing();
            await Wait(()=>player.StageAt(player.StageIndex)==bridge.originalFirstStage && bridge.repairedDeck.activeInHierarchy,"修桥后正式阶段",()=>player.Status);
            Require(!room.HasStarted && (preview || !basket.Stone) && !basket.Boarded,"进场前提前生成或开放");
            Record("模拟教学画成回调后进入 "+player.StageAt(player.StageIndex).stageId+"，场景桥已启用");
            inside=post.transform.position-Vector3.ProjectOnPlane(post.patrolPoints[0].position-post.transform.position,Vector3.up).normalized*3f;
            outside=room.area.transform.TransformPoint(room.area.center-Vector3.right*(room.area.size.x*.5f+2f));
            outside.y=inside.y; room.Cleared+=()=>cleared++;
            MoveFloor(outside); await Fixed(12);
            Chapter02AltarPreviewWitness outsideBatch=null;
            if(preview)
            {
                await Wait(()=>room.enemies.Length==3 && basket.Stone,"配置启用的守敌预加载",()=>room.Status);
                outsideBatch=new Chapter02AltarPreviewWitness(room,post);
                Record("线外守敌 "+outsideBatch.Ids);
            }
            else Require(!room.IsLoadingEnemies && room.enemyManager.ActiveCount==0 && !basket.Stone,"预显示关闭时，站在线外不应预加载");
            Require(!room.HasStarted && !room.PlayerInside && !basket.Boarded,"线外尚未接敌");
            Record($"接敌线外2米：showEnemiesBeforeEngagement={preview} previewDistance={previewMetres} 活跃租约={room.enemyManager.ActiveCount} 已开始={room.HasStarted}");
            MoveFloor(inside);
            await Wait(()=>room.HasStarted && room.enemies.Length==3 && basket.Stone,"三人真实异步生成",()=>room.Status+" / "+room.enemyManager.Status,65);
            if(outsideBatch!=null){outsideBatch.CheckEngaged();Record("过线同批 "+outsideBatch.Ids);}
            foreach (var l in room.enemyManager.ActiveEnemies.Values.Where(l=>room.enemies.Contains(l.Controller)))
            {
                leases.Add(l.EnemyId,l); seenIds.Add(l.Id); string id=l.EnemyId; var c=l.Controller;
                starts[id]=contacts[id]=0; initial[id]=Pos(c.transform); maxDrift[id]=0f;
                c.onAttackStarted.AddListener(_=>starts[id]++); c.onAttackContact.AddListener(_=>contacts[id]++);
                Record($"出生 {id} 租约={l.Id} 控制器={c.GetInstanceID()} 位置={P(initial[id])} 可导航={c.Agent&&c.Agent.isOnNavMesh} 目标={(c.target?c.target.name:"空")}");
            }
            Require(leases.Count==3 && Types.All(leases.ContainsKey),"三种敌人或租约不全");
            rock=basket.Stone; body=rock.GetComponent<Rigidbody>(); heavy=rock.GetComponent<GlyphHeavyBody>();
            stoneId=rock.GetInstanceID(); bodyId=body.GetInstanceID();
            Require(rock==leases[Carrier].Controller.GetComponent<StoneCarrier>().Stone && rock.State==BallastStone.Phase.Carried,"原篮没有绑定出生工奴的背石");
            baselineCreated=room.enemyManager.CreatedCount; baselineReused=room.enemyManager.ReusedCount;
            tracks.Add("frame,time,step,enemy,lease,controller,x,y,z,behaviour,attack,starts,contacts,hp,stone,stonePhase,boarded,clear");
            Identity(0);
            for(int i=0;i<30;i++) { await Fixed(2); Sample("engagement"); }
            await RoundTrip(0);
            await CheckBasketClosed(0);
            var order=Order=="carrier-first"?new[]{Carrier,Melee,Ranged}:new[]{Ranged,Melee,Carrier};
            // 非致命真实命中先改变一个仍然存活的单位的血量，以发现重进时错误地回满。
            var witness=leases[Melee].Controller.Entity;
            var hp=witness.Combat.Health;
            Require(witness.ReceiveHit("trio-wound",1,0,attacker:head) && witness.Combat.Health<hp,"非致命验证命中失败");
            Record($"重入参考残血：{Melee} {hp} → {witness.Combat.Health}");
            for(int i=0;i<order.Length;i++)
            {
                var l=leases[order[i]];
                Require(l.Controller.Entity.ReceiveHit("trio-"+order[i],l.Controller.Entity.Combat.Health,0,attacker:head) && l.Defeated,"致命结算失败 "+l.EnemyId);
                await Frames(3); Identity(i+1); Sample("kill-"+(i+1));
                Require(rock.State==(leases[Carrier].Defeated?BallastStone.Phase.Dropped:BallastStone.Phase.Carried),"工奴生死与原石状态不符");
                Record($"击败 {i+1}/3 {l.EnemyId} 租约={l.Id} 剩余={Live.Count()} room.IsComplete={room.IsComplete} 清场事件={cleared} 石状态={rock.State} 同石={stoneId}/{bodyId}");
                if(i<2)
                {
                    Require(!room.IsComplete && cleared==0,"三亡前触发清场");
                    if(i==0) await RoundTrip(1);
                    await CheckBasketClosed(i+1);
                }
            }
            await Wait(()=>room.IsComplete && cleared==1,"三亡触发唯一清场");
            Require(!HeadInBasket() && !basket.Boarded && !heavy.CanManipulate,"未登篮已开放");
            ToBasket(); await Fixed(5); await Wait(()=>basket.Boarded && heavy.CanManipulate,"清场登原篮后开放");
            Require(HeadInBasket() && cleared==1 && room.IsComplete && basket.Stone==rock && rock.State==BallastStone.Phase.Dropped,"清场后作用在错误的石或重复发事件");
            Identity(3);
            Record($"清场后原槽={P(basket.slot.position)} 头在原乘员盒={HeadInBasket()} Boarded={basket.Boarded} 可牵引={heavy.CanManipulate} 清场事件={cleared} 同石={stoneId}/{bodyId}");
            foreach(var id in Types)Record(FormattableString.Invariant($"观察 {id} 自出生最大位移={maxDrift[id]:F3} m 攻击开始={starts[id]} 生效窗口={contacts[id]}"));
        }
    }
}
