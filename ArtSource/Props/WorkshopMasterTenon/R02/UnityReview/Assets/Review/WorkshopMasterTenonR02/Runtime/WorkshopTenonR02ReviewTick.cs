using System;
using UnityEngine;
namespace NarutoVR.WorkshopTenonR02Review
{
    [DefaultExecutionOrder(1000)]
    public sealed class WorkshopTenonR02ReviewTick : MonoBehaviour
    {
        public static Action Tick;
        void LateUpdate() {Tick?.Invoke();}
    }
}
