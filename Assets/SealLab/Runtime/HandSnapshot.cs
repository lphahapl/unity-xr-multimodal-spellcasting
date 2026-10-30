using System;
using Oculus.Interaction.Input;
using UnityEngine;

namespace NarutoVR.SealLab
{
    [Serializable]
    public sealed class HandSnapshot
    {
        [Header("一、追踪状态与数据质量")]
        [InspectorName("已连接")] public bool connected;
        [InspectorName("追踪有效")] public bool tracked;
        [InspectorName("高质量数据")] public bool highConfidence;
        [InspectorName("数据未过期")] public bool fresh;
        [InspectorName("腕部相对姿态有效")] public bool wristPosesValid;
        [Header("二、更新时间与手部尺度")]
        [InspectorName("数据版本")]
        public int version;
        [InspectorName("最近观测时间（秒）")]
        public double observedAt;
        [InspectorName("手部尺度")]
        public float scale = 1;
        [InspectorName("质量标记来源")]
        public string confidenceSource = "Meta runtime";
        [Header("三、骨骼姿态与有效性")]
        [InspectorName("关节姿态")]
        public Pose[] joints = new Pose[Constants.NUM_HAND_JOINTS];
        [InspectorName("各关节有效性")]
        public bool[] jointValid = new bool[Constants.NUM_HAND_JOINTS];
        [InspectorName("相对手腕的关节姿态")]
        public Pose[] wristPoses = new Pose[Constants.NUM_HAND_JOINTS];
        [InspectorName("逐指质量标记")]
        public bool[] fingerConfidence = new bool[5];
    }

    [Serializable]
    public sealed class SealFrame
    {
        [Header("一、录制帧信息")]
        [InspectorName("数据格式版本")] public int schema = 1;
        [InspectorName("采样时间（秒）")] public double time;
        [InspectorName("样本标签")] public string label = "";
        [InspectorName("骨骼格式")] public string skeleton = Constants.NUM_HAND_JOINTS + "-joint Meta IHand";
        [InspectorName("追踪来源")] public string source = "Meta OVR";
        [Header("二、双手骨骼快照")]
        [InspectorName("左手快照")] public HandSnapshot left = new();
        [InspectorName("右手快照")] public HandSnapshot right = new();
    }

    [Serializable]
    public struct SealFeature
    {
        [Header("特征数值与质量")]
        [InspectorName("固定特征标识")] public string id;
        [InspectorName("单位")] public string unit;
        [InspectorName("特征数值")] public float value;
        [InspectorName("数值有效")] public bool valid;
        [InspectorName("高质量特征")] public bool highConfidence;
        public SealFeature(string id, string unit, float value, bool valid, bool confidence)
        {
            this.id = id; this.unit = unit;
            this.valid = valid && float.IsFinite(value);
            this.value = this.valid ? value : 0;
            highConfidence = confidence && this.valid;
        }
    }

    // Only the frozen snapshot is exposed to SDK feature calculations. No synthetic poses.
    internal sealed class SnapshotHand : IHand
    {
        private HandSnapshot data;
        public void Set(HandSnapshot snapshot) => data = snapshot;
        public Handedness Handedness { get; set; }
        public bool IsConnected => data != null && data.connected;
        public bool IsTrackedDataValid => IsConnected && data.tracked && data.fresh;
        public bool IsHighConfidence => IsTrackedDataValid && data.highConfidence;
        public bool IsDominantHand => false;
        public float Scale => data?.scale ?? 1;
        public int CurrentDataVersion => data?.version ?? 0;
        public bool IsPointerPoseValid => false;
        public event Action WhenHandUpdated { add { } remove { } }
        public bool GetFingerIsHighConfidence(HandFinger f) => IsTrackedDataValid && data.fingerConfidence[(int)f];
        public bool GetJointPose(HandJointId id, out Pose pose)
        {
            int i = (int)id; pose = Pose.identity;
            if (!IsTrackedDataValid || i < 0 || i >= data.joints.Length || !data.jointValid[i]) return false;
            pose = data.joints[i]; return true;
        }
        public bool GetRootPose(out Pose pose) => GetJointPose(HandJointId.HandWristRoot, out pose);
        public bool GetJointPoseFromWrist(HandJointId id, out Pose pose)
        {
            int i = (int)id; pose = Pose.identity;
            if (!IsTrackedDataValid || !data.wristPosesValid || i < 0 || i >= data.wristPoses.Length || !data.jointValid[i]) return false;
            pose = data.wristPoses[i]; return true;
        }
        public bool GetJointPosesFromWrist(out ReadOnlyHandJointPoses poses)
        {
            poses = IsTrackedDataValid && data.wristPosesValid ? new ReadOnlyHandJointPoses(data.wristPoses) : ReadOnlyHandJointPoses.Empty;
            return IsTrackedDataValid && data.wristPosesValid;
        }
        public bool GetJointPoseLocal(HandJointId id, out Pose pose) { pose = Pose.identity; return false; }
        public bool GetJointPosesLocal(out ReadOnlyHandJointPoses poses) { poses = ReadOnlyHandJointPoses.Empty; return false; }
        public bool GetPalmPoseLocal(out Pose pose) { pose = Pose.identity; return false; }
        public bool GetPointerPose(out Pose pose) { pose = Pose.identity; return false; }
        public bool GetFingerIsPinching(HandFinger finger) => false;
        public bool GetIndexFingerIsPinching() => false;
        public float GetFingerPinchStrength(HandFinger finger) => 0;
    }
}
