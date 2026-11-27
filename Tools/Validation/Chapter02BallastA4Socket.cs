// 在隔离工程的 Editor 程序集运行。导出现场 A 篮与实际锁槽坐标，供同件模型净空回归。
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Unity.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class Chapter02BallastA4Socket
{
    static string Number(float value) => value.ToString("R", CultureInfo.InvariantCulture);
    static string Vector(Vector3 v) => string.Join(" ", Number(v.x), Number(v.y), Number(v.z));

    public static void Run()
    {
        if (!Application.isBatchMode || !Application.dataPath.Replace('\\','/').StartsWith("D:/Temp/",StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("仅限独立副本");
        var dir = Environment.GetEnvironmentVariable("T25_SLOT_EXPORT");
        var scene = EditorSceneManager.OpenScene("Assets/Scene/Chapter02_SuspendedPath.unity");
        var basket = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true))
            .Single(c => c && c.GetType().Name == "BallastBasket");
        var slot = (Transform)new SerializedObject(basket).FindProperty("slot").objectReferenceValue;
        var bounds = new Bounds(slot.position, Vector3.one * 2.8f);
        var obj = new StringBuilder();
        var boxes = new StringBuilder();
        foreach (var col in basket.GetComponentsInChildren<BoxCollider>(false))
        {
            if (!col.enabled || col.isTrigger || !bounds.Intersects(col.bounds)) continue;
            var matrix = slot.worldToLocalMatrix * col.transform.localToWorldMatrix;
            boxes.AppendLine(string.Join("\t", col.transform.name, Vector(matrix.MultiplyPoint3x4(col.center)),
                Vector(matrix.MultiplyVector(Vector3.right * col.size.x * .5f)),
                Vector(matrix.MultiplyVector(Vector3.up * col.size.y * .5f)),
                Vector(matrix.MultiplyVector(Vector3.forward * col.size.z * .5f))));
        }
        var index = 1;
        foreach (var filter in basket.GetComponentsInChildren<MeshFilter>(false))
        {
            var renderer = filter.GetComponent<MeshRenderer>();
            if (!renderer || !renderer.enabled || !bounds.Intersects(renderer.bounds) || !filter.sharedMesh) continue;
            using var meshData = UnityEditor.MeshUtility.AcquireReadOnlyMeshData(filter.sharedMesh);
            var mesh = meshData[0];
            using var points = new NativeArray<Vector3>(mesh.vertexCount, Allocator.Temp);
            mesh.GetVertices(points);
            var matrix = slot.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            obj.AppendLine("o " + filter.name);
            foreach (var p in points) obj.AppendLine("v " + Vector(matrix.MultiplyPoint3x4(p)));
            for (int sub = 0; sub < mesh.subMeshCount; sub++)
            {
                var desc = mesh.GetSubMesh(sub);
                if (desc.topology != MeshTopology.Triangles) throw new InvalidOperationException(filter.name);
                using var faces = new NativeArray<int>(desc.indexCount, Allocator.Temp);
                mesh.GetIndices(faces,sub);
                for (int i=0; i < faces.Length; i+=3)
                    obj.AppendLine($"f {index+faces[i]} {index+faces[i+1]} {index+faces[i+2]}");
            }
            index += mesh.vertexCount;
        }
        if (index==1 || boxes.Length==0) throw new InvalidOperationException("实际篮体无网格或碰撞");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir,"socket.obj"),obj.ToString());
        File.WriteAllText(Path.Combine(dir,"colliders.tsv"),boxes.ToString());
        File.WriteAllText(Path.Combine(dir,"slot.txt"),$"{slot.name}\nworld {Vector(slot.position)}\nyaw {Number(slot.eulerAngles.y)}\nscale {Vector(slot.lossyScale)}\n");
        Debug.Log($"T25_A4_SOCKET_PASS {index-1} vertices");
    }

    public static void InspectFbx()
    {
        if (!Application.isBatchMode || !Application.dataPath.Replace('\\','/').StartsWith("D:/Temp/",StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("仅限独立副本");
        var dir = Environment.GetEnvironmentVariable("T25_SLOT_EXPORT");
        const string path = "Assets/T25Review/T25-A4.fbx";
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (!asset) throw new InvalidOperationException(path);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(asset);
        try
        {
            var pointsText = new StringBuilder();
            int triangles=0;
            foreach (var f in go.GetComponentsInChildren<MeshFilter>(true))
            {
                using var data=UnityEditor.MeshUtility.AcquireReadOnlyMeshData(f.sharedMesh);
                var m=data[0];
                using var p=new NativeArray<Vector3>(m.vertexCount,Allocator.Temp);
                m.GetVertices(p);
                foreach(var v in p) pointsText.AppendLine(Vector(f.transform.TransformPoint(v)));
                for (int i=0;i<m.subMeshCount;i++) triangles+=m.GetSubMesh(i).indexCount/3;
            }
            File.WriteAllText(Path.Combine(dir,"unity-fbx-vertices.txt"),pointsText.ToString());
            Debug.Log($"T25_FBX_IMPORT_PASS triangles={triangles}");
        } finally { Object.DestroyImmediate(go); }
    }
}
