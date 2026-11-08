using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class ArmillaryImportAudit
{
    const string Prefix = "Assets/Art/Environment/Opening/BrokenArmillary/";
    public static void Run()
    {
        var output = Path.Combine(Directory.GetCurrentDirectory(), "Logs/armillary-android-readback.txt");
        var lines = new List<string>();
        void Require(bool condition, string reason) { if (!condition) throw new InvalidDataException(reason); }
        try
        {
            lines.Add($"Unity {Application.unityVersion} / target {EditorUserBuildSettings.activeBuildTarget}");
            Require(EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android, "Android build target required");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(Prefix + "BrokenArmillary.mat");
            Require(mat && mat.shader.name == "Universal Render Pipeline/Lit", "URP Lit");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefix + "BrokenArmillary.fbx");
            var renderer = prefab.GetComponentsInChildren<MeshRenderer>(true).Single();
            var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
            var model = (ModelImporter)AssetImporter.GetAtPath(Prefix+"BrokenArmillary.fbx");
            Require(renderer.sharedMaterials.Single() == mat && mesh.subMeshCount == 1, "canonical material");
            Require(mesh.HasVertexAttribute(VertexAttribute.Normal) && mesh.HasVertexAttribute(VertexAttribute.Tangent) && mesh.HasVertexAttribute(VertexAttribute.TexCoord0), "UV/normal/tangent");
            Require(mesh.GetIndexCount(0) == 1776*3 && !model.isReadable && !model.addCollider, "tri budget or import");
            var bounds = renderer.bounds;
            lines.Add($"Renderers=1, submeshes=1, material={AssetDatabase.GetAssetPath(mat)}, vertices={mesh.vertexCount}, tris={mesh.GetIndexCount(0)/3}, normals={model.importNormals}, tangents={model.importTangents}, bottom={bounds.min.y:F4}, world size={bounds.size.ToString("F4")}");
            using (var data = MeshUtility.AcquireReadOnlyMeshData(mesh))
            using (var points = new NativeArray<Vector3>(data[0].vertexCount, Allocator.Temp))
            {
                data[0].GetVertices(points);
                var feet = new List<Vector3>();
                for (int i=0; i<points.Length; ++i)
                {
                    var v=renderer.transform.TransformPoint(points[i]);
                    if (v.y<.001f) feet.Add(v);
                }
                var left=feet.Where(p=>p.x<-.5f).Select(p=>p.x);
                var right=feet.Where(p=>p.x>.5f).Select(p=>p.x);
                lines.Add($"Feet at Y=0: negative X {left.Min():F4}..{left.Max():F4}; positive X {right.Min():F4}..{right.Max():F4}; taller bearing is source X=-1.025.");
            }
            Require(Mathf.Abs(bounds.min.y)<.001f && bounds.size.y>2.72f && bounds.size.y<2.75f, "origin");
            var textureTypes = new[] {"BaseColor","Normal","Mask"};
            var bindings = new[] {"_BaseMap","_BumpMap","_MetallicGlossMap"};
            for(int i=0;i<3;i++) {
                var path=Prefix+"Textures/BrokenArmillary_"+textureTypes[i]+".png";
                var tex=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                var settings=importer.GetPlatformTextureSettings("Android");
                Require(mat.GetTexture(bindings[i])==tex && tex.width==1024 && tex.height==1024 && tex.mipmapCount==11 && !importer.isReadable, "bound and mipmapped "+path);
                Require(tex.format==TextureFormat.ASTC_6x6 && settings.overridden && importer.sRGBTexture==(i==0) && (importer.textureType==TextureImporterType.NormalMap)==(i==1) && !importer.flipGreenChannel, "color/format "+path);
                lines.Add($"{bindings[i]} = {tex.name}, {tex.width}x{tex.height}, {tex.mipmapCount} mips, {tex.graphicsFormat}, sRGB={importer.sRGBTexture}, type={importer.textureType}, readable={importer.isReadable}");
            }
            Require(mat.GetTexture("_OcclusionMap")==mat.GetTexture("_MetallicGlossMap"),"packed AO");
            var keywords=new[]{"_NORMALMAP","_METALLICSPECGLOSSMAP","_OCCLUSIONMAP"};
            foreach(var k in keywords)Require(mat.IsKeywordEnabled(k),k);
            lines.Add($"Keywords {string.Join(" ",mat.shaderKeywords)}, AO = metallic atlas, one opaque material");
            lines.Add("PASS");File.WriteAllLines(output,lines);EditorApplication.Exit(0);
        }
        catch(Exception e){lines.Add(e.ToString());File.WriteAllLines(output,lines);Debug.LogException(e);EditorApplication.Exit(1);}
    }
}
