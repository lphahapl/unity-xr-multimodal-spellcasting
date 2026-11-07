#if UNITY_EDITOR
using System;
using NarutoVR.SealCombat.Rpg;
using UnityEngine;

namespace NarutoVR.SealCombat.Editor
{
    public static class InteractionFeelChecks
    {
        static void Require(bool ok,string message) { if(!ok) throw new Exception("手感回归："+message); }
        static NavigationHandInput Frame(double t, float z=0) => new NavigationHandInput
        {
            leftObserved=t,rightObserved=t,leftValid=true,rightValid=true,leftPointing=true,
            leftIndex=new Vector3(-.4f,1.3f,z),leftWrist=new Vector3(-.4f,1.2f,z),rightWrist=new Vector3(.4f,1.2f,0),
            leftRayValid=true,leftRayOrigin=new Vector3(-.4f,1.3f,z),leftRayDirection=Vector3.forward
        };
        public static int Run()
        {
            int count=0; var settings=new NavigationSmoothingSettings{enabled=true}; var timing=new RpgGestureSettings();
            var filter=new NavigationInputSmoother(); float largest=0;
            for(int i=0;i<100;i++)
            {
                var filtered=filter.Process(Frame(i*.01,i%2==0?.004f:-.004f),settings,timing,i*.01);
                if(i>20) largest=Mathf.Max(largest,Mathf.Abs(filtered.leftIndex.z));
            }
            Require(largest<.002f,"静止4毫米交替噪声应削减一半以上");count++;
            filter.Reset(); filter.Process(Frame(0),settings,timing,0);
            var moved=filter.Process(Frame(.05,.1f),settings,timing,.05);
            Require(moved.leftIndex.z>.08f,"明确快速移动滞后应小于2厘米");count++;
            var duplicate=filter.Process(Frame(.05,.15f),settings,timing,.08);
            Require(duplicate.leftIndex==moved.leftIndex,"重复观测不能继续追赶或引入新位置");count++;
            Require(!filter.Process(Frame(.06,.7f),settings,timing,.06).leftValid,"原始跳变必须在平滑前拒绝");count++;
            var restored=filter.Process(Frame(.07,.7f),settings,timing,.07);
            Require(restored.leftValid&&Mathf.Abs(restored.leftIndex.z-.7f)<.0001f,"恢复第一帧重种，不沿旧点缓慢飘回");count++;
            Require(!filter.Process(Frame(.4,.71f),settings,timing,.4).leftValid,"观测缺口必须重备");count++;
            var invalid=Frame(.41);invalid.leftValid=false; filter.Process(invalid,settings,timing,.41);
            Require(filter.PositionLag==0,"失踪清除视觉滞后");count++;
            var backward=Frame(.4); filter.Process(Frame(.42),settings,timing,.42);
            Require(!filter.Process(backward,settings,timing,.43).leftValid,"倒序时间不能平滑成合法动作");count++;
            Require(!new NavigationSmoothingSettings{stillSeconds=float.NaN}.Valid,"无效平滑参数拒绝");count++;

            var gesture=new RpgGestureState(new RpgGestureSettings{readySeconds=.1f,dwellSeconds=.3f});
            RpgGestureIntent Feed(double t,float z) => gesture.Process(Frame(t,z),Vector3.forward,false,false,t);
            Feed(0,0);Feed(.06,0);Feed(.12,0);Feed(.18,.08f);Feed(.28,.08f);
            float earned=gesture.DwellProgress;
            Feed(.33,.08f);gesture.PauseDwell();
            Require(Mathf.Abs(gesture.DwellProgress-earned)<.0001f,"换点待定暂停应保留已获得进度");count++;
            gesture.PauseDwell();Require(Mathf.Abs(gesture.DwellProgress-earned)<.0001f,"同观测不能重复扣掉驻留时间");count++;
            Feed(.38,.08f);gesture.PauseDwell();
            Require(Mathf.Abs(gesture.DwellProgress-earned)<.0001f,"连续暂停不能偷偷累计或清零");count++;
            Feed(.43,.08f);Require(gesture.DwellProgress>earned,"优势消失后继续累计旧点进度");count++;
            Feed(.53,.08f);var edge=Feed(.59,.08f);gesture.PauseDwell();
            Require(edge.navigationStep!=Vector2.zero&&!gesture.NavigationNeedsNeutral&&gesture.DwellProgress<1,"暂停恰逢完成边沿时不能吞掉后续确认或提交旧点");count++;
            var resume=Feed(.65,.08f);Require(resume.navigationStep!=Vector2.zero,"有效观察恢复后仍可完成一次移动");count++;
            gesture.RestartDwell();Require(gesture.DwellProgress==0,"真正换点必须清零");count++;
            return count;
        }
    }
}
#endif
