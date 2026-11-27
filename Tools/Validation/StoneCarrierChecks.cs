// 复制到隔离项目的 Assets/Editor 后，以 -batchmode -nographics -executeMethod StoneCarrierChecks.Run 启动。
using System;
using System.Linq;
using System.Threading.Tasks;
using NarutoVR.SealCombat.Enemies;
using NarutoVR.SealCombat.Rpg;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class StoneCarrierChecks
{
    const string Pending = "StoneCarrierChecks.Pending";
    const string ScenePath = "Assets/Scene/StoneCarrierGreybox.unity";
    static int count;
    static StoneCarrierChecks() => EditorApplication.update += StartWhenPlaying;
    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("请在隔离工程内运行");
        EditorSceneManager.OpenScene(ScenePath);
        SessionState.SetBool(Pending, true);
        EditorApplication.EnterPlaymode();
    }
    static async void StartWhenPlaying()
    {
        if (!Application.isPlaying || !SessionState.GetBool(Pending, false) || EditorApplication.isCompiling) return;
        SessionState.EraseBool(Pending);
        try { await Verify(); await VerifyRoom(); Debug.Log($"CARRIER_PASS {count}"); EditorApplication.Exit(0); }
        catch (Exception e) { Debug.LogError("CARRIER_FAIL " + e); EditorApplication.Exit(1); }
    }
    static void Check(bool ok, string label)
    {
        if (!ok) throw new Exception(label);
        Debug.Log("CARRIER_CHECK " + (++count) + " " + label);
    }
    static async Task Wait(Func<bool> done, string label, int seconds = 30)
    {
        double until = EditorApplication.timeSinceStartup + seconds;
        bool reached;
        while (!(reached = done()) && EditorApplication.timeSinceStartup < until) await Awaitable.NextFrameAsync();
        Check(reached, label);
    }
    static async Task Verify()
    {
        var lab = Object.FindFirstObjectByType<StoneCarrierGreybox>();
        await Wait(() => lab && lab.Lease != null, "打开独立运输灰盒后通过正式目录和 Addressables 出生");
        var basket = lab.post.basket;
        int dockCount = 0;
        basket.onDocked.AddListener(() => dockCount++);
        var first = lab.Lease;
        var occluded = new GameObject("不可达的最远撤离点");
        occluded.transform.position = Vector3.right * 200;
        var original = lab.post.patrolPoints;
        lab.post.patrolPoints = original.Append(occluded.transform).ToArray();
        for (int life = 1; life <= 2; ++life)
        {
            var lease = lab.Lease;
            var c = lease.Controller;
            var worker = c.GetComponent<StoneCarrier>();
            Check(lab.post.Bind(lease, lab.player), "本轮出生点绑定当前租约");
            var stone = worker.Stone;
            if (life == 1)
            {
                var other = new GameObject("另一个出生点").AddComponent<StoneCarrierPost>();
                other.basket = basket;
                Check(!other.Bind(lease,lab.player), "第二个出生点不能接管已绑定的篮");
                other.ResetPost();
                Check(basket.Stone == stone, "清理其他出生点不会清掉本组镇石");
                Object.Destroy(other.gameObject);
            }
            Check(stone && basket.Stone == stone && stone.transform.IsChildOf(c.transform) &&
                stone.State == BallastStone.Phase.Carried && !stone.GetComponent<Collider>().enabled,
                $"第 {life} 轮挂载真实镇石且背载碰撞关闭");
            var box = stone.GetComponent<BoxCollider>();
            Check((Vector3.Scale(box.size, stone.transform.lossyScale) - Vector3.one * 1.05f).magnitude < .01f,
                "镇石碰撞包络 1.05 米见方");
            Check(!basket.Boarded && !stone.GetComponent<GlyphHeavyBody>().CanManipulate && !lab.Pull(), "背载时拒绝千钧符");
            int attacks = 0;
            c.onAttackStarted.AddListener(_ => attacks++);
            await Wait(() => c.CurrentBehaviour == EnemyController.Behaviour.Patrol, "玩家在场时工奴继续巡逻");
            var hitAt = c.transform.position;
            Check(lab.Strike(false) && worker.Retreating, "普通受击通过正式伤害管线进入撤离");
            await Wait(() => worker.Retreating && c.Agent.hasPath && Vector3.Distance(c.Agent.destination, original[0].position) < .4f,
                "绕过不可达的最远候选，选择远离攻击者的可达巡逻点");
            await Wait(() => (c.transform.position - hitAt).magnitude > .65f, "真 NavMeshAgent 实际撤离至少 0.65 米");
            Check(attacks == 0 && c.CurrentAttack < 0, "巡逻和撤离均未发起攻击");
            var killedAt = c.transform.position;
            Check(lab.Strike(true) && lease.Defeated, "致死受击提交当前租约击败状态");
            Check(!c.TryDie() && !lab.Strike(true) && stone == basket.Stone && !stone.transform.parent &&
                Object.FindObjectsByType<BallastStone>(FindObjectsSortMode.None).Length == 1 &&
                stone.State == BallastStone.Phase.Dropped && stone.GetComponent<Collider>().enabled,
                "重复击败不重复掉落，背载的同一实例脱离敌人");
            Check(Vector3.ProjectOnPlane(stone.transform.position - killedAt, Vector3.up).magnitude < .02f &&
                (c.transform.position - lab.post.transform.position).magnitude > .5f, "掉石取实际击破位置");
            Check(!stone.GetComponent<GlyphHeavyBody>().CanManipulate && !basket.Boarded, "未登篮的落地石不响应符力");
            lab.manager.Recycle(lease);
            Check(!lab.manager.IsCurrent(lease) && stone, "敌人退池后当轮掉石仍留在场景");
            if (life == 1)
            {
                lab.Board();
                await Wait(() => basket.Boarded && stone.GetComponent<GlyphHeavyBody>().CanManipulate, "玩家代表点真正进入 A 篮后解锁镇石");
                var start = stone.transform.position;
                await Wait(() => lab.Pull(), "正式千钧符从篮内射线锁中掉落镇石");
                await Wait(() => stone.MovedByGlyph && Vector3.Distance(start, stone.transform.position) > .45f,
                    "持笔后拉实际施力，物理世界中镇石前进");
                for (int tries = 0; tries < 5 && stone.State != BallastStone.Phase.Docked; tries++)
                {
                    for (int i = 0; i < 90 && stone.State != BallastStone.Phase.Docked; i++) await Awaitable.FixedUpdateAsync();
                    if (stone.State != BallastStone.Phase.Docked) lab.Pull();
                }
                Debug.Log($"CARRIER_TRAVEL start={start:F2} final={stone.transform.position:F2} slot={basket.slot.position:F2} range={Vector3.Distance(start,basket.slot.position):F2}");
                Check(stone.State == BallastStone.Phase.Docked && stone.transform.parent == basket.slot &&
                    (stone.transform.position-basket.slot.position).sqrMagnitude < .0001f && dockCount == 1,
                    "正式千钧符牵引原石至槽心，A 篮只结算一次");
                Check(!stone.GetComponent<GlyphHeavyBody>().CanManipulate && stone.GetComponent<Rigidbody>().isKinematic, "就位后卸除符力并随篮运动");
                var firstId = stone.GetInstanceID();
                lab.Spawn();
                await Wait(() => lab.Lease != null && lab.Lease.Id != first.Id, "启动下一轮真实出生");
                Check(lab.Lease.Controller == first.Controller && basket.Stone.GetInstanceID() != firstId && !basket.Boarded,
                    "同一工奴复用，新租约有新背石且重新要求登篮");
                Check(!lab.post.Bind(first,lab.player), "旧租约不能重新绑定篮");
            }
            else
            {
                stone.GetComponent<Rigidbody>().position = basket.slot.position;
                Physics.SyncTransforms();
                lab.Board();
                await Wait(() => basket.Boarded, "第二轮重新登篮");
                for (int j = 0; j < 5; j++) await Awaitable.FixedUpdateAsync();
                Check(stone.State == BallastStone.Phase.Dropped && !stone.MovedByGlyph && dockCount == 1,
                    "仅靠位置或外力把石头放进槽，不能结算千钧符运输");
                lab.post.ResetPost();
                await Awaitable.NextFrameAsync();
                Check(!stone && !basket.Stone && !basket.Boarded, "重置释放本轮落石及乘员状态");
            }
        }
    }

    static async Task VerifyRoom()
    {
        // 在已有练习房间的内存副本挂 A 篮，验证整批成功后的真实接线；不会保存既有关卡。
        EditorSceneManager.LoadSceneInPlayMode("Assets/Scene/KaogongP1Trial.unity", new LoadSceneParameters(LoadSceneMode.Single));
        await Awaitable.NextFrameAsync();
        var room = Object.FindFirstObjectByType<CombatRoomEncounter>();
        var rpg = room.rpg;
        typeof(LinearRpgController).GetMethod("OnApplicationPause", System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic).Invoke(rpg, new object[]{false});
        await Wait(() => rpg.CanAct, "既有练习房间玩家流程就绪");
        var move = rpg.locomotion;
        var original = room.spawnPoints[0];
        room.ResetEncounter();
        var station = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Enemies/CanJiOu/StoneCarrierStation.prefab"));
        var post = station.GetComponentInChildren<StoneCarrierPost>();
        station.transform.position += original.point.position - post.transform.position;
        room.spawnPoints = new[]{new CombatRoomEncounter.SpawnPoint{enemyId="stone-carrier",point=post.transform}, original};
        var center = room.area.transform.TransformPoint(room.area.center); center.y = original.point.position.y;
        move.rigRoot.position += center - move.PlayerFloorPosition;
        Physics.SyncTransforms(); room.TickEncounter();
        await room.SpawnTask;
        Check(room.HasStarted && post.basket.Stone == room.enemies[0].GetComponent<StoneCarrier>().Stone,
            "既有房间的正式异步生成批次自动绑定本组工奴与篮");
        var stone = post.basket.Stone;
        Check(room.enemies[0].Entity.ReceiveHit("灰盒-房间工奴", room.enemies[0].Entity.Combat.Health, 0), "房间工奴通过真实受击掉石");
        post.basket.transform.position += move.head.position - post.basket.passengerVolume.transform.TransformPoint(post.basket.passengerVolume.center);
        Physics.SyncTransforms();
        for (int i=0;i<3;i++) await Awaitable.FixedUpdateAsync();
        Check(!room.IsComplete && !post.basket.Boarded && !stone.GetComponent<GlyphHeavyBody>().CanManipulate,
            "玩家虽已在篮里，房间另一名敌人仍存活时不开放符力");
        Check(room.enemies[1].Entity.ReceiveHit("灰盒-房间末名", room.enemies[1].Entity.Combat.Health, 0), "房间末名敌人可正常击破");
        room.TickEncounter();
        await Wait(() => room.IsComplete && post.basket.Boarded && stone.GetComponent<GlyphHeavyBody>().CanManipulate,
            "正式房间清场后，篮内玩家获得对本组原石的符力");
        move.rigRoot.position += Vector3.right * (room.area.bounds.size.x + 5f);
        Physics.SyncTransforms();
        room.ResetEncounter();
        Check(!post.basket.Stone && !post.basket.Boarded && !stone.gameObject.activeSelf,
            "房间检查点重置立即清除本组绑定并禁用旧石头");
        await Wait(() => !stone, "房间检查点重置销毁本轮已掉落的镇石");
    }
}
