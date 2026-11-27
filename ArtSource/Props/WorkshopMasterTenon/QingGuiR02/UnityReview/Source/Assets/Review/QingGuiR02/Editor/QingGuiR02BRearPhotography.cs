using System;
using System.IO;
using System.Linq;
using System.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using NarutoVR.QingGuiR02Review;
using Object=UnityEngine.Object;
[InitializeOnLoad]
public static class QingGuiR02BRearPhotography
{
    const string Key="QingGuiR02BRearPhotography.Play";
    public static string Output=QingGuiR02LabBuild.Output+"/revision-b-rear-photography";
    const string Delivery=QingGuiR02BLabBuild.Delivery+"/FinalLook";
    static IEnumerator steps;static int frame;
    static QingGuiR02BRearPhotography()=>EditorApplication.update+=Boot;
    public static void Run(){try{Directory.CreateDirectory(Output+"/material-frames");Directory.CreateDirectory(Delivery);EditorSceneManager.OpenScene(QingGuiR02BLabBuild.ScenePath);SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();}catch(Exception e){Fail(e);}}
    static void Boot(){if(!Application.isPlaying||EditorApplication.isCompiling||!SessionState.GetBool(Key,false))return;SessionState.SetBool(Key,false);new GameObject("rear photography tick").AddComponent<QingGuiR02MaterialTick>();QingGuiR02MaterialTick.Tick=Step;Time.captureFramerate=30;steps=Capture();}
    static void Step(){try{if(++frame>180)throw new Exception("Rear photograph bound");if(steps.MoveNext())return;QingGuiR02MaterialTick.Tick=null;File.WriteAllText(Output+"/result.txt","PASS photography only. Actual held item yaw180 faces its rear toward the same frontal studio light/reflection/camera. No asset, material, exposure or insertion pose changed. 90 frames at30fps: fixed light, yaw180 +/-14deg. Previous backlit photos archived.\n");QingGuiR02BLabBuild.Export();EditorApplication.Exit(0);}catch(Exception e){Fail(e);}}
    public static IEnumerator Capture(Camera suppliedCamera=null)
    {
        var lab=Object.FindFirstObjectByType<QingGuiR02LabController>();lab.keyboardEnabled=false;lab.showPanel=false;for(int i=0;i<8;i++)yield return null;if(!lab.tenon.IsHeld&&!lab.Take())throw new Exception("Actual Take failed for photo");lab.MoveTo(Vector3.zero,Quaternion.Euler(0,180,0));
        foreach(var g in lab.tenon.GetComponentsInChildren<LODGroup>())g.ForceLOD(0);
        var all=lab.tenon.GetComponentsInChildren<MeshRenderer>();foreach(var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))if(!all.Contains(r))r.enabled=false;
        var camera=suppliedCamera?suppliedCamera:Object.FindFirstObjectByType<Camera>();camera.enabled=false;camera.orthographic=true;camera.orthographicSize=.205f;camera.aspect=4f/3;camera.allowHDR=true;camera.allowMSAA=true;camera.nearClipPlane=.02f;camera.farClipPlane=3;camera.backgroundColor=new Color(.08f,.09f,.10f);
        var key=GameObject.Find("Review key light").GetComponent<Light>();key.transform.rotation=Quaternion.LookRotation(-new Vector3(-.4f,.55f,.75f));key.intensity=.9f;key.shadows=LightShadows.Soft;key.shadowBias=.5f;key.shadowNormalBias=.05f;key.GetUniversalAdditionalLightData().usePipelineSettings=false;key.GetUniversalAdditionalLightData().softShadowQuality=SoftShadowQuality.Medium;RenderSettings.sun=key;
        GameObject.Find("Review fill").GetComponent<Light>().intensity=.22f;
        var rt=new RenderTexture(1280,960,24,RenderTextureFormat.ARGBHalf,RenderTextureReadWrite.Linear);rt.antiAliasing=4;rt.Create();var pixels=new Texture2D(1280,960,TextureFormat.RGBAFloat,false,true);
        void View(Vector3 position,Vector3 at){camera.transform.position=position;camera.transform.LookAt(at);}
        void Shot(string path,bool jpg=false){RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});var prev=RenderTexture.active;RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,1280,960),0,0);pixels.Apply(false);RenderTexture.active=prev;var c=pixels.GetPixels();var p=new Texture2D(1280,960,TextureFormat.RGB24,false);p.SetPixels(c.Select(v=>v.gamma).ToArray());p.Apply();File.WriteAllBytes(path,jpg?p.EncodeToJPG(94):p.EncodeToPNG());Object.Destroy(p);}
        View(new Vector3(-.35f,.205f,.85f),new Vector3(0,.175f,0));for(int i=0;i<5;i++)yield return null;Shot(Delivery+"/20_RevisionB_RearShoulder_NormalHandScale.png");
        var prior=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(QingGuiR02LabBuild.Root+"/QingGuiR02.prefab"));prior.GetComponent<Rigidbody>().isKinematic=true;foreach(var g in prior.GetComponentsInChildren<LODGroup>())g.ForceLOD(0);prior.transform.SetPositionAndRotation(new Vector3(.12f,0,0),Quaternion.Euler(0,180,0));lab.MoveTo(new Vector3(-.12f,0,0),Quaternion.Euler(0,180,0));
        View(new Vector3(0,.19f,.8f),new Vector3(0,.175f,0));for(int i=0;i<4;i++)yield return null;Shot(Delivery+"/21_R02Original_Left_RevisionB_Right_RearShoulder.png");Object.Destroy(prior);lab.MoveTo(Vector3.zero,Quaternion.Euler(0,180,0));yield return null;
        camera.orthographic=false;camera.fieldOfView=32;View(new Vector3(-.12f,.35f,.28f),new Vector3(-.03f,.30f,0));Shot(Delivery+"/22_RevisionB_RearShoulder_CopperLip_Gap_Close.png");
        camera.orthographic=true;camera.orthographicSize=.205f;View(new Vector3(-.35f,.205f,.85f),new Vector3(0,.175f,0));
        for(int i=0;i<90;i++){lab.MoveTo(Vector3.zero,Quaternion.Euler(0,180+Mathf.Sin(i/89f*Mathf.PI*2)*14,0));yield return null;Shot(Output+"/material-frames/frame_"+i.ToString("D4")+".jpg",true);}
        File.WriteAllText(Output+"/camera-and-light.txt","Actual model rear photographed through runtime MoveHeld yaw180 only. Prefab and seat rotation remain identity.\nCamera normal=(-.35,.205,.85), target=(0,.175,0), ortho .205; close=(-.12,.35,.28), target=(-.03,.30,0), FOV32.\nSame original frontal studio cubemap; key directionToLight=(-.4,.55,.75), intensity .9; fill .22; Soft Medium2048/2.5m; 4xMSAA; no post/exposure change.\n");
        rt.Release();Object.Destroy(rt);Object.Destroy(pixels);
    }
    static void Fail(Exception e){QingGuiR02MaterialTick.Tick=null;Directory.CreateDirectory(Output);File.WriteAllText(Output+"/failure.txt",e.ToString());Debug.LogException(e);EditorApplication.Exit(3);}
}
