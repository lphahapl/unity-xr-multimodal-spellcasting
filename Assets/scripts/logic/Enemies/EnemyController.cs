using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;
using UnityEngine.Serialization;
using NarutoVR.SealCombat.Events;

namespace NarutoVR.SealCombat.Enemies
{
    /// <summary>只负责敌人行为决策、计时、生成生命周期及业务事件，实体操作交给 EnemyObj。</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(EnemyObj))]
    public sealed partial class EnemyController : MonoBehaviour, IEnemyHitResponse
    {
        public enum Behaviour
        {
            [InspectorName("待机")] Idle, [InspectorName("巡逻")] Patrol,
            [InspectorName("追击")] Chase, [InspectorName("转向")] Turn,
            [InspectorName("攻击")] Attack, [InspectorName("暂停")] Suspended,
            [InspectorName("受击")] Hit, [InspectorName("坐地失衡")] Seated, [InspectorName("死亡")] Dead
        }
        [Serializable] public sealed class AttackEvent : UnityEvent<int> { }

        [Header("一、引用与生成类型")]
        [SerializeField, InspectorName("敌人实体（与控制器在同一物体）")] EnemyObj enemyObj;
        // 接收旧 prefab/场景字段；迁移后清空，事件和调参字段继续保留原序列化路径。
        [SerializeField, HideInInspector, FormerlySerializedAs("animator")] Animator legacyAnimator;
        /// <summary>兼容已有调用方；Inspector 的 Animator 引用改由 EnemyObj 持有。读取不创建组件。</summary>
        public Animator animator
        {
            get => legacyAnimator ? legacyAnimator : enemyObj ? enemyObj.Animator : null;
            set { if (enemyObj) { enemyObj.Animator = value; legacyAnimator = null; } else legacyAnimator = value; }
        }
        public EnemyObj Entity => enemyObj;
        [InspectorName("玩家目标（普通敌人为待发现目标）")] public Transform target;
        [InspectorName("召唤敌人（不巡逻，直接追目标）")] public bool isSpawnedEnemy;
        [InspectorName("自动更新（外部调度 Tick 时关闭）")] public bool tickAutomatically = true;
        [Header("二、场景巡逻点")]
        [InspectorName("巡逻点（按顺序循环）")] public Transform[] patrolPoints = Array.Empty<Transform>();
        [InspectorName("巡逻速度"), Min(.01f)] public float patrolSpeed = .9f;
        [InspectorName("抵达距离"), Min(.05f)] public float patrolArrivalDistance = .3f;
        [InspectorName("到点停留秒数"), Min(0)] public float patrolWaitSeconds = 1.5f;
        [Header("三、发现与追击")]
        [InspectorName("发现距离"), Min(.1f)] public float detectionDistance = 8;
        [InspectorName("视野角度（总角）"), Range(1, 360)] public float fieldOfView = 120;
        [InspectorName("遮挡检测")] public bool checkLineOfSight = true;
        [InspectorName("视线遮挡层（不含自身与玩家层）")] public LayerMask obstructionMask = Physics.DefaultRaycastLayers;
        [InspectorName("视线起点高度"), Min(0)] public float eyeHeight = 1.4f;
        [InspectorName("丢失目标距离"), Min(.1f)] public float loseDistance = 12;
        [InspectorName("遮挡后保留追击秒数"), Min(0)] public float loseSightSeconds = 2;
        [InspectorName("受攻击后强制锁定秒数"), Min(0)] public float provokedLockSeconds = 5;
        [InspectorName("追击速度"), Min(.01f)] public float chaseSpeed = 2;
        [InspectorName("转向角速度"), Min(1)] public float turnSpeed = 120;
        [InspectorName("重算路径间隔"), Min(.05f)] public float repathSeconds = .25f;
        [InspectorName("目标投影到导航面的最大距离"), Min(.1f)] public float navSampleRadius = 2;
        [Header("四、独立攻击动作（0 默认，1 对应 attack1）")]
        [InspectorName("独立攻击动作总数"), Min(1)] public int attackCount = 2;
        [InspectorName("攻击距离"), Min(.1f)] public float attackDistance = 1.6f;
        [InspectorName("允许攻击的朝向误差"), Range(1, 90)] public float attackFacingAngle = 15;
        [InspectorName("攻击结束后冷却秒数"), Min(0)] public float attackCooldown = 1.5f;
        [HideInInspector] public float contactNormalizedTime = .55f; // 旧资产迁移入口，当前时间在 EnemyObj 逐招配置。
        [InspectorName("进入攻击动画超时秒数"), Min(.1f)] public float animationEntryTimeout = 1;
        [InspectorName("攻击动画最大等待秒数"), Min(1)] public float attackTimeout = 8;
        [Header("状态打断")]
        [InspectorName("受击/坐地动画最大等待秒数"), Min(1)] public float reactionTimeout = 8;
        [Header("五、事件（首次发现每轮生成只触发一次）")]
        [InspectorName("首次发现玩家")] public UnityEvent onFirstPlayerDetected = new();
        [InspectorName("开始攻击（动作编号）")] public AttackEvent onAttackStarted = new();
        [InspectorName("进入生效窗口（通知，请勿重复绑定扣血）")] public AttackEvent onAttackContact = new();
        [InspectorName("攻击结束（含取消，动作编号）")] public AttackEvent onAttackFinished = new();
        [Header("六、运行状态（只读观察，请勿手改）")]
        [SerializeField, InspectorName("曾首次发现玩家")] bool hasEverDetectedPlayer;
        [SerializeField, InspectorName("当前已发现有效目标")] bool hasTarget;
        [SerializeField, InspectorName("当前行为")] Behaviour currentBehaviour;
        [SerializeField, InspectorName("水平目标夹角（左负右正）")] float angle;
        [SerializeField, InspectorName("当前攻击编号")] int currentAttack = -1;
        [SerializeField, InspectorName("状态说明")] string status = "等待初始化";
        [SerializeField, InspectorName("最近发现来源")] string detectionReason = "尚未发现";

