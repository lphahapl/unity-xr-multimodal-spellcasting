using System;
using UnityEngine;

namespace NarutoVR.SealCombat.Rpg
{
    public sealed partial class RpgLocomotionController
    {
        [Header("战斗／探索移动")]
        [InspectorName("启用遭遇移动规则")] public bool encounterModesEnabled;
        [InspectorName("探索步速（米／秒）"),Min(.05f)] public float exploreSpeed=.95f;
        [InspectorName("启用头部俯仰减速")] public bool pitchSlowsWalk;
        [InspectorName("开始减速俯仰角"),Range(0,80)] public float pitchSlowStart=35;
        [InspectorName("充分减速俯仰角"),Range(1,89)] public float pitchSlowEnd=70;
        [InspectorName("最低步速倍率"),Range(0,1)] public float minimumPitchFactor=.55f;
        [Header("战斗：右手快推滑退")]
        [InspectorName("胸前准备区域半径（米）"),Min(.05f)] public float chestRadius=.28f;
        [InspectorName("胸口低于头部（米）"),Min(.05f)] public float chestBelowHead=.32f;
        [InspectorName("前推最短距离（米）"),Min(.03f)] public float pushDistance=.14f;
        [InspectorName("最低平均快推速度（米／秒）"),Min(.1f)] public float pushSpeed=.72f;
        [InspectorName("快推动作窗口（秒）"),Min(.1f)] public float pushWindow=.34f;
        [InspectorName("滑退持续时间（秒）"),Min(.05f)] public float slideDuration=.25f;
        [InspectorName("发射后闪避恢复时间（秒）"),Min(0)] public float throwDodgeRecovery=.65f;

        bool modeInitialized, needNeutral, shotGuard, remoteRearm, armed, sliding, walking, movedThisAction;
        double lastProcess=double.NaN, shotAt, lastWristStamp=double.NaN, anchorStamp, slideRemaining;
        Vector3 pushAnchor, pushHeading, lastRelative, slideDirection, slideDelta;
        Collider walkContact;
        string motionReason="等待追踪";
        float pitchFactor=1;
        public bool UsesEncounterModes=>encounterModesEnabled;
        public bool EncounterCombat { get; private set; }
        public bool EncounterSlideActive=>sliding;
        // Pointing still owns the input at a wall. Motion reports only successful travel.
        public bool ContinuousMotionActive => (walking && movedThisAction) || sliding;
        /// <summary>Committed horizontal exploration travel in metres. Zero ends the current stride.</summary>
        public event Action<float> ExplorationTravelled;
        public string EncounterDebugDetails=>$"模式：{(EncounterCombat?"战斗":"探索")}；等待收指={needNeutral}；步速倍率 {pitchFactor:F2}\n闪避：{motionReason}；发射保护 {(shotGuard?Math.Max(0,throwDodgeRecovery-(inputNow-shotAt)):0):F2}s；冷却 {DodgeRemaining:F2}s";

