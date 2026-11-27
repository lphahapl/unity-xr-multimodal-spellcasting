// 仅在复制出来的正式二章场景 Play 内接线；退出批处理即销毁。不得保存场景。
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NarutoVR.SealCombat.Enemies;
using NarutoVR.SealCombat.Rpg;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class Chapter02CarrierTransit
{
    const string Key="Chapter02Carrier.Transit";
    static readonly List<string> notes=new();
    static readonly List<string> samples=new();
    static int checks;
    static string Dir=>Chapter02CarrierNatural.Report;
    static Transform Find(string label)=>Chapter02CarrierNatural.Find(label);
    static Chapter02CarrierTransit()=>EditorApplication.update+=StartPlay;
    public static void Run()
    {
        if(!Application.isBatchMode) throw new InvalidOperationException("仅限独立批处理");
        EditorSceneManager.OpenScene(Chapter02CarrierNatural.ScenePath);
        SessionState.SetBool(Key,true); EditorApplication.EnterPlaymode();
    }
    static async void StartPlay()
    {
        if(!Application.isPlaying || !SessionState.GetBool(Key,false))return;
        SessionState.EraseBool(Key);
        try{await Verify(); Log($"CH02_CARRIER_TRANSIT_PASS {checks}"); Finish(0);}
        catch(Exception e){Log("CH02_CARRIER_TRANSIT_FAIL "+e); Finish(1);}
    }
    static void Log(string line){notes.Add(line);Debug.Log(line);}
    static void Check(bool ok,string line){if(!ok)throw new InvalidOperationException(line);Log($"CHECK {++checks} {line}");}
    static void Finish(int exit)
    {
        File.WriteAllLines(Path.Combine(Dir,"transit.txt"),notes);
        File.WriteAllLines(Path.Combine(Dir,"trajectory.csv"),samples);
        EditorApplication.Exit(exit);
    }
    static Transform Node(string name,Transform parent,Vector3 world)
    {
        var n=new GameObject(name).transform;n.SetParent(parent,false);n.position=world;return n;
    }
    static async Task Step(int n=1){for(int i=0;i<n;i++)await Awaitable.FixedUpdateAsync();}
    static void Capture(string name,Vector3 center,Vector3 eye)
    {
        if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)return;
        var go=new GameObject("QA取景");var cam=go.AddComponent<Camera>();
        cam.transform.position=eye;cam.transform.LookAt(center);cam.nearClipPlane=.1f;cam.farClipPlane=450;
        cam.fieldOfView=47;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.2f,.26f,.33f);
        var old=RenderTexture.active;var rt=RenderTexture.GetTemporary(1000,640,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
        var img=new Texture2D(1000,640,TextureFormat.RGB24,false);
        try{cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;img.ReadPixels(new Rect(0,0,1000,640),0,0);img.Apply();
            File.WriteAllBytes(Path.Combine(Dir,name+".png"),img.EncodeToPNG());}
        finally{cam.targetTexture=null;RenderTexture.active=old;RenderTexture.ReleaseTemporary(rt);Object.Destroy(img);Object.Destroy(go);}
    }
    static float Flat(Vector3 a,Vector3 b)=>Vector3.ProjectOnPlane(a-b,Vector3.up).magnitude;
    static void Sample(string phase,float t,Transform player,Transform stone,Transform a,Transform b,Transform ta,Transform tb)
    {
        var values=new List<string>{phase,t.ToString("F3",CultureInfo.InvariantCulture)};
        foreach(var tr in new[]{player,stone,a,b,ta,tb})foreach(float v in new[]{tr.position.x,tr.position.y,tr.position.z})
            values.Add(v.ToString("F4",CultureInfo.InvariantCulture));
        samples.Add(string.Join(",",values));
    }
    static async Task Verify()
    {
        File.WriteAllLines(Path.Combine(Dir,"external-resolved.txt"),
            File.ReadAllLines(Path.Combine(Dir,"external-guids.txt")).Where(s=>!string.IsNullOrWhiteSpace(s))
                .Select(g=>g+"  "+AssetDatabase.GUIDToAssetPath(g)));
        samples.Add("phase,time,playerX,playerY,playerZ,stoneX,stoneY,stoneZ,Ax,Ay,Az,Bx,By,Bz,AendX,AendY,AendZ,BendX,BendY,BendZ");
        float dt=Time.fixedDeltaTime;
        var start=Find("背石起始位");var drop=Find("示例落石");var front=Find("A 上层篮口");
        var template=Find("背石工奴 · 机体占位");var visual=Find("背载镇石 · 含铜箍");
        var a=Find("A · 镇石配重吊篮");var b=Find("B · 谷底吊篮");
        var slotBase=Find("示例槽心");var middleA=Find("A 中层篮口");var middleB=Find("B 中层篮口");
        var beam=Find("双臂衡梁 · 120 m");var ta=Find("A 梁端吊点");var tb=Find("B 梁端吊点");
        Log($"scene={a.gameObject.scene.path} start={start.position:F3} dropMarker={drop.position:F3} Aupper={front.position:F3} slotFloor={slotBase.position:F3} A0={a.position:F3} B0={b.position:F3}");
        Log($"AsillMid={middleA.position:F3} BsillMid={middleB.position:F3} pivot={beam.position:F3} tipA={ta.position:F3} tipB={tb.position:F3}");
        var axis=Vector3.ProjectOnPlane(drop.position-start.position,Vector3.up).normalized;
        Log($"初始工奴位置到 A 口={Flat(start.position,front.position):F3}m，示例掉点到口={Flat(drop.position,front.position):F3}m，到槽={Flat(drop.position,slotBase.position):F3}m");
        // 现场 MeshCollider/BoxCollider 建导航，保留槽口、篮底、周边碰撞原貌。
        var originalVisualWorld=visual.position;var originalVisualLocal=visual.localPosition;
        string originalVisualPath=Chapter02CarrierNatural.Hierarchy(visual);
        visual.SetParent(null,true);template.gameObject.SetActive(false);
        var bounds=new Bounds((start.position+drop.position)*.5f,new Vector3(35,9,37));
        var sources=new List<NavMeshBuildSource>();
        NavMeshBuilder.CollectSources(bounds,~0,NavMeshCollectGeometry.PhysicsColliders,0,new List<NavMeshBuildMarkup>(),sources);
        var navData=NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByIndex(0),sources,bounds,Vector3.zero,Quaternion.identity);
        Check(navData && sources.Count>0,$"现场物理碰撞生成导航源 {sources.Count} 个");
        var surface=NavMesh.AddNavMeshData(navData);
        Check(surface.valid && NavMesh.SamplePosition(start.position,out var sampled,1.2f,NavMesh.AllAreas),"正式工奴出生点在现场导航上");
        var owner=Node("QA玩家头部代表点",null,start.position-axis*2+Vector3.up*1.58f);
        var service=new GameObject("QA正式 EnemyManager").AddComponent<EnemyManager>();
        service.catalog=AssetDatabase.LoadAssetAtPath<EnemyCatalog>("Assets/ScriptableObjects/敌人目录配置/本关怪物目录.asset");
        var lease=await service.SpawnEnemyAsync("stone-carrier",start.position,Quaternion.LookRotation(axis),owner);
        var worker=lease.Controller.GetComponent<StoneCarrier>();var stone=worker.Stone;
        Log($"灰盒原渲染节点原机体下 local={originalVisualLocal:F3} world={originalVisualWorld:F3}；"+
            $"本次租约正式 carryOffset={worker.profile.carryOffset:F3} 石节点 local={stone.transform.localPosition:F3} world={stone.transform.position:F3} owner={lease.Controller.transform.position:F3}。未增设骨骼/身体锚，动画贴合不在运输验收内。");
        // 同一原场景渲染节点持续绑在当次正式物理石上，原外观不复制，灰盒渲染器让位。
        foreach(var r in stone.GetComponentsInChildren<Renderer>())r.enabled=false;
        foreach(var c in visual.GetComponentsInChildren<Collider>())c.enabled=false;
        visual.SetParent(stone.transform,true);visual.SetLocalPositionAndRotation(Vector3.zero,Quaternion.identity);
        var extents=visual.GetComponentsInChildren<Renderer>().Where(r=>r.enabled).ToArray();
        Chapter02CarrierMeshExport.Export(stone.transform,visual,originalVisualWorld,originalVisualLocal,worker.profile.carryOffset,originalVisualPath);
        Log($"物理石 ID={stone.GetInstanceID()}，原场景石外观 ID={visual.GetInstanceID()}，原石可见渲染件={extents.Length}，包络={string.Join("/",extents.Select(r=>r.bounds.size.ToString("F2")))}");
        Check(extents.Length>0 && visual.IsChildOf(stone.transform),"原背载灰盒外观挂在当次正式镇石刚体上");
        var post=start.gameObject.AddComponent<StoneCarrierPost>();post.patrolPoints=new[]{drop,start};
        var basket=a.gameObject.AddComponent<BallastBasket>();post.basket=basket;
        var passenger=a.gameObject.AddComponent<BoxCollider>();passenger.isTrigger=true;passenger.size=new Vector3(3.2f,2.5f,3);passenger.center=new Vector3(1.6f,1.25f,0);
        Physics.SyncTransforms();
        Check(Physics.Raycast(slotBase.position+Vector3.up*.08f,Vector3.down,out var bed,.8f,~0,QueryTriggerInteraction.Ignore)
            && bed.collider.transform==slotBase.parent && bed.normal.y>.95f,"射线命中现场 A 槽内底原碰撞面");
        var halfHeight=stone.GetComponent<BoxCollider>().size.y*stone.transform.lossyScale.y*.5f;
        basket.passengerVolume=passenger;basket.slot=Node("QA原槽石心",slotBase,bed.point+Vector3.up*halfHeight);
        Log($"标记平面 Y={slotBase.position.y:F3}；槽底碰撞 Y={bed.point.y:F3}；球形承接门限 {basket.captureRadius:F2}m；正式物理石锁槽中心 Y={basket.slot.position.y:F3}。");
        Check(post.Bind(lease,owner),"正式出生点、现场 A 篮及原场景槽心绑定当前租约");
        Check(!stone.GetComponent<GlyphHeavyBody>().CanManipulate && !basket.Boarded,"背载符力关闭");
        Capture("01-carried",stone.transform.position,stone.transform.position-axis*3+Vector3.right*4+Vector3.up*2);
        Check(lease.Controller.Entity.ReceiveHit("CH02普通实击",1,0,attacker:owner) && worker.Retreating,"正式非致命受击进入撤离");
        for(int i=0;i<350 && Flat(lease.Controller.transform.position,drop.position)>.53f;i++)await Step();
        var death=lease.Controller.transform.position;
        Log($"实际撤离={Flat(death,start.position):F3}m，离示例掉点={Flat(death,drop.position):F3}m，导航当前={lease.Controller.Status}");
        Check(Flat(death,drop.position)<.6f,"从出生位沿实际导航撤到示例掉点边缘");
        Check(lease.Controller.Entity.ReceiveHit("CH02致命实击",lease.Controller.Entity.Combat.Health,0,attacker:owner) && lease.Defeated,"正式致死受击掉石");
        var id=stone.GetInstanceID();var vid=visual.GetInstanceID();
        Check(stone.State==BallastStone.Phase.Dropped && Flat(stone.transform.position,death)<.1f && visual.IsChildOf(stone.transform),"掉石位取击破位置，保留原网格与同一刚体");
        service.Recycle(lease);await Step(25);
        var from=stone.transform.position;
        Log($"原网格重绑后随正式物理石；实际击破机体={death:F3}，落地稳定石心={from:F3}");
        Check(!basket.Boarded && !stone.GetComponent<GlyphHeavyBody>().CanManipulate,"已清除唯一工奴但玩家未登篮时不可施力");
        var slot=basket.slot.position;
        var towardBasket=Vector3.ProjectOnPlane(slot-from,Vector3.up).normalized;
        owner.position=slotBase.position+towardBasket*.65f+Vector3.up*1.6f;await Step(3);
        Check(basket.Boarded && stone.GetComponent<GlyphHeavyBody>().CanManipulate,"代表点进入原 A 篮实体范围，正式千钧符可锁此石");
        var effect=AssetDatabase.LoadAssetAtPath<QianjunEffectDefinition>("Assets/Art/Glyphs/QianjunTether.asset");
        var tip=owner.position-towardBasket*.9f-Vector3.up*.55f;
        var grip=owner.position-towardBasket*.38f-Vector3.up*.58f;
        var firstDirection=(from-tip).normalized;
        Physics.SyncTransforms();
        var ray=Physics.RaycastAll(tip,firstDirection,effect.reach,effect.aimMask,QueryTriggerInteraction.Ignore).OrderBy(h=>h.distance).ToArray();
        Log($"笔尖={tip:F3} 石心={from:F3} 槽心={slot:F3} 初始石槽距={Vector3.Distance(from,slot):F3}m；射线="+string.Join("; ",ray.Take(5).Select(h=>$"{Chapter02CarrierNatural.Hierarchy(h.collider.transform)}@{h.distance:F3}")));
        int docked=0;
        Task lift=null;
        async Task StartLift()
        {
            // 留出四个固定步采集锁槽证据，随后由这一回调驱动测试横梁。
            await Step(4);
            await Lift(owner,stone.transform,a,b,beam,ta,tb,middleA,middleB,docked);
        }
        basket.onDocked.AddListener(()=> { if(++docked==1)lift=StartLift(); });
        bool loweredLip=false;
        bool compareFlush=Environment.GetEnvironmentVariable("CH02_CARRIER_RIM")=="flush";
        for(int sweep=0;sweep<5 && docked==0;sweep++)
        {
            if(sweep==2)
            {
                var body=stone.GetComponent<Rigidbody>();
                var hull=stone.GetComponent<BoxCollider>();
                foreach(var other in Physics.OverlapSphere(stone.transform.position,.95f,~0,QueryTriggerInteraction.Ignore))
                {
                    if(other==hull)continue;
                    if(Physics.ComputePenetration(hull,stone.transform.position+towardBasket*.035f,stone.transform.rotation,
                        other,other.transform.position,other.transform.rotation,out var normal,out var depth))
                        Log($"原位前移 3.5cm 的形状穿透 {Chapter02CarrierNatural.Hierarchy(other.transform)}#{other.GetInstanceID()} center={other.bounds.center:F3} top={other.bounds.max.y:F3} depth={depth:F3} n={normal:F2}（仅几何查询）");
                }
                Log("水平刚体前探："+string.Join("; ",body.SweepTestAll(towardBasket,.08f,QueryTriggerInteraction.Ignore)
                    .Select(h=>$"{h.collider.name} center={h.collider.bounds.center:F3} top={h.collider.bounds.max.y:F3} t={h.distance:F3}")));
                Capture("02a-at-lip",stone.transform.position,stone.transform.position+new Vector3(2.3f,1.4f,1.9f));
            }
            if(sweep==2 && compareFlush)
            {
                // 仅作逐因对照：若原入口的水平及抬笔均不通过，只调原槽近战区一根横边的碰撞上沿。
                var near=slotBase.parent.GetComponents<BoxCollider>()
                    .Where(c=>c.size.y>.1f)
                    .OrderByDescending(c=>Vector3.Dot(c.bounds.center-slotBase.position,-towardBasket)).First();
                float top=near.bounds.max.y;
                Check(top>front.position.y && top-front.position.y<.1f,"存在单条高于地板的入槽横边");
                var shift=near.transform.InverseTransformVector(Vector3.up*(front.position.y-.002f-top));
                near.center+=shift;Physics.SyncTransforms();await Step(2);
                loweredLip=true;
                Log($"TEST-ONLY 临时调低槽入口 BoxCollider #{near.GetInstanceID()} center={near.bounds.center:F3}，顶面 {top:F3}→{near.bounds.max.y:F3}。保留其他全部原篮碰撞与渲染，石头未重置。");
            }
            bool liftStroke=!compareFlush && (sweep==2 || sweep==4);
            if(liftStroke)
            {
                // 仍在篮内，手靠近原槽抬高，让作用在石前侧的冲量具有向上分量。
                owner.position=front.position+towardBasket*.75f+Vector3.up*1.6f;
                grip=owner.position-towardBasket*.22f-Vector3.up*.02f;
                tip=grip-towardBasket*.34f-Vector3.up*.34f;
            }
            else if(sweep>1)
            {
                owner.position=slotBase.position+towardBasket*.65f+Vector3.up*1.6f;
                tip=owner.position-towardBasket*.9f-Vector3.up*.55f;
                grip=owner.position-towardBasket*.38f-Vector3.up*.58f;
            }
            var ctx=new GlyphEffectContext{owner=owner,tracked=true,bodyTracked=true,hand=grip,tip=tip,
                forward=(stone.transform.position-tip).normalized,body=owner.position,observed=Time.fixedTimeAsDouble};
            var tether=effect.Begin(ctx);tether.Tick(ctx,dt);
            float strokeDuration=liftStroke ? .16f : .38f;
            if(liftStroke && Environment.GetEnvironmentVariable("CH02_CARRIER_RIM")=="strong")strokeDuration=.38f;
            Log($"施符 {sweep+1} 锁定={tether.IsTethered} 起点={stone.transform.position:F3} 笔尖={tip:F3} 握点={grip:F3}，后拉时间={strokeDuration:F2}s，原槽沿={!loweredLip}");
            if(!tether.IsTethered){tether.End();continue;}
            for(int i=0;i<105 && docked==0;i++)
            {
                if(i*dt<strokeDuration)ctx.hand-=ctx.forward*(1.5f*dt);
                ctx.observed+=dt;tether.Tick(ctx,dt);tether.FixedTick(dt);await Step();
                if(i%3==0)Sample("pull",sweep*105*dt+i*dt,owner,stone.transform,a,b,ta,tb);
            }
            tether.End();
        }
        Log("运输入槽采用原场景全部几何碰撞="+!loweredLip);
        Log($"最终牵引位置={stone.transform.position:F3} 剩余槽距={Vector3.Distance(stone.transform.position,slot):F3} 接触邻体="+
            string.Join(";",Physics.OverlapSphere(stone.transform.position,.85f,~0,QueryTriggerInteraction.Ignore).Select(c=>Chapter02CarrierNatural.Hierarchy(c.transform))));
        Check(stone.MovedByGlyph && docked==1 && stone.State==BallastStone.Phase.Docked && stone.transform.parent==basket.slot,"原镇石经真实千钧符刚体运动入原 A 槽，onDocked 一次");
        Check(stone.GetInstanceID()==id && visual.GetInstanceID()==vid && visual.IsChildOf(stone.transform),"落地、施符、入槽沿用同一石及现场外观");
        Capture("02-docked",slot,slot+Vector3.right*5+Vector3.up*3-towardBasket*4);
        Check(lift!=null,"已由正式 onDocked 回调启动测试升降任务");
        await lift;
        surface.Remove();Object.Destroy(navData);
    }
    static async Task Lift(Transform player,Transform stone,Transform a,Transform b,Transform beam,Transform ta,Transform tb,
        Transform middleA,Transform middleB,int docks)
    {
        Check(docks==1,"测试升降从原槽 onDocked 触发后开始");
        var qa=ta.rotation;var qb=tb.rotation;var q0=beam.rotation;
        var a0=a.position;var b0=b.position;var ar=a.rotation;var br=b.rotation;
        var axis=Vector3.Cross(ta.position-beam.position,Vector3.down).normalized;
        float angle=2*Mathf.Asin((ta.position.y-beam.position.y)/Vector3.Distance(ta.position,beam.position))*Mathf.Rad2Deg;
        var p0=a.InverseTransformPoint(player.position); var s0=a.InverseTransformPoint(stone.position);
        player.SetParent(a,true);
        float maxTilt=0,maxLocal=0,maxSet=0,maxY=0;
        var ba=Find("A 篮顶吊耳");var bb=Find("B 篮顶吊耳");
        int ticks=Mathf.RoundToInt(8/Time.fixedDeltaTime);
        for(int i=1;i<=ticks;i++)
        {
            float u=Mathf.Clamp01((float)i/ticks);float t=Mathf.SmoothStep(0,1,u);
            beam.rotation=Quaternion.AngleAxis(angle*t,axis)*q0;ta.rotation=qa;tb.rotation=qb;
            await Step();
            maxTilt=Mathf.Max(maxTilt,Quaternion.Angle(a.rotation,ar),Quaternion.Angle(b.rotation,br));
            maxLocal=Mathf.Max(maxLocal,Vector3.Distance(a.InverseTransformPoint(player.position),p0),Vector3.Distance(a.InverseTransformPoint(stone.position),s0));
            maxSet=Mathf.Max(maxSet,Flat(ta.position,ba.position),Flat(tb.position,bb.position));
            maxY=Mathf.Max(maxY,Mathf.Abs((a.position.y+b.position.y)-(a0.y+b0.y)));
            if(i%5==0 || i==ticks)Sample("lift",i*Time.fixedDeltaTime,player,stone,a,b,ta,tb);
        }
        var al=a.InverseTransformPoint(middleA.position);var bl=b.InverseTransformPoint(middleB.position);
        Log($"TEST-ONLY V2.4 梁摆 {angle:F3}°，{ticks}×{Time.fixedDeltaTime:F3}s；A={a.position:F3} B={b.position:F3} 乘员={player.position:F3} 石={stone.position:F3}；中层口在篮内坐标 A={al:F3} B={bl:F3}；最大篮姿偏转={maxTilt:F3}°，玩家/石局部漂移={maxLocal:F5}m，吊索水平偏差={maxSet:F5}m，对向高度和偏差={maxY:F5}m");
        Check(Mathf.Abs(a.position.y)<.1f&&Mathf.Abs(b.position.y)<.1f,"8 秒对向升降，两篮从 ±80 m 同步到达 Y=0");
        Check(maxTilt<.1f && maxSet<.06f && maxY<.05f && maxLocal<.01f,"原篮底水平、吊索铅直，玩家和锁槽原石全程随行");
        bool Edge(Vector3 p)=> Mathf.Abs(p.y)<.1f && ((Mathf.Abs(p.x)<.08f && Mathf.Abs(p.z)<1.55f) ||
            (Mathf.Abs(p.x-3.2f)<.08f && Mathf.Abs(p.z)<1.55f) || (Mathf.Abs(Mathf.Abs(p.z)-1.5f)<.08f && p.x>=-.08f && p.x<=3.28f));
        Check(Edge(al)&&Edge(bl),"两篮的真实中层门口标记落在篮底可接驳边缘");
        Capture("03-middle",a.position+Vector3.up*1.0f,a.position+new Vector3(6,4,-5));
    }
}
