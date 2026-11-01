using System;
using System.Collections.Generic;
using UnityEngine;

namespace NarutoVR.SealCombat
{
    [Serializable]
    public sealed class AssistedThrowSettings
    {
        public const float MotionWindowSeconds = .12f;
        [Header("动作评分与辅助发射")]
        [InspectorName("启用评分与辅助发射")] public bool enabled;
        [InspectorName("允许后拉前送发射")] public bool windupEnabled=true;
        [InspectorName("允许伸手保持发射")] public bool reachEnabled=true;
        [InspectorName("发射分数阈值（非概率）"),Range(0,100)] public float scoreThreshold=72;
        [InspectorName("速度／位移／一致性／后拉权重")] public Vector4 weights=new(.35f,.25f,.25f,.15f);
        [Header("后拉和前送（相对身体，同时检查世界手部运动）")]
        [InspectorName("最小后拉（米）"),Min(.03f)] public float windupDistance=.07f;
        [InspectorName("最小前送（米）"),Min(.05f)] public float forwardDistance=.12f;
        [InspectorName("最小前送速度（米/秒）"),Min(.3f)] public float forwardSpeed=.8f;
        [InspectorName("前送有效时间（秒）"), Tooltip("必须不超过0.12秒轨迹窗口；超过会使前送始终无法确认。"),Range(.02f,MotionWindowSeconds)] public float motionConfirm=.04f;
        [InspectorName("动作最长时间（秒）"),Min(.2f)] public float actionTimeout=1.1f;
        [InspectorName("评分速度满分（米/秒）"),Min(.5f)] public float fullSpeed=1.5f;
        [InspectorName("评分位移满分（米）"),Min(.1f)] public float fullDistance=.22f;
        [InspectorName("最低前送方向一致性"),Range(.3f,1)] public float minimumConsistency=.65f;
        [InspectorName("世界运动最低比例"),Range(.5f,1)] public float worldMotionRatio=.8f;
        [Header("伸手保持")]
        [InspectorName("舒适伸手距离（头显到腕，米）"),Range(.35f,.9f)] public float comfortableReach=.65f;
        [InspectorName("开始保持的距离比例"),Range(.65f,1)] public float reachRatio=.85f;
        [InspectorName("主动前伸距离（米）"),Min(.05f)] public float reachMotion=.1f;
        [InspectorName("稳定保持（秒）"),Range(.25f,1.5f)] public float reachSeconds=.6f;
        [InspectorName("允许手部位置波动（米）"),Range(.01f,.08f)] public float reachStability=.04f;
        [InspectorName("保持时最高速度（米/秒）"),Range(.05f,.5f)] public float reachSpeed=.25f;
        [InspectorName("瞄准波动角（度）"),Range(2,15)] public float aimStability=8;
        [InspectorName("前方范围半角（度）"),Range(25,75)] public float forwardCone=55;
        [InspectorName("手腕在头显前方的最低距离（米）"),Range(.1f,.4f)] public float minimumForwardReach=.25f;
        [InspectorName("估计右肩偏移（米，左手自动镜像）")] public Vector3 shoulderOffset=new(.18f,-.22f,0);
        [Header("自动动作参考运动保护")]
        [InspectorName("动作中头部最大平移（米）"),Range(.05f,.4f)] public float maxHeadMove=.18f;
        [InspectorName("动作中头向最大变化（度）"),Range(10,60)] public float maxHeadTurn=35;
        public AssistedThrowSettings Copy() => (AssistedThrowSettings)MemberwiseClone();
        public bool Valid
        {
            get
            {
                var values=new[]{scoreThreshold,weights.x,weights.y,weights.z,weights.w,windupDistance,forwardDistance,forwardSpeed,
                    motionConfirm,actionTimeout,fullSpeed,fullDistance,comfortableReach,reachRatio,reachMotion,reachSeconds,reachStability,
                    reachSpeed,aimStability,forwardCone,maxHeadMove,maxHeadTurn,minimumConsistency,worldMotionRatio,minimumForwardReach};
                foreach(float v in values) if(!float.IsFinite(v)||v<0) return false;
                return scoreThreshold<=100 && weights.x+weights.y+weights.z+weights.w>0 && windupDistance>=.03f && forwardDistance>=.05f &&
                    forwardSpeed>=.3f && motionConfirm>=.02f && motionConfirm<=MotionWindowSeconds && actionTimeout>motionConfirm && fullSpeed>=forwardSpeed && fullDistance>=forwardDistance &&
                    comfortableReach>=.35f && comfortableReach<=.9f && reachRatio>=.65f && reachRatio<=1 && reachMotion>=.05f && reachSeconds>=.25f &&
                    reachStability>=.01f && reachSpeed>.01f && aimStability>=2 && forwardCone>=25 && forwardCone<=75 && maxHeadMove>=.05f && maxHeadTurn>=10 &&
                    minimumConsistency>=.3f && minimumConsistency<=1 && worldMotionRatio>=.5f && worldMotionRatio<=1 && minimumForwardReach>=.1f && minimumForwardReach<=.4f && Finite(shoulderOffset);
            }
        }
        static bool Finite(Vector3 v)=>float.IsFinite(v.x)&&float.IsFinite(v.y)&&float.IsFinite(v.z);
    }

