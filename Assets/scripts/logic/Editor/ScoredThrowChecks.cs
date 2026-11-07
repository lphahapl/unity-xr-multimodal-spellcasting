#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using Object=UnityEngine.Object;

namespace NarutoVR.SealCombat.Editor
{
    public static class ScoredThrowChecks
    {
        static void Require(bool ok,string message) { if(!ok) throw new Exception("评分投掷："+message); }
        static HandThrowSettings Settings() => new HandThrowSettings{gripSeconds=.04f,assisted=new AssistedThrowSettings{enabled=true}};
        static ThrowGripSample Grip(double t,bool closed=true) => new ThrowGripSample{observed=t,fistValid=true,fistClosed=closed,fistOpen=!closed};
        static readonly Vector3 Head=new(0,1.7f,0);
        static Vector3 Wrist(float z) => Head+new Vector3(.18f,-.22f,z);
        public static int Run()
        {
            int count=0;
            foreach(int hz in new[]{72,90,120})
            {
                foreach(bool neutral in new[]{false,true})
                {
                    var state=new HandThrowIntentState(Settings()); int launches=0; AssistedThrowKind mode=default;
                    for(int i=0;i<hz;i++)
                    {
                        double t=(double)i/hz;
                        float z=t<.15?.35f:t<.30?.35f-(float)(t-.15)*.8f:.23f+Mathf.Min(.4f,(float)(t-.30)*2);
                        var grip=Grip(t); if(neutral&&t>.27) grip.fistClosed=false; grip.fistOpen=false;
                        var result=state.Process(Wrist(z),Head,Vector3.forward,grip,t);
                        if(result.kind!=AssistedThrowKind.None) { launches++; mode=result.kind; Require(Vector3.Angle(result.direction,Vector3.forward)<1,"前送方向不应漂移"); }
                    }
                    Require(launches==1&&mode==AssistedThrowKind.WindupThrow,$"{hz}Hz，Neutral={neutral}后拉前送应只发一次，{state.Score.reason}"); count++;
                }
                var hold=new HandThrowIntentState(Settings()); int heldShots=0;
                for(int i=0;i<hz*2;i++)
                {
                    double t=(double)i/hz; float z=t<.15?.30f:.30f+Mathf.Min(.34f,(float)(t-.15)*.4f);
                    var result=hold.Process(Wrist(z),Head,Vector3.forward,Grip(t),t);
                    if(result.kind!=AssistedThrowKind.None){Require(result.kind==AssistedThrowKind.ReachHold&&t>1.5,"伸手保持必须足够时间且不误用评分路径");heldShots++;}
                }
                Require(heldShots==1,$"{hz}Hz主动前伸后保持应发一次：{hold.Score.reason}");count++;
            }
            foreach(string scenario in new[]{"far_spawn","no_grip","body_translation","head_only","head_turn","short_windup"})
            {
                var config=Settings(); if(scenario=="short_windup")config.assisted.reachEnabled=false;
                var state=new HandThrowIntentState(config); int fired=0;
                for(int i=0;i<220;i++)
                {
                    double t=i*.01; Vector3 head=Head,hand=Wrist(.65f),forward=Vector3.forward; var grip=Grip(t);
                    if(scenario=="no_grip") {grip=Grip(t,false); hand=Wrist(.3f+Mathf.Min(.35f,(float)t));}
                    if(scenario=="body_translation"){head+=Vector3.forward*(float)t;hand=Wrist(.3f)+Vector3.forward*(float)t;}
                    if(scenario=="head_only"){head-=Vector3.forward*(float)t;hand=Wrist(.3f);}
                    if(scenario=="head_turn"){forward=Quaternion.Euler(0,(float)t*180,0)*Vector3.forward;hand=Wrist(.3f+Mathf.Min(.35f,(float)t*.4f));}
                    if(scenario=="short_windup")hand=Wrist(t<.15?.3f:t<.25?.3f-(float)(t-.15)*.2f:.28f+Mathf.Min(.2f,(float)(t-.25)*2));
                    if(state.Process(hand,head,forward,grip,t).kind!=AssistedThrowKind.None)fired++;
                }
                Require(fired==0,scenario+"不应自动发射");count++;
            }
            var repeated=new HandThrowIntentState(Settings());
            for(int i=0;i<10;i++) repeated.Process(Wrist(.3f),Head,Vector3.forward,Grip(1),1+i*.01);
            Require(!repeated.Armed,"重复时间戳不能累计握持"); count++;
            var stale=new HandThrowIntentState(Settings());
            for(int i=0;i<15;i++)stale.Process(Wrist(.3f),Head,Vector3.forward,Grip(i*.01),i*.01);
            Require(stale.Armed,"准备序列应握稳");
            stale.Process(Wrist(.7f),Head,Vector3.forward,Grip(.14),1);
            Require(!stale.Armed&&!stale.Score.available,"旧观测不能延续资格"); count++;
            var retreat=new HandThrowIntentState(Settings()); int retractShots=0;
            for(int i=0;i<300;i++)
            {
                double t=i*.01;
                float z=t<.3?.65f:t<.8?.65f-(float)(t-.3)*.7f:t<1.7?.3f+(float)(t-.8)*(.35f/.9f):.65f;
                var result=retreat.Process(Wrist(z),Head,Vector3.forward,Grip(t),t);
                if(result.kind!=AssistedThrowKind.None) {Require(result.kind==AssistedThrowKind.ReachHold&&t>2.2,"远手生成后收近再前伸应走保持路径");retractShots++;}
            }
            Require(retractShots==1,"出生远伸允许收近后新动作发射");count++;
            var duplicateHold=new HandThrowIntentState(Settings());
            for(int i=0;i<=130;i++){double t=i*.01;float z=.3f+Mathf.Clamp((float)t-.15f,0,.85f)*.4f;duplicateHold.Process(Wrist(z),Head,Vector3.forward,Grip(t),t);}
            float progress=duplicateHold.Score.holdProgress;Require(progress>0&&progress<1,"测试必须已进入保持而尚未发射");
            for(int i=1;i<=8;i++)Require(duplicateHold.Process(Wrist(.64f),Head,Vector3.forward,Grip(1.3),1.3+i*.01).kind==AssistedThrowKind.None,"重复保持观测不得发射");
            Require(duplicateHold.Score.holdProgress==progress,"重复保持观测不能增加进度");count++;
            foreach(bool gap in new[]{false,true})
            {
                var clock=new HandThrowIntentState(Settings());
                for(int i=0;i<15;i++)clock.Process(Wrist(.3f),Head,Vector3.forward,Grip(i*.01),i*.01);
                double bad=gap?.5:.05;clock.Process(Wrist(.3f),Head,Vector3.forward,Grip(bad),bad);
                Require(!clock.Armed,"时间倒退/观测间隔必须撤销动作证据");count++;
            }
            var jump=new HandThrowIntentState(Settings());
            for(int i=0;i<15;i++)jump.Process(Wrist(.3f),Head,Vector3.forward,Grip(i*.01),i*.01);
            jump.Process(Wrist(1.3f),Head,Vector3.forward,Grip(.15),.15);
            Require(!jump.Armed,"跳变重置");count++;
            var settings=Settings(); var copy=settings.Copy(); copy.assisted.scoreThreshold=1;
            Require(settings.assisted.scoreThreshold==72,"配置复制不能共享内部辅助参数");count++;
            Require(!new HandThrowSettings{assisted=new AssistedThrowSettings{weights=Vector4.zero}}.Valid,"零权重不应允许除零");count++;
            count+=Recorder();
            count+=RelaxedFireball();
            count+=WindupAimChecks();
            var disabledWindup=Settings();disabledWindup.assisted.windupEnabled=false;disabledWindup.assisted.reachEnabled=false;
            var disabledState=new HandThrowIntentState(disabledWindup);
            for(int i=0;i<40;i++) {
                double t=i*.01;float z=t<.15?.35f:.35f-(float)(t-.15)*.5f;
                disabledState.Process(Wrist(z),Head,Vector3.forward,Grip(t),t);
            }
            Require(!disabledState.ContradictsWindup(Vector3.back),"禁用后拉前送后不能拦截松手投掷方向");count++;
            Require(!new AssistedThrowSettings{motionConfirm=.2f}.Valid,"超过轨迹窗口的前送确认时间必须在配置阶段拒绝");count++;
            return count;
        }
        static int WindupAimChecks()
        {
            int count=0;
            foreach(float turn in new[]{0f,90f})foreach(int sampleMode in new[]{0,1,2}){
                var rotation=Quaternion.AngleAxis(turn,Vector3.up);
                var state=new HandThrowIntentState(Settings());
                var heading=(Vector3.forward+Vector3.right*.45f).normalized;
                var motion=new Vector3(-.18f,.35f,1f);
                // Pre-windup jitter should not leak into this throw; the last samples can be occluded.
                for(int i=0;i<150;i++){
                    double t=i*.01;
                    var grip=Grip(t);
                    grip.wristToKnucklesWorld=sampleMode==2?Vector3.zero:
                        sampleMode==1 && t>.32?Vector3.up:rotation*heading;
                    var position=Wrist(.35f)-Vector3.forward*Mathf.Clamp((float)(t-.15)*.8f,0,.12f)
                        +motion*Mathf.Clamp((float)(t-.30)*2,0,.4f);
                    var candidate=state.Process(Head+rotation*(position-Head),Head,rotation*Vector3.forward,grip,t);
                    if(candidate.kind==AssistedThrowKind.None)continue;
                    var expected=sampleMode==2?motion.normalized:
                        (heading*Vector3.Dot(motion,heading)+Vector3.up*motion.y).normalized;
                    Require(candidate.kind==AssistedThrowKind.WindupThrow && Vector3.Angle(candidate.direction,rotation*expected)<2,"前送瞄准的水平角/仰角应随玩家旋转");
                    Require(Vector3.Angle(candidate.direction,state.Score.windupDirection)<.1f,"候选与本帧预览必须一致");
                    Require(state.Score.windupAimSource==(sampleMode==2?"轨迹回退":sampleMode==1?"近期手向":"本帧手向"),$"手向回退选择错误：模式{sampleMode}，t={t:F2}，来源={state.Score.windupAimSource}");
                    count++;break;
                }
            }
            Require(count==6,"六条真实状态机轨迹都应提交");
            return count;
        }
        static int RelaxedFireball()
        {
            var asset=UnityEditor.AssetDatabase.LoadAssetAtPath<ProjectileEffectDefinition>("Assets/ScriptableObjects/投射物效果配置/效果_fireball.asset");
            Require(asset && asset.ValidateDefinition()==null,"当前火球资产配置有效");
            foreach(bool reach in new[]{false,true}) {
                var settings=asset.throwing.Copy();settings.assisted.reachEnabled=reach;settings.assisted.windupEnabled=!reach;
                var state=new HandThrowIntentState(settings);int fired=0;
                for(int i=0;i<180;i++) {
                    double t=i/90.0;
                    float z=reach ? .27f+Mathf.Clamp01((float)(t-.15)/.5f)*.20f : t<.15?.30f:t<.30?.30f-(float)(t-.15)*.4f:.24f+Mathf.Min(.15f,(float)(t-.30)*.8f);
                    var result=state.Process(Wrist(z),Head,Vector3.forward,Grip(t),t);
                    if(result.kind!=AssistedThrowKind.None) {
                        fired++;Require(result.kind==(reach?AssistedThrowKind.ReachHold:AssistedThrowKind.WindupThrow),"辅助发射方式必须正确");
                    }
                }
                Require(fired==1,"轻动作配置应恰好发射一次："+state.Score.reason+"；"+state.Score.holdReason);
                var stationary=new HandThrowIntentState(settings);
                for(int i=0;i<180;i++) Require(stationary.Process(Wrist(.3f),Head,Vector3.forward,Grip(i/90.0),i/90.0).kind==AssistedThrowKind.None,"静止持球不能自动发射");
            }
            return 5;
        }
        static int Recorder()
        {
            var root=new GameObject("记录器回读检查"){hideFlags=HideFlags.HideAndDontSave};
            try
            {
                var recorder=root.AddComponent<ThrowDiagnosticRecorder>(); recorder.autoRecord=false;
                string folder=Path.GetFullPath("Logs/scored-throw-tests"); recorder.BeginInDirectory(folder);
                string path=recorder.FilePath;
                var row=new HandThrowObservation{shotId="probe",eventType="frame",hasSample=true,observed=1,status="等待",score=new ThrowActionScore{available=true,total=81.5f,threshold=72}};
                recorder.Observe(row);recorder.Observe(row);
                row.eventType="rejected";row.status="碰撞阻挡";recorder.Observe(row);recorder.Stop();
                string[] lines=File.ReadAllLines(path);
                Require(lines.Length==5,"应只有header/config/frame/rejected/end，重复帧省略但同时间不同事件保留");
                var replay=JsonUtility.FromJson<HandThrowObservation>(lines[2]);Require(replay.score.total==81.5f,"动作分应可回读");
                recorder.BeginInDirectory(folder); Require(recorder.FilePath!=path&&File.Exists(path),"新录制不得覆盖旧记录");
                recorder.maximumMegabytes=1;
                row.status=new string('x',2000);
                for(int i=0;i<2000&&recorder.IsRecording;i++){row.observed=i;recorder.Observe(row);}
                Require(!recorder.IsRecording&&File.ReadAllLines(recorder.FilePath).Last().Contains("end"),"大小限制自动保存完整尾行");
                return 4;
            }
            finally {Object.DestroyImmediate(root);}
        }
        public static IEnumerator PlayProbe(Action<string> record)
        {
            var root=new GameObject("辅助发射真实执行检查");var head=new GameObject("头");head.transform.SetParent(root.transform);
            head.transform.position=new Vector3(12000,1.7f,12000);
            var effect=root.AddComponent<SkillEffects>();effect.head=head.transform;effect.playerRoot=root.transform;effect.hitLayers=0;
            var definition=ScriptableObject.CreateInstance<ProjectileEffectDefinition>();definition.releaseMode=ProjectileReleaseMode.GripThrow;definition.throwing=Settings();definition.throwLocalOffset=Vector3.zero;
            var skill=ScriptableObject.CreateInstance<SkillDefinition>();skill.effectDefinition=definition;skill.durationSeconds=10;
            double t=200;float z=.35f;bool valid=true,neutral=false; Vector3 hand=head.transform.position+new Vector3(.18f,-.22f,z);
            effect.GameClock=()=>t;effect.HandInputClock=()=>t;effect.WristPoseOverride=left=>!left&&valid?new Pose(hand,Quaternion.identity):(Pose?)null;
            effect.ThrowGripProvider=(_,__)=>{var grip=Grip(t);grip.fistClosed=!neutral;grip.fistOpen=false;return grip;};
            var trace=new System.Collections.Generic.List<HandThrowObservation>();effect.ThrowObserved+=o=>trace.Add(o);
            var recorder=root.AddComponent<ThrowDiagnosticRecorder>();recorder.effects=effect;recorder.autoRecord=false;recorder.BeginInDirectory(Path.GetFullPath("Logs/scored-throw-tests"));
            GameObject obstacle=null, gazeTarget=null;
            try
            {
                gazeTarget=new GameObject("头显准星近靶"){layer=30};
                gazeTarget.transform.position=head.transform.position+Vector3.forward*1.6f;
                gazeTarget.AddComponent<BoxCollider>().size=new Vector3(.3f,.3f,.1f);
                gazeTarget.AddComponent<SkillTrainingTarget>(); effect.hitLayers=1<<30; Physics.SyncTransforms();
                for(int trial=0;trial<3;trial++)
                {
                    effect.ClearAll(); z=.35f; neutral=false; hand=head.transform.position+new Vector3(.18f,-.22f,z);
                    Require(effect.TryActivate(skill),"辅助发射应能生成");
                    if(trial==2){effect.hitLayers=1<<30;obstacle=new GameObject("阻挡出手"){layer=30};obstacle.transform.position=head.transform.position+new Vector3(.18f,-.22f,.45f);obstacle.AddComponent<BoxCollider>().size=new Vector3(.4f,.4f,.04f);Physics.SyncTransforms();}
                    for(int i=0;i<55;i++){t+=.01;yield return null;}
                    for(int i=1;i<=210&&effect.HasHeldObject;i++)
                    {
                        t+=.01;
                        neutral=trial==0&&i>=14;
                        z=trial==1?.35f+Mathf.Min(.3f,i*.004f):i<=15?.35f-i*.008f:.23f+Mathf.Min(.4f,(i-15)*.02f);
                        hand=head.transform.position+new Vector3(.18f,-.22f,z);yield return null;
                    }
                    if(trial<2)
                    {
                        Require(effect.LaunchCount==trial+1&&!effect.HasHeldObject,"不松手辅助发射失败，"+effect.BuildThrowDebugDetails(t));
                        Vector3 aim=(effect.GetAimPoint()-effect.LastLaunchPosition).normalized;
                        Require(Vector3.Dot(effect.LastLaunchDirection,aim)>.99999f && Vector3.Angle(aim,Vector3.forward)>5,
                            "辅助动作必须从偏置的手位飞向头向近靶，不采用手臂或前送轨迹。");
                        record(trial==0?"真实后拉前送命中头向准星；左手无追踪不阻挡右手":"真实伸手保持触发，弹道从手边汇入头向准星");
                    }
                    else {Require(effect.LaunchCount==2&&effect.HasHeldObject&&trace.Any(o=>o.eventType=="rejected"),"碰撞不得被高分绕过");record("高分候选仍受实际碰撞约束，记录拒绝原因");}
                }
                effect.ThrowInputSuppressed=true;t+=.01;yield return null;
                Require(trace.Last().eventType=="suppressed","占用早退也应记录");effect.ThrowInputSuppressed=false;
                valid=false;t+=.01;yield return null;
                Require(trace.Last().eventType=="tracking_lost","丢追踪早退也应记录");record("早退路径记录输入占用和追踪丢失，不以零分掩盖原因");
                recorder.Stop();Require(File.ReadAllText(recorder.FilePath).Contains("shot_config"),"真实日志需要配置快照");
            }
            finally {recorder.Stop();effect.ClearAll();if(obstacle)Object.Destroy(obstacle);if(gazeTarget)Object.Destroy(gazeTarget);Object.Destroy(root);Object.Destroy(definition);Object.Destroy(skill);}
        }
    }
}
#endif
