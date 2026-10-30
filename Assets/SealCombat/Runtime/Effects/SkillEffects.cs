using System;
using System.Collections.Generic;
using Oculus.Interaction.Input;
using NarutoVR.SealLab;
using UnityEngine;
using UnityEngine.Rendering;

namespace NarutoVR.SealCombat
{
    [DefaultExecutionOrder(350)]
    public sealed class SkillEffects : MonoBehaviour
    {
        [Header("一、玩家与当前追踪来源")]
        [InspectorName("手印采样器")] public NarutoVR.SealLab.SealLab lab;
        [InspectorName("头显视点")] public Transform head;
        [InspectorName("玩家根节点（碰撞忽略）")] public Transform playerRoot;
        [Header("二、旧场景兼容材质（技能绑定效果资产时不使用）")]
        [InspectorName("火球材质")] public Material fireMaterial;
        [InspectorName("护盾材质")] public Material buffMaterial;
        [Header("二（补充）、旧场景兼容预置体")]
        [InspectorName("火球视觉预置体")] public GameObject fireballPrefab;
        [InspectorName("护盾视觉预置体")] public GameObject shieldPrefab;
        [InspectorName("命中爆散预置体")] public GameObject impactPrefab;
        [Header("三、旧场景兼容护盾数值")]
        [InspectorName("护盾上限"), Min(1)] public float shieldCapacity = 100;
        [Header("四、旧场景兼容持握与轻推参数")]
        [InspectorName("火球半径（米）"), Min(0.01f)] public float ballRadius = 0.065f;
        [InspectorName("持握偏移（头显朝向坐标）")] public Vector3 heldOffset = new(0, 0.05f, 0.08f);
        [InspectorName("持握最长时间（秒）"), Min(0.1f)] public float heldLifetime = 10;
        [InspectorName("生成和追踪恢复后的准备时间（秒）"), Min(0.25f)] public float armDelay = 0.25f;
        [InspectorName("丢失追踪后的消散时间（秒）"), Min(0.05f)] public float trackingLossGrace = 0.5f;
        [InspectorName("前推最小位移（米）"), Min(0.05f)] public float pushDistance = 0.12f;
        [InspectorName("前推最短时长（秒）"), Min(0.05f)] public float pushMinimumSeconds = 0.15f;
        [InspectorName("前推最长时长（秒）"), Min(0.1f)] public float pushMaximumSeconds = 0.6f;
        [Header("五、飞行与碰撞")]
        [InspectorName("飞行速度（米/秒）"), Min(0.1f)] public float projectileSpeed = 9;
        [InspectorName("火球命中伤害"), Min(0)] public float projectileDamage = 25;
        [InspectorName("飞行寿命（秒）"), Min(0.1f)] public float projectileLifetime = 4;
        [InspectorName("头向瞄准最远距离（米）"), Min(1)] public float aimDistance = 30;
        [InspectorName("可碰撞层")] public LayerMask hitLayers = ~(1 << 2);
        [InspectorName("玩家层（额外忽略）")] public LayerMask playerLayers;

        public string Status { get; private set; } = "无生效技能";
        public bool HasHeldObject => heldObject;
        public bool HasRightHeldObject => heldObject && !heldInLeftHand;
        public int ActiveProjectileCount => projectiles.Count;
        public int LaunchCount { get; private set; }
        public bool PushInputSuppressed { get; set; }
        public Func<double> GameClock { get; set; }
        // Future actor adapters can bind this seam; production scenes use the existing tracked source by default.
        public Func<bool, Pose?> WristPoseOverride { get; set; }
        public float BuffRemaining => shieldPoints > 0 ? Mathf.Max(0, (float)(buffExpiresAt - Now)) : 0;
        public float ShieldPoints => BuffRemaining > 0 ? shieldPoints : 0;
        public float ShieldCapacity => activeShieldCapacity > 0 ? activeShieldCapacity : shieldCapacity;
        public int ActivationCount { get; private set; }
        public int HitCount { get; private set; }
        public event Action<string> Message;

