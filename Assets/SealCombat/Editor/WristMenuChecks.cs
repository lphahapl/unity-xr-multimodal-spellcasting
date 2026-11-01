#if UNITY_EDITOR
using System;
using System.Linq;
using NarutoVR.SealCombat.Rpg;
using UnityEngine;

namespace NarutoVR.SealCombat.Editor
{
    public static class WristMenuChecks
    {
        public static int Run()
        {
            int checks = 0;
            void Check(bool ok,string message) { if (!ok) throw new Exception("腕饰检查："+message); checks++; }
            var s = new WristMenuSettings { activation=WristMenuActivation.RaiseFist }; // Legacy comparison path.
            WristMenuInput f = Sample(0);
            var state = new WristMenuState();
            for (int i=0;i<5;i++) state.Tick(f,false,false,i*.025,s);
            Check(!state.LauncherOpen,"重复快照不能累计开启时间");
            state.Reset(false);
            for (int i=1;i<=8;i++) { f=Sample(i*.05); state.Tick(f,false,false,i*.05,s); }
            Check(state.LauncherOpen && state.OwnsInput,"稳定握拳抬腕应召出且获取输入");
            f=Sample(.45); f.leftClosed=false; state.Tick(f,false,false,.45,s);
            Check(state.LauncherOpen,"打开后不要求持续握拳");
            f=Sample(.5); f.leftValid=false; state.Tick(f,false,false,.5,s);
            Check(state.LauncherOpen && !state.LeftLow,"丢追踪不能当作放腕，入口保留短暂宽限");
            state.Tick(f,false,false,.9,s);
            Check(!state.LauncherOpen && state.OwnsInput,"超时隐藏入口，等待中立再交回战斗");
            for (int i=0;i<10;i++) { f=Sample(1+i*.05); state.Tick(f,false,false,1+i*.05,s); }
            Check(!state.LauncherOpen,"重获追踪但仍旧握拳不能自动弹出");
            for (int i=0;i<8;i++) { f=Sample(1.6+i*.05); f.leftClosed=false; state.Tick(f,false,false,1.6+i*.05,s); }
            Check(!state.OwnsInput,"双方有效中立后应归还输入");
            for (int i=0;i<9;i++) { f=Sample(2.1+i*.05); state.Tick(f,true,false,2.1+i*.05,s); }
            Check(!state.LauncherOpen,"已有持球或其他动作时不得唤起");
            for (int i=0;i<9;i++) { f=Sample(2.7+i*.05); state.Tick(f,false,false,2.7+i*.05,s); }
            Check(!state.LauncherOpen,"被拒绝的握拳不排队补弹");
            state.Reset(false);
            for (int i=0;i<9;i++) { f=Sample(4+i*.05); state.Tick(f,false,false,4+i*.05,s); }
            for(int i=0;i<4;i++) { double t=4.5+i*.05; f=Sample(t); f.leftWrist.y=.9f; state.Tick(f,false,false,t,s); }
            Check(!state.LauncherOpen && state.LeftLow,"有效放腕收入口");
            state.Tick(f,false,false,3,s);
            Check(!state.LauncherOpen && state.OwnsInput,"时钟回滚丢弃旧点击并重新建立中立");

            var pointer = new WristPointerState();
            Check(pointer.Step("next",false,0,true,false,0,.18f)==null && !pointer.Pressed,"捏住进入目标不得起按");
            pointer.Step("next",false,0,false,true,.05,.18f);
            pointer.Step("next",false,0,true,false,.1,.18f);
            Check(pointer.Step("next",false,0,false,true,.15,.18f)=="next","射线捏合后同目标松开确认");
            Check(pointer.Step("next",false,0,false,true,.15,.18f)==null,"重复观测不得重复确认");
            pointer.Step("next",false,0,true,false,.2,.18f);
            Check(pointer.Step("close",false,0,false,true,.25,.18f)==null,"按下移出原按钮不得确认新按钮");
            pointer.Step("close",false,0,true,false,.3,.18f);
            Check(pointer.Step("close",false,0,false,true,.8,.18f)==null,"观测中断使旧按下作废");
            pointer.Reset(); pointer.Step("seal",true,-.04f,false,true,1,.18f);
            Check(pointer.Step("seal",true,-.002f,false,true,1.05,.18f)=="seal","食指从正面靠近再穿过按压面可确认");
            Check(pointer.Step("seal",true,0,false,true,1.1,.18f)==null,"保持按压不连点");

            var root = new GameObject("腕饰编辑检查") { hideFlags=HideFlags.HideAndDontSave };
            root.SetActive(false);
            try
            {
                var view = root.AddComponent<WristMenuView>(); var menu = root.AddComponent<WristMenuController>(); menu.view=view;
                menu.settings.activation=WristMenuActivation.RaiseFist;
                var rpg = root.AddComponent<LinearRpgController>(); var locomotion=root.AddComponent<RpgLocomotionController>();
                rpg.locomotion=locomotion; locomotion.rigRoot=root.transform;
                var head=new GameObject("头部"); head.transform.SetParent(root.transform); head.transform.position=new Vector3(0,1.7f,0); locomotion.head=head.transform;
                root.SetActive(true); view.EnsureBuilt(); view.audioFeedback=false;
                int targetCount=view.Targets.Count; view.EnsureBuilt();
                Check(targetCount==9 && view.Targets.Count==9,"视图重复构建不能复制九个目标");
                for (int i=0;i<10;i++) menu.Tick(Sample(10+i*.05),rpg,false,10+i*.05);
                Check(menu.State.LauncherOpen,"完整控制器可通过输入DTO开启入口");
                RectTransform seal=view.Targets.Single(t=>t.id=="seal").rect;
                f=Sample(10.5); f.rightTip=seal.position-seal.forward*.04f; menu.Tick(f,rpg,false,10.5);
                f=Sample(10.55); f.rightTip=seal.position-seal.forward*.001f; menu.Tick(f,rpg,false,10.55);
                Check(menu.PanelOpen && menu.PanelKind==WristPanelKind.Seal,"真实矩形近按应打开结印卡");
                var panel=view.Targets.Single(t=>t.id=="next").rect;
                Vector3 panelBefore=panel.position;
                for(int i=0;i<4;i++) { double t=10.6+i*.05; f=Sample(t); f.leftWrist.y=.9f; menu.Tick(f,rpg,false,t); }
                Check(menu.PanelOpen && !menu.State.LauncherOpen && panel.gameObject.activeInHierarchy,"放腕只关闭入口，默认结印卡继续可见");
                Check(panel.position==panelBefore,"子面板不得随左手移动");
                for(int i=0;i<6;i++) { double t=10.8+i*.025; f=Sample(t); f.leftWrist.y=.9f; f.leftClosed=false; menu.Tick(f,rpg,false,t); }
                Check(!menu.OwnsInput && menu.PanelOpen,"速查卡闲置不能一直封锁战斗");
                for(int i=0;i<3;i++)
                {
                    double t=11+i*.05; f=Sample(t); f.leftWrist.y=.9f;
                    f.rightPointing=f.rayValid=true; f.rayOrigin=panel.position-panel.forward*.5f; f.rayDirection=panel.forward;
                    f.pinchDistance=i==1?.015f:.06f; menu.Tick(f,rpg,false,t);
                }
                Check(menu.Page==1 && menu.PanelOpen,"保留面板可射线捏合翻页");
                int beforeLostPinch=menu.ClickCount;
                for(int i=0;i<4;i++)
                {
                    double t=11.12+i*.01; f=Sample(t); f.leftWrist.y=.9f; f.rightPointing=f.rayValid=true;
                    f.rayOrigin=panel.position-panel.forward*.5f; f.rayDirection=panel.forward;
                    f.pinchDistance=i==1?.015f:.06f; f.pinchValid=i!=2; menu.Tick(f,rpg,false,t);
                }
                Check(menu.ClickCount==beforeLostPinch,"捏合关节短暂失效后恢复松开不得补确认");
                var blocker=GameObject.CreatePrimitive(PrimitiveType.Cube);
                blocker.hideFlags=HideFlags.HideAndDontSave;
                try
                {
                    blocker.transform.SetPositionAndRotation(panel.position-panel.forward*.25f,panel.rotation);
                    blocker.transform.localScale=new Vector3(.2f,.2f,.02f); Physics.SyncTransforms();
                    for(int i=0;i<3;i++)
                    {
                        double t=11.16+i*.01; f=Sample(t); f.leftWrist.y=.9f; f.rightPointing=f.rayValid=true;
                        f.rayOrigin=panel.position-panel.forward*.5f; f.rayDirection=panel.forward;
                        f.pinchDistance=i==1?.015f:.06f; menu.Tick(f,rpg,false,t);
                    }
                    Check(menu.ClickCount==beforeLostPinch,"世界物体遮挡时不能穿墙点按面板");
                }
                finally { UnityEngine.Object.DestroyImmediate(blocker); Physics.SyncTransforms(); }
                menu.sealPolicy.allowRay=false; int clicks=menu.ClickCount;
                for(int i=0;i<3;i++)
                { double t=11.2+i*.05; f=Sample(t); f.leftWrist.y=.9f; f.rightPointing=f.rayValid=true; f.rayOrigin=panel.position-panel.forward*.5f; f.rayDirection=panel.forward; f.pinchDistance=i==1?.015f:.06f; menu.Tick(f,rpg,false,t); }
                Check(menu.ClickCount==clicks,"面板可独立禁止射线交互");
                menu.sealPolicy.closeWithWrist=true; f=Sample(11.4); f.leftWrist.y=.9f; menu.Tick(f,rpg,false,11.4);
                Check(!menu.PanelOpen,"同一子面板切换为随腕策略后应放腕关闭");
                Check(WristMenuController.TryRay(seal,seal.position-seal.forward*.4f,seal.forward,1,out _,out float distance) && Mathf.Abs(distance-.4f)<.001f,"射线正面命中距离准确");
                Check(!WristMenuController.TryRay(seal,seal.position+seal.forward*.4f,-seal.forward,1,out _,out _),"背面射线不能穿透点击");
                menu.ResetForWorldChange();
                Check(!menu.PanelOpen && !menu.State.LauncherOpen,"世界坐标改变关闭旧界面并清空点击历史");
                var materialCount=Resources.FindObjectsOfTypeAll<Material>().Count(m=>m.name.StartsWith("腕饰 ·"));
                Check(materialCount>=3,"程序化腕饰材质已建立");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
            return checks;
        }
        public static WristMenuInput Sample(double time) => new()
        {
            leftValid=true,rightValid=true,leftClosed=true,wristFacing=true,leftObserved=time,rightObserved=time,
            leftWrist=new Vector3(-.12f,1.48f,.38f),leftRotation=Quaternion.identity,
            rightTip=new Vector3(.4f,1.2f,.3f),headPosition=new Vector3(0,1.7f,0),headForward=Vector3.forward,
            pinchValid=true,pinchDistance=.06f,source="synthetic-wrist-regression"
        };
    }
}
#endif
