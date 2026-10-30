using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NarutoVR.SealCombat
{
    /// <summary>One swept sphere per movement step; no trigger callbacks or persistent damage.</summary>
    public sealed class SkillProjectile : MonoBehaviour
    {
        [Header("飞行状态（由技能效果初始化）")]
        [InspectorName("飞行速度（米/秒）"), Min(0.1f)] public float speed = 9;
        [InspectorName("碰撞半径（米）"), Min(0.001f)] public float radius = 0.065f;
        [InspectorName("命中伤害"), Min(0)] public float damage = 25;
        [InspectorName("最大寿命（秒）"), Min(0.1f)] public float lifetime = 4;
        [Header("纯视觉反馈")]
        [InspectorName("命中爆散预置体")] public GameObject impactPrefab;
        public Action<GameObject> ImpactCreated;
        public Func<double> GameClock { get; set; }
        private double Now => GameClock?.Invoke() ?? Time.realtimeSinceStartupAsDouble;
        public bool IsFinished { get; private set; }

        private Vector3 direction;
        private Transform playerRoot;
        private int collisionMask;
        private double expiresAt;
        private bool initialized;
        private Action<SkillTrainingTarget> onTargetHit;
        private Action<SkillProjectile> onFinished;

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
            // SphereCast does not detect a sphere already overlapping a surface at launch.
            CheckInitialOverlap();
        }

        private void Update()
        {
            if (!initialized || IsFinished) return;
            if (Time.timeScale <= 0 || Now >= expiresAt) { Finish(); return; }
            Vector3 start = transform.position;
            float distance = Mathf.Max(0, speed) * Time.deltaTime;
            if (distance <= 0) return;

            // Recheck overlaps for surfaces moved into the ball between physics updates.
            if (CheckInitialOverlap()) return;
            RaycastHit[] hits = Physics.SphereCastAll(start, radius, direction, distance,
                collisionMask, QueryTriggerInteraction.Ignore);
            Collider closest = null;
            float closestDistance = float.PositiveInfinity;
            foreach (RaycastHit hit in hits)
            {
                if (ShouldIgnore(hit.collider) || hit.distance >= closestDistance) continue;
                closest = hit.collider;
                closestDistance = hit.distance;
            }
            if (closest)
            {
                transform.position = start + direction * Mathf.Max(0, closestDistance);
                Hit(closest);
            }
            else transform.position = start + direction * distance;
        }

        private bool CheckInitialOverlap()
        {
            Collider[] overlaps = Physics.OverlapSphere(transform.position, radius, collisionMask,
                QueryTriggerInteraction.Ignore);
            // A blocking wall takes precedence over a target if both contain the launch point.
            Collider target = null;
            foreach (Collider candidate in overlaps)
            {
                if (ShouldIgnore(candidate)) continue;
                if (!candidate.GetComponentInParent<SkillTrainingTarget>()) { Hit(candidate); return true; }
                if (!target) target = candidate;
            }
            if (!target) return false;
            Hit(target);
            return true;
        }

        private bool ShouldIgnore(Collider candidate) => !candidate || !candidate.enabled ||
            candidate.transform.IsChildOf(transform) ||
            (playerRoot && candidate.transform.IsChildOf(playerRoot));

        private void Hit(Collider collider)
        {
            if (IsFinished) return;
            // Latch before applying damage/callbacks, including targets with several colliders.
            IsFinished = true;
            var target = collider.GetComponentInParent<SkillTrainingTarget>();
            try { if (target && target.ApplyHit(damage)) onTargetHit?.Invoke(target); }
            finally { SpawnImpact(); Dispose(); }
        }

        private void SpawnImpact()
        {
            if (!impactPrefab) return;
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
            callback?.Invoke(this);
            Destroy(gameObject);
        }

        private void OnDisable()
        {
            if (initialized && !IsFinished) Finish();
        }

        private void OnApplicationPause(bool paused) { if (paused && initialized) Finish(); }
    }
}
