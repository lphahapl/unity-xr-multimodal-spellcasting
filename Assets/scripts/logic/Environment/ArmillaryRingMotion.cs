using UnityEngine;

namespace NarutoVR.SceneArt
{
    /// <summary>Only the intact measuring ring moves; its broken frame is a separate fixed mesh.</summary>
    [DisallowMultipleComponent]
    public sealed class ArmillaryRingMotion : MonoBehaviour
    {
        [SerializeField] private Transform intactRing;
        [SerializeField] private Vector3 localAxis = new Vector3(-.7790773f, -.4742685f, .4100099f);
        [SerializeField, Range(-30f, 30f)] private float degreesPerSecond = 7f;
        private Quaternion restRotation;
        private float phase;
        public Transform IntactRing => intactRing;
        public float DegreesPerSecond => degreesPerSecond;

        private void Awake()
        {
            if (intactRing) restRotation = intactRing.localRotation;
        }

        private void Update()
        {
            if (!intactRing || localAxis.sqrMagnitude < .0001f) return;
            phase = Mathf.Repeat(phase + degreesPerSecond * Time.deltaTime, 360f);
            intactRing.localRotation = Quaternion.AngleAxis(phase, localAxis.normalized) * restRotation;
        }
    }
}
