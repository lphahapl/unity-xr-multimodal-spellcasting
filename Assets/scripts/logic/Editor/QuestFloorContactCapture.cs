using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NarutoVR.SealCombat.Items;
using NarutoVR.SealCombat.Rpg;
using Oculus.Interaction;
using Oculus.Interaction.Input;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR;
using UnityEngine.XR.Hands;

namespace NarutoVR.SealCombat.Editor
{
    // One-shot, editor-only capture for the PC OpenXR playtest. The operator's physical
    // floor touch is the external reference; a tracking sample alone cannot establish it.
    [InitializeOnLoad]
    static class QuestFloorContactCapture
    {
        const string ArmFile = "Library/QuestFloorContact.arm";
        const string Menu = "Tools/NarutoVR/诊断/记录下一次头显触地";
        static readonly List<XRInputSubsystem> inputs = new();
        static readonly List<XRHandSubsystem> hands = new();
        static LinearRpgController player;
        static HandVisual[] visuals;
        static StreamWriter output;
        static double nextSample, nextFlush, end;
        static GameItemBase pen;
        static Collider[] penShape;
        static readonly RaycastHit[] pickupHits = new RaycastHit[32];
        static bool lastLeftDown, lastRightDown;

        [Serializable] sealed class PickupHand
        {
            public double observed;
            public bool freshTip, pinchValid, down, up, gripValid, indexRay, wristRay;
            public float pinchMeters;
            public Vector3 tip, origin, direction;
            public string highlighted, gripSightFromTip;
            public bool highlightedPen;
        }

        [Serializable] sealed class Tip
        {
            public bool rawValid, inputValid, visualValid, floorFound, penSurfaceFound;
            public float rawLocalY, rawWorldY, inputWorldY, visualWorldY, floorY;
            public float penGripDistance, penSurfaceDistance;
            public string surface;
        }

        [Serializable] sealed class Sample
        {
            public string utc, scene, runtime, requestedOrigin, actualOrigin, penState;
            public int frame;
            public float trackingSpaceOffsetY, rigY, feetY, eyeY;
            public Tip left, right;
            public bool penBound, canInteract, skillOccupiesHand;
            public string interactionBlock, gripSightFromHead;
            public Vector3 penGrip, penPosition, headPosition;
            public Vector3 rigPosition, feetPosition, supportPoint;
            public bool supportFound, rawCenterEyeValid;
            public Vector3 rawCenterEyeLocal, rawCenterEyeWorld;
            public string supportCollider;
            public float feetAboveSupport;
            public Quaternion penRotation;
            public float pickupRayReach;
            public PickupHand pickupLeft, pickupRight;
        }

