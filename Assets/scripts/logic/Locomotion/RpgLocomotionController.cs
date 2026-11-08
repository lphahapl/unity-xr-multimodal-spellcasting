using System;
using System.Collections.Generic;
using UnityEngine;

namespace NarutoVR.SealCombat.Rpg
{
    /// <summary>Explicitly driven by the hand adapter. Teleports only the assigned tracking-origin root.</summary>
    [DisallowMultipleComponent]
    public sealed partial class RpgLocomotionController : MonoBehaviour
    {
        [Header("玩家引用（必须显式指定）")]
        [InspectorName("追踪原点根节点"), Tooltip("移动整个Rig，不直接修改相机。根节点的Y加地面偏移应为玩家脚下楼层。")]
        public Transform rigRoot;
        [InspectorName("头部锚点")] public Transform head;
        [InspectorName("根节点地面偏移（米）")] public float rigFloorOffset;

        [Header("手势")]
        [InspectorName("手势参数")] public RpgGestureSettings gestures = new RpgGestureSettings();
        [InspectorName("导航手腕低于头部退出距离（米）"), Min(0.2f), Tooltip("左手独自放下也退出导航；不依赖双手低位取消条件。")]
        public float navigationDropBelowHead = 0.65f;

        [Header("导航操作模式")]
        [InspectorName("使用射线直接选点"), Tooltip("开启后只需左食指指向节点，无需先移动食指四方向。旧模式供历史回归使用。")]
        public bool useRayNavigation;
        [InspectorName("射线选点设置")] public RayNavigationSettings rayNavigation = new RayNavigationSettings();
        readonly RayNavigationState rayState = new RayNavigationState();

        [Header("节点选择")]
        [InspectorName("在点水平容差（米）"), Min(0.03f)] public float nodeTolerance = 0.25f;
        [InspectorName("在点楼层容差（米）"), Min(0.02f)] public float nodeHeightTolerance = 0.18f;
        [InspectorName("最小选向点积"), Range(0.5f, 1f)] public float directionDot = 0.72f;
        [InspectorName("离点选向最大距离（米）"), Min(0.3f)] public float offNodeRange = 6f;
        [Header("朝向、视线与手指融合选点")]
        [InspectorName("导航评分设置")] public NavigationScoringSettings scoring = new NavigationScoringSettings();
        [InspectorName("导航输入平滑")] public NavigationSmoothingSettings smoothing = new NavigationSmoothingSettings();

        [Header("瞬移闪避")]
        [InspectorName("闪避距离（米）"), Min(0.1f)] public float dodgeDistance = 0.8f;
        [InspectorName("闪避冷却（秒）"), Min(0f)] public float dodgeCooldown = 1.2f;

        [Header("地面与占位")]
        [InspectorName("地面层"), Tooltip("必须包含实际楼层的非Trigger碰撞体。无需修改项目Layer定义。")]
        public LayerMask floorLayers = ~0;
        [InspectorName("障碍层"), Tooltip("起点、终点和闪避沿途检查；自动过滤Rig子物体与Trigger。")]
        public LayerMask obstacleLayers = ~0;
        [InspectorName("身体半径（米）"), Min(0.05f)] public float bodyRadius = 0.22f;
        [InspectorName("身体最低高度（米）"), Min(0.3f)] public float minimumBodyHeight = 0.8f;
        [InspectorName("头顶余量（米）"), Min(0f)] public float headClearance = 0.12f;
        [InspectorName("碰撞皮肤（米）"), Min(0.005f)] public float skin = 0.025f;
        [InspectorName("地面向上搜索（米）"), Min(0.05f)] public float floorSearchUp = 0.5f;
        [InspectorName("地面向下搜索（米）"), Min(0.05f)] public float floorSearchDown = 0.6f;
        [InspectorName("最大楼层高度差（米）"), Min(0f)] public float maximumFloorStep = 0.35f;
        [InspectorName("节点落地高度容差（米）"), Min(0.02f)] public float targetFloorTolerance = 0.12f;
        [InspectorName("最大地面坡度（度）"), Range(0f, 60f)] public float maximumSlope = 35f;

        RpgGestureState state;
        NavigationNode currentNode;
        Vector2 candidateDirection;
        NavigationNode challenger;
        double challengerSince = double.NaN;
        bool selectionPending;
        readonly NavigationInputSmoother inputSmoother = new();
        NavigationHandInput rawInput, filteredInput;
        readonly List<(string id, NavigationScore score, float distance)> debugCandidates = new(4);
        int evaluated, invalidNodes, directionRejected, floorRejected, occupiedRejected, occludedRejected;
        double inputNow;
        double lastSelectionObserved = double.NegativeInfinity;
        double lastNow, nextDodgeAt = double.NegativeInfinity;

