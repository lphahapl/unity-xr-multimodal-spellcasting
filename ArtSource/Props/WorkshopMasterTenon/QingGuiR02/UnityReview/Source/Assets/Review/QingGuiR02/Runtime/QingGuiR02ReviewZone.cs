using NarutoVR.SealCombat.Items;
using NeuGameMRTools.Runtime.Components;
using UnityEngine;
using System.Linq;

namespace NarutoVR.QingGuiR02Review
{
    // Review-only identity and geometric admission; interpolation and ownership remain SnapZone's.
    public sealed class QingGuiR02ReviewZone : SnapZone
    {
        public GameItemBase expectedTenon;
        public Transform opening, alignment, seat;
        public MeshCollider[] actualConsoleColliders;
        public BoxCollider[] contactColliders;
        public BoxCollider conservativeGauge;
        // Exact LOD0 triangle positions in this zone's local frame; no replacement hole geometry.
        [HideInInspector] public Vector3[] actualConsoleTriangles;
        public float maximumTilt = 15f;
        public float maximumOffset = .012f;
        public int completedCount, startedCount;
        public float lastMaximumPenetration;
        public string admissionNote;
        Quaternion QuarterRotation(GameObject candidate)
        {
            var forward=Vector3.ProjectOnPlane(candidate.transform.forward,opening.up);
            float yaw=Vector3.SignedAngle(opening.forward,forward,opening.up);
            return opening.rotation*Quaternion.Euler(0,90f*Mathf.Round(yaw/90f),0);
        }
        public bool TryInsertAssigned()
        {
            if(!expectedTenon || !opening || !alignment || !seat || IsSnapping || SnappedObject)return false;
            var orientation=QuarterRotation(expectedTenon.gameObject);
            alignment.rotation=orientation;seat.rotation=orientation;
            return TrySnapObject(expectedTenon.gameObject);
        }
        void Awake()
        {
            AutomaticSnapping=false;
            OnSnapStarted += candidate => startedCount++;
            OnSnapCompleted += candidate => completedCount++;
        }

