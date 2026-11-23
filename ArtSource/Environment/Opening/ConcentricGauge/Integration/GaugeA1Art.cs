using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Obj = UnityEngine.Object;

// Author in a copy. Store direct references to the imported meshes so reimports update
// the art. Gameplay pivots, collisions and demonstration targets belong to the scenes.
public static class GaugeA1Art
{
    public const string Root = "Assets/Art/Environment/Opening/ConcentricGauge";
    public static readonly string[] Parts = { "Bench", "Stele", "Plinth", "Bed", "Outer", "Inner", "Box", "Lid" };

    static void Folder(string name)
    {
        if (!AssetDatabase.IsValidFolder(Root + "/" + name)) AssetDatabase.CreateFolder(Root, name);
    }

    static Texture2D Texture(string path, bool srgb, bool normal)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (!importer) throw new Exception("Missing " + path);
        var type = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        if (importer.textureType != type || importer.sRGBTexture != srgb || importer.alphaIsTransparency)
        {
            importer.textureType = type;
            importer.sRGBTexture = srgb;
            importer.alphaIsTransparency = false;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static Material MaterialFor(bool stone)
    {
        string kind = stone ? "Stone" : "Bronze";
        string prefix = Root + "/" + kind + "/" + (stone ? "Stone_" : "CG_A1_");
        if (!stone)
        {
            var casting = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Bronze/CG_A1_Bronze.mat");
            if (!casting || !casting.GetTexture("_BaseMap") || !casting.GetTexture("_BumpMap") ||
                !casting.GetTexture("_MetallicGlossMap")) throw new Exception("Incomplete casting material");
            return casting;
        }
        string path = Root + "/Materials/" + kind + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!mat)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.SetTexture("_BaseMap", Texture(prefix + "BaseColor.png", true, false));
        mat.SetTexture("_BumpMap", Texture(prefix + "Normal.png", false, true));
        mat.EnableKeyword("_NORMALMAP");
        mat.SetFloat("_Metallic",0);
        mat.SetFloat("_Smoothness",.18f);
        mat.enableInstancing = true;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    public static void Stone() => Build(true);
    public static void Bronze() => Build(false);
    public static void RefreshAll() { Stone(); Bronze(); }
    static void Build(bool stone)
    {
        Folder("Prefabs");
        Folder("Materials");
        var material = MaterialFor(stone);
        var report = new List<string>();
        foreach (var part in stone ? Parts.Take(3) : Parts.Skip(3))
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/" + (stone ? "Stone/" : "Bronze/") + part + ".fbx");
            if (!fbx) throw new Exception("Missing " + part);
            var model = Obj.Instantiate(fbx);
            var root = new GameObject("A1_" + part);
            try
            {
                var filters = model.GetComponentsInChildren<MeshFilter>(true).OrderBy(f => f.name).ToArray();
                if (filters.Length != (stone ? 3 : 1)) throw new Exception(part + " unexpected mesh count " + filters.Length);
                var levels = new List<LOD>();
                for (int i=0; i<filters.Length; i++)
                {
                    var f = filters[i];
                    var mesh = f.sharedMesh;
                    if (stone && !f.name.EndsWith("_LOD"+i)) throw new Exception("Unexpected mesh " + f.name);
                    var child = new GameObject(mesh.name);
                    child.transform.SetParent(root.transform,false);
                    child.transform.SetPositionAndRotation(f.transform.position, f.transform.rotation);
                    child.transform.localScale = f.transform.lossyScale;
                    child.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var r = child.AddComponent<MeshRenderer>();
                    if (mesh.subMeshCount != 1) throw new Exception("Unexpected material slots: " + mesh.name);
                    r.sharedMaterial = material;
                    levels.Add(new LOD(!stone ? .035f : i==0 ? .19f : i==1 ? .065f : .008f, new Renderer[]{r}));
                    var points = mesh.vertices.Select(v=>child.transform.TransformPoint(v)).ToArray();
                    var min = points.Aggregate(Vector3.Min);
                    var max = points.Aggregate(Vector3.Max);
                    var ear = points.Where(v=>v.y>max.y-.004f).ToArray();
                    var crown = ear.Aggregate(Vector3.zero,(sum,v)=>sum+v)/ear.Length;
                    var clue = points.Where(v=>v.y>.65f && v.y<1.35f).ToArray();
                    report.Add($"{part} LOD{i} triangles={mesh.GetIndexCount(0)/3} material={material.name} min={min:F5} max={max:F5} top={crown:F5} middleZ={(clue.Length>0 ? clue.Average(v=>v.z) : 0):F5}");
                }
                var group = root.AddComponent<LODGroup>();
                group.SetLODs(levels.ToArray());
                group.RecalculateBounds();
                if (!PrefabUtility.SaveAsPrefabAsset(root, Root + "/Prefabs/" + root.name + ".prefab"))
                    throw new Exception("Prefab save failed: " + part);
            }
            finally { Obj.DestroyImmediate(model); Obj.DestroyImmediate(root); }
        }
        AssetDatabase.SaveAssets();
        File.WriteAllLines(Path.Combine(GaugeA1Audit.Output, (stone ? "Stone" : "Bronze") + "-import.txt"), report);
    }
}
