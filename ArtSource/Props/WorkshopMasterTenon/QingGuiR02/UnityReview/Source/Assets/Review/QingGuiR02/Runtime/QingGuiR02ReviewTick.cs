using System;
using UnityEngine;
namespace NarutoVR.QingGuiR02Review
{
    [DefaultExecutionOrder(1000)]
    public sealed class QingGuiR02ReviewTick : MonoBehaviour
    {
        public static Action Tick;
        void LateUpdate() {Tick?.Invoke();}
    }
}
