using System;
using UnityEngine;
namespace NarutoVR.WorkshopTenonReview
{
    [DefaultExecutionOrder(1000)]
    public sealed class WorkshopTenonReviewTick : MonoBehaviour
    {
        public static Action Tick;
        void LateUpdate() {Tick?.Invoke();}
    }
}