        public void SetEncounterCombat(bool combat)
        {
            if(modeInitialized && EncounterCombat==combat)return;
            bool switching=modeInitialized;
            modeInitialized=true; EncounterCombat=combat;
            RefreshAuthoredMarkers();
            ResetInput(); needNeutral=switching;
            motionReason=switching?"模式切换，请先收指":"等待手势";
        }
        public void NotifyProjectileLaunched(double now)
        {
            if(!UsesEncounterModes || !double.IsFinite(now))return;
            shotAt=now; shotGuard=true; remoteRearm=false;
            armed=false; lastWristStamp=double.NaN;
            motionReason="发射保护：收回胸前或等待计时，再做一次新的快推";
        }
        void ResetEncounterMotion()
        {
            StopExploration();
            walkContact = null;
            lastProcess=lastWristStamp=double.NaN; armed=sliding=walking=movedThisAction=false;
            slideRemaining=0; remoteRearm=false;
        }
        void EndWalkStride()
        {
            if (!walking || !movedThisAction) return;
            movedThisAction = false;
            ReportMovement(PlayerMovementKind.Walk, Vector3.zero);
            ExplorationTravelled?.Invoke(0f);
        }
        static bool TryHorizontal(Vector3 forward,out Vector3 heading)
        {
            heading=Vector3.ProjectOnPlane(forward,Vector3.up);
            if(!RpgGestureState.Finite(heading) || heading.sqrMagnitude<.01f)return false;
            heading.Normalize();return true;
        }
        bool Fresh(bool valid,double stamp,double now)=>valid && double.IsFinite(stamp) && now>=stamp && now-stamp<=gestures.sampleMaxAge;
        bool ProcessEncounter(NavigationHandInput f,bool held,bool blocked,double now)
        {
            walkContact = null;
            double delta=now-lastProcess;
            lastProcess=now;
            float dt=double.IsFinite(delta) && delta>0 && delta<=gestures.sampleMaxGap ? (float)Math.Min(delta,.05) : 0;
            bool left=Fresh(f.leftValid,f.leftObserved,now);
            // A lowered wrist or missing tracking does not count as releasing the pointing pose.
            if(needNeutral && left && !rawInput.leftPoseUnconfirmed && !rawInput.leftPointing && !rawInput.leftNavigationPointing)needNeutral=false;
            if(blocked || needNeutral)
            {
                ResetEncounterMotion(); rayState.Reset(); CandidateNode=null; debugCandidates.Clear();
                if (!blocked && PickingGround && left && !SealProtectionActive && !f.bothHandsLow)
                    UpdateGroundAimGuide(f, out _);
                motionReason=Status=blocked?"输入被占用或取消":"模式切换，等待有效收指";
                return true;
            }
            if(!EncounterCombat)
            {
                ProcessExploration(f, left, dt, now);
                return true;
            }
            if(sliding)
            {
                rayState.Suspend(); CandidateNode=null;
                return AdvanceSlide(dt);
            }
            // New semantic: right empty hand only; do not reuse legacy four-direction palm dodge.
            if(held || f.rightMotionOwnedBySpell || SealProtectionActive || !Fresh(f.rightValid,f.rightObserved,now) ||
                !TryHorizontal(head.forward,out var look))
            {
                armed=false; lastWristStamp=double.NaN;
                motionReason=held||f.rightMotionOwnedBySpell?"右手持有技能":SealProtectionActive?"双手结印保护":"右腕追踪失效或头向近垂直";return false;
            }
            Vector3 relative=f.rightWrist-head.position;
            double stamp=f.rightObserved;
            if(stamp==lastWristStamp)return false;
            bool continuous=double.IsFinite(lastWristStamp) && stamp>lastWristStamp && stamp-lastWristStamp<=gestures.sampleMaxGap;
            bool inChest=(relative - (Vector3.down*chestBelowHead + look*.16f)).sqrMagnitude<=chestRadius*chestRadius;
            float speed=continuous?(relative-lastRelative).magnitude/(float)(stamp-lastWristStamp):0;
            lastRelative=relative; lastWristStamp=stamp;
            if(!continuous)armed=false;
            if(shotGuard)
            {
                if(stamp<=shotAt || (!inChest && now-shotAt<throwDodgeRecovery))
                { motionReason="发射保护：等待回胸或计时";return false; }
                shotGuard=false; remoteRearm=!inChest;armed=false;
                // Discard this sample entirely. No portion of the launching swipe can seed new motion.
                motionReason="保护结束：等待新的快推";return false;
            }
            if(lastNow<nextDodgeAt){ armed=false;motionReason="闪避冷却中";return false; }
            if(armed)
            {
                float distance=Vector3.Dot(relative-pushAnchor,pushHeading);
                double elapsed=stamp-anchorStamp;
                if(elapsed>0 && elapsed<=pushWindow && distance>=pushDistance && distance/elapsed>=pushSpeed)
                { armed=false;remoteRearm=false;return StartSlide(pushHeading,now); }
                if(elapsed>pushWindow || distance<-.04f)armed=false;
            }
            if((inChest || remoteRearm && continuous && speed<.22f) && (!armed || speed<.22f))
            { armed=true; pushAnchor=relative; pushHeading=look; anchorStamp=stamp; }
            motionReason=armed?"已准备，等待向前快推":remoteRearm?"发射保护结束，先稳定右手再快推":"右手回到胸前准备";
            return false;
        }
        bool StartSlide(Vector3 heading,double now)
        {
            if(sliding || shotGuard || lastNow<nextDodgeAt)return false;
            sliding=true;slideRemaining=Mathf.Max(.05f,slideDuration);slideDirection=-heading;slideDelta=Vector3.zero;
            lastProcess=now;movedThisAction=false;rayState.Suspend();State.InterruptDwell();CandidateNode=null;
            motionReason=Status="滑退开始";return true;
        }
        bool AdvanceSlide(float dt)
        {
            float step=(float)Math.Min(slideRemaining,dt);
            if(step<=0)return true;
            if(!TrySmoothStep(slideDirection*(step/Mathf.Max(.05f,slideDuration))*dodgeDistance,out var moved,out string reason))
            {
                sliding=false;motionReason=Status="滑退停止："+reason;
                RpgFeedbackSignals.Raise(Feedback,RpgActionCue.Blocked,this);
            }
            else
            {
                if(slideDelta.sqrMagnitude<.0000001f)
                { nextDodgeAt=lastNow+Math.Max(0,dodgeCooldown); RpgFeedbackSignals.Raise(Feedback,RpgActionCue.Dodged,this); }
                slideDelta+=moved;slideRemaining-=step;
                sliding=slideRemaining>.00001; motionReason=Status=sliding?"正在向后滑退":"滑退完成";
            }
            if(!sliding) { if(slideDelta.sqrMagnitude>0)Dodged?.Invoke(slideDelta);movedThisAction=false; }
            return true;
        }
        // Continuous walking samples physics each frame. Reuse bounded query buffers; saturation fails closed.
        readonly RaycastHit[] smoothHits=new RaycastHit[32];
        readonly Collider[] smoothOverlaps=new Collider[32];
        bool SmoothFloor(Vector3 position,out RaycastHit floor,out string reason)
        {
            floor=default;reason="未找到合法地面";float best=float.PositiveInfinity;
            int count=Physics.RaycastNonAlloc(position+Vector3.up*floorSearchUp,Vector3.down,smoothHits,floorSearchUp+floorSearchDown,floorLayers,QueryTriggerInteraction.Ignore);
            if(count==smoothHits.Length){reason="地面碰撞密集";return false;}
            for(int i=0;i<count;i++){
                var hit=smoothHits[i];float gap=Mathf.Abs(hit.point.y-position.y);
                if(Ignored(hit.collider) || Vector3.Angle(hit.normal,Vector3.up)>maximumSlope || gap>maximumFloorStep || gap>=best)continue;
                floor=hit;best=gap;
            }
            if(!floor.collider)return false;
            reason=null;return true;
        }
        void SmoothCapsule(RaycastHit floor, Vector3 current, float inset, out Vector3 bottom, out Vector3 top, out float radius)
        {
            Capsule(floor, current, out bottom, out top, out radius);
            // Preserve foot and head clearance when a room-scale offset starts against an edge.
            bottom -= Vector3.up * inset;
            top += Vector3.up * inset;
            radius -= inset;
        }
        bool SmoothClear(RaycastHit floor,Vector3 current,float inset,out Collider overlap,out string reason)
        {
            overlap = null;
            SmoothCapsule(floor,current,inset,out var bottom,out var top,out var radius);
            int count=Physics.OverlapCapsuleNonAlloc(bottom,top,radius,smoothOverlaps,obstacleLayers,QueryTriggerInteraction.Ignore);
            if(count==smoothOverlaps.Length){reason="身体附近碰撞密集";return false;}
            for (int i = 0; i < count; i++)
            {
                if (Ignored(smoothOverlaps[i])) continue;
                overlap = smoothOverlaps[i];
                reason = "身体占位被阻挡";
                return false;
            }
            reason=null;return true;
        }
        bool TrySmoothLeg(RaycastHit from, Vector3 initialFloor, Vector3 delta, float inset,
            out RaycastHit target, out RaycastHit obstacle, out string reason)
        {
            obstacle = default;
            if (!SmoothFloor(from.point + delta, out target, out reason)) return false;
            if (Mathf.Abs(target.point.y - initialFloor.y) > maximumFloorStep) { reason = "楼层高度差过大"; return false; }
            Vector3 travel = target.point - from.point;
            SmoothCapsule(from,initialFloor,inset,out var bottom,out var top,out var radius);
            int count=Physics.CapsuleCastNonAlloc(bottom,top,radius,travel.normalized,smoothHits,travel.magnitude,obstacleLayers,QueryTriggerInteraction.Ignore);
            if(count==smoothHits.Length){reason="路径碰撞密集";return false;}
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                var hit = smoothHits[i];
                if (Ignored(hit.collider) || hit.distance >= nearest) continue;
                nearest = hit.distance;
                obstacle = hit;
            }
            if (obstacle.collider) { reason = "路径有障碍"; return false; }
            if (SmoothClear(target, initialFloor, inset, out var overlap, out reason)) return true;
            if (walking && overlap) walkContact = overlap;
            return false;
        }
        bool TryWalkAlongSurface(RaycastHit from, Vector3 delta, float inset, RaycastHit obstacle,
            RaycastHit intendedFloor, out RaycastHit destination, out string reason)
        {
            destination = from;
            reason = "路径有障碍";
            float length = Vector3.Distance(intendedFloor.point, from.point);
            if (length < .0001f) return false;

            // Recheck both legs against the floor and any other colliders before committing the rig once.
            const float contactGap = .001f;
            float fraction = Mathf.Clamp01((obstacle.distance - contactGap) / length);
            if (fraction > 0 && TrySmoothLeg(from, from.point, delta * fraction, inset, out var approach, out _, out _))
                destination = approach;

            Vector3 normal = Vector3.ProjectOnPlane(obstacle.normal, Vector3.up).normalized;
            if (Vector3.Dot(delta, normal) < 0 && Mathf.Abs(obstacle.normal.y) < .95f)
            {
                Vector3 along = Vector3.ProjectOnPlane(delta * (1f - fraction), normal);
                if (along.sqrMagnitude > .00000001f &&
                    TrySmoothLeg(destination, from.point, along, inset, out var end, out _, out _))
                    destination = end;
            }
            return (destination.point - from.point).sqrMagnitude > .00000001f;
        }
        bool TrySmoothStep(Vector3 delta,out Vector3 moved,out string reason)
        {
            moved = Vector3.zero;
            if (!SmoothFloor(ExpectedFloor, out var from, out reason)) return false;
            float inset = 0f;
            if (!SmoothClear(from, from.point, inset, out var overlap, out reason))
            {
                if (!walking || !overlap) return false;
                walkContact = overlap;
                inset = Mathf.Min(Mathf.Clamp(skin, 0f, .025f), Mathf.Max(.05f, bodyRadius) * .2f);
                if (!SmoothClear(from, from.point, inset, out _, out reason)) return false;
            }
            if (!TrySmoothLeg(from, from.point, delta, inset, out var target, out var obstacle, out reason))
            {
                if (!walking || !obstacle.collider) return false;
                walkContact = obstacle.collider;
                if (!TryWalkAlongSurface(from, delta, inset, obstacle, target, out var destination, out reason)) return false;
                target = destination;
            }
            moved = target.point - from.point;
            var previous = rigRoot.position;
            rigRoot.position += moved;
            if (walking)
                moved = rigRoot.position - previous;
            State.TranslateAnchors(moved);
            if (walking && moved.x == 0f && moved.z == 0f)
            {
                reason = "没有水平位移";
                return false;
            }
            ReportMovement(walking ? PlayerMovementKind.Walk : PlayerMovementKind.Dodge, moved);
            if(!movedThisAction){movedThisAction=true;movedEvent.Publish(moved, this);}
            reason=null;return true;
        }
    }
}