        public bool NavigationActive => UsesEncounterModes && !EncounterCombat ? walking : (UsesEncounterModes || useRayNavigation) ? rayState.Active : state != null && state.NavigationActive;
        public bool SealProtectionActive => state != null && state.SealProtectionActive;
        public Vector3 PlayerFloorPosition => ReferencesValid() ? ExpectedFloor : transform.position;
        public event Action<RpgActionCue> Feedback;
        public string CurrentNodeId => currentNode == null ? string.Empty : currentNode.nodeId;
        public bool OnNode => currentNode != null;
        public float DodgeRemaining => (float)Math.Max(0, nextDodgeAt - lastNow);
        public string Status { get; private set; } = "等待手部输入";
        public float DwellProgress => CandidateNode == null ? 0 : (UsesEncounterModes || useRayNavigation) ? rayState.Progress : state == null ? 0 : state.DwellProgress;
        public NavigationNode CandidateNode { get; private set; }
        // Presentation reads the same adjacency and landing checks used for ray navigation.
        internal IReadOnlyList<NavigationNode> NearbyNavigationNodes => currentNode ? currentNode.neighbors : NavigationNode.ActiveNodes;
        internal bool PreviewNavigationOrigin(out RaycastHit floor)
        {
            floor = default;
            return ReferencesValid() && TryFloor(ExpectedFloor, floorLayers, out floor, out _);
        }
        internal bool PreviewNavigationLanding(NavigationNode node, Vector3 from, out RaycastHit landing)
        {
            landing = default;
            if (!node || node == currentNode || !node.isActiveAndEnabled || node.gameObject.scene != gameObject.scene ||
                !RpgGestureState.Finite(node.floorPosition)) return false;
            float distance = Vector3.Distance(from, node.floorPosition);
            if (distance < nodeTolerance || distance > offNodeRange ||
                !TryNodeDestination(node, from, out landing, out _)) return false;
            int obstacles = node.overrideLayers ? node.obstacleLayers.value : obstacleLayers.value;
            return ClearCapsule(landing, from, obstacles, out _) && Visible(from, landing.point, obstacles);
        }
        public string SelectionDetail { get; private set; } = "尚未选点";
        public event Action CancelRequested;
        /// <summary>Successful rig translation in world space, including a successful dodge.</summary>
        public event Action<Vector3> Moved;
        /// <summary>Successful dodge world delta. Moved is raised first.</summary>
        public event Action<Vector3> Dodged;
        /// <summary>Optional pause-aware clock for dodge cooldown only. Hand sample timestamps still use ProcessInput's now.</summary>
        public Func<double> GameClock { get; set; }

        RpgGestureState State => state ?? (state = new RpgGestureState(gestures));

        public void ProcessInput(NavigationHandInput frame, bool forwardOwnedByHeldSpell, bool blockActions, double now)
        {
            rawInput = frame; inputNow = now;
            if (!RpgGestureState.Finite(now)) { ResetInput(); Status = "输入时钟无效"; return; }
            lastNow = GameClock?.Invoke() ?? now;
            if (!RpgGestureState.Finite(lastNow)) { ResetInput(); Status = "闪避时钟无效"; return; }
            if (!ReferencesValid()) { ResetInput(); Status = "请指定有效的Rig根节点与头部锚点"; return; }
            RefreshCurrentNode();
            if (frame.leftWrist.y < head.position.y - navigationDropBelowHead) frame.leftPointing = false;
            if (blockActions) inputSmoother.Reset();
            else frame = inputSmoother.Process(frame, smoothing, gestures, now);
            filteredInput = frame;
            RpgGestureIntent intent = State.Process(frame, head.forward, forwardOwnedByHeldSpell, blockActions, now);
            if (intent.cancel)
            {
                CandidateNode = null;
                ClearChallenger();
                Status = "双手低位：已请求取消待施法与未发射物";
                CancelRequested?.Invoke();
            }

            if (UsesEncounterModes)
            {
                if (ProcessEncounter(frame,forwardOwnedByHeldSpell,blockActions || intent.cancel,now)) return;
                ProcessRayNavigation(frame,false,now);
                return;
            }
            if (intent.dodgeDirection != Vector3.zero)
            {
                State.InterruptDwell();
                rayState.Suspend();
                CandidateNode = null;
                ClearChallenger();
                TryDodgeWorld(intent.dodgeDirection, now);
                return;
            }
            if (useRayNavigation)
            {
                ProcessRayNavigation(frame, blockActions || intent.cancel, now);
                return;
            }
            if (!NavigationActive || blockActions)
            {
                debugCandidates.Clear();
                CandidateNode = null;
                candidateDirection = Vector2.zero;
                ClearChallenger();
                SelectionDetail = "尚未选点";
                if (!intent.cancel) Status = blockActions ? "动作暂不可用" : SealProtectionActive ? "结印保护：双手分开并稳定后再移动或闪避" : "左食指四向选点；右掌可独立四向短划闪避";
                return;
            }
            Vector2 direction = State.Selection;
            if (direction == Vector2.zero)
            {
                debugCandidates.Clear();
                CandidateNode = null;
                candidateDirection = Vector2.zero;
                ClearChallenger();
                SelectionDetail = "尚未选点";
                Status = State.NavigationNeedsNeutral ? "左食指回中立后可再次选择" : "导航已准备：移动左食指选择方向";
                return;
            }
            NavigationNode next = SelectNode(direction, State.NavigationForward, frame, true);
            bool changed = next != CandidateNode || direction != candidateDirection;
            if (changed)
            {
                CandidateNode = next;
                candidateDirection = direction;
                State.RestartDwell();
                RpgFeedbackSignals.Raise(Feedback, CandidateNode ? RpgActionCue.TargetSelected : RpgActionCue.Blocked, this);
            }
            // A deliberate new aim is being confirmed: do not commit the previous target during that interval.
            if (selectionPending) State.PauseDwell();
            if (intent.navigationStep != Vector2.zero && !changed && !selectionPending)
            {
                if (CandidateNode == null) { Status = "此方向无可用相邻落点；请回中立"; }
                else TryMoveToNode(CandidateNode);
                CandidateNode = null;
                ClearChallenger();
                State.InterruptDwell();
            }
            else Status = CandidateNode == null ? "此方向无可用落点" : (selectionPending ? "确认新瞄准：" : "驻留确认：") + CandidateNode.nodeId;
        }

