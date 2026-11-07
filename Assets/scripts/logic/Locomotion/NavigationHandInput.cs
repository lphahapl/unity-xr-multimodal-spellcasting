using System;
using UnityEngine;

namespace NarutoVR.SealCombat.Rpg
{
    /// <summary>World-space hand samples. Observed timestamps use the same clock as ProcessInput's now.</summary>
    public struct NavigationHandInput
    {
        public double leftObserved, rightObserved;
        public bool leftValid, rightValid;
        public Vector3 leftIndex, leftWrist, rightWrist;
        // Optional world-space index ray from the same valid left-hand observation; never a movement gate.
        public bool leftRayValid;
        public Vector3 leftRayOrigin, leftRayDirection;
        public bool leftPointing, rightOpen, bothHandsLow;
        public bool rightMotionOwnedBySpell;
    }

    [Serializable]
    public sealed class RpgGestureSettings
    {
        [Header("闪避启用条件")]
        [InspectorName("仅导航准备时允许右掌闪避"), Tooltip("兼容旧操作。默认关闭：右掌可独立短划闪避，双手结印保护仍然生效。")]
        public bool requireNavigationForDodge = false;
        [Header("双手结印保护（与手形识别独立）")]
        [InspectorName("双腕靠近保护距离（米）"), Min(.05f)] public float sealEnterDistance = .26f;
        [InspectorName("双腕分开解除距离（米）"), Min(.06f)] public float sealExitDistance = .34f;
        [InspectorName("分开后稳定时间（秒）"), Min(.05f)] public float sealReleaseSeconds = .18f;
        [InspectorName("双手最大时间差（秒）"), Min(.01f)] public float sealMaximumSkew = .08f;
        [Header("左食指四向选点")]
        [InspectorName("指向准备秒数"), Min(0.05f)] public float readySeconds = 0.2f;
        [InspectorName("准备时稳定范围（米）"), Min(0.005f)] public float readyStability = 0.035f;
        [InspectorName("选点驻留秒数"), Min(0.05f)] public float dwellSeconds = 0.65f;
        [InspectorName("选向进入距离（米）"), Min(0.01f)] public float directionDistance = 0.05f;
        [InspectorName("回中立距离（米）"), Min(0.005f)] public float neutralDistance = 0.025f;
        [InspectorName("换向优势比"), Min(1.05f)] public float directionHysteresis = 1.35f;
        [InspectorName("最大样本年龄（秒）"), Min(0.02f)] public float sampleMaxAge = 0.15f;
        [InspectorName("最大样本间隔（秒）"), Min(0.02f)] public float sampleMaxGap = 0.18f;
        [InspectorName("双手低位取消秒数"), Min(0.05f)] public float cancelSeconds = 0.4f;
        [Header("右手张掌四向短划")]
        [InspectorName("短划距离（米）"), Min(0.04f)] public float swipeDistance = 0.1f;
        [InspectorName("短划最短秒数"), Min(0.02f)] public float swipeMinSeconds = 0.06f;
        [InspectorName("短划最长秒数"), Min(0.1f)] public float swipeMaxSeconds = 0.45f;
        [InspectorName("短划轨迹上限（米）"), Min(0.15f)] public float swipeMaxPath = 0.6f;
        [InspectorName("短划竖直偏移上限（米）"), Min(0.03f)] public float swipeMaxVertical = 0.2f;
        [InspectorName("短划直线率下限"), Range(0.2f, 1f)] public float swipeEfficiency = 0.65f;
    }

    public struct RpgGestureIntent
    {
        public Vector2 navigationStep;
        public Vector3 dodgeDirection;
        public bool cancel;
    }
}