        private double Now => GameClock?.Invoke() ?? Time.realtimeSinceStartupAsDouble;
        private float shieldPoints, activeShieldCapacity;
        private double buffExpiresAt, heldExpiresAt, armAt, lostAt = -1;
        private GameObject heldObject, buffObject;
        private bool heldInLeftHand;
        private ProjectileParameters heldParameters;
        private GameObject activeShieldPrefab;
        private Material activeShieldMaterial;
        private Material ownedFireMaterial, ownedBuffMaterial;
        private readonly List<SkillProjectile> projectiles = new();
        private readonly List<GameObject> transientVisuals = new();
        private readonly List<PushSample> pushSamples = new();
        private readonly List<ExtensionLifetime> extensions = new();
        private readonly List<ExtensionLifetime> tickingExtensions = new();
        private bool clearing, activating, createdHeldThisActivation;
        private int releasingExtensions;
        private long nextExtensionId;
        private double lastObserved = double.NegativeInfinity;
        private Vector3 lastHandPosition, lastHeadPosition;
        private bool hasPreviousSample;

        // Values are captured once at activation. Editing a shared SO or activating a different
        // skill cannot change an existing held ball's motion, damage or eventual flight settings.
        private readonly struct ProjectileParameters
        {
            public readonly float Radius, Damage, Speed, FlightLifetime, HeldLifetime, ArmDelay, LossGrace, PushDistance, PushMinimum, PushMaximum;
            public readonly Vector3 Offset;
            public readonly GameObject Visual, Impact;
            public readonly Material Material;
            public ProjectileParameters(ProjectileEffectDefinition definition, SkillEffects legacy)
            {
                Radius = definition ? definition.radius : Mathf.Max(.01f, legacy.ballRadius);
                Damage = definition ? definition.damage : Mathf.Max(0, legacy.projectileDamage);
                Speed = definition ? definition.speed : Mathf.Max(.1f, legacy.projectileSpeed);
                FlightLifetime = definition ? definition.flightLifetime : Mathf.Max(.1f, legacy.projectileLifetime);
                HeldLifetime = definition ? definition.heldLifetime : Mathf.Max(.1f, legacy.heldLifetime);
                ArmDelay = definition ? definition.armDelay : Mathf.Max(.25f, legacy.armDelay);
                LossGrace = definition ? definition.trackingLossGrace : Mathf.Max(.05f, legacy.trackingLossGrace);
                PushDistance = definition ? definition.pushDistance : Mathf.Max(.05f, legacy.pushDistance);
                PushMinimum = definition ? definition.pushMinimumSeconds : Mathf.Max(.05f, legacy.pushMinimumSeconds);
                PushMaximum = definition ? definition.pushMaximumSeconds : Mathf.Max(PushMinimum, legacy.pushMaximumSeconds);
                Offset = definition ? definition.heldOffset : legacy.heldOffset;
                Visual = definition ? definition.visualPrefab : legacy.fireballPrefab;
                Impact = definition ? definition.impactPrefab : legacy.impactPrefab;
                Material = definition ? definition.material : legacy.fireMaterial;
            }
        }

        private sealed class ExtensionLifetime
        {
            public readonly ISkillEffectLifetime Value;
            public readonly long Id;
            public bool Cancelled;
            public ExtensionLifetime(ISkillEffectLifetime value, long id) { Value = value; Id = id; }
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
            if (!lab) lab = GetComponent<NarutoVR.SealLab.SealLab>();
            if (!head && Camera.main) head = Camera.main.transform;
            if (!playerRoot && head) playerRoot = head.root;
            int playerLayer = LayerMask.NameToLayer("Player");
            if (playerLayer >= 0) playerLayers = playerLayers.value | (1 << playerLayer);
        }

