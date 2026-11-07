#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
namespace NarutoVR.SealCombat.Editor
{
    public static class ThrowEaseChecks
    {
        public static int Run()
        {
            var effect=AssetDatabase.LoadAssetAtPath<ProjectileEffectDefinition>("Assets/ScriptableObjects/投射物效果配置/效果_fireball.asset");
            if(!effect) throw new Exception("火球配置未找到");
            int checks=0;
            foreach(int hz in new[]{72,90,120}) foreach(bool pinch in new[]{false,true})
            {
                var state=new HandThrowState(effect.throwing); Vector3 launched=default;
                for(int i=0;i<hz/2;i++)
                {
                    double t=(double)i/hz; bool held=t<.24;
                    var grip=new ThrowGripSample { observed=t,fistValid=!pinch,fistClosed=!pinch&&held,fistOpen=!pinch&&!held,
                        pinchValid=pinch,pinchClosed=pinch&&held,pinchOpen=pinch&&!held };
                    Vector3 v=state.Process(Vector3.forward*(float)Math.Min(t,.24)*.55f,Vector3.up*1.7f,grip,t);
                    if(v!=Vector3.zero) launched=v;
                }
                if(launched==Vector3.zero || Vector3.Angle(launched,Vector3.forward)>1)
                    throw new Exception($"小幅投掷仍黏手：{hz}Hz {(pinch?"捏合":"握拳")}，0.55米/秒向前甩手后松开未发射");
                checks++;
            }
            foreach(bool move in new[]{false,true})
            {
                var state=new HandThrowState(effect.throwing);
                for(int i=0;i<40;i++)
                {
                    double t=i*.01; var p=move?Vector3.right*i*.02f:Vector3.zero;
                    var grip=new ThrowGripSample { observed=t,fistValid=true,fistClosed=i<24,fistOpen=i>=24 };
                    if(state.Process(p,Vector3.up*1.7f+p,grip,t)!=Vector3.zero) throw new Exception("放宽配置不能让静止/身体平移松手发射");
                }
                checks++;
            }
            return checks;
        }
    }
}
#endif
