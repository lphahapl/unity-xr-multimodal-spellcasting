using UnityEngine;

namespace NarutoVR.SealCombat.Enemies
{
    /// <summary>绑定一条循环搬运剪辑与逐帧石根姿态。每个姿态位于角色根空间，包含循环终点。</summary>
    [CreateAssetMenu(menuName = "考工律度/敌人/镇石搬运姿态")]
    public sealed class StoneCarryPose : ScriptableObject
    {
        [SerializeField, InspectorName("采样所用搬运剪辑")] AnimationClip clip;
        [SerializeField, InspectorName("石根局部姿态（含循环终点）")] Pose[] samples;
        [SerializeField, HideInInspector] string[] jointNames;
        [SerializeField, HideInInspector] Pose[] jointPoses;

        // Avatar 重定向会抬直负重的躯干。只在搬运态按实际机偶绑定骨架还原原作。
        public Transform[] BindJoints(Animator animator)
        {
            if (!animator || jointNames == null || jointNames.Length == 0 || jointPoses == null ||
                jointPoses.Length != jointNames.Length * FrameCount) return null;
            var hierarchy = animator.GetComponentsInChildren<Transform>(true);
            var joints = new Transform[jointNames.Length];
            for (int i = 0; i < joints.Length; i++)
            {
                foreach (var bone in hierarchy)
                    if (bone.name == jointNames[i]) { joints[i] = bone; break; }
                if (!joints[i]) return null;
            }
            return joints;
        }

        public void ApplyJoints(Transform actor, Transform[] joints, float normalizedTime)
        {
            if (joints == null) return;
            float frame = Mathf.Repeat(normalizedTime, 1f) * (FrameCount - 1);
            int i = Mathf.Min((int)frame, FrameCount - 2);
            int offset = i * joints.Length;
            float t = frame - i;
            var matrix = actor.localToWorldMatrix;
            var facing = actor.rotation;
            for (int j = 0; j < joints.Length; j++)
            {
                var a = jointPoses[offset + j];
                var b = jointPoses[offset + j + joints.Length];
                joints[j].SetPositionAndRotation(matrix.MultiplyPoint3x4(Vector3.Lerp(a.position, b.position, t)),
                    facing * Quaternion.Slerp(a.rotation, b.rotation, t));
            }
        }

        public AnimationClip Clip => clip;
        public int FrameCount => samples?.Length ?? 0;

        /// <summary>把 Animator 当前归一化时间映射到采样区间，在相邻帧间插值。</summary>
        public bool TryEvaluate(float normalizedTime, out Pose pose)
        {
            pose = Pose.identity;
            if (!clip || FrameCount < 2 || !float.IsFinite(normalizedTime)) return false;
            float frame = Mathf.Repeat(normalizedTime, 1f) * (FrameCount - 1);
            int i = Mathf.Min((int)frame, FrameCount - 2);
            var a = samples[i];
            var b = samples[i + 1];
            pose = new Pose(Vector3.Lerp(a.position, b.position, frame - i),
                Quaternion.Slerp(a.rotation, b.rotation, frame - i));
            return true;
        }
    }
}