        public void ResetInput()
        {
            ResetEncounterMotion();
            lastSelectionObserved = double.NegativeInfinity;
            inputSmoother.Reset(); debugCandidates.Clear();
            state?.Reset(); rayState.Reset();
            CandidateNode = null;
            candidateDirection = Vector2.zero;
            ClearChallenger();
            SelectionDetail = "尚未选点";
        }

        /// <summary>Cancel a pending node selection without rearming the held-low cancel gesture.</summary>
        public void CancelPendingNavigation()
        {
            State.InterruptDwell(); rayState.Suspend(); CandidateNode = null; candidateDirection = Vector2.zero; ClearChallenger();
        }

        /// <summary>Safe respawn teleport. Requires a registered checkpoint in this scene; adjacency and line of sight do not apply.</summary>
        public bool TryRestoreCheckpoint(NavigationNode node)
        {
            if (!ReferencesValid() || node == null || !node.isActiveAndEnabled || node.gameObject.scene != gameObject.scene)
            { Status = "检查点恢复失败：玩家引用或检查点场景无效"; return false; }
            bool registered = false;
            foreach (NavigationNode active in NavigationNode.ActiveNodes)
                if (active == node) { registered = true; break; }
            if (!registered) { Status = "检查点恢复失败：节点未注册"; return false; }
            Physics.SyncTransforms();
            int floors = node.overrideLayers ? node.floorLayers.value : floorLayers.value;
            int obstacles = node.overrideLayers ? node.obstacleLayers.value : obstacleLayers.value;
            if (!TryFloor(node.floorPosition, floors, out RaycastHit target, out string reason) ||
                Mathf.Abs(target.point.y - node.floorPosition.y) > targetFloorTolerance)
            { Status = "检查点恢复失败：" + (reason ?? "节点地面高度不合法"); return false; }
            Vector3 currentFloor = TryFloor(ExpectedFloor, floorLayers, out RaycastHit from, out _) ? from.point : ExpectedFloor;
            if (!ClearCapsule(target, currentFloor, obstacles, out reason))
            { Status = "检查点恢复失败：" + reason; return false; }
            ResetInput();
            Translate(target.point - currentFloor);
            Status = "已恢复检查点：" + node.nodeId;
            return true;
        }

        /// <summary>Explicit simulated action. Uses the same target and physics rules as real input.</summary>
        public bool TryDebugStep(Vector2 direction, double now)
        {
            if (!DebugArgumentsValid(direction, now)) return false;
            RefreshCurrentNode();
            NavigationNode node = SelectNode(RpgGestureState.Cardinal(direction),
                RpgGestureState.HorizontalForward(head.forward, Vector3.forward));
            if (node == null) { Status = "模拟导航失败：此方向无可用落点"; RpgFeedbackSignals.Raise(Feedback, RpgActionCue.Blocked, this); return false; }
            bool moved = TryMoveToNode(node);
            Status = "模拟导航：" + Status;
            State.InterruptDwell();
            CandidateNode = null;
            return moved;
        }

