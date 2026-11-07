using System;
using UnityEngine;

namespace NarutoVR.SealCombat.Rpg
{
    /// <summary>Deterministic hand gesture state, independent of scene objects and Physics.</summary>
    public sealed class RpgGestureState
    {
        readonly RpgGestureSettings settings;
        double lastLeft = double.NegativeInfinity, lastRight = double.NegativeInfinity;
        double readySince = double.NaN, dwellSince = double.NaN;
        double navigationDelta, lastPausedStamp = double.NegativeInfinity;
        Vector3 readyOrigin, leftOrigin, navigationForward = Vector3.forward;
        Vector2 selection;
        bool navigationSpent;
        bool rightStarted, rightSpent, rightMoving, forwardOwned;
        Vector3 rightOrigin, previousRight, rightForward = Vector3.forward;
        double rightSince, rightMotionEnd;
        float rightPath;
        double lowSince = double.NaN, lastLowLeft = double.NegativeInfinity, lastLowRight = double.NegativeInfinity;
        bool lowLatched;
        double sealSeparatedSince = double.NaN, lastSealLeft = double.NegativeInfinity, lastSealRight = double.NegativeInfinity;
        Vector3 separatedLeftOrigin, separatedRightOrigin, ownershipOrigin;
        bool allRightOwned;
        double ownershipReleaseSince = double.NaN;

        public bool SealProtectionActive { get; private set; }

        public bool NavigationActive { get; private set; }
        public bool NavigationNeedsNeutral => NavigationActive && navigationSpent;
        public Vector2 Selection => selection;
        public Vector3 NavigationForward => navigationForward;
        public float DwellProgress { get; private set; }
        public Vector2 NavigationDisplacement { get; private set; }

        public RpgGestureState(RpgGestureSettings configuration = null)
        {
            settings = configuration ?? new RpgGestureSettings();
        }

        public RpgGestureIntent Process(NavigationHandInput frame, Vector3 headForward,
            bool forwardOwnedByHeldSpell, bool blockActions, double now)
        {
            var intent = new RpgGestureIntent();
            bool left = frame.leftValid && Fresh(frame.leftObserved, now) && Finite(frame.leftIndex) && Finite(frame.leftWrist);
            bool right = frame.rightValid && Fresh(frame.rightObserved, now) && Finite(frame.rightWrist);
            ProcessCancel(frame, left, right, ref intent);
            bool releasedProtection = ProcessSealProtection(frame, left, right);

            if (!left || blockActions || SealProtectionActive || !frame.leftPointing || frame.bothHandsLow)
                ClearNavigation();
            else if (frame.leftObserved > lastLeft)
            {
                if (frame.leftObserved - lastLeft > settings.sampleMaxGap) ClearNavigation();
                ProcessNavigation(frame.leftIndex, headForward, frame.leftObserved, ref intent);
            }

            if (!right)
                ClearRight();
            else
            {
                // Gameplay ownership can change between hand samples; it is not a movement observation.
                if (rightStarted) forwardOwned |= forwardOwnedByHeldSpell;
                if (frame.rightMotionOwnedBySpell) { allRightOwned = true; ownershipReleaseSince = double.NaN; }
                if (frame.rightObserved > lastRight)
                {
                    if (frame.rightObserved - lastRight > settings.sampleMaxGap) ClearRight();
                    bool recoveringThrow = RecoverThrowOwnership(frame);
                    if (releasedProtection || SealProtectionActive || recoveringThrow)
                    {
                        // Separation itself is not a swipe. Keep spell ownership even if it fired during protection.
                        BeginRight(frame.rightWrist, headForward, frame.rightObserved, forwardOwned || forwardOwnedByHeldSpell);
                    }
                    else ProcessRight(frame.rightWrist, headForward, frame.rightObserved,
                        frame.rightOpen, forwardOwnedByHeldSpell, blockActions || frame.bothHandsLow, ref intent);
                }
            }
            if (left) lastLeft = Math.Max(lastLeft, frame.leftObserved);
            else lastLeft = double.NegativeInfinity;
            if (right) lastRight = Math.Max(lastRight, frame.rightObserved);
            else lastRight = double.NegativeInfinity;
            return intent;
        }

