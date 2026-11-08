using UnityEngine;
using UnityEngine.Events;
namespace NarutoVR.SealCombat.Rpg
{
    /// <summary>距扣帽未按下的表面测量右指轴向位移。单位为世界米；移动扣帽不改变匣身和事件检测平面。</summary>
    [DisallowMultipleComponent]
    public sealed class ZhunwangPressButton : MonoBehaviour
    {
        public Transform cap;
        [InspectorName("匣面与教学投影位")] public ZhunwangWristDisplay display;
        [Tooltip("场景中的右食指输入；独立展台也可指定 fingertip。")] public WristMenuController handInput;
        public Transform fingertip;
        [Min(.0001f)] public float travelMetres = .0015f;
        [Min(.001f)] public float touchRadius = .022f;
        public UnityEvent onPressed = new();
        public UnityEvent onReleased = new();
        public bool IsPressed { get; private set; }
        Vector3 rest, surface;
        bool ready, armed;
        public Vector3 RestSurface => ready && cap ? cap.parent.TransformPoint(rest)+cap.TransformVector(surface) : transform.position;
        void OnEnable() { if (cap) Prepare(); }
        void Prepare()
        {
            var mesh = cap.GetComponent<MeshFilter>(); if (!mesh || !mesh.sharedMesh || !cap.parent) return;
            rest=cap.localPosition; var b=mesh.sharedMesh.bounds;surface=b.center;surface.z=b.min.z;ready=true;
        }
        void LateUpdate()
        {
            // Worn buttons are sampled once before gameplay arbitrates this same hand frame.
            if (handInput && handInput.wornDevice == this) return;
            if(fingertip){Sample(fingertip.position);return;}
            if(handInput&&handInput.isActiveAndEnabled){
                var frame=handInput.LatestInput;
                if(!frame.tipUnavailable && WristMenuState.Finite(frame.rightTip) && WristMenuState.Fresh(frame.rightValid,frame.rightObserved,Time.realtimeSinceStartupAsDouble,handInput.settings.maxAge)){Sample(frame.rightTip);return;}
            }
            ResetContact();
        }
        public void Sample(Vector3 tip)
        {
            if(!cap)return;
            if(!ready)Prepare(); if(!ready)return;
            Vector3 axis=cap.TransformVector(Vector3.forward).normalized;
            Vector3 origin=cap.parent.TransformPoint(rest)+cap.TransformVector(surface);
            Vector3 delta=tip-origin;float depth=Vector3.Dot(delta,axis);
            bool inside=(delta-depth*axis).sqrMagnitude<=touchRadius*touchRadius;
            if(!inside||depth<-.003f||depth>.015f){ResetContact();if(inside&&depth<0)armed=true;return;}
            if(depth<=0)armed=true;
            if(!armed)return;
            float displacement=Mathf.Clamp(depth,0,travelMetres);
            cap.localPosition=rest+cap.parent.InverseTransformVector(axis*displacement);
            if(!IsPressed&&displacement>=travelMetres*.85f){IsPressed=true;onPressed.Invoke();}
            else if(IsPressed&&displacement<=travelMetres*.20f){IsPressed=false;onReleased.Invoke();}
        }
        public void ResetContact(){if(ready&&cap)cap.localPosition=rest;armed=false;if(IsPressed){IsPressed=false;onReleased.Invoke();}}
        void OnDisable(){ResetContact();ready=false;}
    }
}
