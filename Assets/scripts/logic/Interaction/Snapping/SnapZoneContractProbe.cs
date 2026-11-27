#if UNITY_EDITOR
using System;
using UnityEngine;
using NeuGameMRTools.Runtime.Components;

namespace NarutoVR.SealCombat.Validation
{
    /// <summary>仅供编辑器 Play 回归。所有反馈均由基类发送，探针只作准入和姿态决策。</summary>
    [AddComponentMenu("")]
    public sealed class SnapZoneContractProbe : SnapZone
    {
        [NonSerialized] public bool Accept = true;
        [NonSerialized] public Pose? FinalPose;
        [NonSerialized] public Action DuringPrepare;
        [NonSerialized] public Action<string> Legacy;
        public int Calls { get; private set; }
        public GameObject Candidate { get; private set; }
        public Quaternion? Requested { get; private set; }

        protected override bool TryPrepareSnap(GameObject candidate, Quaternion? requestedWorldRotation, ref Pose targetPose, out string reason)
        {
            Calls++;
            Candidate = candidate;
            Requested = requestedWorldRotation;
            DuringPrepare?.Invoke();
            if (FinalPose.HasValue) targetPose = FinalPose.Value;
            reason = Accept ? "" : "验收探针拒收";
            return Accept;
        }

        public void LegacyStart(GameObject item) => Legacy?.Invoke("U.start");
        public void LegacyPoint(GameObject item, int point) => Legacy?.Invoke("U.point" + point);
        public void LegacyComplete(GameObject item) => Legacy?.Invoke("U.done");
        public void LegacyRelease(GameObject item) => Legacy?.Invoke("U.exit");
    }
}
#endif