        /// <summary>Explicit simulated action; this does not imply a tracked hand gesture was detected.</summary>
        public bool TryDebugDodge(Vector2 direction, double now)
        {
            if (!DebugArgumentsValid(direction, now)) return false;
            if (UsesEncounterModes)
            {
                // A fresh debug button press is a new action; it does not require a tracked wrist.
                string gate=!EncounterCombat?"非战斗":rawInput.rightMotionOwnedBySpell?"持球":
                    shotGuard && now-shotAt<throwDodgeRecovery?"发射保护计时未满":sliding?"正在滑退":
                    lastNow<nextDodgeAt?"闪避冷却中":null;
                if(gate!=null){Status="模拟闪避："+gate;return false;}
                shotGuard=false;
                if (!TryHorizontal(head.forward,out var look)) return false;
                return StartSlide(look,now);
            }
            bool moved = TryDodgeWorld(RpgGestureState.ToWorld(RpgGestureState.Cardinal(direction),
                RpgGestureState.HorizontalForward(head.forward, Vector3.forward)), now);
            Status = "模拟闪避：" + Status;
            State.InterruptDwell();
            CandidateNode = null;
            return moved;
        }

        bool DebugArgumentsValid(Vector2 direction, double now)
        {
            if (!RpgGestureState.Finite(now) || !RpgGestureState.Finite(new Vector3(direction.x, 0, direction.y)) ||
                direction.sqrMagnitude < 0.001f || !ReferencesValid())
            { Status = "模拟输入或玩家引用无效"; return false; }
            lastNow = GameClock?.Invoke() ?? now;
            if (!RpgGestureState.Finite(lastNow)) { Status = "模拟闪避时钟无效"; return false; }
            return true;
        }

        bool ReferencesValid() => rigRoot != null && head != null && head != rigRoot && head.IsChildOf(rigRoot) &&
            RpgGestureState.Finite(rigRoot.position) && RpgGestureState.Finite(head.position);

        Vector3 ExpectedFloor => new Vector3(head.position.x, rigRoot.position.y + rigFloorOffset, head.position.z);

        void RefreshCurrentNode()
        {
            currentNode = null;
            if (!TryFloor(ExpectedFloor, floorLayers, out RaycastHit actualFloor, out _)) return;
            Vector3 floor = actualFloor.point;
            float best = nodeTolerance * nodeTolerance;
            foreach (NavigationNode node in NavigationNode.ActiveNodes)
            {
                if (node == null || node.gameObject.scene != gameObject.scene || !node.isActiveAndEnabled || !RpgGestureState.Finite(node.floorPosition)) continue;
                Vector3 delta = node.floorPosition - floor;
                if (Mathf.Abs(delta.y) > nodeHeightTolerance) continue;
                delta.y = 0;
                if (delta.sqrMagnitude > best) continue;
                best = delta.sqrMagnitude;
                currentNode = node;
            }
        }

        NavigationNode SelectNode(Vector2 direction, Vector3 forward) => SelectNode(direction, forward, default, false);

        void ClearChallenger() { challenger = null; challengerSince = double.NaN; selectionPending = false; }

        NavigationNode SelectNode(Vector2 direction, Vector3 forward, NavigationHandInput frame, bool stabilize)
        {
            bool newEvidence = !stabilize || frame.leftObserved > lastSelectionObserved;
            if (newEvidence && stabilize) lastSelectionObserved = frame.leftObserved;
            debugCandidates.Clear(); evaluated = invalidNodes = directionRejected = floorRejected = occupiedRejected = occludedRejected = 0;
            selectionPending = false;
            if (scoring == null || !scoring.Valid) { ClearChallenger(); SelectionDetail = "导航评分参数无效"; return null; }
            IReadOnlyList<NavigationNode> candidates = NearbyNavigationNodes;
            if (candidates == null) { ClearChallenger(); SelectionDetail = "没有相邻节点"; return null; }
            Vector3 origin = ExpectedFloor;
            Vector3 desired = RpgGestureState.ToWorld(direction, forward).normalized;
            float bestScore = float.NegativeInfinity;
            NavigationNode result = null;
            NavigationScore bestDetail = default, currentDetail = default;
            bool currentEligible = false;
            foreach (NavigationNode node in candidates)
            {
                evaluated++;
                if (node == null || node.gameObject.scene != gameObject.scene || node == currentNode || !node.isActiveAndEnabled || !Registered(node) ||
                    !RpgGestureState.Finite(node.floorPosition)) { invalidNodes++; continue; }
                Vector3 delta = node.floorPosition - origin;
                delta.y = 0;
                float distance = delta.magnitude;
                if (distance < nodeTolerance || (currentNode == null && distance > offNodeRange)) { directionRejected++; continue; }
                float alignment = Vector3.Dot(delta / distance, desired);
                if (alignment < directionDot) { directionRejected++; continue; }
                if (!TryFloor(origin, floorLayers, out RaycastHit from, out _)) { floorRejected++; continue; }
                if (!TryNodeDestination(node, from.point, out RaycastHit destination, out _)) { floorRejected++; continue; }
                int obstacles = node.overrideLayers ? node.obstacleLayers.value : obstacleLayers.value;
                if (!ClearCapsule(destination, from.point, obstacles, out _)) { occupiedRejected++; continue; }
                if (!Visible(from.point, destination.point, obstacles)) { occludedRejected++; continue; }
                NavigationScore score = NavigationTargetScoring.Evaluate(scoring, origin, destination.point, desired,
                    head.position, head.forward, frame.leftValid && frame.leftRayValid,
                    frame.leftRayOrigin, frame.leftRayDirection, direction == Vector2.down);
                int rank = 0;
                while(rank < debugCandidates.Count && score.total <= debugCandidates[rank].score.total) rank++;
                if (rank < 3) { debugCandidates.Insert(rank, (node.nodeId, score, distance)); if (debugCandidates.Count > 3) debugCandidates.RemoveAt(3); }
                if (node == CandidateNode) { currentEligible = true; currentDetail = score; }
                // Stable IDs break exact ties independently of scene registration order.
                if (score.total < bestScore || (score.total == bestScore && result != null &&
                    string.CompareOrdinal(node.nodeId, result.nodeId) >= 0)) continue;
                bestScore = score.total;
                bestDetail = score;
                result = node;
            }
            if (!newEvidence)
            {
                if (currentEligible) { selectionPending = challenger != null; return CandidateNode; }
                ClearChallenger(); SelectionDetail = "当前点已失效，等待下一次新鲜手部观测"; return null;
            }
            if (!result) { ClearChallenger(); SelectionDetail = "无通过楼层、占位与通路检查的落点"; return null; }
            if (stabilize && currentEligible && direction == candidateDirection && result != CandidateNode)
            {
                if (bestScore - currentDetail.total < scoring.switchAdvantage)
                {
                    ClearChallenger(); SelectionDetail = currentDetail.Describe() + "｜保持当前点"; return CandidateNode;
                }
                if (challenger != result || !double.IsFinite(challengerSince) || frame.leftObserved < challengerSince)
                { challenger = result; challengerSince = frame.leftObserved; }
                if (frame.leftObserved - challengerSince < scoring.switchSeconds)
                {
                    selectionPending = true;
                    SelectionDetail = currentDetail.Describe() + "｜新候选 " + result.nodeId + " 优势 " + (bestScore - currentDetail.total).ToString("F2");
                    return CandidateNode;
                }
            }
            ClearChallenger();
            SelectionDetail = bestDetail.Describe();
            return result;
        }

