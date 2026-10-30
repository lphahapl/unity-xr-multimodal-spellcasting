using System;
using System.Collections.Generic;
using UnityEngine;

namespace NarutoVR.SealCombat.Rpg
{
    /// <summary>Explicitly driven by the hand adapter. Teleports only the assigned tracking-origin root.</summary>
    [DisallowMultipleComponent]
    public sealed class RpgLocomotionController : MonoBehaviour
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

        [Header("节点选择")]
        [InspectorName("在点水平容差（米）"), Min(0.03f)] public float nodeTolerance = 0.25f;
        [InspectorName("在点楼层容差（米）"), Min(0.02f)] public float nodeHeightTolerance = 0.18f;
        [InspectorName("最小选向点积"), Range(0.5f, 1f)] public float directionDot = 0.72f;
        [InspectorName("离点选向最大距离（米）"), Min(0.3f)] public float offNodeRange = 6f;

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
        double lastNow, nextDodgeAt = double.NegativeInfinity;

        public bool NavigationActive => state != null && state.NavigationActive;
        public string CurrentNodeId => currentNode == null ? string.Empty : currentNode.nodeId;
        public bool OnNode => currentNode != null;
        public float DodgeRemaining => (float)Math.Max(0, nextDodgeAt - lastNow);
        public string Status { get; private set; } = "等待手部输入";
        public float DwellProgress => CandidateNode == null || state == null ? 0 : state.DwellProgress;
        public NavigationNode CandidateNode { get; private set; }
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
            if (!RpgGestureState.Finite(now)) { ResetInput(); Status = "输入时钟无效"; return; }
            lastNow = GameClock?.Invoke() ?? now;
            if (!RpgGestureState.Finite(lastNow)) { ResetInput(); Status = "闪避时钟无效"; return; }
            if (!ReferencesValid()) { ResetInput(); Status = "请指定有效的Rig根节点与头部锚点"; return; }
            RefreshCurrentNode();
            if (frame.leftWrist.y < head.position.y - navigationDropBelowHead) frame.leftPointing = false;
            RpgGestureIntent intent = State.Process(frame, head.forward, forwardOwnedByHeldSpell, blockActions, now);
            if (intent.cancel)
            {
                CandidateNode = null;
                Status = "双手低位：已请求取消待施法与未发射物";
                CancelRequested?.Invoke();
            }

            if (intent.dodgeDirection != Vector3.zero)
            {
                State.InterruptDwell();
                CandidateNode = null;
                TryDodgeWorld(intent.dodgeDirection, now);
                return;
            }
            if (!NavigationActive || blockActions)
            {
                CandidateNode = null;
                candidateDirection = Vector2.zero;
                if (!intent.cancel) Status = blockActions ? "动作暂不可用" : "左手食指伸直并稳定后导航";
                return;
            }
            Vector2 direction = State.Selection;
            if (direction == Vector2.zero)
            {
                CandidateNode = null;
                candidateDirection = Vector2.zero;
                Status = State.NavigationNeedsNeutral ? "左食指回中立后可再次选择" : "导航已准备：移动左食指选择方向";
                return;
            }
            NavigationNode next = SelectNode(direction, State.NavigationForward);
            bool changed = next != CandidateNode || direction != candidateDirection;
            if (changed)
            {
                CandidateNode = next;
                candidateDirection = direction;
                State.RestartDwell();
            }
            if (intent.navigationStep != Vector2.zero && !changed)
            {
                if (CandidateNode == null) Status = "此方向无可用相邻落点；请回中立";
                else TryMoveToNode(CandidateNode);
                CandidateNode = null;
                State.InterruptDwell();
            }
            else Status = CandidateNode == null ? "此方向无可用落点" : "驻留确认：" + CandidateNode.nodeId;
        }

        public void ResetInput()
        {
            state?.Reset();
            CandidateNode = null;
            candidateDirection = Vector2.zero;
        }

        /// <summary>Cancel a pending node selection without rearming the held-low cancel gesture.</summary>
        public void CancelPendingNavigation()
        {
            State.InterruptDwell(); CandidateNode = null; candidateDirection = Vector2.zero;
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
            if (node == null) { Status = "模拟导航失败：此方向无可用落点"; return false; }
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

        NavigationNode SelectNode(Vector2 direction, Vector3 forward)
        {
            IReadOnlyList<NavigationNode> candidates = currentNode != null ? currentNode.neighbors : NavigationNode.ActiveNodes;
            if (candidates == null) return null;
            Vector3 origin = ExpectedFloor;
            Vector3 desired = RpgGestureState.ToWorld(direction, forward).normalized;
            float bestScore = float.PositiveInfinity;
            NavigationNode result = null;
            foreach (NavigationNode node in candidates)
            {
                if (node == null || node.gameObject.scene != gameObject.scene || node == currentNode || !node.isActiveAndEnabled ||
                    !RpgGestureState.Finite(node.floorPosition)) continue;
                Vector3 delta = node.floorPosition - origin;
                delta.y = 0;
                float distance = delta.magnitude;
                if (distance < nodeTolerance || (currentNode == null && distance > offNodeRange)) continue;
                float alignment = Vector3.Dot(delta / distance, desired);
                if (alignment < directionDot) continue;
                if (!TryFloor(origin, floorLayers, out RaycastHit from, out _)) continue;
                if (!TryNodeDestination(node, from.point, out RaycastHit destination, out _)) continue;
                int obstacles = node.overrideLayers ? node.obstacleLayers.value : obstacleLayers.value;
                if (!ClearCapsule(destination, from.point, obstacles, out _)) continue;
                if (!Visible(from.point, destination.point, obstacles)) continue;
                // Angular mismatch has a modest penalty; among aligned candidates prefer the nearest.
                float score = distance * (1f + (1f - alignment) * 2f);
                if (score >= bestScore) continue;
                bestScore = score;
                result = node;
            }
            return result;
        }

        bool TryMoveToNode(NavigationNode node)
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
            Status = "已移动至 " + node.nodeId + "；左指回中立后可再次选择";
            return true;
        }

        bool TryDodgeWorld(Vector3 direction, double now)
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
            // XZ delta is measured from the current HMD ground projection, preserving room-scale offset.
            rigRoot.position += delta;
            State.TranslateAnchors(delta);
            RefreshCurrentNode();
            Moved?.Invoke(delta);
        }

        void OnDisable() { ResetInput(); }
    }
}
