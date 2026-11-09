// Offline authoring in isolated checkout. Never call against the user's open project.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class ExteriorPolishAuthor
{
    const string Source="Assets/Scene/Chapter01_Awakening.unity";
    const string Candidate="Assets/Scene/QA_ExteriorPolish.unity";
    const string Art="Assets/Art/Environment/Opening/MountainPolish/";
    static string Out;
    static Scene work;
    static readonly List<Change> changes=new List<Change>();
    static readonly List<string> audit=new List<string>();
    static readonly List<CameraStat> cameraStats=new List<CameraStat>();
    [Serializable] public class CameraStat
    {
        public string image,editorStatsMeaning;
        public int frustumRenderers,meshRenderers,nonMeshRenderers,submeshes,editorDrawCalls,editorBatches,editorTriangles;
        public long conservativeMeshTriangles;
        public Vector3 position,forward,up;
    }
    [Serializable] public class CameraStats {public CameraStat[] views;public string caution;}
    [Serializable] public class Change
    {
        public string objectType, path, globalObjectId, assetPath, propertyPath, before, after;
        public long fileID, prefabInstanceFileID, prefabSourceFileID;
        public string prefabSourceGUID;
        public bool prefabInstance;
        public Override[] resultingPrefabOverrides;
    }
    [Serializable] public class Override { public string targetGUID, propertyPath, value, objectReferenceGUID;public long targetFileID,objectReferenceFileID; }
    [Serializable] public class Report
    {
        public string source,candidate,newPrefabGUID,newPrefabPath,sourceSHA256;
        public long newPrefabRootFileID;
        public Change[] changes;
        public string[] notes;
    }
    static T[] All<T>()where T:Component=>work.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<T>(true)).ToArray();
    static string PathOf(Transform t)=>t.parent?PathOf(t.parent)+"/"+t.name:t.name;
    static string F(float v)=>v.ToString("R",System.Globalization.CultureInfo.InvariantCulture);
    static string C(Color v)=>JsonUtility.ToJson(v);
    static long Id(Object o)=>o?(long)GlobalObjectId.GetGlobalObjectIdSlow(o).targetObjectId:0;
    static void Guid(Object o,out string guid,out long id)
    {guid="";id=0;if(o)AssetDatabase.TryGetGUIDAndLocalFileIdentifier(o,out guid,out id);}
    static string Sha(string p){using(var hash=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(p))).Replace("-","").ToLowerInvariant();}
    static Change Capture(Object obj,string prop,string before,string after)
    {
        var component=obj as Component;var gid=GlobalObjectId.GetGlobalObjectIdSlow(obj);
        var source=PrefabUtility.GetCorrespondingObjectFromSource(obj);Guid(source,out string sg,out long sid);
        return new Change{objectType=obj.GetType().Name,path=component?PathOf(component.transform):obj.name,
            globalObjectId=gid.ToString(),fileID=(long)gid.targetObjectId,prefabInstanceFileID=(long)gid.targetPrefabId,
            assetPath=AssetDatabase.GetAssetPath(obj),propertyPath=prop,before=before,after=after,
            prefabInstance=PrefabUtility.IsPartOfPrefabInstance(obj),prefabSourceGUID=sg,prefabSourceFileID=sid};
    }
    static void Mutate(Object obj,string prop,string before,string after,Action apply)
    {
        if(before==after)return;
        var change=Capture(obj,prop,before,after);apply();EditorUtility.SetDirty(obj);
        if(change.prefabInstance)
        {
            PrefabUtility.RecordPrefabInstancePropertyModifications(obj);
            var root=PrefabUtility.GetNearestPrefabInstanceRoot(obj);
            var modifications=PrefabUtility.GetPropertyModifications(root)??new PropertyModification[0];
            change.resultingPrefabOverrides=modifications.Select(m=>{
                Guid(m.target,out string g,out long id);Guid(m.objectReference,out string rg,out long rid);
                return new Override{targetGUID=g,targetFileID=id,propertyPath=m.propertyPath,value=m.value,
                    objectReferenceGUID=rg,objectReferenceFileID=rid};}).ToArray();
        }
        changes.Add(change);
    }
    static void Settings(string prop,string before,string after,Action apply)
    {
        if(before==after)return;apply();changes.Add(new Change{objectType="RenderSettings",path="RenderSettings",fileID=2,propertyPath=prop,before=before,after=after});
    }
    public static void Run()
    {
        Out=Environment.GetEnvironmentVariable("EXTERIOR_POLISH_OUT")??"Logs/exterior-polish-20261108/integration";
        Directory.CreateDirectory(Out);changes.Clear();audit.Clear();cameraStats.Clear();
        var staleFail=Path.Combine(Out,"FAIL.txt");if(File.Exists(staleFail))File.Move(staleFail,Path.Combine(Out,"previous-failure-"+DateTime.Now.ToString("HHmmss")+".txt"));
        if(!File.Exists(Source))throw new Exception("Missing chapter scene");
        var hash=Sha(Source);File.Copy(Source,Path.Combine(Out,"source-scene.unity"),true);
        MountainPolishBuilder.Build();ExteriorLandscapeBuilder.Build();WaterPolishAcceptance.Validate();
        // Preview scenes cannot become active; normal isolated-scene loading is required
        // for RenderSettings, sun selection and URP shadow/ambient rendering to be real.
        work=EditorSceneManager.OpenScene(Source,OpenSceneMode.Single);
        var pipeline=GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        float oldDistance=pipeline?pipeline.shadowDistance:0;
        GameObject root=null;
        try
        {
            Debug.Log("Author scene valid="+work.IsValid()+" loaded="+work.isLoaded+" path="+work.path+" active="+SceneManager.GetActiveScene().path+" same="+(SceneManager.GetActiveScene()==work));
            if(SceneManager.GetActiveScene()!=work && !SceneManager.SetActiveScene(work))throw new Exception("Cannot activate isolated authoring scene");
            File.WriteAllLines(Path.Combine(Out,"before-inventory.txt"),Inventory());
            Photos("before",false);
            root=new GameObject("首章外景润色 · 山川层岩与量水门庭");SceneManager.MoveGameObjectToScene(root,work);
            var mountains=AddPrefab(Art+"MountainPolish.prefab",root.transform);
            var mountainRenderers=mountains.GetComponentsInChildren<MeshRenderer>();
            if(mountainRenderers.Any(r=>r.shadowCastingMode!=ShadowCastingMode.Off))throw new Exception("Mountains unexpectedly cast realtime shadows");
            audit.Add("Mountain shadow audit: all "+mountainRenderers.Length+" cast OFF; only "+mountainRenderers.Count(r=>r.receiveShadows)+" near bluff instances receive. All middle/far ridges receive OFF.");
            AddPrefab("Assets/Art/Environment/Opening/WaterPolish/WaterAudio.prefab",root.transform);
            AddPrefab(Art+"ValleyLandscape.prefab",root.transform);
            ExteriorDressing.Build(root.transform,work,All<MeshFilter>().FirstOrDefault(f=>f.name=="近景地形"));
            ExteriorRoofBuilder.Build(root.transform,work);
            foreach(var renderer in All<MeshRenderer>().Where(r=>r.name=="远山 · 环形轮廓" || r.name=="近景地形" || r.name=="远处天工遗构" || r.name=="破碎峡谷底床" || (r.name=="CliffPanel"&&PathOf(r.transform).Contains("小峡壁"))))
                Mutate(renderer,"m_Enabled",renderer.enabled?"1":"0","0",()=>renderer.enabled=false);
            foreach(var renderer in All<MeshRenderer>().Where(r=>r.enabled&&r.gameObject.activeInHierarchy&&(r.name=="石台与窗洞"||r.name=="窗格木构"||r.name=="远处天工遗构")))
                Mutate(renderer,"m_CastShadows",((int)renderer.shadowCastingMode).ToString(),((int)ShadowCastingMode.On).ToString(),()=>renderer.shadowCastingMode=ShadowCastingMode.On);
            AddPortal(root.transform);
            var suns=All<Light>().Where(l=>l.type==LightType.Directional&&l.enabled&&l.gameObject.activeInHierarchy).ToArray();
            if(suns.Length!=1)throw new Exception("Expected exactly one active directional sunlight, found "+suns.Length);
            var sun=suns[0];
            Mutate(sun,"m_Intensity",F(sun.intensity),"1.2",()=>sun.intensity=1.2f);
            Mutate(sun,"m_Color",C(sun.color),C(new Color(1,.93f,.82f)),()=>sun.color=new Color(1,.93f,.82f));
            Mutate(sun,"m_Shadows.m_Type",((int)sun.shadows).ToString(),((int)LightShadows.Hard).ToString(),()=>sun.shadows=LightShadows.Hard);
            Mutate(sun,"m_Shadows.m_Strength",F(sun.shadowStrength),"0.7",()=>sun.shadowStrength=.7f);
            Mutate(sun,"m_Shadows.m_Bias",F(sun.shadowBias),"0.035",()=>sun.shadowBias=.035f);
            Mutate(sun,"m_Shadows.m_NormalBias",F(sun.shadowNormalBias),"0.2",()=>sun.shadowNormalBias=.2f);
            Settings("m_AmbientMode",((int)RenderSettings.ambientMode).ToString(),((int)AmbientMode.Trilight).ToString(),()=>RenderSettings.ambientMode=AmbientMode.Trilight);
            Settings("m_AmbientSkyColor",C(RenderSettings.ambientSkyColor),C(new Color(.48f,.57f,.64f)),()=>RenderSettings.ambientSkyColor=new Color(.48f,.57f,.64f));
            Settings("m_AmbientEquatorColor",C(RenderSettings.ambientEquatorColor),C(new Color(.30f,.34f,.35f)),()=>RenderSettings.ambientEquatorColor=new Color(.30f,.34f,.35f));
            Settings("m_AmbientGroundColor",C(RenderSettings.ambientGroundColor),C(new Color(.17f,.16f,.14f)),()=>RenderSettings.ambientGroundColor=new Color(.17f,.16f,.14f));
            Settings("m_AmbientIntensity",F(RenderSettings.ambientIntensity),"0.8",()=>RenderSettings.ambientIntensity=.8f);
            Settings("m_Sun","{fileID: "+Id(RenderSettings.sun)+"}","{fileID: "+Id(sun)+"}",()=>RenderSettings.sun=sun);
            Settings("m_Fog",RenderSettings.fog?"1":"0","1",()=>RenderSettings.fog=true);
            Settings("m_FogMode",((int)RenderSettings.fogMode).ToString(),((int)FogMode.Linear).ToString(),()=>RenderSettings.fogMode=FogMode.Linear);
            Settings("m_LinearFogStart",F(RenderSettings.fogStartDistance),"45",()=>RenderSettings.fogStartDistance=45);
            Settings("m_LinearFogEnd",F(RenderSettings.fogEndDistance),"185",()=>RenderSettings.fogEndDistance=185);
            audit.Add("Skybox and fog horizon color retained. Ambient/sun tuned; no additional lights created.");
            if(pipeline)
            {
                var serializedPipeline=new SerializedObject(pipeline);
                var shadowSupport=serializedPipeline.FindProperty("m_MainLightShadowsSupported");
                if(shadowSupport==null||!shadowSupport.boolValue)throw new Exception("URP main-light shadows disabled in active pipeline: "+AssetDatabase.GetAssetPath(pipeline));
                if(pipeline.mainLightRenderingMode!=LightRenderingMode.PerPixel)throw new Exception("Active URP pipeline must render main sun per pixel for shadow acceptance");
                audit.Add("URP main-light shadows supported, PerPixel main sun confirmed; active pipeline="+AssetDatabase.GetAssetPath(pipeline)+", sun.shadows="+sun.shadows+", review camera.renderShadows=true.");
                audit.Add("Existing pipeline shadowDistance="+F(oldDistance)+" retained for photos and publication; shared PC/Mobile pipeline assets are unchanged.");
            }
            foreach(var camera in All<Camera>().Where(c=>c.enabled&&c.gameObject.activeInHierarchy&&c.gameObject.name!="ExteriorPolishReview"&&c.farClipPlane<280))
                Mutate(camera,"far clip plane",F(camera.farClipPlane),"280",()=>camera.farClipPlane=280);
            Photos("after",true);
            File.WriteAllLines(Path.Combine(Out,"after-inventory.txt"),Inventory());
            var result=PrefabUtility.SaveAsPrefabAsset(root,Art+"EnvironmentPolish.prefab");
            Guid(result.transform,out string pg,out long pid);
            if(!EditorSceneManager.SaveScene(work,Candidate,true))throw new Exception("Candidate scene save failed");
            audit.Add("Normal scene loaded only in isolated checkout and saved as candidate copy. Source remains byte-identical. Existing GlobalObjectId target IDs are source-scene identifiers captured before SaveScene; no auto-publication attempted.");
            audit.Add("Doorway is a viewable authoring mockup on new side landing; no teleport gameplay script or destination was invented.");
            audit.Add("New prefab uses nested prefab/model instances. Its own transforms/material refs are new asset content, not modifications to existing scene objects.");
            var report=new Report{source=Source,candidate=Candidate,sourceSHA256=hash,newPrefabPath=Art+"EnvironmentPolish.prefab",newPrefabGUID=pg,newPrefabRootFileID=pid,changes=changes.ToArray(),notes=audit.ToArray()};
            File.WriteAllText(Path.Combine(Out,"scene-field-changes.json"),JsonUtility.ToJson(report,true));
            File.WriteAllText(Path.Combine(Out,"camera-statistics.json"),JsonUtility.ToJson(new CameraStats{views=cameraStats.ToArray(),caution="Frustum mesh totals are conservative whole-renderer geometry estimates, not draw calls: no occlusion, triangle clipping, renderer-feature/shadow/depth/instancing accounting. UnityStats are Editor observations sampled after Camera.Render, may include other Editor work; no Quest GPU timing or per-eye guarantee."},true));
            if(pipeline)pipeline.shadowDistance=oldDistance;
            AssetDatabase.SaveAssets();
            if(Sha(Source)!=hash)throw new Exception("Source scene changed unexpectedly");
            File.WriteAllText(Path.Combine(Out,"PASS.txt"),"Authoring finished; source hash unchanged. Candidate and screenshot inspection still required.\n"+DateTimeOffset.Now.ToString("O"));
        }
        catch(Exception e){File.WriteAllText(Path.Combine(Out,"FAIL.txt"),e.ToString());throw;}
        finally
        {
            if(pipeline)pipeline.shadowDistance=oldDistance;
            // Batch launcher exits after Run; leave unsaved source scene in memory rather
            // than invoking preview-only close APIs or saving any dirty original scene.
        }
    }
    static GameObject AddPrefab(string path,Transform parent)
    {
        var asset=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(!asset)throw new Exception("Missing prefab/model: "+path);
        var instance=(GameObject)PrefabUtility.InstantiatePrefab(asset,work);instance.transform.SetParent(parent,false);return instance;
    }
    static void AddPortal(Transform parent)
    {
        var path=Environment.GetEnvironmentVariable("EXTERIOR_PORTAL_PREFAB")??"Assets/Art/Environment/Opening/PortalGate/PortalGate.fbx";
        var portal=AddPrefab(path,parent);portal.name="量水道尽头 · 残规门庭";
        portal.transform.localPosition=new Vector3(10,0,63);portal.transform.localRotation=Quaternion.Euler(0,180,0);
        void Box(string name,Vector3 p,Vector3 size){var g=new GameObject(name);g.transform.SetParent(portal.transform,false);g.transform.localPosition=p;g.AddComponent<BoxCollider>().size=size;}
        if(portal.GetComponentsInChildren<Collider>(true).Length==0){Box("左门柱碰撞",new Vector3(-1.15f,1.64f,0),new Vector3(.5f,3.28f,.78f));Box("右门柱碰撞",new Vector3(1.15f,1.64f,0),new Vector3(.5f,3.28f,.78f));Box("上梁碰撞",new Vector3(0,3.05f,0),new Vector3(1.8f,.46f,.78f));}
        foreach(var collider in portal.GetComponentsInChildren<BoxCollider>())audit.Add("Portal collider "+collider.name+" localCenter="+collider.center+" localSize="+collider.size+" worldBoundsCenter="+collider.bounds.center+" worldBoundsSize="+collider.bounds.size+" worldRotation="+collider.transform.rotation.eulerAngles);
        var portalMeshes=portal.GetComponentsInChildren<MeshRenderer>();if(portalMeshes.Length==0)throw new Exception("Portal model contains no mesh renderers");
        var portalBounds=portalMeshes[0].bounds;foreach(var renderer in portalMeshes.Skip(1))portalBounds.Encapsulate(renderer.bounds);
        audit.Add("Portal visible world bounds center="+portalBounds.center+" size="+portalBounds.size+"; root world scale="+portal.transform.lossyScale+" yaw="+portal.transform.eulerAngles.y);
        if(portalBounds.size.x<2.3f||portalBounds.size.x>3.4f||portalBounds.size.y<2.8f||portalBounds.size.y>3.8f)throw new Exception("Portal FBX world dimensions differ from authored2.8x3.28m; review collider assumptions");
        var paving=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Chapter01/WorkshopPaving.mat");
        if(!paving)throw new Exception("Missing paving material for portal landing");
        var landing=GameObject.CreatePrimitive(PrimitiveType.Cube);SceneManager.MoveGameObjectToScene(landing,work);landing.name="侧置门庭 · 石台";landing.transform.SetParent(parent,false);landing.transform.position=new Vector3(10,-.35f,63);landing.transform.localScale=new Vector3(5.8f,.64f,5.8f);landing.GetComponent<Renderer>().sharedMaterial=paving;
        audit.Add("Portal placed (10,0,63), yaw180 on 5.8m side landing. Main route x~2.65,z49..65 untouched; existing rail colliders unchanged. No traversable connector is claimed.");
    }
    static string[] Inventory()=>All<Renderer>().Select(r=>$"R|{Id(r)}|{r.enabled}|{PathOf(r.transform)}|center={r.bounds.center:F3}|size={r.bounds.size:F3}|materials={string.Join(",",r.sharedMaterials.Select(AssetDatabase.GetAssetPath))}")
        .Concat(All<Light>().Select(l=>$"L|{Id(l)}|{PathOf(l.transform)}|active={l.gameObject.activeInHierarchy}|enabled={l.enabled}|type={l.type}|intensity={l.intensity}|shadows={l.shadows}"))
        .Concat(All<MonoBehaviour>().Where(x=>!x).Select(x=>"MISSING_SCRIPT"))
        .Concat(new[]{"Skybox="+AssetDatabase.GetAssetPath(RenderSettings.skybox),"Fog="+RenderSettings.fog+" "+RenderSettings.fogMode+" "+RenderSettings.fogStartDistance+".."+RenderSettings.fogEndDistance}).ToArray();
    static void Photos(string prefix,bool cube)
    {
        var go=new GameObject("ExteriorPolishReview");SceneManager.MoveGameObjectToScene(go,work);
        var camera=go.AddComponent<Camera>();camera.scene=work;camera.enabled=false;camera.cameraType=CameraType.Game;
        camera.fieldOfView=72;camera.nearClipPlane=.03f;camera.farClipPlane=320;
        camera.clearFlags=CameraClearFlags.Skybox;camera.backgroundColor=RenderSettings.fogColor;
        camera.allowHDR=false;camera.allowMSAA=false;
        var data=go.AddComponent<UniversalAdditionalCameraData>();data.renderPostProcessing=false;data.renderShadows=true;
        data.requiresColorOption=CameraOverrideOption.Off;data.requiresDepthOption=CameraOverrideOption.Off;
        var old=RenderTexture.active;RenderTexture rt=null;Texture2D image=null;
        try
        {
            rt=new RenderTexture(1440,900,24,RenderTextureFormat.ARGB32);rt.antiAliasing=1;rt.Create();
            image=new Texture2D(1440,900,TextureFormat.RGB24,false);camera.targetTexture=rt;
            void Shot(string name,Vector3 p,Vector3 look){camera.transform.position=p;camera.transform.LookAt(look);camera.Render();RecordCamera(camera,prefix+"-"+name+".png",false);RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);image.Apply();File.WriteAllBytes(Path.Combine(Out,prefix+"-"+name+".png"),image.EncodeToPNG());}
            Shot("wake-window",new Vector3(-3.8f,1.65f,3),new Vector3(-22,1.8f,4));
            Shot("trial-window",new Vector3(4.8f,1.65f,25),new Vector3(24,2,26));
            Shot("court",new Vector3(3.7f,1.65f,40.5f),new Vector3(-3,1.8f,48));
            Shot("canal",new Vector3(3.3f,1.65f,53),new Vector3(0,1.8f,64));
            Shot("portal",new Vector3(5.3f,1.65f,59.7f),new Vector3(10,1.6f,63));
            Shot("overview",new Vector3(45,35,12),new Vector3(0,0,42));
            if(cube)
            {
                camera.targetTexture=null;camera.fieldOfView=90;
                camera.transform.position=new Vector3(3.6f,1.65f,43);camera.transform.rotation=Quaternion.identity;
                var target=new RenderTexture(1024,1024,24,RenderTextureFormat.ARGB32){dimension=TextureDimension.Cube};target.Create();
                try
                {
                    bool nativeCube=false;
                    try{nativeCube=camera.RenderToCubemap(target,63);}
                    catch(Exception e){audit.Add("Native cubemap unavailable; six perspective render fallback: "+e.Message);}
                    var faceImage=new Texture2D(1024,1024,TextureFormat.RGB24,false);
                    var flat=new RenderTexture(1024,1024,24,RenderTextureFormat.ARGB32);flat.Create();
                    try{foreach(CubemapFace face in new[]{CubemapFace.PositiveX,CubemapFace.NegativeX,CubemapFace.PositiveY,CubemapFace.NegativeY,CubemapFace.PositiveZ,CubemapFace.NegativeZ})
                    {
                        Vector3 direction=face==CubemapFace.PositiveX?Vector3.right:face==CubemapFace.NegativeX?Vector3.left:face==CubemapFace.PositiveY?Vector3.up:face==CubemapFace.NegativeY?Vector3.down:face==CubemapFace.PositiveZ?Vector3.forward:Vector3.back;
                        Vector3 up=face==CubemapFace.PositiveY?Vector3.back:face==CubemapFace.NegativeY?Vector3.forward:Vector3.up;
                        camera.transform.rotation=Quaternion.LookRotation(direction,up);camera.aspect=1;
                        if(nativeCube)Graphics.SetRenderTarget(target,0,face);
                        else
                        {
                            camera.targetTexture=flat;camera.Render();RenderTexture.active=flat;
                        }
                        RecordCamera(camera,"cube-"+face+".png",nativeCube);
                        faceImage.ReadPixels(new Rect(0,0,1024,1024),0,0);faceImage.Apply();File.WriteAllBytes(Path.Combine(Out,"cube-"+face+".png"),faceImage.EncodeToPNG());
                    }}
                    finally{camera.targetTexture=null;RenderTexture.active=old;flat.Release();Object.DestroyImmediate(flat);Object.DestroyImmediate(faceImage);}
                    File.WriteAllText(Path.Combine(Out,"cubemap-orientation.txt"),(nativeCube?"Unity Camera.RenderToCubemap":"Six 90-degree square Camera.Render fallback")+"; 6 faces, 1024px, camera world(3.6,1.65,43), identity. File names are Unity CubemapFace values. Fallback axes: +/-X and +/-Z use world +Y up; +Y face uses -Z up; -Y face uses +Z up. Direct render-target readback uses lower-left graphics origin; PNG display convention may require vertical flip for equirect conversion. Inspect matching edges before panorama acceptance. Skybox retained.\n");
                }
                finally{RenderTexture.active=old;target.Release();Object.DestroyImmediate(target);}
            }
        }
        finally{RenderTexture.active=old;camera.targetTexture=null;if(rt){rt.Release();Object.DestroyImmediate(rt);}if(image)Object.DestroyImmediate(image);Object.DestroyImmediate(go);}
    }
    static void RecordCamera(Camera camera,string image,bool nativeCubeAggregate)
    {
        var planes=GeometryUtility.CalculateFrustumPlanes(camera);
        var visible=All<Renderer>().Where(r=>r.enabled&&r.gameObject.activeInHierarchy&&((1<<r.gameObject.layer)&camera.cullingMask)!=0&&GeometryUtility.TestPlanesAABB(planes,r.bounds)).ToArray();
        var entry=new CameraStat{image=image,position=camera.transform.position,forward=camera.transform.forward,up=camera.transform.up,frustumRenderers=visible.Length,
            editorDrawCalls=UnityStats.drawCalls,editorBatches=UnityStats.batches,editorTriangles=UnityStats.triangles,
            editorStatsMeaning=nativeCubeAggregate?"Editor counters observed after one six-face native cubemap render; aggregate/last-view data, NOT this face's measured drawcalls":"Editor counters observed immediately after Camera.Render; Editor estimate, not player GPU profiling"};
        foreach(var renderer in visible)
        {
            var filter=renderer.GetComponent<MeshFilter>();var skinned=renderer as SkinnedMeshRenderer;
            var mesh=filter?filter.sharedMesh:skinned?skinned.sharedMesh:null;
            if(!mesh){entry.nonMeshRenderers++;continue;}entry.meshRenderers++;
            entry.submeshes+=mesh.subMeshCount;
            for(int i=0;i<mesh.subMeshCount;i++)if(mesh.GetTopology(i)==MeshTopology.Triangles)entry.conservativeMeshTriangles+=(long)mesh.GetIndexCount(i)/3;
        }
        cameraStats.Add(entry);
    }
}
