using System;
using UnityEngine;
namespace NarutoVR.QingGuiR03Review {
    public sealed class QingGuiR03ReviewTick:MonoBehaviour {
        public static Action Tick;
        void LateUpdate(){Tick?.Invoke();}
    }
}
