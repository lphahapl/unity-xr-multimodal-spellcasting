using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NarutoVR.SealCombat.Enemies;
using NarutoVR.SealCombat.Rpg;
using NarutoVR.SealLab;
using Oculus.Interaction.Input;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Management;
using UnityEditor.XR.Management;
using Object = UnityEngine.Object;

namespace NarutoVR.SealCombat.Editor
{
    [InitializeOnLoad]
    public static class StoryPickupChecks
    {
        const string Key = "NarutoVR.QA.StoryPickup";
        static string Report => "Logs/StoryPickup-" + SessionState.GetString(Key,"") + ".txt";
        static IEnumerator tests;
        static double started;
        static LinearRpgController p;
        static OpeningLeafPuzzle leaf;
        static WorkshopTenonObjective tenon;
        static SealFrame frame;
        static readonly PropertyInfo Frame = typeof(SealLab.SealLab).GetProperty("Frame");
        static readonly List<XRDisplaySubsystem> displays = new();

        static void AssertXrOff(string phase, bool report)
        {
            var target = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Standalone);
            var settings = XRGeneralSettings.Instance;
            var manager = settings ? settings.Manager : null;
            displays.Clear(); SubsystemManager.GetSubsystems(displays);
            int running = displays.Count(display => display.running);
            var runtime=Environment.GetEnvironmentVariable("XR_RUNTIME_JSON");
            if (!Application.isBatchMode || EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64 || !target || settings != target ||
                settings.InitManagerOnStart || !manager || manager.activeLoaders.Count!=0 || manager.activeLoader || manager.isInitializationComplete || running!=0 ||
                string.IsNullOrEmpty(runtime) || !Path.IsPathFullyQualified(runtime) || File.Exists(runtime))
                throw new InvalidOperationException("XR isolation failed at "+phase);
            if (report) File.AppendAllText(Report,"XR "+phase+": Standalone actual="+settings.name+", InitManagerOnStart="+settings.InitManagerOnStart+", loaderCount="+manager.activeLoaders.Count+", activeLoader=null, isInitializationComplete="+manager.isInitializationComplete+", runningDisplays="+running+", XR_RUNTIME_JSON="+runtime+" (absent)\n");
        }

        static StoryPickupChecks() => EditorApplication.update += Update;
        public static void Leaf() => Start("Leaf","Chapter01_Courtyard");
        public static void Tenon() => Start("Tenon","Chapter02_SuspendedPath");
        public static void LeafSocket() => Start("LeafSocket","Chapter01_Courtyard");
        public static void LeafCheckpoint() => Start("LeafCheckpoint","Chapter01_Courtyard");
        public static void TenonSocket() => Start("TenonSocket","Chapter02_SuspendedPath");
        public static void TenonFloor() => Start("TenonFloor","Chapter02_SuspendedPath");
        static void Start(string kind,string scene)
        {
            if(!Application.isBatchMode) throw new InvalidOperationException("Use an isolated batch editor.");
            EditorSceneManager.OpenScene("Assets/Scene/"+scene+".unity");
            var player=Object.FindFirstObjectByType<LinearRpgController>();
            player.autoLoad=false;
            player.saveFileName="story-pickup-qa-"+Guid.NewGuid().ToString("N")+".json";
            SessionState.SetString(Key,kind);
            File.WriteAllText(Report,DateTimeOffset.Now.ToString("o")+" "+Application.unityVersion+"\n");
            AssertXrOff("before play",true);
            EditorApplication.EnterPlaymode();
        }
        static void Check(bool ok,string name)
        {
            if(!ok) throw new InvalidOperationException(name);
            File.AppendAllText(Report,"PASS "+name+"\n");
        }
        static void Update()
        {
            if(!Application.isPlaying || SessionState.GetString(Key,"").Length==0) return;
            try
            {
                AssertXrOff("play",tests==null);
                if(tests==null) { tests=Run(); started=EditorApplication.timeSinceStartup; }
                if(EditorApplication.timeSinceStartup-started>150) throw new TimeoutException("waiting in "+(p ? p.Status : "startup"));
                if(tests.MoveNext()) return;
                AssertXrOff("after interactions",true);
                File.AppendAllText(Report,"PASS END\n");
                End(0);
            }
            catch(Exception ex) { File.AppendAllText(Report,"FAIL "+ex+"\n"); End(1); }
        }
        static void End(int code){tests=null;SessionState.EraseString(Key);EditorApplication.Exit(code);}

