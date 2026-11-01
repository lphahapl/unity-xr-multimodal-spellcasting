using System;
using UnityEngine;

namespace NarutoVR.SealCombat.Rpg
{
    public enum WristMenuActivation { [InspectorName("抬腕后右手点腕饰开关")] TapWrist, [InspectorName("旧版握拳抬腕（对照实验）")] RaiseFist }
    public enum WristPanelKind { [InspectorName("结印")] Seal, [InspectorName("画符")] Glyph, [InspectorName("咒语")] Voice }
    [Serializable]
    public sealed class WristPanelPolicy
    {
        [Header("子面板保留与交互")]
        [InspectorName("放下左腕时关闭")] public bool closeWithWrist;
        [InspectorName("允许射线捏合翻页或关闭")] public bool allowRay = true;
        [InspectorName("允许食指近距离点按")] public bool allowPoke = true;
    }

    [Serializable]
    public sealed class WristMenuSettings
    {
        [Header("入口方式与腕饰点按")]
        [InspectorName("入口方式")] public WristMenuActivation activation = WristMenuActivation.TapWrist;
        [InspectorName("腕饰相对腕骨偏移（米）")] public Vector3 decorationOffset;
        [InspectorName("接触半径（米）"), Range(.025f,.08f)] public float tapRadius = .055f;
        [InspectorName("离开后重备半径（米）"), Range(.07f,.15f)] public float tapReleaseRadius = .10f;
        [InspectorName("点腕优先区域（米）"), Range(.12f,.3f)] public float tapReserveRadius = .22f;
        [InspectorName("点腕接触确认（秒）"), Range(.01f,.1f)] public float tapHold = .025f;
        [InspectorName("抬腕稳定确认（秒）"), Range(.04f,.25f)] public float tapReadyHold = .08f;
        [InspectorName("右指主动靠近距离（米）"), Range(.01f,.06f)] public float tapApproachDistance = .02f;
        [Header("轻捏目标保护")]
        [InspectorName("高亮目标稳定确认（秒）"), Range(.02f,.15f)] public float rayHoverHold = .04f;
        [InspectorName("捏合弯指时保留目标（秒）"), Range(.04f,.2f)] public float rayTargetGrace = .12f;
        [InspectorName("捏合时掌部移动上限（米）"), Range(.02f,.12f)] public float rayCaptureDistance = .06f;
        [Header("旧版握拳入口（仅对照模式）")]
        [InspectorName("开启保持秒数"), Range(.15f,.8f)] public float openHold = .24f;
        [Header("抬腕可达区域与放腕关闭")]
        [InspectorName("抬腕：低于头部最多（米）"), Range(.1f,.6f)] public float raiseBelowHead = .50f;
        [InspectorName("放腕：低于头部至少（米）"), Range(.4f,.9f)] public float lowerBelowHead = .66f;
        [InspectorName("注意区域半角（度）"), Range(20,70)] public float attentionAngle = 60;
        [InspectorName("相对头部的稳定范围（米）"), Range(.02f,.15f)] public float stableRadius = .12f;
        [InspectorName("要求手背或掌面朝向头显"), Tooltip("默认关闭：无需扭腕对准。握拳、胸前高度、注意范围和动作冲突门控仍然有效。")]
        public bool requireWristFacing;
        [InspectorName("短暂手形/角度波动容忍（秒）"), Range(0,.15f)] public float poseGrace = .09f;
        [InspectorName("放腕关闭确认（秒）"), Range(.05f,.3f)] public float lowerHold = .12f;
        [InspectorName("再次唤起前左手释放确认（秒）"), Range(.05f,.25f)] public float reopenRelease = .1f;
        [Header("追踪与交接")]
        [InspectorName("样本最长年龄（秒）"), Range(.05f,.3f)] public float maxAge = .15f;
        [InspectorName("最大观测间隔（秒）"), Range(.05f,.4f)] public float maxGap = .18f;
        [InspectorName("丢追踪后隐藏入口（秒）"), Range(.1f,1)] public float lostGrace = .35f;
        [InspectorName("放下或放松左手后交回战斗（秒）"), Range(.1f,.5f)] public float neutralHold = .18f;
        [Header("射线与捏合")]
        [InspectorName("射线最大距离（米）"), Range(.4f,3)] public float rayDistance = 2;
        [InspectorName("轻捏确认距离（米）"), Range(.01f,.04f)] public float pinchClose = .03f;
        [InspectorName("捏合松开距离（米）"), Range(.03f,.07f)] public float pinchOpen = .045f;
    }

