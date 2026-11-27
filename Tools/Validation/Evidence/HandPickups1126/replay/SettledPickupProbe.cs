using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NarutoVR.SealCombat.Items;
using NarutoVR.SealCombat.Rpg;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NarutoVR.SealCombat.Editor
{
    static class SettledPickupProbe
    {
        static void Need(bool ok, string msg) { if (!ok) throw new InvalidOperationException(msg); }
        static string V(Vector3 v) => $"({v.x:F6},{v.y:F6},{v.z:F6})";
        public static IEnumerator Run(LinearRpgController p, string mode, Action<string> emit)
        {
            while (!p.CanAct) yield return null;
            var belt = Object.FindObjectsByType<ToolBeltInteraction>(FindObjectsSortMode.None).Single(b=>b.player==p);
            while (!belt.Initialised) yield return null;
            var ration = Object.FindObjectsByType<TravelRationInteraction>(FindObjectsSortMode.None).FirstOrDefault(x=>x.player==p);
            var cases = new List<GameItemBase>();
            if (mode=="floor-chapter")
            {
                var rs=p.GetComponent<ChapterRationSupply>();
                var ss=p.GetComponent<ChapterStakeSupply>();
                Need(rs && rs.TryGrant(), "Expected configured bag award");
                cases.Add(ration.pouch);
                if (ss) { Need(ss.TryGrant(),"Configured copper stake award"); cases.Add(ss.Item); }
            }
            else if (mode=="floor-ink")
            {
                cases.Add(belt.items.Single(i=>i && i.Kind==GameItemKind.PineInk));
                cases.Add(belt.items.Single(i=>i && i.Kind==GameItemKind.BronzeRubbing));
            }
            else cases.Add(belt.items.OfType<CooldownStake>().Single());
            var baseHeight=p.locomotion.PlayerFloorPosition.y+.08f;
            var center=new Vector3(80f,baseHeight,40f);
            var floor=new GameObject("Measure reference plane");
            var box=floor.AddComponent<BoxCollider>(); box.size=new Vector3(3,.1f,3);
            floor.transform.position=center-Vector3.up*.05f;
            int mask=p.locomotion.floorLayers.value;
            Need(mask!=0,"floor layer mask empty");
            int layer=0; while ((mask & (1<<layer))==0) layer++;
            floor.layer=layer;
            Physics.SyncTransforms();
            float y=box.bounds.max.y;
            emit("calibration box top worldY="+y.ToString("F6")+" layer="+layer+"; world space signed mm. neutral plane horizontal; target scene "+p.gameObject.scene.path);
            foreach (var item in cases)
            {
                while (!item.Dock || item.Dock.IsSnapping) yield return null;
                string label=mode+" "+item.Kind;
                bool isBag=item is TravelRationPouch;
                bool isStake=item is CooldownStake;
                bool flatPlate=item.Kind==GameItemKind.BronzeRubbing;
                Quaternion rot=Quaternion.Euler(flatPlate?90:0,32,isStake?0:flatPlate?0:90);
                Vector3 eye=center+new Vector3(.18f,1.6f,-.6f);
                double now=Time.realtimeSinceStartupAsDouble;
                PlayerHandObservation Pose(Vector3 tip,Vector3 point,bool pinch)
                {
                    Vector3 dir=(point-tip).normalized; if (dir.sqrMagnitude<.5f) dir=Vector3.down;
                    return new PlayerHandObservation{source="settled-geometry",headPosition=eye,headForward=Vector3.forward,
                        rightObserved=now,rightValid=true,rightOpen=!pinch,rightPointing=!pinch,rightTip=tip,rightWrist=tip-dir*.085f,
                        gripPoseValid=true,gripRotation=rot,gripPosition=tip,rightIndexRayValid=true,rightIndexDirection=dir,
                        rayValid=true,rayOrigin=tip-dir*.042f,rayDirection=dir,pinchValid=true,systemPinch=true,systemPressed=pinch};
                }
                void Sample(bool left,Vector3 pos,Vector3 point,bool pinch)
                {
                    now+=.016;
                    var f=Pose(pos,point,pinch); var r=left?default:f;
                    p.Input.Pickups.Evaluate(left?f:default,r,now);
                    if (isBag)
                    {
                        var finger=new TravelRationInteraction.PinchSample(pos,rot,pinch?.012f:.06f);
                        ration.Step(left?finger:default,left?default:finger,.016f,false);
                    }
                    else belt.Step(r,now,.016f);
                }
                void Drop(bool left, bool planted)
                {
                    var pos=center+Vector3.up*.15f;
                    if (planted) pos=center+(item.GripPosition-item.insertionTip.position)+Vector3.up*.018f;
                    if (planted)
                    {
                        var targetOffset=rot*Quaternion.Inverse(item.transform.rotation)*(item.GripPosition-item.insertionTip.position);
                        pos=center+targetOffset+Vector3.up*.018f;
                    }
                    Sample(left,pos,pos+Vector3.down,true); Sample(left,pos,pos+Vector3.down,false);
                    Need(!item.IsHeld && !item.Dock && !p.Input.InteractionOwnsHand,label+" release from real owner");
                    if (isBag) ((TravelRationPouch)item).AdvanceOpening(1);
                    Physics.SyncTransforms();
                    var previous=Physics.simulationMode;
                    try
                    {
                        Physics.simulationMode=SimulationMode.Script;
                        for(int k=0;k<300;k++) Physics.Simulate(1f/120);
                    }
                    finally { Physics.simulationMode=previous; }
                    Physics.SyncTransforms();
                }
                // Acquire once via the authored pocket using the chapter's live player eye.
                var stored=item.GripPosition;
                var virtualEye=eye; eye=p.locomotion.head.position;
                Sample(false,stored,stored,false); Sample(false,stored,stored,true);
                Need(item.IsHeld,label+" initial named pocket pickup"); eye=virtualEye;
                bool mine=false;
                int count=isStake?3:2;
                for (int pass=0;pass<count;pass++)
                {
                    bool planted=isStake && pass==2;
                    if (isStake) rot=planted?Quaternion.Euler(0,32,0):Quaternion.Euler(0,32,90);
                    Drop(mine,planted);
                    var body=item.GetComponent<Rigidbody>();
                    var cols=item.GetComponentsInChildren<Collider>().Where(c=>c.enabled && !c.isTrigger && c.attachedRigidbody==body).ToArray();
                    Need(cols.Length>0,label+" physical colliders");
                    float lo=cols.Min(c=>c.bounds.min.y), hi=cols.Max(c=>c.bounds.max.y);
                    float meshMin=float.PositiveInfinity, meshMax=float.NegativeInfinity;
                    int vertices=0;
                    var visible = new HashSet<Renderer>(item.GetComponentsInChildren<MeshRenderer>().Where(r=>r.enabled));
                    foreach (var group in item.GetComponentsInChildren<LODGroup>())
                    {
                        var lods=group.GetLODs();
                        var lod0=new HashSet<Renderer>(lods.Take(1).SelectMany(l=>l.renderers));
                        foreach(var r in lods.Skip(1).SelectMany(l=>l.renderers)) if(!lod0.Contains(r))visible.Remove(r);
                    }
                    var stake=item as CooldownStake;
                    foreach(var renderer in visible)
                    {
                        if(stake && stake.dial && renderer.transform.IsChildOf(stake.dial)) continue;
                        if(!renderer.TryGetComponent<MeshFilter>(out var mf) || !mf.sharedMesh) continue;
                        using var meshes=MeshUtility.AcquireReadOnlyMeshData(mf.sharedMesh);
                        using var buf=new NativeArray<Vector3>(meshes[0].vertexCount,Allocator.Temp);
                        meshes[0].GetVertices(buf);
                        foreach(var vertex in buf)
                        {
                            float height=mf.transform.TransformPoint(vertex).y;
                            meshMin=Mathf.Min(meshMin,height); meshMax=Mathf.Max(meshMax,height);
                        }
                        vertices+=buf.Length;
                    }
                    Need(vertices>0 && Mathf.Abs(lo-y)<.035f,label+" resting on reference plane, bottom "+(lo-y));
                    Need(planted ? stake && stake.Planted && body.isKinematic : body.IsSleeping() || (body.linearVelocity.sqrMagnitude<.00001f && body.angularVelocity.sqrMagnitude<.0001f),label+" stable/planted");
                    // Take an actual downward hit on the upward-facing surface; the center of its AABB is not used as a contact.
                    var highest=cols.OrderByDescending(c=>c.bounds.max.y).First(); var cb=highest.bounds;
                    bool touched=highest.Raycast(new Ray(new Vector3(cb.center.x,hi+.20f,cb.center.z),Vector3.down),out var h,.6f);
                    Need(touched,label+" downward surface ray");
                    var point=h.point; bool near=(pass==0); bool left=near;
                    var tip=near?point+Vector3.up*.012f:point+new Vector3(.15f,.66f,-.46f);
                    if(pass==2) left=true;
                    string row=$"{label} mode={(planted?"planted":near?"free-near":"free-ray")} floorY={y:F6} meshMm=({(meshMin-y)*1000:F2},{(meshMax-y)*1000:F2}) colliderMm=({(lo-y)*1000:F2},{(hi-y)*1000:F2}) gripMm={(item.GripPosition.y-y)*1000:F2} meshMinusColliderMm=({(meshMin-lo)*1000:F2},{(meshMax-hi)*1000:F2}) raySurfaceMm={(point.y-y)*1000:F2} vertices={vertices} solids={cols.Length} center={V(item.transform.position)} euler={V(item.transform.eulerAngles)}";
                    emit(row);
                    if (!near)
                    {
                        var wall=new GameObject("LOS wall");
                        var bc=wall.AddComponent<BoxCollider>();bc.size=new Vector3(.16f,.16f,.16f);
                        wall.transform.position=Vector3.Lerp(tip,point,.45f);
                        Physics.SyncTransforms();
                        Sample(left,tip,point,false);Sample(left,tip,point,true);
                        Need(!item.IsHeld,label+" intervening wall"); wall.SetActive(false);Object.Destroy(wall); Physics.SyncTransforms();
                    }
                    Sample(left,tip,point,false);
                    bool selected=p.Input.Pickups.Highlighted(left)==item.transform;
                    var line=p.GetComponentsInChildren<LineRenderer>().FirstOrDefault(l=>l.enabled && l.GetComponent<Oculus.Interaction.ConicalFrustum>());
                    var mark=line?line.GetPosition(1):Vector3.zero;
                    emit(label+" hover="+selected+" near="+near+" expectedSurface="+V(point)+" marker="+V(mark)+" tip="+V(tip));
                    Sample(left,tip,point,true);
                    Need(selected && item.IsHeld && p.Input.InteractionOwnsHand,label+" surfaced near/ray pickup");
                    mine=left;
                }
                // Leaves the claim open on purpose until this final world release.
                Drop(mine,false);
                item.gameObject.SetActive(false);
            }
        }
    }
}