        bool ProcessSealProtection(NavigationHandInput frame, bool left, bool right)
        {
            // Loss during a seal must not expose a partially occluded hand as a new dodge.
            if (!left || !right || Math.Abs(frame.leftObserved - frame.rightObserved) > settings.sealMaximumSkew)
            { sealSeparatedSince = double.NaN; return false; }
            if (frame.leftObserved <= lastSealLeft || frame.rightObserved <= lastSealRight) return false;
            bool gap = frame.leftObserved - lastSealLeft > settings.sampleMaxGap || frame.rightObserved - lastSealRight > settings.sampleMaxGap;
            lastSealLeft = frame.leftObserved; lastSealRight = frame.rightObserved;
            float distance = Vector3.Distance(frame.leftWrist, frame.rightWrist);
            if (distance <= settings.sealEnterDistance)
            { SealProtectionActive = true; sealSeparatedSince = double.NaN; return false; }
            if (!SealProtectionActive) return false;
            if (gap || distance < Mathf.Max(settings.sealExitDistance, settings.sealEnterDistance + .01f))
            { sealSeparatedSince = double.NaN; return false; }
            double stamp = Math.Min(frame.leftObserved, frame.rightObserved);
            if (double.IsNaN(sealSeparatedSince) || Vector3.Distance(frame.leftWrist, separatedLeftOrigin) > settings.readyStability ||
                Vector3.Distance(frame.rightWrist, separatedRightOrigin) > settings.readyStability)
            { sealSeparatedSince = stamp; separatedLeftOrigin = frame.leftWrist; separatedRightOrigin = frame.rightWrist; }
            if (stamp - sealSeparatedSince < settings.sealReleaseSeconds) return false;
            SealProtectionActive = false;
            sealSeparatedSince = double.NaN;
            return true;
        }

        bool RecoverThrowOwnership(NavigationHandInput frame)
        {
            if (!allRightOwned) return false;
            if (frame.rightMotionOwnedBySpell) return true;
            if (frame.rightObserved - lastRight > settings.sampleMaxGap || double.IsNaN(ownershipReleaseSince) ||
                Vector3.Distance(frame.rightWrist, ownershipOrigin) > settings.readyStability)
            { ownershipReleaseSince = frame.rightObserved; ownershipOrigin = frame.rightWrist; }
            if (frame.rightObserved - ownershipReleaseSince >= settings.sealReleaseSeconds) allRightOwned = false;
            return true; // The rearming observation itself cannot also be a swipe.
        }

        void ProcessNavigation(Vector3 index, Vector3 heading, double stamp, ref RpgGestureIntent intent)
        {
            navigationDelta = double.IsFinite(lastLeft) ? Math.Max(0, stamp - lastLeft) : 0;
            if (!NavigationActive)
            {
                if (double.IsNaN(readySince) || Vector3.Distance(index, readyOrigin) > settings.readyStability)
                {
                    readySince = stamp;
                    readyOrigin = index;
                }
                if (stamp - readySince < settings.readySeconds) return;
                NavigationActive = true;
                leftOrigin = index;
                navigationForward = HorizontalForward(heading, navigationForward);
                navigationSpent = false;
            }
            Vector2 displacement = LocalPlanar(index - leftOrigin, navigationForward);
            NavigationDisplacement = displacement;
            float neutral = Mathf.Min(settings.neutralDistance, settings.directionDistance * 0.8f);
            if (displacement.magnitude <= neutral)
            {
                navigationSpent = false;
                selection = Vector2.zero;
                dwellSince = double.NaN;
                DwellProgress = 0;
                return;
            }
            if (navigationSpent) return;
            Vector2 next = Cardinal(displacement);
            if (selection == Vector2.zero)
            {
                if (displacement.magnitude < settings.directionDistance) return;
                selection = next;
                dwellSince = stamp;
            }
            else if (next != selection && (Vector2.Dot(displacement, selection) <= 0 ||
                     Vector2.Dot(displacement, next) > Vector2.Dot(displacement, selection) * settings.directionHysteresis))
            {
                selection = next;
                dwellSince = stamp;
            }
            DwellProgress = Mathf.Clamp01((float)((stamp - dwellSince) / Math.Max(0.01, settings.dwellSeconds)));
            if (DwellProgress >= 1)
            {
                intent.navigationStep = selection;
                navigationSpent = true;
            }
        }

