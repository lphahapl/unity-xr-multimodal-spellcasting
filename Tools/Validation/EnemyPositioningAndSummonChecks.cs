// 隔离工程中本文件、EliteFormationChecks 和 SummonApproachChecks 放入 Assets/Editor；
// EnemyWaveSpawnInterruptProbe 放入 Assets 的运行时目录。批处理入口为 EnemyPositioningAndSummonChecks.Run。
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NarutoVR.SealCombat;
using NarutoVR.SealCombat.Enemies;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class EnemyPositioningAndSummonChecks
{
    const string Pending = "EnemyPositioningAndSummonChecks.Pending";
    const string CatalogPath = "Assets/ScriptableObjects/敌人目录配置/本关怪物目录.asset";
    static EnemyPositioningAndSummonChecks() => EditorApplication.update += StartWhenPlaying;

    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("请在隔离工程的批处理模式运行。");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        SessionState.SetBool(Pending, true);
        EditorApplication.EnterPlaymode();
    }

    static async void StartWhenPlaying()
    {
        if (!SessionState.GetBool(Pending, false) || !Application.isPlaying || EditorApplication.isCompiling) return;
        SessionState.EraseBool(Pending);
        try { await CheckInPlay(); Debug.Log("ENEMY_BATCH_PASS"); EditorApplication.Exit(0); }
        catch (Exception error) { Debug.LogError("ENEMY_BATCH_FAIL " + error); EditorApplication.Exit(1); }
    }

    static void Check(bool ok, string label)
    {
        if (!ok) throw new Exception(label);
        Debug.Log("ENEMY_CHECK " + label);
    }

    static async Task WaitFor(Func<bool> ready)
    {
        double until = EditorApplication.timeSinceStartup + 40;
        while (!ready() && EditorApplication.timeSinceStartup < until) await Task.Delay(20);
        if (!ready()) throw new TimeoutException("等待真实出生或导航路径超时。");
    }

    static void CommandWave(EnemyLease elite, float elapsed, EliteSummonConfig rule)
    {
        elite.Controller.Tick(elapsed);
        Check(elite.Controller.IsCommanding, "波次先进入可打断的指挥窗口");
        elite.Controller.Tick(rule.commandDuration + .02f);
    }

    static async Task InterruptPartialWave(EnemyManager manager, Transform target, EliteSummonConfig config)
    {
        var leader = await manager.SpawnEnemyAsync("elite-canjiou", Vector3.left * 12, Quaternion.identity, target, true);
        leader.Controller.tickAutomatically = false;
        var first = await manager.SpawnEnemyAsync("canjiou", Vector3.back * 6, Quaternion.identity, target, true);
        var second = await manager.SpawnEnemyAsync("canjiou", Vector3.back * 9, Quaternion.identity, target, true);
        var probe = second.Controller.gameObject.AddComponent<EnemyWaveSpawnInterruptProbe>();
        bool interrupted = false;
        probe.NextEnable = () => interrupted = leader.Controller.IsCommanding && manager.ActiveEnemies.Count == 3 &&
            leader.Controller.Entity.ReceiveHit("mid-spawn", 1, 0);
        manager.Recycle(second);
        manager.Recycle(first);
        CommandWave(leader, .1f, config);
        await WaitFor(() => interrupted && manager.ActiveCount == 1 && !leader.Controller.IsCommanding);
        Check(interrupted, "第二只小怪出生回调打断首波，已暂存第一只也回收");
        leader.Controller.Tick(config.cooldown * .8f);
        Check(manager.ActiveCount == 1 && !leader.Controller.IsCommanding, "异步波次撤销后留出完整冷却");
        CommandWave(leader, config.cooldown * .3f, config);
        await WaitFor(() => manager.ActiveCount == 4 && manager.ActiveEnemies.Values.All(c => c.Controller.CanReceiveHit));
        Check(manager.ActiveCount == 4, "取消的首波不计数，下次完整指挥仍走原首波数量");
        var members = manager.ActiveEnemies.Values.Where(child => child != leader).ToArray();
        leader.Controller.Tick(config.cooldown + .1f);
        var visual = leader.Controller.GetComponent<EliteCommandFormation>();
        EliteFormationChecks.AssertGroup(visual, members, config.formationMaterial);
        Check(members[0].Controller.Entity.ReceiveHit("die-while-linked", 9999, 0), "有算阵连线的成员可被击杀");
        int frame = Time.frameCount;
        await WaitFor(() => Time.frameCount > frame + 1);
        EliteFormationChecks.AssertGroup(visual, members.Skip(1).ToArray(), config.formationMaterial);
        Check(leader.Controller.IsCommanding && members[0].Defeated, "成员死亡后下一帧撤除该线，其余连线仍在");
        leader.Controller.Entity.ReceiveHit("commander-dies-mid-command", 9999, 0);
        EliteFormationChecks.AssertHidden(visual);
        Check(leader.Defeated && manager.ActiveCount == 1, "指挥官在窗口中死亡，立即收阵并回收本组");
        manager.Recycle(leader);
    }

    static async Task CheckTwoCommanders(EnemyManager manager, Transform target, EnemyLease edge, EliteSummonConfig config)
    {
        var firstGroup = manager.ActiveEnemies.Keys.ToArray();
        var other = await manager.SpawnEnemyAsync("elite-canjiou", Vector3.left * 10, Quaternion.identity, target, true);
        other.Controller.tickAutomatically = false;
        CommandWave(other, .1f, config);
        await WaitFor(() => manager.ActiveCount == 8 && manager.ActiveEnemies.Values.All(c => c.Controller.CanReceiveHit));
        var a = manager.ActiveEnemies.Values.Where(c => firstGroup.Contains(c.Id) && c != edge).ToArray();
        var b = manager.ActiveEnemies.Values.Where(c => !firstGroup.Contains(c.Id) && c != other).ToArray();
        foreach (var minion in a.Concat(b)) minion.Controller.tickAutomatically = false;
        a[0].Controller.Tick(.3f);
        b[0].Controller.Tick(.3f);
        Check(a[0].Controller.Status.Contains("外圈槽位") && b[0].Controller.Status.Contains("外圈槽位"),
            "两组真实召唤物通过各自 EnemyController 取得近战外圈目的地");
        Check(edge.Controller.Entity.ReceiveHit("outside", 1, 0) && !edge.Controller.IsCommanding &&
            a.All(c => !c.Controller.IsCommandParalyzed), "普通受击发生在窗口外，不瘫痪本组");
        edge.Controller.Tick(config.cooldown + .1f);
        other.Controller.Tick(config.cooldown + .1f);
        Check(edge.Controller.IsCommanding && other.Controller.IsCommanding &&
            edge.Controller.CurrentBehaviour == EnemyController.Behaviour.Commanding &&
            edge.Controller.Agent.isStopped, $"两名精英同时指挥并停止导航。A={edge.Controller.IsCommanding}/{edge.Controller.CurrentBehaviour}/{edge.Controller.Status}，B={other.Controller.IsCommanding}/{other.Controller.CurrentBehaviour}/{other.Controller.Status}，stop={edge.Controller.Agent.isStopped}");
        var left = edge.Controller.GetComponent<EliteCommandFormation>();
        var right = other.Controller.GetComponent<EliteCommandFormation>();
        var material = config.formationMaterial;
        EliteFormationChecks.Verify(left, right, edge, other, a, b, material);
        Check(!edge.Controller.Entity.ReceiveHit("outside", 1, 0) && edge.Controller.IsCommanding,
            "重复命中 ID 无法打断新窗口");
        edge.Controller.Entity.ReceiveHit("zero", 0, 0);
        Check(edge.Controller.IsCommanding, "零效果接触不会打断");
        Check(edge.Controller.Entity.ReceiveHit("interrupt", 0, 2) && !edge.Controller.IsCommanding &&
            other.Controller.IsCommanding, "有效削韧只打断受击精英");
        EliteFormationChecks.AssertHidden(left);
        EliteFormationChecks.AssertGroup(right, b, material);
        Check(a.All(c => c.Controller.IsCommandParalyzed && c.Controller.CanReceiveHit && c.Controller.Agent.isStopped) &&
            b.All(c => !c.Controller.IsCommandParalyzed), "仅本组活租约停行动，伤害入口仍开");
        foreach (var child in a) child.Controller.Tick(.25f);
        var fallen = a[1].Controller;
        for (int frame = 0; frame < 12; frame++)
        {
            fallen.Tick(.05f);
            fallen.animator.Update(.05f);
        }
        fallen.Entity.ReadReactionAnimation(EnemyReaction.Seated, out bool seated, out _);
        Check(seated && fallen.CurrentBehaviour == EnemyController.Behaviour.Seated,
            $"普通残机偶播放正式坐地瘫痪动画。支持={fallen.Entity.SupportsReactions}，速度={fallen.animator.speed}，当前态={fallen.animator.GetCurrentAnimatorStateInfo(0).shortNameHash}，行为={fallen.CurrentBehaviour}");
        float hp = a[0].Controller.Entity.Combat.Health;
        Check(a[0].Controller.Entity.ReceiveHit("during-paralysis", 1, 0) &&
            a[0].Controller.Entity.Combat.Health < hp && a[0].Controller.IsCommandParalyzed &&
            a.All(c => c.Controller.CurrentAttack == -1), "瘫痪期间可实际扣血，不能出手");
        edge.Controller.Tick(config.commandDuration + .1f);
        Check(manager.ActiveCount == 8 && !edge.Controller.IsCommanding, "原指挥结束时间不补发已打断波次");
        other.Controller.Tick(config.commandDuration + .1f);
        await WaitFor(() => manager.ActiveCount == 9 && manager.ActiveEnemies.Values.All(c => c.Controller.CanReceiveHit));
        Check(manager.ActiveCount == 9, "另一组本波照常完成");
        EliteFormationChecks.AssertHidden(right);
        var victim = a[0];
        Check(victim.Controller.Entity.ReceiveHit("finish-fallen", 9999, 0) && victim.Defeated &&
            !victim.Controller.IsCommandParalyzed, "瘫痪中的小怪能被击杀");
        manager.Recycle(victim);
        var outsider = await manager.SpawnEnemyAsync("canjiou", Vector3.back * 12, Quaternion.identity, target, true);
        outsider.Controller.tickAutomatically = false;
        Check(outsider.Controller == victim.Controller && outsider.Id != victim.Id &&
            !outsider.Controller.IsCommandParalyzed, "同一小怪实例复用后不继承瘫痪");
        var survivor = a[1].Controller;
        survivor.SetSuspended(true); survivor.Tick(20); survivor.SetSuspended(false);
        Check(survivor.IsCommandParalyzed, "房间暂停冻结瘫痪计时");
        survivor.Tick(config.paralysisDuration + .1f);
        Check(!survivor.IsCommandParalyzed && survivor.CurrentBehaviour != EnemyController.Behaviour.Seated &&
            survivor.CanReceiveHit, "瘫痪到时恢复原有行动");
        var meleePoint = target.position - Vector3.forward;
        Check(survivor.Agent.Warp(meleePoint), "幸存者可从外圈前进至玩家面前");
        survivor.transform.rotation = Quaternion.identity;
        Physics.SyncTransforms();
        survivor.Tick(2);
        Check(survivor.CurrentAttack >= 0, "同伴死亡并复用后，真实召唤物仍能近身出手");
        edge.Controller.Tick(config.cooldown);
        EliteFormationChecks.AssertGroup(left, a.Skip(1).ToArray(), material);
        EliteFormationChecks.VerifyRetiredMember(left, edge, victim, a.Skip(1).ToArray(), material);
        var zone = edge.Controller.GetComponentsInChildren<EnemyHitZone>().First(z => z.isActiveAndEnabled);
        var projectile = new GameObject("验证术法实体命中").AddComponent<SkillProjectile>();
        projectile.transform.position = zone.GetComponent<Collider>().bounds.center;
        projectile.damage = 1; projectile.poiseDamage = 0;
        Physics.SyncTransforms();
        float before = edge.Controller.Entity.Combat.Health;
        projectile.Initialize(Vector3.forward, target, ~0, 0, null, null);
        Check(projectile.IsFinished && edge.Controller.Entity.Combat.Health < before && !edge.Controller.IsCommanding &&
            !outsider.Controller.IsCommandParalyzed && survivor.CurrentAttack == -1 && survivor.IsCommandParalyzed,
            "正式术法弹体碰撞打断指挥、取消幸存者已开始的攻击，前成员的新租约不受影响");
        manager.Recycle(outsider);
        manager.Recycle(other);
        Check(manager.ActiveCount == 3 && firstGroup.Where(id => id != victim.Id).All(id=>manager.TryGetActive(id,out _)), "两名精英的召唤物独立回收");
        manager.Recycle(edge);
    }

    static async Task CheckInPlay()
    {
        var bounds = new Bounds(Vector3.zero, new Vector3(64, 4, 64));
        var box = new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box, size = new Vector3(60, .2f, 60),
            transform = Matrix4x4.Translate(Vector3.down * .1f) };
        var mesh = NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByIndex(0), new List<NavMeshBuildSource> { box },
            bounds, Vector3.zero, Quaternion.identity);
        var surface = NavMesh.AddNavMeshData(mesh);
        var rearSurface = NavMesh.AddNavMeshData(mesh, new Vector3(100, 0, -29), Quaternion.identity);
        var root = new GameObject("隔离验证敌人管理器");
        var target = new GameObject("目标").transform;
        var anchor = new GameObject("巡逻点").transform;
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.SetActive(false);
        try
        {
            var manager = root.AddComponent<EnemyManager>();
            manager.catalog = AssetDatabase.LoadAssetAtPath<EnemyCatalog>(CatalogPath);
            Check(manager.catalog && manager.catalog.Validate(out _), "正式敌人目录可解析");
            target.position = Vector3.up * .15f;
            var shot = await manager.SpawnEnemyAsync("zhihuocanji", Vector3.back * 18, Quaternion.identity, target, true);
            var shooter = shot.Controller;
            shooter.tickAutomatically = false;
            shooter.attackDistance = 10;
            shooter.Tick(.2f);
            var first = shooter.Agent.destination;
            var ground = Vector3.ProjectOnPlane(first - target.position, Vector3.up);
            Check(shooter.CurrentBehaviour == EnemyController.Behaviour.Chase && ground.magnitude >= 3.5f &&
                ground.magnitude <= 10.01f && Vector3.Angle(target.forward, ground) <= 75.01f, "正式掷火怪在前方三环内选择射位");
            Check(new[]{6f,8f,10f}.Any(r => Mathf.Abs(r-ground.magnitude) < .2f), "平坦场地射位取自 0.6R、0.8R、1.0R");
            target.rotation = Quaternion.Euler(0, 180, 0);
            shooter.Tick(.1f);
            var turned = Vector3.ProjectOnPlane(shooter.Agent.destination - target.position, Vector3.up);
            Check(Vector3.Angle(target.forward, turned) <= 75.01f, "玩家转身后旧射位失效");
            anchor.position = new Vector3(4, 0, -15);
            shooter.patrolPoints = new[] { anchor };
            wall.transform.position = new Vector3(0, 1.1f, -1);
            wall.transform.localScale = new Vector3(60, 4, .25f);
            wall.SetActive(true); Physics.SyncTransforms();
            shooter.Tick(1.1f);
            Check(shooter.CurrentBehaviour == EnemyController.Behaviour.Patrol &&
                Vector3.Distance(shooter.Agent.destination, anchor.position) < .3f, "硬遮挡淘汰全批射位并切回真实巡逻点");
            shooter.patrolPoints = Array.Empty<Transform>();
            shooter.Tick(1.1f);
            Check(shooter.CurrentBehaviour == EnemyController.Behaviour.Patrol &&
                Vector3.Distance(shooter.Agent.destination, shooter.transform.position) < 2f, "未配场景巡逻点时使用出生驻点两侧");
            wall.SetActive(false); Physics.SyncTransforms();
            shooter.Tick(1.1f);
            Check(shooter.CurrentBehaviour == EnemyController.Behaviour.Chase, "遮挡解除后重新选择射位");
            var arrived = shooter.Agent.destination;
            Check(shooter.Agent.Warp(arrived), "代理可到达选中的射位");
            var aim = target.position - arrived; aim.y = 0;
            shooter.transform.rotation = Quaternion.LookRotation(aim);
            shooter.Tick(.1f);
            Check(shooter.CurrentAttack >= 0, "到射位后进入既有掷火攻击链");
            shooter.CancelAttack();
            shooter.Tick(.1f); shooter.Tick(.3f);
            Check(Vector3.Distance(shooter.Agent.destination, arrived) >= .5f, "到点出手后重选另一个射位");
            manager.Recycle(shot);
            target.SetPositionAndRotation(new Vector3(100, .15f, 0), Quaternion.identity);
            var rear = await manager.SpawnEnemyAsync("zhihuocanji", new Vector3(100, 0, -18), Quaternion.identity, target, true);
            rear.Controller.tickAutomatically = false;
            rear.Controller.attackDistance = 10;
            anchor.position = new Vector3(104, 0, -16);
            rear.Controller.patrolPoints = new[] { anchor };
            rear.Controller.Tick(.1f);
            Check(rear.Controller.CurrentBehaviour == EnemyController.Behaviour.Patrol &&
                Vector3.Distance(rear.Controller.Agent.destination, anchor.position) < .3f, "全部可达射位在视野外时回退巡逻");
            manager.Recycle(rear);
            target.position = Vector3.up * .15f;

            Check(manager.catalog.TryGet("elite-canjiou", out var definition) && definition.summoning, "可按稳定编号生成精英");
            var rule = Object.Instantiate(definition.summoning);
            rule.baseCount = 8; rule.maxAlive = 10;
            Check(rule.CountForWave(1,0)==8 && rule.CountForWave(2,0)==4 && rule.CountForWave(3,0)==3 &&
                rule.CountForWave(2,9)==1, "对数衰减与剩余槽位取小");
            rule.allowRepeat = false;
            Check(rule.CountForWave(2,0)==0, "关闭重复召唤后第二波为零");
            Object.Destroy(rule);
            var elite = await manager.SpawnEnemyAsync("elite-canjiou", Vector3.right * 12, Quaternion.identity, target, true);
            elite.Controller.tickAutomatically = false;
            CommandWave(elite, .1f, definition.summoning);
            await WaitFor(() => manager.ActiveCount == 4 && manager.ActiveEnemies.Values.All(c => c.Controller.CanReceiveHit));
            Check(manager.ActiveEnemies.Values.Count(c=>c.EnemyId=="canjiou")==3, "精英发现玩家后首波三个普通怪均有租约");
            elite.Controller.Tick(5);
            Check(manager.ActiveCount==4, "冷却期不增生");
            CommandWave(elite, 10, definition.summoning);
            await WaitFor(() => manager.ActiveCount == 5 && manager.ActiveEnemies.Values.All(c => c.Controller.CanReceiveHit));
            CommandWave(elite, 15, definition.summoning);
            Check(manager.ActiveCount==5, "第二波只补一个，达到存活上限后不超发");
            var old = manager.ActiveEnemies.Values.First(c=>c.EnemyId=="canjiou");
            manager.Recycle(old);
            CommandWave(elite, 15, definition.summoning);
            await WaitFor(() => manager.ActiveCount == 5 && manager.ActiveEnemies.Values.All(c => c.Controller.CanReceiveHit));
            Check(!manager.IsCurrent(old) && manager.ActiveEnemies.Values.Any(c=>c.Controller==old.Controller && c.Id!=old.Id),
                "复用小怪分配新租约");
            elite.Controller.SetSuspended(true);
            Check(manager.ActiveEnemies.Values.All(c => !c.Controller.CanReceiveHit), "暂停传递给本次精英的召唤物");
            elite.Controller.SetSuspended(false);
            Check(manager.ActiveEnemies.Values.All(c => c.Controller.CanReceiveHit), "恢复后召唤物返回战斗");
            manager.Recycle(elite);
            Check(manager.ActiveCount==0 && !manager.Recycle(old), "房间回收精英后小怪全部回池且旧租约无效");
            var second = await manager.SpawnEnemyAsync("elite-canjiou", Vector3.right * 12, Quaternion.identity, target, true);
            second.Controller.tickAutomatically = false;
            EliteFormationChecks.VerifyRetiredCommander(second.Controller.GetComponent<EliteCommandFormation>(),
                elite, Array.Empty<EnemyLease>(), definition.summoning.formationMaterial);
            CommandWave(second, .1f, definition.summoning);
            await WaitFor(() => manager.ActiveCount == 4 && manager.ActiveEnemies.Values.All(c => c.Controller.CanReceiveHit));
            Check(second.Controller==elite.Controller && second.Id!=elite.Id, "精英复用重新从第一波开始");
            second.Controller.TryDie();
            Check(manager.ActiveCount==1, "精英死亡立即清理所辖小怪");
            manager.Recycle(second);
            manager.sampleRadius = .35f;
            var edge = await manager.SpawnEnemyAsync("elite-canjiou", Vector3.right * 28.5f, Quaternion.identity, target, true);
            edge.Controller.tickAutomatically = false;
            CommandWave(edge, .1f, definition.summoning);
            await WaitFor(() => manager.Status.Contains("出生位置") && manager.ActiveCount == 1);
            Check(manager.ActiveCount == 1, "第二只小怪出生越出导航边界时回滚本波第一只");
            edge.Controller.Agent.Warp(Vector3.right * 10);
            CommandWave(edge, 15f, definition.summoning);
            await WaitFor(() => manager.ActiveCount == 4 && manager.ActiveEnemies.Values.All(c => c.Controller.CanReceiveHit));
            Check(manager.ActiveCount == 4, "修正位置后仍按首波三只重试");
            await CheckTwoCommanders(manager, target, edge, definition.summoning);
            await InterruptPartialWave(manager, target, definition.summoning);
            await SummonApproachChecks.Run(manager, target, Vector3.zero, Check);
        }
        finally
        {
            Object.Destroy(root); Object.Destroy(target.gameObject); Object.Destroy(anchor.gameObject); Object.Destroy(wall);
            rearSurface.Remove(); surface.Remove();
        }
    }
}
