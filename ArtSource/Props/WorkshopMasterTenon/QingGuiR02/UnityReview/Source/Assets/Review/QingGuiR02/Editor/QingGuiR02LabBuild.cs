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
using NarutoVR.QingGuiR02Review;
using NeuGameMRTools.Runtime.Components;
using Object=UnityEngine.Object;

public static class QingGuiR02LabBuild
{
    public const string Root="Assets/Review/QingGuiR02";
    public const string Output="D:/UnityProjects/NarutoVR/Logs/workshop-master-tenon-qinggui-r02-20261126/unity";
    public const string Delivery="D:/UnityProjects/NarutoVR/ArtSource/Props/WorkshopMasterTenon/QingGuiR02/UnityReview";
    public const string ScenePath=Root+"/QingGuiR02_Lab.unity";
    static Bounds lastMeasuredBounds;
    public static readonly Vector3 ConsolePosition=new(.85f,0,0),DisplayPosition=new(-.85f,.653f,0);
    public static readonly Vector3 OpeningLocal=new(-.12f,1.028f,0),SeatLocal=new(-.12f,.9239401f,0);
    public static QingGuiR02LabController Build()
    {
        Directory.CreateDirectory(Output);Directory.CreateDirectory(Delivery);
        Directory.CreateDirectory(Root+"/Generated");AssetDatabase.Refresh();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        ConfigureModel();
        var material=TenonMaterial();
        var pipeline=ReviewPipeline();
        var dark=Material("LabCharcoal",new Color(.08f,.10f,.115f),.0f,.25f);
        var stone=Material("DisplayStone",new Color(.18f,.21f,.22f),.0f,.35f);
        var clay=Material("HandScaleClay",new Color(.39f,.46f,.48f),.0f,.32f);
        var root=new GameObject("QingGuiR02 Independent Workshop Review (not chapter layout)");
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
        var itemPrefab=PrefabUtility.SaveAsPrefabAsset(item.gameObject,Root+"/QingGuiR02.prefab");
        Object.DestroyImmediate(item.gameObject);
        item=((GameObject)PrefabUtility.InstantiatePrefab(itemPrefab)).GetComponent<GameItemBase>();item.transform.SetParent(root.transform);item.transform.position=DisplayPosition;
        var opening=Anchor("Actual opening reference y=1.028",ConsolePosition+OpeningLocal,root.transform);
        var align=Anchor("Pre-align 30 mm above opening",opening.position+Vector3.up*.030f,root.transform);
        var seat=Anchor("Sample seat / 104.06 mm insertion / 4 mm body clearance",ConsolePosition+SeatLocal,root.transform);
        var z=new GameObject("Master-tenon-purpose socket");z.transform.SetParent(root.transform);z.transform.position=opening.position;
        var zone=z.AddComponent<QingGuiR02ReviewZone>();zone.expectedTenon=item;zone.opening=opening;zone.alignment=align;zone.seat=seat;zone.actualConsoleColliders=physical.ToArray();
        zone.conservativeGauge=item.stowEnvelope;zone.contactColliders=item.GetComponentsInChildren<BoxCollider>().Where(c=>c!=item.stowEnvelope).ToArray();
        var triangles=new List<Vector3>();
        foreach(var collider in physical) {var v=collider.sharedMesh.vertices;foreach(int index in collider.sharedMesh.triangles)triangles.Add(zone.transform.InverseTransformPoint(collider.transform.TransformPoint(v[index])));}
        zone.actualConsoleTriangles=triangles.ToArray();
        ConfigureZone(zone,align,seat);
        var controller=root.AddComponent<QingGuiR02LabController>();controller.tenon=item;controller.socket=zone;
        controller.displayOrigin=Anchor("03 Start display origin",DisplayPosition,root.transform);
        controller.inspectionOrigin=Anchor("Near-hand 1:1 inspection origin",new Vector3(-.85f,1.04f,-.12f),root.transform);
        controller.handScaleReference=BuildHand(clay,root.transform);
        controller.handScaleReference.position=controller.inspectionOrigin.position+new Vector3(.18f,.11f,.02f);
        controller.handScaleReference.rotation=Quaternion.Euler(0,0,0);
        Label("03  TAKE / DISPLAY EXAMPLE",new Vector3(-.85f,.50f,-.431f),root.transform);
        Label("04  INSERT / ACTUAL CONSOLE",new Vector3(.85f,.58f,-.805f),root.transform);
        CameraLight(root.transform);root.AddComponent<QingGuiR02ReviewQuality>().reviewPipeline=pipeline;
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),ScenePath);
        AssetDatabase.SaveAssets();
        File.WriteAllText(Output+"/assembly-values.json",JsonUtility.ToJson(new AssemblyValues{opening=opening.position,seat=seat.position,insertDepth=opening.position.y-seat.position.y,bodyMaxY=.9199401f,minimumBodyClearance=.004f,objectSize=lastMeasuredBounds.size,gripHeight=item.grip.localPosition.y,displayPosition=DisplayPosition,consolePosition=ConsolePosition},true));
        return controller;
    }
    [Serializable] class AssemblyValues {public Vector3 opening,seat,objectSize,displayPosition,consolePosition;public float insertDepth,bodyMaxY,minimumBodyClearance,gripHeight;}
    static void ConfigureModel()
    {
        foreach(string filename in new[]{"QingGuiR02_LOD0.fbx","QingGuiR02_LODs.fbx"}) {
            var imp=AssetImporter.GetAtPath(Root+"/Model/"+filename) as ModelImporter;
            if(!imp)throw new Exception("Frozen model not supplied: "+filename);
            imp.importAnimation=false;imp.importCameras=false;imp.importLights=false;imp.isReadable=true;
            imp.importNormals=ModelImporterNormals.Import;imp.importTangents=ModelImporterTangents.CalculateMikk;
            imp.meshCompression=ModelImporterMeshCompression.Off;
            imp.materialImportMode=ModelImporterMaterialImportMode.None;imp.SaveAndReimport();
        }
    }
    static Texture2D Texture(string name,bool linear=false,bool normal=false)
    {
        var path=Root+"/Model/Textures/"+name+".png";var imp=(TextureImporter)AssetImporter.GetAtPath(path);
        if(!imp)throw new Exception("Texture missing "+path);
        imp.sRGBTexture=!linear;imp.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;
        if(normal)imp.flipGreenChannel=false;
        imp.maxTextureSize=2048;imp.textureCompression=TextureImporterCompression.Uncompressed;imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
    static Material TenonMaterial()
    {
        var path=Root+"/Generated/QingGui_R02.mat";
        var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!m){m=new Material(Shader.Find("NarutoVR/Review/QingGui R02 Polished Jade"));AssetDatabase.CreateAsset(m,path);}
        m.SetTexture("_BaseMap",Texture("QingGui_BaseColor"));m.SetColor("_BaseColor",Color.white);
        m.SetTexture("_BumpMap",Texture("QingGui_NormalGL",true,true));m.SetFloat("_BumpScale",1);
        m.SetTexture("_MetallicGlossMap",Texture("QingGui_MetallicSmoothness",true));m.SetFloat("_Metallic",1);m.SetFloat("_Smoothness",1);
        m.SetTexture("_OcclusionMap",Texture("QingGui_AO",true));m.SetFloat("_OcclusionStrength",1);
        m.SetTexture("_ThicknessMap",Texture("QingGui_Thickness",true));m.SetTexture("_JadeMask",Texture("QingGui_JadeMask",true));m.SetFloat("_ThicknessMaxMetres",.12f);m.SetFloat("_TransmissionDebug",0);m.SetFloat("_MinimumLightCosine",.01f);
        // Warm-white jade / faint honey thin edges: fitted appearance, not measured optical constants.
        m.SetVector("_AbsorptionPerMetre",new Vector4(22,24,30,0));m.SetFloat("_TransmissionStrength",.55f); m.SetFloat("_ClearCoatMask",.6f);m.SetFloat("_ClearCoatSmoothness",.9f);m.SetFloat("_DepthColorStrength",.08f);m.SetFloat("_ScatterStrength",.25f);m.SetFloat("_InnerDepthMetres",.012f);m.SetFloat("_CloudScaleMetres",.06f);m.SetFloat("_JadeLayerMode",0);m.SetFloat("_JadeTransparency",.5f);m.SetTexture("_JadeCloud",AssetDatabase.LoadAssetAtPath<Texture3D>(Root+"/Generated/JadeDensity.asset"));m.EnableKeyword("_CLEARCOAT");
        m.EnableKeyword("_NORMALMAP");m.EnableKeyword("_METALLICSPECGLOSSMAP");m.EnableKeyword("_OCCLUSIONMAP");EditorUtility.SetDirty(m);return m;
    }
    static GameItemBase BuildItem(Material m)
    {
        var go=new GameObject("QingGui_R01");
        var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Model/QingGuiR02_LODs.fbx"));model.transform.SetParent(go.transform,false);PrefabUtility.UnpackPrefabInstance(model,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);foreach(var importedLOD in model.GetComponentsInChildren<LODGroup>(true))Object.DestroyImmediate(importedLOD);
        var meshes=model.GetComponentsInChildren<MeshRenderer>(true);
        var gold=Material("QingGuiR02_GoldWire",new Color(.76f,.47f,.14f),1f,.68f);gold.shader=Shader.Find("NarutoVR/Review/QingGui R02 Opaque Gold");gold.SetFloat("_ClearCoatMask",0);gold.DisableKeyword("_CLEARCOAT");gold.renderQueue=3002;EditorUtility.SetDirty(gold);
        var bronze=new Material(m);bronze.name="QingGuiR02_OpaqueBronze";bronze.SetFloat("_JadeLayerMode",1);bronze.SetShaderPassEnabled("DepthOnly",false);bronze.SetShaderPassEnabled("DepthNormals",false);bronze.renderQueue=3001;AssetDatabase.CreateAsset(bronze,Root+"/Generated/QingGuiR02_OpaqueBronze.mat");
        var jade=new Material(Shader.Find("NarutoVR/Review/QingGui R02 Local Jade Transparency"));jade.CopyPropertiesFromMaterial(m);jade.SetFloat("_JadeLayerMode",2);jade.SetFloat("_JadeTransparency",.60f);jade.EnableKeyword("_CLEARCOAT");jade.renderQueue=3000;AssetDatabase.CreateAsset(jade,Root+"/Generated/QingGuiR02_Jade60.mat");
        var jade50=new Material(jade);jade50.SetFloat("_JadeTransparency",.50f);AssetDatabase.CreateAsset(jade50,Root+"/Generated/QingGuiR02_Jade50.mat");
        foreach(var mesh in meshes)mesh.sharedMaterial=mesh.name.Contains("GoldWire")?gold:bronze;
        // Shared material is valid because every exported LOD uses the same object frame.
        var filters=model.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.name.Contains("Body")).ToArray();
        var localBounds=filters[0].sharedMesh.bounds;
        var basis=model.transform.worldToLocalMatrix*filters[0].transform.localToWorldMatrix;
        foreach(var f in filters) {
            var matrix=model.transform.worldToLocalMatrix*f.transform.localToWorldMatrix;
            for(int row=0;row<4;row++)for(int col=0;col<4;col++)if(Mathf.Abs(matrix[row,col]-basis[row,col])>.00001f)throw new Exception("LOD local frames differ; shared material bounds need explicit per-LOD binding");
            localBounds.Encapsulate(f.sharedMesh.bounds);
        }
        foreach(var mat in new[]{m,bronze,jade,jade50}) {mat.SetVector("_BoundsMinOS",localBounds.min);mat.SetVector("_BoundsMaxOS",localBounds.max);EditorUtility.SetDirty(mat);}
        File.WriteAllText(Output+"/shader-object-space-bounds.json",JsonUtility.ToJson(new BoundsValue{min=localBounds.min,max=localBounds.max,size=localBounds.size},true));
        var layeredRenderers=new List<MeshRenderer>(meshes);
        foreach(var filter in filters) {
            var layer=new GameObject(filter.name+"_LocalJade");layer.transform.SetParent(filter.transform,false);layer.AddComponent<MeshFilter>().sharedMesh=filter.sharedMesh;
            var r=layer.AddComponent<MeshRenderer>();r.sharedMaterial=jade;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=true;layeredRenderers.Add(r);
        }
        var lods=new List<LOD>();
        float[] thresholds={.18f,.08f,.015f};
        for(int i=0;i<3;i++) {
            var renderers=layeredRenderers.Where(r=>r.name.Contains("LOD"+i)).Cast<Renderer>().ToArray();
            if(renderers.Length!=3)throw new Exception("Expected Body/GoldWire plus jade layer for LOD"+i+", got "+renderers.Length);
            lods.Add(new LOD(thresholds[i],renderers));
        }
        var group=go.AddComponent<LODGroup>();group.SetLODs(lods.ToArray());group.RecalculateBounds();
        var bounds=new Bounds();bool measured=false;
        foreach(var f in model.GetComponentsInChildren<MeshFilter>()) {
            if(!f.name.Contains("LOD0")||f.name.Contains("LocalJade"))continue;
            foreach(var v in f.sharedMesh.vertices) {var p=go.transform.InverseTransformPoint(f.transform.TransformPoint(v));if(!measured){bounds=new Bounds(p,Vector3.zero);measured=true;}else bounds.Encapsulate(p);}
        }
        if(!measured || (bounds.size.x<.108f || bounds.size.x>.12f || Mathf.Abs(bounds.size.y-.35f)>.001f || Mathf.Abs(bounds.size.z-.106f)>.001f) || Mathf.Abs(bounds.min.y)>.0001f)throw new Exception("Imported 1:1 envelope/origin incorrect: "+bounds);
        lastMeasuredBounds=bounds;
        File.WriteAllText(Output+"/imported-model-bounds.json",JsonUtility.ToJson(new BoundsValue{min=bounds.min,max=bounds.max,size=bounds.size,scale=go.transform.localScale},true));
        var body=go.AddComponent<Rigidbody>();body.mass=.8f;body.interpolation=RigidbodyInterpolation.None;body.collisionDetectionMode=CollisionDetectionMode.ContinuousSpeculative;
        var gauge=go.AddComponent<BoxCollider>();gauge.center=new Vector3(0,.175f,0);gauge.size=new Vector3(.12f,.35f,.12f);gauge.enabled=false;
        var profile=DeriveContactProfile(model,go.transform);
        foreach(var part in profile.parts) {
            if(part.upper<=part.lower || part.lower<-.0001f || part.upper>.3501f || part.width<=0 || part.depth<=0 || part.width>.1201f || part.depth>.1201f)throw new Exception("Invalid collision profile part "+part.name);
            var child=new GameObject("PhysicalContact_"+part.name);child.transform.SetParent(go.transform,false);
            child.transform.localPosition=new Vector3(part.centerX,(part.lower+part.upper)*.5f,part.centerZ);
            var box=child.AddComponent<BoxCollider>();box.size=new Vector3(part.width,part.upper-part.lower,part.depth);box.contactOffset=.001f;
        }
        var contacts=go.GetComponentsInChildren<BoxCollider>().Where(c=>c!=gauge).ToArray();int coveredVertices=0;
        foreach(var f in model.GetComponentsInChildren<MeshFilter>()) {
            if(!f.name.Contains("LOD0")||f.name.Contains("LocalJade"))continue;
            foreach(var vertex in f.sharedMesh.vertices) {
                var world=f.transform.TransformPoint(vertex);
                bool covered=contacts.Any(c=>{var p=c.transform.InverseTransformPoint(world)-c.center;var h=c.size*.5f+Vector3.one*.00002f;return Mathf.Abs(p.x)<=h.x&&Mathf.Abs(p.y)<=h.y&&Mathf.Abs(p.z)<=h.z;});
                if(!covered)throw new Exception("Visible LOD0 vertex outside interval contact boxes: "+go.transform.InverseTransformPoint(world));
                coveredVertices++;
            }
        }
        File.WriteAllText(Output+"/physical-envelope-coverage.txt",$"PASS all {coveredVertices} imported LOD0 vertices contained by {contacts.Length} enabled interval outer boxes; 20 micrometre float tolerance. Corner-radius empty space is conservative collision approximation. Disabled 120mm gauge is separate.\n");
        var submissions=new List<string>();
        for(int i=0;i<3;i++){var lr=layeredRenderers.Where(r=>r.name.Contains("LOD"+i)).ToArray();int submitted=lr.Sum(r=>r.GetComponent<MeshFilter>().sharedMesh.triangles.Length/3);int source=meshes.Where(r=>r.name.Contains("LOD"+i)).Sum(r=>r.GetComponent<MeshFilter>().sharedMesh.triangles.Length/3);submissions.Add($"LOD{i}: sourceTriangles={source}, forwardSubmittedTriangles={submitted}, renderers={lr.Length}, materials=3; excludes shadow caster and depth/extra cameras; Mesh asset shared by Body/jade layer.");}
        File.WriteAllLines(Output+"/actual-render-submission-cost.txt",submissions);
        var item=go.AddComponent<GameItemBase>();item.grip=Anchor("Grip (review position)",new Vector3(profile.gripX,profile.gripHeight,profile.gripZ),go.transform);item.insertionTip=Anchor("InsertionTip",Vector3.zero,go.transform);item.stowEnvelope=gauge;
        File.WriteAllText(Output+"/physical-collider-profile.json",JsonUtility.ToJson(profile,true));
        return item;
    }
    [Serializable] class CollisionProfile {public string method;public float gripHeight=.230f,gripX=-.0015f,gripZ;public CollisionPart[] parts;}
    [Serializable] class CollisionPart {public string name;public float lower,upper,width,depth,centerX,centerZ;}
    static CollisionProfile DeriveContactProfile(GameObject model,Transform root)
    {
        // Exact imported LOD0 triangles are clipped to each height interval.
        // Side bounds are outward approximations, not the older R03 inner boxes.
        float[] cuts={0,.006f,.04f,.08f,.105f,.112f,.127f,.145f,.17f,.195f,.22f,.245f,.27f,.295f,.32f,.35f};
        var triangles=new List<Vector3[]>();
        foreach(var filter in model.GetComponentsInChildren<MeshFilter>()) {
            if(!filter.name.Contains("LOD0")||filter.name.Contains("LocalJade"))continue;var mesh=filter.sharedMesh;var vertices=mesh.vertices;var indices=mesh.triangles;
            for(int i=0;i<indices.Length;i+=3)triangles.Add(Enumerable.Range(0,3).Select(j=>root.InverseTransformPoint(filter.transform.TransformPoint(vertices[indices[i+j]]))).ToArray());
        }
        var parts=new List<CollisionPart>();
        for(int i=0;i<cuts.Length-1;i++) {
            var points=new List<Vector3>();
            foreach(var triangle in triangles)points.AddRange(ClipHeight(ClipHeight(triangle.ToList(),cuts[i],true),cuts[i+1],false));
            if(points.Count==0)continue;
            float minX=points.Min(v=>v.x)-.000002f,maxX=points.Max(v=>v.x)+.000002f,minZ=points.Min(v=>v.z)-.000002f,maxZ=points.Max(v=>v.z)+.000002f;
            parts.Add(new CollisionPart{name="interval_"+i,lower=cuts[i],upper=cuts[i+1],width=maxX-minX,depth=maxZ-minZ,centerX=(minX+maxX)*.5f,centerZ=(minZ+maxZ)*.5f});
        }
        var gripPoints=triangles.SelectMany(t=>ClipHeight(ClipHeight(t.ToList(),.230f,true),.230f,false)).ToArray();
        if(gripPoints.Length<4)throw new Exception("No actual imported section at 230mm grip height");
        return new CollisionProfile{method="Imported LOD0 triangle polygons clipped to 15 Y intervals; outward XZ AABB +2um per side; disabled 120mm gauge separate. Rounded corners and slanted crown leave conservative empty box space. Grip center from actual imported 230mm section.",parts=parts.ToArray(),gripX=(gripPoints.Min(p=>p.x)+gripPoints.Max(p=>p.x))*.5f,gripZ=(gripPoints.Min(p=>p.z)+gripPoints.Max(p=>p.z))*.5f};
    }
    static List<Vector3> ClipHeight(List<Vector3> polygon,float height,bool keepAbove)
    {
        var result=new List<Vector3>();if(polygon.Count==0)return result;
        for(int i=0;i<polygon.Count;i++) {
            var a=polygon[i];var b=polygon[(i+1)%polygon.Count];bool ia=keepAbove?a.y>=height:a.y<=height,ib=keepAbove?b.y>=height:b.y<=height;
            if(ia)result.Add(a);
            if(ia!=ib)result.Add(Vector3.LerpUnclamped(a,b,(height-a.y)/(b.y-a.y)));
        }
        return result;
    }
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
        NeutralStudioReflection();
        RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=Color.white*.22f;RenderSettings.ambientEquatorColor=Color.white*.18f;RenderSettings.ambientGroundColor=Color.white*.08f;
        var c=new GameObject("Review Camera").AddComponent<Camera>();c.tag="MainCamera";c.transform.position=new Vector3(2.6f,2.15f,3.35f);c.transform.LookAt(new Vector3(0,.8f,0));c.nearClipPlane=.02f;c.farClipPlane=20;c.fieldOfView=42;c.backgroundColor=new Color(.08f,.10f,.12f);c.clearFlags=CameraClearFlags.SolidColor;c.GetUniversalAdditionalCameraData().renderPostProcessing=false;c.gameObject.AddComponent<AudioListener>();
        var light=new GameObject("Review key light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=.9f;light.transform.rotation=Quaternion.LookRotation(-new Vector3(-.4f,.55f,.75f));light.color=Color.white;light.shadows=LightShadows.Soft;light.GetUniversalAdditionalLightData().softShadowQuality=SoftShadowQuality.Medium;light.shadowNormalBias=0;light.shadowBias=.02f;
        var fill=new GameObject("Review fill").AddComponent<Light>();fill.type=LightType.Directional;fill.intensity=.22f;fill.transform.rotation=Quaternion.LookRotation(-new Vector3(.65f,.25f,.55f));fill.color=Color.white;
    }
    static void NeutralStudioReflection()
    {
        var map=AssetDatabase.LoadAssetAtPath<Cubemap>(Root+"/Generated/CapturedFrontalStudio.cubemap");if(!map)throw new Exception("Actual captured studio environment missing");
        RenderSettings.defaultReflectionMode=DefaultReflectionMode.Custom;RenderSettings.customReflectionTexture=map;RenderSettings.reflectionIntensity=1;
    }
    public static UniversalRenderPipelineAsset ReviewPipeline()
    {
        var path=Root+"/Generated/QingGuiR02_DesktopReviewRP.asset";var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
        if(!pipeline){pipeline=Object.Instantiate(AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/Mobile_RPAsset.asset"));AssetDatabase.CreateAsset(pipeline,path);}
        var rendererPath=Root+"/Generated/QingGuiR02_DesktopReviewRenderer.asset";
        var renderer=AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
        if(!renderer){renderer=Object.Instantiate(AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/Mobile_Renderer.asset"));AssetDatabase.CreateAsset(renderer,rendererPath);}
        renderer.shadowTransparentReceive=true;EditorUtility.SetDirty(renderer);
        var serialized=new SerializedObject(pipeline);var list=serialized.FindProperty("m_RendererDataList");list.arraySize=1;list.GetArrayElementAtIndex(0).objectReferenceValue=renderer;serialized.ApplyModifiedPropertiesWithoutUndo();
        serialized.Update();serialized.FindProperty("m_SoftShadowsSupported").boolValue=true;serialized.ApplyModifiedPropertiesWithoutUndo();pipeline.mainLightShadowmapResolution=2048;pipeline.shadowDistance=2.5f;pipeline.shadowCascadeCount=1;pipeline.supportsCameraOpaqueTexture=false;EditorUtility.SetDirty(pipeline);return pipeline;
    }
    public static void Export()
    {
        AssetDatabase.SaveAssets();
        AssetDatabase.ExportPackage(Root,Delivery+"/QingGuiR02_UnityReview.unitypackage",ExportPackageOptions.Recurse);
        File.WriteAllText(Output+"/export-path.txt",Delivery+"/QingGuiR02_UnityReview.unitypackage\n");
    }
}
