using UnityEngine;

namespace NarutoVR.SealCombat.Rpg
{
    public enum ExplorationMovement
    {
        [InspectorName("食指指向行走／握拳快跑")] PointWalk,
        [InspectorName("选点自动行走")] AutoWalk,
        [InspectorName("选点传送")] Teleport
    }

    public sealed partial class RpgLocomotionController
    {
        internal const string ExplorationPreference = "NarutoVR.ExplorationMovement";
        [SerializeField, InspectorName("默认探索移动")] ExplorationMovement defaultExplorationMovement;
        [InspectorName("探索快跑倍率"), Range(1f, 4f)] public float sprintMultiplier = 1.8f;
        ExplorationMovement? selectedMovement;
        public ExplorationMovement ExplorationMode => selectedMovement ?? defaultExplorationMovement;
        public string ExplorationModeName => ExplorationMode switch {
            ExplorationMovement.Teleport => "选点传送", ExplorationMovement.AutoWalk => "选点自动行走", _ => "食指行走／握拳快跑"
        };
        public string ExplorationHint => ExplorationMode switch {
            ExplorationMovement.Teleport => "伸出左食指看引导线，指向想去的地面，收拢其余手指，落点圆环填满即传送。",
            ExplorationMovement.AutoWalk => "伸出左食指看引导线，指向想去的地面，收拢其余手指。落点圆环填满后可松手，角色会走到落点。",
            _ => "左食指指向行走，行走中握左拳快跑并转动拳头转向，张手即停。"
        };
        internal bool PickingGround => UsesEncounterModes && !EncounterCombat && ExplorationMode != ExplorationMovement.PointWalk;
        bool aimingAtGround, autoWalking, groundGuideVisible;
        Vector3 groundGuideEnd;
        RaycastHit aimFloor;
        Vector3 autoDestination, walkHeading;
        double lastPointAt = double.NegativeInfinity;
        readonly object groundTarget = new();

        internal void LoadExplorationPreference()
        {
            int saved = PlayerPrefs.GetInt(ExplorationPreference, (int)defaultExplorationMovement);
            if (System.Enum.IsDefined(typeof(ExplorationMovement), saved)) SetExplorationMode((ExplorationMovement)saved);
        }
        public void SetExplorationMode(ExplorationMovement mode)
        {
            if (!System.Enum.IsDefined(typeof(ExplorationMovement), mode) || mode == ExplorationMode) return;
            selectedMovement = mode;
            if (!EncounterCombat) { ResetInput(); needNeutral = true; }
        }
        public void CycleExplorationMode()
        {
            SetExplorationMode(ExplorationMode switch {
                ExplorationMovement.Teleport => ExplorationMovement.PointWalk,
                ExplorationMovement.PointWalk => ExplorationMovement.AutoWalk, _ => ExplorationMovement.Teleport
            });
            PlayerPrefs.SetInt(ExplorationPreference, (int)ExplorationMode);
            PlayerPrefs.Save();
        }

        void StopExploration()
        {
            lastPointAt = double.NegativeInfinity;
            autoWalking = aimingAtGround = groundGuideVisible = false;
            EndWalkStride();
            walking = false;
        }

