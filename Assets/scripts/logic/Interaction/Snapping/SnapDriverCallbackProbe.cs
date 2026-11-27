#if UNITY_EDITOR
using System;
using UnityEngine;

namespace NarutoVR.SealCombat.Validation
{
    /// <summary>重现第三方位置驱动在启停时同步进入吸附系统的情况。</summary>
    [AddComponentMenu("")]
    public sealed class SnapDriverCallbackProbe : MonoBehaviour
    {
        public Action DuringEnable, DuringDisable;
        void OnEnable() => DuringEnable?.Invoke();
        void OnDisable() => DuringDisable?.Invoke();
    }
}
#endif
