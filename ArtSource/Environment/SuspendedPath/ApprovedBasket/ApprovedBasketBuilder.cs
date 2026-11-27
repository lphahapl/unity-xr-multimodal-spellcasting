using System;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ApprovedBasketBuilder
{
    const string Folder = "Assets/Art/Environment/SuspendedPath/ApprovedBasket";
    const string WorkshopMaterials = "Assets/Art/Environment/SuspendedPath/MountainCraft/Materials/";
    const string BronzePath = "Assets/Art/Environment/Opening/PreqinGateFittings/GateHammeredBronze.mat";

    static T Load<T>(string path) where T : UnityEngine.Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (!asset) throw new InvalidOperationException("Missing " + path);
        return asset;
    }

    static Transform Named(string name)
    {
        return SceneManager.GetActiveScene().GetRootGameObjects()
            .SelectMany(go => go.GetComponentsInChildren<Transform>(true)).Single(t => t.name == name);
    }

    static void BuildBandMaterial()
    {
        var normalPath = Folder + "/ForgedBand_Normal.png";
        var ti = (TextureImporter)AssetImporter.GetAtPath(normalPath);
        ti.textureType = TextureImporterType.NormalMap;
        ti.sRGBTexture = false;
        ti.mipmapEnabled = true;
        ti.wrapMode = TextureWrapMode.Clamp;
        ti.maxTextureSize = 512;
        var android = ti.GetPlatformTextureSettings("Android");
        android.overridden = true;
        android.maxTextureSize = 512;
        android.format = TextureImporterFormat.ASTC_6x6;
        ti.SetPlatformTextureSettings(android);
        ti.SaveAndReimport();
        var path = Folder + "/ForgedBand.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!material)
        {
            material = new Material(Load<Material>(BronzePath));
            AssetDatabase.CreateAsset(material, path);
        }
        material.SetTexture("_BumpMap", Load<Texture2D>(normalPath));
        material.SetFloat("_BumpScale", .85f);
        material.EnableKeyword("_NORMALMAP");
        EditorUtility.SetDirty(material);
    }

    static Dictionary<string, Material> Materials()
    {
        return new Dictionary<string, Material> {
            {"Timber", Load<Material>(WorkshopMaterials + "Timber.mat")},
            {"EndGrain", Load<Material>(WorkshopMaterials + "EndGrain.mat")},
            {"ForgedBand", Load<Material>(Folder + "/ForgedBand.mat")},
            {"Ashlar", Load<Material>(WorkshopMaterials + "Ashlar.mat")},
            {"HammeredBronze", Load<Material>(BronzePath)}
        };
    }

    static GameObject BuildPrefab(string name, Dictionary<string, Material> materials)
    {
        var model = Load<GameObject>(Folder + "/" + name + ".fbx");
        var source = (GameObject)PrefabUtility.InstantiatePrefab(model);
        var root = new GameObject(name);
        try
        {
            var trs = source.GetComponentsInChildren<Transform>(true);
            Vector3 Marker(string key) => trs.Single(t => t.name == "Frame" + key).position;
            var basis = Matrix4x4.identity;
            if (name != "PivotBindings")
            {
                var origin = Marker("Origin");
                basis.SetColumn(0, Marker("X") - origin);
                basis.SetColumn(1, Marker("Y") - origin);
                basis.SetColumn(2, Marker("Z") - origin);
                basis.SetColumn(3, new Vector4(origin.x, origin.y, origin.z, 1));
            }
            var levels = new LOD[3];
            for (int tier = 0; tier < 3; tier++)
            {
                var sourceRenderer = source.GetComponentsInChildren<MeshRenderer>(true).Single(r => r.name.EndsWith("_LOD"+tier, StringComparison.Ordinal));
                var item = new GameObject(sourceRenderer.name);
                var tr = item.transform;
                tr.SetParent(root.transform, false);
                var matrix = basis.inverse * sourceRenderer.transform.localToWorldMatrix;
                Vector3 x = matrix.GetColumn(0), y = matrix.GetColumn(1), z = matrix.GetColumn(2);
                tr.localPosition = matrix.GetColumn(3);
                tr.localRotation = Quaternion.LookRotation(z.normalized, y.normalized);
                tr.localScale = new Vector3(Mathf.Sign(matrix.determinant)*x.magnitude,y.magnitude,z.magnitude);
                var mf = item.AddComponent<MeshFilter>();
                mf.sharedMesh = sourceRenderer.GetComponent<MeshFilter>().sharedMesh;
                var mr = item.AddComponent<MeshRenderer>();
                mr.sharedMaterials = sourceRenderer.sharedMaterials.Select(m => materials[m.name]).ToArray();
                levels[tier] = new LOD(new float[] {.36f,.12f,.003f}[tier],new Renderer[] {mr});
            }
            var group = root.AddComponent<LODGroup>();
            group.SetLODs(levels);
            group.RecalculateBounds();
            return PrefabUtility.SaveAsPrefabAsset(root,Folder + "/" + name + ".prefab");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(source);
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    static void AttachVisual(string target, GameObject prefab)
    {
        var parent = Named(target);
        var renderer = parent.GetComponent<MeshRenderer>();
        if (!renderer) throw new InvalidOperationException("Missing measured mesh on " + target);
        var child = parent.Find(prefab.name);
        if (!child)
        {
            child = ((GameObject)PrefabUtility.InstantiatePrefab(prefab,parent)).transform;
            child.SetLocalPositionAndRotation(Vector3.zero,Quaternion.identity);
            child.localScale = Vector3.one;
        }
        renderer.enabled = false;
    }

    static void DressLandingShoulder(Dictionary<string, Material> materials)
    {
        var deck = Named("天堑台 · 战斗地面");
        var original = AssetDatabase.LoadAllAssetsAtPath("Assets/Art/Environment/SuspendedPath/SuspendedPathGeometry.asset")
            .OfType<Mesh>().Single(m => m.name == deck.name);
        if (original.subMeshCount != 4) throw new InvalidOperationException("Unrecognised altar shell");
        var mesh = UnityEngine.Object.Instantiate(original);
        mesh.name = "AltarDeckRockUV";
        var pos = mesh.vertices;
        var uv = mesh.uv;
        for (int slot = 2; slot < 4; slot++)
        {
            // The two native rock strata share the same closed perimeter. A common metric projection
            // on each cardinal face keeps the 3.2 m bedding aligned across the material boundary.
            var tris = mesh.GetTriangles(slot);
            for (int i = 0; i < tris.Length; i += 3)
            {
                var a = pos[tris[i]];
                var b = pos[tris[i+1]];
                var c = pos[tris[i+2]];
                var center = (a+b+c)/3f;
                float dx = (center.x-14f)/14f, dz = (center.z-11f)/11f;
                bool eastWest = Mathf.Abs(dx) > Mathf.Abs(dz);
                for (int corner = 0; corner < 3; corner++)
                {
                    int k = tris[i+corner];
                    float u = eastWest ? -Mathf.Sign(dx)*pos[k].z : Mathf.Sign(dz)*pos[k].x;
                    uv[k] = new Vector2(u,pos[k].y)/3.2f;
                }
            }
        }
        mesh.uv = uv;
        mesh.RecalculateTangents();
        deck.GetComponent<MeshFilter>().sharedMesh = StoreMesh(mesh);
        var renderer = deck.GetComponent<MeshRenderer>();
        var slots = renderer.sharedMaterials;
        slots[0] = slots[1] = materials["Ashlar"];
        slots[2] = slots[3] = Load<Material>(WorkshopMaterials+"Rock.mat");
        renderer.sharedMaterials = slots;
        var shoulder = BuildPrefab("LandingShoulder",materials);
        if (!deck.Find(shoulder.name))
        {
            var instance = ((GameObject)PrefabUtility.InstantiatePrefab(shoulder,deck)).transform;
            instance.SetLocalPositionAndRotation(Vector3.zero,Quaternion.identity);
            instance.localScale = Vector3.one;
        }
    }

    static Mesh StoreMesh(Mesh mesh)
    {
        var path = Folder+"/"+mesh.name+".asset";
        var stored = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (!stored)
        {
            AssetDatabase.CreateAsset(mesh,path);
            return mesh;
        }
        EditorUtility.CopySerialized(mesh,stored);
        UnityEngine.Object.DestroyImmediate(mesh);
        EditorUtility.SetDirty(stored);
        return stored;
    }

    static void DressSocket(Dictionary<string,Material> materials)
    {
        var socket = Named("A 配重槽 · 净口 1.25");
        var original = AssetDatabase.LoadAllAssetsAtPath("Assets/Art/Environment/SuspendedPath/SuspendedPathGeometry.asset")
            .OfType<Mesh>().Single(m=>m.name==socket.name);
        var mesh = UnityEngine.Object.Instantiate(original);
        mesh.name = "AWeightSocket";
        if (mesh.subMeshCount!=3 || mesh.vertexCount!=180) throw new InvalidOperationException("Unrecognised A socket");
        var uv = mesh.uv;
        var pos = mesh.vertices;
        var normal = mesh.normals;
        for (int slot=0; slot<3; slot++)
        {
            foreach(int k in mesh.GetTriangles(slot).Distinct())
            {
                var p=pos[k];
                var n=normal[k];
                Vector2 value;
                if (slot==0)
                    value = Mathf.Abs(n.x)>.5f ? new Vector2(p.z,p.y) : new Vector2(p.x,Mathf.Abs(n.y)>.5f?p.z:p.y);
                else if (slot==1)
                    value = Mathf.Abs(n.z)>.5f ? new Vector2(p.x,p.y) : new Vector2(p.z,Mathf.Abs(n.y)>.5f?p.x:p.y);
                else
                    value = Mathf.Abs(n.y)>.5f ? new Vector2(p.x,p.z) : new Vector2(Mathf.Abs(n.z)>.5f?p.x:p.z,p.y);
                uv[k] = value/(slot==0?2f:slot==1?.72f:1.45f) + new Vector2(.23f,.47f);
            }
        }
        mesh.uv=uv;
        mesh.RecalculateTangents();
        socket.GetComponent<MeshFilter>().sharedMesh=StoreMesh(mesh);
        socket.GetComponent<MeshRenderer>().sharedMaterials=new[]{materials["Timber"],materials["HammeredBronze"],materials["Ashlar"]};

        var v=new List<Vector3>();
        var tri=new List<int>();
        var tex=new List<Vector2>();
        const int sides=9;
        foreach (var x in new[]{-.698f,.698f})
        foreach (var z in new[]{-.50f,-.17f,.17f,.50f})
        {
            int start=v.Count;
            for (int ring=0;ring<2;ring++)
            for (int j=0;j<sides;j++)
            {
                float angle=j*2f*Mathf.PI/sides+.19f;
                float radius=ring==0?.027f:.020f;
                v.Add(new Vector3(x+Mathf.Cos(angle)*radius,ring==0?.029f:.041f,z+Mathf.Sin(angle)*radius));
                tex.Add(new Vector2(.5f+Mathf.Cos(angle)*radius*5f,.5f+Mathf.Sin(angle)*radius*5f));
            }
            int center=v.Count;
            v.Add(new Vector3(x,.045f,z));
            tex.Add(new Vector2(.5f,.5f));
            for(int j=0;j<sides;j++)
            {
                int a=start+j,b=start+(j+1)%sides;
                tri.AddRange(new[]{a,a+sides,b,b,a+sides,b+sides,a+sides,center,b+sides});
            }
        }
        var pins=new Mesh{name="SocketRivetHeads"};
        pins.SetVertices(v);pins.SetUVs(0,tex);pins.SetTriangles(tri,0);
        pins.RecalculateNormals();pins.RecalculateTangents();pins.RecalculateBounds();
        var group=socket.Find("SocketRivetHeads");
        if(!group)
        {
            group=new GameObject("SocketRivetHeads",typeof(MeshFilter),typeof(MeshRenderer)).transform;
            group.SetParent(socket,false);
        }
        group.GetComponent<MeshFilter>().sharedMesh=StoreMesh(pins);
        group.GetComponent<MeshRenderer>().sharedMaterial=materials["HammeredBronze"];
    }

    static Texture2D CloseDetail(string name)
    {
        var path=Folder+"/"+name+".png";
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType=TextureImporterType.Default;
        importer.sRGBTexture=false;
        importer.alphaIsTransparency=false;
        importer.wrapMode=TextureWrapMode.Repeat;
        importer.mipmapEnabled=true;
        importer.anisoLevel=4;
        var platform=importer.GetPlatformTextureSettings("Android");
        platform.overridden=true;
        platform.maxTextureSize=1024;
        platform.format=TextureImporterFormat.ASTC_6x6;
        importer.SetPlatformTextureSettings(platform);
        importer.SaveAndReimport();
        return Load<Texture2D>(path);
    }

    static void DressTowerSurface(Transform tower)
    {
        var cast=CloseDetail("CastSurfaceDetail");
        var stone=CloseDetail("CutStoneDetail");
        var shader=Load<Shader>(Folder+"/H03CloseSurface.shader");
        var names=new[]{"HunyuanPivot","PivotStone"};
        var closeMaterials=new Material[names.Length];
        for(int i=0;i<names.Length;i++)
        {
            var source=Load<Material>(WorkshopMaterials+names[i]+".mat");
            var path=Folder+"/"+names[i]+"Close.mat";
            var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!mat)
            {
                mat=new Material(shader);
                AssetDatabase.CreateAsset(mat,path);
            }
            mat.shader=shader;
            foreach(var key in new[]{"_BaseMap","_BumpMap","_MetallicGlossMap"})
                mat.SetTexture(key,source.GetTexture(key));
            mat.SetColor("_BaseColor",source.GetColor("_BaseColor"));
            foreach(var key in new[]{"_BumpScale","_Metallic","_Smoothness","_SmoothnessTextureChannel"})
                if(mat.HasProperty(key) && source.HasProperty(key))mat.SetFloat(key,source.GetFloat(key));
            foreach(var keyword in new[]{"_METALLICSPECGLOSSMAP","_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A"})
            {
                if(source.IsKeywordEnabled(keyword))mat.EnableKeyword(keyword);
                else mat.DisableKeyword(keyword);
            }
            mat.SetTexture("_CastDetail",cast);
            mat.SetTexture("_StoneDetail",stone);
            mat.SetFloat("_ForceStone",i);
            mat.SetFloat("_FootY",tower.position.y);
            if(mat.GetTexture("_CastDetail")!=cast || mat.GetTexture("_StoneDetail")!=stone)
                throw new InvalidOperationException("H03 scan binding failed for "+mat.name);
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssetIfDirty(mat);
            closeMaterials[i]=mat;
        }
        foreach(var mr in tower.GetComponentsInChildren<MeshRenderer>(true))
        {
            if(mr.name!="HunyuanPivotTower_LOD0" && mr.name!="HunyuanPivotTower_LOD1")continue;
            var slots=mr.sharedMaterials;
            for(int s=0;s<slots.Length;s++)
            for(int i=0;i<names.Length;i++)
                if(slots[s] && (slots[s].name==names[i] || slots[s].name==names[i]+"Close"))
                    slots[s]=closeMaterials[i];
            mr.sharedMaterials=slots;
            PrefabUtility.RecordPrefabInstancePropertyModifications(mr);
        }
    }

    public static void Apply()
    {
        BuildBandMaterial();
        var materials = Materials();
        var basket = BuildPrefab("CounterweightBasket",materials);
        var rails = BuildPrefab("UpperLandingRail",materials);
        AttachVisual("A · 镇石配重吊篮",basket);
        AttachVisual("祭坛区石木护栏",rails);
        DressLandingShoulder(materials);
        DressSocket(materials);
        // The straps are ray-fitted to the original H03 FBX, in its own export frame.
        var tower = Named("混元支点塔 · 齿轮石座");
        var pivotMesh = tower.GetComponentsInChildren<MeshRenderer>(true).Single(r => r.name == "HunyuanPivotTower_LOD0");
        var towerFrame = pivotMesh.transform.parent;
        DressTowerSurface(towerFrame);
        var bindings = BuildPrefab("PivotBindings",materials);
        if (!towerFrame.Find(bindings.name))
        {
            var instance = ((GameObject)PrefabUtility.InstantiatePrefab(bindings,towerFrame)).transform;
            instance.SetLocalPositionAndRotation(Vector3.zero,Quaternion.identity);
            instance.localScale = Vector3.one;
        }
        AssetDatabase.SaveAssets();
    }
}
