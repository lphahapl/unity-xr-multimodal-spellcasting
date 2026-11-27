using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Unity.Collections;
using UnityEditor;
using UnityEngine;

/// <summary>从 Play 中实际挂载的场景石外观读取网格，导出正式物理石局部坐标（Unity 左手系，米）。</summary>
public static class Chapter02CarrierMeshExport
{
    [Serializable] sealed class Dump
    {
        public string basis="Unity stone-local metres; +X right, +Y up, +Z forward. Preserve Unity triangle order.";
        public string originalHierarchy, physicalStone, visualRoot;
        public Vector3 originalVisualWorld, originalVisualLocal, carryOffset;
        public Vector3 visualLocalPosition, visualLocalScale;
        public Quaternion visualLocalRotation;
        public float[] stoneWorldToLocal, visualLocalToWorld, visualLocalToStone;
        public Vector3 boundsMin,boundsMax;
        public int vertices,triangles;
        public List<Part> meshes=new();
    }
    [Serializable] sealed class Part
    {
        public string node,mesh,asset,assetGuid,localFileId;
        public Vector3 filterLocalPosition,filterLocalScale;
        public Quaternion filterLocalRotation;
        public float[] filterLocalToStone;
        public int vertices;
        public List<Group> groups=new();
    }
    [Serializable] sealed class Group { public int index,triangles;public string name,material; }
    static float[] Rows(Matrix4x4 m)
    {
        var values=new float[16];for(int i=0;i<16;i++)values[i]=m[i/4,i%4];return values;
    }
    static string F(float n)=>n.ToString("R",CultureInfo.InvariantCulture);
    static string Label(string text)=>string.Concat(text.Select(c=>char.IsWhiteSpace(c)||c=='/'?'_':c));
    public static void Export(Transform stone,Transform visual,Vector3 oldWorld,Vector3 oldLocal,Vector3 carryOffset,string originalHierarchy)
    {
        var dump=new Dump{originalHierarchy=originalHierarchy,physicalStone=Chapter02CarrierNatural.Hierarchy(stone),visualRoot=visual.name,
            originalVisualWorld=oldWorld,originalVisualLocal=oldLocal,carryOffset=carryOffset,
            visualLocalPosition=visual.localPosition,visualLocalRotation=visual.localRotation,visualLocalScale=visual.localScale,
            stoneWorldToLocal=Rows(stone.worldToLocalMatrix),visualLocalToWorld=Rows(visual.localToWorldMatrix),
            visualLocalToStone=Rows(stone.worldToLocalMatrix*visual.localToWorldMatrix)};
        var obj=new StringBuilder("# stone-local metres, Unity axes and triangle order\n");
        var min=Vector3.one*float.PositiveInfinity;var max=Vector3.one*float.NegativeInfinity;
        foreach(var filter in visual.GetComponentsInChildren<MeshFilter>(true))
        {
            var mesh=filter.sharedMesh;if(!mesh)continue;
            if(!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mesh,out string guid,out long id))throw new InvalidOperationException("无法定位网格 "+mesh.name);
            var mat=filter.GetComponent<Renderer>().sharedMaterials;
            var node=filter.transform;var matrix=stone.worldToLocalMatrix*node.localToWorldMatrix;
            var part=new Part{node=Chapter02CarrierNatural.Hierarchy(node),mesh=mesh.name,asset=AssetDatabase.GetAssetPath(mesh),
                assetGuid=guid,localFileId=id.ToString(),filterLocalPosition=node.localPosition,filterLocalScale=node.localScale,
                filterLocalRotation=node.localRotation,filterLocalToStone=Rows(matrix),vertices=mesh.vertexCount};
            dump.meshes.Add(part);obj.AppendLine("o "+Label(mesh.name));
            using var data=UnityEditor.MeshUtility.AcquireReadOnlyMeshData(mesh);
            var md=data[0];using var vertices=new NativeArray<Vector3>(md.vertexCount,Allocator.Temp);
            md.GetVertices(vertices);
            foreach(var local in vertices)
            {
                var pt=matrix.MultiplyPoint3x4(local);min=Vector3.Min(min,pt);max=Vector3.Max(max,pt);
                obj.AppendLine($"v {F(pt.x)} {F(pt.y)} {F(pt.z)}");
            }
            for(int sub=0;sub<md.subMeshCount;sub++)
            {
                var desc=md.GetSubMesh(sub);if(desc.topology!=MeshTopology.Triangles)throw new InvalidOperationException("非三角网格");
                string material=sub<mat.Length && mat[sub]?mat[sub].name:"none";
                string name=$"submesh_{sub}_{Label(material)}";
                obj.AppendLine("g "+name);obj.AppendLine("usemtl "+Label(material));
                using var idx=new NativeArray<int>(desc.indexCount,Allocator.Temp);md.GetIndices(idx,sub,true);
                for(int j=0;j<idx.Length;j+=3)
                    obj.AppendLine($"f {dump.vertices+1+idx[j]} {dump.vertices+1+idx[j+1]} {dump.vertices+1+idx[j+2]}");
                int faces=idx.Length/3;dump.triangles+=faces;part.groups.Add(new Group{index=sub,triangles=faces,name=name,material=material});
            }
            dump.vertices+=md.vertexCount;
        }
        dump.boundsMin=min;dump.boundsMax=max;
        if(dump.vertices==0||dump.triangles==0)throw new InvalidOperationException("导出为空");
        File.WriteAllText(Path.Combine(Chapter02CarrierNatural.Report,"stone-local.obj"),obj.ToString(),new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(Chapter02CarrierNatural.Report,"stone-local.json"),JsonUtility.ToJson(dump,true),new UTF8Encoding(false));
        Debug.Log($"CH02_STONE_LOCAL_MESH {dump.meshes.Count} meshes {dump.vertices} verts {dump.triangles} triangles stone-local bounds [{min:F5}, {max:F5}]");
    }
}
