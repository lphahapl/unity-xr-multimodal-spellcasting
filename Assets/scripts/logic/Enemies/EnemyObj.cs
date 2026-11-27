using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using NarutoVR.SealCombat.Events;

namespace NarutoVR.SealCombat.Enemies
{
    internal interface IEnemyHitResponse
    {
        bool CanReceiveHit { get; }
        void RespondToHit(EnemyVitalsNotice hit, bool newlyBroken, int revision, float staggerSeconds);
    }

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
        [SerializeField, InspectorName("敌人身份（直接摆放时使用）")] EnemyDefinition definition;
        public EnemyDefinition Definition { get => definition; set => definition = value; }
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
        internal IEnemyHitResponse HitResponse { get; set; }
        internal bool IsCurrentHit(int revision) => isActiveAndEnabled && combatMutation == revision;
        public EnemyCombatDefinition CombatDefinition { get => combatDefinition; set => combatDefinition = value; }
        public string ActiveAttackId => strike?.Id;
        public AttackData[] Attacks => attacks;
        public EnemyRangedAttack RangedAttack { get; private set; }
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
        [Serializable]
        public sealed class AnimationMapping
        {
            public string idleState = "Base Layer.CanJiOu_Idle";
            public string firstAttackState = "Base Layer.Attack.挥砍";
            public string attackTag = "EnemyAttack";
            public string attackTriggerPrefix = "attack";
            public string foundParameter = "isFindPlayer", attackingParameter = "isAttacking";
            public string angleParameter = "angle", motionParameter = "motionState";
            public string hitTrigger = "Hit", seatedTrigger = "isSeated", deadParameter = "dead";
            public string hitState = "Base Layer.Hit", seatedState = "Base Layer.Seated", seatedRecoverState = "", deadState = "Base Layer.Dead";
        }

        [SerializeField, InspectorName("动画图映射（完整状态路径）")] AnimationMapping animationMapping = new();
        internal int DeathState => string.IsNullOrEmpty(animationMapping?.deadState) ? 0 : Animator.StringToHash(animationMapping.deadState);

