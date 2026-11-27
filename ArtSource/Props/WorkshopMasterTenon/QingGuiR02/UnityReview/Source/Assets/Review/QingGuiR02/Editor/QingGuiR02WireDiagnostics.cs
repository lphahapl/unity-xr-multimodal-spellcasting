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
public static class QingGuiR02WireDiagnostics
{
    const string Key="QingGuiR02WireDiagnostics.Play";
    const string Output=QingGuiR02LabBuild.Output+"/wire-specular-aa";
    const string Delivery=QingGuiR02LabBuild.Delivery+"/WireSpecularAA";
    static IEnumerator steps;static int frame;
    static QingGuiR02WireDiagnostics()=>EditorApplication.update+=Boot;
    public static void Run(){try{Directory.CreateDirectory(Output);Directory.CreateDirectory(Delivery);EditorSceneManager.OpenScene(QingGuiR02LabBuild.ScenePath);SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();}catch(Exception e){Fail(e);}}
    static void Boot(){if(!Application.isPlaying||EditorApplication.isCompiling||!SessionState.GetBool(Key,false))return;SessionState.SetBool(Key,false);new GameObject("wire diagnostic tick").AddComponent<QingGuiR02MaterialTick>();QingGuiR02MaterialTick.Tick=Step;steps=Checks();}
    static void Step(){try{if(++frame>220)throw new Exception("Wire diagnostics bound");if(steps.MoveNext())return;QingGuiR02MaterialTick.Tick=null;File.WriteAllText(Output+"/result.txt","Completed isolated differential captures; see images and measurements, not aesthetic acceptance.\n");EditorApplication.Exit(0);}catch(Exception e){Fail(e);}}
    static IEnumerator Checks()
    {
        var lab=Object.FindFirstObjectByType<QingGuiR02LabController>();lab.keyboardEnabled=false;lab.showPanel=false;for(int i=0;i<8;i++)yield return null;lab.Take();lab.MoveTo(Vector3.zero,Quaternion.identity);
        foreach(var g in lab.tenon.GetComponentsInChildren<LODGroup>())g.ForceLOD(0);
        var all=lab.tenon.GetComponentsInChildren<MeshRenderer>();foreach(var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))if(!all.Contains(r))r.enabled=false;
        var wire=all.Single(r=>r.name=="QingGuiR02_GoldWire_LOD0");var gold=wire.sharedMaterial;
        var camera=Camera.main;camera.enabled=false;camera.orthographic=false;camera.fieldOfView=32;camera.aspect=4f/3;camera.allowHDR=true;camera.allowMSAA=true;camera.nearClipPlane=.02f;camera.farClipPlane=3;camera.transform.position=new Vector3(-.12f,.245f,.29f);camera.transform.LookAt(new Vector3(-.005f,.245f,0));camera.backgroundColor=new Color(.08f,.09f,.10f);
        var key=GameObject.Find("Review key light").GetComponent<Light>();key.transform.rotation=Quaternion.LookRotation(-new Vector3(-.9f,.25f,.22f));key.intensity=.9f;key.shadows=LightShadows.Hard;key.shadowBias=.02f;key.shadowNormalBias=0;RenderSettings.sun=key;
        var data=key.GetUniversalAdditionalLightData();var pipeline=(UniversalRenderPipelineAsset)QualitySettings.renderPipeline;
        File.WriteAllText(Output+"/initial-shadow-inputs.txt",$"actualUsePipeline={data.usePipelineSettings}; pipelineBias={pipeline.shadowDepthBias}/{pipeline.shadowNormalBias}; lightBias={key.shadowBias}/{key.shadowNormalBias}; shadowMap={pipeline.mainLightShadowmapResolution}; distance={pipeline.shadowDistance}; cameraMSAA4; goldSmoothness={gold.GetFloat("_Smoothness")}\n");
        var rt=new RenderTexture(1280,960,24,RenderTextureFormat.ARGBHalf,RenderTextureReadWrite.Linear);rt.antiAliasing=4;rt.Create();var pixels=new Texture2D(1280,960,TextureFormat.RGBAFloat,false,true);
        Color[] Shot(string name){RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});var prev=RenderTexture.active;RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,1280,960),0,0);pixels.Apply(false);RenderTexture.active=prev;var c=pixels.GetPixels();var p=new Texture2D(1280,960,TextureFormat.RGB24,false);p.SetPixels(c.Select(v=>v.gamma).ToArray());p.Apply();File.WriteAllBytes(Delivery+"/"+name+".png",p.EncodeToPNG());Object.Destroy(p);return c;}
        gold.DisableKeyword("_RECEIVE_SHADOWS_OFF");for(int i=0;i<5;i++)yield return null;Shot("01_Lit_GeometricSpecAA_PipelineBias1_1");yield break;
        var unlit=new Material(Shader.Find("Universal Render Pipeline/Unlit"));unlit.SetColor("_BaseColor",new Color(.76f,.47f,.14f));unlit.renderQueue=3002;wire.sharedMaterial=unlit;for(int i=0;i<4;i++)yield return null;Shot("02_UnlitGold_SameDepth");wire.sharedMaterial=gold;
        gold.EnableKeyword("_RECEIVE_SHADOWS_OFF");for(int i=0;i<4;i++)yield return null;Shot("03_Lit_OnlyReceiveShadowsOff");gold.DisableKeyword("_RECEIVE_SHADOWS_OFF");
        data.usePipelineSettings=false;key.shadowBias=.02f;key.shadowNormalBias=0;for(int i=0;i<4;i++)yield return null;Shot("04_ExplicitBias002_Normal0");
        key.shadowNormalBias=.05f;for(int i=0;i<4;i++)yield return null;var shadow=Shot("05_ExplicitBias002_Normal005");
        wire.shadowCastingMode=ShadowCastingMode.Off;for(int i=0;i<4;i++)yield return null;var noShadow=Shot("06_OnlyWireCastingOff");wire.shadowCastingMode=ShadowCastingMode.On;
        float difference=Enumerable.Range(0,shadow.Length).Max(i=>Mathf.Max(Mathf.Abs(shadow[i].r-noShadow[i].r),Mathf.Max(Mathf.Abs(shadow[i].g-noShadow[i].g),Mathf.Abs(shadow[i].b-noShadow[i].b))));File.WriteAllText(Output+"/wire-cast-delta.txt",difference.ToString("R"));
        key.shadowNormalBias=.15f;for(int i=0;i<4;i++)yield return null;Shot("07_ExplicitBias002_Normal015");
        gold.DisableKeyword("_RECEIVE_SHADOWS_OFF");EditorUtility.SetDirty(gold);AssetDatabase.SaveAssets();Object.Destroy(unlit);rt.Release();Object.Destroy(rt);Object.Destroy(pixels);
    }
    static void Fail(Exception e){QingGuiR02MaterialTick.Tick=null;Directory.CreateDirectory(Output);File.WriteAllText(Output+"/failure.txt",e.ToString());Debug.LogException(e);EditorApplication.Exit(3);}
}