        void ProcessExploration(NavigationHandInput hand, bool fresh, float dt, double now)
        {
            CandidateNode = null;
            debugCandidates.Clear();
            motionReason = "探索中";
            if (!fresh || SealProtectionActive || hand.bothHandsLow)
            {
                StopExploration();
                rayState.Suspend();
                Status = "探索停止：手部追踪中断、结印或双手放低";
                return;
            }
            if (PickingGround)
            {
                if (autoWalking)
                {
                    groundGuideVisible = false;
                    if (rayState.NeedsRelease)
                        rayState.Process(null, !hand.leftPoseUnconfirmed, !rawInput.leftPointing && !rawInput.leftNavigationPointing,
                            hand.leftObserved, now, rayNavigation, gestures);
                    AdvanceAutoWalk(dt);
                }
                else PickGround(hand, now);
                return;
            }
            bool raised = hand.leftWrist.y >= head.position.y - navigationDropBelowHead;
            bool sprint = raised && hand.leftClosed && now - lastPointAt <= gestures.sampleMaxGap;
            if (!sprint && !(hand.leftPointing && hand.leftRayValid && TryHorizontal(hand.leftRayDirection, out walkHeading)))
            {
                // A partially curled hand may be between pointing and a fist. Pause until confirmed.
                if (!raised || hand.leftOpen || hand.leftPoseUnconfirmed || hand.leftPointing)
                    lastPointAt = double.NegativeInfinity;
                EndWalkStride();
                walking = false;
                Status = "等待左食指水平指向";
                return;
            }
            if (sprint && TryHorizontal(hand.leftFistDirection, out var fistHeading))
                walkHeading = fistHeading;
            lastPointAt = hand.leftObserved;
            float pitch = Mathf.Abs(Mathf.Asin(Mathf.Clamp(head.forward.y, -1, 1)) * Mathf.Rad2Deg);
            float t = Mathf.InverseLerp(pitchSlowStart, Mathf.Max(pitchSlowStart + 1, pitchSlowEnd), pitch);
            pitchFactor = pitchSlowsWalk ? Mathf.Lerp(1, minimumPitchFactor, Mathf.SmoothStep(0, 1, t)) : 1;
            WalkStep(walkHeading * (dt * Mathf.Max(.05f, exploreSpeed) * pitchFactor * (sprint ? Mathf.Max(1, sprintMultiplier) : 1)),
                sprint ? "探索：握拳快跑" : "探索：沿食指方向行走");
        }

        bool WalkStep(Vector3 step, string status)
        {
            if (step.sqrMagnitude > 0) MovementStarting?.Invoke();
            walking = true;
            if (step == Vector3.zero) { EndWalkStride(); Status = "等待连续移动采样"; return false; }
            if (!TrySmoothStep(step, out var travel, out var reason))
            {
                EndWalkStride();
                Status = "探索停止：" + reason;
                return false;
            }
            if (walking && movedThisAction) ExplorationTravelled?.Invoke(new Vector2(travel.x, travel.z).magnitude);
            Status = status;
            return true;
        }

        bool FirstGroundSurface(Vector3 origin, Vector3 direction, out RaycastHit landing)
        {
            landing = default;
            if (!RpgGestureState.Finite(origin) || !RpgGestureState.Finite(direction))
                return false;
            // Measure reach from the player on the floor, allowing for the raised hand's longer, slanted ray.
            float rayLength = offNodeRange + Vector3.Distance(origin, ExpectedFloor);
            int count = Physics.RaycastNonAlloc(origin, direction, smoothHits, rayLength,
                floorLayers.value | obstacleLayers.value, QueryTriggerInteraction.Ignore);
            if (count == smoothHits.Length) return false;
            for (int i = 0; i < count; i++)
                if (!Ignored(smoothHits[i].collider) && (!landing.collider || smoothHits[i].distance < landing.distance)) landing = smoothHits[i];
            return landing.collider;
        }

        bool CanLandAt(RaycastHit landing)
        {
            if ((floorLayers.value & (1 << landing.collider.gameObject.layer)) == 0 ||
                Vector3.Angle(landing.normal, Vector3.up) > maximumSlope || !SmoothFloor(ExpectedFloor, out var from, out _) ||
                Mathf.Abs(landing.point.y - from.point.y) > maximumFloorStep) return false;
            Vector3 delta = landing.point - from.point;
            if (delta.sqrMagnitude < nodeTolerance * nodeTolerance || delta.sqrMagnitude > offNodeRange * offNodeRange) return false;
            return TrySmoothLeg(from, from.point, delta, 0f, out _, out _, out _);
        }

        internal bool TryGetGroundAimGuide(out Vector3 origin, out Vector3 end)
        {
            origin = filteredInput.leftRayOrigin;
            end = groundGuideEnd;
            return isActiveAndEnabled && PickingGround && groundGuideVisible;
        }

