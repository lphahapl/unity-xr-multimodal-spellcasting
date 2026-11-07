#if UNITY_ANDROID && DEVELOPMENT_BUILD && !UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Oculus.Interaction.Input;
using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.XR.OpenXR;

namespace NarutoVR.SealLab
{
    // Runs only in the separately identified development probe, never in a normal player.
    public sealed class SealDeviceDiagnostics : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (Application.identifier != "com.narutovr.handprobe") return;
            var lab = FindFirstObjectByType<SealLab>();
            if (lab && !lab.GetComponent<SealDeviceDiagnostics>())
                lab.gameObject.AddComponent<SealDeviceDiagnostics>();
        }

        private IEnumerator Start()
        {
            var report = new Report { utc = DateTime.UtcNow.ToString("O") };
            var subsystems = new List<XRHandSubsystem>();
            string path = Path.Combine(Application.persistentDataPath, "hand-probe.json");
            bool previousValid = false;
            for (int i = 0; i < 90; i++)
            {
                yield return new WaitForSecondsRealtime(1);
                var lab = GetComponent<SealLab>();
                IHand left = lab.leftTrackedHand as IHand, right = lab.rightTrackedHand as IHand;
                var l = new OVRPlugin.HandState();
                var r = new OVRPlugin.HandState();
                bool lv = OVRPlugin.GetHandState(OVRPlugin.Step.Render, OVRPlugin.Hand.HandLeft, ref l);
                bool rv = OVRPlugin.GetHandState(OVRPlugin.Step.Render, OVRPlugin.Hand.HandRight, ref r);
                subsystems.Clear();
                SubsystemManager.GetSubsystems(subsystems);
                var sample = new Sample
                {
                    time = Time.realtimeSinceStartupAsDouble,
                    focused = OVRManager.hasVrFocus, inputFocus = OVRManager.hasInputFocus,
                    nativeEnabled = OVRPlugin.GetHandTrackingEnabled(),
                    extensionEnabled = OpenXRRuntime.IsExtensionEnabled("XR_EXT_hand_tracking"),
                    nativeLeftValid = lv, nativeRightValid = rv,
                    nativeLeftTracked = lv && (l.Status & OVRPlugin.HandStatus.HandTracked) != 0,
                    nativeRightTracked = rv && (r.Status & OVRPlugin.HandStatus.HandTracked) != 0,
                    leftTracked = left != null && left.IsTrackedDataValid,
                    rightTracked = right != null && right.IsTrackedDataValid,
                    xrSubsystemCount = subsystems.Count
                };
                report.samples.Add(sample);
                File.WriteAllText(path, JsonUtility.ToJson(report, true));
                bool both = sample.leftTracked && sample.rightTracked;
                if (i == 0 || both != previousValid)
                    Debug.Log($"[SealHandProbe] bothTracked={both} nativeEnabled={sample.nativeEnabled} extension={sample.extensionEnabled}");
                previousValid = both;
            }
            Debug.Log("[SealHandProbe] Finite status capture completed.");
            Destroy(this);
        }

        [Serializable] private sealed class Report
        {
            public string utc;
            public List<Sample> samples = new();
        }
        [Serializable] private sealed class Sample
        {
            public double time;
            public bool focused, inputFocus, nativeEnabled, extensionEnabled;
            public bool nativeLeftValid, nativeRightValid, nativeLeftTracked, nativeRightTracked;
            public bool leftTracked, rightTracked;
            public int xrSubsystemCount;
        }
    }
}
#endif
