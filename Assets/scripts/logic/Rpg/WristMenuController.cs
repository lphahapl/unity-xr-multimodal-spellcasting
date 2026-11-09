using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.XR;

namespace NarutoVR.SealCombat.Rpg
{
    /// <summary>Local UI orchestrator. The RPG calls Tick before locomotion/effects/matching.</summary>
    [DisallowMultipleComponent]
    public sealed class WristMenuController : MonoBehaviour
    {
        /// <summary>可选教学内容目录，只影响配方分组和显示文案。</summary>
        public TeachingCatalog teachingCatalog;
        /// <summary>训练界面可展示尚未了解的条目；仅改变可见性，不授予任何施法许可。</summary>
        public bool showUndiscoveredEntries;
        [Header("一、界面与输入配置")]
        [InspectorName("腕饰与面板表现")] public WristMenuView view;
        [InspectorName("输入阈值")] public WristMenuSettings settings = new();
        [Header("二、每类面板的关闭方式")]
        [InspectorName("结印教学卡")] public WristPanelPolicy sealPolicy = new();
        [InspectorName("画符预览卡")] public WristPanelPolicy glyphPolicy = new();
        [InspectorName("咒语速查卡")] public WristPanelPolicy voicePolicy = new();
        [Header("三、世界空间布局")]
        [InspectorName("入口相对手腕偏移（米）")] public Vector3 launcherOffset = new(.08f,.07f,.05f);
        [InspectorName("教学卡距头部（米）"), Range(.4f,1.2f)] public float panelDistance = .65f;
        [InspectorName("教学卡低于头部（米）"), Range(0,.4f)] public float panelLower = .12f;

        [Header("准望匣佩戴与读数")]
        [InspectorName("左腕准望匣")] public ZhunwangPressButton wornDevice;
        [InspectorName("腕骨至匣原点（米）")] public Vector3 deviceOffset = new(-.003f,-.020f,-.052f);
        [InspectorName("匣体佩戴角度")] public Vector3 deviceEuler;
        bool deviceToggle, waitForWithdrawal, recoveryFailed;
        bool TogglePressed => wornDevice ? deviceToggle : tap.Toggle;
        public WristMenuState State { get; } = new();
        public event Action<bool> WristTapConfirmed;
        public bool OwnsInput => isActiveAndEnabled && State.OwnsInput;
        public bool PanelOpen { get; private set; }
        public WristPanelKind PanelKind { get; private set; }
        public int Page { get; private set; }
        public bool FollowPlayer { get; private set; }
        bool dragging, dragPaused;
        double dragLastGood;
        string dragRaySource,dragPinchSource,lastRayCommand;
        public string DragStatus {get;private set;}="未拖动";
        Vector3 dragOffset;
        float dragDistance;
        Pose localPanelPose;
        public int ClickCount { get; private set; }
        public string LastAction { get; private set; } = "等待腕部入口";
        public bool PointerVisible { get; private set; }
        public string PointerStatus { get; private set; } = "等待输入";
        public string OwnershipReason { get; private set; } = "未操作腕部界面";
        public string DebugDetails => $"腕部入口={State.LauncherOpen}，UI占用={OwnsInput}（{OwnershipReason}），唤起={State.OpenProgress:P0}\n" +
            $"子面板={(PanelOpen ? Title(PanelKind) : "无")}，页码={Page+1}，指针={pointer.Hover ?? ray.Target ?? "无"}，确认次数={ClickCount}\n" +
            $"{WristContactDebug()}；轻捏={PinchStatus(input)}\n{ray.Status}\n{PointerStatus}\n{State.Status}；{LastAction}";
        readonly WristPointerState pointer = new();
        readonly WristTapSwitch tap = new();
        readonly WristRayPinchState ray = new();
        bool confirmedRayPress;
        double preparedAt = -1;
        WristMenuInput input;
        /// <summary>Value copy of the latest consumed input. Reading never advances or mutates interaction.</summary>
        public WristMenuInput LatestInput => input;
        Pose panelPose;
        LinearRpgController owner;
        string source;
        bool shown, pinched, frameEstablished;
        Vector3 rigPosition;
        Quaternion rigRotation;
        double nextContent;
        string lastBody, lastFooter;
        readonly WristTeachingLibrary teaching = new();
        WristTeachingLesson shownLesson;
        int shownStep, shownRevision;
        int launcherRevision = -1;
        LinearRpgController launcherOwner;
        string launcherProgress;
        readonly List<XRInputSubsystem> originSources = new();
        readonly List<XRInputSubsystem> foundSources = new();
        readonly RaycastHit[] occlusionHits = new RaycastHit[32];
        double nextOriginScan;

