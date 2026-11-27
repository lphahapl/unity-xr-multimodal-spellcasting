using UnityEngine;

namespace NarutoVR.SealCombat
{
    /// <summary>Shapes the fire at the moving projectile. Its rear point follows the release position until the jet breaks.</summary>
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(MeshRenderer))]
    public sealed class FlyingFlameVisual : MonoBehaviour
    {
        const float LaunchTailLength = 2.62f, FlightTailLength = 1.85f;
        static readonly int Shape = Shader.PropertyToID("_FlameShape");
        MaterialPropertyBlock properties;
        MeshRenderer body;
        Vector3 releasePosition;
        float age;

        void OnEnable()
        {
            body = GetComponent<MeshRenderer>();
            properties ??= new MaterialPropertyBlock();
            releasePosition = transform.position;
            age = 0;
            // The mesh is expanded in the shader. Include the wake in stereo culling.
            body.localBounds = new Bounds(new Vector3(0, .10f, -1.12f), new Vector3(1.9f, 2.0f, 4.26f));
            SetShape(Application.isPlaying ? 0 : .62f, Application.isPlaying ? 0 : FlightTailLength);
        }

        // Seal casts have already shown the ignition at the hand before release.
        internal void LaunchFormed()
        {
            age = .24f;
            SetShape(age, 0);
        }

        void LateUpdate()
        {
            if (!Application.isPlaying) return;
            age += Time.deltaTime;
            float travelled = Mathf.Max(0, -transform.InverseTransformPoint(releasePosition).z);
            float detach = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.32f, .64f, age));
            float tail = Mathf.Min(travelled, Mathf.Lerp(LaunchTailLength, FlightTailLength, detach));
            SetShape(age, tail);
        }

        void SetShape(float seconds, float tail)
        {
            float inflate = 1 - Mathf.Exp(-11f * Mathf.Max(0, seconds - .025f));
            float headRadius = Mathf.Lerp(.032f, .50f, inflate);
            body.GetPropertyBlock(properties);
            properties.SetVector(Shape, new Vector4(seconds, headRadius, tail, 0f));
            body.SetPropertyBlock(properties);
        }
    }

}