        protected override bool TryPrepareSnap(GameObject candidate, Quaternion? requestedWorldRotation, ref Pose targetPose, out string reason)
        {
            reason="This review socket accepts only its assigned workshop master tenon.";
            if (!expectedTenon || candidate != expectedTenon.gameObject || !opening || !alignment || !seat) return Reject(reason);
            if (expectedTenon.IsHeld) { reason="Release the held tenon above the opening first."; return Reject(reason); }
            var tip=opening.InverseTransformPoint(candidate.transform.position);
            if (Mathf.Abs(tip.x)>maximumOffset || Mathf.Abs(tip.z)>maximumOffset || tip.y<.016f || tip.y>.075f)
            { reason="Insertion end must be centered above the real opening, before descent."; return Reject(reason); }
            if (Vector3.Angle(candidate.transform.up,opening.up)>maximumTilt || Quaternion.Angle(candidate.transform.rotation,QuarterRotation(candidate))>maximumTilt)
            { reason="Insertion axis or indexed face is outside the review alignment window."; return Reject(reason); }
            if(!conservativeGauge || conservativeGauge.enabled || contactColliders==null || contactColliders.Length==0 || contactColliders.Any(c=>!c || !c.enabled || c.isTrigger))
            {reason="Separate disabled gauge and enabled physical contact colliders required.";return Reject(reason);}
            lastMaximumPenetration=0;
            var initial=new Pose(candidate.transform.position,candidate.transform.rotation);
            var aligned=new Pose(alignment.position,alignment.rotation);
            var seated=new Pose(seat.position,seat.rotation);
            if(!ClearPath(initial,aligned) || !ClearPath(aligned,seated))
            { reason="The actual console mesh obstructs the proposed insertion path.";return Reject(reason); }
            targetPose=seated;
            admissionNote="Accepted assigned tenon, pre-alignment and descent clear against actual console meshes.";
            reason=null;return true;
        }
        bool Reject(string reason) {admissionNote=reason;return false;}
        bool ClearPath(Pose a,Pose b)
        {
            for(int i=0;i<=32;i++) {
                var p=Vector3.Lerp(a.position,b.position,i/32f);
                var q=Quaternion.Slerp(a.rotation,b.rotation,i/32f);
                float depth=Mathf.Max(PenetrationAt(p,q),GaugePenetrationAt(p,q));
                lastMaximumPenetration=Mathf.Max(lastMaximumPenetration,depth);
                if(depth>.0003f)return false;
            }
            return true;
        }
        public float PenetrationAt(Vector3 position,Quaternion rotation)
        {return Mathf.Max(PhysicsPenetrationAt(position,rotation),GeometryPenetrationAt(position,rotation));}
        public float PhysicsPenetrationAt(Vector3 position,Quaternion rotation)
        {
            float result=0;
            foreach(var shape in contactColliders) {
                var pose=ShapePose(shape,position,rotation);
                foreach(var solid in actualConsoleColliders)
                    if(solid && solid.enabled && Physics.ComputePenetration(shape,pose.position,pose.rotation,solid,solid.transform.position,solid.transform.rotation,out _,out float depth))result=Mathf.Max(result,depth);
            }
            return result;
        }
        public float GeometryPenetrationAt(Vector3 position,Quaternion rotation)
        {
            float result=0;
            foreach(var shape in contactColliders)result=Mathf.Max(result,ShapeTrianglePenetration(shape,position,rotation));
            return result;
        }
        public float GaugePenetrationAt(Vector3 position,Quaternion rotation)
        {return ShapeTrianglePenetration(conservativeGauge,position,rotation);}
        Pose ShapePose(BoxCollider shape,Vector3 rootPosition,Quaternion rootRotation)
        {
            var root=expectedTenon.transform;
            return new Pose(rootPosition+rootRotation*root.InverseTransformPoint(shape.transform.position),rootRotation*Quaternion.Inverse(root.rotation)*shape.transform.rotation);
        }
        float ShapeTrianglePenetration(BoxCollider shape,Vector3 rootPosition,Quaternion rootRotation)
        {
            if(actualConsoleTriangles==null || !shape)return float.PositiveInfinity;
            var pose=ShapePose(shape,rootPosition,rootRotation);var half=shape.size*.5f;
            var matrix=Matrix4x4.TRS(pose.position,pose.rotation,Vector3.one).inverse*transform.localToWorldMatrix;float maximum=0;
            for(int i=0;i+2<actualConsoleTriangles.Length;i+=3) {
                var a=matrix.MultiplyPoint3x4(actualConsoleTriangles[i])-shape.center;
                var b=matrix.MultiplyPoint3x4(actualConsoleTriangles[i+1])-shape.center;
                var c=matrix.MultiplyPoint3x4(actualConsoleTriangles[i+2])-shape.center;
                float depth=float.PositiveInfinity;
                if(!Axis(Vector3.right,a,b,c,half,ref depth)||!Axis(Vector3.up,a,b,c,half,ref depth)||!Axis(Vector3.forward,a,b,c,half,ref depth))continue;
                var ab=b-a;var bc=c-b;var ca=a-c;
                if(!Axis(Vector3.Cross(ab,c-a),a,b,c,half,ref depth))continue;
                if(!Edges(ab,a,b,c,half,ref depth)||!Edges(bc,a,b,c,half,ref depth)||!Edges(ca,a,b,c,half,ref depth))continue;
                if(float.IsFinite(depth))maximum=Mathf.Max(maximum,depth);
            }
            return maximum;
        }
        static bool Edges(Vector3 edge,Vector3 a,Vector3 b,Vector3 c,Vector3 half,ref float depth)
        {return Axis(Vector3.Cross(edge,Vector3.right),a,b,c,half,ref depth)&&Axis(Vector3.Cross(edge,Vector3.up),a,b,c,half,ref depth)&&Axis(Vector3.Cross(edge,Vector3.forward),a,b,c,half,ref depth);}
        static bool Axis(Vector3 axis,Vector3 a,Vector3 b,Vector3 c,Vector3 half,ref float depth)
        {
            float magnitude=axis.magnitude;if(magnitude<1e-9f)return true;axis/=magnitude;
            float av=Vector3.Dot(a,axis),bv=Vector3.Dot(b,axis),cv=Vector3.Dot(c,axis);
            float minimum=Mathf.Min(av,Mathf.Min(bv,cv)),maximum=Mathf.Max(av,Mathf.Max(bv,cv));
            float radius=half.x*Mathf.Abs(axis.x)+half.y*Mathf.Abs(axis.y)+half.z*Mathf.Abs(axis.z);
            if(minimum>radius || maximum<-radius)return false;
            depth=Mathf.Min(depth,Mathf.Min(radius-minimum,maximum+radius));return true;
        }
    }
}