        static QuestFloorContactCapture()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode && !Application.isBatchMode && File.Exists(ArmFile))
                    EditorApplication.update += Tick;
                if (state == PlayModeStateChange.ExitingPlayMode) Stop();
            };
            AssemblyReloadEvents.beforeAssemblyReload += Stop;
        }

        [MenuItem(Menu)]
        static void Arm()
        {
            File.WriteAllText(ArmFile, "");
            Debug.Log("触地记录已准备。下次在头显中运行章节时开始采样。");
        }

        static void Stop()
        {
            EditorApplication.update -= Tick;
            output?.Dispose();
            output = null;
            player = null;
            visuals = null;
            pen = null;
            penShape = null;
            nextSample = 0;
            lastLeftDown = lastRightDown = false;
        }

        static void Tick()
        {
            double now = EditorApplication.timeSinceStartup;
            if (output != null && now >= end) { Stop(); return; }
            if (EditorApplication.isPaused) return;
            // Capture a brief pinch between the normal 10 Hz floor measurements.
            if (now < nextSample && !(output != null && player && pen && !pen.Dock && !pen.IsHeld &&
                (player.Input.LeftPickupHand.PinchDown(.03f) != lastLeftDown ||
                 player.Input.LatestHand.PinchDown(.03f) != lastRightDown))) return;
            nextSample = now + (output == null ? .5 : .1);
            try
            {
                SubsystemManager.GetSubsystems(inputs);
                var input = inputs.Find(i => i.running);
                if (input == null) return;
                if (!player || !player.isActiveAndEnabled)
                {
                    player = UnityEngine.Object.FindObjectsByType<LinearRpgController>(FindObjectsSortMode.None)
                        .FirstOrDefault(p => p.isActiveAndEnabled && p.locomotion && p.handTracking && p.handTracking.xrHandsSource);
                    if (!player) return;
                    visuals = player.handTracking.xrHandsSource.GetComponentsInChildren<HandVisual>(true);
                }
                var move = player.locomotion;
                var space = player.handTracking.recordingOrigin;
                if (!space || !move || !move.head || !move.rigRoot || !move.rigRoot.TryGetComponent<XROrigin>(out var rig)) return;
                if (output == null)
                {
                    const string dir = "Logs/QuestFloorContact";
                    Directory.CreateDirectory(dir);
                    var path = Path.Combine(dir, DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + ".jsonl");
                    output = new StreamWriter(path);
                    end = now + 300;
                    File.Delete(ArmFile);
                    Debug.Log("头显触地采样已开始，记录位置 " + Path.GetFullPath(path));
                }
                var glyph = player.GlyphPen;
                if (glyph && glyph.penRoot && (!pen || glyph.penRoot != pen.transform))
                {
                    pen = glyph.penRoot.GetComponent<GameItemBase>();
                    if (pen) penShape = pen.GetComponentsInChildren<Collider>(true);
                }
                SubsystemManager.GetSubsystems(hands);
                var provider = hands.Find(h => h.running);
                var sample = new Sample {
                    utc = DateTime.UtcNow.ToString("O"), frame = Time.frameCount, scene = player.gameObject.scene.path,
                    runtime = UnityEngine.XR.OpenXR.OpenXRRuntime.name, requestedOrigin = rig.RequestedTrackingOriginMode.ToString(),
                    actualOrigin = input.GetTrackingOriginMode().ToString(), penState = !pen ? "unbound" : !pen.isActiveAndEnabled ? "inactive" :
                        pen.IsHeld ? "held" : pen.Dock ? "docked" : "world",
                    trackingSpaceOffsetY = space.position.y - move.rigRoot.position.y, rigY = move.rigRoot.position.y,
                    feetY = move.PlayerFloorPosition.y, eyeY = move.head.position.y,
                    left = ReadTip(true, provider, space), right = ReadTip(false, provider, space)
                };
                var controls = player.Input;
                sample.canInteract = controls.CanInteract(out sample.interactionBlock);
                sample.skillOccupiesHand = player.skills && player.skills.effects && player.skills.effects.HasOccupiedHand;
                sample.pickupRayReach = player.pickupRayReach;
                sample.headPosition = move.head.position;
                var hmd = InputDevices.GetDeviceAtXRNode(XRNode.Head);
                sample.rawCenterEyeValid = hmd.isValid &&
                    hmd.TryGetFeatureValue(UnityEngine.XR.CommonUsages.trackingState, out var trackingState) &&
                    (trackingState & UnityEngine.XR.InputTrackingState.Position) != 0 &&
                    hmd.TryGetFeatureValue(UnityEngine.XR.CommonUsages.centerEyePosition, out sample.rawCenterEyeLocal);
                if (sample.rawCenterEyeValid) sample.rawCenterEyeWorld = space.TransformPoint(sample.rawCenterEyeLocal);
                sample.rigPosition = move.rigRoot.position;
                sample.feetPosition = move.PlayerFloorPosition;
                // Use the movement controller's support query at the head's horizontal projection.
                // The hand probes often sample a neighbouring slab or even the other bank.
                sample.supportFound = move.PreviewNavigationOrigin(out RaycastHit support);
                if (sample.supportFound)
                {
                    sample.supportPoint = support.point;
                    sample.supportCollider = support.collider.name;
                    sample.feetAboveSupport = sample.feetPosition.y - support.point.y;
                }
                sample.penBound = pen;
                if (pen)
                {
                    sample.penGrip = pen.GripPosition;
                    sample.penPosition = pen.transform.position;
                    sample.penRotation = pen.transform.rotation;
                    if (!pen.Dock && !pen.IsHeld) sample.gripSightFromHead = ProbeGrip(sample.headPosition);
                }
                sample.pickupLeft = ReadPickup(true, controls.LeftPickupHand);
                sample.pickupRight = ReadPickup(false, controls.LatestHand);
                lastLeftDown = sample.pickupLeft.down;
                lastRightDown = sample.pickupRight.down;
                output.WriteLine(JsonUtility.ToJson(sample));
                if (now >= nextFlush) { output.Flush(); nextFlush = now + 1; }
            }
            catch (Exception e)
            {
                Stop();
                Debug.LogWarning("触地记录已结束。" + e.Message);
            }
        }

        static PickupHand ReadPickup(bool left, PlayerHandObservation hand)
        {
            var target = player.Input.Pickups.Highlighted(left);
            bool fresh = !hand.tipUnavailable && PlayerHandObservation.Fresh(hand.rightValid,
                hand.rightObserved, Time.realtimeSinceStartupAsDouble, player.handSampleMaxAge);
            return new PickupHand {
                observed = hand.rightObserved, freshTip = fresh, pinchValid = hand.pinchValid,
                down = hand.PinchDown(.03f), up = hand.PinchUp(.045f), pinchMeters = hand.pinchDistance,
                gripValid = hand.gripPoseValid, indexRay = hand.rightIndexRayValid, wristRay = hand.rayValid,
                tip = hand.rightTip, origin = hand.rightIndexRayValid ? hand.rightTip : hand.rayOrigin,
                direction = hand.rightIndexRayValid ? hand.rightIndexDirection : hand.rayDirection,
                highlighted = target ? target.name : null, highlightedPen = target && pen && target == pen.transform,
                gripSightFromTip = fresh && pen && !pen.Dock && !pen.IsHeld ? ProbeGrip(hand.rightTip) : null
            };
        }

        // Independent straight-line observation to the grip centre. The gameplay cone can pick
        // another surface, and its pinch lock may retarget the ray. An empty probe only means
        // this segment hit no external solid; Highlighted above comes from the live pickup query.
        static string ProbeGrip(Vector3 origin)
        {
            var delta = pen.GripPosition - origin;
            float distance = delta.magnitude;
            if (distance <= .002f) return null;
            var scene = player.gameObject.scene.GetPhysicsScene();
            int count = scene.Raycast(origin, delta / distance, pickupHits, distance, ~0, QueryTriggerInteraction.Ignore);
            if (count == pickupHits.Length) return "(hit buffer full)";
            var rig = player.locomotion.rigRoot;
            Collider closest = null;
            float best = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                var hit = pickupHits[i];
                if (!hit.collider || hit.collider.transform.IsChildOf(pen.transform) ||
                    (hit.collider is CharacterController && rig && hit.collider.transform.IsChildOf(rig)) ||
                    hit.distance >= best) continue;
                closest = hit.collider;
                best = hit.distance;
            }
            return closest ? closest.name : null;
        }

        static Tip ReadTip(bool left, XRHandSubsystem provider, Transform space)
        {
            var tip = new Tip();
            var tracker = player.handTracking;
            var source = left ? tracker.xrHandsSource.leftHand : tracker.xrHandsSource.rightHand;
            int index = (int)HandJointId.HandIndexTip;
            Vector3? point = null;
            if (provider != null)
            {
                var xrHand = left ? provider.leftHand : provider.rightHand;
                if (xrHand.isTracked && xrHand.GetJoint(XRHandJointID.IndexTip).TryGetPose(out var pose))
                {
                    point = space.TransformPoint(pose.position);
                    tip.rawValid = true;
                    tip.rawLocalY = pose.position.y;
                    tip.rawWorldY = point.Value.y;
                }
            }
            var snapshot = left ? tracker.Frame?.left : tracker.Frame?.right;
            var now = Time.realtimeSinceStartupAsDouble;
            if (NarutoVR.SealLab.SealLab.IsCurrentHand(snapshot, now, player.handSampleMaxAge) && snapshot.jointValid[index])
            {
                var observation = player.Input.LatestHand;
                if (left || (!observation.tipUnavailable &&
                    PlayerHandObservation.Fresh(observation.rightValid, observation.rightObserved, now, player.handSampleMaxAge)))
                {
                    var pos = left ? space.TransformPoint(snapshot.joints[index].position) : observation.rightTip;
                    point ??= pos;
                    tip.inputValid = true;
                    tip.inputWorldY = pos.y;
                }
            }
            var visual = Array.Find(visuals, v => v && ReferenceEquals(v.Hand, source) && v.isActiveAndEnabled && v.IsVisible);
            if (visual && visual.Joints != null && visual.Joints.Count > index && visual.Joints[index])
            {
                tip.visualValid = true;
                tip.visualWorldY = visual.Joints[index].position.y;
            }
            if (point.HasValue)
            {
                ReadFloor(point.Value, tip);
                if (pen && penShape != null)
                {
                    tip.penGripDistance = Vector3.Distance(point.Value, pen.GripPosition);
                    float best = float.PositiveInfinity;
                    foreach (var part in penShape)
                    {
                        if (!part || !part.enabled || part.isTrigger || !part.gameObject.activeInHierarchy ||
                            part is MeshCollider mesh && !mesh.convex) continue;
                        best = Mathf.Min(best, Vector3.Distance(point.Value, part.ClosestPoint(point.Value)));
                    }
                    tip.penSurfaceFound = float.IsFinite(best);
                    if (tip.penSurfaceFound) tip.penSurfaceDistance = best;
                }
            }
            return tip;
        }

        static void ReadFloor(Vector3 point, Tip tip)
        {
            var move = player.locomotion;
            float reference = move.PlayerFloorPosition.y;
            var start = new Vector3(point.x, reference + move.floorSearchUp, point.z);
            float closest = float.PositiveInfinity;
            foreach (var hit in Physics.RaycastAll(start, Vector3.down, move.floorSearchUp + move.floorSearchDown,
                         move.floorLayers, QueryTriggerInteraction.Ignore))
            {
                float error = Mathf.Abs(hit.point.y - reference);
                if (error >= closest || hit.transform.IsChildOf(move.rigRoot) ||
                    hit.collider.GetComponentInParent<GameItemBase>() || Vector3.Angle(hit.normal, Vector3.up) > move.maximumSlope) continue;
                closest = error;
                tip.floorFound = true;
                tip.floorY = hit.point.y;
                tip.surface = hit.collider.name;
            }
        }
    }
}
