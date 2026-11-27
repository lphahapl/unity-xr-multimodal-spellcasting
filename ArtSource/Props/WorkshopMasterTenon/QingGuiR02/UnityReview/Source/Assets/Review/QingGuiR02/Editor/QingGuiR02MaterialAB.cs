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
using NarutoVR.QingGuiR02Review;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class QingGuiR02MaterialAB
{
    public const string Root="Assets/Review/QingGuiR02";
    public const string Output="D:/UnityProjects/NarutoVR/Logs/workshop-master-tenon-qinggui-r02-20261126/unity/material-ab-r3";
    public const string Delivery="D:/UnityProjects/NarutoVR/ArtSource/Props/WorkshopMasterTenon/QingGuiR02/UnityReview/MaterialAB";
    const string Key="QingGuiR02MaterialAB.Play";
    const string FinalizeKey="QingGuiR02MaterialAB.SaveStaticScene";
    static bool originalRealtimeProbeSetting;
    static Camera camera;static Light key,fill;static ReflectionProbe probe;static Material[] materials;
    static RenderTexture target;static Texture2D pixels;static IEnumerator routine;static int frame;
    static QingGuiR02MaterialAB()=>EditorApplication.update+=Boot;
    public static void Run()
    {
        try {
            if(Application.dataPath.Replace('\\','/')!="D:/Temp/NarutoVR-QingGuiR01-20261125/Assets")throw new Exception("Owned isolated project only");
            Directory.CreateDirectory(Output);Directory.CreateDirectory(Output+"/frames");Directory.CreateDirectory(Delivery);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/Mobile_RPAsset.asset");GraphicsSettings.defaultRenderPipeline=pipeline;QualitySettings.renderPipeline=pipeline;
            var baseline=AssetDatabase.LoadAssetAtPath<Material>(Root+"/ReferenceR01/QingGui_R01.mat");
            if(!baseline)throw new Exception("Isolated R01 baseline missing");
            var old=new Material(baseline);AssetDatabase.CreateAsset(old,Root+"/Generated/R01_Baseline.mat");
            var lit=new Material(Shader.Find("Universal Render Pipeline/Lit"));lit.CopyPropertiesFromMaterial(baseline);lit.shaderKeywords=new[]{"_NORMALMAP","_METALLICSPECGLOSSMAP","_OCCLUSIONMAP"};AssetDatabase.CreateAsset(lit,Root+"/Generated/StandardLit.mat");
            var shader=AssetDatabase.LoadAssetAtPath<Shader>(Root+"/Shaders/QingGuiJadeLit.shader");
            var errors=ShaderUtil.GetShaderMessages(shader);File.WriteAllLines(Output+"/shader-import.txt",errors.Select(e=>e.severity+" "+e.message));if(errors.Any(e=>e.severity.ToString()=="Error"))throw new Exception("Shader import failed");
            var jade=new Material(shader);jade.CopyPropertiesFromMaterial(baseline);jade.SetVector("_AbsorptionPerMetre",new Vector4(25,28,38,0));jade.SetFloat("_TransmissionStrength",.55f);jade.SetFloat("_ClearCoatMask",.6f);jade.SetFloat("_ClearCoatSmoothness",.9f);jade.SetFloat("_DepthColorStrength",.45f);jade.SetFloat("_ScatterStrength",.3f);jade.SetFloat("_InnerDepthMetres",.012f);jade.SetFloat("_CloudScaleMetres",.06f);jade.SetTexture("_JadeCloud",Cloud());jade.EnableKeyword("_CLEARCOAT");jade.EnableKeyword("_NORMALMAP");jade.EnableKeyword("_METALLICSPECGLOSSMAP");jade.EnableKeyword("_OCCLUSIONMAP");AssetDatabase.CreateAsset(jade,Root+"/Generated/R02_Jade_Candidate.mat");
            var all=new[]{old,lit,jade};string[] names={"R01", "URP Lit", "R02"};
            for(int i=0;i<3;i++) {
                var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/ReferenceR01/Model/QingGui_LOD0.fbx"));go.name=names[i]+" same R01 geometry";go.transform.position=new Vector3(.18f-.18f*i,0,0);
                foreach(var r in go.GetComponentsInChildren<MeshRenderer>()){r.sharedMaterial=all[i];r.shadowCastingMode=ShadowCastingMode.On;r.receiveShadows=true;}
                var text=new GameObject("Label "+names[i]).AddComponent<TextMesh>();text.text=names[i];text.characterSize=.012f;text.fontSize=64;text.anchor=TextAnchor.MiddleCenter;text.transform.position=new Vector3(.18f-.18f*i,-.030f,0);text.transform.rotation=Quaternion.Euler(0,180,0);text.color=Color.white;
            }
            var cam=new GameObject("AB Camera").AddComponent<Camera>();cam.tag="MainCamera";cam.transform.position=new Vector3(0,.16f,.9f);cam.transform.LookAt(new Vector3(0,.16f,0));cam.orthographic=true;cam.orthographicSize=.245f;cam.aspect=1.6f;cam.nearClipPlane=.02f;cam.farClipPlane=3;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.05f,.055f,.06f);cam.cullingMask=~(1<<28);cam.GetUniversalAdditionalCameraData().renderPostProcessing=false;
            var main=new GameObject("AB key").AddComponent<Light>();main.type=LightType.Directional;main.color=Color.white;main.intensity=.9f;main.shadows=LightShadows.Hard;main.shadowNormalBias=0;main.shadowBias=.05f;main.transform.rotation=Quaternion.LookRotation(-new Vector3(-.45f,.55f,.70f));RenderSettings.sun=main;
            var second=new GameObject("AB fill").AddComponent<Light>();second.type=LightType.Directional;second.color=Color.white;second.intensity=.22f;second.transform.rotation=Quaternion.LookRotation(-new Vector3(.65f,.25f,.55f));
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=Color.white*.16f;RenderSettings.reflectionIntensity=1;RenderSettings.skybox=null;RenderSettings.customReflection=null;
            Panel("Large softbox",new Vector3(-1.1f,1.1f,1.5f),new Vector3(.8f,1.7f,.025f),Quaternion.Euler(0,-35,0),1.4f);
            Panel("Narrow strip",new Vector3(1.3f,.7f,1.0f),new Vector3(.18f,1.8f,.025f),Quaternion.Euler(0,35,0),1.0f);
            Panel("Neutral floor",new Vector3(0,-.5f,0),new Vector3(4,.02f,4),Quaternion.identity,.20f);
            Panel("Dark back",new Vector3(0,.8f,-1.5f),new Vector3(3,2.4f,.025f),Quaternion.identity,.035f);
            var reflection=new GameObject("Captured neutral studio reflection").AddComponent<ReflectionProbe>();reflection.transform.position=new Vector3(0,.175f,0);reflection.mode=ReflectionProbeMode.Realtime;reflection.refreshMode=ReflectionProbeRefreshMode.ViaScripting;reflection.timeSlicingMode=ReflectionProbeTimeSlicingMode.NoTimeSlicing;reflection.size=Vector3.one*2;reflection.resolution=128;reflection.hdr=true;reflection.clearFlags=ReflectionProbeClearFlags.SolidColor;reflection.backgroundColor=Color.white*.18f;reflection.cullingMask=1<<28;reflection.intensity=1;
            AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),Root+"/QingGuiR02_MaterialAB.unity");SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
        }catch(Exception e){Fail(e);}
    }
    static Texture3D Cloud()
    {
        var texture=new Texture3D(32,32,32,TextureFormat.R8,true);texture.wrapMode=TextureWrapMode.Repeat;texture.filterMode=FilterMode.Trilinear;var colors=new byte[32*32*32];var random=new System.Random(2702);float[,,] grid=new float[8,8,8];for(int z=0;z<8;z++)for(int y=0;y<8;y++)for(int x=0;x<8;x++)grid[x,y,z]=(float)random.NextDouble();
        for(int z=0;z<32;z++)for(int y=0;y<32;y++)for(int x=0;x<32;x++) {
            float fx=x/4f,fy=y/4f,fz=z/4f;int ix=(int)fx,iy=(int)fy,iz=(int)fz;float tx=Mathf.SmoothStep(0,1,fx-ix),ty=Mathf.SmoothStep(0,1,fy-iy),tz=Mathf.SmoothStep(0,1,fz-iz);float v=0;
            for(int dz=0;dz<2;dz++)for(int dy=0;dy<2;dy++)for(int dx=0;dx<2;dx++)v+=grid[(ix+dx)%8,(iy+dy)%8,(iz+dz)%8]*(dx==0?1-tx:tx)*(dy==0?1-ty:ty)*(dz==0?1-tz:tz);
            colors[x+32*(y+32*z)]=(byte)Mathf.RoundToInt(v*255);
        }
        texture.SetPixelData(colors,0);texture.Apply(true,false);AssetDatabase.CreateAsset(texture,Root+"/Generated/JadeDensity.asset");return texture;
    }
    static void Panel(string name,Vector3 position,Vector3 scale,Quaternion rotation,float radiance)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.layer=28;go.transform.SetPositionAndRotation(position,rotation);go.transform.localScale=scale;var m=new Material(Shader.Find("Universal Render Pipeline/Unlit"));m.SetColor("_BaseColor",new Color(radiance,radiance,radiance,1));AssetDatabase.CreateAsset(m,Root+"/Generated/"+name.Replace(" ","")+".mat");var r=go.GetComponent<Renderer>();r.sharedMaterial=m;r.shadowCastingMode=ShadowCastingMode.Off;
    }
    static void Boot()
    {
        if(!Application.isPlaying&&!EditorApplication.isCompiling&&SessionState.GetBool(FinalizeKey,false)) {
            SessionState.SetBool(FinalizeKey,false);
            try {
                EditorSceneManager.OpenScene(Root+"/QingGuiR02_MaterialAB.unity");
                var p=Object.FindFirstObjectByType<ReflectionProbe>();p.mode=ReflectionProbeMode.Baked;p.bakedTexture=AssetDatabase.LoadAssetAtPath<Cubemap>(Root+"/Generated/CapturedNeutralStudio.cubemap");
                EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),Root+"/QingGuiR02_MaterialAB.unity");AssetDatabase.SaveAssets();
                File.WriteAllText(Output+"/result.txt","PASS R01-geometry first material A/B, moving light and real captured static reflection cubemap. Visual acceptance pending; final R02 mesh/optical regressions not claimed.\n");EditorApplication.Exit(0);
            }catch(Exception e){Fail(e);}return;
        }
        if(!Application.isPlaying||EditorApplication.isCompiling||!SessionState.GetBool(Key,false))return;SessionState.SetBool(Key,false);
        try {camera=Camera.main;camera.enabled=true;originalRealtimeProbeSetting=QualitySettings.realtimeReflectionProbes;QualitySettings.realtimeReflectionProbes=true;key=GameObject.Find("AB key").GetComponent<Light>();fill=GameObject.Find("AB fill").GetComponent<Light>();probe=Object.FindFirstObjectByType<ReflectionProbe>();materials=new[]{"R01_Baseline","StandardLit","R02_Jade_Candidate"}.Select(n=>AssetDatabase.LoadAssetAtPath<Material>(Root+"/Generated/"+n+".mat")).ToArray();target=new RenderTexture(1536,960,24);target.Create();pixels=new Texture2D(1536,960,TextureFormat.RGB24,false);Time.captureFramerate=30;Application.runInBackground=true;new GameObject("R02 AB tick").AddComponent<QingGuiR02MaterialTick>();QingGuiR02MaterialTick.Tick=Step;routine=Checks();}catch(Exception e){Fail(e);}
    }
    static void Step()
    {
        try {if(++frame>400)throw new Exception("AB capture frame limit");if(routine.MoveNext())return;QingGuiR02MaterialTick.Tick=null;QualitySettings.realtimeReflectionProbes=originalRealtimeProbeSetting;SessionState.SetBool(FinalizeKey,true);EditorApplication.ExitPlaymode();}catch(Exception e){Fail(e);}
    }
    static IEnumerator Checks()
    {
        int id=probe.RenderProbe();File.WriteAllText(Output+"/probe-lifecycle.txt",$"request={id}, cameraEnabled={camera.enabled}, realtimeProbeQuality={QualitySettings.realtimeReflectionProbes}, priorQuality={originalRealtimeProbeSetting}\n");for(int i=0;i<120&&!probe.IsFinishedRendering(id);i++)yield return null;if(!probe.IsFinishedRendering(id))throw new Exception("Reflection probe did not render actual studio cards");
        camera.enabled=false;File.AppendAllText(Output+"/probe-lifecycle.txt",$"completedAtFrame={frame}, texture={probe.texture.name}, size={probe.texture.width}x{probe.texture.height}, format={probe.texture.graphicsFormat}\n");
        if(!materials[2].IsKeywordEnabled("_CLEARCOAT"))throw new Exception("Real clearcoat variant missing");
        for(int i=0;i<10;i++)yield return null;
        Shot("01_R01_Lit_R02_DarkBackground");
        camera.backgroundColor=Color.white*.72f;foreach(var t in Object.FindObjectsByType<TextMesh>(FindObjectsSortMode.None))t.color=Color.black;yield return null;Shot("02_R01_Lit_R02_LightBackground");
        File.WriteAllText(Output+"/first-images-ready.txt","READY: three columns R01 | standard URP Lit | R02, all use the same copied R01 mesh and maps, same direct light/real captured reflection/exposure. No final R02 wire geometry yet.\n");
        var save=SaveCapturedProbe();while(save.MoveNext())yield return save.Current;
        for(int i=0;i<90;i++) {
            float angle=Mathf.Lerp(-65,65,i/89f);var direction=Quaternion.Euler(0,angle,0)*new Vector3(0,.55f,.84f);key.transform.rotation=Quaternion.LookRotation(-direction);yield return null;Render();File.WriteAllBytes(Output+"/frames/frame_"+i.ToString("D4")+".jpg",pixels.EncodeToJPG(94));
        }
        File.WriteAllText(Output+"/capture-settings.json",JsonUtility.ToJson(new Settings{baselineGeometry="Frozen R01 copy; this is shader A/B only",columns=new[]{"R01 original shader/parameters","Standard URP Lit with same source maps","R02 jade-only real clearcoat + internal density + local scatter"},r02ClearcoatVariant=materials[2].IsKeywordEnabled("_CLEARCOAT"),environment="Actual ReflectionProbe renders four neutral studio cards on layer28; same probe and lights for all three. No scene colour/opaque texture sampling.",cameraFacing="Actual model front from Unity+Z",frames=90,fps=30},true));
    }
    [Serializable] class Settings {public string baselineGeometry,environment,cameraFacing;public string[] columns;public bool r02ClearcoatVariant;public int frames,fps;}
    static IEnumerator SaveCapturedProbe()
    {
        var source=probe.texture;int mipCount=source is RenderTexture rt?rt.mipmapCount:((Cubemap)source).mipmapCount;
        var saved=new Cubemap(source.width,TextureFormat.RGBAHalf,true);saved.name="CapturedNeutralStudio";
        for(int mip=0;mip<mipCount;mip++) {
            var request=AsyncGPUReadback.Request(source,mip,TextureFormat.RGBAFloat);while(!request.done)yield return null;
            if(request.hasError||request.layerCount!=6)throw new Exception("Actual probe cubemap readback failed: mip="+mip+", layers="+request.layerCount);
            for(int face=0;face<6;face++)saved.SetPixels(request.GetData<Color>(face).ToArray(),(CubemapFace)face,mip);
        }
        saved.Apply(false,false);AssetDatabase.CreateAsset(saved,Root+"/Generated/CapturedNeutralStudio.cubemap");AssetDatabase.SaveAssets();
        probe.mode=ReflectionProbeMode.Baked;probe.bakedTexture=saved;QualitySettings.realtimeReflectionProbes=originalRealtimeProbeSetting;
        File.AppendAllText(Output+"/probe-lifecycle.txt",$"savedRealCapture={saved.width}px/{mipCount}mips, staticProbe=true, realtimeProbeQualityRestored={QualitySettings.realtimeReflectionProbes}\n");
    }
    static void Render(){RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});var old=RenderTexture.active;RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,1536,960),0,0);pixels.Apply(false);RenderTexture.active=old;}
    static void Shot(string name){Render();File.WriteAllBytes(Delivery+"/"+name+".png",pixels.EncodeToPNG());}
    static void Fail(Exception e){QingGuiR02MaterialTick.Tick=null;if(Application.isPlaying)QualitySettings.realtimeReflectionProbes=originalRealtimeProbeSetting;Directory.CreateDirectory(Output);File.WriteAllText(Output+"/failure.txt",e.ToString());Debug.LogException(e);EditorApplication.Exit(3);}
}
