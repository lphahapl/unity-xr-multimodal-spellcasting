using System;
using UnityEngine;
using UnityEngine.Events;

namespace NarutoVR.SealCombat.Rpg
{
    [DisallowMultipleComponent]
    public sealed class WatchGazeActivator : MonoBehaviour
    {
        public Transform face;
        public Transform viewer;
        public Vector3 localFaceNormal = Vector3.up;
        [Range(-1f,1f)] public float wakeFacing = .60f, sleepFacing = .36f;
        [Range(-1f,1f)] public float wakeLook = .70f, sleepLook = .45f;
        [Min(.01f)] public float wakeDistance = .62f, sleepDistance = .74f;
        [Min(0)] public float wakeDelay = .12f, sleepDelay = .20f;
        public UnityEvent onWake = new(), onSleep = new();
        public event Action<bool> Changed;
        public bool AwakeForInteraction { get; private set; }
        double pendingSince = double.NaN;
        Camera cachedCamera;
        // The wrist controller supplies tracking and samples before its button hit test.
        [NonSerialized] public bool externallySampled;

        void LateUpdate()
        {
            if (externallySampled) return;
            if (!viewer && !cachedCamera) cachedCamera = Camera.main;
            Sample(viewer ? viewer : cachedCamera ? cachedCamera.transform : null, true, Time.unscaledTimeAsDouble);
        }

        public void Sample(Transform eye, bool tracked, double now)
        {
            Transform dial = face ? face : transform;
            if (!tracked || !eye || localFaceNormal.sqrMagnitude < .00001f)
            {
                ResetGaze();
                return;
            }
            Vector3 toEye = eye.position - dial.position;
            float distance = toEye.magnitude;
            Vector3 direction = distance > .0001f ? toEye / distance : Vector3.zero;
            float facing = Vector3.Dot(dial.TransformDirection(localFaceNormal.normalized), direction);
            float looking = Vector3.Dot(eye.forward, -direction);
            bool stay = AwakeForInteraction;
            bool visible = distance > .0001f && distance <= (stay ? Mathf.Max(sleepDistance,wakeDistance) : wakeDistance) &&
                facing >= (stay ? Mathf.Min(sleepFacing,wakeFacing) : wakeFacing) &&
                looking >= (stay ? Mathf.Min(sleepLook,wakeLook) : wakeLook);
            if (visible == stay) { pendingSince = double.NaN; return; }
            if (double.IsNaN(pendingSince) || now < pendingSince) pendingSince = now;
            if (now - pendingSince >= (visible ? wakeDelay : sleepDelay)) SetAwake(visible);
        }

        public void ResetGaze() { pendingSince = double.NaN; SetAwake(false); }
        void OnDisable() => ResetGaze();
        void SetAwake(bool value)
        {
            if (AwakeForInteraction == value) return;
            AwakeForInteraction = value;
            pendingSince = double.NaN;
            if (value) onWake.Invoke(); else onSleep.Invoke();
            Changed?.Invoke(value);
        }
    }
}