        public bool TryActivate(SkillDefinition skill)
        {
            if (!isActiveAndEnabled || Time.timeScale <= 0 || !skill || activating || clearing) return false;
            long extensionStart = nextExtensionId;
            bool activated = false;
            createdHeldThisActivation = false;
            activating = true;
            try
            {
                if (skill.effectDefinition)
                {
                    string error = skill.effectDefinition.ValidateDefinition();
                    if (error != null) { Report("效果配置无效：" + error); return false; }
                    activated = skill.effectDefinition.TryActivate(this, skill);
                }
                else if (skill.effectKind == SkillEffectKind.SelfBuff) activated = TryApplyShield(null, skill);
                else if (skill.effectKind == SkillEffectKind.HeldFireball) activated = TryCreateProjectile(null, skill);
                if (activated) ++ActivationCount;
                return activated;
            }
            catch (Exception error)
            {
                // This invocation may have created a held visual before a later setup step failed.
                if (createdHeldThisActivation) ClearHeld();
                Report("效果激活失败：" + error.Message); return false;
            }
            finally
            {
                // A custom definition which registers then rejects/throws cannot leave a new lifetime behind.
                if (!activated)
                    while (true)
                    {
                        ExtensionLifetime newest = null;
                        for (int i = extensions.Count - 1; i >= 0; i--)
                            if (extensions[i].Id > extensionStart) { newest = extensions[i]; break; }
                        if (newest == null) break;
                        RemoveExtension(newest);
                    }
                createdHeldThisActivation = false;
                activating = false;
            }
        }

        /// <summary>Definition implementation entry. TryActivate owns successful activation counting.</summary>
        public bool TryApplyShield(ShieldEffectDefinition definition, SkillDefinition skill)
        {
            if (!isActiveAndEnabled || Time.timeScale <= 0 || !skill || clearing || releasingExtensions > 0 ||
                (definition && definition.ValidateDefinition() != null)) return false;
            bool refreshing = BuffRemaining > 0;
            float capacity = definition ? definition.capacity : (float.IsFinite(shieldCapacity) ? Mathf.Max(1, shieldCapacity) : 100);
            GameObject visual = definition ? definition.visualPrefab : shieldPrefab;
            Material material = definition ? definition.material : buffMaterial;
            if (activeShieldPrefab != visual || activeShieldMaterial != material)
            {
                if (buffObject) { buffObject.SetActive(false); Destroy(buffObject); }
                buffObject = null;
            }
            activeShieldPrefab = visual; activeShieldMaterial = material;
            shieldPoints = activeShieldCapacity = capacity;
            float duration = float.IsFinite(skill.durationSeconds) ? Mathf.Max(.1f, skill.durationSeconds) : 10;
            buffExpiresAt = Now + duration;
            // A purely visual failure must not refund an already committed, working shield.
            try { CreateBuffVisual(); }
            catch (Exception error) { Debug.LogWarning("护盾已生效，但视觉创建失败：" + error.Message, this); }
            Report($"{skill.displayName}：{(refreshing ? "已刷新" : "已获得")}护盾 {shieldPoints:0}，持续 {duration:0.#} 秒");
            return true;
        }

