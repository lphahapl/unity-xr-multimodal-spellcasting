using System;
using UnityEngine;

namespace NarutoVR.SealCombat.Rpg
{
    public abstract class QuestGestureCondition : MonoBehaviour
    {
        public abstract bool CanDemonstrate(Transform target);
    }

    [Serializable]
    public sealed class QuestGestureHint
    {
        [InspectorName("交互接触点")] public Transform target;
        [InspectorName("动作预设")] public QuestGestureMotion motion;
        [InspectorName("位移参考系"), Tooltip("留空使用世界坐标；偏移以米为单位，不受模型缩放影响。")] public Transform frame;
        [InspectorName("旋转中心（可选）")] public Transform pivot;
        [InspectorName("等待完成的目标（可选）"), Tooltip("拖入本阶段或本句正在运行的目标。达成或停用时收起这个示范。")]
        public ChapterObjective pendingObjective;
        [InspectorName("交互状态条件（可选）")] public QuestGestureCondition condition;
        [InspectorName("触发距离（米）"), Min(.01f)] public float range = 1.25f;
        [InspectorName("首次等待（秒）"), Min(0)] public float delay = 7f;
        [InspectorName("演示时长（秒）"), Min(.01f)] public float duration = 2.8f;
        [InspectorName("重复间隔（秒）"), Min(0)] public float repeatInterval = 5f;

        internal bool Available(Vector3 foot)
        {
            if (!motion || !target || !target.gameObject.activeInHierarchy) return false;
            var offset = target.position - foot;
            return offset.x * offset.x + offset.z * offset.z <= range * range &&
                (!pendingObjective || (pendingObjective.IsRunning && !pendingObjective.IsComplete)) &&
                (!condition || (condition.isActiveAndEnabled && condition.CanDemonstrate(target)));
        }

        internal Vector3 ContactAt(float progress)
        {
            var axes = frame ? frame.rotation : Quaternion.identity;
            var position = target.position;
            if (pivot && motion.turnDegrees != 0f)
                position = pivot.position + Quaternion.AngleAxis(motion.turnDegrees * progress, axes * motion.turnAxis) * (position - pivot.position);
            return position + axes * Vector3.Lerp(motion.startOffset, motion.endOffset, progress);
        }
    }

    [CreateAssetMenu(menuName = "任务引导/手势动作预设")]
    public sealed class QuestGestureMotion : ScriptableObject
    {
        public enum Contact { [InspectorName("拇指与食指中点")] Pinch, [InspectorName("食指尖")] IndexTip, [InspectorName("腕部")] Wrist }
        public enum Grip { [InspectorName("捏住再松开")] Grasp, [InspectorName("从握持到松开")] Release, [InspectorName("全程保持指定手形")] Hold }

        [InspectorName("接触部位")] public Contact contact;
        [InspectorName("手形变化")] public Grip grip;
        [InspectorName("手形动画"), Tooltip("采样兼容手骨骼的动画首帧。留空使用示范手的默认捏合。")] public AnimationClip pose;
        [InspectorName("动作起点偏移（米）")] public Vector3 startOffset;
        [InspectorName("动作终点偏移（米）")] public Vector3 endOffset;
        [InspectorName("旋转轴")] public Vector3 turnAxis = Vector3.up;
        [InspectorName("旋转角（度）")] public float turnDegrees;
        [InspectorName("手朝向跟随位移参考系"), Tooltip("参考系的正 Z 是指尖方向、正 Y 是上方。未指定参考系时面向玩家所看的目标。")]
        public bool orientToFrame;
        [InspectorName("手朝向修正（度）")] public Vector3 handEuler;
        [InspectorName("接近与撤手偏移（米）"), Tooltip("跟随参考系时使用其坐标轴，否则负 Z 朝玩家。")]
        public Vector3 approachOffset = new Vector3(0f, .065f, -.065f);
    }
}