        bool UpdateGroundAimGuide(NavigationHandInput hand, out RaycastHit hit)
        {
            hit = default;
            groundGuideVisible = hand.leftRayValid && (hand.leftPointing || hand.leftIndexAim) &&
                !hand.leftOpen && hand.leftWrist.y >= head.position.y - navigationDropBelowHead &&
                RpgGestureState.Finite(hand.leftRayOrigin) && RpgGestureState.Finite(hand.leftRayDirection) &&
                hand.leftRayDirection.sqrMagnitude > .0001f;
            if (!groundGuideVisible) return false;
            bool contact = FirstGroundSurface(hand.leftRayOrigin, hand.leftRayDirection, out hit);
            groundGuideEnd = contact ? hit.point :
                hand.leftRayOrigin + hand.leftRayDirection.normalized * Mathf.Min(3f, offNodeRange);
            return contact;
        }

        bool SameGroundAim(Vector3 origin, RaycastHit hit)
        {
            if (!aimingAtGround || !aimFloor.collider) return false;
            Vector3 offset = hit.point - aimFloor.point;
            if (Mathf.Abs(Vector3.Dot(offset, aimFloor.normal)) > targetFloorTolerance) return false;
            float distance = offset.magnitude;
            if (distance <= rayNavigation.hitRadius) return true;
            // At a shallow angle a small change in finger pitch projects far along the floor.
            return distance <= 1f && Vector3.Angle(aimFloor.point - origin, hit.point - origin) <= 1.5f;
        }

        void PickGround(NavigationHandInput hand, double now)
        {
            bool contact = UpdateGroundAimGuide(hand, out var hit);
            if (rayNavigation == null || !rayNavigation.Valid) { aimingAtGround = false; rayState.Suspend(); Status = "探索选点配置无效"; return; }
            bool hasHit = contact && hand.leftPointing && !hand.leftPoseUnconfirmed && CanLandAt(hit);
            if (hasHit && !SameGroundAim(hand.leftRayOrigin, hit))
            {
                rayState.Suspend();
                aimFloor = hit;
            }
            aimingAtGround = hasHit;
            selectedRayFloor = hasHit ? aimFloor : default;
            bool evidence = !hand.leftPoseUnconfirmed && (!hand.leftPointing || hand.leftRayValid);
            bool confirm = rayState.Process(hasHit ? groundTarget : null, evidence, !hand.leftPointing, hand.leftObserved, now,
                rayNavigation, gestures, countPreparationTowardsDwell: true);
            Status = rayState.Status;
            if (!confirm) return;
            aimingAtGround = false;
            // Recheck the displayed point in case an obstacle or a moving floor changed during dwell.
            if (!FirstGroundSurface(hand.leftRayOrigin, (aimFloor.point - hand.leftRayOrigin).normalized, out hit) ||
                Vector3.Distance(hit.point, aimFloor.point) > .02f || !CanLandAt(hit))
            {
                rayState.Reset();
                Status = "落点已改变，请重新指向";
                return;
            }
            CommitExplorationLanding(hit.point);
        }

        void CommitExplorationLanding(Vector3 point)
        {
            if (ExplorationMode == ExplorationMovement.Teleport)
            {
                Translate(point - PlayerFloorPosition, PlayerMovementKind.Navigation);
                Status = "已传送至所选落点";
                RpgFeedbackSignals.Raise(Feedback, RpgActionCue.Moved, this);
            }
            else
            {
                autoDestination = point;
                MovementStarting?.Invoke();
                autoWalking = true;
                Status = "已选定自动行走终点";
            }
        }

        void AdvanceAutoWalk(float dt)
        {
            Vector3 delta = Vector3.ProjectOnPlane(autoDestination - PlayerFloorPosition, Vector3.up);
            if (delta.magnitude <= .012f) { StopExploration(); Status = "已到达所选落点"; return; }
            if (dt <= 0) { EndWalkStride(); Status = "等待下一帧继续自动行走"; return; }
            if (!WalkStep(Vector3.ClampMagnitude(delta, dt * Mathf.Max(.05f, exploreSpeed)), "正在走向所选落点") || walkContact)
            {
                StopExploration();
                Status = "自动行走已中止，请重新选点";
            }
        }
    }
}
