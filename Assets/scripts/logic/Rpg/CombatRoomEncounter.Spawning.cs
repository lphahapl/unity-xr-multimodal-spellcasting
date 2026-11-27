using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NarutoVR.SealCombat.Enemies;
using UnityEngine;

namespace NarutoVR.SealCombat.Rpg
{
    public sealed partial class CombatRoomEncounter
    {
        [Serializable]
        public sealed class SpawnPoint
        {
            [InspectorName("怪物类型ID")] public string enemyId = "canjiou";
            [InspectorName("出生位置与朝向")] public Transform point;
            [InspectorName("派生怪物（直接追目标）")] public bool isSummoned;
        }
        [Header("四、异步生成（配置后替代预置敌人列表）")]
        [InspectorName("敌人管理服务")] public EnemyManager enemyManager;
        [InspectorName("怪物出生点")] public SpawnPoint[] spawnPoints = Array.Empty<SpawnPoint>();
        [InspectorName("入场前显示守敌"), Tooltip("在附近时提前加载同一批敌人。玩家脚底入场后才启用攻击和受击。")]
        public bool showEnemiesBeforeEngagement;
        [InspectorName("守敌显示距离（米）"), Min(0f)] public float previewDistance = 32f;
        public bool UsesEnemyManager => enemyManager != null;
        public bool IsLoadingEnemies => pendingSpawns != null;
        public Task SpawnTask { get; private set; } = Task.CompletedTask;
        readonly List<EnemyLease> roomLeases = new();
        readonly List<StoneCarrierPost> carrierPosts = new();
        CancellationTokenSource pendingSpawns;
        bool spawnFailed;

        bool EnsureManagedSetup()
        {
            if (spawnPoints == null || spawnPoints.Length == 0 || spawnPoints.Any(s => s == null || !s.point || string.IsNullOrWhiteSpace(s.enemyId)))
            { status = "异步生成未接线：缺少怪物ID或出生点"; return false; }
            if (subscribed != rpg)
            {
                if (subscribed) subscribed.CheckpointRestored -= ResetEncounter;
                subscribed = rpg; subscribed.CheckpointRestored += ResetEncounter;
            }
            return true;
        }
        void TickManagedEncounter()
        {
            if (!rpg.CanAct)
            {
                inside = false; CancelPendingSpawns(); if (!PauseManagedEnemies()) return; rpg.RequestEncounterCombat(this, false);
                status = rpg.Player.Alive ? "玩家暂停，敌人已暂停" : "玩家倒下，等待恢复检查点"; return;
            }
            if (complete) { rpg.RequestEncounterCombat(this, false); status = "房间已完成，探索中"; return; }
            if (started && CheckRoomDefeats()) return;
            var feet = rpg.locomotion.PlayerFloorPosition;
            inside = ContainsFloor(feet, inside);
            if (!inside && (started || !WithinPreviewDistance(feet)))
            {
                CancelPendingSpawns();
                if (!started && roomLeases.Count > 0)
                {
                    RecycleRoomLeases();
                    enemies = Array.Empty<EnemyController>();
                }
                if (!PauseManagedEnemies()) return;
                rpg.RequestEncounterCombat(this, false);
                status = started ? "已离开房间，敌人保留生命并暂停" : "练习区：等待玩家进入房间";
                return;
            }
            if (roomLeases.Count == 0)
            {
                if (spawnFailed) { rpg.RequestEncounterCombat(this, false); return; }
                if (pendingSpawns == null)
                {
                    var request = new CancellationTokenSource(); pendingSpawns = request;
                    SpawnTask = SpawnRoomAsync(revision, request);
                }
                if (roomLeases.Count == 0)
                {
                    if (!spawnFailed) { status = "正在异步准备房间敌人"; rpg.RequestEncounterCombat(this, inside); }
                    return;
                }
            }
            ResumeManagedEnemies(preview: !inside);
        }

        bool WithinPreviewDistance(Vector3 feet) => showEnemiesBeforeEngagement && area.enabled &&
            (feet - area.ClosestPoint(feet)).sqrMagnitude <= previewDistance * previewDistance;

        // 整批就绪后才解开暂停。预展示仍封锁受击与索敌；首次越线只切换权限。
        void ResumeManagedEnemies(bool preview)
        {
            bool firstEngagement = !preview && !started;
            if (!preview) started = true;
            if (pausedEnemies || firstEngagement)
            {
                int token = revision;
                pausedEnemies = false;
                foreach (var lease in roomLeases)
                {
                    if (enemyManager.IsCurrent(lease) && !Defeated(lease.Controller))
                    {
                        lease.PreviewPatrol = preview;
                        lease.Controller.SetSuspended(false);
                    }
                    if (!this || token != revision || !isActiveAndEnabled) return;
                }
            }
            rpg.RequestEncounterCombat(this, !preview);
            status = preview ? "守敌巡逻中，等待玩家入场" : "房间战斗中";
        }

