using System;
using System.Collections.Generic;
using System.Linq;
using NarutoVR.SealCombat.Rpg;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace NarutoVR.Editor
{
    // Build once in the editor. The two views share the Animator, skeleton and materials.
    internal static class TrackedAvatarBodyMesh
    {
        internal static float SleeveCut(TrackedAvatarArms.Arm arm) =>
            Mathf.Min(.215f,.8f*Vector3.Distance(arm.lower.position,arm.targets[0].position));
        struct Vertex
        {
            public Vector3 point, normal;
            public Vector4 tangent;
            public Color color;
            public BoneWeight skin;
        }

        internal static SkinnedMeshRenderer[] Build(GameObject character, Animator animator, TrackedAvatarArms arms, string path)
        {
            var result = new List<SkinnedMeshRenderer>();
            var originals=character.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var original in originals)
            {
                Mesh mesh = Crop(original, animator, arms);
                if (!mesh) continue;
                mesh.name = original.sharedMesh.name + "_FirstPerson";
                if (result.Count == 0) AssetDatabase.CreateAsset(mesh, path);
                else AssetDatabase.AddObjectToAsset(mesh, path);
                var node = new GameObject("FirstPersonBody");
                node.transform.SetParent(original.transform, false);
                var view = node.AddComponent<SkinnedMeshRenderer>();
                view.sharedMesh = mesh;
                view.sharedMaterials = original.sharedMaterials;
                view.bones = original.bones;
                view.rootBone = original.rootBone;
                view.quality = original.quality;
                view.localBounds = original.localBounds;
                view.probeAnchor = original.probeAnchor;
                view.lightProbeUsage = original.lightProbeUsage;
                view.updateWhenOffscreen = original.updateWhenOffscreen;
                view.enabled = false;
                result.Add(view);
            }
            if (result.Count == 0) throw new InvalidOperationException("第一人称裁切后缺少躯干蒙皮。");
            for(int i=0;i<2;++i)
                result.Add(TrackedAvatarSleeve.Build(character,originals,arms.Side(i==0),i==0,path));
            AssetDatabase.SaveAssetIfDirty(AssetDatabase.LoadMainAssetAtPath(path));
            return result.ToArray();
        }

        static Mesh Crop(SkinnedMeshRenderer renderer, Animator animator, TrackedAvatarArms arms)
        {
            var source = renderer.sharedMesh;
            if (!source) return null;
            Transform[] bones = renderer.bones;
            var neck = animator.GetBoneTransform(HumanBodyBones.Neck) ?? animator.GetBoneTransform(HumanBodyBones.Head);
            var head = animator.GetBoneTransform(HumanBodyBones.Head);
            var sides = new[] { arms.Side(true), arms.Side(false) };
            var cuts = sides.Select(SleeveCut).ToArray();
            var axes = sides.Select(s=>(s.lower.position-s.targets[0].position).normalized).ToArray();
            var restMesh = new Mesh();
            Vector3[] world;
            try
            {
                renderer.BakeMesh(restMesh,true);
                world = restMesh.vertices.Select(renderer.transform.TransformPoint).ToArray();
            }
            finally { UnityEngine.Object.DestroyImmediate(restMesh); }
            var sourceWeights = source.boneWeights;
            if (sourceWeights.Length != world.Length || bones.Length == 0)
                throw new InvalidOperationException("网格缺少可用的蒙皮权重 " + source.name);
            var weights = sourceWeights;
            var distance = new float[world.Length];
            var headBones = bones.Select(b=>b && b.IsChildOf(head)).ToArray();
            var armBones = sides.Select(s=>bones.Select(b=>b && b.IsChildOf(s.upper)).ToArray()).ToArray();
            for (int i=0; i<world.Length; ++i)
            {
                var p = world[i];
                float aboveNeck = Vector3.Dot(p-neck.position,animator.transform.up);
                Vector3 horizontal = Vector3.ProjectOnPlane(p-neck.position,animator.transform.up);
                float keep = aboveNeck > -.06f && horizontal.sqrMagnitude < .19f*.19f ? .018f-aboveNeck : 1f;
                if (Mass(weights[i],headBones)>.5f) keep = Mathf.Min(keep,-.02f);
                for (int s=0; s<2; ++s)
                {
                    if (Mass(sourceWeights[i],armBones[s])<.25f) continue;
                    float along = Vector3.Dot(p-sides[s].targets[0].position,axes[s]);
                    keep = Mathf.Min(keep,along-cuts[s]);
                }
                distance[i]=keep;
            }

            var positions=source.vertices;
            var normals=source.normals;
            var tangents=source.tangents;
            var colors=source.colors;
            var vertices=new List<Vertex>();
            var sourceIndices=new Dictionary<int,int>();
            var edges=new Dictionary<(int,int),int>();
            var uvIn=new List<Vector4>[8];
            var uvOut=new List<Vector4>[8];
            for(int ch=0;ch<8;++ch)
            {
                uvIn[ch]=new List<Vector4>();
                source.GetUVs(ch,uvIn[ch]);
                if(uvIn[ch].Count!=0) uvOut[ch]=new List<Vector4>();
            }
            int Add(int a,int b,float t)
            {
                int idx=vertices.Count;
                vertices.Add(new Vertex {
                    point=Vector3.Lerp(positions[a],positions[b],t),
                    normal=normals.Length==0 ? Vector3.zero : Vector3.Lerp(normals[a],normals[b],t).normalized,
                    tangent=tangents.Length==0 ? Vector4.zero : Vector4.Lerp(tangents[a],tangents[b],t),
                    color=colors.Length==0 ? Color.white : Color.Lerp(colors[a],colors[b],t),
                    skin=a==b ? weights[a] : Blend(weights[a],weights[b],t)
                });
                for(int ch=0;ch<8;++ch) uvOut[ch]?.Add(Vector4.Lerp(uvIn[ch][a],uvIn[ch][b],t));
                return idx;
            }
            int Original(int a)
            {
                if(!sourceIndices.TryGetValue(a,out int idx)) sourceIndices[a]=idx=Add(a,a,0);
                return idx;
            }
            int Intersection(int a,int b)
            {
                if(a>b) (a,b)=(b,a);
                if(!edges.TryGetValue((a,b),out int idx))
                    edges[(a,b)]=idx=Add(a,b,Mathf.Clamp01(distance[a]/(distance[a]-distance[b])));
                return idx;
            }
            var indices=new List<int>[source.subMeshCount];
            var polygon=new int[4];
            for(int s=0;s<indices.Length;++s)
            {
                var triangles=source.GetTriangles(s);
                var output=indices[s]=new List<int>();
                for(int i=0;i<triangles.Length;i+=3)
                {
                    int count=0, prev=triangles[i+2];
                    for(int k=0;k<3;++k)
                    {
                        int curr=triangles[i+k];
                        if((distance[prev]>=0)!=(distance[curr]>=0)) polygon[count++]=Intersection(prev,curr);
                        if(distance[curr]>=0) polygon[count++]=Original(curr);
                        prev=curr;
                    }
                    for(int k=1;k+1<count;++k) { output.Add(polygon[0]); output.Add(polygon[k]); output.Add(polygon[k+1]); }
                }
            }
            if(vertices.Count==0) return null;
            var mesh=new Mesh { indexFormat=vertices.Count>65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(vertices.Select(v=>v.point).ToList());
            if(normals.Length!=0) mesh.SetNormals(vertices.Select(v=>v.normal).ToList());
            if(tangents.Length!=0) mesh.SetTangents(vertices.Select(v=>v.tangent).ToList());
            if(colors.Length!=0) mesh.SetColors(vertices.Select(v=>v.color).ToList());
            for(int ch=0;ch<8;++ch)
                if(uvOut[ch]!=null)
                {
                    int dimension=source.GetVertexAttributeDimension((VertexAttribute)((int)VertexAttribute.TexCoord0+ch));
                    if(dimension==2) mesh.SetUVs(ch,uvOut[ch].Select(v=>(Vector2)v).ToList());
                    else if(dimension==3) mesh.SetUVs(ch,uvOut[ch].Select(v=>(Vector3)v).ToList());
                    else mesh.SetUVs(ch,uvOut[ch]);
                }
            mesh.bindposes=source.bindposes;
            mesh.boneWeights=vertices.Select(v=>v.skin).ToArray();
            mesh.subMeshCount=indices.Length;
            for(int s=0;s<indices.Length;++s) mesh.SetTriangles(indices[s],s);
            return mesh;
        }

        static float Mass(BoneWeight w,bool[] matches) =>
            (matches[w.boneIndex0]?w.weight0:0)+(matches[w.boneIndex1]?w.weight1:0)+
            (matches[w.boneIndex2]?w.weight2:0)+(matches[w.boneIndex3]?w.weight3:0);

        static BoneWeight Blend(BoneWeight a,BoneWeight b,float t)
        {
            if(t<=0) return a;
            if(t>=1) return b;
            var totals=new Dictionary<int,float>(8);
            void Add(int bone,float w) {if(w>0) totals[bone]=totals.GetValueOrDefault(bone)+w;}
            void Include(BoneWeight v,float k)
            {
                Add(v.boneIndex0,k*v.weight0); Add(v.boneIndex1,k*v.weight1);
                Add(v.boneIndex2,k*v.weight2); Add(v.boneIndex3,k*v.weight3);
            }
            Include(a,1-t); Include(b,t);
            var top=totals.OrderByDescending(p=>p.Value).Take(4).ToArray();
            float sum=top.Sum(p=>p.Value);
            var w=new BoneWeight {boneIndex0=top[0].Key,weight0=top[0].Value/sum};
            if(top.Length>1) { w.boneIndex1=top[1].Key; w.weight1=top[1].Value/sum; }
            if(top.Length>2) { w.boneIndex2=top[2].Key; w.weight2=top[2].Value/sum; }
            if(top.Length>3) { w.boneIndex3=top[3].Key; w.weight3=top[3].Value/sum; }
            return w;
        }
    }
}