    /// <summary>Platform adapter supplies world poses and observation timestamps. No Meta/XRI dependency.</summary>
    public struct WristMenuInput
    {
        public bool leftValid, leftClosed, rightValid, rightPointing, rightOpen, rightClosed, leftPointing, wristFacing;
        public double leftObserved, rightObserved;
        public Vector3 leftWrist, rightTip, rayOrigin, rayDirection, headPosition, headForward;
        public Quaternion leftRotation;
        public bool rayValid, rayAnchorValid, pinchValid;
        public float pinchDistance;
        public string source;
    }

    public sealed class WristUiTarget
    {
        public string id;
        public RectTransform rect;
        public WristUiTarget(string id, RectTransform rect) { this.id = id; this.rect = rect; }
    }

    /// <summary>Input ownership, visibility and neutral rearming; never emits gameplay commands.</summary>
    public sealed class WristMenuState
    {
        public bool LauncherOpen { get; private set; }
        public bool OwnsInput { get; private set; }
        public bool LeftLow { get; private set; }
        public bool CanInteract { get; private set; }
        public string Status { get; private set; } = "抬左腕，用右食指点腕饰";
        public float OpenProgress { get; private set; }
        double lastLeft = -1, lastNow = -1, lostSince = -1, releaseSince = -1, lowSince = -1, missSince = -1;
        double earned;
        Vector3 anchor;
        bool rearm, reopenBlocked, arming, previousPose, exitQualified;
        double tapHandoff = -1;
        float tapOpenHeight;

        public void Reset(bool requireNeutral = true)
        {
            LauncherOpen = CanInteract = LeftLow = false;
            rearm = requireNeutral; OwnsInput = requireNeutral; reopenBlocked = requireNeutral;
            lastLeft = lastNow = lostSince = releaseSince = lowSince = -1; exitQualified = false;
            ClearArming();
            tapHandoff = -1;
        }

        void ClearArming() { arming = previousPose = false; earned = 0; missSince = -1; OpenProgress = 0; }