        public WristPanelPolicy Policy(WristPanelKind kind) => kind == WristPanelKind.Seal ? sealPolicy : kind == WristPanelKind.Glyph ? glyphPolicy : voicePolicy;

        string WristContactDebug()
        {
            if (!wornDevice) return $"点腕={tap.Status} 距离={tap.Distance*100:0.0}厘米";
            if (!wornDevice.gameObject.activeInHierarchy) return "准望匣未定位";
            if (!input.rightValid || input.tipUnavailable) return "朱砂扣等待右食指追踪";
            float distance = Vector3.Distance(input.rightTip, wornDevice.RestSurface);
            return $"朱砂扣={(waitForWithdrawal ? "等待退指" : "待按")}，指尖距扣面中心 {distance*1000:0} 毫米";
        }

        string PinchStatus(WristMenuInput f)
        {
            if(!f.pinchValid)return "无数据";
            if(f.systemPinch)return f.pinchTransition?"中间态":f.systemPressed?"系统按下":"系统松开";
            return $"{f.pinchDistance*1000:0}毫米";
        }

        Vector3 DecorationCenter(WristMenuInput f) => f.leftWrist+f.leftRotation*settings.decorationOffset;
        // Observe approach even during navigation/proximity protection. Only a confirmed tap
        // may override those guards; raising or pointing toward the wrist owns no gameplay input.
        public bool PrepareWristTap(WristMenuInput sample,bool hardBlocked,double now)
        {
            preparedAt=now;
            deviceToggle=false;
            if (wornDevice)
            {
                bool tracked = sample.mountValid && WristMenuState.Fresh(sample.leftValid,sample.leftObserved,now,settings.maxAge);
                if (wornDevice.gameObject.activeSelf != tracked) wornDevice.gameObject.SetActive(tracked);
                if (tracked)
                    wornDevice.transform.SetPositionAndRotation(sample.leftWrist + sample.mountRotation * deviceOffset,
                        sample.mountRotation * Quaternion.Euler(deviceEuler));
                bool right = !sample.tipUnavailable && WristMenuState.Finite(sample.rightTip) &&
                    WristMenuState.Fresh(sample.rightValid,sample.rightObserved,now,settings.maxAge) &&
                    Math.Abs(sample.leftObserved-sample.rightObserved)<=.08;
                if (!tracked || !right || hardBlocked || dragging || sample.leftWrist.y < sample.headPosition.y-settings.raiseBelowHead)
                {
                    wornDevice.ResetContact();
                    return false;
                }
                if (waitForWithdrawal)
                {
                    if (Vector3.Distance(wornDevice.RestSurface, sample.rightTip) < wornDevice.touchRadius+.012f)
                    {
                        if (wornDevice.IsPressed) wornDevice.Sample(sample.rightTip);
                        return false;
                    }
                    wornDevice.ResetContact();
                    waitForWithdrawal=false;
                }
                bool before=wornDevice.IsPressed;
                wornDevice.Sample(sample.rightTip);
                deviceToggle=!before && wornDevice.IsPressed;
                if (deviceToggle) waitForWithdrawal=true;
                return deviceToggle;
            }
            if(dragging || settings.activation!=WristMenuActivation.TapWrist) { tap.Reset(); return false; }
            tap.Step(sample,DecorationCenter(sample),hardBlocked,now,settings);
            return tap.Toggle;
        }

