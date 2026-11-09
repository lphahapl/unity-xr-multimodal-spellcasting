using UnityEngine;
using NarutoVR.SealCombat.Rpg;

namespace NarutoVR.EnvironmentAudio
{
    /// <summary>One spatial canal voice, positioned on the nearest point of a local polyline.</summary>
    [DisallowMultipleComponent]
    public sealed class WaterAudio : MonoBehaviour
    {
        [SerializeField] private AudioSource waterSource;
        [SerializeField] private AudioListener listener;
        [SerializeField] private P1AudioManager duckingCoordinator;
        [SerializeField, Range(0f, 1f)] private float volume = 0.19f;
        [SerializeField] private Vector3[] channelPoints =
        {
            new Vector3(.539f, .13f, 49.4f),
            new Vector3(.539f, .13f, 60.9f)
        };

        private float nextSearch;
        private float nextPosition;
        private float nextCoordinatorSearch;
        private float fade;
        private Vector3 targetPosition;

        private void OnEnable()
        {
            fade = 0f;
            nextSearch = nextPosition = nextCoordinatorSearch = 0f;
            if (!waterSource || !waterSource.clip) return;
            waterSource.volume = 0f;
            // The moving emitter represents a continuous bed, not an object travelling in water.
            waterSource.dopplerLevel = 0f;
            waterSource.loop = true;
            waterSource.spatialBlend = 1f;
            UpdatePosition(true);
            waterSource.Play();
        }

        private void Update()
        {
            if (!waterSource) return;
            UpdatePosition(false);
            waterSource.transform.position = Vector3.Lerp(waterSource.transform.position,
                targetPosition, 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime));
            fade = Mathf.MoveTowards(fade, 1f, Time.unscaledDeltaTime / .75f);
            // Read the existing speech duck envelope; never create another microphone consumer.
            if (!duckingCoordinator && Time.unscaledTime >= nextCoordinatorSearch)
            {
                nextCoordinatorSearch = Time.unscaledTime + 1f;
                duckingCoordinator = FindFirstObjectByType<P1AudioManager>();
            }
            float duck = duckingCoordinator && duckingCoordinator.isActiveAndEnabled
                ? duckingCoordinator.DuckGain : 1f;
            waterSource.volume = volume * fade * duck;
        }

        private void UpdatePosition(bool snap)
        {
            if ((!listener || !listener.isActiveAndEnabled) && Time.unscaledTime >= nextSearch)
            {
                nextSearch = Time.unscaledTime + 1f;
                listener = null;
                foreach (var candidate in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
                {
                    if (!candidate.isActiveAndEnabled) continue;
                    listener = candidate;
                    break;
                }
            }
            if (!snap && Time.unscaledTime < nextPosition) return;
            nextPosition = Time.unscaledTime + .1f;
            Vector3 ear = listener && listener.isActiveAndEnabled ? listener.transform.position : transform.position;
            targetPosition = transform.position;
            float best = float.PositiveInfinity;
            if (channelPoints != null && channelPoints.Length == 1)
                targetPosition = transform.TransformPoint(channelPoints[0]);
            if (channelPoints != null)
                for (int i = 1; i < channelPoints.Length; ++i)
                {
                    Vector3 a = transform.TransformPoint(channelPoints[i - 1]);
                    Vector3 d = transform.TransformPoint(channelPoints[i]) - a;
                    float t = Mathf.Clamp01(Vector3.Dot(ear - a, d) / Mathf.Max(d.sqrMagnitude, .0001f));
                    Vector3 point = a + t * d;
                    float distance = (ear - point).sqrMagnitude;
                    if (distance >= best) continue;
                    best = distance;
                    targetPosition = point;
                }
            if (snap && waterSource) waterSource.transform.position = targetPosition;
        }

        private void OnDisable()
        {
            if (!waterSource) return;
            waterSource.Stop();
            waterSource.volume = 0f;
        }
    }
}