        /// <summary>Definition implementation entry. Captures all projectile parameters before creating the held instance.</summary>
        public bool TryCreateProjectile(ProjectileEffectDefinition definition, SkillDefinition skill)
        {
            if (!isActiveAndEnabled || Time.timeScale <= 0 || !skill || clearing || releasingExtensions > 0 ||
                (definition && definition.ValidateDefinition() != null)) return false;
            if (heldObject) { Report("手部技能槽已占用，请先发射或取消当前火球"); return false; }
            if (!head || !TryGetWrist(skill.leftHand, out Pose wrist, out _))
            { Report($"{(skill.leftHand ? "左" : "右")}手当前追踪无效，无法生成火球"); return false; }

            var parameters = new ProjectileParameters(definition, this);
            createdHeldThisActivation = true;
            if (parameters.Visual) heldObject = Instantiate(parameters.Visual);
            else
            {
                Material material = GetEffectMaterial(true, parameters.Material);
                if (!material) { Report("火球材质不可用，生成失败"); return false; }
                heldObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Collider sphereCollider = heldObject.GetComponent<Collider>();
                sphereCollider.enabled = false;
                Destroy(sphereCollider);
                var renderer = heldObject.GetComponent<Renderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            heldParameters = parameters;
            heldObject.name = "技能火球_持握";
            heldObject.transform.localScale = Vector3.one * parameters.Radius * 2;
            DisableVisualColliders(heldObject);
            heldObject.SetActive(true);
            if (heldObject.TryGetComponent<SkillVisualMotion>(out var motion)) motion.SetFlying(false);
            heldInLeftHand = skill.leftHand;
            float holdSeconds = float.IsFinite(skill.durationSeconds) ? Mathf.Max(0.1f, skill.durationSeconds) : 10;
            heldExpiresAt = Now + Mathf.Min(parameters.HeldLifetime, holdSeconds);
            lostAt = -1;
            ResetMotion(Now);
            UpdateHeldPosition(wrist);
            Report($"{skill.displayName}已在{(heldInLeftHand ? "左" : "右")}手生成，准备后向前轻推发射");
            return true;
        }

        private void LateUpdate()
        {
            if (Time.timeScale <= 0)
            {
                if (heldObject || shieldPoints > 0 || projectiles.Count > 0 || transientVisuals.Count > 0 || extensions.Count > 0) ClearAll();
                return;
            }
            double now = Now;
            TickExtensions(now);
            transientVisuals.RemoveAll(item => !item);
            if (shieldPoints > 0 && now >= buffExpiresAt)
            {
                ClearBuff();
                Report("护盾持续时间已结束");
            }
            UpdateBuffVisual();
            if (heldObject) UpdateHeld(now);
        }

        private void UpdateHeld(double now)
        {
            if (now >= heldExpiresAt) { ClearHeld(); Report("火球持握超时，已消散"); return; }
            if (!head || !TryGetWrist(heldInLeftHand, out Pose wrist, out double observed))
            {
                heldObject.SetActive(false);
                if (lostAt < 0) { lostAt = now; ResetMotion(now); Report("手部追踪丢失，火球已隐藏"); }
                if (now - lostAt >= heldParameters.LossGrace)
                { ClearHeld(); Report("手部追踪未恢复，火球已消散"); }
                return;
            }
            if (lostAt >= 0)
            {
                lostAt = -1;
                ResetMotion(now);
                Report("手部追踪恢复，请重新向前轻推");
            }
            heldObject.SetActive(true);
            UpdateHeldPosition(wrist);
            if (PushInputSuppressed) ResetMotion(now);
            else TrackPush(wrist.position, observed, now);
        }

        private void UpdateHeldPosition(Pose wrist)
        {
            Vector3 offset = heldParameters.Offset;
            if (heldInLeftHand) offset.x = -offset.x;
            heldObject.transform.SetPositionAndRotation(wrist.position + head.rotation * offset, wrist.rotation);
        }

        private bool TryGetWrist(bool leftHand, out Pose worldPose, out double observed)
        {
            worldPose = Pose.identity;
            observed = 0;
            if (WristPoseOverride != null)
            {
                Pose? supplied = WristPoseOverride(leftHand);
                if (!supplied.HasValue || !NarutoVR.SealLab.SealLab.FinitePose(supplied.Value)) return false;
                worldPose = supplied.Value; observed = Time.realtimeSinceStartupAsDouble; return true;
            }
            if (!lab || !lab.isActiveAndEnabled || !lab.recordingOrigin || lab.Frame == null) return false;
            HandSnapshot hand = leftHand ? lab.Frame.left : lab.Frame.right;
            int wristIndex = (int)HandJointId.HandWristRoot;
            if (hand == null || !hand.connected || !hand.tracked || !hand.fresh ||
                hand.joints == null || hand.jointValid == null || wristIndex >= hand.joints.Length ||
                wristIndex >= hand.jointValid.Length || !hand.jointValid[wristIndex]) return false;
            double age = Time.realtimeSinceStartupAsDouble - hand.observedAt;
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
            pushSamples.Clear();
            lastObserved = double.NegativeInfinity;
            hasPreviousSample = false;
            armAt = now + heldParameters.ArmDelay;
        }

        private void TrackPush(Vector3 handPosition, double observed, double now)
        {
            // Never treat a reused snapshot as new hand motion.
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
                Vector3 direction = (GetAimPoint() - heldObject.transform.position).normalized;
                Launch(direction);
                return;
            }
            pushSamples.Add(new PushSample(handPosition, headPosition, head.forward, observed));
        }

