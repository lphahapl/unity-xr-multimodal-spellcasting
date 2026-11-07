#if UNITY_EDITOR
using System;
using NarutoVR.SealCombat.Rpg;
using UnityEngine;

namespace NarutoVR.SealCombat.Editor
{
    public static class WristMenuEaseChecks
    {
        public static int Run()
        {
            int count=0;
            void Check(bool ok,string why) { if(!ok) throw new Exception("抬腕易用性："+why); count++; }
            var settings=new WristMenuSettings { activation=WristMenuActivation.RaiseFist }; var state=new WristMenuState();
            WristMenuInput f;
            void Tick(double t, bool closed=true, bool low=false, bool right=true)
            {
                f=WristMenuChecks.Sample(t); f.leftClosed=closed; f.rightValid=right; f.rightOpen=true; f.wristFacing=false;
                f.leftWrist=new Vector3(-.15f,low?.9f:1.22f,.45f);
                state.Tick(f,false,false,t,settings);
            }
            state.Tick(default,false,false,0,settings);
            for(int i=0;i<8;i++) Tick(.05+i*.04,right:false);
            Check(state.LauncherOpen,"启动时直接以拳进入视野，胸前48厘米、侧腕、无右手也可打开");
            Tick(.4,low:true,right:false);
            Check(state.LauncherOpen && !state.LeftLow,"一帧低腕不能收起入口");
            // Identical hand sample with advancing render/head state cannot complete low confirmation.
            for(int i=0;i<3;i++) state.Tick(f,false,false,.42+i*.02,settings);
            Check(state.LauncherOpen && !state.LeftLow,"重复低腕快照不推进关闭计时");
            Tick(.48,low:true,right:false); Tick(.53,low:true,right:false); Tick(.58,low:true,right:false); Tick(.63,low:true,right:false);
            Check(!state.LauncherOpen && !state.OwnsInput,"有效放腕后无需右手即可归还战斗");
            for(int i=0;i<8;i++) Tick(.68+i*.04,right:false);
            Check(state.LauncherOpen,"保持左拳放下再抬起，无需右手或额外松拳即可再开");

            // Raise again before combat handoff has completed: no single intervening gameplay frame.
            Tick(1.02,low:true); Tick(1.06,low:true); Tick(1.11,low:true); Tick(1.15,low:true);
            Check(!state.LauncherOpen && state.OwnsInput,"放腕确认后保留短暂交接保护");
            bool continuouslyOwned=true;
            for(int i=0;i<8;i++) { Tick(1.19+i*.04); continuouslyOwned &= state.OwnsInput; }
            Check(state.LauncherOpen && continuouslyOwned,"交接中重新抬腕连续保有UI输入，不要求右手收拢");

            // One-frame hand-shape noise pauses, but contributes no positive hold evidence.
            state.Reset(false);
            Tick(2); Tick(2.05); Tick(2.10);
            Check(!state.OwnsInput,"旧版抬腕保持未完成时也不能抢占战斗");
            float before=state.OpenProgress;
            Tick(2.15,closed:false); Tick(2.20);
            Check(Mathf.Abs(before-state.OpenProgress)<.0001f && !state.LauncherOpen,"短暂手形波动保留进度但不得计入保持时间");
            Tick(2.25); Tick(2.30); Tick(2.35);
            Check(state.LauncherOpen,"姿态恢复后继续累计有效保持");

            state.Reset(false);
            for(int i=0;i<8;i++)
            {
                double t=3+i*.04; f=WristMenuChecks.Sample(t);
                f.headPosition+=Vector3.right*(i*.06f); f.leftWrist+=Vector3.right*(i*.06f);
                state.Tick(f,false,false,t,settings);
            }
            Check(state.LauncherOpen,"身体与手腕共同平移不应不断重置开启进度");
            state.Reset(false);
            for(int i=0;i<8;i++)
            {
                double t=4+i*.04; f=WristMenuChecks.Sample(t); f.leftWrist.x+=(i%2==0?-.08f:.08f);
                state.Tick(f,false,false,t,settings);
            }
            Check(!state.LauncherOpen,"明显挥动仍不能当作稳定抬腕");

            state.Reset(false); settings.requireWristFacing=true;
            for(int i=0;i<8;i++) Tick(5+i*.04);
            Check(!state.LauncherOpen,"可选严格朝向条件仍有效"); settings.requireWristFacing=false;
            state.Reset(false);
            for(int i=0;i<8;i++) { f=WristMenuChecks.Sample(6+i*.04); f.headForward=Vector3.back; state.Tick(f,false,false,6+i*.04,settings); }
            Check(!state.LauncherOpen,"放宽高度仍不允许背对头显的手腕召出");

            state.Reset(false); Tick(7); Tick(7.05);
            f=WristMenuChecks.Sample(7.1); f.leftValid=false; state.Tick(f,false,false,7.1,settings);
            Check(state.OpenProgress==0 && !state.LauncherOpen,"真实追踪失效立即清除开启证据，不能借用手形宽限");
            for(int i=0;i<8;i++) Tick(7.15+i*.04);
            Check(!state.LauncherOpen,"丢失UI意图后旧拳不自动补弹");
            for(int i=0;i<6;i++) Tick(7.5+i*.04,closed:false,right:false);
            Check(!state.OwnsInput,"只放松左手也可解除旧UI归属");

            state.Reset(true);
            Tick(8,closed:false); Tick(8.05,closed:false);
            f=WristMenuChecks.Sample(8.1); f.leftValid=false; state.Tick(f,false,false,8.1,settings);
            state.Tick(f,false,false,8.5,settings);
            Check(state.OwnsInput,"释放未确认前丢追踪，不可把失联当作中立");
            for(int i=0;i<6;i++) Tick(8.6+i*.04,closed:false,right:false);
            Check(!state.OwnsInput,"重新取得左手释放证据即可恢复，无需右手");
            f=WristMenuChecks.Sample(8.9); f.leftValid=false; state.Tick(f,false,false,8.9,settings);
            Check(!state.OwnsInput,"完成交接后再丢左手不会重新抢占战斗输入");

            var gesture=new RpgGestureState(new RpgGestureSettings());
            var n=new NavigationHandInput { rightValid=true,rightOpen=true,rightObserved=9,rightWrist=new Vector3(10,1,4) };
            gesture.Reset(); var intent=gesture.Process(n,Vector3.forward,false,false,9);
            Check(intent.dodgeDirection==Vector3.zero,"交接后已张开的右手第一帧仅重建原点，不触发闪避");
            n.rightObserved=9.05; intent=gesture.Process(n,Vector3.forward,false,false,9.05);
            Check(intent.dodgeDirection==Vector3.zero,"交接后张掌静止不产生闪避");
            return count;
        }
    }
}
#endif