        public bool HasEverDetectedPlayer => hasEverDetectedPlayer;
        public bool HasTarget => hasTarget;
        public Behaviour CurrentBehaviour => currentBehaviour;
        public int CurrentAttack => currentAttack;
        public float Angle => angle;
        public string Status => status;
        public string DetectionReason => detectionReason;
        public float ProvokedSecondsRemaining => Mathf.Max(0, provokedUntil - clock);
        public bool IsDead => reaction == EnemyReaction.Dead;
        public bool CanReceiveHit => isActiveAndEnabled && !suspended && !IsDead && Time.timeScale > 0;
        public NavMeshAgent Agent => enemyObj ? enemyObj.Agent : null;

        float clock, waitUntil, cooldownUntil, attackStartedAt, lastSeenAt;
        readonly List<CombatDamageReceiver> visibleCandidates = new();
        int patrolIndex, generation;
        bool suspended, waitingAtPatrol, attackEntered, contactSent, animationValid;
        bool inTick;
        Transform assignedTarget;
        EnemyReaction reaction;
        float reactionElapsed;
        bool reactionEntered;
        Animator pausedReactionAnimator;
        float reactionResumeSpeed;
        Transform pendingAttacker;
        float provokedUntil = -1;

        void Awake() => EnsureInitialized();
        void OnEnable() { EnsureInitialized(); enemyObj.HitResponse = this; ResetForSpawn(); }
        void Update() { if (tickAutomatically) Tick(Time.deltaTime); }
        void OnDisable()
        {
            OnDeathPlaybackDisabled();
            SetReactionPaused(false);
            if (enemyObj && ReferenceEquals(enemyObj.HitResponse, this)) enemyObj.HitResponse = null;
            StopNavigation();
            CancelAttack();
        }

        void IEnemyHitResponse.RespondToHit(EnemyVitalsNotice hit, bool newlyBroken, int revision)
        {
            if (!hit.Dead && hit.Attacker) NotifyAttackedBy(hit.Attacker);
            bool accepted = true;
            if (hit.Dead) accepted = TryDie();
            else if (newlyBroken) accepted = TryEnterSeated() || CurrentBehaviour == Behaviour.Seated;
            if (!accepted && this && isActiveAndEnabled && enemyObj && enemyObj.IsCurrentHit(revision))
                SetSuspended(true);
        }

