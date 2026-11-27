using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

using NarutoVR.SealCombat.Enemies;
using NarutoVR.SealCombat.Items;
using NarutoVR.SealCombat.Rpg;
using Oculus.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NarutoVR.SealCombat.Editor
{
[InitializeOnLoad]
public static class Chapter02BasketAChecks
{
    const string Session = "Ch02.Basket.A.Play";
    const string Filename = Session + ".save";
    const string ScenePath = "Assets/Scene/Chapter02_SuspendedPath.unity";
    static readonly List<string> notes = new(), trail = new();
    static string mode;
    static bool running;
    static double endTime;
    static string Out => Environment.GetEnvironmentVariable("CH02_BASKET_QA_OUT");
    static Chapter02BasketAChecks() => EditorApplication.update += Update;
    public static void Ride() => Begin("ride");
    public static void Interrupted() => Begin("interrupted");
    public static void Cold() => Begin("cold");

    static void Begin(string run)
    {
        if (!Application.isBatchMode || string.IsNullOrWhiteSpace(Out))
            throw new InvalidOperationException("Use batch mode and CH02_BASKET_QA_OUT");
        Directory.CreateDirectory(Out);
        EditorSceneManager.OpenScene(ScenePath);
        var player = One<LinearRpgController>();
        player.autoLoad = true;
        player.saveFileName = "ch02-basket-test-" + Guid.NewGuid().ToString("N") + ".json";
        Directory.CreateDirectory(Application.persistentDataPath);
        var path = Path.Combine(Application.persistentDataPath, player.saveFileName);
        File.Copy(Path.Combine(Out, run == "cold" ? "a-middle.json" : "postrepair.json"), path);
        SessionState.SetString(Filename, path);
        SessionState.SetString(Session, run);
        EditorApplication.EnterPlaymode();
    }
    static async void Update()
    {
        if (running && EditorApplication.timeSinceStartup > endTime) { Log("FAIL watchdog"); End(1); return; }
        if (!Application.isPlaying || running) return;
        mode = SessionState.GetString(Session, "");
        if (mode == "") return;
        SessionState.EraseString(Session);
        running = true;
        endTime = EditorApplication.timeSinceStartup + 210;
        try
        {
            if (mode == "cold") await ColdCheck();
            else await Test();
            Log("PASS " + mode);
            End(0);
        }
        catch (Exception ex) { Log("FAIL " + ex); End(1); }
    }
    static void End(int code)
    {
        running = false;
        File.WriteAllLines(Path.Combine(Out, "a-" + mode + ".txt"), notes);
        File.WriteAllLines(Path.Combine(Out, "a-" + mode + "-route.csv"), trail);
        var path = SessionState.GetString(Filename, "");
        if (Path.GetFileName(path).StartsWith("ch02-basket-test-") && File.Exists(path)) File.Delete(path);
        SessionState.EraseString(Filename);
        EditorApplication.Exit(code);
    }
    static Transform[] All() => SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToArray();
    static T One<T>() where T : Component => All().Select(t => t.GetComponent<T>()).Single(c => c);
    static string PathOf(Transform t) => t.parent ? PathOf(t.parent) + "/" + t.name : t.name;
    static string P(Vector3 p) => p.ToString("F4");
    static string Stage(LinearRpgController p) => p.StageAt(p.StageIndex).stageId;
    static void Log(string s) { notes.Add(s); Debug.Log("CH02_BASKET_A " + s); }
    static void Check(bool result, string s) { if (!result) throw new InvalidOperationException(s); Log(s); }
    static async Task Fixed(int n = 1) { for (int i = 0; i < n; i++) await Awaitable.FixedUpdateAsync(); }
    static async Task Until(Func<bool> test, string label, double limit = 35)
    {
        var timeout = EditorApplication.timeSinceStartup + limit;
        while (!test() && EditorApplication.timeSinceStartup < timeout) await Task.Delay(12);
        Check(test(), label);
    }
    static void MoveRig(RpgLocomotionController loc, Vector3 floor)
    {
        loc.rigRoot.position += floor - loc.PlayerFloorPosition; Physics.SyncTransforms();
    }
    static async Task Test()
    {
        var rpg=One<LinearRpgController>();var loc=rpg.locomotion;var post=One<StoneCarrierPost>();
        var manager=One<EnemyManager>();var basket=post.basket;var slot=basket.slot;
        var head=loc.head;var glyph=AssetDatabase.LoadAssetAtPath<QianjunEffectDefinition>("Assets/Art/Glyphs/QianjunTether.asset");
        var room=All().SelectMany(t=>t.GetComponents<CombatRoomEncounter>()).Single(r=>r.spawnPoints.Any(s=>s.point==post.transform && s.enemyId=="stone-carrier"));
        var motion=One<Chapter02BasketMotion>();
        bool interrupted = mode == "interrupted";
        bool leftBeforeDock = false;
        bool pauseNegotiation = false;
        Vector3 outsideFloor=default, outsideRig=default;
        Quaternion dockRigRotation=default, dockHeadRotation=default;
        Vector3 dockOffset=default, dockHeadOffset=default;
        int slotId=slot.GetInstanceID(),basketId=basket.GetInstanceID(),volumeId=basket.passengerVolume.GetInstanceID();
        Vector3 anchor=slot.position;
        int count=0;basket.onDocked.AddListener(()=>{count++;
            dockRigRotation=loc.rigRoot.rotation; dockHeadRotation=head.rotation;
            dockOffset=loc.rigRoot.position-slot.position; dockHeadOffset=head.position-slot.position; Log("onDocked count="+count+" stone="+basket.Stone.GetInstanceID()+" at="+P(basket.Stone.transform.position));});
        var old=All().SingleOrDefault(t=>t.name=="背石工奴 · 机体占位");
        Check(!old || !old.gameObject.activeInHierarchy,"现场旧工奴根及其子外观均未启用");
        Check(room.rpg==rpg && room.enemyManager==manager && room.isActiveAndEnabled && loc.rigRoot && head && rpg.GetComponent<RpgDamageReceiver>(),
            "已安装的祭坛房间使用现场 RPG、头部/移动根和 EnemyManager");
        await Until(()=>rpg.CanAct,"现场 RPG 初始化完毕，可进入真实房间，存档隔离："+rpg.saveFileName);
        Check(rpg.StageAt(rpg.StageIndex).stageId=="CH02_REACH_HUB" && !One<Chapter02BridgeEquipment>().brokenBarrier.activeSelf, "Cold post-repair checkpoint restored into altar with bridge repaired");
        await Until(()=>One<ToolBeltInteraction>().Initialised,"实笔工具带完成首次 LateUpdate 挂回");
        var equipment=Equipment(rpg);
        var curtain=motion.comfortCurtain.GetComponent<CanvasGroup>();
        var camera=head.GetComponent<Camera>();
        Check(curtain && curtain.alpha<.001f && !curtain.blocksRaycasts && !curtain.interactable &&
            motion.comfortCurtain.GetComponentsInChildren<UnityEngine.UI.GraphicRaycaster>(true).Length==0 &&
            motion.comfortCurtain.GetComponentsInChildren<UnityEngine.UI.Graphic>(true).All(g=>!g.raycastTarget) &&
            camera && curtain.transform.IsChildOf(head),
            "上层定目帘初始透明、隶属头部相机，无 GraphicRaycaster 或接收射线的图形；相机="+PathOf(head));
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
        if(interrupted)
        {
            // Freeze only the last capture check, keeping glyph ownership, forces and the rigidbody live.
            // During the first actual radius crossing the player steps off before releasing that check.
            basket.enabled=false;
            pauseNegotiation=true;
        }
        trail.Add("sweep,step,stoneID,rigidbodyID,stoneX,stoneY,stoneZ,toSlot_m");
        // Pull from the recorded upper lip. Once inertia carries the stone over the front edge,
        // move the representative tracking point to an actual side deck clear of the ballast opening.
        // During the ride and walk the rig is only translated by production code.
        bool parked=false;
        for(int sweep=0;sweep<12 && count==0;sweep++)
        {
            bool lift=sweep==2 || sweep>=4;
            var stance = lift ? front.position+toward*.75f : floorMarker.position+toward*.65f;
            // A retreat facing away can drop the original stone at the far edge of the authored landing.
            // Aim from the front inside edge of the passenger volume for the initial long pull.
            if (Vector3.ProjectOnPlane(stone.transform.position - slot.position,Vector3.up).magnitude > glyph.reach + .6f)
                stance = front.position + toward * .15f;
            if(!parked) MoveRig(loc,stance);
            Physics.SyncTransforms();
            if(!basket.passengerVolume.bounds.Contains(head.position))
                throw new Exception("QA stance is outside the original basket: "+P(head.position));
            // 只合成笔尖、手与身体输入，所有目标锁定、力、碰撞、入槽和事件使用生产对象。
            var body=new Vector3(head.position.x,loc.PlayerFloorPosition.y+1.6f,head.position.z);
            var grip=body-toward*(lift?.22f:.38f)-Vector3.up*(lift?.02f:.58f);
            var tip=lift ? grip-toward*.34f-Vector3.up*.34f : body-toward*.9f-Vector3.up*.55f;
            var ctx=new GlyphEffectContext{owner=head,tracked=true,bodyTracked=true,hand=grip,tip=tip,body=body,
                forward=(stone.transform.position-tip).normalized,observed=Time.fixedTimeAsDouble};
            var hull = stone.GetComponent<BoxCollider>().bounds;
            var side = Vector3.Cross(toward, Vector3.up).normalized;
            bool visible = false;
            // Probe the surface a player can actually point to. A corpse can occlude the center ray.
            foreach (var dx in new[]{0f, .38f, -.38f})
            {
                foreach (var dy in new[]{0f, .40f, -.28f})
                {
                    var aim = hull.center + side*dx + Vector3.up*dy + toward*.42f;
                    ctx.forward = (aim-ctx.tip).normalized;
                    var ray = Physics.RaycastAll(ctx.tip, ctx.forward, glyph.reach, glyph.aimMask, QueryTriggerInteraction.Ignore)
                        .Where(h=>!h.collider.transform.IsChildOf(head)).OrderBy(h=>h.distance).ToArray();
                    if (ray.Length>0 && ray[0].rigidbody == rb) { visible=true; break; }
                    if(dx==0f && dy==0f)
                        Log("Center ray: "+(ray.Length==0?"no hit":PathOf(ray[0].collider.transform)+" distance="+ray[0].distance));
                }
                if(visible)break;
            }
            if(!visible){Log("No visible hull from occupied slot; letting the death animation finish");await Fixed(20);continue;}
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
                    if(!interrupted && !parked && count==0 && sweep>=2 &&
                        Vector3.Distance(p,slot.position)<1.42f)
                    {
                        var safeDeck=basket.passengerVolume.transform.TransformPoint(new Vector3(1.94f,0f,.84f));
                        MoveRig(loc,safeDeck);
                        parked=true;
                        Check(motion.PlayerInA,"收力后站在 A 篮右侧乘员板，离开镇石槽 "+P(safeDeck));
                    }
                    if(pauseNegotiation && Vector3.Distance(p,slot.position)<=basket.captureRadius)
                    {
                        outsideFloor=front.position-toward*4f;
                        MoveRig(loc,outsideFloor);
                        outsideRig=loc.rigRoot.position;
                        Check(!motion.PlayerInA,"出篮对照：脚/头已移出原乘员盒");
                        leftBeforeDock=true;
                        pauseNegotiation=false;
                        basket.enabled=true;
                        await Fixed();
                    }
                    if(i%3==0 || count>0)trail.Add(FormattableString.Invariant($"{sweep+1},{i},{sid},{bid},{p.x:F6},{p.y:F6},{p.z:F6},{Vector3.Distance(p,slot.position):F6}"));
                }
            }
            finally{action.End();}
        }
        await Fixed(2);
        Check(count==1 && stone.MovedByGlyph && stone.State==BallastStone.Phase.Docked && basket.Stone==stone &&
            stone.GetInstanceID()==sid && rb.GetInstanceID()==bid && stone.transform.parent==slot && rb.isKinematic &&
            meshIds.SequenceEqual(filters.Select(f=>f.sharedMesh.GetInstanceID())), "同一 A4 实例、刚体和渲染网格锁定，onDocked 恰好一次");


        Check(slot.GetInstanceID()==slotId && basket.GetInstanceID()==basketId && basket.passengerVolume.GetInstanceID()==volumeId,
            "原篮、乘员盒、锁槽保持同一实例");
        if (interrupted)
        {
            await Fixed(16);
            Check(leftBeforeDock && !motion.IsMoving && !motion.PlayerInA && count==1 &&
                  Vector3.Distance(slot.position,anchor)<.01f && Vector3.Distance(loc.rigRoot.position,outsideRig)<.01f,
                "玩家移出后镇石已入槽，篮与玩家保持静止");
            MoveRig(loc,floorMarker.position+toward*.65f);
            await Until(()=>motion.IsMoving, "携原镇石回篮后开动");
            await Until(()=>slot.position.y<anchor.y-3 && !motion.AtMiddle,"A 行程中物理高度已下降");
            Check(rpg.SaveCheckpoint() && SavedNode(rpg)=="CH02_ENTRANCE", "A 途中手动保存落在上层安全检查点");
            Check(rpg.RestoreCheckpoint(),"A 途中按真实 RPG 入口读档");
            await Fixed(4);
            Check(!motion.IsMoving && !motion.AtMiddle && !motion.PlayerInA && Vector3.Distance(slot.position,anchor)<.015f &&
                Vector3.Distance(loc.PlayerFloorPosition,rpg.CheckpointAt(rpg.StageIndex).floorPosition)<.08f &&
                Stage(rpg)=="CH02_REACH_HUB" && !basket.Stone,
                "恢复上层起点，原篮回摆，石与战区重置，可重试；floor="+P(loc.PlayerFloorPosition));
            return;
        }
        float drift=0, headDrift=0, rigTurn=0, headTurn=0;
        int samples=0;
        bool sawMoving=false, sawOpaque=false;
        Vector3 midForward=Vector3.zero;
        while (!motion.AtMiddle)
        {
            sawMoving |= motion.IsMoving;
            if(slot.position.y<anchor.y-28f && slot.position.y>anchor.y-52f && curtain.gameObject.activeInHierarchy &&
                curtain.alpha>.9f && motion.comfortCurtain.GetComponentsInChildren<UnityEngine.UI.GraphicRaycaster>(true).Length==0)
            {
                sawOpaque=true; midForward=head.forward;
            }
            drift=Mathf.Max(drift,Vector3.Distance(loc.rigRoot.position-slot.position,dockOffset));
            headDrift=Mathf.Max(headDrift,Vector3.Distance(head.position-slot.position,dockHeadOffset));
            rigTurn=Mathf.Max(rigTurn,Quaternion.Angle(dockRigRotation,loc.rigRoot.rotation));
            headTurn=Mathf.Max(headTurn,Quaternion.Angle(dockHeadRotation,head.rotation));
            if (samples++ % 20 == 0) trail.Add("ride,"+samples+","+P(slot.position)+","+P(loc.PlayerFloorPosition));
            await Awaitable.NextFrameAsync();
        }
        await Fixed(3);
        Check(sawOpaque && curtain.alpha<.001f && !curtain.blocksRaycasts,
            "中程定目帘 activeInHierarchy 且 alpha>0.9、无 GraphicRaycaster，落稳后 alpha=0；采样头部朝向="+P(midForward));
        Check(sawMoving && samples>=3 && drift<.015f && headDrift<.015f && rigTurn<.12f && headTurn<.12f,
            $"A 实际运输 {samples} 帧，rig/head相对篮漂移 {drift:F5}/{headDrift:F5} m，旋转 {rigTurn:F3}/{headTurn:F3}°，始末头部方向 {P(dockHeadRotation*Vector3.forward)}/{P(head.forward)}");
        Check(Mathf.Abs(slot.position.y-(anchor.y-80f))<.04f && count==1 && basket.Stone==stone && stone.transform.parent==slot &&
              stone.State==BallastStone.Phase.Docked && meshIds.SequenceEqual(filters.Select(f=>f.sharedMesh.GetInstanceID())),
              "A 下降约 80m，仅一次入槽，原镇石与网格抵中层");
        await Until(()=>!rpg.CheckpointSavePending && SavedNode(rpg)=="CH02_A_MIDDLE","静态西口检查点已自动写入玩家私有磁盘档");
        Check(Stage(rpg)=="CH02_REACH_HUB","抵西口时仍等待玩家步行至④");
        File.Copy(rpg.SavePath,Path.Combine(Out,"a-middle.json"),true);
        await WalkToWorkshop(rpg,motion);
        Check(count==1 && motion.AtMiddle && !motion.IsMoving && stone && stone.transform.parent==slot,"出篮至工坊全程 A 静止，同一石留在原槽");
        Check(equipment.SequenceEqual(Equipment(rpg)),"抵工坊后官方手、实笔和口粮接线对象实例连续");
    }

    static string SavedNode(LinearRpgController p)
    {
        if (string.IsNullOrEmpty(p.SavePath) || !File.Exists(p.SavePath)) return "";
        return JsonUtility.FromJson<SaveEnvelope>(File.ReadAllText(p.SavePath))?.progress?.checkpointId;
    }

    static int[] Equipment(LinearRpgController p)
    {
        var xr=p.handTracking.xrHandsSource;
        var visuals=All().Select(t=>t.GetComponent<HandVisual>()).Where(v=>v).ToArray();
        var ids=new List<int>();
        Check(xr && xr.IsConfigured && visuals.Length==2,"双手来源已配置，场景恰有两个 HandVisual");
        foreach (var left in new[]{true,false})
        {
            var source=left ? xr.leftHand : xr.rightHand;
            var visual=visuals.Single(v=>ReferenceEquals(v.Hand,source));
            var mesh=(SkinnedMeshRenderer)new SerializedObject(visual).FindProperty("_openXRSkinnedMeshRenderer").objectReferenceValue;
            var side=left?"Left":"Right";
            Check(mesh && visual.isActiveAndEnabled && mesh.sharedMesh &&
                AssetDatabase.GetAssetPath(mesh.sharedMesh)=="Packages/com.meta.xr.sdk.interaction/Runtime/Meshes/OpenXR"+side+"Hand.fbx",
                side+" 官方可见手和对应输入引用");
            ids.Add(visual.GetInstanceID());
        }
        var bridge=One<Chapter02BridgeEquipment>();
        var belt=One<ToolBeltInteraction>();
        var ration=One<TravelRationInteraction>();
        var supply=One<ChapterRationSupply>();
        var pen=belt.items.Single(i=>i && i.Kind==GameItemKind.GlyphPen);
        Check(bridge.player==p && bridge.equipment && bridge.glyph && bridge.equipment.interaction==belt,
              "断桥画符与原玩家、唯一工具带的交互器接线");
        Check(belt.Initialised && pen && pen.isActiveAndEnabled && bridge.equipment.pen==pen,
              "实笔为启用的原章节物品，工具带已完成初始化");
        Check(pen.Dock && pen.Dock==pen.home && pen.Dock.SnappedObject==pen.gameObject && belt.slots.Contains(pen.Dock),
              "实笔确在工具带归位槽，槽与物品双向引用一致");
        Check(ration.sharedBelt==belt && ration.player==p && ration.mouth && ration.mouth.transform.parent==p.locomotion.head,
              "干粮使用原工具带、同一玩家和正式挂在头部的嘴区");
        Check(!supply.HasPouch,"剧情或配置尚未发放口粮，未生成初始口粮包");
        ids.AddRange(new[]{pen.GetInstanceID(),belt.GetInstanceID(),ration.GetInstanceID(),supply.GetInstanceID()});
        return ids.ToArray();
    }

    static async Task ColdCheck()
    {
        var p=One<LinearRpgController>();
        var m=One<Chapter02BasketMotion>();
        await Until(()=>p.CanAct && m.AtMiddle,"西口落稳私有档冷启动进入中层");
        await Fixed(4);
        Check(SavedNode(p)=="CH02_A_MIDDLE" && Stage(p)=="CH02_REACH_HUB" &&
            Vector3.Distance(p.locomotion.PlayerFloorPosition,m.middleCheckpoint.floorPosition)<.08f && !m.PlayerInA && !m.IsMoving,
            "玩家恢复在西口静态板上，尚未抵达中控，floor="+P(p.locomotion.PlayerFloorPosition));
        var restored=m.ballast.slot.GetComponentsInChildren<BallastStone>(true);
        Check(restored.Length==1 && m.ballast.Boarded && restored[0]==m.ballast.Stone && restored[0].State==BallastStone.Phase.Docked,
            "冷启中层后原槽恰有一块锁定镇石");
        var manager = One<EnemyManager>();
        var altar = All().SelectMany(t => t.GetComponents<CombatRoomEncounter>())
            .Single(r => r.spawnPoints.Any(row => row.enemyId == "stone-carrier"));
        bool stayedCold = true;
        for (int sample = 0; sample < 20; sample++)
        {
            await Fixed(5);
            var liveStones = UnityEngine.Object.FindObjectsByType<BallastStone>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            stayedCold &= !altar.HasStarted && !altar.IsLoadingEnemies &&
                !manager.ActiveEnemies.Values.Any(lease => lease.EnemyId == "stone-carrier" || lease.EnemyId == "canjiou" || lease.EnemyId == "zhihuocanji") &&
                liveStones.Length == 1 && liveStones[0] == m.ballast.Stone && liveStones[0].transform.parent == m.ballast.slot &&
                liveStones[0].State == BallastStone.Phase.Docked;
        }
        Check(stayedCold, "中层冷启连续100个物理刻，祭坛不预载、不出租守敌、不开战，全场仅有一件始终绑定A槽的有效镇石。");
        await Until(()=>One<ToolBeltInteraction>().Initialised,"读档后实笔工具带初始化");
        Equipment(p);
        var lesson=One<Chapter02BridgeEquipment>();
        var pen=lesson.equipment.pen;
        var belt=lesson.equipment.interaction;
        var holster=pen.Dock;
        await Until(()=>holster && !holster.IsSnapping,"中层读档完成实笔归槽");
        var grip=pen.GripPosition;
        var orientation=pen.transform.rotation;
        var head=p.locomotion.head;
        double clock=Time.realtimeSinceStartupAsDouble;
        void Hand(bool pinch)
        {
            clock=Math.Max(clock+.02,Time.realtimeSinceStartupAsDouble);
            var observed=new PlayerHandObservation {
                rightValid=true,rightObserved=clock,gripPoseValid=true,pinchValid=true,systemPinch=true,systemPressed=pinch,
                headPosition=head.position,headForward=head.forward,rightTip=grip,rightWrist=grip,gripPosition=grip,gripRotation=orientation
            };
            lesson.glyph.Observe(observed,clock);
            lesson.glyph.Advance(clock);
        }
        Hand(false); Hand(true);
        Check(pen.IsHeld && !pen.Dock && belt.OwnsHeldItem(pen) && lesson.glyph.IsHeld,
            "中层冷启后，右手在笔把处从松开变为捏合，原实笔离开腰槽并获得写符所有权");
        Hand(false);
        await Until(()=>pen.Dock==holster && !pen.IsHeld && !holster.IsSnapping && holster.SnappedObject==pen.gameObject,
            "松开后原实笔返回原腰槽");
        await WalkToWorkshop(p,m);
    }

    static async Task WalkToWorkshop(LinearRpgController player, Chapter02BasketMotion motion)
    {
        var loc=player.locomotion;
        var hub=All().Select(t=>t.GetComponent<ReachAreaChapterObjective>()).Single(o=>o && o.area && o.area.name.Contains("司衡台"));
        var workshop=One<WorkshopTenonObjective>();
        var middle=motion.middleCheckpoint.floorPosition;
        var sill=All().Single(t=>t.name=="A 中层篮口").position;
        var gate=All().Single(t=>t.name=="工坊院口").position;
        var walk=typeof(RpgLocomotionController).GetMethod("WalkStep",BindingFlags.NonPublic|BindingFlags.Instance);
        bool everInHub=false;
        float meters=0f;
        int steps=0;
        async Task Go(Vector3 target, string label)
        {
            var start=loc.PlayerFloorPosition;
            int slow=0;
            for(int i=0;i<1500;i++)
            {
                var from=loc.PlayerFloorPosition;
                var delta=Vector3.ProjectOnPlane(target-from,Vector3.up);
                if(delta.magnitude<.055f) break;
                // This is the same locomotion step used by hand-directed walking; floor, sweep and slide use production masks.
                bool stepped=(bool)walk.Invoke(loc,new object[]{Vector3.ClampMagnitude(delta,.075f),"QA 行走 "+label});
                if(!stepped) throw new InvalidOperationException("实际行走被阻 "+label+" floor="+P(from)+" target="+P(target)+" "+loc.Status);
                var to=loc.PlayerFloorPosition;
                if (Mathf.Abs(to.y-middle.y)>.35f) throw new InvalidOperationException("偏离中层 "+P(to));
                slow=Vector3.Distance(to,target)>=Vector3.Distance(from,target)-.001f ? slow+1 : 0;
                if(slow>=7) throw new InvalidOperationException("实际行走未向目标前进 "+label+" floor="+P(to)+" target="+P(target));
                everInHub |= ReachAreaChapterObjective.ContainsFloor(hub.area,to);
                meters+=Vector3.Distance(from,to); steps++;
                if(steps%12==0) { Physics.SyncTransforms(); await Awaitable.NextFrameAsync(); }
                if(Stage(player)!="CH02_REACH_HUB" && !everInHub) throw new InvalidOperationException("未实际抵达④却提前推进 "+P(to));
            }
            Physics.SyncTransforms();
            await Awaitable.NextFrameAsync();
            Check(Vector3.ProjectOnPlane(target-loc.PlayerFloorPosition,Vector3.up).magnitude<.07f,
                $"{label} {P(start)} → {P(loc.PlayerFloorPosition)}，累计 {meters:F2}m / {steps} 步");
            trail.Add("walk,"+label+","+P(loc.PlayerFloorPosition)+","+Stage(player));
        }
        // The basket has a central stone and a forward rim: walk around its east side before crossing the middle sill.
        if (motion.PlayerInA)
        {
            var floor=loc.PlayerFloorPosition;
            await Go(new Vector3(floor.x,floor.y,sill.z),"A 篮内对准侧向豁口");
            await Go(sill,"A 中层篮口");
        }
        await Go(middle,"西端静态地板");
        Check(!motion.PlayerInA && !motion.IsMoving && motion.AtMiddle && Stage(player)=="CH02_REACH_HUB","已步出 A，西边栈道尚未触发④");
        await Go(new Vector3(21.6f,middle.y,20.4f),"西栈桥折向");
        await Go(new Vector3(29.6f,middle.y,12.5f),"司衡台西接驳");
        // The actor only needs to enter the actual hub trigger; the center of the console is occupied by geometry.
        if(!everInHub)
            await Go(new Vector3(33.6f,middle.y,12.5f),"④ 司衡台中控区");
        await Until(()=>everInHub && Stage(player)=="CH02_WORKSHOP_TENON","步行抵达④原场景区域后才推进至工坊阶段");
        Check(SavedNode(player)=="CH02_HUB","抵达④后的持久化节点已切换至 CH02_HUB");
        await Go(new Vector3(33.6f,middle.y,12.5f),"工坊转向");
        await Go(new Vector3(30.6f,middle.y,24.5f),"工坊桥南端");
        await Go(gate,"③ 工坊院口");
        var inside=gate+Vector3.forward*.75f;
        await Go(inside,"进入工坊院坪");
        await Until(()=>workshop.courtyard.HasStarted || workshop.roof.HasStarted,"脚底入③，真实工坊战区已响应",4);
    }
}
}