        /// <summary>Both world movement and movement relative to the head must advance forward.</summary>
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

        /// <summary>The crosshair ray supplies the aim point; the ball flies toward it from the hand.</summary>
        public Vector3 GetAimPoint()
        {
            if (!head) return transform.position + transform.forward * Mathf.Max(1, aimDistance);
            float range = Mathf.Max(1, aimDistance);
            Vector3 point = head.position + head.forward * range;
            float nearest = range;
            Transform ignoredRoot = playerRoot ? playerRoot : head.root;
            RaycastHit[] hits = Physics.RaycastAll(head.position, head.forward, range,
                hitLayers.value & ~playerLayers.value, QueryTriggerInteraction.Ignore);
            foreach (RaycastHit hit in hits)
            {
                if (!hit.collider || (ignoredRoot && hit.collider.transform.IsChildOf(ignoredRoot)) ||
                    hit.distance >= nearest) continue;
                point = hit.point;
                nearest = hit.distance;
            }
            return point;
        }

        private void Launch(Vector3 direction)
        {
            GameObject ball = heldObject;
            heldObject = null;
            pushSamples.Clear();
            lostAt = -1;
            ball.name = "技能火球_飞行";
            var projectile = ball.AddComponent<SkillProjectile>();
            projectile.speed = heldParameters.Speed;
            projectile.radius = heldParameters.Radius;
            projectile.damage = heldParameters.Damage;
            projectile.lifetime = heldParameters.FlightLifetime;
            projectile.impactPrefab = heldParameters.Impact;
            projectile.ImpactCreated = OnImpactCreated;
            projectile.GameClock = GameClock;
            if (ball.TryGetComponent<SkillVisualMotion>(out var motion)) motion.SetFlying(true);
            projectiles.Add(projectile);
            Report("火球已发射");
            ++LaunchCount;
            projectile.Initialize(direction, playerRoot ? playerRoot : head.root, hitLayers, playerLayers,
                OnTargetHit, OnProjectileFinished);
        }

        private void OnTargetHit(SkillTrainingTarget target)
        {
            ++HitCount;
            Report($"命中训练靶：剩余生命 {target.CurrentHealth:0}，累计命中 {HitCount} 次");
        }

        private void OnProjectileFinished(SkillProjectile projectile) => projectiles.Remove(projectile);
        private void OnImpactCreated(GameObject visual) { if (visual) transientVisuals.Add(visual); }

        /// <summary>
        /// Transfers lifecycle ownership only when true is returned. The caller owns cleanup after rejection.
        /// An existing handle cannot be registered twice; registration during cancellation is rejected.
        /// </summary>
        public bool RegisterEffectLifetime(ISkillEffectLifetime effect)
        {
            if (effect == null || !isActiveAndEnabled || Time.timeScale <= 0 || clearing || releasingExtensions > 0) return false;
            if (effect is UnityEngine.Object unityObject && !unityObject) return false;
            foreach (var entry in extensions) if (ReferenceEquals(entry.Value, effect)) return false;
            try { if (!effect.IsAlive) return false; }
            catch (Exception error) { Debug.LogWarning("扩展效果注册失败：" + error.Message, this); return false; }
            extensions.Add(new ExtensionLifetime(effect, ++nextExtensionId));
            return true;
        }