        static IEnumerator Run()
        {
            p=Object.FindFirstObjectByType<LinearRpgController>();
            while(!p.CanAct) yield return null;
            yield return null;
            frame=new SealFrame{source=p.handTracking.Frame?.source};
            p.handTracking.recordingOrigin=null;
            if(SessionState.GetString(Key,"")=="LeafSocket" || SessionState.GetString(Key,"")=="LeafCheckpoint")
            {
                var chapter=(OpeningTrialChapter)p.chapter;
                leaf=chapter.puzzle;
                var ordered=p.stages.Select(s=>chapter.sections.Single(x=>x.stage==s)).ToArray();
                int idx=Array.FindIndex(ordered,x=>x.lines.Any(l=>l.action==OpeningAction.InsertLeaf));
                var data=p.CaptureProgressSnapshot();
                var skills=p.skills.ConfiguredSkills.Where(s=>s).ToArray();
                var store=new RpgSaveStore(UnityRpgSaveJson.Instance,p.SavePath,"linear-rpg-player",skills.Select(s=>s.skillId),
                    p.PlayerView.MaxHealth,p.PlayerView.MaxMana,skills.Select(s=>string.IsNullOrEmpty(s.cooldownGroup)?s.skillId:s.cooldownGroup).Distinct());
                void Merge(List<string> dest,IEnumerable<string> ids){foreach(var id in ids)if(!dest.Contains(id))dest.Add(id);}
                var def=chapter.inventory.catalog.Find(leaf.leafId);
                foreach(var section in ordered.Take(idx))
                {
                    data.completedStageIds.Add(section.stage.stageId);
                    data.claimedRewardIds.Add("reward."+section.stage.stageId);
                    data.currency+=section.stage.rewardCurrency;
                    Merge(data.discoveredSkillIds,section.stage.unlockSkillIds);Merge(data.unlockedSkillIds,section.stage.unlockSkillIds);
                    if(section.lines.Any(l=>l.action==OpeningAction.CollectLeaf))
                    {data.items.Add(new RpgItemProgressData{itemId=def.itemId});Merge(data.discoveredSkillIds,def.discoverSkillIds);}
                }
                data.checkpointId=p.checkpointNodes[idx].nodeId;
                Check(store.TrySave(data,out string reason),"save collected loose leaf before LF02: "+reason);
                bool restoringCheckpoint=SessionState.GetString(Key,"")=="LeafCheckpoint";
                bool restored=restoringCheckpoint && p.RestoreCheckpoint();
                if (restoringCheckpoint && !restored)
                {
                    var loco=p.locomotion;var target=p.checkpointNodes[idx];
                    var flags=BindingFlags.Instance|BindingFlags.NonPublic;
                    var expected=(Vector3)loco.GetType().GetProperty("ExpectedFloor",flags).GetValue(loco);
                    var floor=loco.GetType().GetMethod("TryFloor",flags);
                    object[] from={expected,loco.floorLayers.value,null,null};
                    var current=(bool)floor.Invoke(loco,from)?((RaycastHit)from[2]).point:expected;
                    object[] to={target.floorPosition,target.overrideLayers?target.floorLayers.value:loco.floorLayers.value,null,null};
                    if((bool)floor.Invoke(loco,to))
                    {
                        object[] shape={to[2],current,null,null,0f};loco.GetType().GetMethod("Capsule",flags).Invoke(loco,shape);
                        var overlaps=Physics.OverlapCapsule((Vector3)shape[2],(Vector3)shape[3],(float)shape[4],target.overrideLayers?target.obstacleLayers.value:loco.obstacleLayers.value,QueryTriggerInteraction.Ignore);
                        File.AppendAllText(Report,"CHECKPOINT "+target.name+" at "+target.floorPosition+", floor "+((RaycastHit)to[2]).collider.name+", height "+(loco.head.position.y-current.y)+", capsule "+shape[2]+".."+shape[3]+", overlaps "+string.Join(", ",overlaps.Select(c=>c.name+"/"+c.transform.root.name))+"\n");
                    }
                }
                if(restoringCheckpoint) Check(restored&&p.StageIndex==idx,"restore authored LF02 stage ("+restored+", "+p.StageIndex+"/"+idx+"): "+p.Status);
                else
                {
                    Check(store.TryLoad(out var progress,out string diagnostic),"read collected, unactivated LF02 progress: "+diagnostic);
                    p.Model.RestoreGrowth(progress);
                    chapter.SendMessage("Restore",SendMessageOptions.RequireReceiver);
                    Check(p.StageIndex==idx && !leaf.IsSeated && leaf.IsEarnedLeaf(leaf.leaf),"seed collected LF02 progress in place, use real chapter and inventory callbacks");
                }
                chapter.SendMessage("Update",SendMessageOptions.RequireReceiver);
                while(!leaf.slot.enabled) yield return null;
                for(var node=leaf.slot.transform;node;node=node.parent)node.gameObject.SetActive(true);
                Check(leaf.slot.isActiveAndEnabled,"LF02 bound authored wrist snap listener");
                var start=leaf.leaf.transform.position;
                Face(start);
                Attempt(false,start+Vector3.up*.45f,start);
                Check(p.Input.InteractionOwnsHand,"right distant pickup owns collected leaf for insertion");
                var insert=leaf.slotEntry.position;
                Send(false,insert,Vector3.down,true);
                int callbacks=0;leaf.slot.OnSnapCompleted+=obj=>{if(obj==leaf.leaf)callbacks++;};
                Send(false,insert,Vector3.down,false);
                Check(leaf.slot.SnappedObject==leaf.leaf && !p.Input.InteractionOwnsHand,"legal wrist release starts real production snap");
                Complete(leaf.slot);
                Check(leaf.IsSeated && callbacks==1 && p.CaptureProgressSnapshot().items.Single(i=>i.itemId==leaf.leafId).activated,
                    "completed LF02 wrist path seats original leaf and persists one activation");
                Surfaces("leaf seated",leaf.leaf.transform,leaf.leaf.transform.position);
                var final=leaf.leaf.transform.position;
                Attempt(false,final+Vector3.up*.012f,final);Attempt(true,final+Vector3.up*.46f,final);
                Check(!p.Input.InteractionOwnsHand && leaf.IsSeated && leaf.slot.SnappedObject==leaf.leaf && callbacks==1 && (final-leaf.leaf.transform.position).sqrMagnitude<.00001f,
                    "seated LF02 leaf survives right near and left distant retry without a second activation");
            }
            else if(SessionState.GetString(Key,"")=="Leaf")
            {
                leaf=Object.FindFirstObjectByType<OpeningLeafPuzzle>(FindObjectsInactive.Include);
                Check(leaf.isActiveAndEnabled,"authored first-chapter puzzle active");
                leaf.Restore(Array.Empty<string>());
                var stand=leaf.prizeRest.position;
                Surfaces("leaf reward rest",leaf.leaf.transform,leaf.leaf.transform.position);
                Face(stand);
                var origin=stand+Vector3.up*.60f;
                Attempt(true,origin,stand);
                Check(!p.Input.InteractionOwnsHand,"unsolved leaf cannot be retrieved");
                leaf.Restore(new[]{leaf.solvedStage.stageId}); leaf.EnableReward();
                int grants=0;leaf.Collected+=()=>grants++;
                Check(leaf.leaf.activeInHierarchy,"solved leaf visible for taking");
                var wall=Block(origin,stand);
                Attempt(true,origin,stand);
                Check(!p.Input.InteractionOwnsHand && grants==0,"wall blocks left pickup of solved leaf");
                wall.SetActive(false);Physics.SyncTransforms();
                Attempt(true,origin,stand);
                Check(p.Input.InteractionOwnsHand && grants==1 && Vector3.Distance(leaf.leaf.transform.position,origin)<.003f,
                    "left far pinch moves same leaf to fingertip, award once");
                Send(true,origin,Vector3.down,true,false);
                Check(p.Input.InteractionOwnsHand,"brief tracking loss retains leaf");
                Send(true,origin,Vector3.down,true);
                Send(true,origin+Vector3.right*.09f,Vector3.down,true);
                Check(Vector3.Distance(leaf.leaf.transform.position,origin+Vector3.right*.09f)<.003f,"held leaf follows left hand after tracking returns");
                Send(true,origin,Vector3.down,false);
                Check(!p.Input.InteractionOwnsHand,"left release gives back ownership");
                var dropped=leaf.leaf.transform.position;
                Surfaces("leaf after release",leaf.leaf.transform,dropped);
                var near=dropped+Vector3.right*.015f;
                Attempt(false,near,dropped);
                Check(p.Input.InteractionOwnsHand && grants==1 && Vector3.Distance(leaf.leaf.transform.position,near)<.003f,
                    "right near pickup of dropped leaf; no duplicate reward");
                Send(false,near,Vector3.down,false);
                double returnAt=Time.time+2.4;
                while(Time.time<returnAt) yield return null;
                Check((leaf.leaf.transform.position-leaf.prizeRest.position).sqrMagnitude<.00001f,"loose story leaf recalls to its reward rest after two seconds");
                leaf.Seat(leaf.leaf);
                Attempt(false,near,leaf.leaf.transform.position);
                Check(leaf.IsSeated && !p.Input.InteractionOwnsHand,"seated leaf cannot be taken back");
            }
            else
            {
                tenon=Object.FindFirstObjectByType<WorkshopTenonObjective>(FindObjectsInactive.Include);
                if(!tenon.IsRunning) tenon.Begin(p);
                var item=tenon.tenon;
                var stand=item.GripPosition;
                Surfaces("tenon on holder",item.transform,stand);
                var origin=stand+Vector3.up*.6f;
                Face(stand);
                Attempt(false,origin,stand);
                Check(!tenon.IsUnlocked && !item.IsHeld && !p.Input.InteractionOwnsHand,"locked workshop tenon ignored");
                var yard=tenon.courtyard.spawnPoints.Single().point.position;
                p.locomotion.rigRoot.position+=yard-p.locomotion.PlayerFloorPosition;
                Physics.SyncTransforms();tenon.roof.TickEncounter();tenon.courtyard.TickEncounter();
                while(!tenon.roof.HasStarted || !tenon.courtyard.HasStarted) yield return null;
                foreach(var enemy in tenon.roof.enemies) Kill(enemy);
                tenon.roof.TickEncounter();
                Check(!tenon.IsUnlocked,"roof cleared alone keeps fixture locked");
                foreach(var enemy in tenon.courtyard.enemies) Kill(enemy);
                tenon.courtyard.TickEncounter();
                while(!tenon.IsUnlocked)yield return null;
                Face(stand);
                var wall=Block(origin,stand);
                Attempt(false,origin,stand);
                Check(!item.IsHeld,"wall blocks retrieval of unlocked workshop tenon");
                wall.SetActive(false);Physics.SyncTransforms();
                Attempt(false,origin,stand);
                Check(item.IsHeld && p.Input.InteractionOwnsHand && (item.GripPosition-origin).sqrMagnitude<.03f,
                    "right far pinch acquires existing tenon after both rooms clear");
                Send(false,origin,Vector3.down,false);
                Check(!item.IsHeld && !p.Input.InteractionOwnsHand && item.GetComponent<Rigidbody>().useGravity,"release outside socket restores world physics");
                if(SessionState.GetString(Key,"")=="TenonSocket")
                {
                    var initial=item.transform.position;var body=item.GetComponent<Rigidbody>();var end=Time.time+2.2f;
                    while(Time.time<end && !body.IsSleeping()) yield return null;
                    Check(!body.isKinematic && body.useGravity && (initial-item.transform.position).y>.015f,"dropped tenon falls via authored rigidbody");
                    Surfaces("tenon after physical fall, speed="+body.linearVelocity.magnitude.ToString("F3"),item.transform,item.GripPosition);
                    Face(item.GripPosition);
                }
                var near=item.GripPosition+Vector3.right*.005f;
                Attempt(true,near,item.GripPosition);
                Check(item.IsHeld && p.Input.InteractionOwnsHand,"left near pickup of dropped tenon");
                Send(true,near,Vector3.down,true,false);
                Check(item.IsHeld,"brief left tracking loss retains tenon");
                Send(true,near,Vector3.down,true);
                Send(true,near+Vector3.right*.1f,Vector3.down,true);
                Check((item.GripPosition-near-Vector3.right*.1f).magnitude<.14f,"left hold follows reacquired hand");
                if(SessionState.GetString(Key,"")=="TenonFloor")
                {
                    var dropFloor=Physics.RaycastAll(yard+new Vector3(.38f,1,.32f),Vector3.down,3,p.locomotion.floorLayers,QueryTriggerInteraction.Ignore)
                        .Where(hit=>hit.collider && !hit.collider.attachedRigidbody && !hit.collider.transform.IsChildOf(p.locomotion.rigRoot) &&
                            Vector3.Angle(hit.normal,Vector3.up)<=p.locomotion.maximumSlope && hit.point.y<=yard.y+.14f)
                        .OrderBy(hit=>hit.distance).First();
                    var release=dropFloor.point+Vector3.up*.63f;
                    Send(true,release,Vector3.down,true);Send(true,release,Vector3.down,false);
                    var body=item.GetComponent<Rigidbody>();var start=item.transform.position;var end=Time.time+3;
                    while(Time.time<end && !body.IsSleeping())yield return null;
                    Check(!body.isKinematic&&body.useGravity && !item.IsHeld && start.y-item.transform.position.y>.25f && body.linearVelocity.magnitude<.05f,
                        "authored tenon settles by gravity on courtyard floor away from its holder: "+dropFloor.collider.name);
                    Surfaces("tenon on courtyard floor",item.transform,item.GripPosition);
                    var grip=item.GripPosition;Face(grip);
                    var far=grip+new Vector3(.03f,.55f,.03f);var direction=(grip-far).normalized;
                    Send(false,far,direction,false);Send(false,far,direction,false);
                    Check(p.Input.Pickups.Highlighted(false)==item.transform,"ray marks the fallen tenon on authored courtyard floor");
                    Send(false,far,direction,true);
                    Check(item.IsHeld && p.Input.InteractionOwnsHand && !tenon.IsSeated,"right distant pinch retrieves that same fallen tenon");
                    Send(false,far,direction,false);
                    yield break;
                }
                Send(true,near,Vector3.down,false);
                Check(!item.IsHeld && !tenon.IsSeated,"ordinary release cannot power machinery");
                if(SessionState.GetString(Key,"")=="TenonSocket")
                {
                    near=item.GripPosition+Vector3.up*.005f;
                    Attempt(false,near,item.GripPosition);
                    Check(item.IsHeld,"right re-acquires loose tenon for legal seating");
                    var pin=tenon.socket.mouth;
                    var rotation=pin.rotation;
                    var rel=Quaternion.Inverse(item.transform.rotation)*(item.GripPosition-item.transform.position);
                    var desiredGrip=pin.TransformPoint(0,.07f,0)+rotation*rel;
                    void Move(bool down)
                    {
                        var now=Time.realtimeSinceStartupAsDouble;
                        var hand=p.Input.LatestHand;
                        hand.rightObserved=now;hand.source="SocketCheck";hand.rightValid=hand.gripPoseValid=hand.pinchValid=hand.systemPinch=true;
                        hand.pinchTransition=hand.tipUnavailable=false;hand.systemPressed=down;hand.rightTip=hand.gripPosition=desiredGrip;hand.gripRotation=rotation;
                        typeof(WorkshopTenonObjective).GetMethod("Step",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(tenon,new object[]{hand,now});
                    }
                    int callbacks=0;tenon.Seated+=()=>callbacks++;
                    Move(true);Move(false);
                    Check(!item.IsHeld && !p.Input.InteractionOwnsHand && tenon.socket.SnappedObject==item.gameObject,"aligned release above workshop mouth enters authored socket");
                    Complete(tenon.socket);
                    Check(tenon.IsSeated && tenon.IsComplete && callbacks==1 && tenon.consoleReady.activeSelf,
                        "completed socket seats original tenon once and powers authored console");
                    var final=item.transform.position;
                    Face(final);
                    Attempt(false,item.GripPosition+Vector3.up*.012f,item.GripPosition);Attempt(true,item.GripPosition+Vector3.up*.45f,item.GripPosition);
                    Check(!item.IsHeld && !p.Input.InteractionOwnsHand && !item.enabled && callbacks==1 && tenon.socket.SnappedObject==item.gameObject && (item.transform.position-final).sqrMagnitude<.00001f,
                        "seated workshop tenon rejects right near and left distant pickup and remains powered");
                }
            }
        }
        static void Complete(NeuGameMRTools.Runtime.Components.SnapZone socket) => typeof(NeuGameMRTools.Runtime.Components.SnapZone)
            .GetMethod("Advance",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(socket,new object[]{10f});
        static void Surfaces(string phase,Transform root,Vector3 grip)
        {
            Physics.SyncTransforms();
            float meshMin=float.PositiveInfinity, surfaceMin=float.PositiveInfinity,surfaceMax=float.NegativeInfinity;
            int samples=0;
            foreach(var shape in root.GetComponentsInChildren<MeshFilter>())
            {
                if(!shape.sharedMesh || !shape.TryGetComponent<MeshRenderer>(out var renderer) || !renderer.enabled) continue;
                using var data=UnityEditor.MeshUtility.AcquireReadOnlyMeshData(shape.sharedMesh);
                using var vertices=new Unity.Collections.NativeArray<Vector3>(data[0].vertexCount,Unity.Collections.Allocator.Temp);
                data[0].GetVertices(vertices);
                foreach(var v in vertices) meshMin=Mathf.Min(meshMin,shape.transform.TransformPoint(v).y);
            }
            var colliders=root.GetComponentsInChildren<Collider>().Where(c=>c.enabled).ToArray();
            foreach(var c in colliders)
            {
                var b=c.bounds;
                foreach(float x in new[]{-.45f,0,.45f}) foreach(float z in new[]{-.45f,0,.45f}) foreach(float sign in new[]{-1f,1f})
                {
                    var start=b.center+new Vector3(x*b.size.x,sign*(b.extents.y+.02f),z*b.size.z);
                    if(c.Raycast(new Ray(start,Vector3.down*sign),out var hit,b.size.y+.04f))
                    {surfaceMin=Mathf.Min(surfaceMin,hit.point.y);surfaceMax=Mathf.Max(surfaceMax,hit.point.y);samples++;}
                }
            }
            var hits=Physics.RaycastAll(grip+Vector3.up*.01f,Vector3.down,80,p.locomotion.floorLayers,QueryTriggerInteraction.Ignore)
                .Where(h=>!h.collider.transform.IsChildOf(root) && !h.collider.transform.IsChildOf(p.locomotion.rigRoot) && Vector3.Angle(h.normal,Vector3.up)<=p.locomotion.maximumSlope).OrderBy(h=>h.distance).ToArray();
            if(hits.Length==0)throw new InvalidOperationException("No authored raycast support below "+phase+" at "+grip);
            var ground=hits[0];
            string Relative(float y)=>float.IsInfinity(y)?"unavailable":(y-ground.point.y).ToString("F4");
            File.AppendAllText(Report,"SURFACE "+phase+" | support="+ground.collider.name+" y="+ground.point.y.ToString("F4")+" | meshMin="+Relative(meshMin)+" | touchLower="+Relative(surfaceMin)+" touchUpper="+Relative(surfaceMax)+" | grip="+Relative(grip.y)+" | enabledColliders="+colliders.Length+" surfaceSamples="+samples+" | metres above nearest walkable support under grip\n");
        }
        static void Kill(EnemyController enemy)
        {
            if(!enemy.Entity.ReceiveHit("pickup-check-"+Guid.NewGuid().ToString("N"),1000,0)) throw new Exception("enemy damage rejected: "+enemy.name);
        }
        static void Face(Vector3 target)
        {
            p.locomotion.rigRoot.position+=target+new Vector3(0,.82f,-.26f)-p.locomotion.head.position;
            Physics.SyncTransforms();
        }
        static GameObject Block(Vector3 from,Vector3 to)
        {
            var o=GameObject.CreatePrimitive(PrimitiveType.Cube);
            o.name="QA pickup visibility blocker";
            o.transform.position=(from+to)*.5f; o.transform.localScale=Vector3.one*.22f;
            Physics.SyncTransforms();return o;
        }
        static void Attempt(bool left,Vector3 tip,Vector3 target)
        {
            var aim=(target-tip).normalized;
            Send(left,tip,aim,false);
            Send(left,tip,aim,false);
            Send(left,tip,aim,true);
        }
        static void Send(bool left,Vector3 tip,Vector3 direction,bool down,bool tracked=true)
        {
            var now=Time.realtimeSinceStartupAsDouble;
            var q=Quaternion.LookRotation(direction,Vector3.Angle(direction,Vector3.up)%180<10 || Vector3.Angle(direction,Vector3.down)<10 ? Vector3.forward : Vector3.up);
            var hand=left?frame.left:frame.right;
            var other=left?frame.right:frame.left;other.tracked=other.fresh=false;
            hand.observedAt=frame.time=now;
            hand.connected=hand.tracked=hand.fresh=hand.highConfidence=tracked;
            void Joint(HandJointId id,Vector3 pos)
            { hand.jointValid[(int)id]=tracked; hand.joints[(int)id]=new Pose(tip+q*pos,q); }
            Joint(HandJointId.HandWristRoot,new Vector3(0,0,-.13f));
            Joint(HandJointId.HandIndex1,new Vector3(0,0,-.06f));
            Joint(HandJointId.HandMiddle1,new Vector3(.015f,0,-.06f));
            Joint(HandJointId.HandPinky1,new Vector3(.035f,0,-.07f));
            Joint(HandJointId.HandIndex2,new Vector3(0,0,-.035f));
            Joint(HandJointId.HandIndexTip,Vector3.zero);
            Joint(HandJointId.HandThumbTip,Vector3.right*(down?.012f:.07f));
            Frame.SetValue(p.handTracking,frame);
            p.Input.SampleHands(now);
            if(leaf)leaf.SendMessage("LateUpdate",SendMessageOptions.RequireReceiver);
            if(tenon)tenon.SendMessage("LateUpdate",SendMessageOptions.RequireReceiver);
        }
    }
}