        void EnsureInitialized()
        {
            if (!enemyObj || enemyObj.gameObject != gameObject)
            {
                enemyObj = GetComponent<EnemyObj>();
                if (!enemyObj) enemyObj = gameObject.AddComponent<EnemyObj>(); // 旧场景的缺失依赖兼容。
            }
            if (legacyAnimator) { enemyObj.Animator = legacyAnimator; legacyAnimator = null; }
            enemyObj.Initialize();
            enemyObj.EnsureAttackData(contactNormalizedTime);
        }

        /// <summary>显式接线/迁移入口，编辑器工具可调用；普通属性读取不会改写场景。</summary>
        public void BindEntityComponents() => EnsureInitialized();

        /// <summary>生成器入口。先设置类型和目标，再重置本轮首次发现标志。</summary>
        public void InitializeSpawn(Transform player, bool summoned)
        { isSpawnedEnemy = summoned; target = player; ResetForSpawn(); }

        /// <summary>更换目标不重置首次发现事件。传 null 后召唤敌人待机。</summary>
        public void SetTarget(Transform player)
        {
            if (target == player && assignedTarget == player) return;
            ++generation; target = assignedTarget = player;
            provokedUntil = -1;
            hasTarget = false;
            StopNavigation(); CancelAttack();
        }

        /// <summary>暂停/演出/死亡均可显式调用。恢复不会重播已触发的首次发现事件。</summary>
        public void SetSuspended(bool value)
        {
            ++generation; suspended = value;
            SetReactionPaused(value);
            SetDeathPlaybackSuspended(value);
            if (value) { StopNavigation(); CancelAttack(); if (this && suspended) SetBehaviour(Behaviour.Suspended, "外部暂停"); }
        }

        public void ResetForSpawn()
        {
            if (retiredDeath || spawnLease?.Defeated == true) return;
            CancelDeathPlayback();
            SetReactionPaused(false);
            EnsureInitialized(); ++generation;
            // 池化重置不发出上一轮攻击结束业务事件。
            enemyObj.EndStrike();
            enemyObj.ResetCombat();
            pendingAttacker = null; provokedUntil = -1;
            detectionReason = "尚未发现";
            currentAttack = -1; hasTarget = hasEverDetectedPlayer = suspended = false;
            reaction = EnemyReaction.None; reactionEntered = false;
            clock = waitUntil = cooldownUntil = lastSeenAt = 0;
            patrolIndex = 0; waitingAtPatrol = attackEntered = contactSent = false;
            assignedTarget = target;
            animationValid = ValidateAnimator(out var reason);
            StopNavigation();
            if (animationValid) enemyObj.ResetAnimation();
            SetBehaviour(Behaviour.Idle, animationValid ? "等待目标或巡逻" : reason);
        }

        public bool ValidateAnimator(out string reason)
        {
            EnsureInitialized();
            return enemyObj.ValidateAnimator(attackCount, out reason) && enemyObj.ValidateAttacks(attackCount, out reason);
        }

        /// <summary>反应接口只控制动作与行动锁，不直接扣血。重复低优先级打断不会重播动画。</summary>
        public void NotifyHit() => TryNotifyHit();
        public void EnterSeated() => TryEnterSeated();
        public void Die() => TryDie();
        public bool TryNotifyHit() => TryReact(EnemyReaction.Hit);
        public bool TryEnterSeated() => TryReact(EnemyReaction.Seated);
        public bool TryDie() => TryReact(EnemyReaction.Dead);
        /// <summary>攻击者使用玩家代表点（XR 头部）；排队到安全 Tick 边界，避免伤害回调重入状态机。</summary>
        public void NotifyAttackedBy(Transform attacker)
        {
            if (!isActiveAndEnabled || IsDead || !attacker || !attacker.gameObject.activeInHierarchy ||
                attacker == transform || attacker.IsChildOf(transform)) return;
            pendingAttacker = attacker;
        }

