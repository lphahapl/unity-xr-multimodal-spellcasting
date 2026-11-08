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
        public bool UsesEnemyManager => enemyManager != null;
        public bool IsLoadingEnemies => pendingSpawns != null;
        public Task SpawnTask { get; private set; } = Task.CompletedTask;
        readonly List<EnemyLease> roomLeases = new();
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
                inside = false; CancelPendingSpawns(); if (!PauseManagedEnemies()) return; rpg.SetEncounterCombat(false);
                status = rpg.Player.Alive ? "玩家暂停，敌人已暂停" : "玩家倒下，等待恢复检查点"; return;
            }
            if (complete) { rpg.SetEncounterCombat(false); status = "房间已完成，探索中"; return; }
            if (started && roomLeases.Any(l => !enemyManager.IsCurrent(l)))
            {
                if (!PauseManagedEnemies()) return; rpg.SetEncounterCombat(false);
                status = "房间实例已被外部回收或管理器重启，请重置遭遇"; return;
            }
            if (started && roomLeases.Count > 0 && roomLeases.All(l => Defeated(l.Controller)))
            { FinishEncounter(); return; }
            if (!ContainsFloor(rpg.locomotion.PlayerFloorPosition, inside))
            {
                inside = false; CancelPendingSpawns(); if (!PauseManagedEnemies()) return; rpg.SetEncounterCombat(false);
                status = started ? "已离开房间，敌人保留生命并暂停" : "练习区：等待玩家进入房间"; return;
            }
            inside = true;
            if (!started)
            {
                if (spawnFailed) { rpg.SetEncounterCombat(false); return; }
                if (pendingSpawns == null)
                {
                    var request = new CancellationTokenSource(); pendingSpawns = request;
                    SpawnTask = SpawnRoomAsync(revision, request);
                }
                if (!started) { if (!spawnFailed) { status = "正在异步准备房间敌人"; rpg.SetEncounterCombat(true); } return; }
            }
            if (pausedEnemies)
            {
                pausedEnemies = false;
                foreach (var lease in roomLeases) if (enemyManager.IsCurrent(lease) && !Defeated(lease.Controller)) lease.Controller.SetSuspended(false);
            }
            rpg.SetEncounterCombat(true); status = "房间战斗中";
        }
        async Task SpawnRoomAsync(int token, CancellationTokenSource request)
        {
            var created = new List<EnemyLease>();
            var manager = enemyManager;
            try
            {
                // Snapshot the batch so Inspector changes do not change an in-flight request.
                var batch = spawnPoints.Select(s => (s.enemyId, s.point.position, s.point.rotation, s.isSummoned)).ToArray();
                foreach (var item in batch)
                {
                    var lease = await manager.SpawnEnemyAsync(item.enemyId, item.position, item.rotation, rpg.locomotion.head, item.isSummoned, request.Token);
                    created.Add(lease);
                    if (!this || !isActiveAndEnabled || token != revision || request.IsCancellationRequested) throw new OperationCanceledException();
                    if (!manager.IsCurrent(lease)) throw new OperationCanceledException("出生租约已失效");
                    lease.Controller.SetSuspended(true);
                }
                if (!this || !isActiveAndEnabled || token != revision || request.IsCancellationRequested ||
                    !rpg.CanAct || !ContainsFloor(rpg.locomotion.PlayerFloorPosition, false) || created.Any(l => !manager.IsCurrent(l))) throw new OperationCanceledException();
                roomLeases.AddRange(created); created.Clear();
                enemies = roomLeases.Select(l => l.Controller).ToArray();
                started = true; pausedEnemies = false;
                foreach (var lease in roomLeases) if (manager.IsCurrent(lease)) lease.Controller.SetSuspended(false);
                rpg.SetEncounterCombat(true); status = "房间战斗中";
            }
            catch (OperationCanceledException) { /* Leaving/resetting invalidates just this batch. */ }
            catch (Exception error)
            {
                if (this && token == revision)
                { spawnFailed = true; status = "敌人准备失败：" + error.Message; if (rpg) rpg.SetEncounterCombat(false); Debug.LogWarning(status, this); }
            }
            finally
            {
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
            var owned = roomLeases.ToArray();
            roomLeases.Clear();
            foreach (var lease in owned) if (enemyManager) enemyManager.Recycle(lease);
        }
        void ResetManagedEncounter()
        {
            if (resetting) return;
            resetting = true; ++revision;
            try
            {
                RecycleRoomLeases(); enemies = Array.Empty<EnemyController>();
                started = complete = inside = spawnFailed = false; pausedEnemies = true;
                if (rpg) rpg.SetEncounterCombat(false); status = "遭遇已重置，实例已回池，等待进入";
            }
            finally { resetting = false; }
        }
        [ContextMenu("重试敌人异步准备")]
        public void RetryEnemyLoading() { spawnFailed = false; TickEncounter(); }
    }
}
