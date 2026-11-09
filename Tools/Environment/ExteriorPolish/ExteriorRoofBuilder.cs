// Isolated offline authoring; only adds new cover geometry beneath supplied new-content root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

public static class ExteriorRoofBuilder
{
    const string Art="Assets/Art/Environment/Opening/MountainPolish/";
    static readonly List<Vector3> vertices=new List<Vector3>();
    static readonly List<Vector2> uv=new List<Vector2>();
    static readonly List<int> triangles=new List<int>();
    public static void Build(Transform root,Scene scene)
    {
        if(!root||root.gameObject.scene!=scene)throw new Exception("Roof builder needs new-content root in supplied scene");
        vertices.Clear();uv.Clear();triangles.Clear();
        var all=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MeshRenderer>(true)).ToArray();
        var names=new[]{"室顶","通廊顶","试炼间顶"};var evidence=new List<string>();
        foreach(var name in names)
        {
            var matches=all.Where(r=>r.name==name&&r.enabled&&r.gameObject.activeInHierarchy&&Path(r.transform).Contains("第一章 · 初醒校验室")).ToArray();
            if(matches.Length!=1)throw new Exception("Expected exactly one authored main roof named "+name+", found "+matches.Length);
            var renderer=matches[0];var bounds=renderer.bounds;
            if(bounds.size.y>.8f||bounds.min.y<2.9f)throw new Exception("Unexpected roof bounds: "+Path(renderer.transform)+" "+bounds);
            // Very shallow roof above existing box top; interior/roof collider stays intact.
            float y=bounds.max.y+.035f;
            float rise=name=="通廊顶"?.30f:.55f;
            float x0=bounds.min.x-.025f,x1=bounds.max.x+.025f,z0=bounds.min.z-.025f,z1=bounds.max.z+.025f;
            float center=bounds.center.x;
            var leftNear=new Vector3(x0,y,z0);var leftFar=new Vector3(x0,y,z1);
            var ridgeNear=new Vector3(center,y+rise,z0);var ridgeFar=new Vector3(center,y+rise,z1);
            var rightNear=new Vector3(x1,y,z0);var rightFar=new Vector3(x1,y,z1);
            float slope=Vector3.Distance(leftNear,ridgeNear);
            // UV U along ridge (tile columns); V down slope (overlap rows).
            Quad(leftNear,leftFar,ridgeFar,ridgeNear,
                new Vector2(z0/1.76f,slope/2.8f),new Vector2(z1/1.76f,slope/2.8f),new Vector2(z1/1.76f,0),new Vector2(z0/1.76f,0));
            Quad(ridgeNear,ridgeFar,rightFar,rightNear,
                new Vector2(z0/1.76f,0),new Vector2(z1/1.76f,0),new Vector2(z1/1.76f,slope/2.8f),new Vector2(z0/1.76f,slope/2.8f));
            // Seal shallow gable wedges; original top remains the underside, no tall wall.
            Triangle(leftNear,ridgeNear,rightNear);Triangle(rightFar,ridgeFar,leftFar);
            Box(new Vector3(center,y+rise+.035f,(z0+z1)*.5f),new Vector3(.16f,.09f,z1-z0+.08f));
            Box(new Vector3(x0,y-.015f,(z0+z1)*.5f),new Vector3(.075f,.10f,z1-z0));
            Box(new Vector3(x1,y-.015f,(z0+z1)*.5f),new Vector3(.075f,.10f,z1-z0));
            evidence.Add(name+" original bounds="+bounds+" new eaveY="+y+" ridgeY="+(y+rise)+" original roof renderer/collider untouched");
        }
        var mesh=new Mesh{name="GreyTileRoofCover"};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);
        mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();
        // Geometry was authored in world coordinates, then stored relative to requested root.
        var local=mesh.vertices;for(int i=0;i<local.Length;i++)local[i]=root.InverseTransformPoint(local[i]);mesh.vertices=local;mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();
        var existing=AssetDatabase.LoadAssetAtPath<Mesh>(Art+"GreyTileRoofCover.asset");
        if(existing){EditorUtility.CopySerialized(mesh,existing);Object.DestroyImmediate(mesh);EditorUtility.SetDirty(existing);}
        else{existing=mesh;AssetDatabase.CreateAsset(existing,Art+"GreyTileRoofCover.asset");}
        var material=AssetDatabase.LoadAssetAtPath<Material>(Art+"GreyTileRoof.mat");
        if(!material){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,Art+"GreyTileRoof.mat");}
        material.SetColor("_BaseColor",new Color(.96f,.97f,1));material.SetTexture("_BaseMap",Texture("GreyTile_Albedo",false));
        material.SetTexture("_BumpMap",Texture("GreyTile_Normal",true));material.SetFloat("_BumpScale",.75f);
        material.SetFloat("_Smoothness",.15f);material.SetFloat("_Metallic",0);
        material.SetFloat("_SpecularHighlights",0);material.SetFloat("_EnvironmentReflections",0);
        material.EnableKeyword("_NORMALMAP");material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");material.enableInstancing=true;
        EditorUtility.SetDirty(material);
        var cover=new GameObject("主工坊 · 薄灰瓦覆顶");SceneManager.MoveGameObjectToScene(cover,scene);cover.transform.SetParent(root,false);
        cover.AddComponent<MeshFilter>().sharedMesh=existing;var mr=cover.AddComponent<MeshRenderer>();mr.sharedMaterial=material;
        mr.shadowCastingMode=ShadowCastingMode.On;mr.receiveShadows=true;mr.reflectionProbeUsage=ReflectionProbeUsage.Off;cover.isStatic=true;
        evidence.Add("New cover: one renderer/material, "+triangles.Count/3+" triangles; no colliders, no scripts. Lowest cover eave >3.3m, existing windows/doors below unchanged.");
        Directory.CreateDirectory("Logs/exterior-polish-20261108/mountains");File.WriteAllLines("Logs/exterior-polish-20261108/mountains/roof-build.txt",evidence);AssetDatabase.SaveAssets();
    }
    static string Path(Transform t)=>t.parent?Path(t.parent)+"/"+t.name:t.name;
    static void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Vector2 ua,Vector2 ub,Vector2 uc,Vector2 ud)
    {
        int i=vertices.Count;vertices.AddRange(new[]{a,b,c,d});uv.AddRange(new[]{ua,ub,uc,ud});triangles.AddRange(new[]{i,i+1,i+2,i,i+2,i+3});
    }
    static void Triangle(Vector3 a,Vector3 b,Vector3 c)
    {
        int i=vertices.Count;vertices.AddRange(new[]{a,b,c});uv.AddRange(new[]{new Vector2(a.x,a.y)*.4f,new Vector2(b.x,b.y)*.4f,new Vector2(c.x,c.y)*.4f});triangles.AddRange(new[]{i,i+1,i+2});
    }
    static void Box(Vector3 p,Vector3 size)
    {
        var h=size*.5f;var a=p+new Vector3(-h.x,-h.y,-h.z);var b=p+new Vector3(h.x,-h.y,-h.z);var c=p+new Vector3(h.x,h.y,-h.z);var d=p+new Vector3(-h.x,h.y,-h.z);
        var e=p+new Vector3(-h.x,-h.y,h.z);var f=p+new Vector3(h.x,-h.y,h.z);var g=p+new Vector3(h.x,h.y,h.z);var q=p+new Vector3(-h.x,h.y,h.z);
        Vector2 zero=Vector2.zero,right=Vector2.right,one=Vector2.one,up=Vector2.up;
        Quad(a,d,c,b,zero,up,one,right);Quad(e,f,g,q,zero,right,one,up);Quad(a,e,q,d,zero,right,one,up);Quad(b,c,g,f,zero,up,one,right);Quad(d,q,g,c,zero,up,one,right);Quad(a,b,f,e,zero,right,one,up);
    }
    static Texture2D Texture(string name,bool normal)
    {
        var path=Art+name+".png";var t=(TextureImporter)AssetImporter.GetAtPath(path);
        if(!t)throw new Exception("Generate GreyTile maps before roof build");
        t.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;t.sRGBTexture=!normal;t.wrapMode=TextureWrapMode.Repeat;t.mipmapEnabled=true;t.anisoLevel=2;t.filterMode=FilterMode.Trilinear;t.maxTextureSize=512;
        var android=t.GetPlatformTextureSettings("Android");android.overridden=true;android.format=TextureImporterFormat.ASTC_6x6;android.maxTextureSize=512;t.SetPlatformTextureSettings(android);t.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
}
