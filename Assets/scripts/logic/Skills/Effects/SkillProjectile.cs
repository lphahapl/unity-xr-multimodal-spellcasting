using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using NarutoVR.SealCombat.Enemies;

namespace NarutoVR.SealCombat
{
    /// <summary>每个运动步进行球形扫掠，按命中锁存保证单次伤害结算。</summary>
    public sealed class SkillProjectile : MonoBehaviour
    {
        [Header("飞行状态（由技能效果初始化）")]
        [InspectorName("飞行速度（米/秒）"), Min(0.1f)] public float speed = 9;
        [InspectorName("碰撞半径（米）"), Min(0.001f)] public float radius = 0.065f;
        [InspectorName("命中伤害"), Min(0)] public float damage = 25;
        [InspectorName("基础削韧值"), Min(0)] public float poiseDamage = 20;
        [InspectorName("最大寿命（秒）"), Min(0.1f)] public float lifetime = 4;
        [Header("纯视觉反馈")]
        [InspectorName("命中爆散预置体")] public GameObject impactPrefab;
        public VfxPoolManager ImpactPool { get; set; }
        public Component ImpactOwner { get; set; }
        public Action<GameObject> ImpactCreated;
        internal Action<Vector3> SurfaceHit { get; set; }
        /// <summary>终止于非战斗目标表面；剑波用它反馈撞墙，敌人反馈走去重后的 StrikeResolved。</summary>
        internal Action<Vector3> WorldHit { get; set; }
        public SkillStrike Strike { get; set; }
        public Action<SkillHitResult> StrikeResolved { get; set; }
        public Func<double> GameClock { get; set; }
        public Transform AttackerTarget { get; set; }
        private double Now => GameClock?.Invoke() ?? Time.realtimeSinceStartupAsDouble;
        public bool IsFinished { get; private set; }

        private Vector3 direction;
        private Transform playerRoot;
        private int collisionMask;
        private double expiresAt;
        private bool initialized;
        private Action<SkillTrainingTarget> onTargetHit;
        private Action<SkillProjectile> onFinished;
        readonly string hitId = "projectile:" + Guid.NewGuid().ToString("N");
        HashSet<EnemyCombatState> passedLives;
        bool lightningOffered;
        Collider[] waveOverlaps;
        RaycastHit[] waveSweeps;

        public void Initialize(Vector3 flightDirection, Transform ignoredPlayerRoot, LayerMask hitMask,
            LayerMask playerLayers, Action<SkillTrainingTarget> targetHit, Action<SkillProjectile> finished)
        {
            direction = flightDirection.sqrMagnitude > 0.0001f ? flightDirection.normalized : Vector3.forward;
            playerRoot = ignoredPlayerRoot;
            collisionMask = hitMask.value & ~playerLayers.value;
            onTargetHit = targetHit;
            onFinished = finished;
            expiresAt = Now + Mathf.Max(0.1f, lifetime);
            initialized = true;
            // SphereCast 无法覆盖发射时已经重叠的表面，需先做重叠检测。
            if (Strike != null)
            {
                waveOverlaps = new Collider[16];
                waveSweeps = new RaycastHit[16];
                AdvanceWave(0);
            }
            else CheckInitialOverlap();
        }

        private void Update()
        {
            if (!initialized || IsFinished) return;
            if (Time.timeScale <= 0 || Now >= expiresAt) { Finish(); return; }
            Vector3 start = transform.position;
            float distance = Mathf.Max(0, speed) * Time.deltaTime;
            if (distance <= 0) return;

            if (Strike != null) { AdvanceWave(distance); return; }

            // 每步重查重叠，覆盖两次物理更新之间主动移入球体的障碍。
            if (CheckInitialOverlap()) return;
            RaycastHit[] hits = Physics.SphereCastAll(start, radius, direction, distance,
                collisionMask, QueryTriggerInteraction.Collide);
            Collider closest = null;
            Vector3 contact = default;
            float closestDistance = float.PositiveInfinity;
            foreach (RaycastHit hit in hits)
            {
                if (ShouldIgnore(hit.collider) || hit.distance >= closestDistance) continue;
                closest = hit.collider;
                closestDistance = hit.distance;
                contact = hit.point;
            }
            if (closest)
            {
                transform.position = start + direction * Mathf.Max(0, closestDistance);
                Hit(closest, contact);
            }
            else transform.position = start + direction * distance;
        }

        private bool CheckInitialOverlap()
        {
            Collider[] overlaps = Physics.OverlapSphere(transform.position, radius, collisionMask,
                QueryTriggerInteraction.Collide);
            // 发射点同时处于墙体和靶子内时，优先由墙体挡住。
            Collider target = null;
            foreach (Collider candidate in overlaps)
            {
                if (ShouldIgnore(candidate)) continue;
                if (!new SkillHitTarget(candidate).IsTarget) { Hit(candidate); return true; }
                if (!target) target = candidate;
            }
            if (!target) return false;
            Hit(target);
            return true;
        }

        private bool ShouldIgnore(Collider candidate) => SkillHitTarget.Ignore(candidate, transform, playerRoot);

        // A piercing contact can synchronously move, recycle, or create another enemy
        // or close a door. Re-query the remaining path after dispatching that contact.
        void AdvanceWave(float remaining)
        {
            while (!IsFinished && Strike != null)
            {
                Vector3 origin = transform.position;
                Collider collider = WaveOverlap(origin);
                Vector3 point = origin;
                if (!collider)
                {
                    if (remaining <= 0) return;
                    if (!WaveSweep(origin, remaining, out var hit))
                    {
                        transform.position = origin + direction * remaining;
                        return;
                    }
                    float travelled = Mathf.Clamp(hit.distance, 0, remaining);
                    transform.position = origin + direction * travelled;
                    remaining -= travelled;
                    collider = hit.collider;
                    point = hit.point;
                }

                var target = new SkillHitTarget(collider);
                if (!Strike.AlreadyHit(target.Enemy))
                {
                    Hit(collider, point);
                    return;
                }
                passedLives ??= new HashSet<EnemyCombatState>();
                passedLives.Add(target.Enemy.Combat);
                try { ReportStrike(target, point); }
                catch { Finish(); throw; }
                if (!IsFinished) Physics.SyncTransforms();
            }
        }