        bool TryReact(EnemyReaction requested)
        {
            EnsureInitialized();
            bool playable = animationValid && enemyObj.AnimationActive && enemyObj.SupportsReactions;
            if (!isActiveAndEnabled || (!playable && requested != EnemyReaction.Dead) ||
                (int)reaction >= (int)requested) return false;
            int token = ++generation;
            if (requested == EnemyReaction.Dead) SetReactionPaused(false);
            reaction = requested; reactionElapsed = 0; reactionEntered = false;
            StopNavigation();
            // 先同时提交动作与行动锁；结束攻击回调若重生，会完整重置二者。
            if (playable) enemyObj.StartReactionAnimation(requested);
            if (requested == EnemyReaction.Dead) BeginDeathPlayback();
            else if (suspended) SetReactionPaused(true);
            CancelAttack();
            if (!this || !isActiveAndEnabled) return false;
            if (generation == token) SetReactionBehaviour();
            return reaction == requested;
        }

        void SetReactionBehaviour() => SetBehaviour(reaction == EnemyReaction.Dead ? Behaviour.Dead :
            reaction == EnemyReaction.Seated ? Behaviour.Seated : Behaviour.Hit,
            reaction == EnemyReaction.Dead ? "死亡，等待演出收尾" : reaction == EnemyReaction.Seated ? "坐地动画播放中" : "受击动画播放中");

        // A room can suspend this actor after a hit trigger, before the first Animator update.
        // Preserve the clip and its AI watchdog together until this actor can observe it again.
        void SetReactionPaused(bool pause)
        {
            if (!pause)
            {
                if (pausedReactionAnimator) pausedReactionAnimator.speed = reactionResumeSpeed;
                pausedReactionAnimator = null;
                return;
            }
            if (pausedReactionAnimator || (reaction != EnemyReaction.Hit && reaction != EnemyReaction.Seated) ||
                !enemyObj || !enemyObj.AnimationActive) return;
            pausedReactionAnimator = enemyObj.Animator;
            reactionResumeSpeed = pausedReactionAnimator.speed;
            pausedReactionAnimator.speed = 0;
        }

        bool TickReaction(float deltaTime)
        {
            if (reaction == EnemyReaction.None) return false;
            StopNavigation(); SetReactionBehaviour();
            if (reaction == EnemyReaction.Dead) return true;
            reactionElapsed += deltaTime;
            enemyObj.ReadReactionAnimation(reaction, out bool active, out bool inTransition);
            if (active) reactionEntered = true;
            if ((reactionEntered && !active && !inTransition) || reactionElapsed >= reactionTimeout)
            {
                if (!reactionEntered || active || inTransition) enemyObj.RecoverReactionAnimation();
                reaction = EnemyReaction.None;
                int token = generation;
                enemyObj.CompleteSeated();
                if (!this || !isActiveAndEnabled || generation != token) return true;
                SetBehaviour(Behaviour.Idle, "受击/坐地结束，恢复行动");
            }
            return true;
        }

        /// <summary>可供外部统一调度；使用时关闭自动更新。时间随游戏时间暂停，不用墙钟跳过攻击。</summary>
        public void Tick(float deltaTime)
        {
            if (inTick) return;
            inTick = true;
            try { TickCore(deltaTime); }
            finally { inTick = false; }
        }

