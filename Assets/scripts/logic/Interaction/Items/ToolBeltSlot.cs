using NeuGameMRTools.Runtime.Components;
using UnityEngine;

namespace NarutoVR.SealCombat.Items
{
    [DisallowMultipleComponent]
    public sealed class ToolBeltSlot : SnapZone
    {
        [Tooltip("固定在带上的开口。局部 X、Z 为口宽和厚度，-Y 为插入方向。")]
        public Transform opening;
        [Min(.001f)] public float width = .08f;
        [Min(.001f)] public float thickness = .06f;
        [Min(.001f)] public float depth = .12f;
        public bool penHolster;
        [SerializeField] internal GameItemConfigSO fittedPen;
        [SerializeField, Min(0)] internal float fittedPenDepth;
        [Tooltip("仅用于首次陈列；通用槽的放回判定只检查空间。")]
        public GameItemKind displayKind;

        void Awake()
        {
            AutomaticSnapping = false;
            OnSnapStarted += obj => obj.GetComponent<GameItemBase>().EnterDock(this);
            OnSnapCompleted += obj => obj.GetComponent<GameItemBase>().ConfirmDock(this);
            OnSnapReleased += (obj, completed) =>
            {
                if (obj && obj.TryGetComponent<GameItemBase>(out var item)) item.LeaveDock(this, completed);
            };
        }

        protected override bool TryPrepareSnap(GameObject candidate, Quaternion? requestedWorldRotation, ref Pose targetPose, out string reason)
        {
            reason = "工具带开口未配置或道具不可收纳";
            if (!opening || transform.parent != opening || !candidate.TryGetComponent<GameItemBase>(out var item) ||
                !item.isActiveAndEnabled || item.IsHeld) return false;
            if (penHolster && item.Kind != GameItemKind.GlyphPen) { reason = "此笔套专收墨规笔"; return false; }
            var rotation = requestedWorldRotation ?? item.transform.rotation;
            if (fittedPen && item.definition == fittedPen && fittedPenDepth > 0 && item.insertionTip)
            {
                var seated = opening.rotation * Quaternion.Euler(item.stowEuler);
                if (Quaternion.Angle(rotation, seated) > 25f)
                { reason = "沿槽口方向放入墨规笔"; return false; }
                var offset = item.transform.InverseTransformPoint(item.insertionTip.position);
                targetPose = new Pose(opening.TransformPoint(Vector3.down * fittedPenDepth) -
                    seated * Vector3.Scale(offset, item.transform.lossyScale / item.CurrentSize), seated);
                reason = null;
                return true;
            }
            if (!item.MeasureInSlot(opening, rotation, out var bounds)) return false;
            var size = bounds.size;
            const float epsilon = .00001f;
            if (!(size.sqrMagnitude > 0f && size.x <= width + epsilon && size.z <= thickness + epsilon && size.y <= depth + epsilon))
            { reason = "放入姿态超过开口宽厚或可用深度"; return false; }

            float reveal = Mathf.Min(.035f, size.y * .35f);
            targetPose = new Pose(opening.TransformPoint(new Vector3(-bounds.center.x, reveal - bounds.max.y, -bounds.center.z)), rotation);
            reason = null;
            return true;
        }
    }
}
