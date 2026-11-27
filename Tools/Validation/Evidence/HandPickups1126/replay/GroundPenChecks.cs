using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NarutoVR.SealCombat.Items;
using NarutoVR.SealCombat.Rpg;
using Unity.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;
namespace NarutoVR.SealCombat.Editor
{
    sealed class PenContactProbe : MonoBehaviour
    {
        public Collider support;
        public ContactPoint contact;
        void OnCollisionEnter(Collision hit) => Record(hit);
        void OnCollisionStay(Collision hit) => Record(hit);
        void Record(Collision hit) { for(int i=0;i<hit.contactCount;i++) { var p=hit.GetContact(i); if(p.normal.y>.75f){contact=p;support=hit.collider;return;} } }
    }

    [InitializeOnLoad]
    public static class GroundPenChecks
    {
        const string Session="IsolatedPenFloorChecks", Report="Logs/GroundPenChecks.txt";
        const float DefaultReturnRadius = .60f;
        const string SaveScope = "NarutoVR_ArmReachPenQA";
        static IEnumerator run;
        static double end,clock;
        static int frame;
        static LinearRpgController player;
        static Chapter02BridgeEquipment chapter;
        static GameItemBase item;
        static GlyphPenController glyph;
        static Vector3 head,heading;
        static string F(Vector3 v)=>v.ToString("F4");
        static void Log(string msg)=>File.AppendAllText(Report,msg+"\n");
        static void Assert(bool p,string s){if(!p)throw new InvalidOperationException(s); Log("PASS "+s);}
        static GroundPenChecks()=>EditorApplication.playModeStateChanged+=state=>{
            if(state!=PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Session,false))return;
            PenPrivateNoXR.Assert("GroundPen.Play");end=EditorApplication.timeSinceStartup+95;
            run=Execute(); EditorApplication.update+=Tick;
        };
        public static void Run()
        {
            PenPrivateNoXR.Assert("GroundPen.Edit");
            RequirePrivateSaveScope();
            EditorSceneManager.OpenScene("Assets/Scene/Chapter02_SuspendedPath.unity");
            var p=Object.FindFirstObjectByType<LinearRpgController>();
            p.autoLoad=false; p.saveFileName="pen-floor-qa-"+Guid.NewGuid().ToString("N")+".json";
            File.WriteAllText(Report,DateTimeOffset.Now.ToString("o")+" Unity "+Application.unityVersion+"\n");
            SessionState.SetBool(Session,true);
            EditorApplication.EnterPlaymode();
        }
        static void RequirePrivateSaveScope()
        {
            var actual = new DirectoryInfo(Application.persistentDataPath);
            if (Application.productName != SaveScope || actual.Name != SaveScope)
                throw new InvalidOperationException($"QA requires a separate product save directory; actual={actual.FullName}");
        }
        static void Tick()
        {
            if (frame==Time.frameCount) return;
            frame=Time.frameCount;
            int code=0;
            try
            {
                if(EditorApplication.timeSinceStartup>end)throw new TimeoutException(player ? player.Status : "player");
                if(run.MoveNext())return;
                PenPrivateNoXR.Assert("GroundPen.Finished");Log("PASS END");
            }
            catch(Exception e){Log("FAIL "+e);code=1;}
            EditorApplication.update-=Tick;SessionState.EraseBool(Session);EditorApplication.Exit(code);
        }
        static IEnumerator Execute()
        {
            RequirePrivateSaveScope();
            player=Object.FindFirstObjectByType<LinearRpgController>();
            while(!player.CanAct){ if (!player.enabled) throw new InvalidOperationException(player.Status); yield return null;}
            Assert(!player.autoLoad && string.Equals(Path.GetDirectoryName(Path.GetFullPath(player.SavePath)),
                Path.GetFullPath(Application.persistentDataPath),StringComparison.OrdinalIgnoreCase) &&
                Path.GetFileName(player.SavePath).StartsWith("pen-floor-qa-", StringComparison.Ordinal),
                "fresh isolated QA save path " + player.SavePath);
            chapter=player.GetComponent<Chapter02BridgeEquipment>();
            var belt=chapter.equipment.interaction;
            while(!belt.Initialised)yield return null;
            Assert(!chapter.equipment.pen.isActiveAndEnabled,"locked before stone-table objective");
            var area=(ReachAreaChapterObjective)((ObjectiveChapterFlow)player.chapter).stages[0].objectives[0];
            Assert(player.locomotion.TryRestoreCheckpoint(area.GetComponent<NavigationNode>()),"enter authored approach");
            item=chapter.equipment.pen;glyph=chapter.glyph;
            while(!chapter.drawingObjective.IsRunning || !item.Dock || item.Dock.IsSnapping)yield return null;
            chapter.OpenPanel();
            var physical=player.locomotion.head.position;
            heading=Vector3.ProjectOnPlane(player.locomotion.head.forward,Vector3.up).normalized;
            var floor=player.locomotion.PlayerFloorPosition;
            var yaw=Quaternion.LookRotation(heading);
            head=physical;
            var originalSlot=item.Dock as ToolBeltSlot;
            Assert(originalSlot && originalSlot==item.home && originalSlot.SnappedObject==item.gameObject &&
                item.LastDock==originalSlot && glyph.penRoot==item.transform && glyph.nib==item.insertionTip,
                "story unlock settled the authored physical pen in its own holster");
            TakeSeatedPen(originalSlot);
            ReleaseBesideBody(originalSlot, floor, yaw, .34f, "straight from holster");
            while(originalSlot.IsSnapping)yield return null;
            TakeSeatedPen(originalSlot);
            var ground=FindFloor(physical,floor,yaw);
            var drop=ground.point+Vector3.up*.44f;
            var body=item.GetComponent<Rigidbody>(); var box=item.GetComponent<BoxCollider>();
            var probe=item.gameObject.AddComponent<PenContactProbe>();
            Log($"floor={ground.collider.name} y={ground.point.y:F4} rigFloor={F(floor)} authoredHead={F(physical)} ground={F(ground.point)} reach={belt.reach:F3} home={F(originalSlot.opening.position)} penId={item.GetInstanceID()}");
            bool held=true;int passed=0;
            for (int mode=0;mode<5;mode++)
            {
                if(held)
                {
                    head=physical;
                    Step(drop,heading,true,yaw);
                    var releaseRadius = BodyRadius(item.GripPosition);
                    Assert(releaseRadius > DefaultReturnRadius + .08f, $"intentional far release at {releaseRadius:F3}m horizontal from head");
                    probe.support=null;
                    Step(drop,heading,false,yaw);
                    Assert(!glyph.IsHeld && !belt.Held && !player.Input.InteractionOwnsHand && !item.Dock && !body.isKinematic && body.useGravity,"release gives gravity and clears both owners");
                    var releasedAt=Time.fixedTime;int low=0;float lastFixed=-1;
                    while(low<6)
                    {
                        if(Time.fixedTime!=lastFixed)
                        {
                            lastFixed=Time.fixedTime;
                            if(probe.support && Time.fixedTime-releasedAt>.35f && body.linearVelocity.magnitude<.035f && body.angularVelocity.magnitude<.25f)low++;else low=0;
                        }
                        if(Time.fixedTime-releasedAt>5f)throw new InvalidOperationException($"pen did not settle: body={F(body.position)} v={F(body.linearVelocity)} omega={F(body.angularVelocity)} support={probe.support}");
                        yield return null;
                    }
                }
                Physics.SyncTransforms();
                Vector3 n=probe.contact.normal,p=probe.contact.point;
                float Height(Vector3 x)=>Vector3.Dot(x-p,n);
                var min=float.PositiveInfinity;var max=float.NegativeInfinity;int verts=0;
                foreach(var filter in item.GetComponentsInChildren<MeshFilter>(false))
                {
                    var renderer=filter.GetComponent<Renderer>();
                    if (!renderer || !renderer.enabled || !filter.sharedMesh)continue;
                    using var data=UnityEditor.MeshUtility.AcquireReadOnlyMeshData(filter.sharedMesh);
                    using var points=new NativeArray<Vector3>(data[0].vertexCount,Allocator.Temp);
                    data[0].GetVertices(points);
                    foreach(var point in points)
                    {float h=Height(filter.transform.TransformPoint(point));min=Mathf.Min(min,h);max=Mathf.Max(max,h);verts++;}
                }
                var center=box.transform.TransformPoint(box.center);
                var axis=box.transform.TransformVector(Vector3.forward * box.size.z);
                var distantEnd=Vector3.Distance(center+axis*.46f,item.GripPosition)>Vector3.Distance(center-axis*.46f,item.GripPosition) ? axis : -axis;
                var shaftAt=mode==2 ? center+distantEnd*.45f : mode==3 ? center+distantEnd*.255f : center;
                var touchSurface=box.ClosestPoint(shaftAt+n*.3f);
                var touch=touchSurface+n*(mode==3 ? .072f : .007f);
                var lowPoint=box.ClosestPoint(center-n*.3f);
                var highPoint=box.ClosestPoint(center+n*.3f);
                var hand=mode==0 ? ground.point+yaw*new Vector3(-.28f,1.05f,.31f) :
                    mode==1 ? ground.point+yaw*new Vector3(.16f,.42f,.24f) : touch;
                var expected=mode>=2 ? touchSurface : item.GripPosition;
                var aim=mode>=2 ? Vector3.up : (expected-hand).normalized;
                head=mode==0 ? floor+Vector3.up*1.64f : mode==1 ? floor+Vector3.up*.95f : floor+Vector3.up*.73f;
                string label=new[]{"standing downward ray","lower-view angled ray","crouched visible shaft near far end, seven mm away", "crouched rear-middle shaft, hand seven cm above", "crouched shaft centre, seven mm above"}[mode];
                Log($"{label} support={probe.support.name} contact={F(p)} normal={F(n)} visualVertices={verts} meshMin={min:F5} meshMax={max:F5} colliderMin={Height(lowPoint):F5} colliderMax={Height(highPoint):F5} actualGrip={Height(item.GripPosition):F5} tipAboveContact={Height(hand):F5} handToGrip={Vector3.Distance(hand,item.GripPosition):F4} surfaceGap={Vector3.Distance(hand,box.ClosestPoint(hand)):F4} hasOwner={player.Input.InteractionOwnsHand} worldFloor={ground.point.y:F4}");
                void Blockers(string line,Vector3 from,Vector3 to)
                {
                    var d=to-from;
                    var hits=Physics.RaycastAll(from,d.normalized,d.magnitude,~0,QueryTriggerInteraction.Ignore);
                    Log(line+" "+string.Join(",",hits.OrderBy(h=>h.distance).Select(h=>h.collider.name+"@"+h.distance.ToString("F4"))));
                }
                Blockers("hand to anchor",hand,expected);Blockers("head to anchor",head,expected);
                for(int i=0;i<3;i++)Step(hand,aim,false,yaw);
                bool shown=player.Input.Pickups.Highlighted(false)==item.transform;
                Step(hand,aim,true,yaw);
                held=belt.OwnsHeldItem(item) && glyph.IsHeld;
                Log($"RESULT {label}: shown={shown} held={held} gate={player.Input.CanInteract(out var reason)} reason={reason} bodyKinematic={body.isKinematic}");
                if(!held)continue;
                Assert(shown && belt.Held==item && glyph.penRoot==item.transform && item.LastDock==originalSlot,
                    "recovered the marked physical pen and retained its last successful holster");
                passed++; chapter.OpenPanel();
                var board=chapter.panel.board;
                var r=board.transform.rotation;
                head=physical;
                Vector3 offset=r*(Quaternion.Inverse(item.transform.rotation)*(glyph.nib.position-item.GripPosition));
                void Draw(float x,float z)=>Step(board.transform.TransformPoint(new Vector3(x,0,z))-offset,heading,true,r);
                Draw(-.03f,-.065f);Draw(-.03f,.002f);Draw(.0f,.002f);Draw(.03f,.002f);
                Assert(board.PointCount>=2,"same recovered brush writes on original story board");
                if(mode==0)
                {
                    ReleaseBesideBody(originalSlot, floor, yaw, .52f, "after a floor pickup and a stroke");
                    while(originalSlot.IsSnapping)yield return null;
                    TakeSeatedPen(originalSlot);
                }
            }
            if (held)
            {
                var home=originalSlot;
                var putRot=home.opening.rotation*Quaternion.Euler(item.stowEuler);
                var tipFromGrip=putRot*(Quaternion.Inverse(item.transform.rotation)*(item.insertionTip.position-item.GripPosition));
                var putGrip=home.opening.position-tipFromGrip;
                Step(putGrip,heading,true,putRot);
                Log($"inserting actual tip distance={Vector3.Distance(home.opening.position,item.insertionTip.position):F5} angle={Quaternion.Angle(putRot,item.transform.rotation):F3}");
                Step(putGrip,heading,false,putRot);
                Assert(item.Dock==home && !glyph.IsHeld && !player.Input.InteractionOwnsHand,"explicit release at original pen pocket");
                while(home.IsSnapping)yield return null;
                var rest=item.GripPosition;
                var remote=physical+yaw*new Vector3(.27f,-.12f,.56f);
                var ray=(rest-remote).normalized;
                Step(remote,ray,false,yaw);Step(remote,ray,false,yaw);
                Assert(player.Input.Pickups.Highlighted(false)==item.transform,"returned pen marked from afar with snap drivers parked");
                Step(remote,ray,true,yaw);
                Assert(belt.OwnsHeldItem(item),"returned same pen can be drawn a second time");
                putRot=home.opening.rotation*Quaternion.Euler(item.stowEuler);
                tipFromGrip=putRot*(Quaternion.Inverse(item.transform.rotation)*(item.insertionTip.position-item.GripPosition));
                putGrip=home.opening.position-tipFromGrip;
                Step(putGrip,heading,true,putRot);Step(putGrip,heading,false,putRot);
                Assert(item.Dock==home && !glyph.IsHeld && !player.Input.InteractionOwnsHand,"reseated cleanly after second pickup");
            }
            Assert(passed==5,"all five physical floor pickups");
        }
        static void TakeSeatedPen(ToolBeltSlot slot)
        {
            var belt=chapter.equipment.interaction;
            Assert(item.Dock==slot && slot.SnappedObject==item.gameObject && item.LastDock==slot,
                "same pen has completed docking before next pickup");
            var rest=item.GripPosition;
            var hand=rest+Vector3.up*.025f;
            var rotation=item.transform.rotation;
            Step(hand,Vector3.down,false,rotation);
            Step(hand,Vector3.down,true,rotation);
            Assert(belt.OwnsHeldItem(item) && glyph.IsHeld && !item.Dock && !slot.SnappedObject,
                "real pen taken from the same original holster");
        }
        static float BodyRadius(Vector3 point) =>
            Vector3.ProjectOnPlane(point-player.locomotion.head.position,Vector3.up).magnitude;

