using System;
using UnityEngine;

namespace NarutoVR.Audio
{
    [AddComponentMenu("考工律度/音频/脚步播放器")]
    [DisallowMultipleComponent, DefaultExecutionOrder(500)]
    public sealed class FootstepPlayer : MonoBehaviour
    {
        [InspectorName("脚步音频")] public AudioClip clip;
        [InspectorName("额外变奏（可选）")] public AudioClip[] variations = Array.Empty<AudioClip>();
        [InspectorName("脚步音量"), Range(0,1)] public float volume = .22f;
        [InspectorName("单步间距（米）"), Min(.15f)] public float stepDistance = .55f;
        [InspectorName("计入父物体移动"), Tooltip("挂在行走根时保持关闭，以扣除父平台的平移和旋转。若本物体完全跟随一个行走的父角色，可开启。")]
        public bool includeParentMotion;

        const float FirstStep = .04f;
        const float IdleSeconds = .15f;
        readonly System.Random random = new();
        AudioSource source;
        AudioClip lastClip;
        Collider body;
        Transform previousParent;
        Vector3 previousLocal, previousWorld, lastContact;
        float toStep = FirstStep, idle, gain = 1;
        bool reported, paused;

        public int StepCount { get; private set; }
        public bool HasClips => clip || variations != null && Array.Exists(variations, v => v);
        public event Action<Vector3> Stepped;

        void OnEnable()
        {
            TryGetComponent(out body);
            ResetTracking();
        }

        void LateUpdate()
        {
            if (paused || Time.timeScale <= 0 || AudioListener.pause)
            {
                ResetTracking();
                return;
            }
            if (source && source.isPlaying) source.transform.position = lastContact;
            if (reported) return;
            if (Time.deltaTime <= 0 || previousParent != transform.parent)
            {
                ResetTracking();
                return;
            }
            // Reconstruct last frame's position in the current parent frame. Riding a
            // rotating platform is silent, including when the character stands off its pivot.
            Vector3 from = !includeParentMotion && previousParent ? previousParent.TransformPoint(previousLocal) : previousWorld;
            Vector3 at = transform.position;
            StorePosition();
            float metres = Vector3.ProjectOnPlane(at-from,Vector3.up).magnitude;
            if (!float.IsFinite(metres) || metres > Mathf.Max(.25f, 7f*Time.deltaTime))
            {
                ResetTracking();
                return;
            }
            if (metres < .015f*Time.deltaTime)
            {
                idle += Time.deltaTime;
                // Physics-driven actors can be stationary for a render frame between fixed steps.
                if (idle >= IdleSeconds) toStep = FirstStep;
                return;
            }
            idle = 0;
            if (body && body.enabled && !body.isTrigger) at.y = body.bounds.min.y;
            Accumulate(metres, at, transform.right);
        }

        /// <summary>Use for a controller that can report actual committed footsteps separately from teleports or sliding.</summary>
        public void UseReportedTravel(bool value)
        {
            reported = value;
            ResetTracking();
        }

        /// <summary>Pass zero to end the stride without cutting off a step that already sounded.</summary>
        public void ReportTravel(float metres, Vector3 feet, Vector3 right)
        {
            if (!reported || !isActiveAndEnabled) return;
            if (paused || AudioListener.pause || Time.timeScale <= 0 || !float.IsFinite(metres) || metres <= 0)
            {
                toStep = FirstStep;
                return;
            }
            Accumulate(metres, feet, right);
        }

        void Accumulate(float metres, Vector3 feet, Vector3 right)
        {
            toStep -= metres;
            if (toStep > .00001f) return;
            float spacing = Mathf.Max(.15f, stepDistance);
            toStep = spacing - (Mathf.Max(0,-toStep) % spacing);
            if (volume <= 0) return;
            var next = ChooseClip();
            if (!next) return;
            if (!source)
            {
                var child = new GameObject("脚步声源");
                child.transform.SetParent(transform,false);
                source = child.AddComponent<AudioSource>();
                source.playOnAwake = false; source.loop = false; source.spatialBlend = 1;
                source.priority = 160; source.dopplerLevel = 0; source.minDistance = .7f;
                source.maxDistance = 18; source.rolloffMode = AudioRolloffMode.Linear;
            }
            Vector3 offset = Vector3.ProjectOnPlane(right,Vector3.up).normalized * (StepCount % 2 == 0 ? -.08f : .08f);
            lastContact = feet+offset;
            source.transform.position = lastContact;
            source.volume = volume*gain; source.clip = next; source.Play();
            lastClip = next; ++StepCount; Stepped?.Invoke(lastContact);
        }

        AudioClip ChooseClip()
        {
            AudioClip fallback = clip, pick = null;
            int count = 0;
            if (clip && clip != lastClip) { pick = clip; count = 1; }
            if (variations != null)
                foreach (var v in variations)
                {
                    if (!v || v == clip) continue;
                    if (!fallback) fallback = v;
                    if (v != lastClip && random.Next(++count) == 0) pick = v;
                }
            return pick ? pick : fallback;
        }

        public void SetGain(float value)
        {
            gain = Mathf.Clamp01(value);
            if (source) source.volume = volume*gain;
        }

        void StorePosition()
        {
            previousParent = transform.parent;
            previousLocal = transform.localPosition;
            previousWorld = transform.position;
        }

        /// <summary>Call immediately after an explicit warp, reparent or checkpoint restore, including small repositioning.</summary>
        public void ResetTracking()
        {
            StorePosition(); idle = 0; toStep = FirstStep;
            if (source) source.Stop();
        }

        void OnApplicationPause(bool value) { paused = value; ResetTracking(); }
        void OnDisable() => ResetTracking();
        void OnDestroy()
        {
            if (source) Destroy(source.gameObject);
        }
    }
}
