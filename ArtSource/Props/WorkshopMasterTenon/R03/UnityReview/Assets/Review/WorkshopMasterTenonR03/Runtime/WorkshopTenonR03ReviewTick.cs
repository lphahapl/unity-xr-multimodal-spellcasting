using System;
using UnityEngine;
namespace NarutoVR.WorkshopTenonR03Review
{
    [DefaultExecutionOrder(1000)]
    public sealed class WorkshopTenonR03ReviewTick : MonoBehaviour
    {
        public static Action Tick;
        void LateUpdate() {Tick?.Invoke();}
    }
}
