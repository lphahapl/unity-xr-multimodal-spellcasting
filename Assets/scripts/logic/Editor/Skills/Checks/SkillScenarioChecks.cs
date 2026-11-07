using System;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NarutoVR.SealCombat.Editor
{
    /// <summary>按训练场配置驱动实际试炼更新循环，验证有限次数演示的完成条件。</summary>
    public static class SkillScenarioChecks
    {
        private static Runner active;
        public static void Begin(Action<bool,string> completed)
        {
            if(!Application.isPlaying)throw new InvalidOperationException("护盾试炼检查需要播放模式。");
            if(active!=null)throw new InvalidOperationException("护盾试炼检查已经在运行。");
            var source=Object.FindFirstObjectByType<SkillScenario>();
            if(!source || source.kind!=SkillScenarioKind.ShieldTrial)throw new InvalidOperationException("请先打开护盾试炼场景。");
            active=new Runner(source,completed);
        }
        private sealed class Runner
        {
            private GameObject root;
            private SkillEffects effects;
            private SkillScenario trial;
            private SkillDefinition skill;
            private readonly Action<bool,string> completed;
            private readonly double deadline;
            private double finishedAt=-1;
            private int previousPulse;
            private bool ended;
            private readonly bool previousRunInBackground;
            public Runner(SkillScenario source,Action<bool,string> completed)
            {
                this.completed=completed;
                previousRunInBackground=Application.runInBackground;Application.runInBackground=true;
                root=new GameObject("自动验收_护盾试炼");root.transform.position=new Vector3(200,100,100);
                effects=root.AddComponent<SkillEffects>();effects.head=root.transform;effects.playerRoot=root.transform;
                effects.shieldPrefab=source.effects.shieldPrefab;effects.shieldCapacity=source.effects.shieldCapacity;
                trial=root.AddComponent<SkillScenario>();trial.kind=SkillScenarioKind.ShieldTrial;trial.effects=effects;
                trial.showDesktopControls=false;trial.autoRunOnFirstShield=true;
                trial.firstPulseDelay=source.firstPulseDelay;trial.pulseInterval=source.pulseInterval;
                trial.pulsesPerTrial=source.pulsesPerTrial;trial.testDamage=source.testDamage;
                skill=ScriptableObject.CreateInstance<SkillDefinition>();skill.effectKind=SkillEffectKind.SelfBuff;skill.durationSeconds=10;
                effects.TryActivate(skill);
                deadline=EditorApplication.timeSinceStartup+14;
                EditorApplication.update+=Poll;AssemblyReloadEvents.beforeAssemblyReload+=Abort;
            }
            private void Poll()
            {
                try
                {
                    if(!Application.isPlaying)throw new Exception("播放模式结束，检查已中断。");
                    if(EditorApplication.timeSinceStartup>deadline)throw new Exception("护盾试炼检查超时。");
                    if(trial.PulseCount!=previousPulse)
                    {
                        previousPulse=trial.PulseCount;
                        float expected=Mathf.Max(0,100-30*previousPulse);
                        if(Mathf.Abs(effects.ShieldPoints-expected)>.01f)throw new Exception($"第 {previousPulse} 次后护盾应为 {expected}，实际 {effects.ShieldPoints}。");
                    }
                    if(trial.PulseCount==4&&!trial.IsTesting)
                    {
                        if(Mathf.Abs(trial.UnabsorbedDamage-20)>.01f)throw new Exception("第四击应报告 20 点未吸收伤害。");
                        if(finishedAt<0)finishedAt=EditorApplication.timeSinceStartup;
                        if(EditorApplication.timeSinceStartup-finishedAt<1) return;
                        Finish(true,"四次伤害使护盾 100→70→40→10→0，未吸收 20；结束后不继续攻击。");
                    }
                    else if(previousPulse>4)throw new Exception("停止后的试炼重复制造了伤害。");
                }
                catch(Exception e){Finish(false,e.ToString());}
            }
            private void Abort()=>Finish(false,"程序集重载，护盾试炼已清理。");
            private void Finish(bool passed,string detail)
            {
                if(ended)return;ended=true;
                EditorApplication.update-=Poll;AssemblyReloadEvents.beforeAssemblyReload-=Abort;
                Application.runInBackground=previousRunInBackground;
                if(root)Object.DestroyImmediate(root);if(skill)Object.DestroyImmediate(skill);
                active=null;completed?.Invoke(passed,detail);
            }
        }
    }
}