        bool IgnoreWaveCollider(Collider c)
        {
            if (ShouldIgnore(c)) return true;
            if (passedLives == null) return false;
            var target = new SkillHitTarget(c);
            return target.Enemy && passedLives.Contains(target.Enemy.Combat);
        }

        Collider WaveOverlap(Vector3 origin)
        {
            var buffer = waveOverlaps;
            int count = Physics.OverlapSphereNonAlloc(origin, radius, buffer, collisionMask, QueryTriggerInteraction.Collide);
            if (count == buffer.Length)
            {
                buffer = Physics.OverlapSphere(origin, radius, collisionMask, QueryTriggerInteraction.Collide);
                count = buffer.Length;
            }
            Collider first = null;
            float firstDepth = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                var c = buffer[i];
                if (IgnoreWaveCollider(c)) continue;
                if (!new SkillHitTarget(c).IsTarget) return c;
                float depth = Vector3.Dot(c.bounds.center - origin, direction);
                if (depth < firstDepth) { first = c; firstDepth = depth; }
            }
            return first;
        }

        bool WaveSweep(Vector3 from, float distance, out RaycastHit closest)
        {
            var buffer = waveSweeps;
            int count = Physics.SphereCastNonAlloc(from, radius, direction, buffer, distance, collisionMask, QueryTriggerInteraction.Collide);
            if (count == buffer.Length)
            {
                buffer = Physics.SphereCastAll(from, radius, direction, distance, collisionMask, QueryTriggerInteraction.Collide);
                count = buffer.Length;
            }
            closest = default;
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                var candidate = buffer[i];
                if (candidate.distance > nearest || IgnoreWaveCollider(candidate.collider)) continue;
                // A wall at exactly the same distance takes priority.
                if (candidate.distance == nearest && new SkillHitTarget(candidate.collider).IsTarget) continue;
                nearest = candidate.distance;
                closest = candidate;
            }
            return closest.collider;
        }

        void ReportStrike(SkillHitTarget target, Vector3 point)
        {
            var result = Strike.Resolve(target, point, !lightningOffered);
            if (result.CanTriggerLightning) lightningOffered = true;
            if (result.DirectApplied && result.TrainingTarget) onTargetHit?.Invoke(result.TrainingTarget);
            StrikeResolved?.Invoke(result);
        }

        private void Hit(Collider collider, Vector3? contact = null)
        {
            if (IsFinished) return;
            // 先锁定本次命中，再扣血及回调，避免多碰撞器或重入造成重复伤害。
            IsFinished = true;
            var target = new SkillHitTarget(collider);
            var point = contact ?? transform.position;
            try
            {
                SurfaceHit?.Invoke(point);
                if (!target.IsTarget) WorldHit?.Invoke(point);
                if (Strike != null)
                    ReportStrike(target, point);
                else if (target.Apply(hitId, damage, poiseDamage, AttackerTarget ? AttackerTarget : playerRoot) &&
                    target.TrainingTarget) onTargetHit?.Invoke(target.TrainingTarget);
            }
            finally { SpawnImpact(); Dispose(); }
        }

        private void SpawnImpact()
        {
            if (!impactPrefab) return;
            if (!ReferenceEquals(ImpactPool, null))
            {
                VfxPoolManager.Lease lease = default;
                try
                {
                    if (ImpactPool && ImpactPool.TryPlayImpact(impactPrefab, transform.position,
                        Quaternion.LookRotation(-direction), ImpactOwner, out lease)) ImpactCreated?.Invoke(lease.Visual);
                }
                catch (Exception error)
                {
                    lease.Release();
                    Debug.LogWarning("池化命中视觉播放失败：" + error.Message, this);
                }
                return;
            }
            GameObject visual = null;
            try
            {
                visual = Instantiate(impactPrefab, transform.position, Quaternion.LookRotation(-direction));
                visual.name = "火球命中_一次性爆散";
                SceneManager.MoveGameObjectToScene(visual, gameObject.scene);
                foreach (var collider in visual.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
                if (!visual.TryGetComponent<SkillVisualLifetime>(out var lifetime)) lifetime = visual.AddComponent<SkillVisualLifetime>();
                lifetime.enabled = true;
                visual.SetActive(true);
                ImpactCreated?.Invoke(visual);
            }
            catch (Exception error)
            {
                if (visual) Destroy(visual);
                Debug.LogWarning("命中视觉生成失败：" + error.Message, this);
            }
        }

        public void Finish()
        {
            if (IsFinished) return;
            IsFinished = true;
            Dispose();
        }

        private void Dispose()
        {
            gameObject.SetActive(false);
            var callback = onFinished;
            onTargetHit = null;
            onFinished = null;
            ImpactCreated = null;
            SurfaceHit = null;
            StrikeResolved = null;
            Strike = null;
            callback?.Invoke(this);
            if (Application.isPlaying) Destroy(gameObject); else DestroyImmediate(gameObject);
        }

        private void OnDisable()
        {
            if (initialized && !IsFinished) Finish();
        }

        private void OnApplicationPause(bool paused) { if (paused && initialized) Finish(); }
    }
}
