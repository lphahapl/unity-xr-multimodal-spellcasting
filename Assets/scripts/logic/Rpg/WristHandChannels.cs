using System;
using Oculus.Interaction.Input;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.XR.Hands;
using UnityEngine.XR.OpenXR.Features.Interactions;

namespace NarutoVR.SealCombat.Rpg
{
    // The caller supplies independent joint-based defaults. Override a channel only on positive
    // evidence that this runtime currently provides it. UI targets never depend on vendor types.
    internal static class WristHandChannels
    {
        internal static void Apply(ref WristMenuInput f, Transform origin, Vector3 wrist,
            WristMenuSettings settings, bool xrHands, IHand native)
        {
            if (!xrHands)
            {
                if (native == null || !native.IsConnected || !native.IsTrackedDataValid) return;
                if (native.IsPointerPoseValid && native.GetPointerPose(out var pointer))
                    SetRay(ref f,pointer,wrist,"Meta·系统射线");
                if (native.GetFingerIsHighConfidence(HandFinger.Index) && native.GetFingerIsHighConfidence(HandFinger.Thumb))
                    SetPinch(ref f,native.GetIndexFingerIsPinching(),false,"Meta·系统捏合");
                return;
            }

            // EXT_hand_interaction is optional. A device must be a right hand, not a controller.
            foreach (var device in InputSystem.devices)
            {
                if (!(device is HandInteractionProfile.HandInteraction hand) || !hand.enabled ||
                    !hand.usages.Contains(UnityEngine.InputSystem.CommonUsages.RightHand) ||
                    !Fresh(hand,f.rightObserved,settings.maxAge)) continue;
                var pose=hand.pointer.ReadValue();
                var poseFields=UnityEngine.XR.InputTrackingState.Position|UnityEngine.XR.InputTrackingState.Rotation;
                if (pose.isTracked && (pose.trackingState & poseFields)==poseFields)
                    SetRay(ref f,WorldPose(origin,pose.position,pose.rotation),wrist,"OpenXR EXT·系统射线");
                if (hand.pinchReady.isPressed)
                {
                    float strength=hand.pinchValue.ReadValue();
                    if(float.IsFinite(strength) && strength>=0 && strength<=1)
                        SetPinch(ref f,strength>=.75f,strength>.35f && strength<.75f,"OpenXR EXT·捏合强度");
                }
                break;
            }

            // FB aim flags distinguish "open" from a device that never provided pinch data.
            var aim=MetaAimHand.right;
            if (aim==null || !aim.added || !aim.enabled || !Fresh(aim,f.rightObserved,settings.maxAge)) return;
            var flags=(MetaAimFlags)aim.aimFlags.ReadValue();
            if ((flags & MetaAimFlags.Computed)==0) return;
            SetPinch(ref f,(flags & MetaAimFlags.IndexPinching)!=0,false,"OpenXR FB·系统捏合");
            if ((flags & MetaAimFlags.Valid)!=0 && aim.isTracked.isPressed)
                SetRay(ref f,WorldPose(origin,aim.devicePosition.ReadValue(),aim.deviceRotation.ReadValue()),wrist,"OpenXR FB·系统射线");
        }

        static bool Fresh(InputDevice device,double observed,double maxAge) =>
            Math.Abs(device.lastUpdateTime-observed)<=maxAge;

        static Pose WorldPose(Transform origin,Vector3 position,Quaternion rotation) =>
            origin ? new Pose(origin.TransformPoint(position),origin.rotation*rotation) : new Pose(position,rotation);

        static void SetPinch(ref WristMenuInput f,bool pressed,bool transition,string source)
        {
            f.systemPinch=true;
            f.systemPressed=pressed;
            f.pinchTransition=transition;
            f.pinchValid=true;
            f.pinchSource=source;
        }
        static void SetRay(ref WristMenuInput f,Pose aim,Vector3 wrist,string source)
        {
            if (!LinearRpgController.PointerPoseNearHand(aim,wrist)) return;
            f.rayOrigin=aim.position;
            f.rayDirection=aim.rotation*Vector3.forward;
            f.rayValid=true;
            f.raySource=source;
        }
    }
}
