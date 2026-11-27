using System;
using UnityEngine;
namespace NarutoVR.QingGuiR01Review
{
    public sealed class QingGuiMaterialTick:MonoBehaviour
    {
        public static Action Tick;
        void LateUpdate(){Tick?.Invoke();}
    }
}
