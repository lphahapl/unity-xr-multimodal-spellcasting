using System;
using System.Linq;
using System.Globalization;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;

public static class ApprovedGorgeBuilder
{
    const string AssetsRoot = "Assets/Art/Environment/SuspendedPath/ApprovedGorge/";
    const string TerrainName = "断衡悬道 · 定稿山谷";
    static readonly Vector3 Center = new Vector3(35.6f, 0f, 24f);
    static readonly Vector2[] Corners = { new(-1280,-1280),new(1280,-1280),new(-1280,1280),new(1280,1280) };

    static Texture2D Prepare(string file, bool data, bool cutout=false)
    {
        string path=AssetsRoot+file;
        var importer=AssetImporter.GetAtPath(path) as TextureImporter;
        if (!importer) throw new InvalidOperationException("Missing terrain map "+path);
        importer.textureType=TextureImporterType.Default;
        importer.sRGBTexture=!data;
        importer.alphaIsTransparency=cutout;
        importer.mipmapEnabled=true;
        importer.mipMapsPreserveCoverage=cutout;
        if(cutout) importer.alphaTestReferenceValue=.32f;
        importer.wrapMode=data ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
        importer.filterMode=FilterMode.Trilinear;
        importer.anisoLevel=data ? 4 : 2;
        var android=importer.GetPlatformTextureSettings("Android");
        android.name="Android";android.overridden=true;
        android.maxTextureSize=data || cutout ? 1024 : 2048;
        android.format=TextureImporterFormat.ASTC_6x6;
        importer.SetPlatformTextureSettings(android);
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static GameObject BuildPrefab(Material material, Material treeMaterial)
    {
        var root=new GameObject(TerrainName);
        try
        {
            var heights=AssetDatabase.LoadAssetAtPath<TextAsset>(AssetsRoot+"GorgeCorners.txt");
            if (!heights) throw new InvalidOperationException("Missing exported terrain corner heights.");
            var cornerHeights=heights.text.Trim().Split(',').Select(s=>float.Parse(s,CultureInfo.InvariantCulture)).ToArray();
            var group=root.AddComponent<LODGroup>();
            var levels=new LOD[3];
            Vector3 horizontalAxes=Vector3.one;
            for(int n=0;n<3;n++)
            {
                string path=AssetsRoot+"Gorge_LOD"+n+".fbx";
                var model=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (!model) throw new InvalidOperationException("Missing terrain mesh "+path);
                var go=(GameObject)PrefabUtility.InstantiatePrefab(model);
                go.transform.SetParent(root.transform,false);
                var filter=go.GetComponentInChildren<MeshFilter>();
                // Each corner has a distinct measured height. Match the two
                // horizontal axes regardless of the installed FBX axis convention.
                var pts=filter.sharedMesh.vertices.Select(filter.transform.TransformPoint).ToArray();
                bool matched=false;
                foreach(int sx in new[]{1,-1})
                foreach(int sz in new[]{1,-1})
                {
                    var ok=true;
                    for(int c=0;c<4;c++)
                    {
                        ok &= pts.Any(v=>Mathf.Abs(sx*v.x-Corners[c].x)<.08f &&
                                     Mathf.Abs(sz*v.z-Corners[c].y)<.08f &&
                                     Mathf.Abs(v.y-cornerHeights[c])<.10f);
                    }
                    if(ok) { go.transform.localScale=new Vector3(sx,1,sz);matched=true; }
                }
                if(!matched) throw new InvalidOperationException("Unexpected FBX orientation "+path);
                if(n==0) horizontalAxes=go.transform.localScale;
                var renderers=go.GetComponentsInChildren<MeshRenderer>();
                foreach(var mr in renderers)
                {
                    mr.sharedMaterial=material;
                    mr.shadowCastingMode=ShadowCastingMode.Off;
                    mr.receiveShadows=false;
                    mr.lightProbeUsage=LightProbeUsage.Off;
                    mr.reflectionProbeUsage=ReflectionProbeUsage.Off;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(mr);
                }
                PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
                levels[n]=new LOD(n==0?.67f:n==1?.19f:.004f,renderers);
            }
            group.SetLODs(levels);group.RecalculateBounds();
            var stands=AssetDatabase.LoadAssetAtPath<GameObject>(AssetsRoot+"PineStands.fbx");
            if(!stands) throw new InvalidOperationException("Missing pine stands");
            var grove=(GameObject)PrefabUtility.InstantiatePrefab(stands);
            grove.transform.SetParent(root.transform,false);
            grove.transform.localScale=horizontalAxes;
            var trees=grove.GetComponentInChildren<MeshRenderer>();
            trees.sharedMaterial=treeMaterial;
            trees.shadowCastingMode=ShadowCastingMode.Off;
            trees.receiveShadows=false;
            trees.lightProbeUsage=LightProbeUsage.Off;
            trees.reflectionProbeUsage=ReflectionProbeUsage.Off;
            PrefabUtility.RecordPrefabInstancePropertyModifications(trees);
            PrefabUtility.RecordPrefabInstancePropertyModifications(grove.transform);
            var prefab=PrefabUtility.SaveAsPrefabAsset(root,AssetsRoot+"ApprovedGorge.prefab");
            if (!prefab) throw new InvalidOperationException("Could not create terrain prefab");
            return prefab;
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    public static void Apply()
    {
        var scene=SceneManager.GetActiveScene();
        if(!scene.IsValid() || scene.name!="Chapter02_SuspendedPath")
            throw new InvalidOperationException("Run in the open Chapter02_SuspendedPath scene.");
        var roots=scene.GetRootGameObjects();
        var oldBackdrop=roots.Single(o=>o.name=="崖谷 · 周边岩脊");
        var oldFloor=roots.SelectMany(o=>o.GetComponentsInChildren<MeshRenderer>(true)).Single(r=>r.name=="连续谷底地面");
        var shader=Shader.Find("NarutoVR/Approved Gorge");
        if(!shader || !shader.isSupported) throw new InvalidOperationException("Gorge shader unavailable.");
        var material=AssetDatabase.LoadAssetAtPath<Material>(AssetsRoot+"GorgeStrata.mat");
        if(!material){material=new Material(shader);AssetDatabase.CreateAsset(material,AssetsRoot+"GorgeStrata.mat");}
        material.shader=shader;
        material.SetTexture("_BaseMap",Prepare("GorgeSurface.png",false));
        material.SetTexture("_Detail",Prepare("StrataDetail.png",true));
        material.SetTexture("_Stone",AssetDatabase.LoadAssetAtPath<Texture2D>(
            "Assets/Art/Environment/SuspendedPath/Surfaces/Geology/CH_Rock_BaseColor.png"));
        material.SetTexture("_Canopy",Prepare("CanopyShade.png",true));
        material.SetFloat("_TileMeters",3.2f);
        EditorUtility.SetDirty(material);
        var treeShader=Shader.Find("NarutoVR/Gorge Pine Stands");
        if(!treeShader || !treeShader.isSupported) throw new InvalidOperationException("Gorge pine shader unavailable.");
        var treeMaterial=AssetDatabase.LoadAssetAtPath<Material>(AssetsRoot+"PineCanopy.mat");
        if(!treeMaterial){treeMaterial=new Material(treeShader);AssetDatabase.CreateAsset(treeMaterial,AssetsRoot+"PineCanopy.mat");}
        treeMaterial.shader=treeShader;
        treeMaterial.SetTexture("_BaseMap",Prepare("PineAtlas.png",false,true));
        EditorUtility.SetDirty(treeMaterial);
        var prefab=BuildPrefab(material,treeMaterial);
        var inst=roots.FirstOrDefault(o=>o.name==TerrainName);
        if(!inst) inst=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);
        inst.transform.position=Center;
        PrefabUtility.RecordPrefabInstancePropertyModifications(inst.transform);

        // The 580 x 540 m grey rectangle was the lower court's giant visual.
        // The sculpt includes the same y=-80 playing surface in the lower court.
        // Retain its original collider and the mechanism's raycast affordances.
        oldFloor.enabled=false;
        foreach(var g in oldBackdrop.GetComponentsInChildren<LODGroup>(true)) g.enabled=false;
        foreach(var r in oldBackdrop.GetComponentsInChildren<Renderer>(true)) r.enabled=false;
        foreach(var c in roots.SelectMany(o=>o.GetComponentsInChildren<Camera>(true))) c.farClipPlane=Mathf.Max(c.farClipPlane,1650);
        RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;
        RenderSettings.fogColor=new Color(.50f,.63f,.75f,1);RenderSettings.fogDensity=.00100f;
        var horizon=Prepare("GorgeHorizon.jpg",false);
        var skyShader=Shader.Find("Skybox/Panoramic");
        if(!skyShader) throw new InvalidOperationException("Missing panoramic sky shader.");
        var sky=AssetDatabase.LoadAssetAtPath<Material>(AssetsRoot+"GorgeSky.mat");
        if(!sky){sky=new Material(skyShader);AssetDatabase.CreateAsset(sky,AssetsRoot+"GorgeSky.mat");}
        sky.shader=skyShader;
        sky.SetTexture("_MainTex",horizon);
        sky.SetFloat("_Mapping",1);
        sky.SetFloat("_Rotation",0);
        EditorUtility.SetDirty(sky);
        RenderSettings.skybox=sky;
        AssetDatabase.SaveAssets();
        Debug.Log("[ApprovedGorge] Three metric retopology LODs; preserved valley collider and original near walls. Use 1650 m far clip for the review camera.");
    }
}