        void ProcessRight(Vector3 wrist, Vector3 heading, double stamp, bool open, bool owned, bool blocked,
            ref RpgGestureIntent intent)
        {
            if (!rightStarted)
            {
                BeginRight(wrist, heading, stamp, owned);
                return;
            }
            // Ownership survives firing and pose changes until the same movement returns to its origin.
            forwardOwned |= owned;
            Vector3 delta = wrist - rightOrigin;
            if (!rightMoving && !rightSpent) rightForward = HorizontalForward(heading, rightForward);
            Vector2 planar = LocalPlanar(delta, rightForward);
            float neutral = Mathf.Max(settings.neutralDistance, 0.035f);
            if (rightSpent)
            {
                if (delta.magnitude <= neutral) BeginRight(wrist, heading, stamp, owned);
                previousRight = wrist;
                return;
            }
            float segment = Vector3.Distance(wrist, previousRight);
            previousRight = wrist;
            rightPath += segment;
            if (blocked)
            {
                rightSpent = true;
                return;
            }
            if (planar.magnitude >= neutral && Cardinal(planar) == Vector2.up && forwardOwned)
            {
                rightSpent = true;
                return;
            }
            if (!open && !owned)
            {
                // A disappearing spell/pose must not erase an already owned push.
                if (!forwardOwned) BeginRight(wrist, heading, stamp, false);
                return;
            }
            if (!rightMoving)
            {
                if (planar.magnitude < 0.015f) return;
                rightMoving = true;
                rightSince = RpgGestureState.Finite(lastRight) ? lastRight : stamp;
                rightPath = delta.magnitude;
            }
            if (segment > 0.002f) rightMotionEnd = stamp;
            double duration = stamp - rightSince;
            // A bounded window prevents slow drift or a tracking jump from becoming a swipe.
            if (rightPath > settings.swipeMaxPath || Mathf.Abs(delta.y) > settings.swipeMaxVertical || segment > 0.25f)
            {
                rightSpent = true;
                return;
            }
            if (duration > settings.swipeMaxSeconds)
            {
                if (delta.magnitude <= neutral) BeginRight(wrist, heading, stamp, owned);
                else rightSpent = true;
                return;
            }
            if (rightMotionEnd - rightSince < settings.swipeMinSeconds || planar.magnitude < settings.swipeDistance) return;
            rightSpent = true;
            if (planar.magnitude / Mathf.Max(0.001f, rightPath) < settings.swipeEfficiency) return;
            Vector2 direction = Cardinal(planar);
            if (direction == Vector2.up && forwardOwned) return;
            if (!open || (settings.requireNavigationForDodge && !NavigationActive)) return;
            intent.dodgeDirection = ToWorld(direction, rightForward);
        }

        void ProcessCancel(NavigationHandInput frame, bool left, bool right, ref RpgGestureIntent intent)
        {
            if (!left || !right)
            {
                lowSince = double.NaN;
                return;
            }
            if (frame.leftObserved <= lastLowLeft || frame.rightObserved <= lastLowRight) return;
            if (!frame.bothHandsLow)
            {
                lowSince = double.NaN;
                lowLatched = false;
                lastLowLeft = frame.leftObserved;
                lastLowRight = frame.rightObserved;
                return;
            }
            double pairStamp = Math.Min(frame.leftObserved, frame.rightObserved);
            if (frame.leftObserved - lastLowLeft > settings.sampleMaxGap ||
                frame.rightObserved - lastLowRight > settings.sampleMaxGap) lowSince = double.NaN;
            lastLowLeft = frame.leftObserved;
            lastLowRight = frame.rightObserved;
            if (double.IsNaN(lowSince)) lowSince = pairStamp;
            if (!lowLatched && pairStamp - lowSince >= settings.cancelSeconds)
            {
                lowLatched = true;
                intent.cancel = true;
            }
        }

