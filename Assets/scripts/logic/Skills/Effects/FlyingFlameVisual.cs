using UnityEngine;

namespace NarutoVR.SealCombat
{
    /// <summary>Animates the shared rolling flame as a projectile or a forward stream.</summary>
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(MeshRenderer))]
    public sealed class FlyingFlameVisual : MonoBehaviour
    {
        static readonly int Shape = Shader.PropertyToID("_FlameShape");
        [SerializeField] bool sustainedStream;
        [SerializeField, Min(.1f)] float streamLength = 2.25f;
        [SerializeField, Min(.03f)] float streamRadius = .36f;
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
            if (sustainedStream)
                body.localBounds = new Bounds(new Vector3(0, streamRadius * .25f, streamLength * .5f),
                    new Vector3(streamRadius * 5f, streamRadius * 5f, streamLength + 2f * streamRadius));
            SetShape(Application.isPlaying ? 0 : .62f, Application.isPlaying ? 0 : .92f);
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
            if (sustainedStream)
            {
                SetShape(age, 0);
                return;
            }
            float travelled = Mathf.Max(0, -transform.InverseTransformPoint(releasePosition).z);
            float detach = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.32f, .64f, age));
            float tail = Mathf.Lerp(Mathf.Min(travelled, 3.4f), Mathf.Min(travelled, .72f), detach);
            SetShape(age, tail);
        }

        void SetShape(float seconds, float tail)
        {
            float inflate = 1 - Mathf.Exp(-11f * Mathf.Max(0, seconds - .025f));
            float headRadius = Mathf.Lerp(.032f, .50f, inflate);
            body.GetPropertyBlock(properties);
            properties.SetVector(Shape, sustainedStream
                ? new Vector4(seconds, streamRadius, streamLength, 1f)
                : new Vector4(seconds, headRadius, tail, 0f));
            body.SetPropertyBlock(properties);
        }
    }

}
