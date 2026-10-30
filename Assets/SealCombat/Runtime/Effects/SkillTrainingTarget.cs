using UnityEngine;

namespace NarutoVR.SealCombat
{
    /// <summary>A reusable, finite-health target. Projectiles own their single-hit guarantee.</summary>
    public sealed class SkillTrainingTarget : MonoBehaviour
    {
        [Header("一、训练靶生命与恢复")]
        [InspectorName("满额生命值"), Min(1)] public float health = 100;
        [InspectorName("击破后自动恢复（秒，0 为不恢复）"), Min(0)] public float resetAfterSeconds = 2;
        [Header("二、命中反馈")]
        [InspectorName("靶子渲染器")] public Renderer targetRenderer;
        [InspectorName("命中颜色")] public Color hitColor = new(1f, 0.35f, 0.06f);
        [InspectorName("击破颜色")] public Color defeatedColor = new(0.18f, 0.18f, 0.18f);
        [InspectorName("命中闪烁时间（秒）"), Min(0)] public float flashSeconds = 0.18f;

        public float CurrentHealth { get; private set; }
        public int HitCount { get; private set; }
        public bool IsDefeated => CurrentHealth <= 0;

        private MaterialPropertyBlock originalProperties, hitProperties;
        private double restoreAt, flashUntil;
        private bool initialized;
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private void Awake() => Initialize();

        private void Initialize()
        {
            if (initialized) return;
            initialized = true;
            if (!targetRenderer) targetRenderer = GetComponentInChildren<Renderer>();
            originalProperties = new MaterialPropertyBlock();
            hitProperties = new MaterialPropertyBlock();
            if (targetRenderer) targetRenderer.GetPropertyBlock(originalProperties);
            ResetTarget();
        }

        /// <returns>True only when a live target actually received damage.</returns>
        public bool ApplyHit(float damage)
        {
            Initialize();
            if (!isActiveAndEnabled || IsDefeated || !float.IsFinite(damage) || damage <= 0) return false;
            CurrentHealth = Mathf.Max(0, CurrentHealth - damage);
            ++HitCount;
            double now = Time.realtimeSinceStartupAsDouble;
            flashUntil = now + Mathf.Max(0, flashSeconds);
            if (IsDefeated) restoreAt = resetAfterSeconds > 0 ? now + resetAfterSeconds : double.PositiveInfinity;
            ShowColor(IsDefeated ? defeatedColor : hitColor);
            return true;
        }

        public void ResetTarget()
        {
            if (!initialized) { Initialize(); return; }
            CurrentHealth = float.IsFinite(health) ? Mathf.Max(1, health) : 100;
            restoreAt = flashUntil = 0;
            RestoreAppearance();
        }

        private void Update()
        {
            double now = Time.realtimeSinceStartupAsDouble;
            if (IsDefeated && restoreAt > 0 && now >= restoreAt) ResetTarget();
            else if (!IsDefeated && flashUntil > 0 && now >= flashUntil)
            {
                flashUntil = 0;
                RestoreAppearance();
            }
        }

        private void ShowColor(Color color)
        {
            if (!targetRenderer) return;
            targetRenderer.GetPropertyBlock(hitProperties);
            hitProperties.SetColor(BaseColor, color);
            hitProperties.SetColor(ColorId, color);
            targetRenderer.SetPropertyBlock(hitProperties);
        }

        private void RestoreAppearance()
        {
            if (targetRenderer && originalProperties != null) targetRenderer.SetPropertyBlock(originalProperties);
        }

        private void OnDisable() => RestoreAppearance();
    }
}