        void ProcessRayNavigation(NavigationHandInput frame, bool blocked, double now)
        {
            bool fresh = double.IsFinite(frame.leftObserved) && frame.leftObserved <= now + .01 &&
                now - frame.leftObserved <= gestures.sampleMaxAge;
            bool observed = frame.leftValid && fresh && RpgGestureState.Finite(frame.leftIndex) && RpgGestureState.Finite(frame.leftWrist);
            bool allowed = observed && !blocked && !SealProtectionActive && !frame.bothHandsLow;
            debugCandidates.Clear(); evaluated = invalidNodes = directionRejected = floorRejected = occupiedRejected = occludedRejected = 0;
            ClearChallenger();
            NavigationNode next = null;
            bool validRay = frame.leftRayValid && RpgGestureState.Finite(frame.leftRayOrigin) &&
                RpgGestureState.Finite(frame.leftRayDirection) && frame.leftRayDirection.sqrMagnitude > .0001f;
            if (allowed && frame.leftPointing && validRay && rayNavigation != null && rayNavigation.Valid)
                next = SelectRayNode(frame);
            else SelectionDetail = blocked ? "动作被界面或其他事件占用" : SealProtectionActive ? "双手结印保护" :
                !observed ? "左手追踪失效或样本过期" : !frame.leftPointing ? "左手未作指向手型或已放下" : "射线无效或配置错误";
            // Loss, ownership and malformed rays never count as a deliberate release.
            bool release = allowed && (!frame.leftPointing || validRay && next == null);
            bool changed = next != CandidateNode;
            CandidateNode = next;
            bool confirmed = rayState.Process(next, allowed && (!frame.leftPointing || validRay), release,
                frame.leftObserved, now, rayNavigation, gestures);
            if (changed && next != null) RpgFeedbackSignals.Raise(Feedback, RpgActionCue.TargetSelected, this);
            if (confirmed && next != null)
            {
                TryMoveToNode(next);
                CandidateNode = null;
                Status += "；请移开射线或放松左手后再次选择";
            }
            else Status = SelectionDetail + "｜" + rayState.Status + (next ? "：" + next.nodeId : "");
        }