        public void Tick(WristMenuInput f, bool busy, bool pointerOwns, double now, WristMenuSettings s, bool wristToggle = false)
        {
            if (s.activation == WristMenuActivation.TapWrist) { TickTap(f,busy,pointerOwns,now,s,wristToggle); return; }
            if (!double.IsFinite(now) || (lastNow >= 0 && now < lastNow)) { Reset(); return; }
            lastNow = now;
            bool valid = Fresh(f.leftValid, f.leftObserved, now, s.maxAge) && Finite(f.leftWrist) && Finite(f.headPosition) &&
                Finite(f.headForward) && f.headForward.sqrMagnitude > .1f;
            bool right = Fresh(f.rightValid, f.rightObserved, now, s.maxAge);
            CanInteract = right && !busy; // A retained card remains operable with the left wrist out of view.
            bool newLeft = valid && f.leftObserved > lastLeft;
            if (valid && f.leftObserved < lastLeft) { Reset(); return; }
            Vector3 offset = f.leftWrist-f.headPosition;
            bool low = valid && offset.y < -Math.Max(s.lowerBelowHead,s.raiseBelowHead+.08f);
            bool continuity = valid && lostSince < 0 && (lastLeft < 0 || f.leftObserved-lastLeft <= s.maxGap);
            if (!valid || !continuity)
            {
                bool abandonedUi = OwnsInput || arming || LauncherOpen;
                ClearArming(); lowSince = releaseSince = -1; LeftLow = false;
                // Qualified deliberate release survives later loss; loss cannot create that qualification.
                if (abandonedUi) reopenBlocked = true;
            }
            if (newLeft)
            {
                if (low)
                {
                    if (lowSince < 0) lowSince = f.leftObserved;
                    LeftLow = f.leftObserved-lowSince >= s.lowerHold;
                }
                else { lowSince = -1; LeftLow = false; }
                bool released = low || (!f.leftClosed && !f.leftPointing);
                if (released)
                {
                    if (releaseSince < 0) releaseSince = f.leftObserved;
                    if (f.leftObserved-releaseSince >= s.reopenRelease) reopenBlocked = false;
                    if (f.leftObserved-releaseSince >= s.neutralHold) exitQualified = true;
                }
                else { releaseSince = -1; exitQualified = false; }
            }
            if (busy)
            {
                if (OwnsInput) rearm = true;
                LauncherOpen = false; ClearArming(); releaseSince = -1; exitQualified = false;
                if (newLeft) lastLeft = f.leftObserved;
                lostSince = valid ? -1 : now;
                OwnsInput = rearm; reopenBlocked = true; Status = "现有动作优先，腕部入口暂不可用"; return;
            }
            if (!valid)
            {
                if (lostSince < 0) lostSince = now;
                if (LauncherOpen && now - lostSince >= s.lostGrace) { LauncherOpen = false; rearm = true; reopenBlocked = true; }
                Status = "左手移回视野，再放松手指";
            }
            else if (newLeft)
            {
                lostSince = -1;
                if (LeftLow && LauncherOpen) { LauncherOpen = false; rearm = true; }
                bool pose = f.leftClosed && (!s.requireWristFacing || f.wristFacing) && !reopenBlocked &&
                    offset.y >= -s.raiseBelowHead && offset.y <= .2f && offset.magnitude >= .15f && offset.magnitude <= .85f &&
                    Vector3.Angle(f.headForward, offset) <= s.attentionAngle;
                // Re-opening only needs a fresh left-hand intent, even while combat handoff is pending.
                if (!LauncherOpen && pose)
                {
                    if (!arming || Vector3.Distance(anchor,offset) > s.stableRadius || (missSince >= 0 && f.leftObserved-missSince > s.poseGrace))
                    { ClearArming(); arming = true; anchor = offset; }
                    else if (previousPose && continuity) earned += Math.Max(0,f.leftObserved-lastLeft);
                    previousPose = true; missSince = -1;
                    OpenProgress = Mathf.Clamp01((float)(earned/Math.Max(.1,s.openHold)));
                    Status = $"保持一下 · {OpenProgress:P0}";
                    if (OpenProgress >= 1) { LauncherOpen = true; ClearArming(); Status = "已打开，可以放松左手手指"; }
                }
                else if (!LauncherOpen && arming)
                {
                    previousPose = false;
                    if (missSince < 0) missSince = f.leftObserved;
                    if (f.leftObserved-missSince > s.poseGrace) ClearArming();
                    Status = "稍稳一下，进度暂时保留";
                }
                else
                {
                    ClearArming();
                    if (!LauncherOpen) Status = reopenBlocked ? "放松左手，再握拳抬腕" :
                        !f.leftClosed ? "左手握拳抬到胸前" : offset.y < -s.raiseBelowHead ? "再抬高一点即可" :
                        Vector3.Angle(f.headForward,offset)>s.attentionAngle ? "稍微看向左腕" : "左手握拳抬到胸前";
                }
                lastLeft = f.leftObserved;
            }

            bool active = LauncherOpen || arming || pointerOwns;
            if (OwnsInput && !active) rearm = true;
            if (!active && rearm && exitQualified) { rearm = false; Status = "已交回战斗输入"; }
            OwnsInput = active || rearm;
        }

        void TickTap(WristMenuInput f,bool busy,bool interacting,double now,WristMenuSettings s,bool toggle)
        {
            if (!double.IsFinite(now) || (lastNow>=0 && now<lastNow)) { Reset(); return; }
            lastNow=now; OpenProgress=0;
            bool left=Fresh(f.leftValid,f.leftObserved,now,s.maxAge) && Finite(f.leftWrist) && Finite(f.headPosition);
            CanInteract=Fresh(f.rightValid,f.rightObserved,now,s.maxAge) && !busy;
            if (!left) { lowSince=-1; LeftLow=false; if(lostSince<0) lostSince=now; }
            else if(f.leftObserved>lastLeft)
            {
                bool interrupted=lastLeft>=0 && f.leftObserved-lastLeft>s.maxGap;
                if(interrupted) lowSince=-1;
                bool low=f.leftWrist.y<f.headPosition.y-s.lowerBelowHead || (LauncherOpen && f.leftWrist.y-f.headPosition.y<tapOpenHeight-.18f);
                // Hiding is safe after a gap; never require a fresh sequence merely to dismiss UI.
                if(low) { if(lowSince<0) lowSince=f.leftObserved; LeftLow=interrupted || f.leftObserved-lowSince>=s.lowerHold; }
                else { lowSince=-1; LeftLow=false; }
                lastLeft=f.leftObserved; lostSince=-1;
            }
            else if(f.leftObserved<lastLeft) { Reset(); return; }
            if(busy || LeftLow || (!left && lostSince>=0 && now-lostSince>=s.lostGrace)) LauncherOpen=false;
            else if(toggle && CanInteract && left) { if(!LauncherOpen) tapOpenHeight=f.leftWrist.y-f.headPosition.y; LauncherOpen=!LauncherOpen; }
            bool active=LauncherOpen || (!busy && interacting);
            if(active) { tapHandoff=-1; rearm=false; OwnsInput=true; }
            else if(OwnsInput)
            {
                if(tapHandoff<0) tapHandoff=now;
                if(now-tapHandoff>=.12) { OwnsInput=false; rearm=false; }
            }
            Status=busy ? "当前动作优先" : LauncherOpen ? "再点一次腕饰可关闭" : "抬左腕，用右食指点腕饰";
        }