        /// <summary>只初始化执行资源；不重置 Controller 的生成记录或业务事件。</summary>
        public void Initialize()
        {
            path ??= new NavMeshPath(); // 原生资源只能在主线程调用期创建。
            if (initialized) return;
            agent = GetComponent<NavMeshAgent>();
            RangedAttack = GetComponent<EnemyRangedAttack>();
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
            if (attacks[index].shape == AttackShape.Projectile && (!RangedAttack || !RangedAttack.IsConfigured))
            { reason = "远程招式缺少炮口或火核配置"; return false; }
            strike ??= new EnemyAttackExecution(this);
            strike.Begin(index, attacks[index]); return true;
        }
        public bool ValidateAttacks(int count, out string reason)
        {
            if (attacks == null || attacks.Length < count) { reason = "AttackData 数量少于独立攻击动作总数"; return false; }
            for (int i = 0; i < count; i++)
            {
                if (attacks[i] == null) { reason = "AttackData 为空：" + i; return false; }
                if (attacks[i].shape == AttackShape.Projectile && (!RangedAttack || !RangedAttack.IsConfigured))
                { reason = "远程攻击需要已配置炮臂、炮口与火核的 EnemyRangedAttack"; return false; }
                if (!attacks[i].Validate(out reason)) { reason = "攻击 " + i + "：" + reason; return false; }
            }
            reason = "配置有效"; return true;
        }
        public bool EvaluateStrike(float progress, bool occlusion, float height, LayerMask mask) =>
            strike != null && strike.Evaluate(progress, occlusion, height, mask);
        public void EndStrike() => strike?.End();
        internal void LockStrikeDirection() => strike?.LockDirection();

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
        public bool ReceiveHit(string hitId, float damage, float poiseDamage, EnemyHitZone zone = null, Transform attacker = null, float staggerSeconds = 0)
        {
            var response = HitResponse;
            if (!isActiveAndEnabled || response == null || !response.CanReceiveHit || Combat == null || Combat.Dead ||
                !float.IsFinite(damage) || damage < 0 || damage > 10000 || !float.IsFinite(poiseDamage) || poiseDamage < 0 || poiseDamage > 100000 ||
                !float.IsFinite(staggerSeconds) || staggerSeconds < 0) return false;
            bool weak = zone && zone.isActiveAndEnabled && zone.isWeakPoint && zone.Owner == this;
            var source = CombatDamageReceiver.FromHitTransform(attacker);
            float baseDamage = source ? damage * source.OutgoingDamageMultiplier : damage;
            float hpDamage = Mathf.Min(10000, baseDamage * (weak ? zone.DamageMultiplier : 1) + (weak ? zone.BonusDamage : 0));
            float poiseLoss = Mathf.Min(100000, poiseDamage * (weak ? zone.PoiseMultiplier : 1));
            var state = Combat;
            bool wasBroken = state.Broken;
            float hpBefore = state.Health, poiseBefore = state.Poise;
            if (!state.Apply(hitId, hpDamage, poiseLoss)) return false;
            int mutation = ++combatMutation;
            SyncCombatFields();
            var notice = new EnemyVitalsNotice(this, hitId, state.Health, state.Poise, hpBefore - state.Health, poiseBefore - state.Poise, weak, state.Broken, state.Dead, zone, attacker);
            // 先提交状态机，防止通知回调中继续攻击已死亡/瘫痪的对象。
            bool CurrentLife() => this && IsCurrentHit(mutation) && ReferenceEquals(response, HitResponse);
            response.RespondToHit(notice, notice.Broken && !wasBroken, mutation, staggerSeconds);
            // 反应可能结束旧攻击；结束监听若重生/销毁，不得暂停或回收新一轮对象。
            if (!CurrentLife()) return true;
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
            if (animationMapping == null || string.IsNullOrWhiteSpace(animationMapping.attackTag) ||
                string.IsNullOrWhiteSpace(animationMapping.firstAttackState) || string.IsNullOrWhiteSpace(animationMapping.idleState) ||
                (attackCount > 1 && string.IsNullOrWhiteSpace(animationMapping.attackTriggerPrefix)))
            { reason = "动画图映射缺少待机、首招、标签或攻击触发器前缀"; return false; }
            var parameters = animator.parameters;
            bool Has(string n, AnimatorControllerParameterType type) => Array.Exists(parameters, p => p.name == n && p.type == type);
            if (!Has(animationMapping.foundParameter, AnimatorControllerParameterType.Bool) || !Has(animationMapping.attackingParameter, AnimatorControllerParameterType.Bool) ||
                !Has(animationMapping.angleParameter, AnimatorControllerParameterType.Float) || !Has(animationMapping.motionParameter, AnimatorControllerParameterType.Int))
            { reason = "Animator 缺少基础参数或参数类型不符"; return false; }
            if (!animator.HasState(0, Animator.StringToHash(animationMapping.idleState)))
            { reason = "待机状态路径无效：" + animationMapping.idleState; return false; }
            if (attackCount < 1 || !animator.HasState(0, UnityEngine.Animator.StringToHash(animationMapping.firstAttackState)))
            { reason = "攻击总数或默认攻击状态无效"; return false; }
            for (int i = 1; i < attackCount; i++)
                if (!Has(animationMapping.attackTriggerPrefix + i, AnimatorControllerParameterType.Trigger))
                { reason = "缺少攻击 Trigger：" + animationMapping.attackTriggerPrefix + i; return false; }
            bool hasRecover = string.IsNullOrWhiteSpace(animationMapping.seatedRecoverState) ||
                animator.HasState(0, UnityEngine.Animator.StringToHash(animationMapping.seatedRecoverState));
            SupportsReactions = Has(animationMapping.deadParameter, AnimatorControllerParameterType.Bool) && Has(animationMapping.hitTrigger, AnimatorControllerParameterType.Trigger) &&
                Has(animationMapping.seatedTrigger, AnimatorControllerParameterType.Trigger) &&
                !string.IsNullOrWhiteSpace(animationMapping.hitState) && !string.IsNullOrWhiteSpace(animationMapping.seatedState) &&
                !string.IsNullOrWhiteSpace(animationMapping.deadState) && animator.HasState(0, UnityEngine.Animator.StringToHash(animationMapping.hitState)) &&
                animator.HasState(0, UnityEngine.Animator.StringToHash(animationMapping.seatedState)) &&
                animator.HasState(0, UnityEngine.Animator.StringToHash(animationMapping.deadState)) && hasRecover;
            reason = "配置有效"; return true;
        }

