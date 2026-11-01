#if UNITY_EDITOR
using System;
using UnityEngine;

namespace NarutoVR.SealCombat.Editor
{
    /// <summary>Feed synthetic tracking before gameplay each player frame, not from the throttled Editor UI update.</summary>
    [DefaultExecutionOrder(-900)]
    public sealed class RpgPlayCheckDriver : MonoBehaviour
    {
        public Action Tick;
        void Update() => Tick?.Invoke();
    }
}
#endif
