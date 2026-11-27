using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using NarutoVR.SealCombat.Items;
using NarutoVR.WorkshopTenonR03Review;
using NeuGameMRTools.Runtime.Components;
using Object=UnityEngine.Object;

public static class WorkshopTenonR03LabBuild
{
    public const string Root="Assets/Review/WorkshopMasterTenonR03";
    public const string Output="D:/UnityProjects/NarutoVR/Logs/workshop-master-tenon-r03-20261125/unity";
    public const string Delivery="D:/UnityProjects/NarutoVR/ArtSource/Props/WorkshopMasterTenon/R03/UnityReview";
    public const string ScenePath=Root+"/WorkshopMasterTenonR03_Lab.unity";
    public static readonly Vector3 ConsolePosition=new(.85f,0,0),DisplayPosition=new(-.85f,.653f,0);
    public static readonly Vector3 OpeningLocal=new(-.12f,1.028f,0),SeatLocal=new(-.12f,.9239401f,0);
    public static WorkshopTenonR03LabController Build()
    {
        Directory.CreateDirectory(Output);Directory.CreateDirectory(Delivery);
        Directory.CreateDirectory(Root+"/Generated");AssetDatabase.Refresh();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        ConfigureModel();
        var material=TenonMaterial();
        var dark=Material("LabCharcoal",new Color(.08f,.10f,.115f),.0f,.25f);
        var stone=Material("DisplayStone",new Color(.18f,.21f,.22f),.0f,.35f);
        var clay=Material("HandScaleClay",new Color(.39f,.46f,.48f),.0f,.32f);
        var root=new GameObject("R03 Independent Workshop Review (not chapter layout)");
        Cube("Ground",new Vector3(0,-.05f,0),new Vector3(6,.1f,5),dark,root.transform);
        Cube("03 Display example 0.85 x 0.85 x 0.65 m",new Vector3(-.85f,.325f,0),new Vector3(.85f,.65f,.85f),stone,root.transform);
        var console=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/ReferenceConsole/TenonConsole.prefab"));
        console.name="04 Actual production TenonConsole reference";console.transform.SetParent(root.transform);console.transform.position=ConsolePosition;
        var physical=new List<MeshCollider>();
        foreach(var group in console.GetComponentsInChildren<LODGroup>())group.ForceLOD(0);
        foreach(var f in console.GetComponentsInChildren<MeshFilter>(true)) {
            if(!f.sharedMesh || !f.name.Contains("LOD0"))continue;
            var c=f.GetComponent<MeshCollider>();if(!c)c=f.gameObject.AddComponent<MeshCollider>();c.sharedMesh=f.sharedMesh;c.convex=false;physical.Add(c);
        }
        if(physical.Count!=2)throw new Exception("Expected actual Body and Socket LOD0 mesh colliders");
        var item=BuildItem(material);
        var itemPrefab=PrefabUtility.SaveAsPrefabAsset(item.gameObject,Root+"/WorkshopMasterTenonR03.prefab");
        Object.DestroyImmediate(item.gameObject);
        item=((GameObject)PrefabUtility.InstantiatePrefab(itemPrefab)).GetComponent<GameItemBase>();item.transform.SetParent(root.transform);item.transform.position=DisplayPosition;
        var opening=Anchor("Actual opening reference y=1.028",ConsolePosition+OpeningLocal,root.transform);
        var align=Anchor("Pre-align 30 mm above opening",opening.position+Vector3.up*.030f,root.transform);
        var seat=Anchor("Sample seat / 104.06 mm insertion / 4 mm body clearance",ConsolePosition+SeatLocal,root.transform);
        var z=new GameObject("Master-tenon-purpose socket");z.transform.SetParent(root.transform);z.transform.position=opening.position;
        var zone=z.AddComponent<WorkshopTenonR03ReviewZone>();zone.expectedTenon=item;zone.opening=opening;zone.alignment=align;zone.seat=seat;zone.actualConsoleColliders=physical.ToArray();
        zone.conservativeGauge=item.stowEnvelope;zone.contactColliders=item.GetComponentsInChildren<BoxCollider>().Where(c=>c!=item.stowEnvelope).ToArray();
        var triangles=new List<Vector3>();
        foreach(var collider in physical) {var v=collider.sharedMesh.vertices;foreach(int index in collider.sharedMesh.triangles)triangles.Add(zone.transform.InverseTransformPoint(collider.transform.TransformPoint(v[index])));}
        zone.actualConsoleTriangles=triangles.ToArray();
        ConfigureZone(zone,align,seat);
        var controller=root.AddComponent<WorkshopTenonR03LabController>();controller.tenon=item;controller.socket=zone;
        controller.displayOrigin=Anchor("03 Start display origin",DisplayPosition,root.transform);
        controller.inspectionOrigin=Anchor("Near-hand 1:1 inspection origin",new Vector3(-.85f,1.04f,-.12f),root.transform);
        controller.handScaleReference=BuildHand(clay,root.transform);
        controller.handScaleReference.position=controller.inspectionOrigin.position+new Vector3(.18f,.11f,.02f);
        controller.handScaleReference.rotation=Quaternion.Euler(0,0,0);
        Label("03  TAKE / DISPLAY EXAMPLE",new Vector3(-.85f,.50f,-.431f),root.transform);
        Label("04  INSERT / ACTUAL CONSOLE",new Vector3(.85f,.58f,-.805f),root.transform);
        CameraLight(root.transform);
        WorkshopTenonR03ClampReview.BuildAssets();
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),ScenePath);
        AssetDatabase.SaveAssets();
        File.WriteAllText(Output+"/assembly-values.json",JsonUtility.ToJson(new AssemblyValues{opening=opening.position,seat=seat.position,insertDepth=opening.position.y-seat.position.y,bodyMaxY=.9199401f,minimumBodyClearance=.004f,objectSize=new Vector3(.12f,.35f,.12f),gripHeight=item.grip.localPosition.y,displayPosition=DisplayPosition,consolePosition=ConsolePosition},true));
        return controller;
    }
    [Serializable] class AssemblyValues {public Vector3 opening,seat,objectSize,displayPosition,consolePosition;public float insertDepth,bodyMaxY,minimumBodyClearance,gripHeight;}
    static void ConfigureModel()
    {
        foreach(string filename in new[]{"MasterTenon_LOD0.fbx","MasterTenon_LODs.fbx"}) {
            var imp=AssetImporter.GetAtPath(Root+"/Model/"+filename) as ModelImporter;
            if(!imp)throw new Exception("Frozen model not supplied: "+filename);
            imp.importAnimation=false;imp.importCameras=false;imp.importLights=false;imp.isReadable=true;
            imp.materialImportMode=ModelImporterMaterialImportMode.None;imp.SaveAndReimport();
        }
    }
    static Texture2D Texture(string name,bool linear=false,bool normal=false)
    {
        var path=Root+"/Model/Textures/"+name+".png";var imp=(TextureImporter)AssetImporter.GetAtPath(path);
        if(!imp)throw new Exception("Texture missing "+path);
        imp.sRGBTexture=!linear;imp.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;
        imp.maxTextureSize=2048;imp.textureCompression=TextureImporterCompression.Uncompressed;imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
    static Material TenonMaterial()
    {
        var path=Root+"/Generated/MasterTenon_R03.mat";
        var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
        m.SetTexture("_BaseMap",Texture("MasterTenon_BaseColor"));m.SetColor("_BaseColor",Color.white);
        m.SetTexture("_BumpMap",Texture("MasterTenon_Normal",true,true));m.SetFloat("_BumpScale",1);
        m.SetTexture("_MetallicGlossMap",Texture("MasterTenon_MetallicSmoothness",true));m.SetFloat("_Metallic",1);m.SetFloat("_Smoothness",1);
        m.SetTexture("_OcclusionMap",Texture("MasterTenon_AO",true));m.SetFloat("_OcclusionStrength",1);
        m.EnableKeyword("_NORMALMAP");m.EnableKeyword("_METALLICSPECGLOSSMAP");m.EnableKeyword("_OCCLUSIONMAP");EditorUtility.SetDirty(m);return m;
    }
    static GameItemBase BuildItem(Material m)
    {
        var go=new GameObject("WorkshopMasterTenon_R03");
        var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Model/MasterTenon_LODs.fbx"));model.transform.SetParent(go.transform,false);
        var meshes=model.GetComponentsInChildren<MeshRenderer>(true);
        foreach(var mesh in meshes)mesh.sharedMaterial=m;
        var lods=new List<LOD>();
        float[] thresholds={.18f,.08f,.015f};
        for(int i=0;i<3;i++) {
            var renderers=meshes.Where(r=>r.name.Contains("LOD"+i)).Cast<Renderer>().ToArray();
            if(renderers.Length!=1)throw new Exception("Expected one renderer for LOD"+i+", got "+renderers.Length);
            lods.Add(new LOD(thresholds[i],renderers));
        }
        var group=go.AddComponent<LODGroup>();group.SetLODs(lods.ToArray());group.RecalculateBounds();
        var bounds=new Bounds();bool measured=false;
        foreach(var f in model.GetComponentsInChildren<MeshFilter>()) {
            if(!f.name.Contains("LOD0"))continue;
            foreach(var v in f.sharedMesh.vertices) {var p=go.transform.InverseTransformPoint(f.transform.TransformPoint(v));if(!measured){bounds=new Bounds(p,Vector3.zero);measured=true;}else bounds.Encapsulate(p);}
        }
        if(!measured || Vector3.Distance(bounds.size,new Vector3(.12f,.35f,.12f))>.0001f || Mathf.Abs(bounds.min.y)>.0001f)throw new Exception("Imported 1:1 envelope/origin incorrect: "+bounds);
        File.WriteAllText(Output+"/imported-model-bounds.json",JsonUtility.ToJson(new BoundsValue{min=bounds.min,max=bounds.max,size=bounds.size,scale=go.transform.localScale},true));
        var body=go.AddComponent<Rigidbody>();body.mass=.8f;body.interpolation=RigidbodyInterpolation.None;body.collisionDetectionMode=CollisionDetectionMode.ContinuousSpeculative;
        var gauge=go.AddComponent<BoxCollider>();gauge.center=bounds.center;gauge.size=bounds.size;gauge.enabled=false;
        var profilePath=Root+"/Model/collision-profile.json";
        if(!File.Exists(profilePath))throw new Exception("Frozen-model collision profile required before building R03");
        var profile=JsonUtility.FromJson<CollisionProfile>(File.ReadAllText(profilePath));
        if(profile.parts==null || profile.parts.Length<2)throw new Exception("R03 requires a narrow-stem compound physical collider, distinct from 120mm gauge");
        foreach(var part in profile.parts) {
            if(part.upper<=part.lower || part.lower<-.0001f || part.upper>.3501f || part.width<=0 || part.depth<=0 || part.width>.1201f || part.depth>.1201f)throw new Exception("Invalid collision profile part "+part.name);
            var child=new GameObject("PhysicalContact_"+part.name);child.transform.SetParent(go.transform,false);
            child.transform.localPosition=new Vector3(0,(part.lower+part.upper)*.5f,0);
            var box=child.AddComponent<BoxCollider>();box.size=new Vector3(part.width,part.upper-part.lower,part.depth);box.contactOffset=.001f;
        }
        var contacts=go.GetComponentsInChildren<BoxCollider>().Where(c=>c!=gauge).ToArray();int coveredVertices=0;
        foreach(var f in model.GetComponentsInChildren<MeshFilter>()) {
            if(!f.name.Contains("LOD0"))continue;
            foreach(var vertex in f.sharedMesh.vertices) {
                var world=f.transform.TransformPoint(vertex);
                bool covered=contacts.Any(c=>{var p=c.transform.InverseTransformPoint(world)-c.center;var h=c.size*.5f+Vector3.one*.00002f;return Mathf.Abs(p.x)<=h.x&&Mathf.Abs(p.y)<=h.y&&Mathf.Abs(p.z)<=h.z;});
                if(!covered)throw new Exception("Visible LOD0 vertex outside interval contact boxes: "+go.transform.InverseTransformPoint(world));
                coveredVertices++;
            }
        }
        File.WriteAllText(Output+"/physical-envelope-coverage.txt",$"PASS all {coveredVertices} imported LOD0 vertices contained by {contacts.Length} enabled interval outer boxes; 20 micrometre float tolerance. Corner-radius empty space is conservative collision approximation. Disabled 120mm gauge is separate.\n");
        var item=go.AddComponent<GameItemBase>();item.grip=Anchor("Grip (review position)",new Vector3(0,profile.gripHeight,0),go.transform);item.insertionTip=Anchor("InsertionTip",Vector3.zero,go.transform);item.stowEnvelope=gauge;
        File.WriteAllText(Output+"/physical-collider-profile.json",JsonUtility.ToJson(profile,true));
        return item;
    }
    [Serializable] class CollisionProfile {public float gripHeight=.215f;public CollisionPart[] parts;}
    [Serializable] class CollisionPart {public string name;public float lower,upper,width,depth;}
    [Serializable] class BoundsValue {public Vector3 min,max,size,scale;}
    static void ConfigureZone(SnapZone zone,Transform align,Transform seat)
    {
        var so=new SerializedObject(zone);so.FindProperty("_snapMode").enumValueIndex=1;
        so.FindProperty("_requireNameMatch").boolValue=false;so.FindProperty("_disableCollidersOnSnap").boolValue=false;
        so.FindProperty("_snapTarget").objectReferenceValue=seat;so.FindProperty("_snapRotationTarget").objectReferenceValue=seat;
        var points=so.FindProperty("_snapPoints");points.arraySize=2;
        for(int i=0;i<2;i++){var p=points.GetArrayElementAtIndex(i);p.FindPropertyRelative("target").objectReferenceValue=i==0?align:seat;p.FindPropertyRelative("rotationTarget").objectReferenceValue=i==0?align:seat;p.FindPropertyRelative("overrideDuration").boolValue=true;p.FindPropertyRelative("duration").floatValue=i==0?.25f:.55f;}
        so.ApplyModifiedPropertiesWithoutUndo();
    }
    static Transform BuildHand(Material clay,Transform parent)
    {
        const string source="Assets/Art/PlayerHands/V3/PlayerRightHand.prefab";
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(source);if(!prefab)throw new Exception("Production hand reference missing");
        var original=(GameObject)PrefabUtility.InstantiatePrefab(prefab);
        foreach(var b in original.GetComponentsInChildren<Behaviour>(true))b.enabled=false;
        var reference=new GameObject("Production hand baked at 1:1 (scale reference, not tracked grip)");reference.transform.SetParent(parent);
        int index=0;var descriptions=new List<string>();
        foreach(var skin in original.GetComponentsInChildren<SkinnedMeshRenderer>(true)) {
            if(!skin.sharedMesh)continue;
            var mesh=new Mesh();skin.BakeMesh(mesh);
            var vertices=mesh.vertices;var transform=original.transform.worldToLocalMatrix*skin.transform.localToWorldMatrix;
            for(int i=0;i<vertices.Length;i++)vertices[i]=transform.MultiplyPoint3x4(vertices[i]);mesh.vertices=vertices;
            var normals=mesh.normals;for(int i=0;i<normals.Length;i++)normals[i]=transform.MultiplyVector(normals[i]).normalized;mesh.normals=normals;mesh.RecalculateBounds();
            string path=Root+"/Generated/HandScaleReference_"+(index++)+".asset";var prior=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(prior){EditorUtility.CopySerialized(mesh,prior);Object.DestroyImmediate(mesh);mesh=prior;}else AssetDatabase.CreateAsset(mesh,path);
            var part=new GameObject(skin.name);part.transform.SetParent(reference.transform,false);part.AddComponent<MeshFilter>().sharedMesh=mesh;part.AddComponent<MeshRenderer>().sharedMaterial=clay;
            descriptions.Add(skin.name+" "+mesh.bounds);
        }
        File.WriteAllLines(Output+"/hand-scale-reference.txt",new[]{source,"Baked static production hand geometry, world unit scale; does not assert grip pose/tracking."}.Concat(descriptions));
        Object.DestroyImmediate(original);return reference.transform;
    }
    static Material Material(string name,Color color,float metallic,float smoothness)
    {
        var path=Root+"/Generated/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
        m.SetColor("_BaseColor",color);m.SetFloat("_Metallic",metallic);m.SetFloat("_Smoothness",smoothness);return m;
    }
    static GameObject Cube(string name,Vector3 position,Vector3 size,Material material,Transform parent)
    {var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(parent);g.transform.position=position;g.transform.localScale=size;g.GetComponent<MeshRenderer>().sharedMaterial=material;return g;}
    static Transform Anchor(string name,Vector3 position,Transform parent)
    {var t=new GameObject(name).transform;t.SetParent(parent,false);t.localPosition=position;return t;}
    static void Label(string text,Vector3 position,Transform parent)
    {var t=new GameObject(text).AddComponent<TextMesh>();t.transform.SetParent(parent);t.transform.position=position;t.anchor=TextAnchor.MiddleCenter;t.alignment=TextAlignment.Center;t.characterSize=.010f;t.fontSize=52;t.text=text;t.color=new Color(.7f,.75f,.72f);}
    static void CameraLight(Transform parent)
    {
        RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=Color.white*.22f;RenderSettings.ambientEquatorColor=Color.white*.18f;RenderSettings.ambientGroundColor=Color.white*.08f;
        var c=new GameObject("Review Camera").AddComponent<Camera>();c.tag="MainCamera";c.transform.position=new Vector3(2.6f,2.15f,-3.35f);c.transform.LookAt(new Vector3(0,.8f,0));c.nearClipPlane=.02f;c.farClipPlane=20;c.fieldOfView=42;c.backgroundColor=new Color(.08f,.10f,.12f);c.clearFlags=CameraClearFlags.SolidColor;c.GetUniversalAdditionalCameraData().renderPostProcessing=false;c.gameObject.AddComponent<AudioListener>();
        var light=new GameObject("Review key light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.1f;light.transform.rotation=Quaternion.Euler(38,-30,0);light.color=Color.white;light.shadows=LightShadows.Soft;
        var fill=new GameObject("Review fill").AddComponent<Light>();fill.type=LightType.Directional;fill.intensity=.35f;fill.transform.rotation=Quaternion.Euler(25,140,0);fill.color=Color.white;
    }
    public static void Export()
    {
        AssetDatabase.SaveAssets();
        AssetDatabase.ExportPackage(Root,Delivery+"/WorkshopMasterTenonR03_UnityReview.unitypackage",ExportPackageOptions.Recurse);
        File.WriteAllText(Output+"/export-path.txt",Delivery+"/WorkshopMasterTenonR03_UnityReview.unitypackage\n");
    }
}