        public void ResetAnimation()
        {
            ClearAttackTriggers(); animator.SetBool(animationMapping.attackingParameter, false); animator.SetBool(animationMapping.foundParameter, false);
            animator.SetInteger(animationMapping.motionParameter, 0); animator.SetFloat(animationMapping.angleParameter, 0);
            if (SupportsReactions) { animator.SetBool(animationMapping.deadParameter, false); animator.ResetTrigger(animationMapping.hitTrigger); animator.ResetTrigger(animationMapping.seatedTrigger); }
            if (animator.isActiveAndEnabled) animator.Play(animationMapping.idleState, 0, 0);
        }

        public void SetTargetAnimation(bool found, float angle)
        { animator.SetBool(animationMapping.foundParameter, found); animator.SetFloat(animationMapping.angleParameter, angle); }

        public void SetMotionAnimation(int motion, float angle)
        {
            if (!animator) return;
            animator.SetInteger(animationMapping.motionParameter, motion); animator.SetFloat(animationMapping.angleParameter, angle);
        }

        public bool CanStartAttackAnimation => !animator.IsInTransition(0) && !animator.GetCurrentAnimatorStateInfo(0).IsTag(animationMapping.attackTag);

        public void StartAttackAnimation(int attack)
        {
            if (attack > 0) animator.SetTrigger(animationMapping.attackTriggerPrefix + attack);
            animator.SetBool(animationMapping.attackingParameter, true);
        }

        public void StopAttackAnimation()
        {
            if (!animator) return;
            animator.SetBool(animationMapping.attackingParameter, false); ClearAttackTriggers();
        }

        public void StartReactionAnimation(EnemyReaction reaction)
        {
            animator.ResetTrigger(animationMapping.hitTrigger); animator.ResetTrigger(animationMapping.seatedTrigger);
            if (reaction == EnemyReaction.Dead) animator.SetBool(animationMapping.deadParameter, true);
            else animator.SetTrigger(reaction == EnemyReaction.Hit ? animationMapping.hitTrigger : animationMapping.seatedTrigger);
        }

        public void ReadReactionAnimation(EnemyReaction reaction, out bool active, out bool inTransition)
        {
            var state = animator.GetCurrentAnimatorStateInfo(0);
            active = state.IsName(reaction == EnemyReaction.Hit ? animationMapping.hitState : reaction == EnemyReaction.Seated ? animationMapping.seatedState : animationMapping.deadState) ||
                (reaction == EnemyReaction.Seated && !string.IsNullOrWhiteSpace(animationMapping.seatedRecoverState) && state.IsName(animationMapping.seatedRecoverState));
            inTransition = animator.IsInTransition(0);
        }

        public void RecoverReactionAnimation()
        {
            animator.ResetTrigger(animationMapping.hitTrigger); animator.ResetTrigger(animationMapping.seatedTrigger);
            animator.CrossFadeInFixedTime(animationMapping.idleState, .08f, 0);
        }

        /// <summary>返回动画观测，攻击完成、接触时间和超时仍由 Controller 决定。</summary>
        public void ReadAttackAnimation(out bool isAttack, out bool inTransition, out float progress)
        {
            var state = animator.GetCurrentAnimatorStateInfo(0);
            isAttack = state.IsTag(animationMapping.attackTag); progress = state.normalizedTime;
            inTransition = animator.IsInTransition(0);
        }

        public void ClearAttackTriggers()
        {
            if (!animator || string.IsNullOrEmpty(animationMapping?.attackTriggerPrefix)) return;
            foreach (var parameter in animator.parameters)
                if (parameter.type == AnimatorControllerParameterType.Trigger && parameter.name.StartsWith(animationMapping.attackTriggerPrefix, StringComparison.Ordinal))
                    animator.ResetTrigger(parameter.nameHash);
        }

        public bool HasLineOfSight(Transform target, bool checkOcclusion, float eyeHeight, LayerMask obstructionMask)
            => target && HasLineOfSightTo(target, target.position, checkOcclusion, eyeHeight, obstructionMask);

