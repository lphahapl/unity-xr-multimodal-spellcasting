using UnityEngine;

namespace NarutoVR.SealCombat
{
    /// <summary>管理脱离投射物后的短时视觉资源，按独立寿命销毁或交由对象池回收。</summary>
    public sealed class SkillVisualLifetime : MonoBehaviour
    {
        [Header("一次性视觉生命周期")]
        [InspectorName("自动销毁时间（秒）"), Min(.05f)] public float lifetime = 1.1f;
        private double expiresAt;
        private bool finished, poolOwned;
        internal void GiveLifetimeToPool() { poolOwned = true; enabled = false; }
        private void OnEnable() { finished = false; expiresAt = Time.realtimeSinceStartupAsDouble + Mathf.Max(.05f, lifetime); }
        private void Update()
        {
            if (Time.timeScale <= 0 || Time.realtimeSinceStartupAsDouble >= expiresAt) Finish();
        }
        public void Finish()
        {
            if (finished || poolOwned) return;
            finished = true; gameObject.SetActive(false); Destroy(gameObject);
        }
        private void OnApplicationPause(bool paused) { if (paused) Finish(); }
    }
}
