using System;
using UnityEngine;

namespace NarutoVR.SealCombat
{
    [Serializable]
    public struct HandThrowObservation
    {
        public int schemaVersion;
        public string shotId, eventType, source, releaseMode;
        public long sequence;
        public bool hasSample, suppressed;
        public bool gripTimestampValid, headPoseValid;
        public double wristObserved;
        public ThrowActionScore score;
        public double observed, now;
        public Vector3 hand, head, headForward, launchVelocity;
        public ThrowGripSample grip;
        public HandThrowEstimate estimate;
        public bool armed;
        public string status;
        public long candidateId;
        public bool pending, inputValid;
        public float gripProgress, releaseProgress;
        public Vector3 releaseOrigin;
        public float storageRemainingSeconds, projectileSpeed;
    }
}