        NavigationNode SelectRayNode(NavigationHandInput frame)
        {
            bool combat=UsesEncounterModes;
            if (combat && !rayNavigation.CombatScoringValid) { SelectionDetail="战斗候选角或评分权重无效"; return null; }
            IReadOnlyList<NavigationNode> candidates = currentNode != null ? currentNode.neighbors : NavigationNode.ActiveNodes;
            if (candidates == null) { SelectionDetail = "没有相邻节点"; return null; }
            if (!TryFloor(ExpectedFloor, floorLayers, out RaycastHit from, out string reason))
            { SelectionDetail = "玩家地面无效：" + reason; return null; }
            NavigationNode best = null;
            NavigationScore bestScore = default, previousScore = default;
            bool previousEligible = false;
            int rangeRejected=0, angleRejected=0;
            foreach (NavigationNode node in candidates)
            {
                evaluated++;
                if (node == null || node == currentNode || !node.isActiveAndEnabled || node.gameObject.scene != gameObject.scene ||
                    !Registered(node) || !RpgGestureState.Finite(node.floorPosition)) { invalidNodes++; continue; }
                float distance = Vector3.Distance(from.point, node.floorPosition);
                if (distance < nodeTolerance || distance > offNodeRange) { rangeRejected++; directionRejected++; continue; }
                NavigationScore score=default;
                if (!combat && !NavigationTargetScoring.TryEvaluateRay(node.floorPosition, frame.leftRayOrigin,
                    frame.leftRayDirection, rayNavigation.hitRadius, out score)) { angleRejected++; directionRejected++; continue; }
                if (!TryNodeDestination(node, from.point, out RaycastHit destination, out _)) { floorRejected++; continue; }
                int obstacles = node.overrideLayers ? node.obstacleLayers.value : obstacleLayers.value;
                if (!ClearCapsule(destination, from.point, obstacles, out _)) { occupiedRejected++; continue; }
                if (!Visible(from.point, destination.point, obstacles)) { occludedRejected++; continue; }
                if (combat && !NavigationTargetScoring.TryEvaluateCombatRay(rayNavigation,destination.point,
                    head.position,head.forward,frame.leftRayOrigin,frame.leftRayDirection,distance,offNodeRange,out score))
                { angleRejected++; directionRejected++; continue; }
                int rank = 0;
                while (rank < debugCandidates.Count && score.total <= debugCandidates[rank].score.total) rank++;
                if (rank < 3) { debugCandidates.Insert(rank, (node.nodeId, score, distance)); if (debugCandidates.Count > 3) debugCandidates.RemoveAt(3); }
                if (node == CandidateNode) { previousEligible = true; previousScore = score; }
                if (best == null || score.total > bestScore.total || score.total == bestScore.total && string.CompareOrdinal(node.nodeId,best.nodeId) < 0)
                { best = node; bestScore = score; }
            }
            // Only a still-eligible, still-reachable target may benefit from hysteresis.
            if (previousEligible && best != CandidateNode && bestScore.total - previousScore.total < rayNavigation.switchAdvantage)
            { best = CandidateNode; bestScore = previousScore; }
            string result=best ? combat ? $"战斗候选 {best.nodeId}：{bestScore.DescribeCombatRay(rayNavigation)}" :
                $"射线命中：{best.nodeId}，总分 {bestScore.total:F2} / 射线 {bestScore.handRay:F2}" : "无可达候选";
            SelectionDetail=$"{result}｜排除：{(combat?"角度":"射线未命中")} {angleRejected}，距离 {rangeRejected}，楼层 {floorRejected}，占位 {occupiedRejected}，遮挡 {occludedRejected}";
            return best;
        }

        static bool Registered(NavigationNode node)
        {
            foreach (NavigationNode active in NavigationNode.ActiveNodes) if (active == node) return true;
            return false;
        }

        bool TryMoveToNode(NavigationNode node)
        {
            bool success = MoveToNode(node);
            RpgFeedbackSignals.Raise(Feedback, success ? RpgActionCue.Moved : RpgActionCue.Blocked, this);
            return success;
        }

        bool MoveToNode(NavigationNode node)
        {
            // Revalidate current adjacency, dynamic occupancy and floors at the actual execution point.
            Physics.SyncTransforms();
            RefreshCurrentNode();
            if (node == null || node.gameObject.scene != gameObject.scene || !node.isActiveAndEnabled || (currentNode != null &&
                (currentNode.neighbors == null || !currentNode.neighbors.Contains(node))))
            { Status = "导航失败：节点已失效或不再相邻"; return false; }
            if (!TryFloor(ExpectedFloor, floorLayers, out RaycastHit from, out string reason))
            { Status = "导航失败：" + reason; return false; }
            if (!TryNodeDestination(node, from.point, out RaycastHit target, out reason))
            { Status = "导航失败：" + reason; return false; }
            int obstacles = node.overrideLayers ? node.obstacleLayers.value : obstacleLayers.value;
            if (!ClearCapsule(target, from.point, obstacles, out reason) || !Visible(from.point, target.point, obstacles))
            { Status = "导航失败：" + (reason ?? "目标不可见"); return false; }
            Translate(target.point - from.point);
            Status = "已移动至 " + node.nodeId + (useRayNavigation ? "" : "；左指回中立后可再次选择");
            return true;
        }

        bool TryDodgeWorld(Vector3 direction, double now)
        {
            bool success = DodgeWorld(direction, now);
            RpgFeedbackSignals.Raise(Feedback, success ? RpgActionCue.Dodged : RpgActionCue.Blocked, this);
            return success;
        }

