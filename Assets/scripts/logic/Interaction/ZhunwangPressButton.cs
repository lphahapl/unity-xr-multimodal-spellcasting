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
        Vector3 previousDelta;
        bool hasPrevious;
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
            if (!WristMenuState.Finite(tip)) { ResetContact(); return; }
            if(!cap)return;
            if(!ready)Prepare(); if(!ready)return;
            Vector3 axis=cap.TransformVector(Vector3.forward).normalized;
            Vector3 origin=cap.parent.TransformPoint(rest)+cap.TransformVector(surface);
            Vector3 delta=tip-origin;float depth=Vector3.Dot(delta,axis);
            // Track in cap orientation, in metres, so movement of the worn device does not
            // leave a stale world-space press plane. A real fast poke may cross it between frames.
            Vector3 relative=Quaternion.Inverse(cap.rotation)*delta;
            float pressDepth=travelMetres*.85f;
            bool crossed=false;
            if(armed && hasPrevious && previousDelta.z<=pressDepth && relative.z>=pressDepth &&
                relative.z>previousDelta.z && (relative-previousDelta).sqrMagnitude<=.08f*.08f)
            {
                Vector3 contact=Vector3.Lerp(previousDelta,relative,(pressDepth-previousDelta.z)/(relative.z-previousDelta.z));
                crossed=contact.x*contact.x+contact.y*contact.y<=touchRadius*touchRadius;
            }
            previousDelta=relative;hasPrevious=true;
            if(crossed)
            {
                cap.localPosition=rest+cap.parent.InverseTransformVector(axis*travelMetres);
                if(!IsPressed){IsPressed=true;onPressed.Invoke();}
                return;
            }
            bool inside=(delta-depth*axis).sqrMagnitude<=touchRadius*touchRadius;
            if(!inside||depth<-.003f||depth>.015f)
            {
                ResetContact();
                if(inside&&depth<0){armed=true;previousDelta=relative;hasPrevious=true;}
                return;
            }
            if(depth<=0)armed=true;
            if(!armed)return;
            float displacement=Mathf.Clamp(depth,0,travelMetres);
            cap.localPosition=rest+cap.parent.InverseTransformVector(axis*displacement);
            if(!IsPressed&&displacement>=travelMetres*.85f){IsPressed=true;onPressed.Invoke();}
            else if(IsPressed&&displacement<=travelMetres*.20f){IsPressed=false;onReleased.Invoke();}
        }
        public void ResetContact(){if(ready&&cap)cap.localPosition=rest;armed=hasPrevious=false;if(IsPressed){IsPressed=false;onReleased.Invoke();}}
        void OnDisable(){ResetContact();ready=false;}
    }
}
