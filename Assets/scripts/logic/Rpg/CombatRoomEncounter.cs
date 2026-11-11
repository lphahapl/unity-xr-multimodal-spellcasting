using System;
using System.Collections.Generic;
using NarutoVR.SealCombat.Enemies;
using NarutoVR.SealCombat.Events;
using UnityEngine;

namespace NarutoVR.SealCombat.Rpg
{
    /// <summary>场景局部遭遇。区域只读取玩家地面代表点；敌人退出暂停，检查点恢复才重新生成。</summary>
    [DefaultExecutionOrder(-100), DisallowMultipleComponent]
    public sealed partial class CombatRoomEncounter : MonoBehaviour
    {
        [Header("一、房间接线")]
        [InspectorName("玩家流程控制器")] public LinearRpgController rpg;
        [InspectorName("房间范围（触发盒）")] public BoxCollider area;
        [InspectorName("房间敌人")] public EnemyController[] enemies = Array.Empty<EnemyController>();
        [Header("二、进出判定")]
        [InspectorName("退出边界余量（米）"), Min(0)] public float exitHysteresis = .3f;
        [Header("三、运行状态")]
        [SerializeField, InspectorName("遭遇状态（运行时）")] string status = "等待接线";
        public string Status => status;
        public bool HasStarted => started;
        public bool IsComplete => complete;
        public event Action Cleared;
        public event Action CompletionChanged;
        public bool PlayerInside => inside;

        struct Spawn
        {
            public EnemyController Enemy;
            public Vector3 Position;
            public Quaternion Rotation;
            public bool Summoned;
        }
        readonly List<Spawn> spawns = new();
        LinearRpgController subscribed;
        IDisposable enemyVitalsSubscription;
        IDisposable detectionSubscription;
        bool captured, started, complete, inside, pausedEnemies = true, resetting, ticking;
        int revision;

        void Awake() => EnsureSetup();
        void OnEnable()
        {
            EnsureSetup();
            enemyVitalsSubscription = EventCenter.Subscribe(EnemyEvents.VitalsChanged, TraceEnemyDamage, this);
            detectionSubscription = EventCenter.Subscribe(EnemyEvents.FirstPlayerDetected, TraceFirstDetection, this);
        }
        void Update() => TickEncounter();
        void OnDisable()
        {
            enemyVitalsSubscription?.Dispose();
            enemyVitalsSubscription = null;
            detectionSubscription?.Dispose();
            detectionSubscription = null;
            CancelPendingSpawns();
            ++revision;
            if (subscribed) subscribed.CheckpointRestored -= ResetEncounter;
            subscribed = null;
            inside = false;
            PauseEnemies();
            if (rpg) rpg.SetEncounterCombat(false);
        }
        void OnDestroy()
        {
            RecycleRoomLeases();
            foreach (var spawn in spawns)
                if (spawn.Enemy) spawn.Enemy.SetSceneRespawnOwner(false);
        }

        bool TracesEnemy(EnemyObj entity, out long leaseId)
        {
            leaseId = 0;
            if (!entity || !rpg || !rpg.skills || !rpg.skills.enableCastTrace) return false;
            if (!UsesEnemyManager)
                return enemies != null && Array.Exists(enemies, enemy => enemy && enemy.Entity == entity);
            foreach (var lease in roomLeases)
                if (enemyManager.IsCurrent(lease) && lease.Controller.Entity == entity)
                {
                    leaseId = lease.Id;
                    return true;
                }
            return false;
        }

        void TraceFirstDetection(EnemyDetectionNotice notice)
        {
            if (!notice.Enemy || !TracesEnemy(notice.Enemy.Entity, out long lease)) return;
            var entity = notice.Enemy.Entity;
            rpg.skills.TraceCast("enemy_detected", $"敌人={entity.name}#{entity.GetInstanceID()} " +
                $"租约={lease} 目标={(notice.Target ? notice.Target.name : "未指定")} 来源={notice.Enemy.DetectionReason}");
        }

        void TraceEnemyDamage(EnemyVitalsNotice hit)
        {
            // Regeneration and spawn resets also publish vitals. Only settled, identified hits belong here.
            if (string.IsNullOrEmpty(hit.HitId) || !TracesEnemy(hit.Enemy, out long lease)) return;
            string weapon = hit.HitId.StartsWith("projectile:", StringComparison.Ordinal) ? "投射物" : "其他攻击";
            string attacker = hit.Attacker ? hit.Attacker.name : "未指定";
            string zone = hit.HitZone ? hit.HitZone.name : "本体";
            rpg.skills.TraceCast("enemy_damage", $"敌人={hit.Enemy.name}#{hit.Enemy.GetInstanceID()} 租约={lease} 来源={attacker}/{weapon} " +
                $"部位={zone} 弱点={hit.WeakPoint} HP={hit.Health + hit.HealthDamage:0.##}→{hit.Health:0.##} " +
                $"削韧={hit.PoiseDamage:0.##} 死亡={hit.Dead} hitId={hit.HitId}");
        }

        bool EnsureSetup()
        {
            if (!rpg || !area || !rpg.roomControlsEncounter)
            { status = "未接线：需要玩家、范围及玩家的房间管理开关"; return false; }
            if (UsesEnemyManager) return EnsureManagedSetup();
            if (!captured)
            {
                if (enemies == null || enemies.Length == 0) { status = "未配置房间敌人"; return false; }
                var seen = new HashSet<EnemyController>();
                foreach (var enemy in enemies)
                    if (!enemy || !seen.Add(enemy) || transform.IsChildOf(enemy.transform) || rpg.transform.IsChildOf(enemy.transform))
                    { status = "敌人引用缺失、重复或包含区域/玩家根物体"; return false; }
                foreach (var enemy in enemies)
                {
                    enemy.SetSceneRespawnOwner(true);
                    spawns.Add(new Spawn { Enemy = enemy, Position = enemy.transform.position,
                        Rotation = enemy.transform.rotation, Summoned = enemy.isSpawnedEnemy });
                }
                captured = true;
                ResetEncounter();
            }
            if (subscribed != rpg)
            {
                if (subscribed) subscribed.CheckpointRestored -= ResetEncounter;
                subscribed = rpg; subscribed.CheckpointRestored += ResetEncounter;
            }
            return true;
        }

