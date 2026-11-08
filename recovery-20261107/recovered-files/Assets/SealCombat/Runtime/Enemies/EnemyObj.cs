using System;
using UnityEngine;
using UnityEngine.AI;
using NarutoVR.SealCombat.Events;

namespace NarutoVR.SealCombat.Enemies
{
    public enum EnemyReaction
    {
        [InspectorName("无")] None, [InspectorName("受击")] Hit,
        [InspectorName("坐地失衡")] Seated, [InspectorName("死亡")] Dead
    }
    /// <summary>
    /// 敌人在场景中的执行对象。封装组件、导航、视线、攻击判定与动画细节，不决定行为切换。
    /// 无自主 Update，所有操作由 EnemyController 调度，避免出现两个行为状态来源。
    /// </summary>
    [DisallowMultipleComponent, RequireComponent(typeof(NavMeshAgent))]
    public sealed class EnemyObj : MonoBehaviour
    {
        [Header("一、实体组件")]
        [SerializeField, InspectorName("动画控制器")] Animator animator;
        [SerializeField, InspectorName("近距探测器（身体碰撞之外的 Trigger）")] EnemyDetectionSensor detectionSensor;
        public EnemyDetectionSensor DetectionSensor { get => detectionSensor; set => detectionSensor = value; }
        [Header("二、攻击配置（数组下标对应动画编号）")]
        [SerializeField, InspectorName("每招攻击数据")] AttackData[] attacks = Array.Empty<AttackData>();
        [SerializeField, HideInInspector] bool attackDataInitialized;
        [Header("三、Scene 判定预览（选中敌人并开启 Gizmos）")]
        [SerializeField, InspectorName("预览攻击编号"), Min(0)] int previewAttackIndex;
        [SerializeField, InspectorName("显示判定范围")] bool drawAttackRange = true;
        EnemyAttackExecution strike;
        [Header("四、生命与削韧（重生时读取配置）")]
        [SerializeField, InspectorName("战斗数值资产（空则使用基础默认值）")] EnemyCombatDefinition combatDefinition;
        [SerializeField, InspectorName("当前生命（运行时只读）")] float currentHealth;
        [SerializeField, InspectorName("当前韧性（运行时只读）")] float currentPoise;
        [SerializeField, InspectorName("削韧状态（运行时只读）")] string combatStatus;
        public EnemyCombatState Combat { get; private set; }
        int combatMutation;
        public EnemyCombatDefinition CombatDefinition { get => combatDefinition; set => combatDefinition = value; }
        public string ActiveAttackId => strike?.Id;
        public AttackData[] Attacks => attacks;
        public Animator Animator { get => animator; set => animator = value; }
        public NavMeshAgent Agent => agent;
        public bool AnimationActive => animator && animator.isActiveAndEnabled;
        public bool SupportsReactions { get; private set; }
        public Vector3 DesiredVelocity => agent.desiredVelocity;

        NavMeshAgent agent;
        NavMeshPath path;
        bool initialized, hasNavigationDestination;
        Vector3 navigationDestination;
        float nextPathAt;
        readonly RaycastHit[] sightHits = new RaycastHit[32];
        static readonly int Found = UnityEngine.Animator.StringToHash("isFindPlayer"),
            Attacking = UnityEngine.Animator.StringToHash("isAttacking"),
            AngleParameter = UnityEngine.Animator.StringToHash("angle"),
            Motion = UnityEngine.Animator.StringToHash("motionState");
        static readonly int Dead = UnityEngine.Animator.StringToHash("dead"), Hit = UnityEngine.Animator.StringToHash("Hit"),
            Seated = UnityEngine.Animator.StringToHash("isSeated");

        /// <summary>只初始化执行资源；不重置 Controller 的生成记录或业务事件。</summary>
        public void Initialize()
        {
            path ??= new NavMeshPath(); // 原生资源只能在主线程调用期创建。
            if (initialized) return;
            agent = GetComponent<NavMeshAgent>();
            if (!animator) animator = GetComponentInChildren<Animator>();
            if (!detectionSensor) detectionSensor = GetComponentInChildren<EnemyDetectionSensor>();
            agent.updateRotation = false;
            if (animator) animator.applyRootMotion = false;
            initialized = true;
        }

