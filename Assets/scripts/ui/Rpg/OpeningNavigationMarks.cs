using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace NarutoVR.SealCombat.Rpg
{
    /// <summary>Small ground inlays for the chapter's combat destinations. Reads locomotion state only.</summary>
    internal sealed class OpeningNavigationMarks
    {
        const int MaxLandings = 6;
        const int ProgressSteps = 32;
        readonly RpgLocomotionController movement;
        readonly Transform holder;
        readonly MeshRenderer renderer;
        readonly Mesh mesh;
        readonly List<(NavigationNode node, float rank)> nearby = new();
        readonly List<(NavigationNode node, RaycastHit floor)> marks = new(MaxLandings);
        readonly List<Vector3> vertices = new(448);
        readonly List<int> cut = new(192);
        readonly List<int> stone = new(128);
        readonly List<int> copper = new(256);
        readonly Material cutMaterial, stoneMaterial, copperMaterial;
        NavigationNode previousFocus;
        Vector3 lastOrigin;
        RaycastHit focusFloor;
        int lastStep = -1;
        double refreshAfter;
        bool wasVisible, hasOrigin;
        internal bool HasFocus { get; private set; }

        public OpeningNavigationMarks(Transform parent,RpgLocomotionController movement,Material cutMaterial,Material stoneMaterial,Material copperMaterial)
        {
            this.movement=movement; this.cutMaterial=cutMaterial; this.stoneMaterial=stoneMaterial; this.copperMaterial=copperMaterial;
            var go=new GameObject("战斗落点 · 地面尺刻",typeof(MeshFilter),typeof(MeshRenderer));
            holder=go.transform; holder.SetParent(parent,false);
            mesh=new Mesh { name="局部落点与驻留尺刻" }; mesh.MarkDynamic();
            go.GetComponent<MeshFilter>().sharedMesh=mesh;
            renderer=go.GetComponent<MeshRenderer>();
            renderer.sharedMaterials=new[]{cutMaterial,stoneMaterial,copperMaterial};
            renderer.shadowCastingMode=ShadowCastingMode.Off; renderer.receiveShadows=false;
            renderer.enabled=false;
        }

        public void Refresh(bool visible)
        {
            if (!renderer) return;
            if (!visible)
            {
                renderer.enabled=false; wasVisible=HasFocus=false; previousFocus=null; lastStep=-1;
                return;
            }
            Vector3 origin=movement.PlayerFloorPosition;
            double now=Time.unscaledTimeAsDouble;
            bool refresh=!wasVisible || now>=refreshAfter || (origin-lastOrigin).sqrMagnitude>.25f;
            wasVisible=true;
            NavigationNode focus=movement.NavigationActive ? movement.CandidateNode : null;
            int step=focus ? Mathf.Clamp(Mathf.RoundToInt(movement.DwellProgress*ProgressSteps),0,ProgressSteps) : 0;
            if (!refresh && focus==previousFocus && step==lastStep) return;
            if (refresh)
            {
                marks.Clear(); refreshAfter=now+.33; lastOrigin=origin;
                hasOrigin=movement.PreviewNavigationOrigin(out var here);
                if (hasOrigin)
                {
                    lastOrigin=here.point; CollectLandings(here.point);
                }
            }
            if (refresh || focus!=previousFocus)
            {
                HasFocus=false;
                if (focus && hasOrigin)
                {
                    foreach (var entry in marks) if(entry.node==focus) { focusFloor=entry.floor; HasFocus=true; break; }
                    if (!HasFocus) HasFocus=movement.PreviewNavigationLanding(focus,lastOrigin,out focusFloor);
                }
            }
            previousFocus=focus; lastStep=step;
            vertices.Clear();cut.Clear();stone.Clear();copper.Clear();
            foreach (var entry in marks)
            {
                if (entry.node && (entry.node!=focus || !HasFocus)) DrawResting(entry.floor);
            }
            if (HasFocus) DrawFocus(focusFloor,step);
            mesh.Clear();mesh.SetVertices(vertices);mesh.subMeshCount=3;
            mesh.SetTriangles(cut,0,false);mesh.SetTriangles(stone,1,false);mesh.SetTriangles(copper,2,false);mesh.RecalculateBounds();
            renderer.enabled=vertices.Count!=0;
        }

        void CollectLandings(Vector3 origin)
        {
            nearby.Clear();
            var nodes=movement.NearbyNavigationNodes;
            if (nodes==null) return;
            float maximum=movement.offNodeRange*movement.offNodeRange;
            Vector3 forward=Vector3.ProjectOnPlane(movement.head.forward,Vector3.up).normalized;
            foreach(var node in nodes)
            {
                if (!node || !node.isActiveAndEnabled || node.gameObject.scene!=movement.gameObject.scene) continue;
                Vector3 delta=node.floorPosition-origin;
                float distance=delta.sqrMagnitude;
                if (distance<=maximum) nearby.Add((node,distance*(1f-.08f*Vector3.Dot(forward,delta.normalized))));
            }
            nearby.Sort((a,b)=>a.rank!=b.rank ? a.rank.CompareTo(b.rank) : string.CompareOrdinal(a.node.nodeId,b.node.nodeId));
            foreach (var pair in nearby)
            {
                if (movement.PreviewNavigationLanding(pair.node,origin,out var floor)) marks.Add((pair.node,floor));
                if (marks.Count==MaxLandings) break;
            }
        }

        void Basis(RaycastHit floor,out Vector3 center,out Vector3 direction,out Vector3 side)
        {
            Vector3 normal=floor.normal;
            center=floor.point+normal*.026f;
            direction=Vector3.ProjectOnPlane(center-lastOrigin,normal).normalized;
            if (direction.sqrMagnitude<.5f) direction=Vector3.ProjectOnPlane(Vector3.forward,normal).normalized;
            side=Vector3.Cross(normal,direction);
        }
        void DrawResting(RaycastHit floor)
        {
            Basis(floor,out var c,out var f,out var s);
            // Two broken strokes along the approach end at an open, notched ruler head.
            Inlay(c-f*.43f,c-f*.32f,floor.normal,.024f);
            Inlay(c-f*.26f,c-f*.15f,floor.normal,.030f);
            Inlay(c-s*.15f,c+s*.15f,floor.normal,.032f);
            Inlay(c-s*.15f,c-s*.15f+f*.087f,floor.normal,.030f);
            Inlay(c+s*.15f,c+s*.15f+f*.087f,floor.normal,.030f);
        }
        void Inlay(Vector3 a,Vector3 b,Vector3 normal,float width)
        {
            Vector3 edge=(b-a).normalized*.006f;
            Stroke(a-edge-normal*.002f,b+edge-normal*.002f,normal,width+.014f,cut);
            Stroke(a,b,normal,width,stone);
        }
        void DrawFocus(RaycastHit floor,int step)
        {
            Basis(floor,out var c,out var f,out var s);
            Stroke(c-f*.50f,c-f*.34f,floor.normal,.035f,copper);
            Stroke(c-f*.29f,c-f*.215f,floor.normal,.03f,copper);
            for(int corner=0;corner<4;corner++)
            {
                float a=corner*Mathf.PI*.5f;
                for(int j=0;j<4;j++)
                    ArcStroke(c,f,s,floor.normal,.21f,a+(j-2)*.145f,a+(j-1)*.145f,.030f);
            }
            for(int i=0;i<step;i++)
            {
                float a=i*Mathf.PI*2/ProgressSteps;
                ArcStroke(c,f,s,floor.normal,.136f,a,a+Mathf.PI*2/ProgressSteps,.026f);
            }
        }
        void ArcStroke(Vector3 c,Vector3 f,Vector3 s,Vector3 normal,float radius,float start,float end,float width)
        {
            Stroke(c+radius*(f*Mathf.Cos(start)+s*Mathf.Sin(start)),
                c+radius*(f*Mathf.Cos(end)+s*Mathf.Sin(end)),normal,width,copper);
        }
        void Stroke(Vector3 a,Vector3 b,Vector3 normal,float width,List<int> indices)
        {
            Vector3 across=Vector3.Cross(normal,b-a).normalized*(width*.5f);
            int first=vertices.Count;
            vertices.Add(holder.InverseTransformPoint(a-across));vertices.Add(holder.InverseTransformPoint(a+across));
            vertices.Add(holder.InverseTransformPoint(b+across));vertices.Add(holder.InverseTransformPoint(b-across));
            // Upward facing quads. They respect normal scene depth and never receive input.
            indices.Add(first);indices.Add(first+2);indices.Add(first+1);
            indices.Add(first);indices.Add(first+3);indices.Add(first+2);
        }
        public void Dispose()
        {
            UnityEngine.Object.Destroy(mesh);UnityEngine.Object.Destroy(cutMaterial);
            UnityEngine.Object.Destroy(stoneMaterial);UnityEngine.Object.Destroy(copperMaterial);
        }
    }
}
