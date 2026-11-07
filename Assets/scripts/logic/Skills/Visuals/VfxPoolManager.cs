using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NarutoVR.SealCombat
{
    /// <summary>场景级固定容量命中特效池；提前创建实例，命中时只租用已有槽位。</summary>
    [DisallowMultipleComponent]
    public sealed class VfxPoolManager : MonoBehaviour
    {
        [Header("一、命中特效预热")]
        [InspectorName("命中爆散预置体")] public GameObject impactPrefab;
        [InspectorName("预热数量"), Range(1, 32)] public int capacity = 6;
        [Header("二、实例颜色（可选）")]
        [InspectorName("覆盖命中特效颜色")] public bool overrideImpactTint;
        [InspectorName("命中特效颜色"), ColorUsage(true, true)] public Color impactTint = Color.white;

        public int PrewarmedCount => slots.Length;
        public int ActiveCount { get; private set; }
        public int AvailableCount => PrewarmedCount - ActiveCount;
        public int SkippedAtCapacity { get; private set; }

        // 租约携带代次，防止旧租约错误回收已经再次出租的槽位。
        public readonly struct Lease
        {
            private readonly VfxPoolManager pool;
            private readonly int index;
            private readonly ulong generation;

            internal Lease(VfxPoolManager pool, int index, ulong generation)
            { this.pool = pool; this.index = index; this.generation = generation; }

            public GameObject Visual => pool && pool.IsCurrent(index, generation) ? pool.slots[index].visual : null;
            public bool Release() => pool && pool.IsCurrent(index, generation) && pool.ReturnSlot(pool.slots[index]);
        }

        private sealed class Slot
        {
            public GameObject visual;
            public Component owner;
            public bool active;
            public ulong generation;
            public double expiresAt;
            public float lifetime;
            public Renderer[] renderers;
            public MaterialPropertyBlock[] originalBlocks;
            public int[] colorIds;
            public ParticleSystem[] particles;
        }

        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private MaterialPropertyBlock tintBlock;
        private GameObject poolRoot, preparedPrefab;
        private Slot[] slots = Array.Empty<Slot>();
        private ulong nextGeneration;
        private int returning;

        private void Awake() => Prewarm();

        public bool Handles(GameObject prefab) => prefab && prefab == (preparedPrefab ? preparedPrefab : impactPrefab);

        /// <summary>按配置容量提前创建视觉实例，复用时恢复颜色、粒子和动画状态。</summary>
        public void Prewarm()
        {
            if (poolRoot || !impactPrefab) return;
            tintBlock ??= new MaterialPropertyBlock();
            // 命中特效挂到世界空间根节点，避免继续跟随玩家或 XR Rig 移动。
            poolRoot = new GameObject("命中特效池（运行时）");
            poolRoot.SetActive(false);
            SceneManager.MoveGameObjectToScene(poolRoot, gameObject.scene);
            preparedPrefab = impactPrefab;
            try
            {
                slots = new Slot[Mathf.Clamp(capacity, 1, 32)];
                for (int i = 0; i < slots.Length; ++i)
                {
                    var visual = Instantiate(preparedPrefab, poolRoot.transform, false);
                    visual.name = $"命中爆散（池 {i + 1}）";
                    visual.SetActive(false);
                    var lifetime = visual.GetComponent<SkillVisualLifetime>();
                    float seconds = lifetime ? Mathf.Max(.05f, lifetime.lifetime) : 1.1f;
                    foreach (var timer in visual.GetComponentsInChildren<SkillVisualLifetime>(true)) timer.GiveLifetimeToPool();
                    foreach (var collider in visual.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
                    foreach (var motion in visual.GetComponentsInChildren<SkillVisualMotion>(true)) motion.Prepare();
                    var renderers = visual.GetComponentsInChildren<Renderer>(true);
                    var slot = new Slot { visual = visual, lifetime = seconds, renderers = renderers,
                        originalBlocks = new MaterialPropertyBlock[renderers.Length], colorIds = new int[renderers.Length],
                        particles = visual.GetComponentsInChildren<ParticleSystem>(true) };
                    for (int j = 0; j < renderers.Length; ++j)
                    {
                        var material = renderers[j].sharedMaterial;
                        slot.colorIds[j] = material && material.HasProperty(BaseColor) ? BaseColor :
                            material && material.HasProperty(ColorId) ? ColorId : -1;
                        slot.originalBlocks[j] = new MaterialPropertyBlock();
                        renderers[j].GetPropertyBlock(slot.originalBlocks[j]);
                    }
                    foreach (var particle in slot.particles)
                    {
                        var main = particle.main;
                        if (main.stopAction == ParticleSystemStopAction.Destroy || main.stopAction == ParticleSystemStopAction.Disable)
                            main.stopAction = ParticleSystemStopAction.None;
                    }
                    slots[i] = slot;
                }
                poolRoot.SetActive(true);
            }
            catch { DisposePool(); throw; }
        }

        /// <summary>尝试租用空槽并返回带代次的租约；容量用尽时跳过视觉，不改变命中结算。</summary>
        public bool TryPlayImpact(GameObject prefab, Vector3 position, Quaternion rotation, Component owner, out Lease lease)
        {
            lease = default;
            if (!isActiveAndEnabled || !poolRoot || !Handles(prefab) || returning != 0 || Time.timeScale <= 0) return false;
            for (int i = 0; i < slots.Length; ++i)
            {
                var slot = slots[i];
                if (slot.active || !slot.visual) continue;
                slot.active = true;
                slot.owner = owner;
                slot.generation = ++nextGeneration;
                slot.expiresAt = Time.realtimeSinceStartupAsDouble + slot.lifetime;
                ActiveCount++;
                lease = new Lease(this, i, slot.generation);
                try
                {
                    slot.visual.transform.SetPositionAndRotation(position, rotation);
                    for (int j = 0; j < slot.renderers.Length; ++j)
                    {
                        var renderer = slot.renderers[j];
                        if (!renderer) continue;
                        renderer.SetPropertyBlock(slot.originalBlocks[j].isEmpty ? null : slot.originalBlocks[j]);
                        if (!overrideImpactTint || slot.colorIds[j] == -1) continue;
                        renderer.GetPropertyBlock(tintBlock);
                        tintBlock.SetColor(slot.colorIds[j], impactTint);
                        renderer.SetPropertyBlock(tintBlock);
                    }
                    slot.visual.SetActive(true);
                    return true;
                }
                catch { lease.Release(); throw; }
            }
            SkippedAtCapacity++;
            return false;
        }

        private bool IsCurrent(int index, ulong generation) => index >= 0 && index < slots.Length &&
            slots[index].active && slots[index].generation == generation;

        private bool ReturnSlot(Slot slot)
        {
            if (slot == null || !slot.active) return false;
            slot.active = false;
            slot.owner = null;
            ActiveCount--;
            returning++;
            try
            {
                if (slot.visual)
                {
                    foreach (var particle in slot.particles)
                        if (particle) particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                    slot.visual.SetActive(false);
                }
            }
            finally { returning--; }
            return true;
        }

        /// <summary>按所属执行器回收效果，避免一个角色的清理误伤其他角色的视觉。</summary>
        public void CancelOwned(Component owner)
        {
            if (!owner) return;
            foreach (var slot in slots) if (slot != null && slot.owner == owner) ReturnSlot(slot);
        }

        public void CancelAll() { foreach (var slot in slots) ReturnSlot(slot); }

        private void Update() => CollectExpired(Time.realtimeSinceStartupAsDouble);

        public void CollectExpired(double now)
        {
            if (Time.timeScale <= 0) { CancelAll(); return; }
            foreach (var slot in slots)
                if (slot.active && (!slot.visual || !slot.visual.activeSelf || now >= slot.expiresAt)) ReturnSlot(slot);
        }

        public void DisposePool()
        {
            CancelAll();
            slots = Array.Empty<Slot>();
            preparedPrefab = null;
            if (poolRoot)
            {
                poolRoot.SetActive(false);
                if (Application.isPlaying) Destroy(poolRoot); else DestroyImmediate(poolRoot);
                poolRoot = null;
            }
        }

        private void OnDisable() => CancelAll();
        private void OnApplicationPause(bool paused) { if (paused) CancelAll(); }
        private void OnDestroy() => DisposePool();
    }
}
