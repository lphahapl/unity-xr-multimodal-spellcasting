using System;
using UnityEngine;
namespace NarutoVR.QingGuiR01Review
{
    [DefaultExecutionOrder(1000)]
    public sealed class QingGuiR01ReviewTick : MonoBehaviour
    {
        public static Action Tick;
        void LateUpdate() {Tick?.Invoke();}
    }
}
