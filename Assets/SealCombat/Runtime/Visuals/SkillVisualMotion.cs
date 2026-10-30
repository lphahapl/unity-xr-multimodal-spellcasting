using System;
using UnityEngine;

namespace NarutoVR.SealCombat
{
    /// <summary>Visual-only animation. No colliders, input, damage, or material instances.</summary>
    public sealed class SkillVisualMotion : MonoBehaviour
    {
        [Serializable]
        public struct Orbit
        {
            [InspectorName("旋转节点")] public Transform node;
            [InspectorName("每秒旋转角度")] public Vector3 angularVelocity;
        }
        [Header("一、符环与轨道")]
        [InspectorName("旋转层")] public Orbit[] orbits = Array.Empty<Orbit>();
        [InspectorName("呼吸核心")] public Transform pulseNode;
        [InspectorName("呼吸幅度"), Range(0, .2f)] public float pulseAmount = .055f;
        [InspectorName("呼吸频率"), Min(0)] public float pulseFrequency = 2;
        [Header("二、飞行尾迹")]
        [InspectorName("飞行时启用的尾迹")] public TrailRenderer[] flightTrails = Array.Empty<TrailRenderer>();
        [Header("三、一次性爆散（0为循环）")]
        [InspectorName("淡出时长（秒）"), Min(0)] public float fadeSeconds;
        [InspectorName("扩张节点")] public Transform expansionNode;
        [InspectorName("最终扩张倍率"), Min(1)] public float expansionMultiplier = 4;

        private Quaternion[] rotations;
        private float[] trailWidths;
        private Vector3 pulseScale, expansionScale;
        private Renderer[] renderers;
        private ParticleSystem[] particles;
        private MaterialPropertyBlock properties;
        private float elapsed;
        private bool prepared, flying;
        private static readonly int VisualOpacity = Shader.PropertyToID("_VisualOpacity");

        private void Prepare()
        {
            if (prepared) return;
            prepared = true;
            rotations = new Quaternion[orbits.Length];
            for (int i = 0; i < orbits.Length; ++i) rotations[i] = orbits[i].node ? orbits[i].node.localRotation : Quaternion.identity;
            trailWidths = new float[flightTrails.Length];
            for (int i = 0; i < flightTrails.Length; ++i) if (flightTrails[i]) trailWidths[i] = flightTrails[i].widthMultiplier;
            if (pulseNode) pulseScale = pulseNode.localScale;
            if (expansionNode) expansionScale = expansionNode.localScale;
            particles = GetComponentsInChildren<ParticleSystem>(true);
            renderers = GetComponentsInChildren<Renderer>(true);
            properties = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            Prepare(); elapsed = 0;
            foreach (var p in particles) if (p) { p.Clear(true); p.Play(true); }
            SetFlying(flying);
            SetOpacity(1);
        }

        public void SetFlying(bool active)
        {
            Prepare(); flying = active;
            Vector3 scale = transform.lossyScale;
            float largest = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            for (int i = 0; i < flightTrails.Length; ++i)
            {
                var trail = flightTrails[i]; if (!trail) continue;
                trail.Clear(); trail.emitting = active;
                trail.widthMultiplier = trailWidths[i] * largest;
            }
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            for (int i = 0; i < orbits.Length; ++i)
                if (orbits[i].node) orbits[i].node.localRotation = rotations[i] * Quaternion.Euler(orbits[i].angularVelocity * elapsed);
            if (pulseNode) pulseNode.localScale = pulseScale * (1 + Mathf.Sin(elapsed * pulseFrequency * Mathf.PI * 2) * pulseAmount);
            if (fadeSeconds <= 0) return;
            float progress = Mathf.Clamp01(elapsed / fadeSeconds);
            SetOpacity((1 - progress) * (1 - progress));
            if (expansionNode) expansionNode.localScale = expansionScale * Mathf.Lerp(1, expansionMultiplier, 1 - Mathf.Pow(1 - progress, 3));
        }

        private void SetOpacity(float opacity)
        {
            foreach (var renderer in renderers)
            {
                if (!renderer) continue;
                renderer.GetPropertyBlock(properties); properties.SetFloat(VisualOpacity, opacity); renderer.SetPropertyBlock(properties);
            }
        }

        private void OnDisable()
        {
            if (!prepared) return;
            foreach (var p in particles) if (p) p.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            foreach (var trail in flightTrails) if (trail) { trail.emitting = false; trail.Clear(); }
        }
    }
}