        bool DodgeWorld(Vector3 direction, double now)
        {
            double cooldownNow = GameClock?.Invoke() ?? now;
            if (!RpgGestureState.Finite(cooldownNow)) { Status = "闪避时钟无效"; return false; }
            lastNow = cooldownNow;
            if (cooldownNow < nextDodgeAt) { Status = "闪避冷却中"; return false; }
            Physics.SyncTransforms();
            if (!TryFloor(ExpectedFloor, floorLayers, out RaycastHit from, out string reason))
            { Status = "闪避失败：" + reason; return false; }
            Vector3 requested = from.point + direction.normalized * dodgeDistance;
            if (!TryFloor(requested, floorLayers, out RaycastHit target, out reason) ||
                Mathf.Abs(target.point.y - from.point.y) > maximumFloorStep)
            { Status = "闪避失败：" + (reason ?? "楼层高度差过大"); return false; }
            if (!ClearCapsule(from, from.point, obstacleLayers, out reason) ||
                !ClearCapsule(target, from.point, obstacleLayers, out reason))
            { Status = "闪避失败：" + reason; return false; }
            Capsule(from, from.point, out Vector3 bottom, out Vector3 top, out float radius);
            Vector3 delta = target.point - from.point;
            foreach (RaycastHit hit in Physics.CapsuleCastAll(bottom, top, radius, delta.normalized,
                         delta.magnitude, obstacleLayers, QueryTriggerInteraction.Ignore))
            {
                if (Ignored(hit.collider)) continue;
                Status = "闪避失败：沿途存在障碍";
                return false;
            }
            // Cooldown is charged only after all destination and path checks pass.
            nextDodgeAt = cooldownNow + Math.Max(0, dodgeCooldown);
            Translate(delta);
            Status = "已瞬移闪避；不提供无敌帧";
            Dodged?.Invoke(delta);
            return true;
        }

        bool TryNodeDestination(NavigationNode node, Vector3 currentFloor, out RaycastHit result, out string reason)
        {
            int floors = node.overrideLayers ? node.floorLayers.value : floorLayers.value;
            if (!TryFloor(node.floorPosition, floors, out result, out reason)) return false;
            if (Mathf.Abs(result.point.y - node.floorPosition.y) > targetFloorTolerance)
            { reason = "节点未放置于合法地面高度"; return false; }
            if (Mathf.Abs(result.point.y - currentFloor.y) > maximumFloorStep)
            { reason = "目标楼层高度差过大"; return false; }
            return true;
        }

        bool TryFloor(Vector3 expected, int floors, out RaycastHit result, out string reason)
        {
            result = default;
            reason = "未找到合法地面";
            if (!RpgGestureState.Finite(expected)) return false;
            RaycastHit[] hits = Physics.RaycastAll(expected + Vector3.up * floorSearchUp, Vector3.down,
                floorSearchUp + floorSearchDown, floors, QueryTriggerInteraction.Ignore);
            float best = float.PositiveInfinity;
            foreach (RaycastHit hit in hits)
            {
                if (Ignored(hit.collider) || Vector3.Angle(hit.normal, Vector3.up) > maximumSlope) continue;
                float difference = Mathf.Abs(hit.point.y - expected.y);
                if (difference > maximumFloorStep || difference >= best) continue;
                best = difference;
                result = hit;
            }
            if (result.collider == null) return false;
            reason = null;
            return true;
        }

        bool ClearCapsule(RaycastHit floor, Vector3 currentFloor, int obstacles, out string reason)
        {
            Capsule(floor, currentFloor, out Vector3 bottom, out Vector3 top, out float radius);
            foreach (Collider overlap in Physics.OverlapCapsule(bottom, top, radius, obstacles, QueryTriggerInteraction.Ignore))
            {
                if (Ignored(overlap)) continue;
                reason = "身体占位被阻挡";
                return false;
            }
            reason = null;
            return true;
        }

        void Capsule(RaycastHit floor, Vector3 currentFloor, out Vector3 bottom, out Vector3 top, out float radius)
        {
            radius = Mathf.Max(0.05f, bodyRadius);
            float height = Mathf.Max(minimumBodyHeight, head.position.y - currentFloor.y + headClearance);
            // Raising the lower sphere by r/cos(slope) prevents a legal sloped floor from self-blocking.
            float bottomHeight = (radius + Mathf.Max(0.005f, skin)) / Mathf.Max(0.5f, Vector3.Dot(floor.normal, Vector3.up));
            bottom = floor.point + Vector3.up * bottomHeight;
            top = floor.point + Vector3.up * Mathf.Max(bottomHeight, height - radius);
        }

        bool Visible(Vector3 from, Vector3 to, int obstacles)
        {
            float eyeHeight = Mathf.Max(0.4f, head.position.y - from.y);
            Vector3 start = from + Vector3.up * eyeHeight;
            Vector3 delta = to + Vector3.up * eyeHeight - start;
            foreach (RaycastHit hit in Physics.RaycastAll(start, delta.normalized, delta.magnitude,
                         obstacles, QueryTriggerInteraction.Ignore))
                if (!Ignored(hit.collider)) return false;
            return true;
        }