        bool CheckRoomDefeats()
        {
            bool allDefeated = roomLeases.Count > 0;
            for (int i = 0; i < roomLeases.Count; ++i)
            {
                var lease = roomLeases[i];
                // The controller can already belong to a different spawn while this room is still fighting.
                if (lease.Defeated)
                {
                    enemies[i] = null;
                    continue;
                }
                if (!enemyManager.IsCurrent(lease))
                {
                    enemies[i] = null;
                    if (!PauseManagedEnemies()) return true;
                    rpg.RequestEncounterCombat(this, false);
                    status = "房间实例已被外部回收或管理器重启，请重置遭遇";
                    return true;
                }
                allDefeated = false;
            }
            if (allDefeated) FinishEncounter();
            return allDefeated;
        }
        async Task SpawnRoomAsync(int token, CancellationTokenSource request)
        {
            var created = new List<EnemyLease>();
            var boundPosts = new List<StoneCarrierPost>();
            var manager = enemyManager;
            var owner = rpg;
            bool Current() => this && isActiveAndEnabled && token == revision && !request.IsCancellationRequested &&
                owner && owner == rpg && manager == enemyManager;
            try
            {
                // Snapshot the batch so Inspector changes do not change an in-flight request.
                var batch = spawnPoints.Select(s => (s.enemyId, s.point.position, s.point.rotation, s.isSummoned,
                    carrierPost: s.point.GetComponent<StoneCarrierPost>(), pace: s.point.GetComponent<EncounterEnemyPace>())).ToArray();
                foreach (var item in batch)
                {
                    var lease = await manager.SpawnEnemyAsync(item.enemyId, item.position, item.rotation, owner.locomotion.head, item.isSummoned, request.Token);
                    created.Add(lease);
                    if (!Current()) throw new OperationCanceledException();
                    if (!manager.IsCurrent(lease)) throw new OperationCanceledException("出生租约已失效");
                    lease.Controller.SetSuspended(true);
                    if (item.pace && item.pace.isActiveAndEnabled) lease.Pace = item.pace;
                }
                if (!Current() || !owner.CanAct || created.Any(l => !manager.IsCurrent(l))) throw new OperationCanceledException();
                var feet = owner.locomotion.PlayerFloorPosition;
                bool enter = ContainsFloor(feet, false);
                if (!enter && !WithinPreviewDistance(feet)) throw new OperationCanceledException();
                for (int i = 0; i < batch.Length; i++)
                {
                    var post = batch[i].carrierPost;
                    if (!post) continue;
                    if (!post.Bind(created[i], owner.locomotion.head, this))
                        throw new InvalidOperationException("背石工奴出生点缺少工奴、A 篮或槽心：" + post.name);
                    boundPosts.Add(post);
                }
                carrierPosts.AddRange(boundPosts); boundPosts.Clear();
                roomLeases.AddRange(created); created.Clear();
                enemies = roomLeases.Select(l => l.Controller).ToArray();
                pausedEnemies = true;
                inside = enter;
                ResumeManagedEnemies(preview: !enter);
            }
            catch (OperationCanceledException) { /* Leaving/resetting invalidates just this batch. */ }
            catch (Exception error)
            {
                if (Current())
                { spawnFailed = true; status = "敌人准备失败：" + error.Message; owner.RequestEncounterCombat(this, false); Debug.LogWarning(status, this); }
            }
            finally
            {
                foreach (var post in boundPosts) if (post) post.ResetPost();
                foreach (var lease in created) if (manager) manager.Recycle(lease);
                if (ReferenceEquals(pendingSpawns, request)) pendingSpawns = null;
                request.Dispose();
            }
        }
        void CancelPendingSpawns()
        {
            if (pendingSpawns == null) return;
            ++revision; var request = pendingSpawns; pendingSpawns = null; request.Cancel();
        }
        bool PauseManagedEnemies()
        {
            if (pausedEnemies) return true;
            pausedEnemies = true;
            int token = revision;
            foreach (var lease in roomLeases.ToArray())
            {
                if (enemyManager && enemyManager.IsCurrent(lease) && !Defeated(lease.Controller)) lease.Controller.SetSuspended(true);
                // Ending an attack can call user events that reset or disable this room.
                if (!this || revision != token) return false;
            }
            return true;
        }
        void RecycleRoomLeases()
        {
            CancelPendingSpawns();
            ResetCarrierPosts();
            var owned = roomLeases.ToArray();
            roomLeases.Clear();
            foreach (var lease in owned) if (enemyManager) enemyManager.Recycle(lease);
        }
        void ResetCarrierPosts()
        {
            foreach (var post in carrierPosts) if (post) post.ResetPost();
            carrierPosts.Clear();
        }
        void ResetManagedEncounter()
        {
            if (resetting) return;
            resetting = true; ++revision;
            try
            {
                RecycleRoomLeases(); enemies = Array.Empty<EnemyController>();
                started = complete = inside = spawnFailed = false; pausedEnemies = true;
                if (rpg) rpg.RequestEncounterCombat(this, false); status = "遭遇已重置，实例已回池，等待进入";
            }
            finally { resetting = false; }
        }
        [ContextMenu("重试敌人异步准备")]
        public void RetryEnemyLoading() { spawnFailed = false; TickEncounter(); }
    }
}
