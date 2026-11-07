#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using NarutoVR.SealCombat.Rpg;
using UnityEngine;

namespace NarutoVR.SealCombat.Editor
{
    /// <summary>Runs in the isolated RPG Play session, with live component wiring and synthetic UI poses.</summary>
    public static class WristMenuPlayChecks
    {
        public static void Run(LinearRpgController rpg, Action<string> record)
        {
            void Require(bool ok,string text) { if(!ok) throw new InvalidOperationException("腕饰Play："+text); }
            var menu=rpg.wristMenu; var effects=rpg.skills.effects;
            Require(menu && menu.view,"场景必须接入腕饰菜单与视图");
            float mana=rpg.Player.Mana, shield=effects.ShieldPoints; int flying=effects.ActiveProjectileCount;
            var lab=rpg.skills.lab; var previous=lab.Frame;
            var property=lab.GetType().GetProperty("Frame");
            var late=typeof(LinearRpgController).GetMethod("LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic);
            double now=Time.realtimeSinceStartupAsDouble;
            WristMenuInput Sample(double time)
            {
                var f=WristMenuChecks.Sample(time);
                var head=rpg.locomotion.head;
                Quaternion rotation=Quaternion.LookRotation(RpgGestureState.HorizontalForward(head.forward,Vector3.forward));
                f.leftWrist=head.position+rotation*new Vector3(-.12f,-.22f,.38f);
                f.headPosition=head.position; f.headForward=rotation*Vector3.forward;
                f.rightTip=head.position+rotation*new Vector3(.4f,-.2f,.25f); f.source=null;
                return f;
            }
            try
            {
                property.SetValue(lab,null); menu.ResetForWorldChange(); menu.State.Reset(false);
                Require(menu.settings.activation==WristMenuActivation.TapWrist,"实际场景必须使用点腕模式");
                for(int i=0;i<20;i++)
                {
                    double t=now-.5+i*.025; var f=Sample(t); f.leftClosed=false; f.leftPointing=true;
                    f.rightTip=f.leftWrist+Vector3.right*(i<7 || i>=18 ? .30f : i<10 ? .18f : .035f);
                    var nav=new NavigationHandInput{leftValid=true,rightValid=true,leftWrist=f.leftWrist,rightWrist=f.leftWrist+Vector3.right*.12f};
                    rpg.UpdateWristUi(nav,f,false,t);
                }
                Require(menu.State.LauncherOpen,"抬左腕并主动点腕应打开实际场景入口");
                late.Invoke(rpg,null);
                Require(rpg.UiInputOwned && rpg.skills.GestureInputBlocked() && rpg.skills.VoiceInputBlocked() && rpg.skills.ExclusiveInputBlocked(),"主控必须统一阻断骨骼、语音和取消输入");
                Require(!rpg.DebugCast("fireball") && !rpg.DebugMove(true,Vector2.right),"菜单中调试发射与闪避也不得绕过仲裁");
                Require(Mathf.Abs(mana-rpg.Player.Mana)<.03f && effects.ShieldPoints==shield && effects.ActiveProjectileCount==flying,"UI仲裁不能扣费或清除已生效效果");
                record("实际RPG接线：腕饰占用阻断手势/语音/施法/闪避，保留护盾与飞行物计数");

                // Generate a real world-canvas poke, then lower only the left wrist.
                var target=menu.view.Targets.Single(t=>t.id=="seal").rect;
                double next=Time.realtimeSinceStartupAsDouble+.01;
                var input=Sample(next); input.rightTip=target.position-target.forward*.04f; menu.Tick(input,rpg,false,next);
                input=Sample(next+.05); input.rightTip=target.position-target.forward*.001f; menu.Tick(input,rpg,false,next+.05);
                Require(menu.PanelOpen,"物理按钮点按应打开实际教学卡");
                for(int i=0;i<4;i++) { double t=next+.1+i*.05; input=Sample(t); input.leftWrist.y=input.headPosition.y-.7f; menu.Tick(input,rpg,false,t); }
                Require(!menu.State.LauncherOpen && menu.PanelOpen,"放腕后入口关闭而结印卡保留");
                menu.ClosePanel();
                Require(effects.ShieldPoints==shield && effects.ActiveProjectileCount==flying,"关闭子面板不得调用ClearAll");
                record("实际UGUI矩形点按打开结印卡；放腕不销毁保留卡；关卡不清技能效果");
            }
            finally
            {
                property.SetValue(lab,previous);
                menu.ResetForWorldChange(); menu.State.Reset(false);
                // Restore the test's ordinary battle ownership through the real adapter.
                late.Invoke(rpg,null);
            }
        }
    }
}
#endif