        /// <summary>显式检查入口，使用玩家地面点而非手、射线或投射物。重复调用不重置敌人。</summary>
        public void TickEncounter()
        {
            if (!isActiveAndEnabled || ticking || resetting || !EnsureSetup()) return;
            ticking = true;
            try
            {
                if (rpg.Player == null || !rpg.locomotion || !rpg.locomotion.head) return;
                if (UsesEnemyManager) { TickManagedEncounter(); return; }
                if (!rpg.CanAct)
                {
                    inside = false; PauseEnemies(); rpg.SetEncounterCombat(false);
                    status = rpg.Player.Alive ? "玩家暂停，敌人已暂停" : "玩家倒下，等待恢复检查点";
                    return;
                }
                if (complete) { rpg.SetEncounterCombat(false); status = "房间已完成，探索中"; return; }
                if (started && AllDefeated())
                {
                    FinishEncounter(); return;
                }
                bool nextInside = ContainsFloor(rpg.locomotion.PlayerFloorPosition, inside);
                if (!nextInside)
                {
                    inside = false; PauseEnemies(); rpg.SetEncounterCombat(false);
                    status = started ? "已离开房间，敌人保留生命并暂停" : "练习区：等待玩家进入房间";
                    return;
                }
                inside = true;
                int token = revision;
                if (!started)
                {
                    started = true; pausedEnemies = false;
                    foreach (var spawn in spawns)
                    {
                        if (!spawn.Enemy) continue;
                        spawn.Enemy.gameObject.SetActive(true); // OnEnable 会重置，必须先激活再显式初始化。
                        if (revision != token || !isActiveAndEnabled) return;
                        spawn.Enemy.InitializeSpawn(rpg.locomotion.head, spawn.Summoned);
                        if (revision != token || !isActiveAndEnabled) return;
                    }
                }
                if (pausedEnemies)
                {
                    pausedEnemies = false;
                    foreach (var spawn in spawns)
                    {
                        if (spawn.Enemy && !Defeated(spawn.Enemy)) spawn.Enemy.SetSuspended(false);
                        if (revision != token || !isActiveAndEnabled) return;
                    }
                }
                rpg.SetEncounterCombat(true); status = "房间战斗中";
            }
            finally { ticking = false; }
        }

        void FinishEncounter()
        {
            complete = true; inside = false; pausedEnemies = true;
            rpg.SetEncounterCombat(false);
            status = "房间敌人已全部击败";
            Cleared?.Invoke();
            if (complete) CompletionChanged?.Invoke();
        }

        bool ContainsFloor(Vector3 world, bool wasInside)
        {
            if (!float.IsFinite(world.x) || !float.IsFinite(world.y) || !float.IsFinite(world.z)) return false;
            Vector3 local = area.transform.InverseTransformPoint(world) - area.center;
            Vector3 half = area.size * .5f;
            if (wasInside)
            {
                float margin = float.IsFinite(exitHysteresis) ? Mathf.Max(0, exitHysteresis) : 0;
                Vector3 scale = area.transform.lossyScale;
                half += new Vector3(margin / Mathf.Max(.0001f, Mathf.Abs(scale.x)),
                    margin / Mathf.Max(.0001f, Mathf.Abs(scale.y)), margin / Mathf.Max(.0001f, Mathf.Abs(scale.z)));
            }
            return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y && Mathf.Abs(local.z) <= half.z;
        }

        static bool Defeated(EnemyController enemy) => enemy.IsDead || enemy.Entity && enemy.Entity.Combat != null && enemy.Entity.Combat.Dead;
        bool AllDefeated()
        {
            foreach (var spawn in spawns) if (!spawn.Enemy || !Defeated(spawn.Enemy)) return false;
            return spawns.Count > 0;
        }
        void PauseEnemies()
        {
            if (UsesEnemyManager) { PauseManagedEnemies(); return; }
            if (pausedEnemies) return;
            pausedEnemies = true;
            int token = revision;
            foreach (var spawn in spawns)
            {
                if (spawn.Enemy && spawn.Enemy.isActiveAndEnabled && !Defeated(spawn.Enemy)) spawn.Enemy.SetSuspended(true);
                if (revision != token) return;
            }
        }

        /// <summary>回到首次捕获的出生位置，清除遭遇完成标记；敌人关闭，下一次进入才生成新运行态。</summary>
        public void ResetEncounter()
        {
            bool previouslyCleared = complete;
            if (UsesEnemyManager) ResetManagedEncounter();
            else ResetSceneEncounter();
            if (previouslyCleared && !complete) CompletionChanged?.Invoke();
        }

        void ResetSceneEncounter()
        {
            if (resetting) return;
            if (!captured) { EnsureSetup(); return; }
            resetting = true; ++revision;
            try
            {
                started = complete = inside = false; pausedEnemies = true;
                foreach (var spawn in spawns)
                {
                    if (!spawn.Enemy) continue;
                    spawn.Enemy.gameObject.SetActive(false);
                    spawn.Enemy.transform.SetPositionAndRotation(spawn.Position, spawn.Rotation);
                }
                if (rpg) rpg.SetEncounterCombat(false);
                status = "遭遇已重置，等待进入房间";
            }
            finally { resetting = false; }
        }
    }
}