        public bool HasLineOfSightTo(Transform target, Vector3 position, bool checkOcclusion, float eyeHeight, LayerMask obstructionMask)
        {
            if (!target) return false;
            if (!checkOcclusion) return true;
            return HasClearSightFrom(transform.position + Vector3.up * eyeHeight, target, position, obstructionMask);
        }

        internal bool HasClearSightFrom(Vector3 start, Transform target, Vector3 position, LayerMask obstructionMask)
        {
            Vector3 delta = position - start;
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
            {
                agent.ResetPath();
                agent.velocity = Vector3.zero;
                agent.isStopped = true;
            }
            if (resetRepath) RequestRepath();
        }

        [SerializeField, InspectorName("本体受击音效")] AudioClip hitClip;
        [SerializeField, Range(0,1), InspectorName("受击音量")] float hitVolume = .9f;
        AudioSource hitSource;
        IDisposable hitListener;
        Coroutine hitFade;
        SkinnedMeshRenderer[] hitMeshes;
        MaterialPropertyBlock hitProperties;
        static readonly int Emission = Shader.PropertyToID("_EmissionColor");

        void OnEnable() => hitListener = EventCenter.Subscribe(EnemyEvents.VitalsChanged, PresentHit, this);

        void PresentHit(EnemyVitalsNotice hit)
        {
            if (hit.Enemy != this || string.IsNullOrEmpty(hit.HitId) ||
                (hit.HealthDamage <= 0 && hit.PoiseDamage <= 0)) return;
            if (hitClip && hitVolume > 0)
            {
                if (!hitSource)
                {
                    hitSource = gameObject.AddComponent<AudioSource>();
                    hitSource.playOnAwake = false;
                    hitSource.spatialBlend = 1;
                    hitSource.minDistance = 1.5f;
                    hitSource.dopplerLevel = 0;
                }
                hitSource.PlayOneShot(hitClip, hitVolume);
            }
            if (hitFade != null) StopCoroutine(hitFade);
            hitMeshes ??= GetComponentsInChildren<SkinnedMeshRenderer>(true);
            hitProperties ??= new MaterialPropertyBlock();
            hitFade = StartCoroutine(FadeHit());
        }

        IEnumerator FadeHit()
        {
            const float duration = .28f;
            for (float age = 0; age < duration; age += Time.deltaTime)
            {
                SetHitGlow(1 - Mathf.SmoothStep(0, 1, age / duration));
                yield return null;
            }
            SetHitGlow(0);
            hitFade = null;
        }

        void SetHitGlow(float strength)
        {
            foreach (var mesh in hitMeshes)
            {
                if (!mesh || !mesh.sharedMaterial || !mesh.sharedMaterial.HasProperty(Emission)) continue;
                mesh.GetPropertyBlock(hitProperties);
                var rest = mesh.sharedMaterial.GetColor(Emission);
                hitProperties.SetColor(Emission, rest + new Color(.95f, .38f, .075f, 0) * strength);
                mesh.SetPropertyBlock(hitProperties);
            }
        }

        void OnDisable()
        {
            if (HitResponse is EnemyController controller) controller.ResetDodgeTime();
            hitListener?.Dispose();
            hitListener = null;
            if (hitSource) hitSource.Stop();
            if (hitFade != null)
            {
                StopCoroutine(hitFade);
                SetHitGlow(0);
                hitFade = null;
            }
            EndStrike();
        }

        void OnDrawGizmosSelected()
        {
            if (!drawAttackRange) return;
            var data = Application.isPlaying && strike != null && strike.Active ? strike.Data :
                attacks != null && attacks.Length > 0 ? attacks[Mathf.Clamp(previewAttackIndex, 0, attacks.Length - 1)] : null;
            if (data == null || !data.Validate(out _) || data.shape == AttackShape.Projectile) return;
            Gizmos.color = Application.isPlaying && strike != null && strike.Active ? new Color(1, .25f, .1f) : Color.yellow;
            Quaternion rotation = Application.isPlaying && strike != null && strike.Active
                ? strike.Facing : AttackData.HorizontalRotation(transform);
            Vector3 center = transform.position + rotation * data.centerOffset;
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
