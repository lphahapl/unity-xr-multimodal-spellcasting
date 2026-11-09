// Offline authoring; isolated checkout only. No scene save or existing-scene mutation.
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

public static class ExteriorLandscapeBuilder
{
    public const string PrefabPath="Assets/Art/Environment/Opening/MountainPolish/ValleyLandscape.prefab";
    const string Art="Assets/Art/Environment/Opening/MountainPolish/";
    // One continuous 80x80-cell heightfield: exactly 12,800 triangles / one material.
    public static GameObject Build()
    {
        Directory.CreateDirectory(Art);AssetDatabase.Refresh();
        const int n=80;
        var vertices=new Vector3[(n+1)*(n+1)];var uv=new Vector2[vertices.Length];
        var triangles=new int[n*n*6];int k=0;float coreMax=float.NegativeInfinity;
        for(int iz=0;iz<=n;iz++)for(int ix=0;ix<=n;ix++)
        {
            float x=Mathf.Lerp(-80,80,ix/(float)n),z=Mathf.Lerp(-45,135,iz/(float)n);
            int i=iz*(n+1)+ix;float y=Height(x,z);
            vertices[i]=new Vector3(x,y,z);uv[i]=new Vector2(x,z)*.16f;
            if(IsProtectedCore(x,z)&&!IsPortalPad(x,z))coreMax=Mathf.Max(coreMax,y);
            if(ix==n||iz==n)continue;
            int a=i,b=i+1,c=i+n+1,d=c+1;
            triangles[k++]=a;triangles[k++]=c;triangles[k++]=b;
            triangles[k++]=b;triangles[k++]=c;triangles[k++]=d;
        }
        if(coreMax>-1.499f)throw new Exception("Landscape obstructs protected route: "+coreMax);
        var mesh=new Mesh{name="ValleyLandscape"};mesh.vertices=vertices;mesh.uv=uv;mesh.triangles=triangles;
        mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();
        var stored=AssetDatabase.LoadAssetAtPath<Mesh>(Art+"ValleyLandscape.asset");
        if(stored){EditorUtility.CopySerialized(mesh,stored);Object.DestroyImmediate(mesh);EditorUtility.SetDirty(stored);}
        else {stored=mesh;AssetDatabase.CreateAsset(stored,Art+"ValleyLandscape.asset");}
        var source=AssetDatabase.LoadAssetAtPath<Material>(Art+"Slate_Near.mat");
        if(!source)throw new Exception("Run MountainPolishBuilder.Build before Landscape build");
        var material=AssetDatabase.LoadAssetAtPath<Material>(Art+"ValleySlate.mat");
        if(!material){material=new Material(source);AssetDatabase.CreateAsset(material,Art+"ValleySlate.mat");}
        else EditorUtility.CopySerialized(source,material);
        material.name="ValleySlate";material.SetColor("_BaseColor",new Color(.96f,.96f,.97f));
        material.SetTexture("_BaseMap",GroundTexture("ValleyScree_Albedo",false));
        material.SetTexture("_BumpMap",GroundTexture("ValleyScree_Normal",true));
        material.EnableKeyword("_NORMALMAP");material.SetFloat("_BumpScale",.28f);
        material.enableInstancing=true;EditorUtility.SetDirty(material);
        var root=new GameObject("连续谷地 · 缓坡层岩");
        try
        {
            root.AddComponent<MeshFilter>().sharedMesh=stored;
            var renderer=root.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=true;
            renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;renderer.lightProbeUsage=LightProbeUsage.Off;
            root.isStatic=true;
            var prefab=PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);AssetDatabase.SaveAssets();
            Directory.CreateDirectory("Logs/exterior-polish-20261108/mountains");
            File.WriteAllText("Logs/exterior-polish-20261108/mountains/landscape-build.txt",
                "Bounds x[-80,80],z[-45,135]; cells80x80; triangles12800; vertices6561; renderer1; material1; colliders0; scripts0.\n"+
                "Protected core max="+coreMax+"; endpoint edges sunk to-12m with smooth slope.\n"+
                "Only portal pad x7.1..12.9,z60.1..65.9 may rise to-.70 per parent instruction; main route x1..4 remains below-1.5.\n"+
                "Pavilion pad centers(-22,8),(26,25),(28,66) at-.55, with broad approach slopes.\n"+
                "Disable old 破碎峡谷底床 renderer; its top-1.4 would otherwise cover the protected valley.\n");
            return prefab;
        }
        finally{Object.DestroyImmediate(root);}
    }
    static Texture2D GroundTexture(string name,bool normal)
    {
        string path=Art+name+".png";var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        if(!importer)throw new Exception("Run MountainPolish generate.py before building valley textures: "+path);
        importer.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;
        importer.sRGBTexture=!normal;importer.wrapMode=TextureWrapMode.Repeat;
        importer.mipmapEnabled=true;importer.filterMode=FilterMode.Trilinear;importer.anisoLevel=2;importer.maxTextureSize=512;
        importer.textureCompression=TextureImporterCompression.CompressedHQ;
        var android=importer.GetPlatformTextureSettings("Android");android.overridden=true;android.format=TextureImporterFormat.ASTC_6x6;android.maxTextureSize=512;importer.SetPlatformTextureSettings(android);
        importer.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
    static float Ease(float from,float to,float value)=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(from,to,value));
    public static bool IsProtectedCore(float x,float z)=>Mathf.Abs(x)<=10&&z>=-5&&z<=70;
    public static bool IsPortalPad(float x,float z)=>x>=7.1f&&x<=12.9f&&z>=60.1f&&z<=65.9f;
    public static float Height(float x,float z)
    {
        float d=Mathf.Max(Mathf.Abs(x)-10,Mathf.Max(-5-z,z-70));
        float rise=Ease(0,34,Mathf.Max(0,d));
        float broad=Mathf.PerlinNoise((x+151)*.019f,(z+93)*.017f);
        float fold=Mathf.Sin(x*.066f+Mathf.Sin(z*.032f)*1.2f)*.55f+Mathf.Cos(z*.049f+x*.019f)*.45f;
        float basin=-3.15f+Mathf.Sin(z*.065f+x*.018f)*.36f+Mathf.Sin(x*.11f-z*.047f)*.22f;
        float outer=2.1f+broad*5.1f+fold*.75f;
        float y=Mathf.Lerp(basin,outer,rise);
        // Bedrock ledges are broad curved shelves, never disconnected rectangular blocks.
        y+=Mathf.Sin((d+z*.09f)*.30f)*.22f*rise;
        Pad(ref y,x,z,-22,8,5.8f,11f,-.55f);
        Pad(ref y,x,z,26,25,6.5f,12f,-.55f);
        Pad(ref y,x,z,28,66,7f,13f,-.55f);
        // Leave aqueduct column plinths visible in their existing sunken setting.
        float aqueduct=Mathf.Sqrt(Mathf.Pow((x+22)/12,2)+Mathf.Pow((z-65)/5,2));
        y=Mathf.Lerp(-2.25f,y,Ease(.8f,1.6f,aqueduct));
        // End the heightfield beneath overlapping ridges, with zero-slope boundary.
        float edge=Mathf.Min(80-Mathf.Abs(x),Mathf.Min(z+45,135-z));
        y=Mathf.Lerp(-12,y,Ease(0,19,edge));
        if(IsProtectedCore(x,z))y=Mathf.Min(y,-1.5f);
        // Approved side landing exception: under stone only, never the x~2.65 route.
        float portalDistance=Mathf.Max(Mathf.Abs(x-10)/2.9f,Mathf.Abs(z-63)/2.9f);
        float portalBlend=1-Ease(.62f,1,portalDistance);
        if(IsPortalPad(x,z))y=Mathf.Lerp(y,-.70f,portalBlend);
        return y;
    }
    static void Pad(ref float height,float x,float z,float px,float pz,float inner,float outer,float elevation)
    {
        float distance=Vector2.Distance(new Vector2(x,z),new Vector2(px,pz));
        height=Mathf.Lerp(elevation,height,Ease(inner,outer,distance));
    }
}
