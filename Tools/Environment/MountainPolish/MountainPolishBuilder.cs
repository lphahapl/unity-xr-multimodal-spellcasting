using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

// Offline tool: copy to Assets/Editor in an isolated authoring checkout, invoke
// -executeMethod MountainPolishBuilder.Build. Creates assets/prefab only, never saves a scene.
public static class MountainPolishBuilder
{
    const string Art = "Assets/Art/Environment/Opening/MountainPolish/";
    static readonly string[] Names = {"Ridge_BrokenCrown","Ridge_LongShoulder","Ridge_SplitSaddle","Ridge_WeatheredSpine","Bluff_BeddedSlate"};
    public static void Build()
    {
        AssetDatabase.Refresh();
        foreach (var name in Names)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(Art+name+".obj");
            if (!importer) throw new Exception("Generate OBJ assets before building prefab: "+name);
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importCameras = false; importer.importLights = false;
            importer.addCollider = false; importer.isReadable = false;
            importer.globalScale = 1; importer.SaveAndReimport();
        }
        var albedo = Texture("WeatheredSlate_Albedo",false,true);
        var normal = Texture("WeatheredSlate_Normal",true,false);
        var smooth = Texture("WeatheredSlate_MetalSmooth",false,false);
        var near = Material("Slate_Near",new Color(.88f,.93f,1.0f),albedo,normal,smooth,.20f);
        var mid = Material("Slate_Middle",new Color(.97f,1.02f,1.08f),albedo,normal,smooth,.25f);
        var far = Material("Slate_Far",new Color(1.08f,1.14f,1.22f),albedo,normal,smooth,0f);
        var root = new GameObject("山川重塑 · 层岩与远岫");
        try
        {
            // Object footprint is 64 x 42 m for ridges, 22 x 17 for bluffs; base elevation -1m.
            Add(root,0,"西窗 · 断冠山",new Vector3(-59,-11,9),new Vector3(1.5f,.80f,1.65f),72,mid);
            Add(root,1,"东窗 · 长肩山",new Vector3(62,-11,27),new Vector3(1.6f,.85f,1.65f),-74,mid);
            Add(root,2,"北岭 · 双鞍",new Vector3(-27,-11,101),new Vector3(1.55f,.90f,1.45f),9,mid);
            Add(root,3,"北东 · 风蚀脊",new Vector3(42,-11,105),new Vector3(1.5f,.82f,1.5f),-17,mid);
            Add(root,2,"远西 · 雾中层山",new Vector3(-104,-13,45),new Vector3(2.2f,1.10f,1.7f),75,far);
            Add(root,0,"远东 · 叠峰",new Vector3(110,-14,60),new Vector3(2.2f,1.15f,1.8f),-76,far);
            Add(root,1,"远北 · 横岫",new Vector3(-12,-14,151),new Vector3(2.7f,1.10f,1.6f),5,far);
            Add(root,3,"回望 · 南山",new Vector3(0,-14,-67),new Vector3(2.7f,.90f,1.6f),-4,far);
            // v4 actual renders showed these five broad bluffs intersecting the three
            // workshop pavilions. Keep reusable source mesh, omit all near instances;
            // parent-authored StrataRock dressing now owns near-field stone forms.
            var prefab = PrefabUtility.SaveAsPrefabAsset(root,Art+"MountainPolish.prefab");
            AssetDatabase.SaveAssets();
            var renderers=root.GetComponentsInChildren<MeshRenderer>();
            var triangles=root.GetComponentsInChildren<MeshFilter>().Sum(f=>f.sharedMesh.triangles.Length/3);
            var report="MountainPolishBuilder: triangles="+triangles+", renderers="+renderers.Length+", unique materials="+renderers.Select(r=>r.sharedMaterial).Distinct().Count()+", colliders="+root.GetComponentsInChildren<Collider>().Length+", scripts="+root.GetComponentsInChildren<MonoBehaviour>().Length+", prefab="+AssetDatabase.GetAssetPath(prefab);
            Directory.CreateDirectory("Logs/exterior-polish-20261108/mountains");
            File.WriteAllText("Logs/exterior-polish-20261108/mountains/unity-build.txt",report);
            Debug.Log(report);
        }
        finally { Object.DestroyImmediate(root); }
    }
    static Texture2D Texture(string name,bool normal,bool srgb)
    {
        var p=Art+name+".png"; var t=(TextureImporter)AssetImporter.GetAtPath(p);
        t.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;
        t.sRGBTexture=srgb;t.mipmapEnabled=true;t.wrapMode=TextureWrapMode.Repeat;
        t.filterMode=FilterMode.Trilinear;t.anisoLevel=2;t.maxTextureSize=512;
        t.textureCompression=TextureImporterCompression.CompressedHQ;
        var android=t.GetPlatformTextureSettings("Android");android.overridden=true;android.format=TextureImporterFormat.ASTC_6x6;android.maxTextureSize=512;t.SetPlatformTextureSettings(android);
        t.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Texture2D>(p);
    }
    static Material Material(string name,Color tint,Texture2D a,Texture2D n,Texture2D s,float normalScale)
    {
        var p=Art+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(p);
        if (!m) { m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,p); }
        m.SetColor("_BaseColor",tint);m.SetTexture("_BaseMap",a);m.SetTexture("_BumpMap",n);
        m.SetFloat("_BumpScale",normalScale);m.SetTexture("_MetallicGlossMap",s);m.SetFloat("_Metallic",0);
        m.SetFloat("_Smoothness",1);m.SetFloat("_SmoothnessTextureChannel",0);
        m.SetFloat("_SpecularHighlights",0);m.SetFloat("_EnvironmentReflections",0);
        if(normalScale>0)m.EnableKeyword("_NORMALMAP");else{m.DisableKeyword("_NORMALMAP");m.SetTexture("_BumpMap",null);}
        m.EnableKeyword("_METALLICSPECGLOSSMAP");
        m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        m.enableInstancing=true;EditorUtility.SetDirty(m);return m;
    }
    static void Add(GameObject root,int shape,string name,Vector3 p,Vector3 scale,float yaw,Material material)
    {
        var source=AssetDatabase.LoadAssetAtPath<GameObject>(Art+Names[shape]+".obj");
        var mesh=source.GetComponentInChildren<MeshFilter>().sharedMesh;
        var go=new GameObject(name);go.transform.SetParent(root.transform,false);
        go.transform.localPosition=p;go.transform.localRotation=Quaternion.Euler(0,yaw,0);go.transform.localScale=scale;
        go.AddComponent<MeshFilter>().sharedMesh=mesh;
        var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
        renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=shape==4;
        renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
        go.isStatic=true;
    }
}
