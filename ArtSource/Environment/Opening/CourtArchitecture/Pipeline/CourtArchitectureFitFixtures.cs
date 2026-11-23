using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;

public static class CourtArchitectureFitFixtures {
    const string Root="Assets/Art/Environment/Opening/CourtArchitecture/";
    const string ScenePath="Assets/Scene/Chapter01_Awakening.unity";
    const string SetPath=Root+"CourtHallRoofSet.prefab";
    const string Out="CourtStructureCheck/";
    static GameObject Insert(string path,Transform parent=null) {
        var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));
        if(parent)go.transform.SetParent(parent,false);return go;
    }
    static string PathOf(string n){return Root+n+"/"+n+".prefab";}
    static void Configure(Transform g,Vector3 pos,Vector3 scale){
        g.localPosition=pos;g.localRotation=Quaternion.Euler(0,90,0);g.localScale=scale;
        PrefabUtility.RecordPrefabInstancePropertyModifications(g);
    }
    static void FrontLight() {
        RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.65f,.68f,.73f);
        var sun=new GameObject("Review sun").AddComponent<Light>();
        sun.type=LightType.Directional;sun.color=new Color(1,.91f,.8f);sun.intensity=1.7f;sun.transform.rotation=Quaternion.Euler(36,-28,0);
        sun.shadows=LightShadows.Soft;
    }
    static void Shot(string file,Vector3 eye,Vector3 target,float fov){
        var cam=new GameObject("Review camera").AddComponent<Camera>();cam.enabled=false;cam.stereoTargetEye=StereoTargetEyeMask.None;
        cam.clearFlags=RenderSettings.skybox?CameraClearFlags.Skybox:CameraClearFlags.SolidColor;
        cam.backgroundColor=new Color(.54f,.64f,.70f);cam.nearClipPlane=.05f;cam.farClipPlane=210;cam.fieldOfView=fov;
        var u=cam.gameObject.AddComponent<UniversalAdditionalCameraData>();u.renderPostProcessing=false;
        cam.transform.position=eye;cam.transform.LookAt(target);
        const int w=1600,h=900;var rt=new RenderTexture(w,h,24);rt.Create();cam.targetTexture=rt;
        cam.Render();cam.Render();RenderTexture.active=rt;var tex=new Texture2D(w,h,TextureFormat.RGB24,false);
        tex.ReadPixels(new Rect(0,0,w,h),0,0);tex.Apply();
        File.WriteAllBytes(Out+file+".jpg",tex.EncodeToJPG(94));cam.targetTexture=null;RenderTexture.active=null;
        rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(tex);Object.DestroyImmediate(cam.gameObject);
    }
    static Vector3[] Windows(){
        return new[]{new Vector3(-6.38f,0,.2f),new Vector3(-6.38f,0,3.6f),new Vector3(-6.38f,0,7f),
           new Vector3(6.38f,0,1.2f),new Vector3(6.38f,0,6.5f),new Vector3(7.2f,0,20.8f),new Vector3(7.2f,0,26f),new Vector3(7.2f,0,31.2f),
           new Vector3(-7.2f,0,21.7f),new Vector3(-7.2f,0,29.7f)};
    }
    static void CheckWindows(StringBuilder sb,GameObject set){
        const int layer=29;var gs=set.GetComponentsInChildren<LODGroup>();var cols=new List<MeshCollider>();
        foreach(var g in gs)foreach(var r in g.GetLODs()[0].renderers){
            r.gameObject.layer=layer;var c=r.gameObject.AddComponent<MeshCollider>();c.sharedMesh=r.GetComponent<MeshFilter>().sharedMesh;cols.Add(c);
        }Physics.SyncTransforms();
        foreach(var point in Windows()) {
            var direction=Vector3.right*Mathf.Sign(point.x);int rays=0,baseClear=0;
            foreach(float z in new[]{-.21f,0,.21f})foreach(float h in new[]{1.18f,1.68f,2.15f,2.58f,2.93f}){
                var origin=point - direction*1 + Vector3.up*h + Vector3.forward*z;
                if(Physics.Raycast(origin,direction,out var hit,4f,1<<layer,QueryTriggerInteraction.Ignore))
                   throw new InvalidOperationException("Roofing intersects existing window "+point+" at "+hit.point);
                if(!Physics.Raycast(origin,direction,2.0f,~(1<<layer),QueryTriggerInteraction.Ignore))baseClear++;
                rays++;
            }
            sb.AppendLine("Existing window x="+point.x+" z="+point.z+" new roof intersections 0/"+rays+"; existing wall/trim-clear baseline rays="+baseClear+"/"+rays);
        }
        foreach(var c in cols){c.gameObject.layer=0;Object.DestroyImmediate(c);}
    }
    static void AddRoofSet(StringBuilder sb){
        var sc=EditorSceneManager.OpenScene(ScenePath);
        var all=sc.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<MeshRenderer>(true)).ToArray();
        string[] names={"室顶","通廊顶","试炼间顶"};
        var slabs=names.Select(n=>all.Single(x=>x.name==n&&x.bounds.min.y>2.9f&&x.bounds.size.y<.8f)).ToArray();
        var old=sc.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<LODGroup>(true))
            .Where(x=>names.Any(n=>x.name=="工官建筑 · "+n)).ToArray();
        if(old.Length!=3)throw new Exception("Expected three A01 replacements, saw "+old.Length);
        // Only the isolated, unsaved review scene toggles the older exterior layers.
        foreach(var g in old)g.gameObject.SetActive(false);
        var root=new GameObject("CourtHallRoofSet");
        for(int i=0;i<3;i++){
            var b=slabs[i].bounds;var at=new Vector3(b.center.x,b.max.y+.025f,b.center.z);
            var go=Insert(PathOf(i==2?"CourtMainRoof":"CourtLowWing"),root.transform);
            var scale=i==2?Vector3.one:i==1?new Vector3(.590f,.64f,.83f):new Vector3(1.0f,1.0f,1.62f);
            Configure(go.transform,at,scale);var r=go.GetComponent<LODGroup>().GetLODs()[0].renderers[0];
            sb.AppendLine("Room "+names[i]+" raw slab="+b+"; added at "+at.ToString("F3")+" yaw 90 localScale "+scale.ToString("F3")+" visible LOD0 "+r.bounds);
            if(r.bounds.min.y<2.96f||r.bounds.min.x>b.min.x||r.bounds.max.x<b.max.x||r.bounds.min.z>b.min.z||r.bounds.max.z<b.max.z)
                throw new Exception("Roof upper wall fit failed "+names[i]);
        }
        PrefabUtility.SaveAsPrefabAsset(root,SetPath);Object.DestroyImmediate(root);
        var check=Insert(SetPath);
        foreach(Transform c in check.transform)if(PrefabUtility.GetPrefabInstanceStatus(c.gameObject)!=PrefabInstanceStatus.Connected)throw new Exception("Lost nested source "+c.name);
        if(check.GetComponentsInChildren<Collider>(true).Length!=0)throw new Exception("Visual-only roof set contains physical blockers");
        CheckWindows(sb,check);
        foreach(var lod in check.GetComponentsInChildren<LODGroup>())lod.ForceLOD(0);
        Shot("H01_both_rooms_in_original_scene",new Vector3(29,19,45),new Vector3(0,4.6f,15),52);
        // Inspect fitted architecture in a neutral outside lighting rig to preserve detail.
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var set=Insert(SetPath);foreach(var g in set.GetComponentsInChildren<LODGroup>())g.ForceLOD(0);
        FrontLight();Shot("H01_roof_arrangement",new Vector3(30,21,41),new Vector3(0,6.1f,16),45);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var gate=Insert(PathOf("CourtGatehouse"));gate.GetComponent<LODGroup>().ForceLOD(0);
        var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.position=Vector3.down*.23f;floor.transform.localScale=new Vector3(18,.44f,19);
        floor.GetComponent<Renderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Chapter01/WorkshopPaving.mat");
        FrontLight();Shot("H02_front_entrance",new Vector3(6.7f,3.65f,-9.8f),new Vector3(0,2.6f,0),41);
        Shot("H02_eye_reverse",new Vector3(-.8f,1.59f,7.8f),new Vector3(.18f,2.17f,-.1f),60);
    }
    public static void All(){
        Directory.CreateDirectory(Out);
        string p=Path.Combine(Environment.CurrentDirectory,"CourtStructureCheck","gate-collider.json");
        Environment.SetEnvironmentVariable("COURT_GATE_COLLIDERS",p);
        foreach(string key in new[]{"CourtMainRoof","CourtLowWing","CourtGatehouse"}){
            Environment.SetEnvironmentVariable("COURT_STRUCTURE",key);
            CourtStructureImport.Import();
        }
        var sb=new StringBuilder();AddRoofSet(sb);AssetDatabase.SaveAssets();File.WriteAllText(Out+"FitChecks.txt",sb.ToString());Debug.Log("COURT_ARCHITECTURE_FIT_PASS "+sb);
    }
}