        public void RestartDwell()
        {
            dwellSince = lastLeft;
            DwellProgress = 0;
            navigationSpent = false;
        }

        /// <summary>Keep earned confirmation while a challenger is being checked. Duplicate observations cannot pause twice.</summary>
        public void PauseDwell()
        {
            if (double.IsNaN(dwellSince) || lastLeft <= lastPausedStamp) return;
            dwellSince = Math.Min(lastLeft, dwellSince + navigationDelta);
            DwellProgress = Mathf.Clamp01((float)((lastLeft - dwellSince) / Math.Max(.01, settings.dwellSeconds)));
            navigationSpent = false; lastPausedStamp = lastLeft;
        }

        public void InterruptDwell()
        {
            selection = Vector2.zero;
            dwellSince = double.NaN;
            DwellProgress = 0;
            navigationSpent = true;
        }

        /// <summary>Compensate for moving the tracking origin, keeping gestures relative to physical hand motion.</summary>
        public void TranslateAnchors(Vector3 worldDelta)
        {
            readyOrigin += worldDelta;
            leftOrigin += worldDelta;
            rightOrigin += worldDelta;
            previousRight += worldDelta;
            separatedLeftOrigin += worldDelta;
            separatedRightOrigin += worldDelta;
            ownershipOrigin += worldDelta;
        }

        public void Reset()
        {
            ClearNavigation();
            ClearRight();
            lastLeft = lastRight = lastLowLeft = lastLowRight = double.NegativeInfinity;
            lowSince = double.NaN;
            lowLatched = false;
            SealProtectionActive = false;
            sealSeparatedSince = double.NaN;
            lastSealLeft = lastSealRight = double.NegativeInfinity;
            allRightOwned = false; ownershipReleaseSince = double.NaN;
        }

        void ClearNavigation()
        {
            NavigationActive = false;
            readySince = dwellSince = double.NaN;
            selection = Vector2.zero;
            DwellProgress = 0;
            navigationSpent = false;
            navigationDelta = 0; lastPausedStamp = double.NegativeInfinity; NavigationDisplacement = Vector2.zero;
        }

        void ClearRight()
        {
            rightStarted = rightSpent = rightMoving = forwardOwned = false;
            rightPath = 0;
        }

        void BeginRight(Vector3 wrist, Vector3 heading, double stamp, bool owned)
        {
            rightStarted = true;
            rightSpent = false;
            rightMoving = false;
            forwardOwned = owned;
            rightOrigin = previousRight = wrist;
            rightSince = stamp;
            rightMotionEnd = stamp;
            rightPath = 0;
            rightForward = HorizontalForward(heading, rightForward);
        }

        bool Fresh(double observed, double now) => Finite(observed) && Finite(now) && observed <= now + 0.01 &&
            now - observed <= settings.sampleMaxAge;
        public static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        public static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
        public static Vector3 HorizontalForward(Vector3 heading, Vector3 fallback)
        {
            heading.y = 0;
            if (Finite(heading) && heading.sqrMagnitude > 0.001f) return heading.normalized;
            fallback.y = 0;
            return Finite(fallback) && fallback.sqrMagnitude > 0.001f ? fallback.normalized : Vector3.forward;
        }
        public static Vector2 LocalPlanar(Vector3 delta, Vector3 forward) =>
            new Vector2(Vector3.Dot(delta, Vector3.Cross(Vector3.up, forward)), Vector3.Dot(delta, forward));
        public static Vector3 ToWorld(Vector2 direction, Vector3 forward) =>
            Vector3.Cross(Vector3.up, forward) * direction.x + forward * direction.y;
        public static Vector2 Cardinal(Vector2 delta) => Mathf.Abs(delta.x) > Mathf.Abs(delta.y)
            ? (delta.x > 0 ? Vector2.right : Vector2.left) : (delta.y >= 0 ? Vector2.up : Vector2.down);
    }
}