        /// <summary>旧工程仅首次迁移默认两招；已配置的空数组不会被偷偷填回。</summary>
        public void EnsureAttackData(float legacyContact)
        {
            strike ??= new EnemyAttackExecution(this);
            if (attackDataInitialized) return;
            if (attacks == null || attacks.Length == 0)
            {
                float start = float.IsFinite(legacyContact) ? Mathf.Clamp(legacyContact, 0, .95f) : .55f;
                attacks = new[] {
                    new AttackData { activeStart = start, activeEnd = Mathf.Min(.98f, start + .15f) },
                    new AttackData { displayName = "转圈", shape = AttackShape.Circle, damage = 25, radius = 2.3f,
                        activeStart = start, activeEnd = Mathf.Min(.98f, start + .25f) }
                };
            }
            attackDataInitialized = true;
        }

        public bool BeginStrike(int index, out string reason)
        {
            EndStrike();
            if (attacks == null || index < 0 || index >= attacks.Length || attacks[index] == null)
            { reason = "EnemyObj 缺少攻击编号 " + index + " 的 AttackData"; return false; }
            if (!attacks[index].Validate(out reason)) return false;
            strike ??= new EnemyAttackExecution(this);
            strike.Begin(index, attacks[index]); return true;
        }
        public bool ValidateAttacks(int count, out string reason)
        {
            if (attacks == null || attacks.Length < count) { reason = "AttackData 数量少于独立攻击动作总数"; return false; }
            for (int i = 0; i < count; i++)
            {
                if (attacks[i] == null) { reason = "AttackData 为空：" + i; return false; }
                if (!attacks[i].Validate(out reason)) { reason = "攻击 " + i + "：" + reason; return false; }
            }
            reason = "配置有效"; return true;
        }
        public bool EvaluateStrike(float progress, bool occlusion, float height, LayerMask mask) =>
            strike != null && strike.Evaluate(progress, occlusion, height, mask);
        public void EndStrike() => strike?.End();