        void TickCore(float deltaTime)
        {
            EnsureInitialized();
            if (!isActiveAndEnabled || !float.IsFinite(deltaTime) || deltaTime <= 0) return;
            clock += deltaTime;
            lastDelta = deltaTime;
            if (!suspended && !IsDead && pendingAttacker)
            {
                Transform attacker = pendingAttacker; pendingAttacker = null;
                if (CombatDamageReceiver.IsDetectable(attacker))
                {
                    if (!AssignSensedTarget(attacker)) return;
                    provokedUntil = clock + Mathf.Max(.01f, provokedLockSeconds);
                    if (!ConfirmDetection("受击锁定")) return;
                }
            }
            if (assignedTarget != target) SetTarget(target);
            if (!this || !isActiveAndEnabled) return;
            bool validTarget = CombatDamageReceiver.IsDetectable(target);
            bool nearby = false;
            if (!suspended && !IsDead && !isSpawnedEnemy && enemyObj.DetectionSensor)
            {
                var sensor = enemyObj.DetectionSensor;
                if (!hasTarget && clock > provokedUntil)
                {
                    var close = sensor.FindNearby(validTarget ? target : null);
                    if (close)
                    {
                        if (!AssignSensedTarget(close)) return;
                        validTarget = true;
                    }
                }
                nearby = validTarget && sensor.Contains(target);
            }
            if (!suspended && !IsDead && !isSpawnedEnemy && !hasTarget && !nearby &&
                (!validTarget || !IsVisible(target, false)))
            {
                var seen = FindVisiblePlayer();
                if (seen)
                {
                    if (!AssignSensedTarget(seen)) return;
                    validTarget = true;
                }
            }
            if (!this || !isActiveAndEnabled) return;
            int tickGeneration = generation;
            if (suspended) { StopNavigation(); SetBehaviour(Behaviour.Suspended, "外部暂停"); return; }
            if (!animationValid || !enemyObj.isActiveAndEnabled || !enemyObj.AnimationActive)
            { StopNavigation(); status = "动画配置无效或已禁用，暂停行动"; CancelAttack(); return; }
            enemyObj.TickCombat(deltaTime);
            if (!this || !isActiveAndEnabled || suspended || generation != tickGeneration) return;
            if (TickReaction(deltaTime)) return;

            if (!validTarget || !target || !target.gameObject.activeInHierarchy)
            {
                if (hasTarget) StopNavigation();
                hasTarget = false; provokedUntil = -1; CancelAttack();
            }
            else
            {
                Vector3 delta = target.position - transform.position;
                angle = HorizontalAngle(transform.forward, delta);
                bool visible = IsVisible(target, hasTarget);
                if (isSpawnedEnemy || visible || nearby || clock <= provokedUntil)
                {
                    if (hasTarget) lastSeenAt = clock;
                    else if (!ConfirmDetection(isSpawnedEnemy ? "召唤目标" : nearby ? "近身范围" : "巡逻视野")) return;
                }
                else if (hasTarget && (delta.magnitude > Mathf.Max(detectionDistance, loseDistance) || clock - lastSeenAt >= loseSightSeconds))
                { hasTarget = false; StopNavigation(); CancelAttack(); }
            }
            if (!this || !isActiveAndEnabled || suspended || generation != tickGeneration) return;
            enemyObj.SetTargetAnimation(hasTarget, angle);
            if (currentAttack >= 0) { TickAttack(); return; }
            if (!hasTarget) { TickPatrol(); return; }

            // VR 目标的头部用于视线与受击，寻路和跨层判断使用角色的地面位置。
            Vector3 navigationTarget = CombatDamageReceiver.GetNavigationPosition(target);
            Vector3 toTarget = navigationTarget - transform.position;
            var flat = Vector3.ProjectOnPlane(toTarget, Vector3.up);
            if (flat.magnitude <= attackDistance && Mathf.Abs(toTarget.y) <= navSampleRadius)
            {
                StopNavigation();
                enemyObj.RotateTowards(flat, turnSpeed, deltaTime);
                angle = HorizontalAngle(transform.forward, flat);
                if (Mathf.Abs(angle) > attackFacingAngle)
                    SetBehaviour(Behaviour.Turn, "近距离转向目标");
                else if (clock >= cooldownUntil && HasLineOfSight() && enemyObj.CanStartAttackAnimation) BeginAttack();
                else SetBehaviour(Behaviour.Idle, "攻击冷却或视线被遮挡");
            }
            else
            {
                if (MoveTo(navigationTarget, chaseSpeed, attackDistance * .85f))
                { enemyObj.RotateTowards(enemyObj.DesiredVelocity.sqrMagnitude > .001f ? enemyObj.DesiredVelocity : flat, turnSpeed, deltaTime); SetBehaviour(Behaviour.Chase, "追击玩家"); }
                else SetBehaviour(Behaviour.Idle, status);
            }
        }

        public static float HorizontalAngle(Vector3 forward, Vector3 direction)
        {
            forward.y = direction.y = 0;
            return forward.sqrMagnitude < .0001f || direction.sqrMagnitude < .0001f ? 0 : Vector3.SignedAngle(forward, direction, Vector3.up);
        }

        bool AssignSensedTarget(Transform candidate)
        {
            int expected = generation + (target == candidate && assignedTarget == candidate ? 0 : 1);
            SetTarget(candidate);
            return this && isActiveAndEnabled && !suspended && !IsDead && generation == expected && target == candidate;
        }

