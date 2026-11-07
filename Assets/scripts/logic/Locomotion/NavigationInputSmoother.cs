using System;
using UnityEngine;

namespace NarutoVR.SealCombat.Rpg
{
    [Serializable]
    public sealed class NavigationSmoothingSettings
    {
        [Header("左手导航稳定性（不修改结印或投掷骨骼）")]
        [InspectorName("启用自适应平滑")] public bool enabled;
        [InspectorName("静止平滑时间（秒）"), Range(.01f,.08f)] public float stillSeconds = .035f;
        [InspectorName("快速移动平滑时间（秒）"), Range(.002f,.03f)] public float movingSeconds = .008f;
        [InspectorName("快速跟手速度（米/秒）"), Min(.1f)] public float fastSpeed = .5f;
        [InspectorName("射线快速跟手角速度（度/秒）"), Min(30)] public float fastAngularSpeed = 200;
        [InspectorName("原始输入跳变拒绝（米）"), Min(.1f)] public float maximumJump = .25f;
        public bool Valid => Positive(stillSeconds) && stillSeconds <= .08f && Positive(movingSeconds) && movingSeconds <= stillSeconds &&
            Positive(fastSpeed) && Positive(fastAngularSpeed) && Positive(maximumJump);
        static bool Positive(float x) => float.IsFinite(x) && x > 0;
    }

    /// <summary>Only navigation intent is filtered. Raw tracking validity and discontinuities are checked before smoothing.</summary>
    public sealed class NavigationInputSmoother
    {
        bool initialized, rayInitialized;
        double previous = double.NegativeInfinity;
        Vector3 rawPoint, smoothPoint, smoothVelocity, rawRay, smoothRay;
        public float PositionLag { get; private set; }
        public float RayLagDegrees { get; private set; }
        public string Status { get; private set; } = "尚无导航样本";
        public void Reset()
        {
            initialized = rayInitialized = false; previous = double.NegativeInfinity;
            smoothVelocity = Vector3.zero; PositionLag = RayLagDegrees = 0;
            Status = "等待新鲜导航样本";
        }
        public NavigationHandInput Process(NavigationHandInput input, NavigationSmoothingSettings settings, RpgGestureSettings timing, double now)
        {
            if (settings == null || !settings.enabled) { Reset(); Status = "平滑关闭"; return input; }
            bool fresh = double.IsFinite(now) && double.IsFinite(input.leftObserved) && input.leftObserved <= now + .01 &&
                now - input.leftObserved <= timing.sampleMaxAge;
            if (!settings.Valid || !fresh || !input.leftValid || !input.leftPointing || !RpgGestureState.Finite(input.leftIndex))
            { Reset(); if (!settings.Valid) input.leftValid = false; Status = "无效输入，平滑已清空"; return input; }
            if (initialized && (input.leftObserved < previous || input.leftObserved - previous > timing.sampleMaxGap ||
                Vector3.Distance(input.leftIndex, rawPoint) > settings.maximumJump))
            { Reset(); input.leftValid = false; input.leftRayValid = false; Status = "原始样本中断或跳变，重新准备"; return input; }
            bool validRay = input.leftRayValid && RpgGestureState.Finite(input.leftRayDirection) &&
                RpgGestureState.Finite(input.leftRayOrigin) && input.leftRayDirection.sqrMagnitude > .0001f;
            if (!initialized)
            {
                initialized = true; previous = input.leftObserved; rawPoint = smoothPoint = input.leftIndex;
                if (validRay) { rawRay = smoothRay = input.leftRayDirection.normalized; rayInitialized = true; }
            }
            else if (input.leftObserved > previous)
            {
                float dt = (float)(input.leftObserved - previous);
                // Filter the signed velocity, so alternating millimetre noise does not look like deliberate high speed.
                smoothVelocity = Vector3.Lerp(smoothVelocity, (input.leftIndex - rawPoint) / dt, Alpha(dt, .05f));
                float tau = Mathf.Lerp(settings.stillSeconds, settings.movingSeconds, Mathf.Clamp01(smoothVelocity.magnitude / settings.fastSpeed));
                smoothPoint = Vector3.Lerp(smoothPoint, input.leftIndex, Alpha(dt, tau));
                if (validRay)
                {
                    Vector3 ray = input.leftRayDirection.normalized;
                    if (!rayInitialized) smoothRay = ray;
                    else
                    {
                        // Use remaining angular error rather than raw differentiated angular noise.
                        float turnRate = Vector3.Angle(smoothRay, ray) / Mathf.Max(dt, .025f);
                        float rayTau = Mathf.Lerp(settings.stillSeconds, settings.movingSeconds, Mathf.Clamp01(turnRate / settings.fastAngularSpeed));
                        smoothRay = Vector3.Slerp(smoothRay, ray, Alpha(dt, rayTau)).normalized;
                    }
                    rawRay = ray; rayInitialized = true;
                }
                else rayInitialized = false;
                rawPoint = input.leftIndex; previous = input.leftObserved;
            }
            PositionLag = Vector3.Distance(rawPoint, smoothPoint);
            RayLagDegrees = validRay && rayInitialized ? Vector3.Angle(rawRay, smoothRay) : 0;
            input.leftIndex = smoothPoint;
            if (validRay && rayInitialized) { input.leftRayOrigin = smoothPoint; input.leftRayDirection = smoothRay; }
            else input.leftRayValid = false;
            Status = "导航平滑有效";
            return input;
        }
        static float Alpha(float dt, float tau) => 1 - Mathf.Exp(-dt / Mathf.Max(.001f,tau));
    }
}