        bool Ignored(Collider collider) => collider == null || collider.isTrigger ||
            collider.transform == rigRoot || collider.transform.IsChildOf(rigRoot);

        void Translate(Vector3 delta)
        {
            inputSmoother.Reset();
            // XZ delta is measured from the current HMD ground projection, preserving room-scale offset.
            rigRoot.position += delta;
            State.TranslateAnchors(delta);
            RefreshCurrentNode();
            Moved?.Invoke(delta);
        }

        public string BuildDebugDetails(double now)
        {
            var b = new System.Text.StringBuilder();
            bool combatRay=UsesEncounterModes && EncounterCombat;
            bool rayMode=UsesEncounterModes ? combatRay : useRayNavigation;
            string mode=UsesEncounterModes ? combatRay ? "战斗头向与手指融合选点" : "探索沿头显朝向行走" :
                rayMode ? "射线直接选点（无需食指位移）" : "旧四向位移";
            b.AppendLine($"导航模式：{mode}；候选 {(CandidateNode ? CandidateNode.nodeId : "无")}");
            if (rayMode) b.AppendLine($"射线状态：{rayState.Status}；准备 {rayNavigation?.readySeconds:F2}s / 驻留 {rayNavigation?.dwellSeconds:F2}s；等待重备={rayState.NeedsRelease}");
            b.AppendLine($"左手：{(rawInput.leftValid ? "追踪有效" : "无效")} / 指向={rawInput.leftPointing}；样本年龄 {Age(now,rawInput.leftObserved):0} ms");
            b.AppendLine($"右手：{(rawInput.rightValid ? "追踪有效" : "无效")} / 张掌={rawInput.rightOpen}；样本年龄 {Age(now,rawInput.rightObserved):0} ms");
            if (!UsesEncounterModes && !useRayNavigation) b.AppendLine($"导航位移 左右/前后：{State.NavigationDisplacement.x*100:0.0}/{State.NavigationDisplacement.y*100:0.0} cm；阈值 {gestures.directionDistance*100:0.0} cm");
            b.AppendLine($"平滑：{inputSmoother.Status}；位置滞后 {inputSmoother.PositionLag*1000:0.0} mm / 射线差 {inputSmoother.RayLagDegrees:0.0}°");
            b.AppendLine($"原始指尖 {rawInput.leftIndex:F3} → 导航指尖 {filteredInput.leftIndex:F3}");
            b.AppendLine($"原始射线 {rawInput.leftRayDirection:F2} → 导航射线 {filteredInput.leftRayDirection:F2}；有效={filteredInput.leftRayValid}");
            if (!UsesEncounterModes || combatRay) b.AppendLine($"驻留 {DwellProgress*100:0}% / {(rayMode ? rayNavigation?.dwellSeconds ?? 0 : gestures.dwellSeconds):0.00}s；{(rayMode ? rayState.Status : selectionPending ? "换点确认中，保留进度" : State.NavigationNeedsNeutral ? "等待左手回中" : "等待或累计有效输入")}");
            if (!UsesEncounterModes && !useRayNavigation) b.AppendLine($"换点候选 {(challenger ? challenger.nodeId : "无")}；优势要求 {scoring?.switchAdvantage:0.00}；已保持 {(challenger ? Math.Max(0,inputNow-challengerSince)*1000 : 0):0} ms");
            if (!UsesEncounterModes || combatRay) b.AppendLine($"上次候选检查 {evaluated}：无效 {invalidNodes} / 方向距离 {directionRejected} / 地面楼层 {floorRejected} / 占位 {occupiedRejected} / 遮挡 {occludedRejected}");
            if (combatRay && rayNavigation!=null)
                b.AppendLine($"候选半角 {rayNavigation.combatCandidateAngle:0}°；{SelectionDetail}");
            if (!UsesEncounterModes || combatRay) foreach(var c in debugCandidates)
                b.AppendLine(rayMode ? combatRay ? $"{c.id} · {c.distance:0.0}m · 可达 · {c.score.DescribeCombatRay(rayNavigation)}" :
                    $"{c.id} · {c.distance:0.0}m · 总分 {c.score.total:F2} / 射线 {c.score.handRay:F2}" :
                    $"{c.id} · {c.distance:0.0}m · {c.score.Describe()}");
            b.Append($"结印保护={SealProtectionActive}；右手技能占用={rawInput.rightMotionOwnedBySpell}；双手低位={rawInput.bothHandsLow}\n当前结果：{Status}");
            return b.ToString();
        }
        static double Age(double now, double stamp) => double.IsFinite(now) && double.IsFinite(stamp) ? Math.Max(0,now-stamp)*1000 : -1;

        void OnDisable() { ResetInput(); }
    }
}
