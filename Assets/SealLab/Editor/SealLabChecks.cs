using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NarutoVR.SealLab;
using Oculus.Interaction.Input;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Lab = NarutoVR.SealLab.SealLab;

namespace NarutoVR.Editor
{
    public static class SealLabChecks
    {
        [Serializable] private class Report { public bool passed; public string utc; public string[] checks; public bool hardwareTested; }
        [MenuItem("Tools/NarutoVR/Run Seal Lab Checks")]
        public static void Run()
        {
            var checks = new List<string>();
            var profile = ScriptableObject.CreateInstance<SealProfile>();
            profile.calibrated = true; profile.threshold = 0.8f; profile.minimumCoverage = 0.8f;
            profile.rules.Add(new FeatureRule { featureId = "a", min = 0, max = 1, tolerance = 1, weight = 1 });
            var input = new List<SealFeature> { new("a", "deg", 0.5f, true, true) };
            var competitor = ScriptableObject.CreateInstance<SealProfile>();
            try
            {
                var schedule = new RecordingSchedule(); schedule.Arm(10, 5, 5);
                Check(schedule.Advance(14.99) == RecordingPhase.Preparing, "recording waits through preparation");
                Check(schedule.Advance(15) == RecordingPhase.Capturing, "recording starts at countdown deadline");
                Near((float)schedule.Remaining(17), 3, "recording countdown remaining");
                Check(schedule.Advance(20) == RecordingPhase.Idle, "recording stops without new hand frames");
                schedule.Arm(30, 5, 5); schedule.Cancel();
                Check(schedule.Advance(36) == RecordingPhase.Idle, "cancelled countdown cannot restart");
                schedule.Arm(40, 5, 5);
                Check(schedule.Advance(51) == RecordingPhase.Idle, "missed window does not start unexpected capture");
                bool badTimeRejected = false; try { schedule.Arm(1, double.NaN, 5); } catch (ArgumentException) { badTimeRejected = true; }
                Check(badTimeRejected, "invalid countdown rejected");
                Check(SealScorer.Evaluate(profile, input).passes, "in-range passes");
                input[0] = new SealFeature("a", "deg", 1.5f, true, true);
                Near(SealScorer.Evaluate(profile, input).score, 0.5f, "linear soft tolerance");
                input[0] = new SealFeature("a", "deg", 2, true, true);
                Near(SealScorer.Evaluate(profile, input).score, 0, "tolerance boundary");
                input[0] = new SealFeature("a", "deg", 0.5f, true, true);
                profile.rules.Add(new FeatureRule { featureId = "missing", min = 0, max = 1, tolerance = 1, weight = 3 });
                var missing = SealScorer.Evaluate(profile, input);
                Check(!missing.passes, "missing optional features cannot inflate score");
                Near(missing.score, 0.25f, "missing weight remains in denominator");
                Near(missing.coverage, 0.25f, "coverage uses configured weights");
                profile.rules[0].weight = 9; profile.rules[1].weight = 1; profile.rules[1].required = true;
                Check(!SealScorer.Evaluate(profile, input).passes, "required missing rejects despite 90 percent score/coverage");
                profile.rules.RemoveAt(1); profile.rules[0].weight = 1;
                input[0] = new SealFeature("a", "deg", 0.5f, true, false);
                Check(!SealScorer.Evaluate(profile, input).passes, "low confidence rejects by default");
                profile.allowLowConfidence = true;
                Check(SealScorer.Evaluate(profile, input).passes, "explicit low confidence policy");
                input[0] = new SealFeature { id = "a", value = float.NaN, valid = true, highConfidence = true };
                Check(!SealScorer.Evaluate(profile, input).passes, "nonfinite input rejects");
                profile.rules.Add(new FeatureRule { featureId = "a" });
                Check(!SealScorer.Evaluate(profile, input).passes && profile.ValidateRules() != null, "duplicate IDs reject");
                profile.rules.RemoveAt(1); profile.calibrated = false;
                Check(!SealScorer.Evaluate(profile, input).passes, "uncalibrated profile rejects");

                profile.calibrated = true; profile.holdSeconds = 0.1f;
                competitor.calibrated = true; competitor.holdSeconds = 0.1f;
                competitor.rules.Add(new FeatureRule { featureId = "a", min = 0, max = 1, tolerance = 1, weight = 1 });
                input[0] = new SealFeature("a", "deg", 0.5f, true, true);
                var recognizer = new SealRecognizer(); var candidates = new List<SealProfile> { profile };
                recognizer.Update(candidates, input, 0, true, 0.05f);
                Check(recognizer.State == SealRecognitionState.Holding && !recognizer.Recognized, "live test does not report a single-frame winner as recognized");
                recognizer.Update(candidates, input, 0.09, true, 0.05f);
                Check(!recognizer.Recognized, "live test enforces hold duration");
                recognizer.Update(candidates, input, 0.11, true, 0.05f);
                Check(recognizer.Recognized == profile, "live test returns the confirmed matching seal");
                recognizer.Update(candidates, input, 0.12, false, 0.05f);
                Check(recognizer.State == SealRecognitionState.NoTracking && !recognizer.Recognized, "tracking loss clears stale test result");
                recognizer.Update(candidates, input, 0.13, true, 0.05f);
                Check(recognizer.HoldProgress == 0, "tracking recovery restarts hold duration");
                candidates.Add(competitor);
                recognizer.Update(candidates, input, 0.25, true, 0.05f);
                Check(recognizer.State == SealRecognitionState.Ambiguous && !recognizer.Recognized, "equal passing templates produce ambiguity");
                candidates.Reverse(); recognizer.Update(candidates, input, 0.3, true, 0.05f);
                Check(recognizer.State == SealRecognitionState.Ambiguous, "template ordering cannot resolve an ambiguous gesture");
                input[0] = new SealFeature("a", "deg", 10, true, true);
                recognizer.Update(candidates, input, 0.4, true, 0.05f);
                Check(recognizer.State == SealRecognitionState.Unknown && !recognizer.Recognized, "no threshold match returns unknown rather than highest score");
                profile.calibrated = competitor.calibrated = false;
                recognizer.Update(candidates, input, 0.5, true, 0.05f);
                Check(recognizer.State == SealRecognitionState.NoTemplates, "empty or unconfirmed templates cannot pass live testing");

                var recorded = new List<SealFrame>();
                for (int i = 0; i < 12; ++i)
                {
                    var sample = TestFrame(); sample.label = "寅"; sample.time += i * 0.02;
                    for (int j = 0; j < sample.right.joints.Length; ++j) sample.right.joints[j].position += new Vector3(i * 0.001f, 0, 0);
                    recorded.Add(sample);
                }
                var negative = TestFrame(); negative.label = "none"; recorded.Add(negative);
                var draft = SealTemplateFromRecording.Build(recorded, "寅");
                Check(draft.error == null && draft.acceptedFrames == 12 && draft.matchingFrames == 12 && draft.rules.Count > 0, "recorded positive label produces a multi-frame template draft");
                var distanceRule = draft.rules.Single(r => r.featureId == "Both.WristDistance");
                Check(distanceRule.min > 0.15f && distanceRule.max < 0.161f && distanceRule.min < distanceRule.max, "template ranges use inner percentiles rather than one frame or extremes");
                Check(SealTemplateFromRecording.Build(recorded, "none").error != null, "negative recording label cannot become a positive seal template");
                Check(SealTemplateFromRecording.Build(recorded.Take(9).ToList(), "寅").error != null, "insufficient reference data rejected");
                foreach (var sample in recorded) sample.left.highConfidence = false;
                Check(SealTemplateFromRecording.Build(recorded, "寅").acceptedFrames == 0, "low-confidence recordings are excluded from template statistics");

                var frame = TestFrame(); var extractor = new SealFeatureExtractor(); var values = new List<SealFeature>();
                var xrMask = Enumerable.Repeat(true, Oculus.Interaction.Input.Constants.NUM_HAND_JOINTS).ToArray();
                xrMask[(int)HandJointId.HandIndexTip] = false;
                SealXRHandsSource.ApplyValidity(frame.left, xrMask);
                extractor.Extract(frame, values);
                Check(!frame.left.jointValid[(int)HandJointId.HandIndexTip] && !Get(values, "Both.IndexTipDistance").valid,
                    "XR Hands invalid joint cannot reuse cached SDK position");
                Check(!frame.left.fingerConfidence[(int)HandFinger.Index] && frame.left.fingerConfidence[(int)HandFinger.Middle],
                    "XR Hands derived per-finger quality respects missing joints");
                Check(frame.left.confidenceSource.StartsWith("derived"), "XR Hands quality provenance is explicit");
                frame.left.fresh = false; SealXRHandsSource.ApplyValidity(frame.left, xrMask);
                Check(frame.left.jointValid.All(v => !v) && !frame.left.tracked, "XR Hands stale sample invalidates cached joints");
                Check(SealXRHandsSource.MetaJointIndex(UnityEngine.XR.Hands.XRHandJointID.Wrist) == (int)HandJointId.HandWristRoot &&
                    SealXRHandsSource.MetaJointIndex(UnityEngine.XR.Hands.XRHandJointID.Palm) == (int)HandJointId.HandPalm &&
                    SealXRHandsSource.MetaJointIndex(UnityEngine.XR.Hands.XRHandJointID.IndexTip) == (int)HandJointId.HandIndexTip,
                    "XR Hands wrist palm fingertip mapping matches SDK");
                frame = TestFrame();
                extractor.Extract(frame, values);
                Check(values.Count == 45 && !values.Any(f => f.id == "L.Thumb.Opposition" || f.id == "R.Pinky.Abduction"), "unsupported SDK combinations omitted");
                float distance = Get(values, "Both.WristDistance").value;
                float angle = Get(values, "Both.PalmAngle").value;
                float offset = Get(values, "Both.RightWristInLeft.X").value;
                var rotation = Quaternion.Euler(37, 81, 14); var translation = new Vector3(4, -2, 3);
                foreach (var hand in new[] { frame.left, frame.right })
                for (int i = 0; i < hand.joints.Length; ++i)
                    hand.joints[i] = new Pose(rotation * hand.joints[i].position + translation, rotation * hand.joints[i].rotation);
                extractor.Extract(frame, values);
                Near(Get(values, "Both.WristDistance").value, distance, "distance invariant to global pose");
                Near(Get(values, "Both.PalmAngle").value, angle, "palm angle invariant to global pose", 0.01f);
                Near(Get(values, "Both.RightWristInLeft.X").value, offset, "wrist-relative offset invariant");
                frame.right.observedAt += 0.2;
                extractor.Extract(frame, values);
                Check(values.Where(f => f.id.StartsWith("Both.")).All(f => !f.valid), "asynchronous hands rejected");
                frame.right.observedAt = frame.left.observedAt; frame.left.fresh = false;
                extractor.Extract(frame, values);
                Check(values.Where(f => f.id.StartsWith("Both.") || f.id.StartsWith("L.")).All(f => !f.valid), "stale hand invalidates dependent features");

                Directory.CreateDirectory("Logs/SealLabChecks");
                using var recording = new SealRecording();
                recording.Start("Logs/SealLabChecks"); frame = TestFrame();
                frame.source = "XR Hands test fixture"; frame.left.confidenceSource = "derived test fixture";
                recording.Write(frame); recording.Dispose();
                var replay = SealRecording.Read(recording.Path);
                Check(replay.Count == 1 && replay[0].label == "synthetic-test-only", "raw recording round trip");
                Check(replay[0].source == frame.source && replay[0].left.confidenceSource == frame.left.confidenceSource,
                    "recording preserves input and quality provenance");
                var a = new List<SealFeature>(); var b = new List<SealFeature>(); extractor.Extract(frame, a); extractor.Extract(replay[0], b);
                Check(a.Count == b.Count && a.Zip(b, (x, y) => x.valid == y.valid && Mathf.Abs(x.value - y.value) < 0.0001f).All(x => x), "replay recomputes identical SDK and geometry features");
                frame.left.joints[(int)HandJointId.HandIndexTip].position = new Vector3(float.NaN, 0, 0);
                string badPath = "Logs/SealLabChecks/nonfinite.jsonl"; File.WriteAllText(badPath, JsonUtility.ToJson(frame));
                bool rejected = false; try { SealRecording.Read(badPath); } catch (InvalidDataException) { rejected = true; }
                Check(rejected, "malformed raw pose rejected");
                ValidateScene(checks);
                File.WriteAllText("Logs/seal-lab-checks.json", JsonUtility.ToJson(new Report { passed = true, utc = DateTime.UtcNow.ToString("O"), checks = checks.ToArray(), hardwareTested = false }, true));
                Debug.Log($"[SealLab] {checks.Count} deterministic checks passed. Synthetic fixtures do not validate device tracking.");
            }
            finally { UnityEngine.Object.DestroyImmediate(profile); UnityEngine.Object.DestroyImmediate(competitor); }
            void Check(bool condition, string name) { if (!condition) throw new Exception("SealLab check failed: " + name); checks.Add(name); }
            void Near(float actual, float expected, string name, float tolerance = 0.0001f) => Check(float.IsFinite(actual) && Mathf.Abs(actual - expected) <= tolerance, name);
        }
        private static SealFeature Get(List<SealFeature> features, string id) => features.Single(f => f.id == id);
        private static SealFrame TestFrame()
        {
            var frame = new SealFrame { time = 10, label = "synthetic-test-only" };
            for (int side = 0; side < 2; ++side)
            {
                var hand = side == 0 ? frame.left : frame.right;
                hand.connected = hand.tracked = hand.highConfidence = hand.fresh = hand.wristPosesValid = true;
                hand.observedAt = 10; hand.version = 4;
                Vector3 wrist = new Vector3(side * 0.15f, 0, 0);
                for (int i = 0; i < hand.joints.Length; ++i)
                { hand.jointValid[i] = true; hand.joints[i] = new Pose(wrist, Quaternion.identity); hand.wristPoses[i] = Pose.identity; }
                for (int f = 0; f < 5; ++f)
                {
                    hand.fingerConfidence[f] = true; var chain = HandJointUtils.FingerToJointList[f];
                    for (int j = 0; j < chain.Length; ++j)
                    {
                        var p = new Vector3((f - 2) * 0.02f * (side == 0 ? 1 : -1), 0.04f + j * 0.02f, 0.004f * f);
                        hand.wristPoses[(int)chain[j]] = new Pose(p, Quaternion.identity);
                        hand.joints[(int)chain[j]] = new Pose(wrist + p, Quaternion.identity);
                    }
                }
            }
            return frame;
        }
        private static void ValidateScene(List<string> checks)
        {
            var scene = SceneManager.GetSceneByPath(SealLabBuilder.ScenePath);
            bool alreadyOpen = scene.IsValid() && scene.isLoaded;
            if (!alreadyOpen) scene = EditorSceneManager.OpenScene(SealLabBuilder.ScenePath, OpenSceneMode.Additive);
            try
            {
                var lab = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Lab>(true)).Single();
                if (lab.leftTrackedHand.GetType() != typeof(Hand) || lab.rightTrackedHand.GetType() != typeof(Hand) ||
                    !lab.recordingOrigin || lab.profiles.Count != 12)
                    throw new Exception("Seal Lab scene references or initial template state invalid");
                foreach (var raw in new[] { lab.leftTrackedHand, lab.rightTrackedHand })
                {
                    var serialized = new SerializedObject(raw);
                    if (serialized.FindProperty("_iModifyDataFromSourceMono").objectReferenceValue != raw.GetComponent<FromOVRHandDataSource>())
                        throw new Exception("Recognition hand must branch directly from FromOVRHandDataSource");
                }
                foreach (var source in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<FromOVRHandDataSource>(true)))
                {
                    var serialized = new SerializedObject(source);
                    if (!serialized.FindProperty("_cameraRigRef").objectReferenceValue || !serialized.FindProperty("_trackingToWorldTransformer").objectReferenceValue)
                        throw new Exception("Unwired actual OVR data source");
                }
                checks.Add("scene actual IHand sources wired before LastKnownGoodHand; 12 profiles; origin present");
            }
            finally { if (!alreadyOpen) EditorSceneManager.CloseScene(scene, true); }
        }
    }
}
