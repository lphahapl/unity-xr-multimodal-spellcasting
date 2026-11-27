using System;
using System.Collections.Generic;
using System.Linq;
using NarutoVR.SealCombat.Items;
using UnityEditor;
using UnityEngine;

namespace NarutoVR.SealCombat.Editor
{
    // Extract the existing fixed marker so its first pickup removes exactly what was visible on the stand.
    public static class WorkshopWhiteboxTenon
    {
        const string Folder = "Assets/ScriptableObjects/Chapter02Workshop";
        public const string PrefabPath = Folder + "/WhiteboxMasterTenon.prefab";

        public static GameObject FromStand(MeshFilter stand)
        {
            var saved = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (saved) return saved;
            const int marker = 3;
            var mesh = stand ? stand.sharedMesh : null;
            var renderer = stand ? stand.GetComponent<MeshRenderer>() : null;
            if (!mesh || mesh.subMeshCount != 5 || !renderer || renderer.sharedMaterials.Length != 5 ||
                !renderer.sharedMaterials[marker] || renderer.sharedMaterials[marker].name != "WorkshopLimeInlay")
                throw new InvalidOperationException("当前承座已经替换，请直接传入它的可持主榫 Prefab");
            var indices = mesh.GetTriangles(marker);
            var bounds = mesh.GetSubMesh(marker).bounds;
            if (indices.Length == 0 || Mathf.Abs(bounds.size.y - .35f) > .001f)
                throw new InvalidOperationException("无法从承座识别原有白盒主榫");
            var pivot = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            var vertices = mesh.vertices;
            var normals = mesh.normals;
            var uv = mesh.uv;
            var source = indices.Distinct().ToArray();
            var remap = new Dictionary<int,int>();
            for (int i = 0; i < source.Length; i++) remap.Add(source[i], i);
            string meshPath = Folder + "/WhiteboxMasterTenon.asset";
            var part = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (!part)
            {
                part = new Mesh { name = "白盒主榫", vertices = source.Select(i => vertices[i] - pivot).ToArray() };
                if (normals.Length == vertices.Length) part.normals = source.Select(i => normals[i]).ToArray();
                if (uv.Length == vertices.Length) part.uv = source.Select(i => uv[i]).ToArray();
                part.triangles = indices.Select(i => remap[i]).ToArray();
                if (normals.Length != vertices.Length) part.RecalculateNormals();
                part.RecalculateBounds();
                AssetDatabase.CreateAsset(part, meshPath);
            }
            var root = new GameObject("校准主榫 · 现有白盒");
            try
            {
                root.AddComponent<MeshFilter>().sharedMesh = part;
                root.AddComponent<MeshRenderer>().sharedMaterial = renderer.sharedMaterials[marker];
                var collider = root.AddComponent<BoxCollider>();
                collider.center = part.bounds.center;
                collider.size = part.bounds.size;
                var item = root.AddComponent<GameItemBase>();
                item.grip = new GameObject("握心").transform;
                item.grip.SetParent(root.transform, false);
                item.grip.localPosition = Vector3.up * .215f;
                item.insertionTip = root.transform;
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                if (!prefab) throw new InvalidOperationException("保存白盒主榫失败");
                return prefab;
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
