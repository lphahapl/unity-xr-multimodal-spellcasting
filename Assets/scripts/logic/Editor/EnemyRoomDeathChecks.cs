using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NarutoVR.SealCombat.Enemies;
using NarutoVR.SealCombat.Rpg;
using UnityEngine;

namespace NarutoVR.SealCombat.Editor
{
    public static class EnemyRoomDeathChecks
    {
        // Two room lives and a third, unrelated renter exercise an instance changing owners mid-encounter.
        public static async Task<string> Run(CombatRoomEncounter room)
        {
            var failures = new List<string>();
            int checks = 0, cleared = 0;
            void Check(bool valid, string name)
            {
                checks++;
                if (!valid) failures.Add(name);
            }
            void OnCleared() => cleared++;
            var manager = room.enemyManager;
            var rpg = room.rpg;
            var move = rpg.locomotion;
            var originalPoints = room.spawnPoints;
            var source = originalPoints[0];
            float originalDelta = Time.captureDeltaTime;
            EnemyLease outsider = null;
            var secondPoint = new GameObject("第二个房间测试出生点");
            secondPoint.transform.SetPositionAndRotation(source.point.position + Vector3.right * .6f, source.point.rotation);
            room.Cleared += OnCleared;
            try
            {
                Time.captureDeltaTime = 1f / 60;
                room.ResetEncounter();
                room.spawnPoints = new[] { source, new CombatRoomEncounter.SpawnPoint { enemyId = source.enemyId, point = secondPoint.transform } };
                void Enter()
                {
                    var center = room.area.transform.TransformPoint(room.area.center);
                    center.y = source.point.position.y;
                    move.rigRoot.position += center - move.PlayerFloorPosition;
                    Physics.SyncTransforms();
                    room.TickEncounter();
                }
                async Task SpawnRoom()
                {
                    Enter();
                    await EnemyPoolingChecks.WithTimeout(room.SpawnTask);
                    if (!room.HasStarted || room.enemies.Length != 2)
                        throw new InvalidOperationException("两名房间敌人未成功出生：" + room.Status);
                }
                await SpawnRoom();
                var first = manager.ActiveEnemies.Values.Single(lease => lease.Controller == room.enemies[0]);
                var second = manager.ActiveEnemies.Values.Single(lease => lease.Controller == room.enemies[1]);
                Check(first.Controller.Entity.ReceiveHit("房间验证-首名", 10000, 0), "首名敌人接受致死命中");
                room.TickEncounter();
                Check(!room.IsComplete && cleared == 0 && manager.IsCurrent(first), "一名死亡、另一名存活时继续战斗并播放死亡动画");
                int deathHash = Animator.StringToHash("Base Layer.Dead");
                float highest = -1;
                for (int frame = 0; frame < 900 && manager.IsCurrent(first); frame++)
                {
                    var state = first.Controller.animator.GetCurrentAnimatorStateInfo(0);
                    if (state.fullPathHash == deathHash) highest = Mathf.Max(highest, state.normalizedTime);
                    if (highest > 1.15f) break;
                    await Awaitable.NextFrameAsync();
                }
                Check(highest > .5f && !manager.IsCurrent(first), $"死亡动作播毕后首名租约失效（已观察 Dead 进度 {highest:F2}）");
                if (manager.IsCurrent(first)) manager.Recycle(first); // Also expose the previous stale-room failure.
                outsider = await manager.SpawnEnemyAsync(source.enemyId, source.point.position, source.point.rotation, null, false);
                Check(outsider.Controller == first.Controller && outsider.Id != first.Id, "同一对象以新编号借给房间外部");
                room.TickEncounter();
                Check(!room.IsComplete && move.EncounterCombat && second.Controller.CanReceiveHit,
                    "首名对象被其他系统复用后，房间仍允许击败第二名");
                Check(second.Controller.Entity.ReceiveHit("房间验证-末名", 10000, 0), "第二名接受致死命中");
                room.TickEncounter();
                Check(room.IsComplete && cleared == 1 && !move.EncounterCombat, "两次出生都已击败，恰好清房一次");
                room.ResetEncounter();
                Check(!room.IsComplete && manager.IsCurrent(outsider), "重置不会回收其他系统复用的出生");
                await SpawnRoom();
                var missing = manager.ActiveEnemies.Values.Single(lease => lease.Controller == room.enemies[0]);
                manager.Recycle(missing);
                room.TickEncounter();
                Check(!room.IsComplete && !move.EncounterCombat && room.Status.Contains("外部回收"), "活敌被外部回收时提示重置，仍不计入击败");
                if (failures.Count != 0) throw new InvalidOperationException(string.Join(" / ", failures));
                var playback = new List<string>();
                await EnemyDeathPlaybackChecks.Run(room, playback.Add);
                return $"双敌房间、外部复用和重置 {checks} 项通过（实际 Dead 进度 {highest:F2}）。" +
                    $"死亡播放及清理专项 {playback.Count} 项通过。";
            }
            finally
            {
                room.Cleared -= OnCleared;
                room.ResetEncounter();
                room.spawnPoints = originalPoints;
                if (outsider != null) manager.Recycle(outsider);
                UnityEngine.Object.Destroy(secondPoint);
                Time.captureDeltaTime = originalDelta;
                rpg.RestoreCheckpoint();
            }
        }
    }
}
