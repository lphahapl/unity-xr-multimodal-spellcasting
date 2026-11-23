using System;
using System.Collections.Generic;
using System.Linq;
using NarutoVR.SealCombat.Rpg;
using UnityEditor;
using UnityEngine;

namespace NarutoVR.Editor
{
    internal static class TrackedAvatarSleeve
    {
        const int Segments=32;
        const float HandJoin=.073f;

        struct Section
        {
            public Vector3 center, u, v;
            public float width, depth;
            public Vector3 At(float angle,float clearance) => center +
                Mathf.Cos(angle)*(width+clearance)*u + Mathf.Sin(angle)*(depth+clearance)*v;
        }

        static Section Measure(IEnumerable<SkinnedMeshRenderer> skins,Vector3 center,Vector3 axis,Vector3 across,
            Func<Vector3,Vector3> map)
        {
            var u=Vector3.ProjectOnPlane(across,axis).normalized;
            var v=Vector3.Cross(axis,u);
            var min=new Vector2(float.MaxValue,float.MaxValue);
            var max=new Vector2(float.MinValue,float.MinValue);
            foreach(var renderer in skins)
            {
                var baked=new Mesh();
                Vector3[] p;
                try
                {
                    renderer.BakeMesh(baked,true);
                    p=baked.vertices.Select(x=>map(renderer.transform.TransformPoint(x))-center).ToArray();
                }
                finally {UnityEngine.Object.DestroyImmediate(baked);}
                var tris=renderer.sharedMesh.triangles;
                for(int i=0;i<tris.Length;i+=3)
                {
                    for(int k=0;k<3;k++)
                    {
                        Vector3 a=p[tris[i+k]], b=p[tris[i+(k+1)%3]];
                        float da=Vector3.Dot(a,axis), db=Vector3.Dot(b,axis);
                        if((da>0)==(db>0)) continue;
                        var point=Vector3.Lerp(a,b,da/(da-db));
                        if(point.sqrMagnitude>.10f*.10f) continue;
                        var xy=new Vector2(Vector3.Dot(point,u),Vector3.Dot(point,v));
                        min=Vector2.Min(min,xy); max=Vector2.Max(max,xy);
                    }
                }
            }
            if(min.x>max.x) throw new InvalidOperationException("袖口截面没有与蒙皮相交。");
            return new Section {center=center+.5f*((max.x+min.x)*u+(max.y+min.y)*v),u=u,v=v,
                width=(max.x-min.x)*.5f,depth=(max.y-min.y)*.5f};
        }

        internal static SkinnedMeshRenderer Build(GameObject character,SkinnedMeshRenderer[] original,
            TrackedAvatarArms.Arm arm,bool left,string path)
        {
            string side=left?"Left":"Right";
            var v2=AssetDatabase.LoadAssetAtPath<GameObject>(PlayerHandRig.Art+"Player"+side+"Hand.prefab");
            var src=v2.GetComponentInChildren<SkinnedMeshRenderer>(true);
            var wrist=arm.targets[0];
            Vector3 wristAxis=(wrist.position-(arm.targets[4].position+arm.targets[13].position)*.5f).normalized;
            Vector3 elbowAxis=(arm.lower.position-wrist.position).normalized;
            Vector3 across=(arm.targets[4].position-arm.targets[13].position).normalized;
            Quaternion rotation=wrist.rotation*Quaternion.Inverse(arm.axisOffsets[0])*Quaternion.Inverse(src.rootBone.rotation);
            var distal=Measure(new[]{src},wrist.position+HandJoin*wristAxis,wristAxis,across,
                p=>wrist.position+rotation*(p-src.rootBone.position));
            var proximal=Measure(original,wrist.position+TrackedAvatarBodyMesh.SleeveCut(arm)*elbowAxis,
                elbowAxis,across,p=>p);
            Vector3[] vertices=new Vector3[7*(Segments+1)];
            var weights=new BoneWeight[vertices.Length];
            var uv=new Vector2[vertices.Length];
            var triangles=new List<int>();
            var root=character.transform;
            // First and last rings turn in under the hand cloth and under the original rolled sleeve.
            float[] stations={0,0,.22f,.48f,.73f,1,1};
            for(int j=0;j<stations.Length;j++)
                for(int i=0;i<=Segments;i++)
                {
                    float t=stations[j], angle=2*Mathf.PI*i/Segments;
                    Vector3 p=Vector3.Lerp(distal.At(angle,j==0?-.001f:.0025f),proximal.At(angle,j==6?-.003f:.002f),t);
                    if(j==0) p+=wristAxis*.004f;
                    if(j==6) p+=elbowAxis*.012f;
                    int n=j*(Segments+1)+i;
                    vertices[n]=root.InverseTransformPoint(p);
                    float share=Mathf.SmoothStep(0,1,t);
                    weights[n]=new BoneWeight {boneIndex0=0,weight0=1-share,boneIndex1=1,weight1=share};
                    uv[n]=new Vector2((float)i/Segments,.04f+.92f*(float)j/(stations.Length-1));
                    if(j==0 || i==0) continue;
                    int a=n-Segments-2, b=a+1, c=n-1;
                    triangles.AddRange(new[]{a,b,n,a,n,c});
                }
            var mesh=new Mesh {name=side+"Sleeve",vertices=vertices,uv=uv,triangles=triangles.ToArray(),
                bindposes=new[]{wrist.worldToLocalMatrix*root.localToWorldMatrix,arm.lower.worldToLocalMatrix*root.localToWorldMatrix},
                boneWeights=weights};
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AssetDatabase.AddObjectToAsset(mesh,path);
            var mat=new Material(AssetDatabase.LoadAssetAtPath<Material>(PlayerHandRig.Art+side+"Cuff.mat"))
                {name=side+"SleeveCloth"};
            mat.DisableKeyword("_FOREARM_FADE");mat.SetFloat("_ForearmFade",0);
            AssetDatabase.AddObjectToAsset(mat,path);
            var node=new GameObject(side+"Sleeve");
            node.transform.SetParent(root,false);
            var view=node.AddComponent<SkinnedMeshRenderer>();
            view.bones=new[]{wrist,arm.lower}; view.rootBone=root;
            view.sharedMesh=mesh;view.sharedMaterial=mat;
            float reach=Vector3.Distance(arm.upper.position,arm.lower.position)+Vector3.Distance(arm.lower.position,wrist.position);
            view.localBounds=new Bounds(root.InverseTransformPoint(arm.upper.position),Vector3.one*(2*reach+.4f));
            view.enabled=false;
            return view;
        }
    }
}