    public enum AssistedThrowKind { None, WindupThrow, ReachHold }
    [Serializable]
    public struct ThrowActionScore
    {
        public bool available, armed, windupReady, extensionReady;
        public double observed, actionEpoch;
        public int samples;
        public float total, threshold, speedScore, distanceScore, directionScore, windupScore;
        public float speed, worldSpeed, forwardDistance, worldForwardDistance, windupDistance, consistency, fitError;
        public float headWristDistance, reachRequired, holdProgress;
        public Vector3 direction, referenceForward,rawWindupDirection,windupDirection;
        public string windupAimSource;
        public string phase, reason, holdReason;
        public AssistedThrowKind candidate;
    }
    public readonly struct AssistedThrowCandidate
    {
        public readonly AssistedThrowKind kind;
        public readonly Vector3 direction;
        public AssistedThrowCandidate(AssistedThrowKind kind,Vector3 direction) { this.kind=kind; this.direction=direction; }
    }

    /// <summary>Scores fresh observations and proposes one action. Never spawns or bypasses collision/ownership gates.</summary>
    public sealed class HandThrowIntentState
    {
        readonly HandThrowSettings tracking;
        readonly AssistedThrowSettings s;
        readonly bool left;
        struct Point { public double time; public Vector3 relative,world; }
        readonly List<Point> points=new(48);
        double last=double.NegativeInfinity,gripSince=-1,windupAt=-1,holdSince=-1,epoch;
        Vector3 previousHand,previousHead,baseline,baselineWorld,forward,headStart,trough,troughWorld,stable,stableAim;
        Vector3 reachBase,reachWorldBase,lastWindupYaw;
        double yawTime;
        bool armed,windup,extension,consumed,nearBaseline;
        float backDistance;
        public ThrowActionScore Score { get; private set; }
        public bool Armed=>armed;
        public HandThrowIntentState(HandThrowSettings settings,bool leftHand=false)
        { if(settings==null||!settings.Valid)throw new ArgumentException("动作评分参数无效"); tracking=settings.Copy(); s=tracking.assisted; left=leftHand; Reset(); }
        public void Reset(string reason="等待握持确认")
        {
            last=double.NegativeInfinity; gripSince=windupAt=holdSince=-1;
            yawTime=double.NegativeInfinity;lastWindupYaw=Vector3.zero;
            armed=windup=extension=consumed=nearBaseline=false; backDistance=0; points.Clear(); epoch++;
            Score=new ThrowActionScore{threshold=s.scoreThreshold,reachRequired=s.comfortableReach*s.reachRatio,phase=reason,reason=reason,actionEpoch=epoch};
        }
        public bool ContradictsWindup(Vector3 velocity) => s.enabled && s.windupEnabled && windup && velocity.sqrMagnitude>.0001f && Vector3.Dot(velocity.normalized,forward)<-.3f;

