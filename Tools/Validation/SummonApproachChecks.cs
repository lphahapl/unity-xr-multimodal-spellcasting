using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NarutoVR.SealCombat.Enemies;
using UnityEngine;
using UnityEngine.AI;

/// <summary>隔离 Play 验证。调用者提供正式敌人目录的管理器、玩家和断言记录器。</summary>
public static class SummonApproachChecks
{
    public static async Task Run(EnemyManager manager, Transform target, Vector3 origin, Action<bool, string> check)
    {
        // 同一张导航数据包含一片平地、一对需绕桥的岸和一座孤岛，避免借用章节烘焙或依赖直线路径。
        var surfaces = new List<NavMeshBuildSource>();
        void Box(float x, float z, float width, float depth) => surfaces.Add(new NavMeshBuildSource {
            shape = NavMeshBuildSourceShape.Box, size = new Vector3(width, .2f, depth),
            transform = Matrix4x4.Translate(new Vector3(x, -.1f, z))
        });
        Box(0, 0, 20, 22);
        Box(20, 0, 4, 24);
        Box(28, 0, 5.5f, 24);
        Box(24, 9, 10, 4);
        Box(43, 0, 12, 16);
        var mesh = NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByIndex(0), surfaces,
            new Bounds(new Vector3(20, 0, 0), new Vector3(65, 6, 30)), Vector3.zero, Quaternion.identity);
        if (!mesh) throw new InvalidOperationException("未能构造 T-20 验证导航面");
        var floor = origin + Vector3.right * 320;
        var surface = NavMesh.AddNavMeshData(mesh, floor, Quaternion.identity);
        var saved = new Pose(target.position, target.rotation);
        var group = new List<EnemyLease>();
        var otherGroup = new List<EnemyLease>();
        var spawned = new List<EnemyLease>();
        var slots = new SummonApproachSlots();
        var others = new SummonApproachSlots();
        async Task<EnemyLease> Spawn(float x, float z)
        {
            var lease = await manager.SpawnEnemyAsync("canjiou", floor + new Vector3(x, 0, z), Quaternion.identity, target, true);
            spawned.Add(lease);
            lease.Controller.tickAutomatically = false;
            return lease;
        }
        void Warp(EnemyLease lease, Vector3 point)
        {
            if (!lease.Controller.Agent.Warp(point)) throw new Exception("T-20 导航代理定位失败 " + point);
        }
        float Bearing(float now, EnemyLease lease)
        {
            if (!slots.TryGetDestination(lease, group, target, floor, now, out var point))
                throw new Exception("T-20 平地缺少外圈目的地，t=" + now);
            return Vector3.SignedAngle(target.forward, Vector3.ProjectOnPlane(point - floor, Vector3.up), Vector3.up);
        }
        try
        {
            target.SetPositionAndRotation(floor, Quaternion.identity);
            var a = await Spawn(-1.6f, 7);
            var b = await Spawn(1.6f, 7);
            var c = await Spawn(-1.45f, 7);
            group.Add(a); group.Add(b); otherGroup.Add(c);
            float left = Bearing(0, a), right = Bearing(0, b);
            check(left >= -50.1f && left <= -34.9f && right >= 34.9f && right <= 50.1f, "T20 两只召唤物各有玩家前方一侧的弧线槽");
            group.Reverse();
            check(Mathf.Abs(Bearing(.2f, a) - left) < .1f && Mathf.Abs(Bearing(.2f, b) - right) < .1f,
                "T20 名单顺序改变后原侧保持；近身的另一组成员不触发分离");
            target.rotation = Quaternion.Euler(0, 180, 0);
            check(Mathf.Abs(Bearing(.21f, a) - left) < .1f, "T20 玩家急转身时在同一重算间隔内重新定位前侧槽");
            target.rotation = Quaternion.identity;
            check(others.TryGetDestination(c, otherGroup, target, floor, .3f, out _) &&
                !slots.TryGetDestination(c, group, target, floor, .3f, out _) &&
                !others.TryGetDestination(a, otherGroup, target, floor, .3f, out _), "T20 两个精英的选位实例只处理各自租约");

            void Gap(float distance)
            {
                Warp(a, floor + new Vector3(-distance * .5f, 0, 7));
                Warp(b, floor + new Vector3(distance * .5f, 0, 7));
            }
            Gap(1.6f);
            check(Mathf.Abs(Bearing(1f, a) - left) < .1f, "T20 进入 1.8 米时从零开始切向渐入");
            float beginning = Bearing(1.13f, a);
            float strong = Bearing(1.3f, a);
            check(strong < beginning - .15f && beginning < left - .15f && Bearing(1.3f, b) > right + .4f,
                "T20 相向侧移随时间连续增强且两人获得相反方向");
            Gap(2.55f);
            Bearing(1.55f, a);
            Gap(1.95f);
            check(Bearing(1.8f, a) < left - .25f, "T20 锁定未满两秒，短时越过 2.4 米再回到滞后区仍保留原配对");
            check(Bearing(3.1f, a) < left - .25f, "T20 锁定期满、距离仍处于滞后区时持续分离");
            Gap(2.55f);
            Bearing(3.4f, a);
            Gap(1.95f);
            check(Mathf.Abs(Bearing(3.7f, a) - left) < .1f, "T20 满两秒且越过 2.4 米后解除，回到滞后区不会二次误触发");
            Gap(1.5f);
            Bearing(4f, a);
            check(Bearing(4.3f, a) < left - .5f, "T20 重新穿过触发半径可以开始下一次侧移");

            slots.TryGetDestination(a, group, target, floor, 4.5f, out var arrival);
            Warp(a, arrival);
            check(!slots.TryGetDestination(a, group, target, floor, 4.7f, out _), "T20 抵达外圈后主动交回原近战追击");
            Warp(a, floor + Vector3.forward * 2.1f);
            check(!slots.TryGetDestination(a, group, target, floor, 5f, out _), "T20 进入内圈后不再拉回外圈");
            check(a.Controller.TryDie() && a.Defeated && Mathf.Abs(Bearing(5.3f, b) - right) < .1f,
                "T20 一只死亡后同伴保持原侧且立即解除配对");
            manager.Recycle(a);
            var fresh = await Spawn(-.75f, 7);
            group.Add(fresh);
            check(fresh.Controller == a.Controller && fresh.Id != a.Id &&
                !slots.TryGetDestination(a, group, target, floor, 5.6f, out _) &&
                Mathf.Abs(Bearing(5.6f, fresh) - left) < .1f, "T20 原物体复用后旧租约失效，新租约无旧的近战交接或配对状态");

            // 左岸跨崖直线不通，向北绕桥可抵达右岸玩家前方的左槽；另一槽位系统与此无关。
            slots.Clear();
            group.Clear(); group.Add(fresh); group.Add(b);
            Warp(fresh, floor + new Vector3(20, 0, -2.5f));
            Warp(b, floor + new Vector3(20, 0, -1.2f));
            var bankFloor = floor + new Vector3(27.65f, 0, -4);
            target.position = bankFloor;
            slots.TryGetDestination(fresh, group, target, bankFloor, 6f, out _);
            bool found = slots.TryGetDestination(fresh, group, target, bankFloor, 6.3f, out var bankSlot);
            var agent = fresh.Controller.Agent;
            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            var path = new NavMeshPath();
            float angle = Vector3.SignedAngle(target.forward, Vector3.ProjectOnPlane(bankSlot - bankFloor, Vector3.up), Vector3.up);
            check(found && angle >= -50.1f && angle <= -34.9f &&
                NavMesh.Raycast(agent.nextPosition, bankSlot, out _, filter) &&
                agent.CalculatePath(bankSlot, path) && path.status == NavMeshPathStatus.PathComplete && path.corners.Length >= 3,
                "T20 崖边交汇：切向槽位在可达岸面，完整路径绕过缺口走桥");
            var island = floor + new Vector3(43, 0, -4);
            target.position = island;
            check(!slots.TryGetDestination(fresh, group, target, island, 7f, out _),
                "T20 候选位虽能采样但位于断开的孤岛时拒绝接管导航");
        }
        finally
        {
            foreach (var lease in spawned) if (manager.IsCurrent(lease)) manager.Recycle(lease);
            target.SetPositionAndRotation(saved.position, saved.rotation);
            surface.Remove();
            UnityEngine.Object.Destroy(mesh);
        }
    }
}
