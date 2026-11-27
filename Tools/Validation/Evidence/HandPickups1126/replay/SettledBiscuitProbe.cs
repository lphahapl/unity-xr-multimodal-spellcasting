using System;
using System.Collections;
using System.Linq;
using NarutoVR.SealCombat.Items;
using NarutoVR.SealCombat.Rpg;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace NarutoVR.SealCombat.Editor
{
    static class SettledBiscuitProbe
    {
        static void Need(bool b,string label) { if(!b) throw new InvalidOperationException(label); }
        public static IEnumerator Run(LinearRpgController p, Action<string> emit)
        {
            while(!p.CanAct) yield return null;
            var g=Object.FindObjectsByType<TravelRationInteraction>(FindObjectsSortMode.None).Single(x=>x.player==p);
            while(!g.sharedBelt.Initialised) yield return null;
            var supply=p.GetComponent<ChapterRationSupply>(); Need(supply.TryGrant(),"grant");
            var bag=g.pouch;
            while(!bag.Dock || bag.Dock.IsSnapping) yield return null;
            double now=Time.realtimeSinceStartupAsDouble;
            var head=p.locomotion.head; var rot=head.rotation;
            var left=bag.GripPosition; var right=head.TransformPoint(new Vector3(.25f,-.20f,.55f));
            PlayerHandObservation Pose(Vector3 tip,Vector3 target,bool pinched)
            {
                var dir=target-tip; dir=dir.sqrMagnitude>.000001f ? dir.normalized : head.forward;
                return new PlayerHandObservation{source="biscuit-face",rightObserved=now,rightValid=true,rightOpen=!pinched,rightPointing=!pinched,
                    headPosition=head.position,headForward=head.forward,rightTip=tip,rightWrist=tip-dir*.075f,gripPosition=tip,gripRotation=rot,gripPoseValid=true,
                    rightIndexRayValid=true,rightIndexDirection=dir,rayOrigin=tip-dir*.038f,rayValid=true,rayDirection=dir,pinchValid=true,
                    systemPinch=true,systemPressed=pinched};
            }
            void Frame(Vector3 at,bool lp, bool rp, bool step)
            {
                now+=.016; p.Input.Pickups.Evaluate(Pose(left,left,lp),Pose(right,at,rp),now);
                if(step) g.Step(new TravelRationInteraction.PinchSample(left,rot,lp?.012f:.06f),new TravelRationInteraction.PinchSample(right,rot,rp?.012f:.06f),.016f,false);
            }
            Frame(left,false,false,false);Frame(left,true,false,false);
            Need(g.BagHand==0 && bag.IsHeld,"left owns bag");
            left=head.TransformPoint(new Vector3(-.2f,-.15f,.42f));Frame(right,true,false,true);bag.AdvanceOpening(1);
            Need(bag.Open,"bag unfolded");
            foreach(bool near in new[]{true,false})
            {
                Transform target=null; Vector3 at=default; int index=-1;
                foreach(var part in bag.biscuits.Reverse())
                {
                    var mf=part.GetComponentsInChildren<MeshFilter>().FirstOrDefault(x=>x.sharedMesh && x.GetComponent<Renderer>().enabled);
                    if(!mf) continue;
                    var approach=head.TransformPoint(new Vector3(.24f,-.15f,.38f));
                    using var meshes=MeshUtility.AcquireReadOnlyMeshData(mf.sharedMesh);
                    using var vertices=new NativeArray<Vector3>(meshes[0].vertexCount,Allocator.Temp);
                    meshes[0].GetVertices(vertices);
                    var aim=(part.position-approach).normalized;
                    var point=part.position;float nearest=float.PositiveInfinity;
                    // Pick an actual rendered vertex close to the camera-facing part of the wafer.
                    foreach(var v in vertices)
                    {
                        var w=mf.transform.TransformPoint(v); var delta=w-approach;
                        float error=Vector3.Cross(delta,aim).sqrMagnitude+.0001f*delta.sqrMagnitude;
                        if(error<nearest){nearest=error;point=w;}
                    }
                    var toHand=(approach-point).normalized;
                    right=near?point+toHand*.008f:approach;Frame(point,true,false,true);
                    if(p.Input.Pickups.Highlighted(false)!=part) continue;
                    target=part;index=Array.IndexOf(bag.biscuits,part);at=point;break;
                }
                Need(target,"No authored biscuit target at actual rendered surface, near="+near);
                Frame(at,true,true,true);Need(g.HasServing && bag.InPouch==3 && !target.gameObject.activeInHierarchy && bag.ConsumedMask==0,"visible identity lifts once");
                var partCollider=target.GetComponentInChildren<Collider>(true);
                var serving=Object.FindObjectsByType<ConsumableItem>(FindObjectsSortMode.None).Single(s=>s.IsHeld && s.Kind==GameItemKind.TravelRation);
                var sc=serving.GetComponentsInChildren<Collider>().Count(c=>c.enabled && !c.isTrigger && c.attachedRigidbody==serving.GetComponent<Rigidbody>());
                emit($"thin biscuit index={index} near={near}, rendered vertex to packed grip={(at-target.position).magnitude*1000:F2}mm, handToSurface={Vector3.Distance(right,at)*1000:F2}mm, packedPieceOwnCollider={(bool)partCollider}, servingSolidColliders={sc}, servingHeld={serving.IsHeld}");
                Frame(at,true,false,true);
                Need(!g.HasServing && bag.InPouch==4 && !serving.gameObject.activeInHierarchy && bag.ConsumedMask==0,"release restores unspent slot; no world item");
                emit("release restores the same physical display and deactivates serving; no independent grounded biscuit");
            }
        }
    }
}

