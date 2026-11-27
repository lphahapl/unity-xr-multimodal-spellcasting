// Place in Assets/Editor in an isolated project. Run -batchmode -executeMethod StoryCameraPlayChecks.Run.
using System;
using System.IO;
using System.Collections.Generic;
using NarutoVR.SceneArt;
using NarutoVR.SealCombat.Events;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using LegacyDriver = UnityEngine.SpatialTracking.TrackedPoseDriver;

[InitializeOnLoad]
public static class StoryCameraPlayChecks
{
    const string Key = "StoryCameraPlayChecks.Pending";

    static StoryCameraPlayChecks()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Key, false)) return;
            SessionState.EraseBool(Key);
            CheckPlayback();
        };
    }

    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated batch project.");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Key, true);
        EditorApplication.EnterPlaymode();
    }

    static async void CheckPlayback()
    {
        var results = new List<string>();
        void Check(bool passed, string name)
        {
            if (!passed) throw new InvalidOperationException(name);
            results.Add(name);
        }
        int exitCode = 0;
        try
        {
            await Awaitable.NextFrameAsync();
            Application.runInBackground = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var rig = new GameObject("XR origin").transform;
            rig.SetPositionAndRotation(Vector3.right * 2, Quaternion.Euler(0, 25, 0));
            var head = new GameObject("CenterEyeAnchor").AddComponent<Camera>();
            head.transform.SetParent(rig, false);
            head.tag = "MainCamera";
            var legacy = head.gameObject.AddComponent<LegacyDriver>();
            var driver = head.gameObject.AddComponent<TrackedPoseDriver>();
            var device = InputSystem.AddDevice<XRHMD>();
            driver.ignoreTrackingState = true;
            driver.positionInput = new InputActionProperty(new InputAction(type: InputActionType.Value, binding: "<XRHMD>/centerEyePosition"));
            driver.rotationInput = new InputActionProperty(new InputAction(type: InputActionType.Value, binding: "<XRHMD>/centerEyeRotation"));
            void MoveHead(Vector3 position, float yaw)
            {
                InputSystem.QueueDeltaStateEvent(device.centerEyePosition, position);
                InputSystem.QueueDeltaStateEvent(device.centerEyeRotation, Quaternion.Euler(0, yaw, 0));
                InputSystem.Update();
            }
            MoveHead(Vector3.up, 10);
            Check(Vector3.Distance(head.transform.localPosition, Vector3.up) < .001f,
                $"Simulated headset drives the player before the cutscene (device {device.centerEyePosition.ReadValue()}, head {head.transform.localPosition})");

            var guide = new GameObject("Timeline shot output").transform;
            var brain = guide.gameObject.AddComponent<CinemachineBrain>();
            var director = new GameObject("Story sequence").AddComponent<PlayableDirector>();
            var manager = StoryCameraManager.Instance;
            Check(manager && manager.gameObject.scene != director.gameObject.scene, "Manager bootstraps outside the story scene");
            var sequence = director.gameObject.AddComponent<StoryCameraSequence>();
            var fields = new SerializedObject(sequence);
            fields.FindProperty("shotPose").objectReferenceValue = guide;
            fields.FindProperty("playerCamera").objectReferenceValue = head;
            fields.ApplyModifiedPropertiesWithoutUndo();
            var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            var track = timeline.CreateTrack<CinemachineTrack>(null, "Story shots");
            CinemachineCamera Shot(double start, float yaw)
            {
                var cam = new GameObject("Shot " + start).AddComponent<CinemachineCamera>();
                cam.transform.SetPositionAndRotation(new Vector3(4, 2, 0), Quaternion.Euler(0, yaw, 0));
                var clip = track.CreateClip<CinemachineShot>();
                clip.start = start;
                clip.duration = 1;
                ((CinemachineShot)clip.asset).VirtualCamera.defaultValue = cam;
                return cam;
            }
            var first = Shot(0, 20);
            var last = Shot(1, 100);
            director.playableAsset = timeline;
            director.SetGenericBinding(track, brain);
            director.timeUpdateMode = DirectorUpdateMode.Manual;
            director.extrapolationMode = DirectorWrapMode.Hold;
            sequence.Play();
            Check(!legacy.enabled && !driver.enabled, "Both head pose drivers suspended");
            foreach (var shot in new[]{first, last})
            {
                director.time = shot == first ? .5 : 1.5;
                director.Evaluate();
                brain.ManualUpdate();
                await Awaitable.NextFrameAsync();
                await Awaitable.NextFrameAsync();
                MoveHead(new Vector3(-3, 1, 2), -65);
                Check(Vector3.Distance(head.transform.position, shot.transform.position) < .001f &&
                    Quaternion.Angle(head.transform.rotation, shot.transform.rotation) < .1f,
                    $"Shot {director.time:F1} controls world position and rotation through a headset input update");
            }
            sequence.Play();
            Check(director.time >= 1.5 && Camera.main == head, "Duplicate trigger preserves shot time and player camera");
            director.Stop();
            Check(legacy.enabled && driver.enabled && director.extrapolationMode == DirectorWrapMode.Hold,
                "External stop restores both drivers and the Director wrap mode");
            Check(Vector3.Distance(head.transform.localPosition, Vector3.up) < .001f &&
                Quaternion.Angle(head.transform.localRotation, Quaternion.Euler(0, 10, 0)) < .1f,
                "Original local head pose is restored under a transformed rig");
            legacy.enabled = false;
            MoveHead(Vector3.forward * 2, 55);
            Check(Vector3.Distance(head.transform.localPosition, Vector3.forward * 2) < .001f &&
                Quaternion.Angle(head.transform.localRotation, Quaternion.Euler(0, 55, 0)) < .1f,
                "New headset input controls the restored camera");
            sequence.Play();
            manager.enabled = false;
            Check(!legacy.enabled && driver.enabled && !director.playableGraph.IsValid(),
                "Disabling the manager stops playback and preserves an initially disabled driver");
            manager.enabled = true;
            director.timeUpdateMode = DirectorUpdateMode.UnscaledGameTime;
            sequence.Play();
            double timeout = Time.realtimeSinceStartupAsDouble + 8;
            while (!driver.enabled && Time.realtimeSinceStartupAsDouble < timeout)
                await Awaitable.NextFrameAsync();
            Check(driver.enabled && !legacy.enabled, "Natural Timeline completion releases the camera");

            brain.enabled = false;
            var panTimeline = ScriptableObject.CreateInstance<TimelineAsset>();
            var pan = new AnimationClip();
            AnimationUtility.SetEditorCurve(pan, EditorCurveBinding.FloatCurve("", typeof(Transform), "localEulerAnglesRaw.y"),
                AnimationCurve.Linear(0, 0, 1, 90));
            var panTrack = panTimeline.CreateTrack<AnimationTrack>(null, "Fixed camera pan");
            panTrack.trackOffset = TrackOffset.ApplyTransformOffsets;
            panTrack.CreateClip(pan);
            director.playableAsset = panTimeline;
            director.SetGenericBinding(panTrack, guide.gameObject.AddComponent<Animator>());
            director.timeUpdateMode = DirectorUpdateMode.Manual;
            sequence.Play();
            async Awaitable SamplePan(double time)
            {
                director.time = time;
                director.Evaluate();
                await Awaitable.NextFrameAsync();
                await Awaitable.NextFrameAsync();
            }
            await SamplePan(.25);
            var panStart = new Pose(head.transform.position, head.transform.rotation);
            await SamplePan(.75);
            MoveHead(Vector3.down, -30);
            Check(Vector3.Distance(head.transform.position, panStart.position) < .001f &&
                Mathf.Abs(Quaternion.Angle(head.transform.rotation, panStart.rotation) - 45f) < 1f &&
                Quaternion.Angle(guide.rotation, head.transform.rotation) < .1f,
                "A fixed Animation Track turns the player camera 45 degrees without translating while headset input changes");
            manager.Stop();
            Check(driver.enabled && !legacy.enabled, "Explicit skip restores tracking during a pan");
            var blackout = new GameObject("Tracking return cue");
            blackout.SetActive(false);
            fields.Update();
            fields.FindProperty("trackingReturnCue").objectReferenceValue = blackout;
            fields.ApplyModifiedPropertiesWithoutUndo();
            sequence.Play();
            await SamplePan(.25);
            Check(!driver.enabled, "An inactive return cue keeps the authored shot in control");
            blackout.SetActive(true);
            await SamplePan(.5);
            Check(driver.enabled && !legacy.enabled && manager.ActiveDirector == director,
                "An active blackout returns head tracking while the same Director continues");
            MoveHead(new Vector3(.25f, 1.42f, -.16f), -31);
            await SamplePan(.85);
            var returnedPose = new Pose(head.transform.localPosition, head.transform.localRotation);
            Check(Vector3.Distance(returnedPose.position, new Vector3(.25f, 1.42f, -.16f)) < .001f &&
                Quaternion.Angle(returnedPose.rotation, Quaternion.Euler(0, -31, 0)) < .1f,
                "A new headset sample owns the pose during the remaining Timeline");
            sequence.Stop();
            Check(Vector3.Distance(head.transform.localPosition, returnedPose.position) < .001f &&
                Quaternion.Angle(head.transform.localRotation, returnedPose.rotation) < .1f,
                "Stopping after the handoff preserves the new headset pose");
            blackout.SetActive(false);

            var second = new GameObject("Second director, same asset").AddComponent<PlayableDirector>();
            second.playOnAwake = false;
            second.playableAsset = panTimeline;
            second.SetGenericBinding(panTrack, guide.GetComponent<Animator>());
            EventCenter.Publish(TravelEvents.StoryPlayRequested, new StoryPlayRequest(second, guide));
            sequence.Play();
            sequence.Stop();
            Check(manager.ActiveDirector == second && second.state == PlayState.Playing &&
                director.state != PlayState.Playing && !driver.enabled,
                "Shared asset selects the requested director; another sequence cannot interrupt or stop it");
            manager.Stop();
            Check(!manager.ActiveDirector && driver.enabled, "Singleton releases the second director and current MainCamera");
            InputSystem.RemoveDevice(device);
        }
        catch (Exception error)
        {
            results.Add(error.ToString());
            Debug.LogException(error);
            exitCode = 1;
        }
        File.WriteAllLines("Logs/story-camera-checks.txt", results);
        EditorApplication.Exit(exitCode);
    }
}