        static void ReleaseBesideBody(ToolBeltSlot slot,Vector3 floor,Quaternion yaw,float offset,string attempt)
        {
            head=player.locomotion.head.position;
            var low=head+yaw*new Vector3(offset,0,-.22f);
            low.y=floor.y+.18f;
            Step(low,heading,true,yaw);
            var belt=chapter.equipment.interaction;
            float radius=BodyRadius(item.GripPosition);
            float nearest=belt.slots.OfType<ToolBeltSlot>().Min(s=>Mathf.Sqrt(item.DistanceToOpeningSquared(s.opening.position)));
            Assert(belt.OwnsHeldItem(item) && radius<DefaultReturnRadius && head.y-item.GripPosition.y>DefaultReturnRadius &&
                nearest>belt.placementReach && !slot.SnappedObject,
                $"{attempt}: low side grip XZ={radius:F3}m, head height above grip={head.y-item.GripPosition.y:F3}m, nearest opening={nearest:F3}m");
            Step(low,heading,false,yaw);
            Assert(item.Dock==slot && slot.SnappedObject==item.gameObject && !belt.Held && !glyph.IsHeld &&
                !player.Input.InteractionOwnsHand, attempt+": low loose release returns this same pen to its previous slot");
        }
        static RaycastHit FindFloor(Vector3 eye,Vector3 floor,Quaternion yaw)
        {
            var centre=new Vector3(eye.x,floor.y,eye.z);
            foreach(var dir in new[]{new Vector3(-.48f,0,-.65f),new Vector3(.48f,0,-.65f),
                new Vector3(.80f,0,-.08f),new Vector3(-.8f,0,-.08f),new Vector3(0,0,-.80f),new Vector3(0,0,.80f)})
            {
                var top=centre+yaw*dir+Vector3.up*.95f;
                var hits=Physics.RaycastAll(top,Vector3.down,1.5f,~0,QueryTriggerInteraction.Ignore);
                foreach(var h in hits.OrderBy(h=>h.distance))
                {
                    if(h.collider.transform.IsChildOf(player.locomotion.rigRoot))continue;
                    if(h.normal.y>.93f && Mathf.Abs(h.point.y-floor.y)<.15f) return h;
                    break;
                }
            }
            throw new InvalidOperationException("No chapter floor close to stone-table objective");
        }
        static void Step(Vector3 grip,Vector3 dir,bool down,Quaternion rot)
        {
            clock=Math.Max(clock+.016,Time.realtimeSinceStartupAsDouble);
            var hand=new PlayerHandObservation {headPosition=head,headForward=heading,rightObserved=clock,rightValid=true,rightTip=grip,rightWrist=grip,
                rightOpen=!down,rightPointing=!down,gripPoseValid=true,gripPosition=grip,gripRotation=rot,pinchValid=true,systemPinch=true,systemPressed=down,
                rightIndexRayValid=true,rightIndexDirection=dir,rayValid=true,rayOrigin=grip,rayDirection=dir,rayAnchorValid=true};
            player.Input.Pickups.Evaluate(default,hand,clock);glyph.Observe(hand,clock);glyph.Advance(clock);
        }
    }
}
