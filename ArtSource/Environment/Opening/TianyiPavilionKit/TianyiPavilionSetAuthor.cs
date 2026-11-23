using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;

public static class TianyiPavilionSetAuthor {
    const string Kit="Assets/Art/Environment/Opening/TianyiPavilionKit/";
    const string SetPath=Kit+"TianyiPavilionSet.prefab";
    const string ScenePath="Assets/Scene/Chapter01_Awakening.unity";
    static Bounds VisibleBounds(GameObject go){
        var renderers=go.GetComponentsInChildren<Renderer>(true).Where(x=>x is MeshRenderer || x is SkinnedMeshRenderer).ToArray();
        if(renderers.Length==0)throw new Exception("No geometry on "+go.name);
        Bounds b=renderers[0].bounds;foreach(var r in renderers)b.Encapsulate(r.bounds);return b;
    }
    static GameObject Insert(string path,Transform parent) {
        var original=AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if(!original)throw new Exception("Missing prefab "+path);
        var nested=(GameObject)PrefabUtility.InstantiatePrefab(original);
        nested.transform.SetParent(parent,false);return nested;
    }
    public static void Run() {
        string device=Environment.GetEnvironmentVariable("TIANYI_DISPLAY_PREFAB");
        if(string.IsNullOrEmpty(device))throw new ArgumentException("TIANYI_DISPLAY_PREFAB must name the completed prop");
        var witness=EditorSceneManager.OpenScene(ScenePath);
        var backdrop=witness.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<LODGroup>(true))
            .Select(g=>g.GetLODs()).Where(level=>level.Length>=2)
            .SelectMany(level=>level[1].renderers).OfType<MeshRenderer>()
            .Where(r=>r.sharedMaterials.Any(m=>m && (m.name=="RidgeStrata" || m.name=="DistantStrata")))
            .Select(r=>new {mesh=r.GetComponent<MeshFilter>().sharedMesh,materials=r.sharedMaterials,
                pos=r.transform.position,rot=r.transform.rotation,scale=r.transform.lossyScale}).ToArray();
        Material sky=RenderSettings.skybox;
        var sc=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var assembled=new GameObject("天仪阁 · 仪器陈设");
        Insert(Kit+"TianyiPavilion.prefab",assembled.transform);
        var exhibit=Insert(device,assembled.transform);
        var b=VisibleBounds(exhibit);
        exhibit.transform.position+=new Vector3(-b.center.x,-b.min.y,-b.center.z);
        PrefabUtility.RecordPrefabInstancePropertyModifications(exhibit.transform);
        PrefabUtility.SaveAsPrefabAsset(assembled,SetPath);Object.DestroyImmediate(assembled);
        AssetDatabase.SaveAssets();
        var actual=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(SetPath));
        if(actual.transform.childCount!=2)throw new Exception("Expected exactly two nested roots.");
        foreach(Transform child in actual.transform)
            if(PrefabUtility.GetPrefabInstanceStatus(child.gameObject)!=PrefabInstanceStatus.Connected)throw new Exception("Lost nested prefab for "+child.name);
        Physics.SyncTransforms();
        var target=actual.transform.GetChild(1).gameObject;var bounds=VisibleBounds(target);
        if(bounds.min.y<-.025f||bounds.min.y>.025f||bounds.size.y>5.45f)throw new Exception("Instrument misaligned vertically "+bounds);
        var clearance=new StringBuilder();clearance.AppendLine("Environment + exhibit "+device+"; nested roots=2; exhibit bounds (m) "+bounds);
        Vector3 eye=new Vector3(0,1,0);
        foreach(float side in new[]{-1f,1f}){
            // Each segment checks a standing capsule against actual colliders, as well as
            // ensuring it stays clear of the visible heavy instrument, including its underside.
            var points=new[]{new Vector3(0,0,-5.4f),new Vector3(0,0,-3.45f),new Vector3(side*2.85f,0,-2.95f),new Vector3(side*2.85f,0,2.95f),new Vector3(0,0,3.45f),new Vector3(0,0,5.4f)};
            float minGap=99;
            for(int i=1;i<points.Length;i++){
                var start=points[i-1];var delta=points[i]-start;
                if(Physics.CapsuleCast(start+new Vector3(0,.33f,0),start+new Vector3(0,1.59f,0),.29f,delta.normalized,out var hit,delta.magnitude,~0,QueryTriggerInteraction.Ignore))throw new Exception("Blocked player route "+hit.collider.name);
                for(int j=0;j<=20;j++) {
                    var q=Vector3.Lerp(start,points[i],j/20f)+Vector3.up*.7f;
                    var closest=bounds.ClosestPoint(q);var gap=new Vector2(q.x-closest.x,q.z-closest.z).magnitude-.29f;minGap=Mathf.Min(minGap,gap);
                    if(gap<=0f)throw new Exception("Visible exhibit touches swept capsule");
                }
            }
            clearance.AppendLine("Walk around "+side+" (r=0.29m) passed, minimum horizontal capsule gap to exhibit AABB "+minGap.ToString("F3")+" m");
        }
        if(Physics.Raycast(new Vector3(0,bounds.max.y+.08f,0),Vector3.up,7))throw new Exception("Skylight over instrument blocked");
        Directory.CreateDirectory("PavilionReview");File.WriteAllText("PavilionReview/set-checks.txt",clearance.ToString());
        RenderSettings.skybox=sky;RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.54f,.57f,.58f);
        // Original distant ridge meshes and the existing portal are photographic context only.
        foreach(var r in backdrop){
            var ridge=new GameObject("远山取景");ridge.transform.position=r.pos-new Vector3(2.67f,0,64.65f);
            ridge.transform.rotation=r.rot;ridge.transform.localScale=r.scale;ridge.AddComponent<MeshFilter>().sharedMesh=r.mesh;
            ridge.AddComponent<MeshRenderer>().sharedMaterials=r.materials;
        }
        var gate=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Environment/Opening/PortalGate/PortalGate.fbx"));
        gate.transform.position=new Vector3(0,0,12);gate.transform.rotation=Quaternion.Euler(0,180,0);
        var sun=new GameObject("天光").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=2.05f;sun.transform.rotation=Quaternion.Euler(80,-32,0);
        sun.shadows=LightShadows.Soft;
        foreach(float side in new[]{-1f,1f}){var light=new GameObject("器物近身补光").AddComponent<Light>();light.type=LightType.Point;light.range=5.4f;light.intensity=2.1f;light.color=new Color(1f,.83f,.65f);light.transform.position=new Vector3(side*2.15f,2.4f,side*1.75f);light.renderMode=LightRenderMode.ForcePixel;}
        // A neutral outside floor grounds the view through a portal in the isolated lookdev room.
        var apron=GameObject.CreatePrimitive(PrimitiveType.Cube);apron.transform.position=new Vector3(0,-.25f,0);apron.transform.localScale=new Vector3(13,.30f,23);
        apron.GetComponent<Renderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Chapter01/WorkshopPaving.mat");
        foreach(var lod in actual.GetComponentsInChildren<LODGroup>(true))lod.ForceLOD(0);
        var cam=new GameObject("房间机位").AddComponent<Camera>();cam.enabled=false;cam.clearFlags=sky?CameraClearFlags.Skybox:CameraClearFlags.SolidColor;
        cam.backgroundColor=new Color(.52f,.61f,.65f);cam.nearClipPlane=.035f;cam.farClipPlane=100;cam.fieldOfView=74f;
        cam.stereoTargetEye=StereoTargetEyeMask.None;cam.gameObject.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing=false;
        int w=1600,h=900;var rt=new RenderTexture(w,h,24);rt.Create();cam.targetTexture=rt;
        var tex=new Texture2D(w,h,TextureFormat.RGB24,false);
        foreach(float sign in new[]{-1f,1f}){
            cam.transform.position=new Vector3(sign*2.00f,1.59f,sign*3.70f);
            cam.transform.LookAt(new Vector3(0,bounds.max.y*.52f,0));cam.Render();cam.Render();
            RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,w,h),0,0);tex.Apply();
            File.WriteAllBytes("PavilionReview/"+(sign<0?"03_入阁看天仪":"04_阁后回看天仪")+".jpg",tex.EncodeToJPG(93));
        }
        cam.targetTexture=null;RenderTexture.active=null;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(tex);
        Debug.Log("PAVILION_SET_PASS "+clearance);
    }
}
