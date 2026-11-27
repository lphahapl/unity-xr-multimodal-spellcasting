using System;
using NarutoVR.SealCombat.Enemies;
using UnityEngine;
using UnityEngine.SceneManagement;

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
        [InspectorName("战斗滑退免伤窗口（秒）"), Range(0, .8f)] public float dodgeProtectionSeconds = .35f;
        [InspectorName("极限闪避窗口（秒）"), Range(0, .5f)] public float perfectDodgeSeconds = .25f;
        [InspectorName("极限闪避回度力冷却（秒）"), Min(0)] public float perfectDodgeCooldown = 2f;
        [InspectorName("极限闪避回复度力"), Range(0, 100)] public float perfectDodgeMana = 25f;
        [InspectorName("极限闪避敌方时间倍率"), Range(.05f, 1f)] public float perfectDodgeEnemyScale = .25f;
        [InspectorName("敌方慢动作时长（现实秒）"), Min(0)] public float perfectDodgeSlowSeconds = 1.6f;
        [InspectorName("原地虚影时长（现实秒）"), Min(0)] public float perfectDodgeAfterimageSeconds = .9f;
        [SerializeField, InspectorName("当前玩家全身模型（可选）")]
        Animator dodgeAvatar;

        double dodgeStartedAt = double.NegativeInfinity;
        double nextPerfectDodgeAt = double.NegativeInfinity;
        bool perfectDodgeAwarded;
        bool hasDodgeEffects;
        RpgDodgeAfterimage dodgeAfterimage;
        Scene dodgeScene;
        bool dodgeAvatarAssigned;

        void Start()
        {
            if (!dodgeAvatarAssigned && dodgeAvatar) BindDodgeAvatar(dodgeAvatar);
        }

        internal void ReleaseDodgeAvatar(TrackedAvatarPresentation owner)
        {
            if (dodgeAfterimage && dodgeAfterimage.Release(owner)) dodgeAvatar = null;
        }

        /// <summary>生成或更换本地玩家的全身展示时调用。头显专用的躯干、袖口、手模不参与墨影。</summary>
        public bool BindDodgeAvatar(TrackedAvatarPresentation presentation)
        {
            if (!presentation || !presentation.player || presentation.player.locomotion != this)
                return BindDodgeAvatar((Animator)null);
            return BindDodgeAvatar(presentation.GetComponentInChildren<Animator>(), presentation);
        }

        /// <summary>普通 Humanoid 全身模型可直接绑定 Animator。传空解除当前外观。</summary>
        public bool BindDodgeAvatar(Animator fullBody)
        {
            var presentation = fullBody ? fullBody.GetComponentInParent<TrackedAvatarPresentation>() : null;
            if (presentation && (!presentation.player || presentation.player.locomotion != this)) fullBody = null;
            return BindDodgeAvatar(fullBody, presentation);
        }

        bool BindDodgeAvatar(Animator fullBody, TrackedAvatarPresentation presentation)
        {
            dodgeAvatarAssigned = true;
            dodgeAvatar = fullBody;
            if (!dodgeAfterimage && !TryGetComponent(out dodgeAfterimage))
            {
                if (!fullBody) return false;
                dodgeAfterimage = gameObject.AddComponent<RpgDodgeAfterimage>();
            }
            return dodgeAfterimage.Bind(fullBody, presentation);
        }

        void PresentPerfectDodge()
        {
            hasDodgeEffects = true;
            EnemyDodgeEffects.SlowEnemies(dodgeScene, transform, perfectDodgeEnemyScale, perfectDodgeSlowSeconds);
            if (dodgeAfterimage) dodgeAfterimage.ShowCaptured(perfectDodgeAfterimageSeconds, dodgeScene);
        }

        void ClearDodgeEffects()
        {
            if (!hasDodgeEffects) return;
            hasDodgeEffects = false;
            EnemyDodgeEffects.ClearSlow(transform);
            if (dodgeAfterimage) dodgeAfterimage.Clear();
        }

        internal bool TryAvoidEnemyHit(RpgPlayerState player, string hitId, float amount, double now)
        {
            double elapsed = now - dodgeStartedAt;
            if (!isActiveAndEnabled || !UsesEncounterModes || !EncounterCombat ||
                elapsed < 0 || elapsed >= dodgeProtectionSeconds || !player.TryClaimHit(hitId, amount, now)) return false;

            if (!perfectDodgeAwarded && elapsed < perfectDodgeSeconds && now >= nextPerfectDodgeAt)
            {
                perfectDodgeAwarded = true;
                nextPerfectDodgeAt = now + perfectDodgeCooldown;
                player.RestoreMana(perfectDodgeMana);
                PresentPerfectDodge();
                RpgFeedbackSignals.Raise(Feedback, RpgActionCue.PerfectDodge, this);
            }
            return true;
        }

        bool modeInitialized, needNeutral, shotGuard, remoteRearm, armed, sliding, walking, movedThisAction;
        bool drawingSlideBeforeLeftNeutral;
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

        public void SetEncounterCombat(bool combat) => SetEncounterCombat(combat, false);

        internal void SetEncounterCombat(bool combat, bool carryDrawingWrist)
        {
            if(modeInitialized && EncounterCombat==combat)return;
            bool switching=modeInitialized;
            modeInitialized=true; EncounterCombat=combat;
            RefreshAuthoredMarkers();
            bool canCarry = carryDrawingWrist && !SealProtectionActive && !rawInput.bothHandsLow &&
                !rawInput.rightMotionOwnedBySpell && Fresh(rawInput.rightValid, rawInput.rightObserved, inputNow);
            ResetInput(); needNeutral=switching;
            // Carry only the last chest pose. A second, continuous combat sample must
            // still pass the normal speed, distance, protection and cooldown checks.
            if (canCarry && head && TryHorizontal(head.forward, out var heading))
            {
                var relative = rawInput.rightWrist - head.position;
                if (InChest(relative, heading))
                {
                    lastRelative = pushAnchor = relative;
                    lastWristStamp = anchorStamp = rawInput.rightObserved;
                    pushHeading = heading;
                    armed = true;
                }
            }
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
            dodgeStartedAt = double.NegativeInfinity;
            drawingSlideBeforeLeftNeutral = false;
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
            if (!EncounterCombat) ExplorationTravelled?.Invoke(0f);
        }
        static bool TryHorizontal(Vector3 forward,out Vector3 heading)
        {
            heading=Vector3.ProjectOnPlane(forward,Vector3.up);
            if(!RpgGestureState.Finite(heading) || heading.sqrMagnitude<.01f)return false;
            heading.Normalize();return true;
        }
        bool Fresh(bool valid,double stamp,double now)=>valid && double.IsFinite(stamp) && now>=stamp && now-stamp<=gestures.sampleMaxAge;
        bool InChest(Vector3 relative, Vector3 heading) =>
            (relative - (Vector3.down * chestBelowHead + heading * .16f)).sqrMagnitude <= chestRadius * chestRadius;
        bool ProcessEncounter(NavigationHandInput f,bool held,bool blocked,double now,bool drawingDodge)
        {
            walkContact = null;
            double delta=now-lastProcess;
            lastProcess=now;
            float dt=double.IsFinite(delta) && delta>0 && delta<=gestures.sampleMaxGap ? (float)Math.Min(delta,.05) : 0;
            bool left=Fresh(f.leftValid,f.leftObserved,now);
            // A lowered wrist or missing tracking does not count as releasing the pointing pose.
            if(needNeutral && left && !rawInput.leftPoseUnconfirmed && !rawInput.leftPointing && !rawInput.leftNavigationPointing)needNeutral=false;
            // Left-hand navigation still needs a neutral pose. A draft can yield to a
            // right-only push, and the resulting slide can finish with the left hand out of view.
            if(blocked || needNeutral && !drawingDodge && !drawingSlideBeforeLeftNeutral)
            {
                ResetEncounterMotion(); rayState.Reset(); CandidateNode=null; debugCandidates.Clear();
                if (!blocked && PickingGround && left && !SealProtectionActive && !f.bothHandsLow)
                    UpdateGroundAimGuide(f, out _);
                motionReason=Status=blocked?"输入被占用或取消":"模式切换，等待有效收指";
                return true;
            }
            if (EncounterCombat && (ProcessCombatDodge(f, held, now, dt, drawingDodge) || drawingDodge)) return true;
            if (UsingCombatNodes) return false;
            ProcessWalkingInput(f, left, dt, now);
            return true;
        }

        bool ProcessCombatDodge(NavigationHandInput f, bool held, double now, float dt, bool drawingDodge)
        {
            if(sliding)
            {
                rayState.Suspend(); CandidateNode=null;
                return AdvanceSlide(dt);
            }
            if (needNeutral && drawingSlideBeforeLeftNeutral) return true;
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
            bool inChest=InChest(relative, look);
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
                {
                    armed=false; remoteRearm=false;
                    bool started = StartSlide(pushHeading,now);
                    drawingSlideBeforeLeftNeutral = started && drawingDodge && needNeutral;
                    return started;
                }
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
            StopExploration();
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
        bool SmoothClear(RaycastHit floor,Vector3 current,float inset,out Collider overlap,out string reason, int contacts = 0)
        {
            overlap = null;
            SmoothCapsule(floor,current,inset,out var bottom,out var top,out var radius);
            int count=Physics.OverlapCapsuleNonAlloc(bottom,top,radius,smoothOverlaps,obstacleLayers,QueryTriggerInteraction.Ignore);
            if(count==smoothOverlaps.Length){reason="身体附近碰撞密集";return false;}
            for (int i = 0; i < count; i++)
            {
                if (Ignored(smoothOverlaps[i])) continue;
                int index = ContactIndex(smoothOverlaps[i], contacts);
                if (index >= 0 &&
                    MeasureContact(smoothOverlaps[i], bottom, top, radius + WalkGap, out var normal, out float depth) &&
                    depth <= startingContacts[index].depth + .0001f && Vector3.Dot(normal, startingContacts[index].normal) > 0f)
                    continue;
                overlap = smoothOverlaps[i];
                reason = "身体占位被阻挡";
                return false;
            }
            reason=null;return true;
        }
        bool TrySmoothLeg(RaycastHit from, Vector3 initialFloor, Vector3 delta, float inset,
            out RaycastHit target, out RaycastHit obstacle, out string reason, int contacts = 0)
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
                int index = ContactIndex(hit.collider, contacts);
                // A convex collider stays behind its measured support plane on tangent/outward motion.
                // PhysX sweeps starting in contact can instead report the reverse travel direction.
                // A concave collider still needs later faces checked. Always verify the end occupancy.
                if (index >= 0 && Vector3.Dot(travel, startingContacts[index].normal) >= -.0001f &&
                    (hit.collider is not MeshCollider { convex: false } || hit.distance <= WalkGap))
                    continue;
                nearest = hit.distance;
                obstacle = hit;
            }
            // A concave mesh may contain another face in the same collider. Sweep again from
            // just outside the touching face so a distance-zero hit cannot hide the next face.
            for (int i = 0; i < contacts; i++)
            {
                var contact = startingContacts[i];
                if (!(contact.collider is MeshCollider mesh) || mesh.convex) continue;
                var offset = contact.normal * (contact.depth + WalkGap);
                count = Physics.CapsuleCastNonAlloc(bottom + offset, top + offset, radius, travel.normalized, smoothHits,
                    travel.magnitude, obstacleLayers, QueryTriggerInteraction.Ignore);
                if (count == smoothHits.Length) { reason = "路径碰撞密集"; return false; }
                for (int j = 0; j < count; j++)
                {
                    var hit = smoothHits[j];
                    if (hit.collider != mesh || hit.distance >= nearest) continue;
                    nearest = hit.distance;
                    obstacle = hit;
                }
            }
            if (obstacle.collider) { reason = "路径有障碍"; return false; }
            if (SmoothClear(target, initialFloor, inset, out var overlap, out reason, contacts)) return true;
            if (walking && overlap) walkContact = overlap;
            return false;
        }
        // Only colliders touching the starting capsule may retain a shallow room-scale overlap.
        // Store their outward planes for this step. Every other obstacle uses the full body radius.
        const float WalkGap = .001f;
        struct WalkContact { public Collider collider; public Vector3 normal; public float depth; }
        readonly WalkContact[] startingContacts = new WalkContact[32];
        readonly Vector3[] contactPlanes = new Vector3[35]; // starting overlaps + up to three slide impacts
        CapsuleCollider walkProbe;

        bool MeasureContact(Collider other, Vector3 bottom, Vector3 top, float radius, out Vector3 normal, out float depth)
        {
            if (!walkProbe)
            {
                // ComputePenetration needs an enabled shape. Keep the query trigger outside the level
                // and exclude simulation contacts. Its actual pose is supplied by each query.
                var probe = new GameObject("步行占位测量") { layer = LayerMask.NameToLayer("Ignore Raycast") };
                probe.transform.position = Vector3.down * 10000f;
                walkProbe = probe.AddComponent<CapsuleCollider>();
                walkProbe.isTrigger = true;
                walkProbe.excludeLayers = ~0;
            }
            float height = top.y - bottom.y + 2f * radius;
            if (walkProbe.radius != radius) walkProbe.radius = radius;
            if (walkProbe.height != height) walkProbe.height = height;
            return Physics.ComputePenetration(walkProbe, (bottom + top) * .5f, Quaternion.identity,
                other, other.transform.position, other.transform.rotation, out normal, out depth);
        }

        void OnDestroy()
        {
            if (!walkProbe) return;
            if (Application.isPlaying) Destroy(walkProbe.gameObject);
            else DestroyImmediate(walkProbe.gameObject);
        }

        bool WalkStart(RaycastHit from, out int contacts, out string reason)
        {
            contacts = 0;
            reason = null;
            Capsule(from, from.point, out var bottom, out var top, out float radius);
            int count = Physics.OverlapCapsuleNonAlloc(bottom, top, radius + WalkGap, smoothOverlaps, obstacleLayers, QueryTriggerInteraction.Ignore);
            if (count == smoothOverlaps.Length) { reason = "身体附近碰撞密集"; return false; }
            for (int i = 0; i < count; i++)
            {
                var other = smoothOverlaps[i];
                if (Ignored(other)) continue;
                if (!MeasureContact(other, bottom, top, radius + WalkGap, out var normal, out float depth) ||
                    depth > Mathf.Min(radius * .25f, .06f) + WalkGap || Mathf.Abs(normal.y) >= .95f)
                {
                    reason = "身体占位被阻挡";
                    return false;
                }
                walkContact = other;
                contactPlanes[contacts] = Vector3.ProjectOnPlane(normal, Vector3.up).normalized;
                startingContacts[contacts++] = new WalkContact { collider = other, depth = depth, normal = normal };
            }
            return true;
        }

        int ContactIndex(Collider other, int contacts)
        {
            for (int i = 0; i < contacts; ++i)
                if (startingContacts[i].collider == other) return i;
            return -1;
        }

        bool PermittedHeading(Vector3 step, int planes)
        {
            for (int i = 0; i < planes; i++)
                if (Vector3.Dot(step, contactPlanes[i]) < -.000001f) return false;
            return true;
        }

        Vector3 ClipWalk(Vector3 step, int planes)
        {
            if (PermittedHeading(step, planes)) return step;
            // In the horizontal plane the nearest feasible displacement is the original step,
            // a tangent to one of the faces, or zero in a closed corner.
            Vector3 best = Vector3.zero;
            for (int i = 0; i < planes; i++)
            {
                Vector3 along = Vector3.ProjectOnPlane(step, contactPlanes[i]);
                if (along.sqrMagnitude > best.sqrMagnitude && PermittedHeading(along, planes)) best = along;
            }
            return best;
        }

        bool TryWalkStep(RaycastHit from, Vector3 delta, out RaycastHit destination, out string reason)
        {
            destination = from;
            if (!WalkStart(from, out int contacts, out reason)) return false;
            int planes = contacts;
            for (int leg = 0; leg < 3; leg++)
            {
                delta = ClipWalk(delta, planes);
                if (delta.sqrMagnitude < .00000001f) break;
                if (TrySmoothLeg(destination, from.point, delta, 0, out var target, out var hit, out reason, contacts))
                {
                    destination = target;
                    return true;
                }
                if (!hit.collider) break;
                walkContact = hit.collider;
                float length = Vector3.Distance(destination.point, target.point);
                if (length < WalkGap || Mathf.Abs(hit.normal.y) >= .95f) break;
                float fraction = Mathf.Clamp01((hit.distance - WalkGap) / length);
                if (fraction > 0 && TrySmoothLeg(destination, from.point, delta * fraction, 0, out var approach, out _, out _, contacts))
                    destination = approach;
                contactPlanes[planes++] = Vector3.ProjectOnPlane(hit.normal, Vector3.up).normalized;
                delta *= 1f - fraction;
            }
            reason ??= "路径有障碍";
            return (destination.point - from.point).sqrMagnitude > .00000001f;
        }

        bool TrySmoothStep(Vector3 delta,out Vector3 moved,out string reason)
        {
            moved = Vector3.zero;
            if (!SmoothFloor(ExpectedFloor, out var from, out reason)) return false;
            RaycastHit target;
            if (walking)
            {
                if (!TryWalkStep(from, delta, out target, out reason)) return false;
            }
            else if (!SmoothClear(from, from.point, 0, out _, out reason) ||
                !TrySmoothLeg(from, from.point, delta, 0, out target, out _, out reason)) return false;
            moved = target.point - from.point;
            if (!walking && !movedThisAction && moved.sqrMagnitude > 0)
            {
                dodgeStartedAt = lastNow;
                perfectDodgeAwarded = false;
                if (dodgeAfterimage && perfectDodgeAfterimageSeconds > 0f)
                    dodgeAfterimage.CapturePose(from.point);
                // A persistent XR rig can stand in a different scene from the encounter.
                dodgeScene = from.collider.gameObject.scene;
                if (!dodgeScene.IsValid()) dodgeScene = gameObject.scene;
                if (!dodgeScene.IsValid()) dodgeScene = SceneManager.GetActiveScene();
                // A tracking ranged attack must see the player's position before the rig moves.
                EnemyDodgeEffects.LockCurrentAttacks(dodgeScene, head, rigRoot);
            }
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
