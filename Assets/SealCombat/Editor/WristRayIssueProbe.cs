#if UNITY_EDITOR
using System;
using System.Linq;
using NarutoVR.SealCombat.Rpg;
using UnityEngine;

namespace NarutoVR.SealCombat.Editor
{
    public static class WristRayIssueProbe
    {
        public static int Run()
        {
            var root=new GameObject("腕部射线问题复现") { hideFlags=HideFlags.HideAndDontSave }; root.SetActive(false);
            try
            {
                var view=root.AddComponent<WristMenuView>(); view.audioFeedback=false;
                var menu=root.AddComponent<WristMenuController>(); menu.view=view;
                menu.settings.activation=WristMenuActivation.RaiseFist; // Isolate ray regression from entry gestures.
                var rpg=root.AddComponent<LinearRpgController>(); var movement=root.AddComponent<RpgLocomotionController>(); rpg.locomotion=movement;
                movement.rigRoot=root.transform; var head=new GameObject("测试头部"); head.transform.SetParent(root.transform); head.transform.position=new Vector3(0,1.7f,0); movement.head=head.transform;
                root.SetActive(true); view.EnsureBuilt();
                for(int i=0;i<10;i++) menu.Tick(WristMenuChecks.Sample(20+i*.05),rpg,false,20+i*.05);
                var target=view.Targets.Single(x=>x.id=="seal").rect;
                for(int i=0;i<3;i++)
                {
                    double t=20.5+i*.04; var f=WristMenuChecks.Sample(t);
                    f.rightPointing=true; f.rayValid=true; f.rayOrigin=target.position-target.forward*.5f; f.rayDirection=target.forward;
                    menu.Tick(f,rpg,false,t);
                }
                // Real pinch first bends the index away while thumb/index remain above the open threshold.
                var transition=WristMenuChecks.Sample(20.60); transition.rightPointing=false; transition.pinchDistance=.05f;
                transition.rayValid=true; transition.rayOrigin=target.position-target.forward*.5f;
                transition.rayDirection=Quaternion.AngleAxis(35,Vector3.up)*target.forward;
                menu.Tick(transition,rpg,false,20.60);
                var pinch=WristMenuChecks.Sample(20.62); pinch.rightPointing=false; pinch.pinchDistance=.02f;
                pinch.rayValid=true; pinch.rayOrigin=target.position-target.forward*.5f;
                pinch.rayDirection=Quaternion.AngleAxis(35,Vector3.up)*target.forward;
                menu.Tick(pinch,rpg,false,20.62);
                // Pinch bends the index ray away, but should select the visibly hovered button ON pinch down.
                if(!menu.PanelOpen) throw new InvalidOperationException("复现：按钮已高亮，轻捏使食指射线偏35度后没有确认；PanelOpen=False");
                int checks=1;
                if(LinearRpgController.PointerPoseNearHand(new Pose(Vector3.zero,Quaternion.identity),new Vector3(0,1.7f,0))) throw new Exception("原点残留Aim不得覆盖真人手部射线"); checks++;
                if(!LinearRpgController.PointerPoseNearHand(new Pose(new Vector3(0,1.7f,.05f),Quaternion.identity),new Vector3(0,1.7f,0))) throw new Exception("有效手边Aim被错误拒绝"); checks++;

                var miss=WristMenuChecks.Sample(20.64); miss.rightPointing=false; miss.rayValid=miss.rayAnchorValid=true;
                miss.rayOrigin=target.position-target.forward*.5f; miss.rayDirection=Vector3.right;
                menu.Tick(miss,rpg,false,20.64);
                if(!menu.PointerVisible) throw new Exception("教学面板开启时未命中按钮的有效搜索射线不可见"); checks++;

                Vector3 before=target.position;
                var moved=WristMenuChecks.Sample(20.66); moved.leftWrist+=Vector3.right*.04f;
                menu.Tick(moved,rpg,false,20.66);
                if(Vector3.Distance(before,target.position)<.02f) throw new Exception("腕部入口没有跟随手腕"); checks++;
                double time=20.7;
                void Click(string id) {
                    var rect=view.Targets.Single(x=>x.id==id).rect;
                    for(int n=0;n<4;n++) {
                        var f=WristMenuChecks.Sample(time); f.rightPointing=f.rayValid=f.rayAnchorValid=true;
                        f.rayOrigin=rect.position-rect.forward*.5f; f.rayDirection=rect.forward;
                        f.pinchDistance=n==3?.02f:.06f; menu.Tick(f,rpg,false,time); time+=.025;
                    }
                }
                Click("follow");
                if(!menu.FollowPlayer) throw new Exception("跟随按钮未生效"); checks++;
                var next=view.Targets.Single(x=>x.id=="next").rect;
                before=next.position; root.transform.position+=Vector3.right*.04f;
                var follow=WristMenuChecks.Sample(time); menu.Tick(follow,rpg,false,time); time+=.025;
                if(Mathf.Abs(next.position.x-before.x-.04f)>.005f) throw new Exception("面板未随玩家坐标移动"); checks++;
                Click("drag");
                var drag=view.Targets.Single(x=>x.id=="drag").rect;
                before=next.position;
                var hold=WristMenuChecks.Sample(time); hold.rayValid=hold.rayAnchorValid=true; hold.pinchDistance=.02f;
                hold.rayOrigin=drag.position-drag.forward*.5f+Vector3.right*.08f;
                hold.rayDirection=drag.forward;
                menu.Tick(hold,rpg,false,time); time+=.025;
                if(next.position.x-before.x<.06f) throw new Exception("捏住拖动条未移动面板"); checks++;
                var release=WristMenuChecks.Sample(time); menu.Tick(release,rpg,false,time); time+=.025;
                // At arm's reach the fingertip can enter the poke volume before pinching the handle.
                before=next.position;
                for(int i=0;i<4;i++) {
                    var f=WristMenuChecks.Sample(time); f.rayValid=f.rayAnchorValid=true;
                    f.rayOrigin=drag.position-drag.forward*.15f; f.rayDirection=drag.forward;
                    f.rightTip=drag.position-drag.forward*.01f;
                    f.pinchDistance=i==3?.02f:.06f;
                    menu.Tick(f,rpg,false,time); time+=.025;
                }
                var nearHold=WristMenuChecks.Sample(time); nearHold.rayValid=nearHold.rayAnchorValid=true;
                nearHold.pinchDistance=.02f; nearHold.rayOrigin=drag.position-drag.forward*.15f+Vector3.right*.08f;
                nearHold.rayDirection=drag.forward;
                menu.Tick(nearHold,rpg,false,time); time+=.025;
                if(next.position.x-before.x<.06f) throw new Exception("手指贴近拖动条时轻捏被近按吞掉，面板未移动"); checks++;
                before=next.position;
                var spatial=nearHold; spatial.leftObserved=spatial.rightObserved=time;
                spatial.rayOrigin+=new Vector3(0,.08f,.12f);
                spatial.rayDirection=Quaternion.AngleAxis(12,Vector3.up)*nearHold.rayDirection;
                menu.Tick(spatial,rpg,false,time); time+=.025;
                if(Vector3.Distance(next.position,before)<.08f) throw new Exception("拖动应支持深度、上下平移和射线转向"); checks++;
                var panel=view.OwnedRoot.GetComponentsInChildren<RectTransform>().First(x=>x.name=="独立术式面板");
                if(Vector3.Dot(panel.forward,(panel.position-spatial.headPosition).normalized)<.999f) throw new Exception("拖动中面板未朝向玩家"); checks++;
                menu.Tick(WristMenuChecks.Sample(time),rpg,false,time); time+=.025;
                Click("follow"); // Previously enabled: freeze the pose when disabled.
                Quaternion fixedRotation=panel.rotation; Vector3 fixedPosition=panel.position;
                var shifted=WristMenuChecks.Sample(time); shifted.headPosition+=Vector3.right*.2f;
                menu.Tick(shifted,rpg,false,time); time+=.025;
                if(Vector3.Distance(panel.position,fixedPosition)>.001f || Quaternion.Angle(panel.rotation,fixedRotation)>.01f) throw new Exception("固定模式松手后不应跟随头部改变位置或旋转"); checks++;
                Click("follow"); shifted.leftObserved=shifted.rightObserved=time;
                menu.Tick(shifted,rpg,false,time); time+=.025;
                if(Vector3.Dot(panel.forward,(panel.position-shifted.headPosition).normalized)<.999f) throw new Exception("跟随模式应持续朝向玩家"); checks++;
                Click("close"); if(menu.PanelOpen) throw new Exception("拖动松开后面板无法关闭"); checks++;
                return checks;
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
#endif
