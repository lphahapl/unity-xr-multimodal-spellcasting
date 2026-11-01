#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using NarutoVR.SealCombat.Rpg;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
namespace NarutoVR.SealCombat.Editor
{
    public static class RayNavigationChecks
    {
        public static int Run()
        {
            if (Application.isPlaying) throw new InvalidOperationException("射线导航检查须在编辑模式运行");
            int count = 0;
            void Check(bool pass,string message) { if (!pass) throw new InvalidOperationException("射线导航："+message); count++; }
            var config = new RayNavigationSettings(); var timing = new RpgGestureSettings();
            var state = new RayNavigationState(); object a = new object(), b = new object();
            bool Feed(object target,double t,bool valid=true,bool release=false) => state.Process(target,valid,release,t,t,config,timing);
            bool fired = false;
            for(int i=0;i<=10;i++) fired |= Feed(a,i*.05);
            Check(fired && state.NeedsRelease,"静止指向应在约0.5秒内触发，不依赖指尖位移");
            for(int i=11;i<25;i++) Check(!Feed(b,i*.05),"持有同一次指向不得换目标连跳");
            Feed(null,1.25,false); Check(state.NeedsRelease,"失追不能重备");
            Feed(null,1.3,true,true); Feed(null,1.45,true,true); Check(!state.NeedsRelease,"主动移开持续足够时间重备");
            state.Reset(); for(int i=0;i<8;i++) Feed(a,i*.05);
            Feed(b,.4); Check(state.Progress==0 && !state.Active,"换目标必须从准备重新累计");
            for(int i=0;i<10;i++) state.Process(b,true,false,.4,.4+i*.01,config,timing);
            Check(state.Progress==0,"同一骨骼样本重复渲染不得累计");
            Feed(b,.7); Check(state.Progress==0,"样本间隔过大不得补传");
            state.Process(b,true,false,.7,1,config,timing); Check(!state.Active && state.Progress==0,"样本过期清空");
            Check(!NavigationTargetScoring.TryEvaluateRay(Vector3.forward*2,Vector3.up,Vector3.up,.35f,out _),"乱指不能靠距离或头向选点");
            Check(NavigationTargetScoring.TryEvaluateRay(Vector3.forward*2,Vector3.up,new Vector3(0,-1,2),.35f,out var score) && score.handRay>.999f,"正确指向命中");
            count += Controller();
            return count;
        }
        static int Controller()
        {
            int count=0; void Check(bool pass,string message) { if(!pass) throw new InvalidOperationException("射线控制器："+message); count++; }
            Scene previous=SceneManager.GetActiveScene(), temporary=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            SceneManager.SetActiveScene(temporary);
            var world=new GameObject("射线导航回归"); var nodes=new List<NavigationNode>();
            Vector3 origin=new Vector3(8500,0,8500); const int layer=30;
            try
            {
                var ground=new GameObject("地面"); ground.transform.SetParent(world.transform); ground.transform.position=origin+Vector3.down*.1f;
                ground.layer=layer; ground.AddComponent<BoxCollider>().size=new Vector3(15,.2f,15);
                var rig=new GameObject("Rig"); rig.transform.SetParent(world.transform); rig.transform.position=origin;
                var head=new GameObject("头部"); head.transform.SetParent(rig.transform,false); head.transform.localPosition=Vector3.up*1.7f;
                var c=world.AddComponent<RpgLocomotionController>(); c.rigRoot=rig.transform; c.head=head.transform; c.useRayNavigation=true;
                c.floorLayers=c.obstacleLayers=1<<layer;
                NavigationNode Node(string id,Vector3 point) { var go=new GameObject(id); go.transform.SetParent(world.transform); go.transform.position=point;
                    var n=go.AddComponent<NavigationNode>(); n.nodeId=id; NavigationNodeFixture.SetRegistered(n,true); nodes.Add(n); return n; }
                var start=Node("起点",origin); var target=Node("目标",origin+Vector3.forward*2); var side=Node("侧点",origin+Vector3.right*2);
                start.neighbors.Add(target); start.neighbors.Add(side); target.neighbors.Add(side);
                Physics.SyncTransforms();
                NavigationHandInput Frame(double t) { Vector3 hand=origin+new Vector3(-.3f,1.2f,.4f); return new NavigationHandInput {
                    leftValid=true,rightValid=true,leftPointing=true,leftObserved=t,rightObserved=t,leftIndex=hand,leftWrist=hand,
                    rightWrist=origin+new Vector3(.4f,1.2f,.4f),leftRayValid=true,leftRayOrigin=hand,leftRayDirection=target.floorPosition-hand }; }
                void Reset() { c.ResetInput(); rig.transform.position=origin; Physics.SyncTransforms(); }
                void Samples(Action<double> action) { for(int i=0;i<=14;i++) action(i*.05); }
                Samples(t=>c.ProcessInput(Frame(t),false,false,t));
                Check(Vector3.Distance(rig.transform.position,target.floorPosition)<.01f,"不移动左食指即可驻留传送");
                Reset(); Samples(t=> { var f=Frame(t); f.leftRayDirection=Vector3.up; c.ProcessInput(f,false,false,t); });
                Check(rig.transform.position==origin && c.CandidateNode==null,"未命中射线不得选择");
                Reset(); Samples(t=>c.ProcessInput(Frame(t),false,true,t));
                Check(rig.transform.position==origin && !c.NavigationActive,"界面占用时不能移动");
                Reset(); Samples(t=> { var f=Frame(t); f.rightWrist=f.leftWrist+Vector3.right*.1f; c.ProcessInput(f,false,false,t); });
                Check(rig.transform.position==origin && c.SealProtectionActive,"结印保护优先");
                Reset(); Samples(t=> { var f=Frame(t); f.bothHandsLow=true; c.ProcessInput(f,false,false,t); });
                Check(rig.transform.position==origin,"双手低位不会传送");
                Reset(); Samples(t=> { var f=Frame(t); f.leftObserved=0; c.ProcessInput(f,false,false,t); });
                Check(rig.transform.position==origin,"陈旧样本不累计");
                Reset(); int dodges=0; c.Dodged += _=>dodges++;
                Samples(t=> { var f=Frame(t); f.rightOpen=true; f.rightMotionOwnedBySpell=true;
                    f.rightWrist+=Vector3.forward*(float)t; c.ProcessInput(f,true,false,t); });
                Check(dodges==0,"持球右手运动不能误闪避，左手仍可导航");
                Reset(); var wall=new GameObject("阻挡墙"); wall.transform.SetParent(world.transform); wall.layer=layer;
                wall.transform.position=origin+new Vector3(0,1,1); wall.AddComponent<BoxCollider>().size=new Vector3(2,2,.1f); Physics.SyncTransforms();
                Samples(t=>c.ProcessInput(Frame(t),false,false,t)); Check(rig.transform.position==origin,"墙后节点禁止传送");
                Object.DestroyImmediate(wall); target.transform.position+=Vector3.up; Physics.SyncTransforms(); Reset();
                Samples(t=>c.ProcessInput(Frame(t),false,false,t)); Check(rig.transform.position==origin,"悬空节点禁止传送");
            }
            finally { foreach(var n in nodes) if(n) NavigationNodeFixture.SetRegistered(n,false); Object.DestroyImmediate(world);
                SceneManager.SetActiveScene(previous); EditorSceneManager.CloseScene(temporary,true); }
            return count;
        }
    }
}
#endif