        public static bool Fresh(bool valid, double observed, double now, double maxAge) => valid && double.IsFinite(observed) && now >= observed && now-observed <= maxAge;
        public static bool Finite(Vector3 v) => float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);
    }

    /// <summary>Two-hand contact switch; left hand shape is deliberately irrelevant.</summary>
    public sealed class WristTapSwitch
    {
        public bool Approaching { get; private set; }
        public bool Toggle { get; private set; }
        public bool Ready { get; private set; }
        public float Distance { get; private set; }
        public string Status { get; private set; } = "抬左腕，用右食指点腕饰";
        double lastLeft=-1,lastRight=-1,readySince=-1,contactSince=-1;
        Vector3 leftAnchor,tipStart,targetStart;
        bool armed,pressed;
        public void Reset() { lastLeft=lastRight=readySince=contactSince=-1; armed=pressed=Approaching=Toggle=Ready=false; }
        public void Step(WristMenuInput f,Vector3 center,bool blocked,double now,WristMenuSettings s)
        {
            Toggle=false;
            bool left=WristMenuState.Fresh(f.leftValid,f.leftObserved,now,s.maxAge);
            bool pair=left && WristMenuState.Fresh(f.rightValid,f.rightObserved,now,s.maxAge) &&
                Math.Abs(f.leftObserved-f.rightObserved)<=.08 && WristMenuState.Finite(f.rightTip) && WristMenuState.Finite(center);
            Vector3 offset=center-f.headPosition;
            bool raised=left && WristMenuState.Finite(f.headForward) && f.headForward.sqrMagnitude>.1f &&
                offset.y>=-s.raiseBelowHead && offset.y<=.2f && offset.magnitude>=.08f && offset.magnitude<=1f;
            // Deliberate cross-hand contact is the intent signal; looking down/leaning over the wrist
            // must not reset it merely because the wrist crosses behind the HMD's forward plane.
            Distance=pair ? Vector3.Distance(center,f.rightTip) : float.NaN;
            if(blocked || !pair || !raised) { Reset(); Status=blocked?"当前动作优先":!pair?"双手追踪无效或不同步":"左腕高度或离头距离不在范围"; return; }
            if((lastLeft>=0 && (f.leftObserved<lastLeft || f.leftObserved-lastLeft>s.maxGap)) ||
                (lastRight>=0 && (f.rightObserved<lastRight || f.rightObserved-lastRight>s.maxGap))) { Reset(); return; }
            if(f.leftObserved<=lastLeft || f.rightObserved<=lastRight) return;
            lastLeft=f.leftObserved; lastRight=f.rightObserved;
            double stamp=Math.Min(lastLeft,lastRight);
            if(readySince<0 || Vector3.Distance(leftAnchor,offset)>.06f)
            { readySince=stamp; leftAnchor=offset; armed=pressed=Approaching=false; contactSince=-1; }
            Ready=stamp-readySince>=s.tapReadyHold;
            Distance=Vector3.Distance(center,f.rightTip);
            Vector3 tip=f.rightTip-f.headPosition;
            if(!Ready) { Status="左腕稍稳一下"; return; }
            if(Distance>=s.tapReleaseRadius)
            {
                if(!armed || pressed || Distance>s.tapReserveRadius) { tipStart=tip; targetStart=offset; }
                armed=true; pressed=false; contactSince=-1;
            }
            float movement=Vector3.Dot(tip-tipStart,(targetStart-tipStart).normalized);
            bool intent=armed && movement>=s.tapApproachDistance;
            Approaching=Distance<s.tapReserveRadius && (intent || pressed);
            if(pressed) { Status="已接触，右指离开后可再点"; return; }
            if(!intent || Distance>s.tapRadius) { contactSince=-1; Status=Approaching?"点向腕饰":"右食指点腕饰：开 / 关"; return; }
            if(contactSince<0) contactSince=stamp;
            if(stamp-contactSince<s.tapHold) return;
            pressed=true; Toggle=true; Approaching=true; Status="腕饰点按已确认";
        }
    }

    /// <summary>Pinch-down selects the last stable open-hand hover; release only rearms. No SDK dependency.</summary>
    public sealed class WristRayPinchState
    {
        public string Target { get; private set; }
        public Vector3 Point { get; private set; }
        public bool Pressed { get; private set; }
        public string Status { get; private set; } = "先指向按钮，再轻捏";
        bool armed;
        double last=-1, since=-1, seen=-1;
        Vector3 anchor;
        public void ClearTarget() { Target=null; since=seen=-1; }
        public void Reset() { ClearTarget(); armed=Pressed=false; last=-1; }
        public string Step(string hover,Vector3 point,WristMenuInput f,double now,WristMenuSettings s)
        {
            if (!WristMenuState.Fresh(f.rightValid,f.rightObserved,now,s.maxAge) || !f.pinchValid ||
                !float.IsFinite(f.pinchDistance) || f.rightClosed || (!f.rayAnchorValid && !f.rayValid) || !WristMenuState.Finite(f.rayOrigin))
            { Reset(); Status="右手追踪或捏合不可用"; return null; }
            if(last>=0 && (f.rightObserved<last || f.rightObserved-last>s.maxGap)) Reset();
            if(f.rightObserved==last) return null;
            last=f.rightObserved;
            if(f.pinchDistance>=s.pinchOpen)
            {
                armed=true; Pressed=false;
                if(hover!=Target && (hover!=null || last-seen>s.rayTargetGrace || Vector3.Distance(anchor,f.rayOrigin)>s.rayCaptureDistance))
                { ClearTarget(); Target=hover; since=last; }
                if(hover!=null) { Point=point; anchor=f.rayOrigin; seen=last; }
                Status=hover==null?"先指向按钮":"高亮后轻捏即可确认";
                return null;
            }
            if(f.pinchDistance>s.pinchClose || !armed) return null;
            armed=false; Pressed=true;
            string result=Target!=null && last-since+1e-6>=s.rayHoverHold && last-seen<=s.rayTargetGrace &&
                Vector3.Distance(anchor,f.rayOrigin)<=s.rayCaptureDistance ? Target : null;
            Status=result==null?"目标未稳定或已离开：松开后重试":"轻捏已确认，松开后可再点";
            return result;
        }
    }

    /// <summary>Near-poke state. Legacy far-release branch retained for comparison tests only.</summary>
    public sealed class WristPointerState
    {
        public string Hover { get; private set; }
        public bool Pressed { get; private set; }
        public bool Captured => Pressed;
        bool armed, poke;
        double observed = -1;
        public void Reset() { Hover = null; Pressed = armed = poke = false; observed = -1; }
        public string Step(string id, bool near, float depth, bool closed, bool released, double time, float maxGap)
        {
            if (!double.IsFinite(time)) { Reset(); return null; }
            if (time < observed || (observed >= 0 && time-observed > maxGap)) Reset();
            if (time == observed) return null;
            observed = time;
            if (id != Hover || near != poke) { Hover = id; poke = near; Pressed = armed = false; }
            if (id == null) return null;
            if (near)
            {
                if (depth <= -.015f) { armed = true; Pressed = false; }
                if (armed && !Pressed && depth >= -.004f && depth <= .018f) { Pressed = true; armed = false; return id; }
            }
            else
            {
                if (Pressed && released) { Pressed = false; armed = true; return id; }
                if (released) armed = true;
                if (armed && closed) { Pressed = true; armed = false; }
            }
            return null;
        }
    }
}
