using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Oculus.Interaction.Input;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Hands;
using Lab = NarutoVR.SealLab.SealLab;

namespace NarutoVR.Editor
{
    // Opt-in, finite capture of status flags only; no joint poses or device identifiers.
    [InitializeOnLoad]
    public static class SealTrackingDiagnostics
    {
        private const string RequestPath = "Logs/seal-tracking-diagnostics.request";
        private const string ReportPath = "Logs/seal-tracking-diagnostics.json";
        private static double nextPoll;
        private static double captureUntil;
        private static readonly List<Sample> samples = new();
        static SealTrackingDiagnostics() => EditorApplication.update += Tick;

        [MenuItem("Tools/NarutoVR/Capture Tracking Diagnostics")]
        public static void RequestCapture()
        {
            Directory.CreateDirectory("Logs");
            File.WriteAllText(RequestPath, "30 seconds of tracking status only");
        }

        private static void Tick()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now < nextPoll || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            nextPoll = now + 1;
            if (File.Exists(RequestPath) && Application.isPlaying)
            {
                File.Delete(RequestPath);
                samples.Clear();
                captureUntil = now + 30;
            }
            if (captureUntil == 0) return;
            if (!Application.isPlaying || now > captureUntil)
            {
                captureUntil = 0;
                return;
            }
            try
            {
                var lab = UnityEngine.Object.FindFirstObjectByType<Lab>();
                var subsystems = new List<XRHandSubsystem>();
                SubsystemManager.GetSubsystems(subsystems);
                var sample = new Sample
                {
                    time = Time.realtimeSinceStartupAsDouble,
                    applicationFocused = Application.isFocused,
                    runInBackground = Application.runInBackground,
                    hmdPresent = OVRManager.isHmdPresent,
                    vrFocus = OVRManager.hasVrFocus,
                    inputFocus = OVRManager.hasInputFocus,
                    nativeHandTrackingEnabled = OVRPlugin.GetHandTrackingEnabled(),
                    connectedControllers = OVRPlugin.GetConnectedControllers().ToString(),
                    nativeSkeleton = OVRPlugin.HandSkeletonVersion.ToString(),
                    configuredSkeleton = OVRRuntimeSettings.GetRuntimeSettings().HandSkeletonVersion.ToString(),
                    nativeLeft = ReadNative(OVRPlugin.Hand.HandLeft),
                    nativeRight = ReadNative(OVRPlugin.Hand.HandRight),
                    ovrHands = UnityEngine.Object.FindObjectsByType<OVRHand>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                        .Select(h => new HandState
                        {
                            name = h.name, active = h.isActiveAndEnabled,
                            dataValid = h.IsDataValid, tracked = h.IsTracked,
                            highConfidence = h.IsDataHighConfidence, scale = h.HandScale
                        }).ToArray(),
                    left = ReadHand(lab ? lab.leftTrackedHand as IHand : null),
                    right = ReadHand(lab ? lab.rightTrackedHand as IHand : null),
                    xrHands = subsystems.Select(s => new XrHands
                    {
                        running = s.running, leftTracked = s.leftHand.isTracked,
                        rightTracked = s.rightHand.isTracked
                    }).ToArray()
                };
                samples.Add(sample);
                File.WriteAllText(ReportPath, JsonUtility.ToJson(new Report { samples = samples.ToArray() }, true));
            }
            catch (Exception ex)
            {
                captureUntil = 0;
                File.WriteAllText(ReportPath, JsonUtility.ToJson(new Report { error = ex.Message }, true));
            }
        }

        private static HandState ReadNative(OVRPlugin.Hand hand)
        {
            var data = new OVRPlugin.HandState();
            bool valid = OVRPlugin.GetHandState(OVRPlugin.Step.Render, hand, ref data);
            return new HandState
            {
                name = hand.ToString(), dataValid = valid,
                tracked = valid && (data.Status & OVRPlugin.HandStatus.HandTracked) != 0,
                highConfidence = valid && data.HandConfidence == OVRPlugin.TrackingConfidence.High,
                scale = data.HandScale
            };
        }

        private static HandState ReadHand(IHand hand) => new()
        {
            active = hand != null, dataValid = hand != null && hand.IsConnected,
            tracked = hand != null && hand.IsTrackedDataValid,
            highConfidence = hand != null && hand.IsHighConfidence,
            scale = hand == null ? 0 : hand.Scale
        };

        [Serializable] private class Report { public string error; public Sample[] samples; }
        [Serializable] private class Sample
        {
            public double time;
            public bool applicationFocused, runInBackground, hmdPresent, vrFocus, inputFocus, nativeHandTrackingEnabled;
            public string connectedControllers, nativeSkeleton, configuredSkeleton;
            public HandState nativeLeft, nativeRight, left, right;
            public HandState[] ovrHands;
            public XrHands[] xrHands;
        }
        [Serializable] private class HandState
        {
            public string name;
            public bool active, dataValid, tracked, highConfidence;
            public float scale;
        }
        [Serializable] private class XrHands { public bool running, leftTracked, rightTracked; }
    }
}
