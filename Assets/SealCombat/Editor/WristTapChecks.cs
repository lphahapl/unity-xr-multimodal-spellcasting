#if UNITY_EDITOR
using System;
using System.Linq;
using NarutoVR.SealCombat.Rpg;
using UnityEngine;
namespace NarutoVR.SealCombat.Editor
{
    public static class WristTapChecks
    {
        public static int Run()
        {
            int n=0; void Check(bool v,string text) { if(!v) throw new Exception("点腕回归："+text); n++; }
            var s=new WristMenuSettings(); var tap=new WristTapSwitch(); var state=new WristMenuState();
            void Tick(double t,float distance,bool blocked=false,bool valid=true)
            {
                var f=WristMenuChecks.Sample(t); f.leftClosed=false; f.leftPointing=true;
                f.rightTip=f.leftWrist+Vector3.right*distance; f.rightValid=valid;
                tap.Step(f,f.leftWrist,blocked,t,s); state.Tick(f,blocked,false,t,s,tap.Toggle);
            }
            for(int i=0;i<20;i++) Tick(i*.02,.3f);
            Check(!state.LauncherOpen && !state.OwnsInput,"左食指或拳头抬腕不能自动开启或抢输入");
            Tick(.4,.2f); Check(tap.Approaching && !state.OwnsInput && !state.LauncherOpen,"靠近仅提供提示，实际触腕确认之前不能抢输入");
            Tick(.42,.04f); Tick(.46,.04f);
            Check(state.LauncherOpen,"不握拳也能点腕打开");
            for(int i=0;i<8;i++) Tick(.48+i*.02,.04f);
            Check(state.LauncherOpen && !tap.Toggle,"持续接触不会反复开关");
            Tick(.66,.14f); Tick(.68,.04f); Tick(.72,.04f);
            Check(!state.LauncherOpen,"离开后第二次点腕关闭");
            for(int i=0;i<10;i++) Tick(.74+i*.03,.3f);
            Check(!state.OwnsInput,"右指离开后自动交回战斗，不依赖左右拳形");
            tap.Reset(); state.Reset(false);
            for(int i=0;i<20;i++) Tick(2+i*.02,.02f);
            Check(!state.LauncherOpen,"起始就在腕饰内，不算一次主动点按");
            tap.Reset(); state.Reset(false);
            for(int i=0;i<15;i++) Tick(3+i*.02,i<8?.25f:.03f,true);
            Check(!state.LauncherOpen,"持球/死亡/暂停等硬阻断不让腕饰抢占");
            tap.Reset(); state.Reset(false);
            for(int i=0;i<7;i++) Tick(4+i*.02,.25f);
            Tick(4.14,.04f); Tick(4.16,.04f,valid:false); Tick(4.18,.04f); Tick(4.22,.04f);
            Check(!state.LauncherOpen,"接触中丢追踪不能补开");
            tap.Reset();
            for(int i=0;i<7;i++) Tick(5+i*.02,.25f);
            var repeated=WristMenuChecks.Sample(5.14); repeated.rightTip=repeated.leftWrist+Vector3.right*.04f;
            tap.Step(repeated,repeated.leftWrist,false,5.14,s);
            tap.Step(repeated,repeated.leftWrist,false,5.20,s);
            Check(!tap.Toggle,"重复快照不能推进接触确认");

            // A short natural lowering must close even above the old absolute 66cm threshold.
            var lowerState=new WristMenuState(); var opened=WristMenuChecks.Sample(7);
            lowerState.Tick(opened,false,false,7,s,true);
            for(int i=1;i<=6;i++) { var f=WristMenuChecks.Sample(7+i*.04); f.leftWrist.y-=.23f; lowerState.Tick(f,false,false,7+i*.04,s); }
            Check(!lowerState.LauncherOpen,"相对打开高度放腕23厘米，应关闭主入口");
            lowerState.Reset(false);
            lowerState.Tick(WristMenuChecks.Sample(7.5),false,false,7.5,s,true);
            var slowLower=WristMenuChecks.Sample(7.8); slowLower.leftWrist.y-=.23f;
            lowerState.Tick(slowLower,false,false,7.8,s);
            Check(!lowerState.LauncherOpen,"低帧率后的新鲜放腕样本应收起入口，不能无限重置关闭计时");
            var nearTap=new WristTapSwitch(); var nearState=new WristMenuState();
            for(int i=0;i<20;i++) {
                double t=8+i*.02; var f=WristMenuChecks.Sample(t);
                f.leftWrist=f.headPosition+new Vector3(0,-.1f,-.08f);
                f.rightTip=f.leftWrist+Vector3.right*(i<8?.3f:.03f);
                nearTap.Step(f,f.leftWrist,false,t,s); nearState.Tick(f,false,false,t,s,nearTap.Toggle);
            }
            Check(nearState.LauncherOpen,"俯身看腕时靠近头部的显式点按不应被前向锥体拦截");
            var root=new GameObject("点腕仲裁回归"){hideFlags=HideFlags.HideAndDontSave}; root.SetActive(false);
            var recipe=ScriptableObject.CreateInstance<SkillDefinition>();
            try
            {
                var view=root.AddComponent<WristMenuView>(); view.audioFeedback=false;
                var menu=root.AddComponent<WristMenuController>(); menu.view=view;
                var rpg=root.AddComponent<LinearRpgController>(); var movement=root.AddComponent<RpgLocomotionController>();
                rpg.locomotion=movement; rpg.wristMenu=menu; movement.rigRoot=root.transform;
                rpg.skills=root.AddComponent<SkillTestController>(); rpg.skills.InputClock=()=>10.5;
                recipe.skillId="wrist.combo";recipe.displayName="腕饰回归";recipe.phrase="测试术";recipe.sealName="寅";recipe.triggerMode=SkillTriggerMode.VoiceAndGesture;
                var head=new GameObject("测试头"); head.transform.SetParent(root.transform); head.transform.position=new Vector3(0,1.7f,0); movement.head=head.transform;
                root.SetActive(true);
                var matcher=new SkillMatchEngine(new[]{recipe}); matcher.Tick(9.6);matcher.Voice(recipe.phrase,9.6,9.6);
                typeof(SkillTestController).GetProperty(nameof(SkillTestController.Engine)).SetValue(rpg.skills,matcher);
                long epoch=matcher.Epoch;
                Check(matcher.PendingSkillId==recipe.skillId,"前提：已收到有效口令，正在等待印");
                var start=WristMenuChecks.Sample(9);
                for(int i=0;i<15;i++)
                {
                    double t=9+i*.04;
                    movement.ProcessInput(new NavigationHandInput {leftValid=true,rightValid=true,leftObserved=t,rightObserved=t,
                        leftPointing=true,leftIndex=start.leftWrist,leftWrist=start.leftWrist,rightWrist=start.leftWrist+Vector3.right*.5f},false,false,t);
                }
                Check(movement.NavigationActive,"前提：未提交的导航已准备");
                Vector3 rigBefore=root.transform.position;
                for(int i=0;i<15;i++)
                {
                    double t=10+i*.02; var f=WristMenuChecks.Sample(t); f.leftClosed=false; f.leftPointing=true;
                    f.rightTip=f.leftWrist+Vector3.right*(i<7?.3f:i<9?.18f:.035f);
                    // Both wrists are close: the normal seal proximity guard must not prevent deliberate contact.
                    var nav=new NavigationHandInput { leftValid=true,rightValid=true,leftWrist=f.leftWrist,rightWrist=f.leftWrist+Vector3.right*.12f };
                    rpg.UpdateWristUi(nav,f,false,t);
                    if(i==8)Check(!menu.OwnsInput && menu.OwnershipReason.Contains("靠近") && movement.NavigationActive &&
                        matcher.PendingSkillId==recipe.skillId && matcher.Epoch==epoch,"真实仲裁：点腕靠近阶段必须保留导航与待施法口令，不得触发取消屏障");
                }
                Check(menu.State.LauncherOpen && menu.OwnsInput,"通过实际RPG仲裁：点腕可越过双腕距离的结印启发式");
                Check(!movement.NavigationActive && movement.CandidateNode==null && root.transform.position==rigBefore && matcher.Epoch==epoch+1 &&
                    string.IsNullOrEmpty(matcher.PendingSkillId),"点腕确认后仅一次接管输入、清除待施法与导航驻留，玩家不移动");
                menu.Tick(WristMenuChecks.Sample(10.32),rpg,true,10.32);
                Check(!menu.State.LauncherOpen,"硬阻断及时关闭入口");
            }
            finally { UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(recipe); }

            var ray=new WristRayPinchState();
            WristMenuInput R(double t,float distance) { var f=WristMenuChecks.Sample(t); f.rayAnchorValid=true; f.rayOrigin=Vector3.zero; f.pinchDistance=distance; return f; }
            Check(ray.Step("next",Vector3.forward,R(20,.02f),20,s)==null,"捏住进入不能点击");
            ray.Step("next",Vector3.forward,R(20.02,.06f),20.02,s);
            Check(ray.Step(null,default,R(20.08,.028f),20.08,s)=="next","轻捏弯指丢射线仍确认刚高亮的按钮");
            ray.ClearTarget();
            Check(ray.Step("close",Vector3.forward,R(20.10,.02f),20.10,s)==null,"翻页后保持捏合不能点新按钮");
            ray.Step("close",Vector3.forward,R(20.12,.06f),20.12,s);
            Check(ray.Step("close",Vector3.forward,R(20.18,.02f),20.18,s)=="close","松开重备后可再次确认");
            ray.Reset(); ray.Step("next",Vector3.forward,R(21,.06f),21,s);
            var fist=R(21.06,.02f); fist.rightClosed=true;
            Check(ray.Step("next",Vector3.forward,fist,21.06,s)==null,"完整握拳不当作菜单轻捏");
            ray.Reset(); ray.Step("next",Vector3.forward,R(22,.06f),22,s);
            var moved=R(22.06,.02f); moved.rayOrigin=Vector3.right*.2f;
            Check(ray.Step("next",Vector3.forward,moved,22.06,s)==null,"手掌移离目标不能使用旧高亮");
            ray.Reset(); ray.Step("next",Vector3.forward,R(23,.06f),23,s);
            Check(ray.Step(null,default,R(23.15,.02f),23.15,s)==null,"过期高亮不能点击");
            return n;
        }
    }
}
#endif
