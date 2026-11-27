using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace NarutoVR.SealCombat.Enemies
{
    /// <summary>同一精英的存活小怪共用一份外圈选点状态。只返回追击目的地，移动和出手仍由各自控制器决定。</summary>
    public sealed class SummonApproachSlots
    {
        const float MinAngle = 35f, MaxAngle = 50f;
        const float EnterDistance = 1.8f, ExitDistance = 2.4f, MinHold = 2f;
        const float ScanInterval = .12f;

        sealed class Member
        {
            public readonly EnemyLease Lease;
            // 键是另一只怪的本次出生租约。每一对只由槽号较小的一方记录。
            public readonly Dictionary<EnemyLease, (float since, float sign)> Pairs = new();
            public Vector3 Destination, HandoffFloor;
            public float NextScan;
            public bool HasDestination, Closing;

            public Member(EnemyLease lease) { Lease = lease; }
        }

        readonly List<Member> slots = new();
        NavMeshPath path;
        Transform target;

        /// <summary>精英回收或目标换人时丢弃槽位、配对和近战交接状态。</summary>
        public void Clear() { slots.Clear(); target = null; }

        static bool Live(EnemyLease lease) => lease != null && !lease.Defeated && lease.Owner &&
            lease.Owner.IsCurrent(lease) && !lease.Controller.IsDead;

        static bool Contains(IReadOnlyList<EnemyLease> group, EnemyLease lease)
        {
            for (int i = 0; i < group.Count; ++i)
                if (group[i] == lease) return true;
            return false;
        }

        // 回收/死亡只释放本人的坑位，幸存者保持原侧；新一波优先补空位。
        int Sync(IReadOnlyList<EnemyLease> group, EnemyLease caller)
        {
            for (int i = 0; i < slots.Count; ++i)
            {
                var member = slots[i];
                if (member == null || (Live(member.Lease) && Contains(group, member.Lease))) continue;
                slots[i] = null;
                foreach (var other in slots)
                {
                    if (other == null) continue;
                    other.Pairs.Remove(member.Lease);
                    other.NextScan = 0;
                }
            }
            int result = -1;
            for (int i = 0; i < group.Count; ++i)
            {
                var lease = group[i];
                if (!Live(lease)) continue;
                int free = -1, index = 0;
                while (index < slots.Count && slots[index]?.Lease != lease)
                {
                    if (free < 0 && slots[index] == null) free = index;
                    ++index;
                }
                if (index == slots.Count)
                {
                    if (free < 0) slots.Add(new Member(lease));
                    else { index = free; slots[index] = new Member(lease); }
                }
                if (lease == caller) result = index;
            }
            return result;
        }

        static float BaseAngle(int index) => (index % 2 == 0 ? -1 : 1) * (index / 2 % 2 == 0 ? 42.5f : 46f);

        float Separation(int index, Vector3 floor, Vector3 forward, float now)
        {
            float sum = 0;
            for (int j = 0; j < slots.Count; ++j)
            {
                if (index == j || slots[j] == null) continue;
                int first = Mathf.Min(index, j), second = Mathf.Max(index, j);
                var a = slots[first];
                var b = slots[second];
                var pa = a.Lease.Controller.transform.position;
                var pb = b.Lease.Controller.transform.position;
                var delta = Vector3.ProjectOnPlane(pa - pb, Vector3.up);
                float distance = delta.magnitude;
                if (!a.Pairs.TryGetValue(b.Lease, out var pair))
                {
                    if (distance >= EnterDistance) continue;
                    var radial = Vector3.ProjectOnPlane((pa + pb) * .5f - floor, Vector3.up).normalized;
                    var tangent = Vector3.Cross(Vector3.up, radial.sqrMagnitude > .01f ? radial : forward);
                    float side = Vector3.Dot(delta, tangent);
                    pair = (now, Mathf.Abs(side) > .05f ? Mathf.Sign(side) : Mathf.Sign(BaseAngle(first) - BaseAngle(second)));
                    a.Pairs.Add(b.Lease, pair);
                }
                if (now - pair.since >= MinHold && distance >= ExitDistance)
                {
                    a.Pairs.Remove(b.Lease);
                    continue;
                }
                // 距离连续决定侧移强度；新配对有短暂渐入，跨门槛或争抢同一点时不会突然掉头。
                float strength = Mathf.SmoothStep(0, 1, 1f - distance / ExitDistance);
                float blend = Mathf.SmoothStep(0, 1, (now - pair.since) / .25f);
                sum += (index == first ? pair.sign : -pair.sign) * strength * blend;
            }
            return Mathf.Clamp(sum, -1f, 1f) * (MaxAngle - MinAngle);
        }

        /// <summary>floor 为玩家脚下，now 为全组共享的游戏时间。抵达外圈、身处近战或没有可达槽位时返回 false。</summary>
        public bool TryGetDestination(EnemyLease caller, IReadOnlyList<EnemyLease> children, Transform player,
            Vector3 floor, float now, out Vector3 point)
        {
            point = default;
            if (!player || children == null || !Contains(children, caller)) return false;
            if (target != player) { Clear(); target = player; }
            int index = Sync(children, caller);
            if (index < 0 || !caller.Controller.CanReceiveHit) return false;
            var actor = caller.Controller;
            var agent = actor.Agent;
            if (!agent || !agent.isActiveAndEnabled || !agent.isOnNavMesh) return false;
            var member = slots[index];
            float radius = Mathf.Max(3f, actor.attackDistance + .9f) + index / 2 * .9f;
            float distance = Vector3.ProjectOnPlane(actor.transform.position - floor, Vector3.up).magnitude;
            float arrival = Mathf.Max(.4f, actor.patrolArrivalDistance + .1f);
            if (member.Closing)
            {
                if (distance < radius + 1f || (floor - member.HandoffFloor).sqrMagnitude < 1f) return false;
                member.Closing = false;
                member.NextScan = 0;
            }
            var forward = Vector3.ProjectOnPlane(player.forward, Vector3.up).normalized;
            // 玩家正看头顶/脚下时，头部的水平右轴仍能确定方位。
            if (forward.sqrMagnitude < .001f) forward = Vector3.Cross(player.right, Vector3.up).normalized;
            float nominal = BaseAngle(index), side = Mathf.Sign(nominal);
            if (member.HasDestination)
            {
                var offset = member.Destination - floor;
                if (Mathf.Abs(offset.y) > .65f || Mathf.Abs(Vector3.ProjectOnPlane(offset, Vector3.up).magnitude - radius) > .65f ||
                    !InArc(offset, forward, side))
                { member.HasDestination = false; member.NextScan = 0; }
            }
            var toSlot = member.Destination - actor.transform.position;
            if ((distance <= actor.attackDistance + .15f && Mathf.Abs(actor.transform.position.y - floor.y) <= actor.navSampleRadius) ||
                (member.HasDestination && Vector3.ProjectOnPlane(toSlot, Vector3.up).sqrMagnitude <= arrival * arrival &&
                 Mathf.Abs(toSlot.y) <= arrival + Mathf.Abs(agent.baseOffset) && !agent.Raycast(member.Destination, out _)))
            {
                member.HandoffFloor = floor;
                member.Closing = true;
                member.HasDestination = false;
                return false;
            }

            if (now >= member.NextScan)
            {
                float angle = Mathf.Clamp(Mathf.Abs(nominal) + side * Separation(index, floor, forward, now), MinAngle, MaxAngle);
                member.NextScan = now + ScanInterval;
                member.HasDestination = TryReach(agent, floor, forward, side, angle, radius, out member.Destination) ||
                    TryReach(agent, floor, forward, side, MinAngle, radius, out member.Destination) ||
                    TryReach(agent, floor, forward, side, MaxAngle, radius, out member.Destination);
            }
            point = member.Destination;
            return member.HasDestination;
        }

        static bool InArc(Vector3 offset, Vector3 forward, float side)
        {
            float angle = side * Vector3.SignedAngle(forward, Vector3.ProjectOnPlane(offset, Vector3.up), Vector3.up);
            return angle >= MinAngle - .1f && angle <= MaxAngle + .1f;
        }

        bool TryReach(NavMeshAgent agent, Vector3 floor, Vector3 forward, float side, float bearing, float radius, out Vector3 point)
        {
            point = default;
            var desired = floor + Quaternion.AngleAxis(side * bearing, Vector3.up) * forward * radius;
            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            if (!NavMesh.SamplePosition(desired, out var hit, .65f, filter)) return false;
            if (!InArc(hit.position - floor, forward, side)) return false;
            path ??= new NavMeshPath();
            if (!agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete) return false;
            point = hit.position;
            return true;
        }
    }
}
