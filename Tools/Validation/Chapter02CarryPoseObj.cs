using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static partial class Chapter02CarryPoseSampler
{
    const string SceneStoneGeometry = "Assets/Art/Environment/SuspendedPath/SuspendedPathGeometry.asset";

    // OBJ 是 Unity 现场读取的石根局部米和三角顺序；原 Mesh 子资源提供 OBJ 遗失的法线、UV 和切线。
    static void LoadStoneObj(GameObject stone, string path, Material bronzeMat)
    {
        var positions = new List<Vector3>();
        var groups = new Dictionary<string,List<int>>();
        List<int> indices = null;
        foreach (var line in File.ReadLines(path))
        {
            var fields = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length == 0) continue;
            switch (fields[0])
            {
                case "v":
                    positions.Add(new Vector3(Number(fields[1]), Number(fields[2]), Number(fields[3])));
                    break;
                case "usemtl":
                    var material = string.Join(" ", fields.Skip(1));
                    if (!groups.TryGetValue(material, out indices)) groups.Add(material, indices = new List<int>());
                    break;
                case "f":
                    if (indices == null || fields.Length != 4) throw new Exception("场景导出须包含各材质组并已三角化");
                    for (int i=1;i<4;i++) indices.Add(int.Parse(fields[i].Split('/')[0], CultureInfo.InvariantCulture)-1);
                    break;
            }
        }
        var source = AssetDatabase.LoadAllAssetsAtPath(SceneStoneGeometry).OfType<Mesh>().Single(mesh =>
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mesh,out string _,out long id) && id == 5343060100998540203L);
        var materials = new[] { "FoundationStone", "旧铜", "Timber" };
        if (source.subMeshCount != materials.Length || positions.Count != source.vertexCount) throw new Exception("现场网格顶点/材质与对应 Mesh 子资源数目不一致");
        for (int i=0;i<materials.Length;i++)
            if (!source.GetTriangles(i).SequenceEqual(groups[materials[i]])) throw new Exception("现场 OBJ 三角面序与 Mesh 不一致，材质 " + materials[i]);
        var original = source.vertices;
        float maxDelta=0;
        for(int i=0;i<original.Length;i++) maxDelta=Mathf.Max(maxDelta,Vector3.Distance(original[i],positions[i]));
        if(maxDelta>.00001f) throw new Exception("现场坐标基准异常，原始 Mesh 与 OBJ 顶点偏差 "+maxDelta);
        var meshCopy = Object.Instantiate(source);
        meshCopy.SetVertices(positions);
        meshCopy.RecalculateBounds();
        stone.AddComponent<MeshFilter>().sharedMesh=meshCopy;
        var shader = bronzeMat.shader;
        var stoneMat=Surface(shader,new Color(.62f,.68f,.72f),0,.2f,"Assets/Materials/Chapter01/WorkshopStoneWall.png");
        stoneMat.mainTextureScale = new Vector2(2,18);
        var timberMat=Surface(shader,Color.white,0,.19f,"Assets/Art/Environment/Opening/TianyiPavilionKit/TimberColor.png");
        stone.AddComponent<MeshRenderer>().sharedMaterials=new[]{stoneMat,bronzeMat,timberMat};
        Debug.Log("SCENE_STONE_OBJ vertices="+positions.Count+" triangles="+(groups.Values.Sum(x=>x.Count)/3)+
            " UnityBounds="+meshCopy.bounds+" maxSourceDelta="+maxDelta+" originalUVs="+source.uv.Length+
            " originalNormals="+source.normals.Length+" materials="+string.Join("|",materials));
    }
    static float Number(string value) => float.Parse(value,CultureInfo.InvariantCulture);
}
