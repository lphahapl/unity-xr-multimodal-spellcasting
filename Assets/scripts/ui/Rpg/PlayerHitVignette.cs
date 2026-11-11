using UnityEngine;
using UnityEngine.Rendering;

namespace NarutoVR.SealCombat.Rpg
{
    [DisallowMultipleComponent, AddComponentMenu("NarutoVR/反馈/玩家受击视野边缘")]
    public sealed class PlayerHitVignette : MonoBehaviour
    {
        const float Rise = .07f, HoldEnd = .16f, Duration = .58f, RearmInterval = .72f;
        static readonly int StrengthId = Shader.PropertyToID("_Strength");
        public float CurrentStrength { get; private set; }
        public int PulseCount { get; private set; }

        LinearRpgController player;
        PlayerObj entity;
        Camera view;
        MeshRenderer border;
        Mesh mesh;
        MaterialPropertyBlock properties;
        double began = double.NegativeInfinity;
        float peak;
        bool pulsing;

        void OnEnable()
        {
            player = GetComponent<LinearRpgController>();
            if (!player) return;
            entity = GetComponent<PlayerObj>();
            if (entity) entity.DamageSettled += OnDamage;
            player.CheckpointRestored += Clear;
        }

        float Intensity => player.playerDefinition ? Mathf.Clamp01(player.playerDefinition.hitVignetteIntensity) : 1f;

        void OnDamage(PlayerDamageResult damage)
        {
            if (!player.CanAct || Intensity <= 0) { Clear(); return; }
            if (damage.HealthLost <= 0) return;
            float severity = Mathf.Clamp01(damage.HealthLost / (player.Player.MaxHealth * .25f));
            float strength = Mathf.Lerp(.65f, 1, severity);
            double now = Time.unscaledTimeAsDouble;
            if (now - began < RearmInterval)
            {
                // 合并至当前一轮，既不从头闪烁，也不延长消退截止时间。
                if (pulsing) peak = Mathf.Max(peak, strength);
                return;
            }
            if (!EnsureBorder()) return;
            if (!pulsing)
            {
                RenderPipelineManager.beginCameraRendering += BeginCamera;
                RenderPipelineManager.endCameraRendering += EndCamera;
            }
            began = now;
            peak = strength;
            pulsing = true;
            PulseCount++;
            SetStrength(0);
        }

        void LateUpdate()
        {
            if (!pulsing) return;
            if (!player || !player.CanAct || !view || !view.isActiveAndEnabled || !border || Intensity <= 0)
            { Clear(); return; }
            float age = (float)(Time.unscaledTimeAsDouble - began);
            if (age >= Duration) { EndPulse(); return; }
            float envelope = age < Rise ? Mathf.SmoothStep(0, 1, age / Rise) :
                1f - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(HoldEnd, Duration, age));
            SetStrength(peak * Intensity * envelope);
        }

        void SetStrength(float value)
        {
            CurrentStrength = value;
            properties.SetFloat(StrengthId, value);
            border.SetPropertyBlock(properties);
        }

        void BeginCamera(ScriptableRenderContext context, Camera camera)
        {
            if (border) border.enabled = camera == view && CurrentStrength > 0 && player && player.CanAct;
        }
        void EndCamera(ScriptableRenderContext context, Camera camera)
        {
            if (border && camera == view) border.enabled = false;
        }

        bool EnsureBorder()
        {
            if (!player.locomotion || !player.locomotion.head ||
                !player.locomotion.head.TryGetComponent(out Camera headCamera)) return false;
            if (border && headCamera == view) return true;
            var material = Resources.Load<Material>("PlayerHitVignette");
            if (!material) return false;
            ReleaseBorder();
            view = headCamera;
            var surface = new GameObject("玩家受击·视野边缘");
            surface.transform.SetParent(view.transform, false);
            surface.transform.localPosition = Vector3.forward * Mathf.Max(1f, view.nearClipPlane + .1f);
            for (int layer = 0; layer < 32; layer++)
                if ((view.cullingMask & (1 << layer)) != 0) { surface.layer = layer; break; }
            var filter = surface.AddComponent<MeshFilter>();
            mesh = MakeBorderMesh();
            filter.sharedMesh = mesh;
            border = surface.AddComponent<MeshRenderer>();
            border.enabled = false;
            border.sharedMaterial = material;
            border.shadowCastingMode = ShadowCastingMode.Off;
            border.receiveShadows = false;
            border.lightProbeUsage = LightProbeUsage.Off;
            border.reflectionProbeUsage = ReflectionProbeUsage.Off;
            border.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            border.allowOcclusionWhenDynamic = false;
            properties ??= new MaterialPropertyBlock();
            return true;
        }

        static Mesh MakeBorderMesh()
        {
            var corners = new[] { new Vector3(-1,-1,0), new Vector3(1,-1,0), new Vector3(1,1,0), new Vector3(-1,1,0) };
            var vertices = new Vector3[8];
            var indices = new int[24];
            for (int i = 0; i < 4; i++)
            {
                vertices[i] = corners[i];
                vertices[i + 4] = corners[i] * .46f;
                int next = (i + 1) % 4, offset = i * 6;
                indices[offset] = i; indices[offset + 1] = next; indices[offset + 2] = next + 4;
                indices[offset + 3] = i; indices[offset + 4] = next + 4; indices[offset + 5] = i + 4;
            }
            var ring = new Mesh { name = "玩家受击·双眼边框", vertices = vertices, triangles = indices };
            ring.UploadMeshData(true);
            return ring;
        }

        void EndPulse()
        {
            if (pulsing)
            {
                RenderPipelineManager.beginCameraRendering -= BeginCamera;
                RenderPipelineManager.endCameraRendering -= EndCamera;
            }
            pulsing = false;
            peak = CurrentStrength = 0;
            if (border) border.enabled = false;
        }
        void Clear() { EndPulse(); began = double.NegativeInfinity; }
        void OnApplicationPause(bool paused) { if (paused) Clear(); }

        void ReleaseBorder()
        {
            if (border) { border.enabled = false; Destroy(border.gameObject); }
            if (mesh) Destroy(mesh);
            border = null; mesh = null; view = null;
        }
        void OnDisable()
        {
            Clear();
            if (entity) entity.DamageSettled -= OnDamage;
            if (player) player.CheckpointRestored -= Clear;
            ReleaseBorder();
        }
    }
}