        public void ResetCombat()
        {
            Combat = new EnemyCombatState(combatDefinition);
            ++combatMutation;
            SyncCombatFields();
        }
        public void TickCombat(float deltaTime)
        {
            if (Combat == null) return;
            float before = Combat.Poise;
            Combat.Tick(deltaTime); SyncCombatFields();
            if (Combat.Poise != before) { ++combatMutation; PublishVitals(null, 0, 0, false); }
        }
        public void CompleteSeated()
        {
            if (Combat == null || !Combat.Broken || Combat.Dead) return;
            Combat.CompleteSeated(); ++combatMutation; SyncCombatFields(); PublishVitals(null, 0, 0, false);
        }
        /// <summary>技能/武器统一受击接口。传本次唯一 ID；弱点区必须属于当前敌人。</summary>
        public bool ReceiveHit(string hitId, float damage, float poiseDamage, EnemyHitZone zone = null, Transform attacker = null)
        {
            var controller = GetComponent<EnemyController>();
            if (!isActiveAndEnabled || !controller || !controller.CanReceiveHit || Combat == null || Combat.Dead ||
                !float.IsFinite(damage) || damage < 0 || damage > 10000 || !float.IsFinite(poiseDamage) || poiseDamage < 0 || poiseDamage > 100000) return false;
            bool weak = zone && zone.isActiveAndEnabled && zone.isWeakPoint && zone.Owner == this;
            float hpDamage = Mathf.Min(10000, damage * (weak ? zone.DamageMultiplier : 1) + (weak ? zone.BonusDamage : 0));
            float poiseLoss = Mathf.Min(100000, poiseDamage * (weak ? zone.PoiseMultiplier : 1));
            var state = Combat;
            bool wasBroken = state.Broken;
            float hpBefore = state.Health, poiseBefore = state.Poise;
            if (!state.Apply(hitId, hpDamage, poiseLoss)) return false;
            if (!state.Dead && attacker) controller.NotifyAttackedBy(attacker);
            int mutation = ++combatMutation;
            SyncCombatFields();
            var notice = new EnemyVitalsNotice(this, hitId, state.Health, state.Poise, hpBefore - state.Health, poiseBefore - state.Poise, weak, state.Broken, state.Dead);
            // 先提交状态机，防止通知回调中继续攻击已死亡/瘫痪的对象。
            bool CurrentLife() => this && isActiveAndEnabled && controller && controller.isActiveAndEnabled &&
                ReferenceEquals(Combat, state) && combatMutation == mutation;
            bool reactionAccepted = true;
            if (notice.Dead) reactionAccepted = controller.TryDie();
            else if (notice.Broken && !wasBroken) reactionAccepted = controller.TryEnterSeated() || controller.CurrentBehaviour == EnemyController.Behaviour.Seated;
            // 反应可能结束旧攻击；结束监听若重生/销毁，不得暂停或回收新一轮对象。
            if (!CurrentLife()) return true;
            if (!reactionAccepted) controller.SetSuspended(true);
            EventCenter.Publish(EnemyEvents.VitalsChanged, notice);
            if (!CurrentLife()) return true;
            if (notice.Dead) EventCenter.Publish(EnemyEvents.Died, notice);
            else if (notice.Broken && !wasBroken && !state.Dead && state.Broken) EventCenter.Publish(EnemyEvents.PoiseBroken, notice);
            return true;
        }
        void SyncCombatFields()
        {
            currentHealth = Combat.Health; currentPoise = Combat.Poise;
            combatStatus = Combat.Dead ? "死亡" : Combat.Broken ? "韧性耗尽，瘫痪中" : "可行动";
        }
        void PublishVitals(string id, float healthDamage, float poiseDamage, bool weak) => EventCenter.Publish(EnemyEvents.VitalsChanged,
            new EnemyVitalsNotice(this, id, Combat.Health, Combat.Poise, healthDamage, poiseDamage, weak, Combat.Broken, Combat.Dead));

        public bool ValidateAnimator(int attackCount, out string reason)
        {
            SupportsReactions = false;
            if (!animator || !animator.runtimeAnimatorController) { reason = "未绑定 Animator Controller"; return false; }
            var parameters = animator.parameters;
            bool Has(string n, AnimatorControllerParameterType type) => Array.Exists(parameters, p => p.name == n && p.type == type);
            if (!Has("isFindPlayer", AnimatorControllerParameterType.Bool) || !Has("isAttacking", AnimatorControllerParameterType.Bool) ||
                !Has("angle", AnimatorControllerParameterType.Float) || !Has("motionState", AnimatorControllerParameterType.Int))
            { reason = "Animator 缺少基础参数或参数类型不符"; return false; }
            if (attackCount < 1 || !animator.HasState(0, UnityEngine.Animator.StringToHash("Base Layer.Attack.挥砍")))
            { reason = "攻击总数或默认攻击状态无效"; return false; }
            for (int i = 1; i < attackCount; i++)
                if (!Has("attack" + i, AnimatorControllerParameterType.Trigger))
                { reason = "缺少攻击 Trigger：attack" + i; return false; }
            SupportsReactions = Has("dead", AnimatorControllerParameterType.Bool) && Has("Hit", AnimatorControllerParameterType.Trigger) &&
                Has("isSeated", AnimatorControllerParameterType.Trigger) &&
                animator.HasState(0, UnityEngine.Animator.StringToHash("Base Layer.Hit")) &&
                animator.HasState(0, UnityEngine.Animator.StringToHash("Base Layer.Seated")) &&
                animator.HasState(0, UnityEngine.Animator.StringToHash("Base Layer.Dead"));
            reason = "配置有效"; return true;
        }