        internal void DetectProximity(Transform player)
        {
            if (!CanReceiveHit || !enemyObj || !enemyObj.isActiveAndEnabled || isSpawnedEnemy ||
                !player || !player.gameObject.activeInHierarchy || player.IsChildOf(transform)) return;
            if (hasTarget)
            {
                if (target == player) lastSeenAt = clock;
                return;
            }
            if (AssignSensedTarget(player)) ConfirmDetection("近身Trigger接触");
        }

        bool ConfirmDetection(string reason)
        {
            detectionReason = reason;
            if (!hasTarget) { waitingAtPatrol = false; StopNavigation(); }
            hasTarget = true;
            lastSeenAt = clock;
            if (hasEverDetectedPlayer) return true;
            hasEverDetectedPlayer = true;
            int token = generation;
            Transform seen = target;
            Notify(EnemyEvents.FirstPlayerDetected, new EnemyDetectionNotice(this, seen), onFirstPlayerDetected.Invoke);
            if (!this || !isActiveAndEnabled || suspended || generation != token || target != seen) return false;
            if (target && target.gameObject.activeInHierarchy) return true;
            hasTarget = false;
            StopNavigation();
            return false;
        }

        bool IsVisible(Transform candidate, bool tracking)
        {
            if (!candidate || !candidate.gameObject.activeInHierarchy) return false;
            Vector3 delta = candidate.position - transform.position;
            float radius = tracking ? Mathf.Max(detectionDistance, loseDistance) : detectionDistance;
            return delta.sqrMagnitude <= radius * radius &&
                (tracking || Mathf.Abs(HorizontalAngle(transform.forward, delta)) <= fieldOfView * .5f) &&
                enemyObj.HasLineOfSight(candidate, checkLineOfSight, eyeHeight, obstructionMask);
        }

        Transform FindVisiblePlayer()
        {
            CombatDamageReceiver.CopyActive(visibleCandidates);
            Transform best = null;
            float closest = float.PositiveInfinity;
            foreach (var receiver in visibleCandidates)
            {
                var candidate = receiver.HitTransform;
                if (!receiver.CanReceiveDamage || !candidate || candidate == target || candidate.IsChildOf(transform)) continue;
                float distance = (candidate.position - transform.position).sqrMagnitude;
                if (distance >= closest || !IsVisible(candidate, false)) continue;
                closest = distance;
                best = candidate;
            }
            return best;
        }

        bool HasLineOfSight() => enemyObj.HasLineOfSight(target, checkLineOfSight, eyeHeight, obstructionMask);

        void TickPatrol()
        {
            if (isSpawnedEnemy) { StopNavigation(); SetBehaviour(Behaviour.Idle, "召唤敌人无目标，待机"); return; }
            if (patrolPoints == null || patrolPoints.Length == 0)
            { StopNavigation(); SetBehaviour(Behaviour.Idle, "未配置巡逻点，待机"); return; }
            patrolIndex %= patrolPoints.Length;
            int searched = 0;
            while (!patrolPoints[patrolIndex] && searched++ < patrolPoints.Length) patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
            if (!patrolPoints[patrolIndex]) { StopNavigation(); SetBehaviour(Behaviour.Idle, "巡逻点均为空"); return; }
            if (waitingAtPatrol)
            {
                if (clock < waitUntil) { SetBehaviour(Behaviour.Idle, "巡逻到点停留"); return; }
                waitingAtPatrol = false; patrolIndex = (patrolIndex + 1) % patrolPoints.Length; enemyObj.RequestRepath();
                return; // 下一次更新跳过空点后再寻路。
            }
            if (!MoveTo(patrolPoints[patrolIndex].position, patrolSpeed, patrolArrivalDistance))
            { SetBehaviour(Behaviour.Idle, status); return; }
            if (enemyObj.HasArrived(patrolArrivalDistance + .05f))
            { StopNavigation(); waitingAtPatrol = true; waitUntil = clock + patrolWaitSeconds; SetBehaviour(Behaviour.Idle, "巡逻到点停留"); return; }
            enemyObj.RotateTowards(enemyObj.DesiredVelocity, turnSpeed, lastDelta);
            SetBehaviour(Behaviour.Patrol, "巡逻点 " + (patrolIndex + 1));
        }

        float lastDelta;

