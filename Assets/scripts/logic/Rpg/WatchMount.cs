using UnityEngine;

namespace NarutoVR.SealCombat.Rpg
{
    [DisallowMultipleComponent]
    public sealed class WatchMount : MonoBehaviour
    {
        public WatchMountProfile profile;
        [HideInInspector] public Quaternion wristToPalm = Quaternion.identity;
        public WatchGazeActivator gaze;

        void OnEnable() => ApplyProfile();
        public void ApplyProfile()
        {
            if (!profile) return;
            transform.SetLocalPositionAndRotation(wristToPalm * profile.offsetMetres,
                wristToPalm * Quaternion.Euler(profile.tiltDegrees));
        }
    }
}
