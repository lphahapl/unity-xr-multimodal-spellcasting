using System;
using System.Collections.Generic;
using Oculus.Interaction.Input;
using NarutoVR.SealLab;
using UnityEngine;
using UnityEngine.Rendering;

namespace NarutoVR.SealCombat
{
    /// <summary>保存头向瞄准点与手部发射点之间的飞行解算结果，包括遮挡信息。</summary>
    public readonly struct FireballAim
    {
        public readonly Vector3 origin, direction, gazePoint, endpoint;
        public readonly bool blocked;
        public FireballAim(Vector3 origin, Vector3 direction, Vector3 gazePoint, Vector3 endpoint, bool blocked)
        { this.origin=origin; this.direction=direction; this.gazePoint=gazePoint; this.endpoint=endpoint; this.blocked=blocked; }
    }

    public enum ProjectileStage { Formed, Launched, Impact }

    /// <summary>持有护盾、手持物、飞行投射物和扩展效果的运行态，负责生成、使用、命中与清理。</summary>
    [DefaultExecutionOrder(350)]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "NarutoVR.SealCombat", "Assembly-CSharp", "SkillEffects")]
    public sealed partial class SkillController : MonoBehaviour
    {
        [Header("一、玩家与当前追踪来源")]
        [InspectorName("手印采样器")] public NarutoVR.SealLab.SealLab lab;
        [InspectorName("头显视点")] public Transform head;
        [InspectorName("玩家根节点（碰撞忽略）")] public Transform playerRoot;
        [Header("命中特效对象池（可选）")]
        [InspectorName("场景特效池"), Tooltip("留空时尝试使用同一物体的 VfxPoolManager。")] public VfxPoolManager vfxPool;
        [InspectorName("头向瞄准最远距离（米）"), Min(1)] public float aimDistance = 30;
        [InspectorName("显示瞄准时手腕最多低于头部（米）"), Range(.15f,.65f)] public float aimWristDrop = .4f;
        [InspectorName("可碰撞层")] public LayerMask hitLayers = ~(1 << 2);
        [InspectorName("玩家层（额外忽略）")] public LayerMask playerLayers;

        public string Status { get; private set; } = "无生效技能";
        public bool HasHeldObject => heldObject;
        public bool HasOccupiedHand => heldObject || rightHandEffect != null;
        public float HeldRemainingSeconds => heldObject ? Mathf.Max(0,(float)(heldExpiresAt-Now)) : 0;
        public bool HasRightHeldObject => rightHandEffect != null || (heldObject && !heldInLeftHand);
        public bool HasRightThrowObject => heldObject && !heldInLeftHand && heldParameters.ReleaseMode == ProjectileReleaseMode.GripThrow;
        public bool RightMotionOwnedBySpell => rightHandEffect != null || HasRightThrowObject;
        public Func<bool> RightHandActionPolicy { get; set; }
        public bool RightHandActionAllowed => isActiveAndEnabled && Time.timeScale > 0 &&
            !PushInputSuppressed && (RightHandActionPolicy?.Invoke() ?? true);
        public int WeaponHitMask => hitLayers.value & ~playerLayers.value;
        public Transform WeaponOwnerRoot => playerRoot ? playerRoot : head ? head.root : null;
        public Transform WeaponAttacker => head;
        public string ThrowStatus => heldObject && throwState != null ? throwBlockReason ?? (throwIntent != null ? throwIntent.Score.reason : throwState.Status) : string.Empty;
        public ThrowActionScore CurrentThrowScore => throwIntent != null ? throwIntent.Score : default;
        public string LastThrowOutcome { get; private set; } = "尚无发射或拒绝结果";
        public string LastReleaseMode { get; private set; } = "尚未发射";
        public static string ReleaseModeLabel(string mode) => mode=="GripRelease"?"松手投掷":mode=="WindupThrow"?"后拉前送":mode=="ReachHold"?"伸手保持":"尚无通过候选";
        public HandThrowSettings ActiveThrowSettings => heldParameters.Throwing?.Copy();
        public float ActiveProjectileSpeed => heldParameters.Speed;
        public Vector3 LastLaunchDirection { get; private set; }
        public Vector3 LastLaunchPosition { get; private set; }
        public HandThrowObservation LastThrowObservation { get; private set; }
        public bool HasThrowObservation { get; private set; }
        public bool ThrowInputValid => heldObject && throwState != null && throwInputValid && !ThrowInputSuppressed && !PushInputSuppressed &&
            HandNow >= LastThrowObservation.observed && HandNow - LastThrowObservation.observed <= heldParameters.Throwing.sampleMaxAge;
        public bool ThrowArmed => ThrowInputValid && (throwState.Armed || (throwIntent?.Armed ?? false));
        public bool ReleasePending => ThrowInputValid && throwState.ReleasePending && !(throwIntent?.ContradictsWindup(throwState.Estimate.velocity)??false);
        public float ThrowGripProgress => ThrowInputValid ? throwState.GripProgress : 0;
        public float ThrowReleaseProgress => ThrowInputValid ? throwState.ReleaseProgress : 0;
        public Vector3 HeldThrowPosition => heldObject ? heldObject.transform.position : Vector3.zero;
        public bool WindupPreview => ThrowInputValid && CurrentThrowScore.rawWindupDirection.sqrMagnitude>.5f;
        public bool ThrowPreviewValid => TryGetThrowPreview(out _);
        public Vector3 ThrowPreviewOrigin => releasePoseValid ? releasePose.position : HeldThrowPosition;
        public Vector3 ThrowPreviewDirection => TryGetThrowPreview(out var aim) ? aim.direction : Vector3.zero;
        public event Action<HandThrowObservation> ThrowObserved;
        public event Action ProjectileLaunched;
        public event Action<SkillTrainingTarget> ProjectileHitTarget;
        public event Action<ProjectileStage, Vector3> ProjectileStageChanged;
        public event Action<float> ShieldImpact;
        public bool ProjectileHandIsLeft => heldInLeftHand;
        public int HeldProjectileInstanceId => heldObject ? heldObject.GetInstanceID() : 0;
        public bool ThrowInputSuppressed { get; set; }
        public Func<bool, HandThrowSettings, ThrowGripSample> ThrowGripProvider { get; set; }
        public int ActiveProjectileCount => projectiles.Count;
        public int LaunchCount { get; private set; }
        public bool PushInputSuppressed { get; set; }
        public Func<double> GameClock { get; set; }
        /// <summary>观测时钟注入点，用于确定性回放；正常流程使用追踪快照的实时时间。</summary>
        public Func<double> HandInputClock { get; set; }
        // 可为其他施法者注入腕部位姿；正常场景默认读取现有追踪来源。
        public Func<bool, Pose?> WristPoseOverride { get; set; }
        public float BuffRemaining => shieldPoints > 0 ? Mathf.Max(0, (float)(buffExpiresAt - Now)) : 0;
        public float ShieldPoints => BuffRemaining > 0 ? shieldPoints : 0;
        public float ShieldCapacity => activeShieldCapacity > 0 ? activeShieldCapacity : shieldCapacity;
        public int ActivationCount { get; private set; }
        public bool IsCastingTransaction => activating;
        public bool IsPublishingCast { get; private set; }
        public int HitCount { get; private set; }
        public event Action<string> Message;

        internal double Now => GameClock?.Invoke() ?? (Application.isPlaying ? Time.timeAsDouble : Time.realtimeSinceStartupAsDouble);
        private double HandNow => HandInputClock?.Invoke() ?? Time.realtimeSinceStartupAsDouble;
        float shieldPoints => Vitals.ShieldPoints;
        float activeShieldCapacity => Vitals.ShieldCapacity;
        double buffExpiresAt => Vitals.ShieldExpiresAt;
        private double armAt, lostAt = -1;
        private GameObject buffObject;
        private GameObject activeShieldPrefab;
        private Material activeShieldMaterial;
        private ShieldWristPulse shieldPulse;
        private Material ownedFireMaterial, ownedBuffMaterial;
        private readonly List<SkillProjectile> projectiles = new();
        private readonly List<GameObject> transientVisuals = new();
        private readonly List<PushSample> pushSamples = new();
        private readonly List<ExtensionLifetime> extensions = new();
        private readonly List<ExtensionLifetime> pendingExtensions = new();
        private ISkillEffectLifetime rightHandEffect;
        private ISkillEffectLifetime pendingRightHand;
        private readonly List<ExtensionLifetime> tickingExtensions = new();
        private bool clearing, activating;
        private int releasingEffects;
        private double lastObserved = double.NegativeInfinity;
        private double previewWristStamp = double.NegativeInfinity;
        private Vector3 previewWrist;
        private Vector3 lastHandPosition, lastHeadPosition;
        private bool hasPreviousSample;
        private HandThrowState throwState;
        private HandThrowIntentState throwIntent;
        private string shotId;
        private string heldSource;
        private string ThrowSource => WristPoseOverride!=null?"测试注入":lab?lab.TrackingSource:"无采样器";
        private long observationSequence;
        private bool throwInputValid, releasePoseValid, hasSafeHeldPoint;
        private Pose releasePose;
        private Vector3 safeHeldPoint;
        private long releasePoseCandidate;
        private readonly Collider[] throwOverlaps = new Collider[32];
        private readonly RaycastHit[] throwSweeps = new RaycastHit[32];
        private string throwBlockReason;

        // 激活时冻结本发投射物的参数。之后编辑共享配置或激活其他技能，
        // 都不会改变当前手持物的运动、伤害及发射后的飞行参数。
        private readonly struct ProjectileParameters
        {
            public readonly float Radius, Damage, PoiseDamage, Speed, FlightLifetime, HeldLifetime, ArmDelay, PushDistance, PushMinimum, PushMaximum;
            public readonly Vector3 Offset;
            public readonly GameObject Visual, Impact;
            public readonly Material Material;
            public readonly ProjectileReleaseMode ReleaseMode;
            public readonly HandThrowSettings Throwing;
            public readonly SkillAudioProfile Audio;
            public ProjectileParameters(ProjectileEffectDefinition definition)
            {
                Audio = definition.audioProfile;
                Radius = definition.radius;
                Damage = definition.damage;
                PoiseDamage = definition.poiseDamage;
                Speed = definition.speed;
                FlightLifetime = definition.flightLifetime;
                HeldLifetime = definition.heldLifetime;
                ArmDelay = definition.armDelay;
                PushDistance = definition.pushDistance;
                PushMinimum = definition.pushMinimumSeconds;
                PushMaximum = definition.pushMaximumSeconds;
                ReleaseMode = definition.releaseMode;
                Throwing = definition.throwing?.Copy() ?? new HandThrowSettings();
                Offset = ReleaseMode == ProjectileReleaseMode.GripThrow ? definition.throwLocalOffset : definition.heldOffset;
                Visual = definition.visualPrefab;
                Impact = definition.impactPrefab;
                Material = definition.material;
            }
        }

        private sealed class ExtensionLifetime
        {
            public readonly ISkillEffectLifetime Value;
            public bool Cancelled;
            public ExtensionLifetime(ISkillEffectLifetime value) { Value = value; }
        }

        private readonly struct PushSample
        {
            public readonly Vector3 Hand, Head, Forward;
            public readonly double Time;
            public PushSample(Vector3 hand, Vector3 headPosition, Vector3 forward, double time)
            { Hand = hand; Head = headPosition; Forward = forward; Time = time; }
        }

        private void Awake()
        {
            if (!vfxPool) vfxPool = GetComponent<VfxPoolManager>();
            if (!lab) lab = GetComponent<NarutoVR.SealLab.SealLab>();
            if (!head && Camera.main) head = Camera.main.transform;
            if (!playerRoot && head) playerRoot = head.root;
            int playerLayer = LayerMask.NameToLayer("Player");
            if (playerLayer >= 0) playerLayers = playerLayers.value | (1 << playerLayer);
        }

        readonly Dictionary<Type, Func<SkillEffectDefinition, SkillDefinition, PreparedSkillCast>> executors = new();
        Transform stagingRoot;
        Rpg.RpgPlayerState castVitals;
        long trainingRequests;
        long clearingVersion;
        long lastFullClear;
        Rpg.PlayerModel trainingPlayer;
        HashSet<SkillDefinition> trainingRecipes;
        internal void BindDefinitions(IReadOnlyList<SkillDefinition> skills)
        {
            if (castVitals == null)
            {
                trainingPlayer ??= new Rpg.PlayerModel(new Rpg.RpgPlayerState());
                trainingRecipes = new HashSet<SkillDefinition>(skills);
            }
            BindLegacyDefinitions(skills);
        }
        internal Rpg.RpgPlayerState Vitals => castVitals ?? (trainingPlayer ??= new Rpg.PlayerModel(new Rpg.RpgPlayerState())).Vitals;
        internal void BindVitals(Rpg.RpgPlayerState player) => castVitals = player;
        public void RegisterExecutor<T>(Func<T, SkillDefinition, PreparedSkillCast> prepare) where T : SkillEffectDefinition
        {
            if (prepare == null) throw new ArgumentNullException(nameof(prepare));
            EnsureExecutors();
            AddExecutor(prepare);
        }

        void AddExecutor<T>(Func<T, SkillDefinition, PreparedSkillCast> prepare) where T : SkillEffectDefinition
        {
            executors.Add(typeof(T), (definition, skill) => prepare((T)definition, skill));
        }

        internal Transform StagingRoot
        {
            get
            {
                if (!stagingRoot)
                {
                    var container = new GameObject("未提交的施法");
                    container.SetActive(false);
                    container.transform.SetParent(transform, false);
                    stagingRoot = container.transform;
                }
                return stagingRoot;
            }
        }

        internal static void FreePrepared(GameObject instance)
        {
            if (!instance) return;
            instance.SetActive(false);
            if (Application.isPlaying) Destroy(instance); else DestroyImmediate(instance);
        }

        PreparedSkillCast Prepare(SkillDefinition skill, SkillEffectDefinition definition)
        {
            if (!definition) { Report("技能缺少效果配置：" + skill.skillId); return null; }
            if (definition.ValidateDefinition() is string error) { Report(error); return null; }
            if (executors.TryGetValue(definition.GetType(), out var executor)) return executor(definition, skill);
            Report("未注册的技能效果：" + definition.name);
            return null;
        }

        internal ISkillActivationPolicy GameplayPolicy { get; set; }
        public bool TryCast(SkillDefinition skill) => Cast(skill).Confirmed;

        public SkillCastResult Cast(SkillDefinition skill)
        {
            if (!skill) return SkillCastResult.Rejected(SkillCastFailure.InvalidRequest, "技能为空");
            if (!isActiveAndEnabled) return SkillCastResult.Rejected(SkillCastFailure.NotReady, "技能控制器未启用");
            if (GameplayPolicy != null) return GameplayPolicy.TryActivate(skill);
            if (trainingRecipes != null && !trainingRecipes.Contains(skill)) return SkillCastResult.Rejected(SkillCastFailure.NotPermitted, "训练目录未授予此技能");
            var result = CommitCast(skill, Vitals, "training:" + ++trainingRequests, 0, Now, null);
            if (!result.Confirmed && result.Detail != Status) Report(result.Detail);
            return result;
        }

        internal SkillCastResult CommitCast(SkillDefinition skill, Rpg.RpgPlayerState player, string requestId,
            float manaCost, double gameNow, Func<bool> stillAllowed)
        {
            if (activating || clearing || releasingEffects > 0) return SkillCastResult.Rejected(SkillCastFailure.Busy, "上一个施法事务尚未结束");
            if (!isActiveAndEnabled || Time.timeScale <= 0 || !skill) return SkillCastResult.Rejected(SkillCastFailure.NotReady, "当前无法施法");
            long version = clearingVersion;
            long fullClear = lastFullClear;
            bool confirmed = false;
            activating = true;
            LastActivationAudioHandled = false;
            activationAudioAnchor = null;
            try
            {
                EnsureExecutors();
                using var cast = Prepare(skill, ResolveDefinition(skill));
                if (cast == null) return SkillCastResult.Rejected(SkillCastFailure.PreparationRejected, Status);
                if (!CanFinish()) return SkillCastResult.Rejected(SkillCastFailure.Invalidated, "准备期间施法权限改变");
                if (!player.TryReserveCast(requestId, skill.skillId, skill.cooldownGroup,
                    manaCost, skill.cooldownSeconds, gameNow, out var reservation, out var failure, out var reason))
                    return SkillCastResult.Rejected(failure switch
                    {
                        Rpg.CastReservationFailure.Dead => SkillCastFailure.Dead,
                        Rpg.CastReservationFailure.Cooldown => SkillCastFailure.Cooldown,
                        Rpg.CastReservationFailure.InsufficientMana => SkillCastFailure.InsufficientMana,
                        Rpg.CastReservationFailure.Busy => SkillCastFailure.Busy,
                        _ => SkillCastFailure.InvalidRequest
                    }, reason);
                using (reservation)
                {
                    if (!cast.Arm() || !CanFinish()) return SkillCastResult.Rejected(SkillCastFailure.Invalidated, "施法准备已失效");
                    reservation.Commit();
                }
                confirmed = true;
                extensions.AddRange(pendingExtensions);
                pendingExtensions.Clear();
                if (pendingRightHand != null) { rightHandEffect = pendingRightHand; pendingRightHand = null; }
                IsPublishingCast = true;
                ++ActivationCount;
                cast.Publish();
                if (clearingVersion == version) CompleteActivationAudio(skill);
                return SkillCastResult.Accepted();
            }
            catch (Exception error)
            {
                string reason = (confirmed ? "施法已提交，呈现失败：" : "施法准备失败：") + error.Message;
                Report(reason);
                return confirmed ? SkillCastResult.Accepted(reason) : SkillCastResult.Rejected(SkillCastFailure.PreparationRejected, reason);
            }
            finally
            {
                if (!confirmed)
                {
                    while (pendingExtensions.Count > 0) RemoveExtension(pendingExtensions[pendingExtensions.Count - 1]);
                    if (lastFullClear != fullClear) ClearBuff();
                }
                activating = false;
                IsPublishingCast = false;
            }
            bool CanFinish() => isActiveAndEnabled && Time.timeScale > 0 && version == clearingVersion &&
                (stillAllowed == null || stillAllowed());
        }

        bool installedExecutors;
        void EnsureExecutors()
        {
            if (installedExecutors) return;
            AddExecutor<ProjectileEffectDefinition>(PrepareFireball);
            AddExecutor<ShieldEffectDefinition>(PrepareShield);
            AddExecutor<ThunderSwordEffectDefinition>(PrepareThunderSword);
            installedExecutors = true;
        }

        private void LateUpdate()
        {
            if (Time.timeScale <= 0)
            {
                if (heldObject || shieldPoints > 0 || shieldPulse?.IsPlaying == true || projectiles.Count > 0 || transientVisuals.Count > 0 || extensions.Count > 0) ClearAll();
                return;
            }
            double now = Now;
            TickExtensions(now);
            transientVisuals.RemoveAll(item => !item);
            if (shieldPoints > 0 ? now >= buffExpiresAt : buffObject)
            {
                ClearBuff();
                shieldPulse?.Stop();
                Report("护盾持续时间已结束");
            }
            UpdateBuffVisual();
            if (heldObject) UpdateHeld(now);
        }

        public bool TryGetRightWeaponWrist(out Pose worldPose, out double observed)
        {
            if (TryGetWrist(false, out worldPose, out observed) && double.IsFinite(observed) &&
                (WristPoseOverride != null || lab.Frame.right.highConfidence)) return true;
            worldPose = Pose.identity;
            observed = 0;
            return false;
        }

        private bool TryGetWrist(bool leftHand, out Pose worldPose, out double observed)
        {
            worldPose = Pose.identity;
            observed = 0;
            if (WristPoseOverride != null)
            {
                Pose? supplied = WristPoseOverride(leftHand);
                if (!supplied.HasValue || !NarutoVR.SealLab.SealLab.FinitePose(supplied.Value)) return false;
                worldPose = supplied.Value; observed = HandNow; return true;
            }
            if (!lab || !lab.isActiveAndEnabled || !lab.recordingOrigin || lab.Frame == null) return false;
            HandSnapshot hand = leftHand ? lab.Frame.left : lab.Frame.right;
            int wristIndex = (int)HandJointId.HandWristRoot;
            if (hand == null || !hand.connected || !hand.tracked || !hand.fresh ||
                hand.joints == null || hand.jointValid == null || wristIndex >= hand.joints.Length ||
                wristIndex >= hand.jointValid.Length || !hand.jointValid[wristIndex]) return false;
            double age = HandNow - hand.observedAt;
            if (age < -0.01 || age > Mathf.Max(0.04f, lab.staleAfterSeconds)) return false;
            Pose localPose = hand.joints[wristIndex];
            if (!NarutoVR.SealLab.SealLab.FinitePose(localPose)) return false;
            worldPose = new Pose(lab.recordingOrigin.TransformPoint(localPose.position),
                lab.recordingOrigin.rotation * localPose.rotation);
            observed = hand.observedAt;
            return NarutoVR.SealLab.SealLab.FinitePose(worldPose);
        }

        private void ResetMotion(double now)
        {
            throwInputValid = releasePoseValid = hasSafeHeldPoint = false; throwBlockReason = null;
            pushSamples.Clear();
            lastObserved = double.NegativeInfinity;
            hasPreviousSample = false;
            throwState?.Reset();
            throwIntent?.Reset();
            armAt = now + heldParameters.ArmDelay;
        }

        private void TrackPush(Vector3 handPosition, double observed, double now)
        {
            // 重复读取的骨骼快照不累计为新的手部运动。
            if (observed <= lastObserved) return;
            Vector3 headPosition = head.position;
            if (hasPreviousSample && ((handPosition - lastHandPosition).sqrMagnitude > 0.35f * 0.35f ||
                (headPosition - lastHeadPosition).sqrMagnitude > 0.35f * 0.35f)) ResetMotion(now);
            lastObserved = observed;
            lastHandPosition = handPosition;
            lastHeadPosition = headPosition;
            hasPreviousSample = true;
            if (now < armAt) return;

            float minSeconds = heldParameters.PushMinimum;
            float maxSeconds = heldParameters.PushMaximum;
            while (pushSamples.Count > 0 && observed - pushSamples[0].Time > maxSeconds)
                pushSamples.RemoveAt(0);
            foreach (PushSample start in pushSamples)
            {
                if (!IsForwardPush(start.Hand, handPosition, start.Head, headPosition, start.Forward,
                    (float)(observed - start.Time), heldParameters.PushDistance, minSeconds, maxSeconds)) continue;
                if (TryResolveAim(heldObject.transform.position, out var aim)) Launch(aim.direction);
                return;
            }
            pushSamples.Add(new PushSample(handPosition, headPosition, head.forward, observed));
        }

        /// <summary>同时要求手在世界空间和相对头部的空间向前移动，避免转头或身体位移误触发。</summary>
        public static bool IsForwardPush(Vector3 startWorld, Vector3 endWorld, Vector3 startHead,
            Vector3 endHead, Vector3 referenceForward, float seconds, float distance = 0.12f,
            float minimumSeconds = 0.15f, float maximumSeconds = 0.6f)
        {
            if (!float.IsFinite(seconds) || !float.IsFinite(distance) || seconds < minimumSeconds ||
                seconds > maximumSeconds || distance <= 0 || referenceForward.sqrMagnitude < 0.0001f) return false;
            Vector3 forward = referenceForward.normalized;
            Vector3 worldDelta = endWorld - startWorld;
            Vector3 relativeDelta = (endWorld - endHead) - (startWorld - startHead);
            return Vector3.Dot(worldDelta, forward) >= distance && Vector3.Dot(relativeDelta, forward) >= distance;
        }

        /// <summary>头向准星射线给出目标点，投射物从手部实际位置朝该点飞行。</summary>
        public Vector3 GetAimPoint()
        {
            if (!head) return transform.position + transform.forward * Mathf.Max(1, aimDistance);
            float range = Mathf.Max(1, aimDistance);
            Vector3 point = head.position + head.forward * range;
            float nearest = range;
            int mask=hitLayers.value & ~playerLayers.value;
            int count=Physics.RaycastNonAlloc(head.position,head.forward,throwSweeps,range,mask,QueryTriggerInteraction.Ignore);
            RaycastHit[] hits=throwSweeps;
            if(count==hits.Length) { hits=Physics.RaycastAll(head.position,head.forward,range,mask,QueryTriggerInteraction.Ignore); count=hits.Length; }
            for(int i=0;i<count;i++)
            {
                var hit=hits[i];
                if (IgnoreThrowCollider(hit.collider) || hit.distance >= nearest) continue;
                point = hit.point;
                nearest = hit.distance;
            }
            return point;
        }

        // 瞄准预览独立于握持和投掷评分，抬腕静止时也可预览，发射仍需通过动作检查。
        public bool TryGetThrowPreview(out FireballAim aim)
        {
            aim=default;
            if (!heldObject || !heldObject.activeSelf || !head || PushInputSuppressed || ThrowInputSuppressed ||
                Time.timeScale<=0 || !double.IsFinite(previewWristStamp)) return false;
            double age=HandNow-previewWristStamp;
            if(!double.IsFinite(age) || age<-.01 || age>heldParameters.Throwing.sampleMaxAge ||
                previewWrist.y<head.position.y-Mathf.Clamp(aimWristDrop,.15f,.65f)) return false;
            return TryResolveAim(ThrowPreviewOrigin,out aim);
        }

        public bool TryGetWeaponWaveDirection(Vector3 origin, out Vector3 direction)
        {
            direction = Vector3.zero;
            if (!RightHandActionAllowed || !head || !NarutoVR.SealLab.SealLab.FinitePose(new Pose(head.position, head.rotation))) return false;
            Vector3 delta = GetAimPoint() - origin;
            float distance = delta.magnitude;
            if (!float.IsFinite(distance) || distance < .001f || Vector3.Dot(delta, head.forward) <= 0f) return false;
            direction = delta / distance;
            return true;
        }

        private bool TryResolveAim(Vector3 origin,out FireballAim aim)
        {
            aim=default;
            if(!head || !NarutoVR.SealLab.SealLab.FinitePose(new Pose(head.position,head.rotation))) return false;
            Vector3 point=GetAimPoint(), delta=point-origin;
            float distance=delta.magnitude;
            // 头部与发射平面之间存在遮挡时，限制目标点，避免朝后发射或越过掩体。
            if(!float.IsFinite(distance) || distance<.001f || Vector3.Dot(delta,head.forward)<=0) return false;
            if(!ThrowPathClear(origin,origin)) return false;
            Vector3 direction=delta/distance, endpoint=point; bool blocked=false;
            int mask=hitLayers.value & ~playerLayers.value;
            // 实际飞行使用有半径的球形扫掠，因此准星射线旁的墙体也可能挡住投射物。
            int count=Physics.SphereCastNonAlloc(origin,heldParameters.Radius,direction,throwSweeps,distance,mask,QueryTriggerInteraction.Ignore);
            RaycastHit[] hits=throwSweeps;
            if(count==hits.Length) { hits=Physics.SphereCastAll(origin,heldParameters.Radius,direction,distance,mask,QueryTriggerInteraction.Ignore); count=hits.Length; }
            float nearest=distance;
            for(int i=0;i<count;i++)
            {
                var hit=hits[i];
                if(IgnoreThrowCollider(hit.collider) || hit.distance>nearest) continue;
                nearest=hit.distance; endpoint=origin+direction*nearest;
                blocked=!hit.collider.GetComponentInParent<SkillTrainingTarget>();
            }
            aim=new FireballAim(origin,direction,point,endpoint,blocked); return true;
        }

        private void OnTargetHit(SkillTrainingTarget target)
        {
            ++HitCount;
            if (ProjectileHitTarget != null)
                foreach (Action<SkillTrainingTarget> listener in ProjectileHitTarget.GetInvocationList())
                    try { listener(target); } catch (Exception e) { Debug.LogWarning("火球命中监听失败：" + e.Message, this); }
            Report($"命中训练靶：剩余生命 {target.CurrentHealth:0}，累计命中 {HitCount} 次");
        }

        private void OnProjectileFinished(SkillProjectile projectile) => projectiles.Remove(projectile);
        void PublishProjectileStage(ProjectileStage stage, Vector3 point)
        {
            if (ProjectileStageChanged == null) return;
            foreach (Action<ProjectileStage, Vector3> listener in ProjectileStageChanged.GetInvocationList())
                try { listener(stage, point); }
                catch (Exception e) { Debug.LogWarning("火球表现监听失败：" + e.Message, this); }
        }
        private void OnImpactCreated(GameObject visual) { if (visual) transientVisuals.Add(visual); }

        /// <summary>Acquire the hand and register the same lifetime for tick and cleanup in one operation.</summary>
        public bool TryRegisterRightHandEffect(ISkillEffectLifetime effect)
        {
            if (effect == null || pendingRightHand != null || HasOccupiedHand || !RightHandActionAllowed) return false;
            if (activating && !IsPublishingCast) pendingRightHand = effect; else rightHandEffect = effect;
            if (RegisterEffectLifetime(effect)) return true;
            ReleaseRightHandEffect(effect);
            return false;
        }

        /// <summary>An effect can free the hand while its released visuals run to completion.</summary>
        public void ReleaseRightHandEffect(ISkillEffectLifetime effect)
        {
            if (ReferenceEquals(rightHandEffect, effect)) rightHandEffect = null;
            if (ReferenceEquals(pendingRightHand, effect)) pendingRightHand = null;
        }

        /// <summary>
        /// 仅在返回 true 时接管生命周期；注册被拒绝时仍由调用方清理资源。
        /// 同一对象只可注册一次；清理期间拒绝新注册。
        /// </summary>
        public bool RegisterEffectLifetime(ISkillEffectLifetime effect)
        {
            if (effect == null || !isActiveAndEnabled || Time.timeScale <= 0 || clearing || releasingEffects > 0) return false;
            if (effect is UnityEngine.Object unityObject && !unityObject) return false;
            foreach (var entry in extensions) if (ReferenceEquals(entry.Value, effect)) return false;
            foreach (var entry in pendingExtensions) if (ReferenceEquals(entry.Value, effect)) return false;
            try { if (!effect.IsAlive) return false; }
            catch (Exception error) { Debug.LogWarning("扩展效果注册失败：" + error.Message, this); return false; }
            var registrations = activating && !IsPublishingCast ? pendingExtensions : extensions;
            registrations.Add(new ExtensionLifetime(effect));
            return true;
        }

        private void TickExtensions(double now)
        {
            // 自定义效果可在 Tick 内注册或取消其他效果，因此遍历时使用登记项快照，
            // 每次操作前重新确认登记项仍归本执行器管理。
            tickingExtensions.Clear(); tickingExtensions.AddRange(extensions);
            foreach (var entry in tickingExtensions)
            {
                if (entry.Cancelled || !extensions.Contains(entry)) continue;
                try
                {
                    if (entry.Value.IsAlive) entry.Value.Tick(now);
                    if (!entry.Cancelled && !entry.Value.IsAlive) RemoveExtension(entry);
                }
                catch (Exception error)
                {
                    Debug.LogWarning("扩展效果更新失败，已清理：" + error.Message, this);
                    RemoveExtension(entry);
                }
            }
            tickingExtensions.Clear();
        }

        private void RemoveExtension(ExtensionLifetime entry)
        {
            if (entry.Cancelled) return;
            extensions.Remove(entry); pendingExtensions.Remove(entry); entry.Cancelled = true;
            ReleaseRightHandEffect(entry.Value);
            releasingEffects++;
            try { entry.Value.Cancel(); }
            catch (Exception error) { Debug.LogWarning("扩展效果结束时发生异常：" + error.Message, this); }
            finally { releasingEffects--; }
        }

        /// <summary>训练靶场的测试脉冲只消耗盾量，返回可供教学显示的剩余伤害。</summary>
        public float AbsorbPracticeDamage(float damage)
        {
            if (!float.IsFinite(damage) || damage <= 0) return 0;
            if (BuffRemaining <= 0)
            {
                if (shieldPoints > 0) shieldPulse?.Stop();
                ClearBuff();
                return damage;
            }
            float absorbed = Vitals.AbsorbShield(damage, Now);
            PresentShieldHit(absorbed);
            return damage - absorbed;
        }

        public void PresentShieldHit(float absorbed)
        {
            if (absorbed <= 0) return;
            bool broken = shieldPoints <= 0;
            if (broken) { RemoveBuffVisual(); Report("护盾已被击破"); }
            else Report($"护盾吸收 {absorbed:0} 点伤害，剩余 {shieldPoints:0}");
            PlayShieldPulse(broken ? ShieldPulseKind.Break : ShieldPulseKind.Impact, absorbed / Mathf.Max(1, lastShieldCapacity));
            if (ShieldImpact != null)
                foreach (Action<float> listener in ShieldImpact.GetInvocationList())
                    try { listener(absorbed); } catch (Exception e) { Debug.LogWarning("护盾受击反馈监听失败：" + e.Message, this); }
        }

        /// <summary>按手持投射物、最新自定义效果、护盾的优先级取消一项；飞行中的投射物继续运行。</summary>
        public bool CancelLatest()
        {
            ++clearingVersion;
            if (heldObject) { ClearHeld(); Report("已取消持握火球"); return true; }
            while (extensions.Count > 0)
            {
                var entry = extensions[extensions.Count - 1]; bool alive = false;
                try { alive = entry.Value.IsAlive; }
                catch (Exception error) { Debug.LogWarning("扩展效果状态无效：" + error.Message, this); }
                RemoveExtension(entry);
                if (alive) { Report("已取消扩展效果"); return true; }
            }
            if (shieldPoints > 0) { ClearBuff(); shieldPulse?.Stop(); Report("已取消护盾"); return true; }
            Report("当前没有可取消的效果");
            return false;
        }

        /// <summary>放低双手时取消尚未发射的物品，已生效护盾和飞行中的投射物继续运行。</summary>
        public bool CancelUnreleased()
        {
            if (activating) ++clearingVersion;
            if (rightHandEffect != null)
            {
                var held = extensions.Find(entry => ReferenceEquals(entry.Value, rightHandEffect));
                if (held != null)
                {
                    if (held.Value is IReleasableHandEffect releasable)
                    {
                        try { releasable.ReleaseHand(); }
                        catch (Exception error)
                        {
                            Debug.LogWarning("右手效果收起失败，已清理：" + error.Message, this);
                            RemoveExtension(held);
                        }
                    }
                    else RemoveExtension(held);
                    Report("已收起右手法宝"); return true;
                }
            }
            if (!heldObject) return false;
            ClearHeld(); Report("已收起未发射火球，护盾与已发射效果保留"); return true;
        }

        public void ResetPushInput() => ResetMotion(Now);

        /// <summary>统一释放本执行器拥有的效果、临时视觉和材料，供禁用、销毁及暂停清理使用。</summary>
        public void ClearAll() => ClearEffects(false);

        /// <summary>倒下时立即清掉玩法状态，已触发的破盾余光按原有时长结束。</summary>
        public void ClearOnPlayerDeath() => ClearEffects(shieldPulse?.IsBreaking == true);

        private void ClearEffects(bool keepBreakAfterglow)
        {
            lastFullClear = ++clearingVersion;
            if (clearing) return;
            clearing = true;
            try
            {
                ClearHeld();
                ClearBuff();
                if (!keepBreakAfterglow)
                {
                    shieldPulse?.Dispose();
                    shieldPulse = null;
                }
                while (extensions.Count > 0) RemoveExtension(extensions[extensions.Count - 1]);
                while (projectiles.Count > 0)
                {
                    var projectile = projectiles[projectiles.Count - 1];
                    projectiles.RemoveAt(projectiles.Count - 1);
                    if (projectile) projectile.Finish();
                }
                foreach (var visual in transientVisuals)
                    if (visual) { visual.SetActive(false); Destroy(visual); }
                transientVisuals.Clear();
                if (vfxPool) vfxPool.CancelOwned(this);
                ReleaseOwnedMaterials();
                StopAllAudio();
                Status = "所有技能效果已清理";
            }
            finally { clearing = false; }
        }

        bool releasingHeld;
        private void ClearHeld()
        {
            if (releasingHeld) return;
            releasingHeld = true;
            releasingEffects++;
            try
            {
                if (heldObject && throwState != null) ObserveThrow("cancelled", "持球结束或取消", default, default, 0, false);
                throwInputValid = releasePoseValid = hasSafeHeldPoint = false;
                throwState?.Reset();
                throwIntent?.Reset();
                if (heldObject)
                {
                    StopHeldAudio(heldObject.transform);
                    if (!clearing) PlayAudio(heldParameters.Audio, SkillAudioCue.Ended, heldObject.transform.position);
                    currentFireball.Discard();
                }
                pushSamples.Clear();
                lostAt = -1;
                hasPreviousSample = false;
            }
            finally { releasingEffects--; releasingHeld = false; }
        }

        bool ThrowPathClear(Vector3 from, Vector3 to)
        {
            int mask = hitLayers.value & ~playerLayers.value;
            int count = Physics.OverlapSphereNonAlloc(to, heldParameters.Radius, throwOverlaps, mask, QueryTriggerInteraction.Ignore);
            if (count == throwOverlaps.Length) return false;
            for (int i=0;i<count;i++) if (!IgnoreThrowCollider(throwOverlaps[i])) return false;
            Vector3 delta = to-from; float distance=delta.magnitude;
            if (distance < .0001f) return true;
            count = Physics.SphereCastNonAlloc(from, heldParameters.Radius, delta/distance, throwSweeps,distance,mask,QueryTriggerInteraction.Ignore);
            if (count == throwSweeps.Length) return false;
            for(int i=0;i<count;i++) if(!IgnoreThrowCollider(throwSweeps[i].collider)) return false;
            return true;
        }
        bool IgnoreThrowCollider(Collider c) => !c || !c.enabled || (heldObject && c.transform.IsChildOf(heldObject.transform)) ||
            ((playerRoot ? playerRoot : head ? head.root : null) is Transform root && c.transform.IsChildOf(root));

        public string BuildThrowDebugDetails(double now)
        {
            if (!HasThrowObservation) return "尚无投掷观察；先生成火球。\n抬起持球手显示瞄准线，视线选择目标，手部动作确认发射。";
            var o=LastThrowObservation; var e=o.estimate;
            double age=double.IsFinite(now)&&double.IsFinite(o.observed)?Math.Max(0,now-o.observed)*1000:0;
            var b=new System.Text.StringBuilder();
            var score=o.score;
            b.AppendLine("准星采用头显正前方；三种动作只判定发射，火球从手边飞向准星。");
            if(score.windupDirection.sqrMagnitude>.5f)b.AppendLine($"前送动作诊断（{score.windupAimSource}）：原轨迹 {score.rawWindupDirection:F2} / 判定参考 {score.windupDirection:F2}");
            b.AppendLine($"未发射储存剩余 {HeldRemainingSeconds:0.0} 秒");
            b.AppendLine($"通过候选：{ReleaseModeLabel(score.candidate.ToString())}；最近实际发射：{LastReleaseMode}");
            b.AppendLine($"动作评分 {(score.available?$"{score.total:0.0} / 100":"未计算")}；前送阈值 {score.threshold:0}（非置信概率）");
            b.AppendLine($"阶段：{score.phase}；判定：{score.reason}");
            if(score.available) b.AppendLine($"速度项 {score.speedScore*100:0} / 位移项 {score.distanceScore*100:0} / 方向项 {score.directionScore*100:0} / 后拉项 {score.windupScore*100:0}");
            b.AppendLine($"后拉 {score.windupDistance*100:0.0} cm；前送 {score.forwardDistance*100:0.0} cm；相对/世界前向速度 {score.speed:0.00}/{score.worldSpeed:0.00} m/s");
            b.AppendLine($"头腕距离 {score.headWristDistance*100:0.0}/{score.reachRequired*100:0.0} cm；主动伸手={score.extensionReady}；保持 {score.holdProgress*100:0}%");
            b.AppendLine("保持路径："+score.holdReason);
            b.AppendLine($"采样 {score.samples}；方向一致性 {score.consistency:0.00}；前送误差 {score.fitError*1000:0.0} mm；动作轮次 {score.actionEpoch:0}");
            var assist=heldParameters.Throwing?.assisted;
            if(assist!=null && assist.enabled) {
                b.AppendLine($"前送启用={assist.windupEnabled}：后拉≥{assist.windupDistance*100:0}cm / 前送≥{assist.forwardDistance*100:0}cm / 速度≥{assist.forwardSpeed:0.00}m/s / 一致性≥{assist.minimumConsistency:0.00} / 分数≥{assist.scoreThreshold:0}；期限{assist.actionTimeout:0.00}s");
                b.AppendLine($"保持启用={assist.reachEnabled}：头腕≥{assist.comfortableReach*assist.reachRatio*100:0}cm / 主动前伸≥{assist.reachMotion*100:0}cm / 身前>{assist.minimumForwardReach*100:0}cm / 保持{assist.reachSeconds:0.00}s");
                b.AppendLine($"稳定门控：位置≤{assist.reachStability*100:0}cm / 速度≤{assist.reachSpeed:0.00}m/s / 瞄准波动≤{assist.aimStability:0}° / 前方半角≤{assist.forwardCone:0}°");
            }
            b.AppendLine($"事件 {o.eventType}；手形/腕骨时间差 {Math.Abs(o.observed-o.wristObserved)*1000:0} ms；输入占用={o.suppressed}");
            b.AppendLine("最近发射或拒绝："+LastThrowOutcome);
            b.AppendLine($"持球={HasHeldObject}；当前输入有效={ThrowInputValid}；观察年龄 {age:0} ms");
            b.AppendLine($"握拳 有效/闭合/张开={o.grip.fistValid}/{o.grip.fistClosed}/{o.grip.fistOpen}；捏合={o.grip.pinchValid}/{o.grip.pinchClosed}/{o.grip.pinchOpen}");
            b.AppendLine($"握持 {ThrowGripProgress*100:0}%；已准备={ThrowArmed}；释放候选={ReleasePending}；松手确认 {ThrowReleaseProgress*100:0}%");
            b.AppendLine($"候选编号 {o.candidateId}；样本 {e.samples}；轨迹跨度 {Math.Max(0,e.end-e.start)*1000:0} ms；确认延迟 {(e.onset>0?Math.Max(0,o.observed-e.onset)*1000:0):0} ms");
            b.AppendLine($"拟合速度 {e.velocity.magnitude:0.00} m/s；相对位移 {e.relativeDistance*100:0.0} cm；RMS误差 {e.rmsError*1000:0.0} mm；通过={e.accepted}");
            b.AppendLine($"释放位置 {o.releaseOrigin:F3}；输出速度 {o.launchVelocity:F2}");
            b.AppendLine($"上次发射位置 {LastLaunchPosition:F3}；方向 {LastLaunchDirection:F2}；累计 {LaunchCount} 发 / {HitCount} 次命中");
            if (heldParameters.Throwing != null) b.AppendLine($"配置：握持 {heldParameters.Throwing.gripSeconds*1000:0} ms / 松手 {heldParameters.Throwing.releaseSeconds*1000:0} ms / 最低速度 {heldParameters.Throwing.minimumSpeed:0.00} m/s / 误差上限 {heldParameters.Throwing.maximumFitError*1000:0} mm");
            b.Append("最近结果："+o.status+"\n轨迹判定："+(string.IsNullOrEmpty(e.reason)?"尚无冻结轨迹":e.reason));
            return b.ToString();
        }

        private void ClearBuff()
        {
            Vitals.ClearShield();
            RemoveBuffVisual();
        }

        private void RemoveBuffVisual()
        {
            FreePrepared(buffObject);
            buffObject = null;
        }

        private void CreateBuffVisual()
        {
            if (buffObject) return;
            if (activeShieldPrefab)
            {
                buffObject = Instantiate(activeShieldPrefab);
                buffObject.name = "自身加持_青纹护腕";
                DisableVisualColliders(buffObject);
                UpdateBuffVisual();
                return;
            }
            Material material = GetEffectMaterial(false, activeShieldMaterial);
            if (!material) return;
            buffObject = new GameObject("自身加持_护腕光环");
            var ring = buffObject.AddComponent<LineRenderer>();
            ring.sharedMaterial = material;
            ring.useWorldSpace = false;
            ring.loop = true;
            ring.widthMultiplier = 0.008f;
            ring.positionCount = 32;
            ring.shadowCastingMode = ShadowCastingMode.Off;
            ring.receiveShadows = false;
            for (int i = 0; i < ring.positionCount; ++i)
            {
                float angle = 2 * Mathf.PI * i / ring.positionCount;
                ring.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * 0.055f);
            }
            UpdateBuffVisual();
        }

        private static void DisableVisualColliders(GameObject visual)
        {
            // 关闭纯视觉资源中的碰撞器，避免它们影响瞄准射线或额外参与伤害碰撞。
            foreach (var collider in visual.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        }

        private void UpdateBuffVisual()
        {
            if (!buffObject && shieldPulse?.IsPlaying != true) return;
            bool tracked = TryGetWrist(true, out Pose wrist, out _);
            if (buffObject)
            {
                buffObject.SetActive(tracked);
                if (tracked) buffObject.transform.SetPositionAndRotation(wrist.position, wrist.rotation);
            }
            shieldPulse?.UpdatePose(Now, tracked, wrist, head);
        }

        private void PlayShieldPulse(ShieldPulseKind kind, float strength)
        {
            try
            {
                shieldPulse ??= ShieldWristPulse.Create(gameObject);
                shieldPulse?.Play(kind, strength, Now);
                UpdateBuffVisual();
            }
            catch (Exception error) { Debug.LogWarning("护盾瞬态反馈失败：" + error.Message, this); }
        }

        private Material GetEffectMaterial(bool fire, Material configured)
        {
            if (configured) return configured;
            if (fire && ownedFireMaterial) return ownedFireMaterial;
            if (!fire && ownedBuffMaterial) return ownedBuffMaterial;
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (!shader) shader = Shader.Find("Unlit/Color");
            if (!shader) shader = Shader.Find("Sprites/Default");
            if (!shader) return null;
            var material = new Material(shader) { name = fire ? "运行时火球材质" : "运行时护盾材质" };
            Color color = fire ? new Color(1, 0.22f, 0.025f) : new Color(0.05f, 0.9f, 1f);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (fire) ownedFireMaterial = material; else ownedBuffMaterial = material;
            return material;
        }

        private void ReleaseOwnedMaterials()
        {
            if (ownedFireMaterial) Destroy(ownedFireMaterial);
            if (ownedBuffMaterial) Destroy(ownedBuffMaterial);
            ownedFireMaterial = ownedBuffMaterial = null;
        }

        private void Report(string message)
        {
            Status = message;
            var listeners = Message;
            if (listeners == null) return;
            foreach (Action<string> listener in listeners.GetInvocationList())
            {
                try { listener(message); }
                catch (Exception error) { Debug.LogWarning("效果通知监听失败，不影响效果结算：" + error.Message, this); }
            }
        }
        private void OnDisable() => ClearAll();
        private void OnDestroy() { ClearAll(); ReleaseLegacyDefinitions(); }
        private void OnApplicationPause(bool paused) { if (paused) ClearAll(); }
    }
}