        bool MoveTo(Vector3 destination, float speed, float stopping)
        {
            bool moving = enemyObj.TryMoveTo(destination, speed, stopping, clock, repathSeconds, navSampleRadius, out var failure);
            if (failure != null) status = failure;
            return moving;
        }

        void StopNavigation() { if (enemyObj) enemyObj.StopNavigation(); }

        void BeginAttack()
        {
            // 整数上界不包含，N 个独立动作严格选择 0..N-1。
            enemyObj.ClearAttackTriggers(); currentAttack = UnityEngine.Random.Range(0, Mathf.Max(1, attackCount));
            if (!enemyObj.BeginStrike(currentAttack, out var failure))
            { currentAttack = -1; cooldownUntil = clock + Mathf.Max(.1f, attackCooldown); SetBehaviour(Behaviour.Idle, failure); return; }
            attackStartedAt = clock; attackEntered = contactSent = false;
            enemyObj.StartAttackAnimation(currentAttack);
            SetBehaviour(Behaviour.Attack, "攻击动作 " + currentAttack);
            int started = currentAttack;
            Notify(EnemyEvents.AttackStarted, new EnemyAttackNotice(this, started, enemyObj.ActiveAttackId), () => onAttackStarted.Invoke(started));
        }

        void TickAttack()
        {
            StopNavigation();
            enemyObj.ReadAttackAnimation(out bool isAttack, out bool inTransition, out float progress);
            if (isAttack && !inTransition)
            {
                attackEntered = true;
                int token = generation, attack = currentAttack;
                string id = enemyObj.ActiveAttackId;
                bool opened = enemyObj.EvaluateStrike(progress, checkLineOfSight, eyeHeight, obstructionMask);
                // 扣血或回调可导致死亡、换目标、重生，不能继续提交旧攻击通知。
                if (!this || generation != token || currentAttack != attack || suspended || !isActiveAndEnabled) return;
                if (opened && !contactSent)
                {
                    contactSent = true;
                    Notify(EnemyEvents.AttackWindowOpened, new EnemyAttackNotice(this, attack, id), () => onAttackContact.Invoke(attack));
                    if (!this || generation != token || currentAttack != attack || suspended || !isActiveAndEnabled) return;
                }
                if (progress >= .98f) { CancelAttack(); return; }
            }
            else if (attackEntered && !inTransition) { CancelAttack(); return; }
            if ((!attackEntered && clock - attackStartedAt >= animationEntryTimeout) || clock - attackStartedAt >= attackTimeout)
            { CancelAttack(); if (this && currentAttack < 0) status = "攻击动画未进入或超时，已复位"; }
        }

        public void CancelAttack()
        {
            if (currentAttack < 0) return;
            string id = enemyObj ? enemyObj.ActiveAttackId : null;
            if (enemyObj) enemyObj.EndStrike();
            int finished = currentAttack; currentAttack = -1; cooldownUntil = clock + Mathf.Max(0, attackCooldown);
            if (animationValid && enemyObj) enemyObj.StopAttackAnimation();
            SetBehaviour(Behaviour.Idle, "攻击结束或取消，等待冷却");
            Notify(EnemyEvents.AttackFinished, new EnemyAttackNotice(this, finished, id), () => onAttackFinished.Invoke(finished));
        }


        // 保留 Inspector 绑定，并桥接外部订阅；伤害不交给通知监听者执行。
        static void Notify<T>(EventKey<T> key, T payload, Action configured)
        {
            try { configured?.Invoke(); }
            catch (Exception error) { Debug.LogWarning("敌人配置事件回调异常：" + error); }
            finally { EventCenter.Publish(key, payload); }
        }

        void SetBehaviour(Behaviour behaviour, string reason)
        {
            currentBehaviour = behaviour; status = reason;
            if (!animationValid || !enemyObj) return;
            enemyObj.SetMotionAnimation(behaviour == Behaviour.Patrol ? 1 : behaviour == Behaviour.Chase ? 2 : behaviour == Behaviour.Turn ? 3 : 0, angle);
        }

        void OnValidate()
        {
            attackCount = Mathf.Max(1, attackCount); loseDistance = Mathf.Max(detectionDistance, loseDistance);
            attackTimeout = Mathf.Max(animationEntryTimeout + .1f, attackTimeout);
        }
    }
}