        public AssistedThrowCandidate Process(Vector3 hand,Vector3 head,Vector3 headForward,ThrowGripSample input,double now)
        {
            if(!s.enabled) return default;
            if(!double.IsFinite(now)||!double.IsFinite(input.observed)||input.observed>now+.01||now-input.observed>tracking.sampleMaxAge ||
                !Finite(hand)||!Finite(head)||!Finite(headForward)||headForward.sqrMagnitude<.1f || (!input.fistValid&&!input.pinchValid))
            { Reset("追踪或握持特征无效"); return default; }
            if(input.observed<last) { Reset("观测时间倒退"); return default; }
            if(input.observed==last) return default; // Render time is never evidence.
            if(double.IsFinite(last) && (input.observed-last>tracking.sampleMaxGap || Vector3.Distance(hand,previousHand)>tracking.maximumJump || Vector3.Distance(head,previousHead)>tracking.maximumJump))
            { Reset("观测间隔或位置跳变"); return default; }
            double time=input.observed;
            float dt=double.IsFinite(last)?(float)(time-last):0;
            float immediateSpeed=dt>0?((hand-head)-(previousHand-previousHead)).magnitude/dt:0;
            last=time; previousHand=hand; previousHead=head;
            Vector3 relative=hand-head;
            var report=Score; report.observed=time; report.headWristDistance=relative.magnitude; report.candidate=AssistedThrowKind.None;
            bool closed=(input.fistValid&&input.fistClosed)||(input.pinchValid&&input.pinchClosed);
            if(!armed)
            {
                if(!closed) gripSince=-1;
                else if(gripSince<0) gripSince=time;
                if(gripSince<0 || time-gripSince<tracking.gripSeconds) { report.phase="等待握持确认"; Score=report; return default; }
                armed=true; baseline=trough=relative; baselineWorld=troughWorld=hand;
                reachBase=relative;reachWorldBase=hand;
                headStart=head; forward=Horizontal(headForward); nearBaseline=relative.magnitude<s.comfortableReach*s.reachRatio-.04f;
            }
            report.armed=true; report.referenceForward=forward;
            if(consumed) { report.phase="本轮动作已提交，等待新动作"; Score=report; return default; }
            if(Vector3.Distance(head,headStart)>s.maxHeadMove || Vector3.Angle(forward,Horizontal(headForward))>s.maxHeadTurn)
            { Reset("头部参考移动过大，重新握住"); return default; }
            // A far hand at creation cannot pre-qualify hold. Establish a fresh near baseline first.
            if(!nearBaseline && relative.magnitude<s.comfortableReach*s.reachRatio-.04f)
            { reachBase=relative; reachWorldBase=hand; nearBaseline=true; extension=false; holdSince=-1; }
            float depth=Vector3.Dot(relative-reachBase,forward);
            float worldDepth=Vector3.Dot(hand-reachWorldBase,forward);
            if(nearBaseline && depth>=s.reachMotion && worldDepth>=s.reachMotion*s.worldMotionRatio) extension=true;
            if(extension && (depth<s.reachMotion*.4f || relative.magnitude<s.comfortableReach*s.reachRatio-.06f)) { extension=false; holdSince=-1; }

            float retreat=Vector3.Dot(baseline-relative,forward);
            if(retreat>backDistance && Vector3.Dot(baselineWorld-hand,forward)>s.windupDistance*.5f)
            {
                backDistance=retreat; trough=relative; troughWorld=hand; points.Clear();
                if(backDistance>=s.windupDistance) { windup=true; windupAt=time; }
            }
            if(windup && time-windupAt>s.actionTimeout)
            { windup=false; backDistance=0; baseline=trough=relative; baselineWorld=troughWorld=hand; points.Clear(); }
            if(s.windupEnabled && windup) ObserveYaw(input.wristToKnucklesWorld,time);
            else yawTime=double.NegativeInfinity;
            report.windupDirection=report.rawWindupDirection=Vector3.zero;
            report.windupAimSource=null;
            points.Add(new Point{time=time,relative=relative,world=hand});
            while(points.Count>1 && time-points[0].time>AssistedThrowSettings.MotionWindowSeconds) points.RemoveAt(0);
            report.windupReady=windup; report.extensionReady=extension; report.windupDistance=backDistance;
            report.phase=windup?"后拉已确认，前送评分中":"持球：前送或伸手保持";
            report.forwardDistance=Mathf.Max(0,Vector3.Dot(relative-(windup?trough:baseline),forward));
            report.worldForwardDistance=Mathf.Max(0,Vector3.Dot(hand-(windup?troughWorld:baselineWorld),forward));
            report.samples=points.Count; report.available=false;
            if(points.Count>=4 && time-points[0].time>=s.motionConfirm)
            {
                var a=points[0]; float span=(float)(time-a.time); Vector3 delta=relative-a.relative,worldDelta=hand-a.world;
                float path=0; for(int i=1;i<points.Count;i++) path+=(points[i].relative-points[i-1].relative).magnitude;
                report.speed=Mathf.Max(0,Vector3.Dot(delta,forward)/span);
                report.worldSpeed=Mathf.Max(0,Vector3.Dot(worldDelta,forward)/span);
                report.consistency=path>.0001f?Mathf.Clamp01(Vector3.Dot(delta,forward)/path):0;
                float error=0;
                foreach(var p in points) error+=(p.relative-Vector3.Lerp(a.relative,relative,(float)((p.time-a.time)/span))).sqrMagnitude;
                report.fitError=Mathf.Sqrt(error/points.Count);
                report.speedScore=Mathf.Clamp01(report.speed/s.fullSpeed);
                report.distanceScore=Mathf.Clamp01(report.forwardDistance/s.fullDistance);
                report.directionScore=report.consistency;
                report.windupScore=Mathf.Clamp01(backDistance/(s.windupDistance*1.5f));
                float sum=s.weights.x+s.weights.y+s.weights.z+s.weights.w;
                report.total=100*(report.speedScore*s.weights.x+report.distanceScore*s.weights.y+report.directionScore*s.weights.z+report.windupScore*s.weights.w)/sum;
                report.direction=worldDelta.sqrMagnitude>.00001f?worldDelta.normalized:forward;
                report.available=true;
                if(s.windupEnabled && windup && report.speed>0 && report.worldSpeed>0) {
                    report.rawWindupDirection=report.direction;
                    report.windupDirection=CombineWindupAim(report.direction,time,out report.windupAimSource);
                }
                bool hard=windup && report.forwardDistance>=s.forwardDistance && report.worldForwardDistance>=s.forwardDistance*s.worldMotionRatio &&
                    report.speed>=s.forwardSpeed && report.worldSpeed>=s.forwardSpeed*s.worldMotionRatio && report.consistency>=s.minimumConsistency && report.fitError<=tracking.maximumFitError;
                if(s.windupEnabled && hard && report.total>=s.scoreThreshold)
                    return Propose(AssistedThrowKind.WindupThrow,report,report.windupDirection,"后拉前送评分通过");
                report.reason=!windup?"自动前送需要先后拉":report.forwardDistance<s.forwardDistance?"前送位移不足":report.worldForwardDistance<s.forwardDistance*s.worldMotionRatio?"世界手部前送位移不足":report.speed<s.forwardSpeed?"前送速度不足":
                    report.worldSpeed<s.forwardSpeed*s.worldMotionRatio?"世界手部运动不足，疑似头部补偿":report.fitError>tracking.maximumFitError?"前送轨迹抖动过大":
                    report.consistency<s.minimumConsistency?"前送方向不一致":$"前送分数 {report.total:0}/{s.scoreThreshold:0}";
            }
            else report.reason="等待足够的新观测";

            Vector3 shoulder=Quaternion.LookRotation(forward)*new Vector3(left?-s.shoulderOffset.x:s.shoulderOffset.x,s.shoulderOffset.y,s.shoulderOffset.z);
            Vector3 aim=(relative-shoulder).normalized;
            bool reach=s.reachEnabled && extension && relative.magnitude>=report.reachRequired && Vector3.Dot(relative,forward)>s.minimumForwardReach &&
                Vector3.Angle(forward,aim)<=s.forwardCone && immediateSpeed<=s.reachSpeed;
            report.holdReason=!s.reachEnabled?"未启用伸手保持":!nearBaseline?"先收近手，再主动前伸":!extension?"主动前伸距离不足":
                relative.magnitude<report.reachRequired?"头显到腕距离不足":Vector3.Dot(relative,forward)<=s.minimumForwardReach?"请将持球手移到身体前方":
                Vector3.Angle(forward,aim)>s.forwardCone?"保持方向超出前方范围":immediateSpeed>s.reachSpeed?"手仍在移动，等待稳定":"稳定计时中";
            if(!reach) { holdSince=-1; report.holdProgress=0; }
            else
            {
                if(holdSince<0 || Vector3.Distance(relative,stable)>s.reachStability || Vector3.Angle(aim,stableAim)>s.aimStability)
                { holdSince=time; stable=relative; stableAim=aim; }
                report.holdProgress=Mathf.Clamp01((float)((time-holdSince)/s.reachSeconds)); report.direction=aim;
                report.holdReason=$"稳定保持 {report.holdProgress:P0}";
                report.phase="伸手保持"; report.reason=$"保持进度 {report.holdProgress:P0}";
                if(report.holdProgress>=1) return Propose(AssistedThrowKind.ReachHold,report,aim,"伸手保持完成");
            }
            Score=report; return default;
        }
        void ObserveYaw(Vector3 axis,double time)
        {
            if(!Finite(axis) || axis.sqrMagnitude<.5f)return;
            Vector3 horizontal=Vector3.ProjectOnPlane(axis.normalized,Vector3.up);
            if(horizontal.sqrMagnitude<.04f)return; // A nearly vertical hand has unreliable yaw.
            horizontal.Normalize();
            if(time-yawTime<=AssistedThrowSettings.MotionWindowSeconds &&
               Vector3.Angle(lastWindupYaw,horizontal)>60f)return;
            lastWindupYaw=horizontal; yawTime=time;
        }

        Vector3 CombineWindupAim(Vector3 trajectory,double time,out string source)
        {
            if(time-yawTime<=AssistedThrowSettings.MotionWindowSeconds){
                float depth=Vector3.Dot(trajectory,lastWindupYaw);
                if(depth>.05f){
                    source=time==yawTime?"本帧手向":"近期手向";
                    return (lastWindupYaw*depth+Vector3.up*trajectory.y).normalized;
                }
            }
            source="轨迹回退";
            return trajectory;
        }
        AssistedThrowCandidate Propose(AssistedThrowKind kind,ThrowActionScore report,Vector3 direction,string reason)
        {
            consumed=true; report.candidate=kind; report.phase="候选已通过，等待碰撞检查"; report.reason=reason; report.direction=direction;
            Score=report; return new AssistedThrowCandidate(kind,direction);
        }
        static bool Finite(Vector3 v)=>float.IsFinite(v.x)&&float.IsFinite(v.y)&&float.IsFinite(v.z);
        static Vector3 Horizontal(Vector3 v) { v.y=0; return v.sqrMagnitude>.001f?v.normalized:Vector3.forward; }
    }
}