        public void Tick(WristMenuInput sample, LinearRpgController controller, bool busy, double now,string gameplayBlockReason=null)
        {
            if (!isActiveAndEnabled || !view || controller == null) return;
            // A visible wrist joint alone cannot place the device if its finger-base joints are lost.
            if (wornDevice && !sample.mountValid) sample.leftValid=false;
            owner = controller; input = sample;
            if (now >= nextOriginScan)
            {
                nextOriginScan = now+1;
                foundSources.Clear(); SubsystemManager.GetSubsystems(foundSources);
                foreach (var subsystem in foundSources)
                    if (!originSources.Contains(subsystem)) { subsystem.trackingOriginUpdated += OnOriginChanged; originSources.Add(subsystem); }
            }
            var rig = controller.locomotion.rigRoot;
            if ((source != null && sample.source != null && source != sample.source) || (frameEstablished && rig &&
                (Vector3.Distance(rigPosition,rig.position) > .15f || Quaternion.Angle(rigRotation,rig.rotation) > 15))) ResetForWorldChange();
            if(sample.source!=null)source=sample.source;
            if (rig) { rigPosition = rig.position; rigRotation = rig.rotation; frameEstablished = true; }
            view.EnsureBuilt();
            bool left = WristMenuState.Fresh(sample.leftValid,sample.leftObserved,now,settings.maxAge);
            bool right = WristMenuState.Fresh(sample.rightValid,sample.rightObserved,now,settings.maxAge);
            view.SetDecoration(new Pose(DecorationCenter(sample),sample.leftRotation),left && !wornDevice);
            if(preparedAt!=now) PrepareWristTap(sample,busy,now);
            preparedAt=-1;
            if (wornDevice)
            {
                var player=controller.Player;
                if (player!=null && player.Alive) recoveryFailed=false;
                if (wornDevice.display) wornDevice.display.Present(player,left && sample.mountValid,recoveryFailed);
                if (player!=null && !player.Alive)
                {
                    if (State.LauncherOpen || PanelOpen) ResetForWorldChange();
                    view.SetPointer(default,default,false,false);
                    if (!busy && deviceToggle)
                    {
                        recoveryFailed=!controller.RestoreCheckpoint();
                        LastAction=controller.Status;
                        waitForWithdrawal=true;
                    }
                    return;
                }
            }
            if(right && sample.PinchDown(settings))pinched=true;
            else if(right && sample.PinchUp(settings) || !dragging && (!right || !sample.pinchValid))pinched=false;

            string hit = null; bool near = false; float depth = 0; Vector3 point = default;
            // Never click launcher through a tracking loss, or on the frame the wrist is lowered.
            bool lowered = left && sample.leftWrist.y < sample.headPosition.y-settings.lowerBelowHead;
            bool launcherClickable = left && !lowered && State.LauncherOpen;
            bool hasMount = !wornDevice || left;
            bool hasUi = hasMount || PanelOpen;
            if (right && hasUi && !busy && !dragging)
                FindTarget(sample,launcherClickable,lowered,out hit,out near,out depth,out point);
            if(PanelOpen && FollowPlayer && rig && !dragging)
                panelPose=Facing(rig.TransformPoint(localPanelPose.position),sample.headPosition);
            bool hadDrag=dragging;
            if(dragging) UpdateDrag(sample,right,busy,now);
            string rayCommand=null;
            if(busy || !hasUi || tap.Approaching || near || hadDrag) ray.Reset();
            else rayCommand=ray.Step(hit,point,sample,now,settings);
            if(rayCommand!=null && !RayTargetAvailable(rayCommand,sample,launcherClickable,lowered,ray.Point))
            { rayCommand=null; ray.ClearTarget(); }
            if(!ray.Pressed) confirmedRayPress=false;
            if(rayCommand!=null) confirmedRayPress=true;
            // Evaluate the near press before ownership, so a command claims its actual press
            // frame without claiming the preceding hover. Execute commands only after arbitration.
            string pokeCommand=null;
            if(hadDrag || !right || busy || tap.Approaching || !near || hit==null) pointer.Reset();
            else pokeCommand=pointer.Step(hit,true,depth,pinched,sample.PinchUp(settings),sample.rightObserved,settings.maxGap);
            bool pointerOwns=pointer.Captured || confirmedRayPress || dragging;
            bool wasOpen = State.LauncherOpen;
            State.Tick(sample,busy,pointerOwns,now,settings,TogglePressed);
            if (TogglePressed && left && State.CanInteract && !State.LeftLow && wasOpen != State.LauncherOpen && WristTapConfirmed != null)
                foreach (Action<bool> listener in WristTapConfirmed.GetInvocationList())
                    try { listener(State.LauncherOpen); } catch (Exception e) { Debug.LogWarning("点腕反馈监听失败：" + e.Message, this); }
            if (State.LauncherOpen && hasMount)
            {
                Vector3 f = Horizontal(sample.headForward);
                Vector3 position = wornDevice && wornDevice.display && wornDevice.display.menuAnchor
                    ? wornDevice.display.menuAnchor.position
                    : sample.leftWrist + Quaternion.LookRotation(f)*launcherOffset;
                view.ShowLauncher(Facing(position,sample.headPosition));
                RefreshLauncherReadout();
                if(!shown) { shown = true; }
            }
            if ((!State.LauncherOpen || !hasMount) && shown) { view.HideLauncher(); shown = false; if (!State.LauncherOpen) view.PlayCue(false); }
            if (PanelOpen && State.LeftLow && Policy(PanelKind).closeWithWrist) ClosePanel();
            // 抬腕开启提示仅在电脑调试界面展示，普通手部动作不弹出世界提示。
            view.SetWristActivationHint(Pose.identity,"",0,false);
            if(State.CanInteract && pokeCommand!=null && pokeCommand!="drag")Execute(pokeCommand);
            if(State.CanInteract && !tap.Approaching && rayCommand!=null && !dragging){lastRayCommand=rayCommand;Execute(rayCommand);}
            OwnershipReason = State.OwnsInput
                ? dragging ? "教学卡拖动中" : confirmedRayPress ? "教学卡捏合已确认" : pokeCommand!=null ? "界面按钮按压已确认" :
                  State.LauncherOpen ? "腕部主入口已打开" : "已确认的界面操作结束，正在交回输入"
                : tap.Approaching ? "靠近腕饰，尚未确认（不占输入）" : hit!=null || ray.Target!=null ? "仅悬停高亮（不占输入）" :
                  PanelOpen ? "教学卡闲置（不占输入）" : "未操作腕部界面";
            if(PanelOpen) view.SetPanelPose(panelPose);
            bool capturedHover=ray.Target!=null && sample.pinchValid && !sample.PinchUp(settings) && !dragging;
            view.SetHover(near ? pointer.Hover : capturedHover?ray.Target:hit ?? ray.Target, (near ? pointer.Pressed : ray.Pressed) ? 1 : .35f);
            bool uiAvailable=State.LauncherOpen || PanelOpen;
            PointerVisible=right && hasUi && sample.rayValid && State.CanInteract && !tap.Approaching && uiAvailable && !sample.rightClosed;
            Vector3 displayPoint=dragging && !dragPaused ? sample.rayOrigin+sample.rayDirection.normalized*dragDistance : capturedHover ? ray.Point : hit!=null ? point : ray.Target!=null ? ray.Point : sample.rayOrigin+sample.rayDirection*settings.rayDistance;
            // Visible search ray is feedback only. Empty space never becomes an interactive target.
            view.SetPointer(near ? sample.rightTip : sample.rayOrigin,displayPoint,PointerVisible || (near && State.CanInteract),pointer.Pressed || ray.Pressed);
            PointerStatus=$"射线来源={sample.raySource??"无"}；捏合来源={sample.pinchSource??"无"}；锁定目标={ray.Target??"无"}；上次确认={lastRayCommand??"无"}；拖动={DragStatus}\n射线显示={PointerVisible}，右手新鲜={right}，外部门控={gameplayBlockReason??(busy?"其他操作占用":"无")}，UI可交互={State.CanInteract}，点按已确认={TogglePressed}\n范围={settings.rayDistance:0.0}米，命中={hit ?? "无"}；轻捏={PinchStatus(sample)}";
            if (wasOpen && !State.LauncherOpen) pointer.Reset();
            if (PanelOpen && now >= nextContent) { nextContent = now+.15; RefreshPanel(false); }
        }