        private void TickExtensions(double now)
        {
            // User effects may register or cancel other effects inside Tick. Snapshot entries, not
            // runtime objects, and check ownership again before touching each one.
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
            extensions.Remove(entry); entry.Cancelled = true;
            releasingExtensions++;
            try { entry.Value.Cancel(); }
            catch (Exception error) { Debug.LogWarning("扩展效果结束时发生异常：" + error.Message, this); }
            finally { releasingExtensions--; }
        }

        /// <returns>The portion of incoming damage that remains after this shield absorbs it.</returns>
        public float AbsorbDamage(float damage)
        {
            if (!float.IsFinite(damage) || damage <= 0) return 0;
            if (BuffRemaining <= 0) { ClearBuff(); return damage; }
            float absorbed = Mathf.Min(shieldPoints, damage);
            shieldPoints -= absorbed;
            if (shieldPoints <= 0) { ClearBuff(); Report("护盾已被击破"); }
            else Report($"护盾吸收 {absorbed:0} 点伤害，剩余 {shieldPoints:0}");
            return damage - absorbed;
        }

        /// <summary>Cancel priority: held projectile, newest custom lifetime, then shield. Launched projectiles continue.</summary>
        public bool CancelLatest()
        {
            if (heldObject) { ClearHeld(); Report("已取消持握火球"); return true; }
            while (extensions.Count > 0)
            {
                var entry = extensions[extensions.Count - 1]; bool alive = false;
                try { alive = entry.Value.IsAlive; }
                catch (Exception error) { Debug.LogWarning("扩展效果状态无效：" + error.Message, this); }
                RemoveExtension(entry);
                if (alive) { Report("已取消扩展效果"); return true; }
            }
            if (shieldPoints > 0) { ClearBuff(); Report("已取消护盾"); return true; }
            Report("当前没有可取消的效果");
            return false;
        }

        /// <summary>Low-hands cancellation preserves shields and already flying projectiles.</summary>
        public bool CancelUnreleased()
        {
            if (!heldObject) return false;
            ClearHeld(); Report("已收起未发射火球，护盾与已发射效果保留"); return true;
        }

        public void ResetPushInput() => ResetMotion(Now);

        public void ClearAll()
        {
            if (clearing) return;
            clearing = true;
            try
            {
                ClearHeld();
                ClearBuff();
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
                ReleaseOwnedMaterials();
                Status = "所有技能效果已清理";
            }
            finally { clearing = false; }
        }

        private void ClearHeld()
        {
            if (heldObject) { heldObject.SetActive(false); Destroy(heldObject); }
            heldObject = null;
            pushSamples.Clear();
            lostAt = -1;
            hasPreviousSample = false;
        }

        private void ClearBuff()
        {
            shieldPoints = 0;
            buffExpiresAt = 0;
            if (buffObject) { buffObject.SetActive(false); Destroy(buffObject); }
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
            // User-edited visuals must never add damage or interfere with aim/collision queries.
            foreach (var collider in visual.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        }

        private void UpdateBuffVisual()
        {
            if (!buffObject) return;
            if (TryGetWrist(true, out Pose wrist, out _))
            {
                buffObject.SetActive(true);
                buffObject.transform.SetPositionAndRotation(wrist.position, wrist.rotation);
            }
            else if (head)
            {
                buffObject.SetActive(true);
                buffObject.transform.SetPositionAndRotation(head.position + head.rotation * new Vector3(-0.25f, -0.22f, 0.5f), head.rotation);
            }
            else buffObject.SetActive(false);
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
        private void OnDestroy() => ClearAll();
        private void OnApplicationPause(bool paused) { if (paused) ClearAll(); }
    }
}
