using NarutoVR.SealCombat.Items;
using NeuGameMRTools.Runtime.Components;
using UnityEngine;

namespace NarutoVR.SealCombat.Rpg
{
    public sealed class WorkshopTenonSocket : SnapZone
    {
        public WorkshopTenonObjective objective;
        public Transform mouth, align, bottom;
        public bool TryInsert(GameItemBase item)
        {
            if (!item || !objective || !objective.Accepts(item) || !mouth || !align || !bottom) return false;
            var orientation = IndexedRotation(item.transform);
            align.rotation = bottom.rotation = orientation;
            return TrySnapObject(item.gameObject);
        }

        Quaternion IndexedRotation(Transform part)
        {
            var direction = Vector3.ProjectOnPlane(part.forward, mouth.up);
            float yaw = Vector3.SignedAngle(mouth.forward, direction, mouth.up);
            return mouth.rotation * Quaternion.Euler(0, 90 * Mathf.Round(yaw / 90), 0);
        }

        protected override bool TryPrepareSnap(GameObject candidate, Quaternion? requested, ref Pose target, out string reason)
        {
            reason = "工坊主榫须由玩家持至插口正上方并松手";
            if (!objective || !objective.Accepts(candidate.GetComponent<GameItemBase>()) || !mouth || !bottom || !align || requested.HasValue)
                return false;
            var p = mouth.InverseTransformPoint(candidate.transform.position);
            var rotation = IndexedRotation(candidate.transform);
            if (Mathf.Abs(p.x) > .012f || Mathf.Abs(p.z) > .012f || p.y < .016f || p.y > .12f ||
                Quaternion.Angle(candidate.transform.rotation, rotation) > 12f ||
                Quaternion.Angle(align.rotation,rotation) > .1f) return false;
            target = new Pose(bottom.position, rotation);
            reason = null;
            return true;
        }
    }
}
