using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Editor-side snapshot of visible LOD0 art for the local review workbench.
// Source PNGs and shader parameters are resolved by build_review_glb.py.
public static class Chapter02ReviewMesh
{
    [Serializable] class Snapshot { public List<Part> parts=new(); public List<Surface> materials=new(); }
    [Serializable] class Part { public string name; public int mat,verts,indices; public long offset; }
    [Serializable] class Surface
    {
        public string name,albedo,normal,metalMap; public float[] color;
        public float metal,smooth,bump; public bool smoothFromColor;
    }
    static string TexturePath(Material m,string key)
    {
        if (!m.HasTexture(key)) return "";
        var t=m.GetTexture(key);
        return t?AssetDatabase.GetAssetPath(t):"";
    }
    static Surface Describe(Material m)
    {
        string map=TexturePath(m,"_BaseMap");
        if(string.IsNullOrEmpty(map))map=TexturePath(m,"_MainTex");
        var color=m.HasColor("_BaseColor")?m.GetColor("_BaseColor"):m.HasColor("_Color")?m.color:Color.white;
        return new Surface{name=m.name,albedo=map,normal=TexturePath(m,"_BumpMap"),metalMap=TexturePath(m,"_MetallicGlossMap"),
            color=new[]{color.r,color.g,color.b,1f},metal=m.HasFloat("_Metallic")?m.GetFloat("_Metallic"):0,
            smooth=m.HasFloat("_Smoothness")?m.GetFloat("_Smoothness"):.35f,
            smoothFromColor=m.HasFloat("_SmoothnessTextureChannel")&&m.GetFloat("_SmoothnessTextureChannel")>.5f,
            bump=m.HasFloat("_BumpScale")?m.GetFloat("_BumpScale"):1};
    }
    public static void Export()
    {
        var scene=EditorSceneManager.OpenScene("Assets/Scene/Chapter02_SuspendedPath.unity");
        var detailRoots=scene.GetRootGameObjects();
        var omitted=new HashSet<Renderer>();
        foreach(var group in detailRoots.SelectMany(o=>o.GetComponentsInChildren<LODGroup>()))
        {
            var levels=group.GetLODs(); if(levels.Length<1)continue;
            var first=new HashSet<Renderer>(levels[0].renderers);
            foreach(var other in levels.Skip(1).SelectMany(l=>l.renderers)) if(!first.Contains(other))omitted.Add(other);
        }
        var renderers=detailRoots.SelectMany(o=>o.GetComponentsInChildren<MeshRenderer>())
            .Where(r=>r.enabled&&!omitted.Contains(r)).ToArray();
        string folder=Environment.GetEnvironmentVariable("CH2_ART_OUTPUT");
        foreach(var set in new[]{"basket","workshop"})
        {
            var snapshot=new Snapshot();var materials=new Dictionary<Material,int>();
            using(var writer=new BinaryWriter(File.Create(Path.Combine(folder,set+"-mesh.bin"))))
            foreach(var renderer in renderers)
            {
                var names=new List<string>();
                for(var t=renderer.transform;t;t=t.parent)names.Add(t.name);
                names.Reverse();string path=string.Join("/",names);
                bool include=set=="basket"?names.Contains("A · 镇石配重吊篮"):
                    (names.Any(n=>n.Contains("锻造房")||n.Contains("炭棚")||n.Contains("修械房")||n.Contains("石券桥楼")||n.Contains("工坊器物")||n.Contains("锻造与修械工位")||n.Contains("取榫")||n.Contains("主榫与承座")) ||
                     path.Contains("ApprovedWorkshop") || path.Contains("定稿工坊") || path.Contains("工坊桥口定稿"));
                if(!include||!renderer.TryGetComponent<MeshFilter>(out var filter)||!filter.sharedMesh)continue;
                var mesh=filter.sharedMesh;var vertices=mesh.vertices;var normals=mesh.normals;var uv=mesh.uv;
                var matrix=filter.transform.localToWorldMatrix;var nMatrix=matrix.inverse.transpose;
                var slots=renderer.sharedMaterials;
                for(int sm=0;sm<Math.Min(mesh.subMeshCount,slots.Length);sm++)
                {
                    var mat=slots[sm];if(!mat||mesh.GetTopology(sm)!=MeshTopology.Triangles)continue;
                    if(!materials.TryGetValue(mat,out int mi)){mi=materials.Count;materials.Add(mat,mi);snapshot.materials.Add(Describe(mat));}
                    var indices=mesh.GetTriangles(sm);if(indices.Length==0)continue;
                    var unique=indices.Distinct().ToArray();var remap=new Dictionary<int,int>();
                    var part=new Part{name=path+" / "+mat.name,mat=mi,verts=unique.Length,indices=indices.Length,offset=writer.BaseStream.Position};
                    snapshot.parts.Add(part);
                    string uvKey=mat.HasTexture("_BaseMap")?"_BaseMap":"_MainTex";
                    var scale=mat.HasTexture(uvKey)?mat.GetTextureScale(uvKey):Vector2.one;
                    var shift=mat.HasTexture(uvKey)?mat.GetTextureOffset(uvKey):Vector2.zero;
                    foreach(int k in unique){
                        remap[k]=remap.Count;var p=matrix.MultiplyPoint3x4(vertices[k]);
                        var n=normals.Length==vertices.Length?nMatrix.MultiplyVector(normals[k]).normalized:Vector3.up;
                        var u=uv.Length==vertices.Length?Vector2.Scale(uv[k],scale)+shift:Vector2.zero;
                        writer.Write(p.x);writer.Write(p.y);writer.Write(-p.z);
                        writer.Write(n.x);writer.Write(n.y);writer.Write(-n.z);
                        writer.Write(u.x);writer.Write(1-u.y);
                    }
                    for(int i=0;i<indices.Length;i+=3){
                        writer.Write(remap[indices[i]]);
                        bool flip=matrix.determinant>0;
                        writer.Write(remap[indices[i+(flip?2:1)]]);writer.Write(remap[indices[i+(flip?1:2)]]);
                    }
                }
            }
            if(snapshot.parts.Count==0)throw new Exception("No review mesh: "+set);
            File.WriteAllText(Path.Combine(folder,set+"-mesh.json"),JsonUtility.ToJson(snapshot));
        }
    }
}
