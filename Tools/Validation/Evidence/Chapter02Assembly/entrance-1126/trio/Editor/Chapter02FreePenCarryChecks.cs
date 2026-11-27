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
sealed class FreePenPhysicsInput : IDisposable
{
    readonly UnityEngine.LowLevel.PlayerLoopSystem original;
    public Action sample;
    public FreePenPhysicsInput()
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
        subs.Insert(script,new UnityEngine.LowLevel.PlayerLoopSystem{type=typeof(FreePenPhysicsInput),updateDelegate=Tick});
        fixedLoop.subSystemList=subs.ToArray();
        modified.subSystemList[fixedIndex]=fixedLoop;
        UnityEngine.LowLevel.PlayerLoop.SetPlayerLoop(modified);
    }
    void Tick()=>sample?.Invoke();
    public void Dispose(){sample=null;UnityEngine.LowLevel.PlayerLoop.SetPlayerLoop(original);}
}

[InitializeOnLoad]
public static class Chapter02FreePenCarryChecks
{
    const string Key="Ch02.FreePen.Carry";
    static string Out=>Environment.GetEnvironmentVariable("CH02_FREEPEN_QA_OUT");
    static bool running;
    static double timeout;
    static Chapter02FreePenCarryChecks()=>EditorApplication.update+=Update;
    static Transform[] All()=>SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
    static T One<T>() where T:Component=>All().Select(t=>t.GetComponent<T>()).Single(c=>c);
    static string P(Vector3 p)=>p.ToString("F4");
    static void Log(string s){File.AppendAllText(Path.Combine(Out,"free-pen.txt"),s+Environment.NewLine);Debug.Log("CH02_FREE_PEN "+s);}
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
        if(!Application.isBatchMode || string.IsNullOrWhiteSpace(Out))throw new InvalidOperationException("Set CH02_FREEPEN_QA_OUT in isolated batch project");
        Directory.CreateDirectory(Out);
        EditorSceneManager.OpenScene("Assets/Scene/Chapter02_SuspendedPath.unity");
        var p=One<LinearRpgController>();p.autoLoad=true;p.saveFileName="ch02-freepen-"+Guid.NewGuid().ToString("N")+".json";
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
        if(Path.GetFileName(path).StartsWith("ch02-freepen-") && File.Exists(path))File.Delete(path);
        running=false;EditorApplication.Exit(code);
    }
    static void MoveRig(RpgLocomotionController loc,Vector3 floor)
    {loc.rigRoot.position+=floor-loc.PlayerFloorPosition;Physics.SyncTransforms();}
    static async Task Test()
    {
        File.WriteAllText(Path.Combine(Out,"free-pen.txt"),"");
        File.WriteAllText(Path.Combine(Out,"free-pen.csv"),"cast,step,stoneID,stoneX,stoneY,stoneZ,distance,locked"+Environment.NewLine);
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
        MoveRig(loc,post.transform.position-axis*3);
        await Until(()=>room.HasStarted && manager.ActiveEnemies.Values.Any(e=>e.EnemyId=="stone-carrier") && basket.Stone,
            "玩家进入原祭坛，由正式房间出生持石工奴");
        var lease=manager.ActiveEnemies.Values.Single(e=>e.EnemyId=="stone-carrier");
        var carrier=lease.Controller.GetComponent<StoneCarrier>();var stone=carrier.Stone;var rb=stone.GetComponent<Rigidbody>();
        var heavy=stone.GetComponent<GlyphHeavyBody>();int id=stone.GetInstanceID(),rigid=rb.GetInstanceID(),events=0;
        basket.onDocked.AddListener(()=>events++);
        Check(basket.Stone==stone && stone.State==BallastStone.Phase.Carried && !heavy.CanManipulate,"原石在工奴身上，还不能施加符力 "+id);
        await Until(()=>carrier.TryGetCarryPhase(out _),"原工奴搬运姿势有效");
        Check(lease.Controller.Entity.ReceiveHit("QA 千钧轻击",1,0,attacker:head) && carrier.Retreating,"原敌人轻击后进入撤离");
        await Until(()=>Vector3.ProjectOnPlane(lease.Controller.transform.position-drop,Vector3.up).magnitude<.54f,"撤到正式掉落边缘");
        Check(lease.Controller.Entity.ReceiveHit("QA 千钧击破",lease.Controller.Entity.Combat.Health,0,attacker:head) && lease.Defeated &&
            stone.State==BallastStone.Phase.Dropped && stone.GetInstanceID()==id,"原敌人结算，身上同一件镇石携原刚体掉落");
        await Until(()=>room.IsComplete && !loc.EncounterCombat,"原战区清场并回到可书写状态");
        await Fixed(25);
        var toward=Vector3.ProjectOnPlane(slot.position-stone.transform.position,Vector3.up).normalized;
        var floor=All().Single(t=>t.name=="示例槽心");var front=All().Single(t=>t.name=="A 上层篮口");
        MoveRig(loc,floor.position+toward*.65f);
        await Until(()=>basket.Boarded && heavy.CanManipulate,"进入原乘员盒才开放原石受力");
        using var handFeed=new FreePenPhysicsInput();
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
            for(int cast=0;cast<12 && events==0;cast++)
            {
                bool lift=cast==2 || cast>=4;
                var stance=lift?front.position+toward*.75f:floor.position+toward*.65f;
                if(Vector3.ProjectOnPlane(stone.transform.position-slot.position,Vector3.up).magnitude>settings.reach+.6f)
                    stance=front.position+toward*.15f;
                MoveRig(loc,stance);await Fixed(2);
                Check(basket.passengerVolume.bounds.Contains(head.position),"施符站在原 A 乘员盒 "+P(head.position));
                await WriteGlyph();
                var body=new Vector3(head.position.x,loc.PlayerFloorPosition.y+1.6f,head.position.z);
                var tip=lift?body-toward*.56f-Vector3.up*.18f:body-toward*.90f-Vector3.up*.55f;
                var hull=stone.GetComponent<BoxCollider>().bounds;
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
                int n=0;
                handFeed.sample=()=>
                {
                    if(events>0 || n>=105)return;
                    float dt=Time.fixedDeltaTime;
                    // Two neighboring tracked poses describe one physical hand displacement. Engine FixedUpdate applies all forces.
                    Hand(grip,orient,true,dt*.1f);
                    if(n*dt<(lift?.25f:.38f))grip-=dir*(1.5f*dt);
                    Hand(grip,orient,true,dt*.9f);
                    if(n++%3==0)trace.Add(FormattableString.Invariant($"{cast+1},{n},{id},{stone.transform.position.x:F5},{stone.transform.position.y:F5},{stone.transform.position.z:F5},{Vector3.Distance(stone.transform.position,slot.position):F5},{line.enabled}"));
                };
                while(n<105 && events==0)await Fixed();
                handFeed.sample=null;
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
            File.AppendAllLines(Path.Combine(Out,"free-pen.csv"),trace);
        }
    }
}
}


