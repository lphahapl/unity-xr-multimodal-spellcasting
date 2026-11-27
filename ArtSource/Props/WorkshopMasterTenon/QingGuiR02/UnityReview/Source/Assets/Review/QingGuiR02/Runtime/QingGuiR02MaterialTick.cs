using System;
using UnityEngine;
namespace NarutoVR.QingGuiR02Review { public sealed class QingGuiR02MaterialTick:MonoBehaviour {public static Action Tick;void LateUpdate()=>Tick?.Invoke();} }