        public void ResetAnimation()
        {
            ClearAttackTriggers(); animator.SetBool(Attacking, false); animator.SetBool(Found, false);
            animator.SetInteger(Motion, 0); animator.SetFloat(AngleParameter, 0);
            if (SupportsReactions) { animator.SetBool(Dead, false); animator.ResetTrigger(Hit); animator.ResetTrigger(Seated); }
            if (animator.isActiveAndEnabled) animator.Play("Base Layer.CanJiOu_Idle", 0, 0);
        }

        public void SetTargetAnimation(bool found, float angle)
        { animator.SetBool(Found, found); animator.SetFloat(AngleParameter, angle); }

        public void SetMotionAnimation(int motion, float angle)
        {
            if (!animator) return;
            animator.SetInteger(Motion, motion); animator.SetFloat(AngleParameter, angle);
        }

        public bool CanStartAttackAnimation => !animator.IsInTransition(0) && !animator.GetCurrentAnimatorStateInfo(0).IsTag("EnemyAttack");

        public void StartAttackAnimation(int attack)
        {
            if (attack > 0) animator.SetTrigger("attack" + attack);
            animator.SetBool(Attacking, true);
        }

        public void StopAttackAnimation()
        {
            if (!animator) return;
            animator.SetBool(Attacking, false); ClearAttackTriggers();
        }

        public void StartReactionAnimation(EnemyReaction reaction)
        {
            animator.ResetTrigger(Hit); animator.ResetTrigger(Seated);
            if (reaction == EnemyReaction.Dead) animator.SetBool(Dead, true);
            else animator.SetTrigger(reaction == EnemyReaction.Hit ? Hit : Seated);
        }

        public void ReadReactionAnimation(EnemyReaction reaction, out bool active, out bool inTransition)
        {
            active = animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer." + reaction);
            inTransition = animator.IsInTransition(0);
        }

        public void RecoverReactionAnimation()
        {
            animator.ResetTrigger(Hit); animator.ResetTrigger(Seated);
            animator.CrossFadeInFixedTime("Base Layer.CanJiOu_Idle", .08f, 0);
        }

        /// <summary>返回动画观测，攻击完成、接触时间和超时仍由 Controller 决定。</summary>
        public void ReadAttackAnimation(out bool isAttack, out bool inTransition, out float progress)
        {
            var state = animator.GetCurrentAnimatorStateInfo(0);
            isAttack = state.IsTag("EnemyAttack"); progress = state.normalizedTime;
            inTransition = animator.IsInTransition(0);
        }

        public void ClearAttackTriggers()
        {
            if (!animator) return;
            foreach (var parameter in animator.parameters)
                if (parameter.type == AnimatorControllerParameterType.Trigger && parameter.name.StartsWith("attack", StringComparison.Ordinal))
                    animator.ResetTrigger(parameter.nameHash);
        }

        public bool HasLineOfSight(Transform target, bool checkOcclusion, float eyeHeight, LayerMask obstructionMask)
            => target && HasLineOfSightTo(target, target.position, checkOcclusion, eyeHeight, obstructionMask);

        public bool HasLineOfSightTo(Transform target, Vector3 position, bool checkOcclusion, float eyeHeight, LayerMask obstructionMask)
        {
            if (!target) return false;
            if (!checkOcclusion) return true;
            Vector3 start = transform.position + Vector3.up * eyeHeight, delta = position - start;
            if (delta.sqrMagnitude < .000001f) return true;
            int count = Physics.RaycastNonAlloc(start, delta.normalized, sightHits, delta.magnitude, obstructionMask, QueryTriggerInteraction.Ignore);
            if (count == sightHits.Length) return false;
            for (int i = 0; i < count; i++)
            {
                Transform hit = sightHits[i].transform;
                if (hit == transform || hit.IsChildOf(transform) || hit == target || hit.IsChildOf(target) || target.IsChildOf(hit)) continue;
                return false;
            }
            return true;
        }

