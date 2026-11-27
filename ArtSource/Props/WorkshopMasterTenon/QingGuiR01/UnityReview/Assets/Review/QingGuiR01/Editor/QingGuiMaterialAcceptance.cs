using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using NarutoVR.QingGuiR01Review;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class QingGuiMaterialAcceptance
{
    public const string Root="Assets/Review/QingGuiR01";
    public const string Output="D:/UnityProjects/NarutoVR/Logs/workshop-master-tenon-qinggui-r01-20261125/unity/material-bounded";
    public const string Delivery="D:/UnityProjects/NarutoVR/ArtSource/Props/WorkshopMasterTenon/QingGuiR01/UnityReview/BoundedSamples";
    const string Key="QingGuiMaterialAcceptance.Sample";
    static Camera camera;static Light key;static GameObject blocker;static Material[] materials;
    static RenderTexture target;static Texture2D pixels;static IEnumerator routine;static int frame;
    static readonly List<string> results=new();static readonly List<Measurement> measures=new();
    [Serializable] class Measurement {public string name;public float[] meanLinearLuminance;}
    [Serializable] class Measurements {public Measurement[] cases;public string boundary;}
    static QingGuiMaterialAcceptance(){EditorApplication.update+=Boot;}
    public static void Run()
    {
        if(Application.dataPath.Replace('\\','/')!="D:/Temp/NarutoVR-QingGuiR01-20261125/Assets")throw new Exception("Dedicated QingGui project required");
        try {
            Directory.CreateDirectory(Output+"/material-samples");Directory.CreateDirectory(Delivery+"/MaterialSamples");
            var shader=AssetDatabase.LoadAssetAtPath<Shader>(Root+"/Shaders/QingGuiJadeLit.shader");if(!shader)throw new Exception("Shader not imported");
            var messages=ShaderUtil.GetShaderMessages(shader);File.WriteAllLines(Output+"/shader-import-messages.txt",messages.Select(m=>m.severity+" "+m.message+" "+m.file+":"+m.line));
            if(messages.Any(m=>m.severity.ToString()=="Error"))throw new Exception("Shader import has compilation errors");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/Mobile_RPAsset.asset");
            GraphicsSettings.defaultRenderPipeline=pipeline;QualitySettings.renderPipeline=pipeline;
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=Color.black;RenderSettings.reflectionIntensity=0;RenderSettings.skybox=null;RenderSettings.fog=false;
            var cam=new GameObject("SampleCamera").AddComponent<Camera>();cam.tag="MainCamera";cam.transform.position=new Vector3(0,0,-1);cam.transform.LookAt(Vector3.zero);cam.orthographic=true;cam.orthographicSize=.15f;cam.aspect=2;cam.nearClipPlane=.02f;cam.farClipPlane=3;cam.backgroundColor=Color.black;cam.clearFlags=CameraClearFlags.SolidColor;cam.allowHDR=true;cam.allowMSAA=false;cam.GetUniversalAdditionalCameraData().renderPostProcessing=false;
            var light=new GameObject("MainDirectional").AddComponent<Light>();light.type=LightType.Directional;light.color=Color.white;light.intensity=1;light.shadows=LightShadows.Hard;light.shadowStrength=1;light.shadowBias=.05f;light.shadowNormalBias=0;light.transform.rotation=Quaternion.LookRotation(Vector3.back);RenderSettings.sun=light;
            float[] thick={.002f,.008f,.025f,.008f};
            for(int i=0;i<4;i++) {
                var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name="MeasuredSlab_"+i;go.transform.position=new Vector3(-.225f+.15f*i,0,0);go.transform.localScale=new Vector3(.10f,.18f,thick[i]);
                var path=Root+"/Generated/Slab_"+i+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);if(!mat){mat=new Material(shader);AssetDatabase.CreateAsset(mat,path);}mat.SetFloat("_MinimumLightCosine",.01f);mat.SetVector("_BoundsMinOS",Vector3.one*-.5f);mat.SetVector("_BoundsMaxOS",Vector3.one*.5f);mat.SetFloat("_ThicknessMaxMetres",thick[i]);mat.SetFloat("_Metallic",i==3?1:0);mat.SetFloat("_Smoothness",i==3?.45f:.65f);mat.SetColor("_BaseColor",i==3?new Color(.52f,.30f,.10f,1):new Color(.38f,.56f,.44f,1));mat.SetFloat("_TransmissionDebug",1);
                if(i==3)mat.SetTexture("_JadeMask",Texture2D.blackTexture);
                EditorUtility.SetDirty(mat);go.GetComponent<Renderer>().sharedMaterial=mat;go.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.On;go.GetComponent<Renderer>().receiveShadows=true;
            }
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="ExternalOccluder";wall.transform.position=new Vector3(0,0,.16f);wall.transform.localScale=new Vector3(.85f,.45f,.025f);wall.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.ShadowsOnly;wall.SetActive(false);
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),Root+"/QingGui_MaterialSamples.unity");AssetDatabase.SaveAssets();SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
        }catch(Exception e){Fail(e);}
    }
    static void Boot()
    {
        if(!Application.isPlaying||EditorApplication.isCompiling||!SessionState.GetBool(Key,false))return;SessionState.SetBool(Key,false);
        try {
            camera=Camera.main;camera.enabled=false;key=GameObject.Find("MainDirectional").GetComponent<Light>();blocker=Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).Single(t=>t.name=="ExternalOccluder").gameObject;
            materials=Enumerable.Range(0,4).Select(i=>GameObject.Find("MeasuredSlab_"+i).GetComponent<Renderer>().sharedMaterial).ToArray();
            target=new RenderTexture(1024,512,24,RenderTextureFormat.ARGBHalf,RenderTextureReadWrite.Linear);target.Create();pixels=new Texture2D(1024,512,TextureFormat.RGBAFloat,false,true);
            Time.captureFramerate=30;Application.runInBackground=true;new GameObject("MaterialAcceptanceTick").AddComponent<QingGuiMaterialTick>();QingGuiMaterialTick.Tick=Step;routine=Checks();
        }catch(Exception e){Fail(e);}
    }
    static void Step()
    {
        try {
            if(++frame>300)throw new Exception("Material sample frame limit");
            if(routine.MoveNext())return;
            QingGuiMaterialTick.Tick=null;
            File.WriteAllLines(Output+"/material-sample-result.txt",new[]{"PASS QingGui material samples"}.Concat(results));
            File.WriteAllText(Output+"/material-sample-measurements.json",JsonUtility.ToJson(new Measurements{cases=measures.ToArray(),boundary="Known-geometry 2/8/25mm slabs plus metal under same exposure, current Mobile URP; not final model or Quest hardware. Opaque local approximation; original shadows remain enabled."},true));
            AssetDatabase.SaveAssets();EditorApplication.Exit(0);
        }catch(Exception e){Fail(e);}
    }
    static void Require(bool test,string note){if(!test)throw new Exception(note);results.Add("PASS "+note);File.WriteAllLines(Output+"/material-sample-progress.txt",results);}
    static IEnumerator Checks()
    {
        for(int i=0;i<20;i++)yield return null;
        Require(materials.All(m=>m.shader.isSupported),"Custom opaque shader supported on actual desktop graphics device "+SystemInfo.graphicsDeviceType+" / "+SystemInfo.graphicsDeviceName);
        var clear=Capture("01_Backlight_TransmissionDelta");
        Require(clear[0]>.08f && clear[1]>clear[2]*1.25f && clear[0]>clear[1]*1.10f,"Backlight delta is nonzero and decreases with real 2/8/25mm slab thickness");
        Require(clear[3]<.0001f,"Metal region transmission delta is zero");
        foreach(var m in materials)m.SetFloat("_TransmissionDebug",0);for(int i=0;i<3;i++)yield return null;Capture("02_Backlight_PBRSurface");
        foreach(var m in materials)m.SetFloat("_TransmissionDebug",1);blocker.SetActive(true);for(int i=0;i<6;i++)yield return null;
        var blocked=Capture("03_ExternalOccluder_TransmissionDelta");Require(blocked.Take(3).Sum()<clear.Take(3).Sum()*.05f,"Separate opaque object blocks entrance-shadow transmission without disabling own shadows");
        foreach(var m in materials)m.SetFloat("_TransmissionDebug",2);for(int i=0;i<3;i++)yield return null;Capture("04_ExternalOccluder_EntryVisibility");
        blocker.SetActive(false);foreach(var m in materials)m.SetFloat("_TransmissionDebug",1);key.intensity=0;for(int i=0;i<4;i++)yield return null;
        var off=Capture("05_MainLightOff_TransmissionDelta");Require(off.All(v=>v<.0001f),"Main light off makes all transmission delta zero; no emission substitute");
        key.intensity=1;key.transform.rotation=Quaternion.LookRotation(new Vector3(-.42261826f,0,.90630779f));for(int i=0;i<4;i++)yield return null;
        var front=Capture("06_FrontLight_TransmissionDelta");Require(front.All(v=>v<.0001f),"Front-lit surface produces no backlight delta");
        foreach(var m in materials)m.SetFloat("_TransmissionDebug",0);for(int i=0;i<3;i++)yield return null;Capture("07_FrontLight_PBRSurface");
        key.transform.rotation=Quaternion.LookRotation(-new Vector3(.98480775f,0,.17364818f));for(int i=0;i<3;i++)yield return null;Capture("08_GrazingSide_PBRSurface");
        foreach(var m in materials)m.SetFloat("_TransmissionDebug",1);for(int i=0;i<3;i++)yield return null;Capture("09_GrazingSide_TransmissionDelta");
        // Only the 25mm specimen remains active: no neighbouring plate may shadow it.
        var slabs=Enumerable.Range(0,4).Select(i=>GameObject.Find("MeasuredSlab_"+i)).ToArray();
        for(int i=0;i<4;i++)slabs[i].SetActive(i==2);
        for(int i=0;i<4;i++)yield return null;
        var grazeClear=Capture("10_IsolatedGrazing_TransmissionDelta");
        Require(grazeClear[2]>.002f,"25mm specimen transmits at grazing incidence through its side-exit bound");
        blocker.transform.position=new Vector3(.133f,0,0);blocker.transform.localScale=new Vector3(.006f,.28f,.12f);blocker.SetActive(true);
        for(int i=0;i<6;i++)yield return null;
        var grazeBlocked=Capture("11_CloseGrazingOccluder_TransmissionDelta");
        Require(grazeBlocked[2]<grazeClear[2]*.1f,"5mm-gap external side blocker prevents grazing transmission; exit estimate cannot skip it");
        blocker.SetActive(false);for(int i=0;i<4;i++)slabs[i].SetActive(true);
        Require(Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Where(r=>r.name.StartsWith("MeasuredSlab")).All(r=>r.shadowCastingMode==ShadowCastingMode.On && r.receiveShadows),"All four specimen renderers keep casting and receiving shadows throughout");
        Require(materials.All(m=>m.GetColor("_EmissionColor").maxColorComponent==0),"All specimen emission values remain zero");
        foreach(var m in materials)m.SetFloat("_TransmissionDebug",0);
    }
    static float[] Capture(string name)
    {
        RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});var old=RenderTexture.active;RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,1024,512),0,0);pixels.Apply(false);RenderTexture.active=old;
        var colors=pixels.GetPixels();var means=new float[4];
        for(int s=0;s<4;s++){float sum=0;int count=0;int cx=128+s*256;for(int y=244;y<268;y++)for(int x=cx-12;x<cx+12;x++){var c=colors[y*1024+x];sum+=.2126f*c.r+.7152f*c.g+.0722f*c.b;count++;}means[s]=sum/count;}
        var preview=new Texture2D(1024,512,TextureFormat.RGB24,false);preview.SetPixels(colors.Select(c=>c.gamma).ToArray());preview.Apply();var data=preview.EncodeToPNG();Object.Destroy(preview);File.WriteAllBytes(Delivery+"/MaterialSamples/"+name+".png",data);File.WriteAllBytes(Output+"/material-samples/"+name+".png",data);
        measures.Add(new Measurement{name=name,meanLinearLuminance=means});File.WriteAllText(Output+"/material-sample-measurements.partial.json",JsonUtility.ToJson(new Measurements{cases=measures.ToArray()},true));return means;
    }
    static void Fail(Exception e){QingGuiMaterialTick.Tick=null;Directory.CreateDirectory(Output);File.WriteAllText(Output+"/material-sample-failure.txt",e+"\n"+string.Join("\n",results));Debug.LogException(e);EditorApplication.Exit(3);}
}