        bool RayTargetAvailable(string id,WristMenuInput f,bool launcherClickable,bool lowered,Vector3 point)
        {
            foreach(var target in view.Targets)
            {
                if(target.id!=id || !target.rect || !target.rect.gameObject.activeInHierarchy) continue;
                bool launcher=id=="seal" || id=="glyph" || id=="voice";
                var policy=Policy(PanelKind);
                if(launcher ? !launcherClickable : (!PanelOpen || !policy.allowRay || (lowered && policy.closeWithWrist))) return false;
                Vector3 delta=point-f.rayOrigin;
                if(!TryRay(target.rect,f.rayOrigin,delta.normalized,settings.rayDistance,out _,out float distance)) return false;
                return !Occluded(f.rayOrigin,delta.normalized,distance);
            }
            return false;
        }
        bool Occluded(Vector3 origin,Vector3 direction,float distance)
        {
            int count=Physics.RaycastNonAlloc(origin,direction,occlusionHits,distance,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
            if(count>=occlusionHits.Length) return true;
            for(int i=0;i<count;i++) if(!owner.locomotion.rigRoot || !occlusionHits[i].transform.IsChildOf(owner.locomotion.rigRoot)) return true;
            return false;
        }

        void FindTarget(WristMenuInput f, bool launcherClickable, bool lowered, out string id, out bool near, out float depth, out Vector3 point)
        {
            id = null; near = false; depth = 0; point = default;
            float nearest = float.PositiveInfinity;
            foreach (var target in view.Targets)
            {
                if (target?.rect == null || !target.rect.gameObject.activeInHierarchy) continue;
                bool launcher = target.id == "seal" || target.id == "glyph" || target.id == "voice";
                var policy = Policy(PanelKind);
                if (launcher ? !launcherClickable : (!PanelOpen || (lowered && policy.closeWithWrist))) continue;
                // Child panels always use ray + pinch, including when the fingertip is close to the handle.
                if (launcher && !f.tipUnavailable && TryPoke(target.rect,f.rightTip,out float z) && Mathf.Abs(z) < nearest)
                { nearest = Mathf.Abs(z); id = target.id; near = true; depth = z; point = f.rightTip; }
            }
            if (near || !f.rayValid || (!f.rightPointing && !pinched && !pointer.Captured && !PanelOpen && !State.LauncherOpen)) return;
            nearest = settings.rayDistance;
            foreach (var target in view.Targets)
            {
                if (target?.rect == null || !target.rect.gameObject.activeInHierarchy) continue;
                bool launcher = target.id == "seal" || target.id == "glyph" || target.id == "voice";
                var policy = Policy(PanelKind);
                if (launcher ? !launcherClickable : (!PanelOpen || !policy.allowRay || (lowered && policy.closeWithWrist))) continue;
                if (!TryRay(target.rect,f.rayOrigin,f.rayDirection,nearest,out var p,out float distance)) continue;
                // Environment occlusion matters; UI itself has no colliders.
                int obstacles = Physics.RaycastNonAlloc(f.rayOrigin,f.rayDirection,occlusionHits,distance,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
                bool blocked = obstacles >= occlusionHits.Length;
                for (int i=0;i<obstacles && !blocked;i++)
                    blocked = !owner.locomotion.rigRoot || !occlusionHits[i].transform.IsChildOf(owner.locomotion.rigRoot);
                if (blocked) continue;
                nearest = distance; id = target.id; point = p;
            }
        }

        public static bool TryPoke(RectTransform rect, Vector3 tip, out float depth)
        {
            Vector3 local = rect.InverseTransformPoint(tip);
            depth = Vector3.Dot(tip-rect.position,rect.forward);
            return WristMenuState.Finite(tip) && rect.rect.Contains(new Vector2(local.x,local.y)) && depth >= -.065f && depth <= .018f;
        }
        public static bool TryRay(RectTransform rect, Vector3 origin, Vector3 direction, float maximum, out Vector3 point, out float distance)
        {
            point = default; distance = 0;
            if (!WristMenuState.Finite(origin) || !WristMenuState.Finite(direction) || direction.sqrMagnitude < .9f) return false;
            direction.Normalize(); float denominator = Vector3.Dot(direction,rect.forward);
            if (denominator < .1f) return false; // Front face only, not through the back.
            distance = Vector3.Dot(rect.position-origin,rect.forward)/denominator;
            if (distance <= .015f || distance > maximum) return false;
            point = origin+direction*distance;
            Vector3 local = rect.InverseTransformPoint(point);
            return rect.rect.Contains(new Vector2(local.x,local.y));
        }

        void Execute(string command)
        {
            ClickCount++; LastAction = "已确认：" + command; view.PlayCue(true);
            switch (command)
            {
                case "seal":
                    OpenPanel(WristPanelKind.Seal);
                    if (owner && owner.chapter) owner.chapter.ReplayGuidance();
                    break;
                case "glyph": OpenPanel(WristPanelKind.Glyph); break;
                case "voice": OpenPanel(WristPanelKind.Voice); break;
                case "retry-microphone":
                    var voice = owner && owner.skills ? owner.skills.voice : null;
                    if (PanelOpen && PanelKind == WristPanelKind.Voice && voice && voice.NativeNeedsRetry)
                    {
                        voice.RetryNativeMicrophone();
                        RefreshPanel(true);
                    }
                    break;
                case "previous": Page = (Page+PageCount()-1)%PageCount(); RefreshPanel(true); break;
                case "next": Page = (Page+1)%PageCount(); RefreshPanel(true); break;
                case "close": ClosePanel(); break;
                case "relocate": panelPose = NewPanelPose(); CachePanelLocal(); RefreshPanel(true); break;
                case "follow": FollowPlayer=!FollowPlayer; CachePanelLocal(); RefreshPanel(true); break;
                case "drag":
                    dragging=true;dragPaused=false;dragLastGood=input.rightObserved;
                    dragRaySource=input.raySource;dragPinchSource=input.pinchSource;DragStatus="已抓住拖动条";
                    dragDistance=Mathf.Clamp(Vector3.Dot(ray.Point-input.rayOrigin,input.rayDirection.normalized),.05f,settings.rayDistance);
                    dragOffset=panelPose.position-(input.rayOrigin+input.rayDirection.normalized*dragDistance);
                    break;
            }
            pointer.Reset(); ray.ClearTarget(); // Keep the pinch latch: a held pinch cannot click the new page.
        }
        internal bool OpenTeachingFromLauncher()
        {
            if (!isActiveAndEnabled || !State.LauncherOpen) return false;
            OpenPanel(WristPanelKind.Seal);
            return PanelOpen && PanelKind == WristPanelKind.Seal;
        }

        void OpenPanel(WristPanelKind kind)
        {
            bool same = PanelOpen && PanelKind == kind;
            if (!same) { Page = 0; PanelKind = kind; }
            PanelOpen = true; panelPose = NewPanelPose(); CachePanelLocal(); RefreshPanel(true);
        }
        void UpdateDrag(WristMenuInput f,bool right,bool busy,double now)
        {
            if(busy){EndDrag("其他操作占用");return;}
            if(right && f.PinchUp(settings)){ReleaseDraggedPanel("明确松手");return;}
            if(now-dragLastGood>settings.dragLossGrace){ReleaseDraggedPanel("追踪中断超时");return;}
            bool valid=right && f.pinchValid && f.rayValid && WristMenuState.Finite(f.rayOrigin)
                && WristMenuState.Finite(f.rayDirection) && f.rayDirection.sqrMagnitude>=.5f;
            if(!valid){dragPaused=true;DragStatus="遮挡，保持原位";return;}
            var anchor=f.rayOrigin+f.rayDirection.normalized*dragDistance;
            if(dragPaused || f.raySource!=dragRaySource || f.pinchSource!=dragPinchSource || f.rightObserved-dragLastGood>settings.maxGap){
                if(!f.PinchDown(settings)){dragPaused=true;DragStatus="等待重新确认捏住";return;}
                dragOffset=panelPose.position-anchor;
                dragRaySource=f.raySource;dragPinchSource=f.pinchSource;dragPaused=false;
            }
            dragLastGood=f.rightObserved;
            panelPose=Facing(anchor+dragOffset,f.headPosition);
            CachePanelLocal();
            DragStatus="拖动中";
        }
        void EndDrag(string reason){dragging=dragPaused=false;DragStatus=reason;}
        void ReleaseDraggedPanel(string reason)
        {
            EndDrag(reason);
            CachePanelLocal();
            LastAction = "教学卡已放置，点关闭收起 · " + reason;
        }
        public void ClosePanel() { if(dragging)EndDrag("面板关闭"); PanelOpen = false; pointer.Reset(); ray.ClearTarget(); if (view) view.HidePanel(); LastAction = "教学卡已关闭，技能效果保持"; }
        public void ResetForWorldChange()
        {
            State.Reset(State.OwnsInput); pointer.Reset(); confirmedRayPress = pinched = shown = false; frameEstablished = false;
            tap.Reset(); ray.Reset(); preparedAt=-1; deviceToggle=false;
            if (wornDevice) wornDevice.ResetContact();
            OwnershipReason=State.OwnsInput?"追踪变化，正在交回已占用的输入":"未操作腕部界面";
            if (view) { view.HideLauncher(); view.SetPointer(default,default,false,false); }
            if (view) view.SetWristActivationHint(Pose.identity,"",0,false);
            ClosePanel(); LastAction = "追踪来源或空间改变，旧点击已作废，请重新抬腕";
        }
        void CachePanelLocal() {
            var rig=owner ? owner.locomotion.rigRoot : null;
            if(rig) localPanelPose=new Pose(rig.InverseTransformPoint(panelPose.position),Quaternion.Inverse(rig.rotation)*panelPose.rotation);
        }
        Pose NewPanelPose() => Facing(input.headPosition+Horizontal(input.headForward)*panelDistance-Vector3.up*panelLower,input.headPosition);
        static Pose Facing(Vector3 position,Vector3 eye) => new(position,Quaternion.LookRotation((position-eye).normalized,Vector3.up));
        static Vector3 Horizontal(Vector3 value) { value.y = 0; return value.sqrMagnitude > .001f ? value.normalized : Vector3.forward; }
        static string Title(WristPanelKind kind) => kind == WristPanelKind.Seal ? "术式教学" : kind == WristPanelKind.Glyph ? "画符预览" : "咒语速查";
        void RefreshLauncherReadout()
        {
            if (owner && wornDevice && owner.Player!=null) view.SetLauncherVitals(owner.Player.Health,owner.Player.Mana);
            if (!owner || owner.Progress==null) { view.SetLauncherProgress(null); return; }
            if (launcherOwner!=owner || launcherRevision!=owner.ProgressRevision)
            {
                teaching.Sync(owner);
                launcherProgress=$"教学 {teaching.EntryCount} 项 · 已解锁 {owner.Progress.unlockedSkillIds.Count} 项" +
                    (owner.CheckpointSavePending ? " · 待存档" : "");
                launcherOwner=owner;launcherRevision=owner.ProgressRevision;
            }
            view.SetLauncherProgress(launcherProgress);
        }
        int PageCount()
        {
            if (PanelKind == WristPanelKind.Glyph) return owner && owner.chapter ? Mathf.Max(1, owner.chapter.GlyphPreviewPages) : 2;
            if (owner == null || !owner.skills) return 2;
            teaching.Sync(owner);
            return Mathf.Max(1, teaching.PageCount(PanelKind == WristPanelKind.Voice));
        }
        void RefreshPanel(bool force)
        {
            if (!PanelOpen || !view) return;
            var receiver = owner && owner.skills ? owner.skills.voice : null;
            bool retryMicrophone = PanelKind == WristPanelKind.Voice && receiver && receiver.NativeNeedsRetry;
            int count = PageCount();
            Page = Mathf.Clamp(Page,0,count-1);
            if (PanelKind!=WristPanelKind.Glyph && owner && owner.skills)
            {
                bool voice = PanelKind==WristPanelKind.Voice;
                var lesson = teaching.PageAt(Page,voice,out int step);
                if (lesson!=null)
                {
                    if (!force && shownLesson==lesson && shownStep==step && shownRevision==owner.ProgressRevision)
                    {
                        view.SetMicrophoneRetry(retryMicrophone);
                        return;
                    }
                    var primary = voice && lesson.Voice ? lesson.Voice : lesson.Reference;
                    string id = primary.skillId;
                    bool available = owner.IsSkillAvailable(id);
                    bool permanent = owner.IsSkillUnlocked(id);
                    string lessonFooter = $"{Title(PanelKind)}　{Page+1} / {count}　　　　　　{(FollowPlayer?"跟随玩家":"固定位置")} · 远指后轻捏";
                    view.ShowLesson(panelPose,lesson,step,voice,owner.IsSkillDiscovered(id),available,permanent,owner.CheckpointSavePending,lessonFooter);
                    view.SetMicrophoneRetry(retryMicrophone);
                    shownLesson=lesson; shownStep=step; shownRevision=owner.ProgressRevision;return;
                }
            }
            shownLesson=null;
            string body = BuildPage();
            var policy = Policy(PanelKind);
            string footer = $"{Page+1} / {count}　{(policy.closeWithWrist ? "放腕收起" : "放腕保留")}" +
                (policy.allowRay ? " · 高亮后轻捏确认" : "") + "\n"+(FollowPlayer?"跟随玩家坐标":"固定世界位置")+
                " · 顶部捏住拖动，松手放置；点关闭收起";
            if (force || body != lastBody || footer != lastFooter) { view.ShowPanel(panelPose,Title(PanelKind),body,footer); lastBody = body; lastFooter = footer; }
            view.SetMicrophoneRetry(retryMicrophone);
        }
        public string BuildPage()
        {
            if (PanelKind == WristPanelKind.Glyph)
                return Page == 0 ? "此处尚未开放画符。\n请到「术式教学」查看已习得的术式。" : "收起预览，返回腕匣后可选择「术式教学」或「咒语」。";
            if (owner && owner.skills)
            {
                teaching.Sync(owner);
                var course=teaching.PageAt(Page,PanelKind==WristPanelKind.Voice,out int index);
                if (course==null) return "当前没有已习得的对应术式";
                var content=new StringBuilder(course.Name);
                if (course.Seals.Count>0) content.Append("\n").Append(string.Join(" → ",course.Seals));
                if (course.Voice) content.Append("\n").Append(course.Voice.phrase);
                return content.ToString();
            }
            if (Page == 0)
                return PanelKind == WristPanelKind.Seal ? "结印与施法\n按右侧箭头查看当前技能配方。\n双手做印；参考卡保留时可正常施法。\n双手低位保持可取消未发射火球。\n移动：左食指指向落点，短暂停留确认。\n投掷：握住火球，甩手后松开。" :
                    "中文咒语\n按下一页查看当前目录中的完整口令。\n组合技允许先结印或先念咒。\n取消口令：" + (owner && owner.skills ? owner.skills.CancelPhrase : "取消施法") +
                    "\n当前没有示范语音播放；不会自动念出技能。\n本页是实战速查，不是无伤害练习模式。";
            return "当前目录没有对应配方";
        }
        void OnOriginChanged(XRInputSubsystem subsystem) => ResetForWorldChange();
        void OnDisable()
        {
            foreach (var subsystem in originSources) subsystem.trackingOriginUpdated -= OnOriginChanged;
            originSources.Clear(); nextOriginScan = 0;
            ResetForWorldChange();
            if (wornDevice) { wornDevice.ResetContact(); wornDevice.gameObject.SetActive(false); }
            if (view) view.SetDecoration(Pose.identity,false);
        }
    }
}
