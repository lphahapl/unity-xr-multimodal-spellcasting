using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace NarutoVR.SealCombat
{
    internal enum ShieldPulseKind { Cast, Impact, Break }

    /// <summary>每个施法者复用一张世界空间符纹，破盾余光独立于常驻护腕。</summary>
    internal sealed class ShieldWristPulse
    {
        private static readonly int Pulse = Shader.PropertyToID("_Pulse");
        private readonly GameObject root;
        private readonly MeshRenderer renderer;
        private readonly MaterialPropertyBlock properties = new();
        private ShieldPulseKind kind;
        private double started;
        private float duration, strength;

        public bool IsPlaying { get; private set; }
        public bool IsBreaking => IsPlaying && kind == ShieldPulseKind.Break;

        private ShieldWristPulse(GameObject root, MeshRenderer renderer)
        {
            this.root = root;
            this.renderer = renderer;
        }

        public static ShieldWristPulse Create(GameObject owner)
        {
            var material = Resources.Load<Material>("ShieldWristPulse");
            var quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            if (!material || !quad) return null;
            var root = new GameObject("护盾反馈 · 度纹", typeof(MeshFilter), typeof(MeshRenderer));
            SceneManager.MoveGameObjectToScene(root, owner.scene);
            root.GetComponent<MeshFilter>().sharedMesh = quad;
            var renderer = root.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.enabled = false;
            return new ShieldWristPulse(root, renderer);
        }

        public void Play(ShieldPulseKind next, float fraction, double now)
        {
            float energy = Mathf.Lerp(.58f, 1f, Mathf.Sqrt(Mathf.Clamp01(fraction)));
            // 同一瞬间的连续命中只提高这一束余光的强度。
            if (next == ShieldPulseKind.Impact && kind == next && IsPlaying && now - started < .055)
            {
                strength = Mathf.Max(strength, energy);
                return;
            }
            kind = next;
            strength = next == ShieldPulseKind.Impact ? energy : 1;
            duration = next switch { ShieldPulseKind.Cast => .46f, ShieldPulseKind.Break => .32f, _ => .22f };
            started = now;
            IsPlaying = true;
        }

        public void UpdatePose(double now, bool tracked, Pose wrist, Transform eye)
        {
            if (!IsPlaying || !renderer) return;
            float age = Mathf.Max(0, (float)(now - started) / duration);
            if (age >= 1) { Stop(); return; }
            Vector3 fromEye = eye ? wrist.position - eye.position : wrist.rotation * Vector3.forward;
            float distance = fromEye.magnitude;
            renderer.enabled = tracked && (!eye || distance > .17f);
            if (!renderer.enabled) return;
            // 随腕移动，正面朝眼。近眼收小并渐隐，中心始终留空。
            Vector3 forward = fromEye.normalized;
            Vector3 up = eye ? eye.up : wrist.rotation * Vector3.up;
            if (Vector3.Cross(up, forward).sqrMagnitude < .001f) up = eye ? eye.right : wrist.rotation * Vector3.right;
            float near = eye ? Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.17f, .32f, distance)) : 1;
            root.transform.SetPositionAndRotation(wrist.position - forward * .012f, Quaternion.LookRotation(forward, up));
            float diameter = kind == ShieldPulseKind.Cast ? .22f : Mathf.Lerp(.18f, .24f, strength);
            root.transform.localScale = Vector3.one * diameter * Mathf.Lerp(.52f, 1, near);
            properties.SetVector(Pulse, new Vector4(age, strength, (float)kind, near));
            renderer.SetPropertyBlock(properties);
        }

        public void Stop()
        {
            IsPlaying = false;
            if (renderer) renderer.enabled = false;
        }

        public void Dispose()
        {
            Stop();
            if (!root) return;
            if (Application.isPlaying) Object.Destroy(root);
            else Object.DestroyImmediate(root);
        }
    }
}