        public void RotateTowards(Vector3 direction, float degreesPerSecond, float deltaTime)
        {
            direction.y = 0;
            if (direction.sqrMagnitude > .0001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), degreesPerSecond * deltaTime);
        }

        public bool HasArrived(float tolerance) => !agent.pathPending && hasNavigationDestination &&
            (Vector3.Distance(transform.position, navigationDestination) <= tolerance || (agent.hasPath && agent.remainingDistance <= tolerance));

        /// <summary>完整路径才移动；重算节流和路径缓存由实体自己维护，失败原因供 Controller 展示。</summary>
        public bool TryMoveTo(Vector3 destination, float speed, float stopping, float now, float repathSeconds,
            float sampleRadius, out string failure)
        {
            failure = null;
            if (!agent || !agent.isActiveAndEnabled || !agent.isOnNavMesh)
            { failure = "未处于有效 NavMesh，停止移动"; return false; }
            agent.speed = speed; agent.stoppingDistance = stopping;
            if (now < nextPathAt) return hasNavigationDestination &&
                ((agent.hasPath && agent.pathStatus == NavMeshPathStatus.PathComplete) || Vector3.Distance(transform.position, navigationDestination) <= stopping + .05f);
            nextPathAt = now + Mathf.Max(.05f, repathSeconds);
            if (!NavMesh.SamplePosition(destination, out var hit, sampleRadius, agent.areaMask) ||
                !agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete)
            { StopNavigation(false); failure = "目标无完整导航路径，等待重试"; return false; }
            agent.isStopped = false;
            if (agent.SetPath(path)) { navigationDestination = hit.position; hasNavigationDestination = true; return true; }
            StopNavigation(false); failure = "导航路径提交失败"; return false;
        }

        public void RequestRepath() => nextPathAt = 0;

        public void StopNavigation(bool resetRepath = true)
        {
            hasNavigationDestination = false;
            if (agent && agent.isActiveAndEnabled && agent.isOnNavMesh)
            { agent.isStopped = true; agent.ResetPath(); agent.velocity = Vector3.zero; }
            if (resetRepath) RequestRepath();
        }

        void OnDisable() => EndStrike();

        void OnDrawGizmosSelected()
        {
            if (!drawAttackRange) return;
            var data = Application.isPlaying && strike != null && strike.Active ? strike.Data :
                attacks != null && attacks.Length > 0 ? attacks[Mathf.Clamp(previewAttackIndex, 0, attacks.Length - 1)] : null;
            if (data == null || !data.Validate(out _)) return;
            Gizmos.color = Application.isPlaying && strike != null && strike.Active ? new Color(1, .25f, .1f) : Color.yellow;
            Vector3 center = data.Center(transform);
            Quaternion rotation = AttackData.HorizontalRotation(transform);
            float sweep = data.shape == AttackShape.Circle ? 360 : data.angle;
            int segments = Mathf.Max(8, Mathf.CeilToInt(sweep / 6));
            for (int level = -1; level <= 1; level++)
            {
                Vector3 origin = center + Vector3.up * (data.halfHeight * level);
                Vector3 previous = origin + rotation * Quaternion.Euler(0, -sweep * .5f, 0) * Vector3.forward * data.radius;
                if (sweep < 360) Gizmos.DrawLine(origin, previous);
                for (int i = 1; i <= segments; i++)
                {
                    Vector3 next = origin + rotation * Quaternion.Euler(0, -sweep * .5f + sweep * i / segments, 0) * Vector3.forward * data.radius;
                    Gizmos.DrawLine(previous, next); previous = next;
                    if (level == 0 && i % 4 == 0) Gizmos.DrawLine(next - Vector3.up * data.halfHeight, next + Vector3.up * data.halfHeight);
                }
                if (sweep < 360) Gizmos.DrawLine(origin, previous);
            }
            Gizmos.DrawLine(center - Vector3.up * data.halfHeight, center + Vector3.up * data.halfHeight);
        }
    }
}
