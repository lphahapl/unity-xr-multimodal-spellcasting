using System;
using UnityEngine;
using UnityEngine.AI;

namespace NarutoVR.SealCombat.Enemies
{
    [Serializable]
    public sealed class FireThrowerPositioning
    {
        public enum Mode { Disabled, Dynamic, PatrolOnly }
        [SerializeField, InspectorName("走位模式")] Mode mode;
        [SerializeField, Min(.1f), InspectorName("驻点巡逻左右半幅（米）")] float guardHalfWidth = 1.5f;
        [SerializeField, Min(.1f), InspectorName("无可用射位时重试秒数")] float retrySeconds = 1f;

        const float MinRange = 3.5f;
        const float MinSpacing = .5f;
        const float HalfFov = 75f;
        const float MinBearingGap = 60f;
        readonly Vector3[] accepted = new Vector3[9];
        NavMeshPath path;
        Vector3 station, right, destination, guardPoint;
        Transform target;
        float nextScan, nextGuard;
        int guardSign;
        bool hasDestination, hasGuard;
        public bool Enabled => mode != Mode.Disabled;

        /// <summary>巡逻锚点固定在此次出生位置，复用同一只机偶时重新取样。</summary>
        internal void Reset(Transform root)
        {
            station = root.position;
            right = root.right;
            target = null;
            hasDestination = hasGuard = false;
            guardSign = 1;
            nextScan = nextGuard = 0;
        }

        internal void ReachedDestination() { hasDestination = false; nextScan = 0; }
        internal void ReachedGuard(float retryAt) { guardSign = -guardSign; hasGuard = false; nextGuard = retryAt; }

        internal bool TryGetGuardPoint(EnemyController actor, float now, out Vector3 point)
        {
            point = guardPoint;
            if (now < nextGuard) return hasGuard;
            nextGuard = now + Mathf.Max(.1f, retrySeconds);
            hasGuard = TryReach(actor.Agent, station + right * (guardSign * guardHalfWidth), out guardPoint);
            if (!hasGuard)
            {
                guardSign = -guardSign;
                hasGuard = TryReach(actor.Agent, station + right * (guardSign * guardHalfWidth), out guardPoint);
            }
            point = guardPoint;
            return hasGuard;
        }

        /// <summary>从已发现玩家的地面位置布三环，从头部前向计算水平视角。只向控制器返回可走目标。</summary>
        internal bool TryGetDestination(EnemyController actor, Vector3 floor, float now, out Vector3 point)
        {
            point = default;
            if (mode != Mode.Dynamic || !actor.target || !actor.Agent || !actor.Agent.isOnNavMesh) return false;
            if (target != actor.target) { target = actor.target; ReachedDestination(); }
            Vector3 forward = Vector3.ProjectOnPlane(target.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < .01f) { ReachedDestination(); return false; }
            if (hasDestination && !IsFiringPoint(actor, floor, forward, destination))
            {
                ReachedDestination();
                actor.Entity.RequestRepath();
            }
            if (now < nextScan)
            {
                point = destination;
                return hasDestination;
            }

            nextScan = now + Mathf.Max(.1f, retrySeconds);
            if (hasDestination && TryReach(actor.Agent, destination, out point)) return true;

            hasDestination = false;
            int count = 0;
            float jitter = UnityEngine.Random.Range(-10f, 10f);
            int firstRing = UnityEngine.Random.Range(0, 3);
            for (int bearingIndex = 0; bearingIndex < 3; bearingIndex++)
            {
                float bearing = (bearingIndex - 1) * 65f + jitter;
                Vector3 direction = Quaternion.AngleAxis(bearing, Vector3.up) * forward;
                for (int offset = 0; offset < 3; offset++)
                {
                    int ring = (firstRing + bearingIndex + offset) % 3;
                    float radius = Mathf.Max(MinRange, actor.attackDistance * (.6f + ring * .2f));
                    if (!TryReach(actor.Agent, floor + direction * radius, out var sample) ||
                        !IsFiringPoint(actor, floor, forward, sample) ||
                        Vector3.Distance(actor.transform.position, sample) <= Mathf.Max(MinSpacing, actor.patrolArrivalDistance + .05f))
                        continue;
                    bool separated = true;
                    for (int i = 0; i < count; i++)
                        if (Vector3.Distance(accepted[i], sample) < MinSpacing ||
                            Mathf.Abs(EnemyController.HorizontalAngle(accepted[i] - floor, sample - floor)) < MinBearingGap)
                        { separated = false; break; }
                    if (separated) accepted[count++] = sample;
                }
            }
            if (count == 0) return false;
            point = destination = accepted[UnityEngine.Random.Range(0, count)];
            actor.Entity.RequestRepath();
            hasDestination = true;
            return true;
        }

        static bool IsFiringPoint(EnemyController actor, Vector3 floor, Vector3 forward, Vector3 p)
        {
            var offset = p - floor;
            return offset.sqrMagnitude <= actor.attackDistance * actor.attackDistance + .001f &&
                Vector3.ProjectOnPlane(offset, Vector3.up).sqrMagnitude >= MinRange * MinRange &&
                Mathf.Abs(EnemyController.HorizontalAngle(forward, offset)) <= HalfFov &&
                actor.Entity.HasClearSightFrom(p + Vector3.up * actor.eyeHeight, actor.target, actor.target.position, actor.obstructionMask);
        }

        bool TryReach(NavMeshAgent agent, Vector3 raw, out Vector3 point)
        {
            point = default;
            if (!agent || !agent.isActiveAndEnabled || !agent.isOnNavMesh) return false;
            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            path ??= new NavMeshPath();
            if (!NavMesh.SamplePosition(raw, out var hit, 1.5f, filter) ||
                !agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete) return false;
            point = hit.position;
            return true;
        }
    }
}
