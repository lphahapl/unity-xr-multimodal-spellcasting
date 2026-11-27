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
public static class QingGuiR02PolishProbe
{
    const string Root=QingGuiR02MaterialAB.Root;
    const string Output="D:/UnityProjects/NarutoVR/Logs/workshop-master-tenon-qinggui-r02-20261126/unity/material-polish-r2";
    const string Delivery=QingGuiR02MaterialAB.Delivery+"/PolishProbe-r2";
    const string Key="QingGuiR02PolishProbe.Play";
    static Camera camera;static ReflectionProbe probe;static Light key,fill;
    static Quaternion[] importedRotations;static SphericalHarmonicsL2 originalAmbient;static GameObject[] models;static Material candidate;static Material[] originals;
    static IEnumerator routine;static int frame;static RenderTexture target;static Texture2D pixels;
    static bool priorRealtime;static readonly List<string> report=new();
    static QingGuiR02PolishProbe()=>EditorApplication.update+=Boot;
    public static void Run()
    {
        try {
            if(Application.dataPath.Replace('\\','/')!="D:/Temp/NarutoVR-QingGuiR01-20261125/Assets")throw new Exception("Owned project only");
            Directory.CreateDirectory(Output);Directory.CreateDirectory(Delivery);Directory.CreateDirectory(Output+"/frames");
            AssetDatabase.ImportAsset(Root+"/Shaders/QingGuiJadeLit.shader",ImportAssetOptions.ForceSynchronousImport|ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(Root+"/Shaders/QingGuiJadeTransparent.shader",ImportAssetOptions.ForceSynchronousImport|ImportAssetOptions.ForceUpdate);
            EditorSceneManager.OpenScene(Root+"/QingGuiR02_MaterialAB.unity");
            var softbox=GameObject.Find("Large softbox");softbox.transform.position=new Vector3(.3f,.4f,1.5f);softbox.transform.rotation=Quaternion.Euler(0,10,0);softbox.transform.localScale=new Vector3(.9f,1.5f,.025f);
            foreach(var t in Object.FindObjectsByType<TextMesh>(FindObjectsSortMode.None))t.characterSize=.0032f;
            var mat=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Generated/R02_Jade_Candidate.mat");
            foreach(string property in new[]{"_JadeCloud","_DepthColorStrength","_ScatterStrength","_JadeTransparency"})if(!mat.HasProperty(property))throw new Exception("Actual shader missing property "+property);
            mat.SetTexture("_JadeCloud",AssetDatabase.LoadAssetAtPath<Texture3D>(Root+"/Generated/JadeDensity.asset"));
            mat.SetFloat("_DepthColorStrength",.18f);mat.SetFloat("_ScatterStrength",.25f);mat.SetVector("_AbsorptionPerMetre",new Vector4(22,24,30,0));mat.SetFloat("_JadeLayerMode",0);mat.SetFloat("_JadeTransparency",.5f);mat.SetFloat("_ClearCoatMask",.6f);mat.EnableKeyword("_CLEARCOAT");EditorUtility.SetDirty(mat);
            File.WriteAllLines(Output+"/runtime-property-inputs.txt",Enumerable.Range(0,mat.shader.GetPropertyCount()).Select(i=>mat.shader.GetPropertyName(i)+"\t"+mat.shader.GetPropertyType(i)).Concat(new[]{"cloud="+AssetDatabase.GetAssetPath(mat.GetTexture("_JadeCloud")),"coat="+mat.GetFloat("_ClearCoatMask"),"depth="+mat.GetFloat("_DepthColorStrength"),"scatter="+mat.GetFloat("_ScatterStrength")}));
            AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),Root+"/QingGuiR02_PolishProbe.unity");SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
        }catch(Exception e){Fail(e);}
    }
    static void Boot()
    {
        if(!Application.isPlaying||EditorApplication.isCompiling||!SessionState.GetBool(Key,false))return;SessionState.SetBool(Key,false);
        try {
            models=new[]{"R01","URP Lit","R02"}.Select(n=>GameObject.Find(n+" same R01 geometry")).ToArray();
            importedRotations=models.Select(m=>m.transform.rotation).ToArray();originalAmbient=RenderSettings.ambientProbe;originals=models.Select(m=>m.GetComponentInChildren<MeshRenderer>().sharedMaterial).ToArray();candidate=originals[2];
            camera=Camera.main;camera.enabled=true;camera.backgroundColor=new Color(.05f,.055f,.06f);camera.allowHDR=true;camera.allowMSAA=false;
            key=GameObject.Find("AB key").GetComponent<Light>();fill=GameObject.Find("AB fill").GetComponent<Light>();
            probe=Object.FindFirstObjectByType<ReflectionProbe>();probe.mode=ReflectionProbeMode.Realtime;priorRealtime=QualitySettings.realtimeReflectionProbes;QualitySettings.realtimeReflectionProbes=true;
            target=new RenderTexture(1536,960,24,RenderTextureFormat.ARGBHalf,RenderTextureReadWrite.Linear);target.Create();pixels=new Texture2D(1536,960,TextureFormat.RGBAFloat,false,true);
            new GameObject("Polish probe tick").AddComponent<QingGuiR02MaterialTick>();QingGuiR02MaterialTick.Tick=Step;routine=Checks();Time.captureFramerate=30;Application.runInBackground=true;
        }catch(Exception e){Fail(e);}
    }
    static void Step()
    {
        try {
            if(++frame>500)throw new Exception("Polish probe frame limit");if(routine.MoveNext())return;
            QingGuiR02MaterialTick.Tick=null;QualitySettings.realtimeReflectionProbes=priorRealtime;
            File.WriteAllLines(Output+"/result.txt",new[]{"PASS measured reflection/coat controls; ART ACCEPTANCE PENDING. All geometry R01. Local transparent candidate is not refraction or validated Quest stereo."}.Concat(report));AssetDatabase.SaveAssets();EditorApplication.Exit(0);
        }catch(Exception e){Fail(e);}
    }
    static void Require(bool pass,string message){if(!pass)throw new Exception(message);report.Add("PASS "+message);File.WriteAllLines(Output+"/progress.txt",report);}
    static IEnumerator Checks()
    {
        int id=probe.RenderProbe();for(int i=0;i<120&&!probe.IsFinishedRendering(id);i++)yield return null;Require(probe.IsFinishedRendering(id),"Actual frontal softbox probe completed");camera.enabled=false;
        var save=SaveProbe();while(save.MoveNext())yield return save.Current;
        for(int i=0;i<8;i++)yield return null;
        Capture("01_Front_ActualFrontalSoftbox_R01_Lit_R02");
        for(int i=0;i<3;i++)models[i].transform.rotation=Quaternion.Euler(0,35,0)*importedRotations[i];for(int i=0;i<3;i++)yield return null;
        Capture("02_Front35_R01_Lit_R02_Dark");camera.backgroundColor=Color.white*.72f;foreach(var t in Object.FindObjectsByType<TextMesh>(FindObjectsSortMode.None))t.color=Color.black;
        Capture("03_Front35_R01_Lit_R02_Light");
        File.WriteAllText(Output+"/first-images-ready.txt","READY shared real front softbox, R01 geometry, same exposure.\n");
        models[0].SetActive(false);models[1].SetActive(false);models[2].transform.position=Vector3.zero;foreach(var t in Object.FindObjectsByType<TextMesh>(FindObjectsSortMode.None))t.gameObject.SetActive(false);
        camera.backgroundColor=Color.black;camera.orthographicSize=.2f;
        candidate.SetFloat("_TransmissionDebug",4);var mask=Capture("04_JadeMask");candidate.SetFloat("_TransmissionDebug",7);var coat=Capture("05_CoatMask_ActualBranch");
        var jadePixels=Enumerable.Range(0,mask.Length).Where(i=>mask[i].r>.99f).ToArray();Require(jadePixels.Length>1000,"Actual jade mask readback exists");
        Require(jadePixels.Average(i=>coat[i].r)>.58f,"Actual compiled coat mask reaches jade shader surface");
        candidate.SetFloat("_TransmissionDebug",0);var on=Capture("06_CoatOn");candidate.SetFloat("_ClearCoatMask",0);var off=Capture("07_CoatOff");
        float coatDifference=jadePixels.Max(i=>Max(on[i]-off[i]));Require(coatDifference>.001f,"Coat changes actual jade pixels; max linear absolute RGB delta="+coatDifference);
        candidate.SetFloat("_ClearCoatMask",.6f);key.intensity=0;fill.intensity=0;RenderSettings.ambientLight=Color.black;RenderSettings.ambientProbe=new SphericalHarmonicsL2();
        for(int i=0;i<5;i++)yield return null;var environment=Capture("08_EnvironmentReflectionOnly");
        float environmentPeak=jadePixels.Max(i=>Max(environment[i]));Require(environmentPeak>.005f,"Real captured environment produces jade reflected highlights; max="+environmentPeak);
        probe.intensity=0;RenderSettings.reflectionIntensity=0;for(int i=0;i<5;i++)yield return null;var dark=Capture("09_AllLightsAndEnvironmentOff");Require(jadePixels.Max(i=>Max(dark[i]))<.0002f,"All lights, SH and reflection off gives zero jade surface output");
        probe.intensity=1;RenderSettings.reflectionIntensity=1;RenderSettings.ambientLight=Color.white*.16f;RenderSettings.ambientProbe=originalAmbient;key.intensity=.9f;fill.intensity=.22f;
        // Three columns: unchanged R01, opaque R02, same R02 with measured-thickness local jade transparency.
        models[0].SetActive(true);models[1].SetActive(true);for(int i=0;i<3;i++){models[i].transform.position=new Vector3(.18f-.18f*i,0,0);models[i].transform.rotation=importedRotations[i];}
        foreach(var r in models[1].GetComponentsInChildren<MeshRenderer>())r.sharedMaterial=candidate;
        var metal=new Material(candidate);metal.SetFloat("_JadeLayerMode",1);metal.SetShaderPassEnabled("DepthOnly",false);metal.SetShaderPassEnabled("DepthNormals",false);
        var transparent=new Material(Shader.Find("NarutoVR/Review/QingGui R02 Local Jade Transparency"));transparent.CopyPropertiesFromMaterial(candidate);transparent.SetFloat("_JadeLayerMode",2);transparent.SetFloat("_JadeTransparency",.5f);transparent.EnableKeyword("_CLEARCOAT");
        AssetDatabase.CreateAsset(metal,Root+"/Generated/LocalLayer_OpaqueMetal.mat");AssetDatabase.CreateAsset(transparent,Root+"/Generated/LocalLayer_TransparentJade.mat");
        foreach(var f in models[2].GetComponentsInChildren<MeshFilter>().ToArray()) {
            var r=f.GetComponent<MeshRenderer>();r.sharedMaterial=metal;
            var layer=new GameObject("Local jade surface (R01 geometry only)");layer.transform.SetParent(f.transform,false);layer.AddComponent<MeshFilter>().sharedMesh=f.sharedMesh;var lr=layer.AddComponent<MeshRenderer>();lr.sharedMaterial=transparent;lr.shadowCastingMode=ShadowCastingMode.Off;lr.receiveShadows=true;
        }
        // Real simple background geometry, common across all columns, to reveal local opacity rather than infer it from shine.
        for(int i=0;i<8;i++) {
            var bar=GameObject.CreatePrimitive(PrimitiveType.Cube);bar.name="Shared opacity witness stripe "+i;bar.transform.position=new Vector3(0,.025f+i*.044f,-.09f);bar.transform.localScale=new Vector3(.68f,.016f,.003f);
            var m=new Material(Shader.Find("Universal Render Pipeline/Unlit"));m.SetColor("_BaseColor",Color.white*(i%2==0?.025f:.35f));bar.GetComponent<Renderer>().sharedMaterial=m;bar.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
        }
        camera.orthographicSize=.245f;camera.backgroundColor=Color.white*.2f;
        for(int i=0;i<5;i++)yield return null;Capture("10_LocalJadeLayer_R01_Opaque_LocalTransparent");
        for(int i=0;i<3;i++)models[i].transform.rotation=Quaternion.Euler(0,35,0)*importedRotations[i];for(int i=0;i<4;i++)yield return null;Capture("11_LocalJadeLayer35_R01_Opaque_LocalTransparent");
        for(int i=0;i<90;i++) {
            float angle=Mathf.Lerp(-55,55,i/89f);key.transform.rotation=Quaternion.LookRotation(-(Quaternion.Euler(0,angle,0)*new Vector3(0,.55f,.84f)));yield return null;var colors=Read();Save(colors,Output+"/frames/frame_"+i.ToString("D4")+".jpg",true);
        }
        report.Add("BOUNDARY: local transparency uses a second jade-only renderer, thickness/view-path opacity, opaque bronze first, full original shadow caster; no scene refraction or screen capture. Depth-only for metal pass disabled in this candidate; camera sorting/stereo/mobile cost unverified.");
        candidate.SetFloat("_TransmissionDebug",0);candidate.SetFloat("_ClearCoatMask",.6f);candidate.SetFloat("_JadeLayerMode",0);
    }
    static float Max(Color c)=>Mathf.Max(Mathf.Abs(c.r),Mathf.Max(Mathf.Abs(c.g),Mathf.Abs(c.b)));
    static IEnumerator SaveProbe()
    {
        var source=probe.texture;int count=source is RenderTexture rt?rt.mipmapCount:((Cubemap)source).mipmapCount;var saved=new Cubemap(source.width,TextureFormat.RGBAHalf,true);
        for(int mip=0;mip<count;mip++){var req=AsyncGPUReadback.Request(source,mip,TextureFormat.RGBAFloat);while(!req.done)yield return null;if(req.hasError||req.layerCount!=6)throw new Exception("Actual cubemap readback failed");for(int f=0;f<6;f++)saved.SetPixels(req.GetData<Color>(f).ToArray(),(CubemapFace)f,mip);}
        saved.Apply(false,false);AssetDatabase.CreateAsset(saved,Root+"/Generated/CapturedFrontalStudio.cubemap");probe.mode=ReflectionProbeMode.Baked;probe.bakedTexture=saved;QualitySettings.realtimeReflectionProbes=priorRealtime;AssetDatabase.SaveAssets();
    }
    static Color[] Read(){RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});var old=RenderTexture.active;RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,1536,960),0,0);pixels.Apply(false);RenderTexture.active=old;return pixels.GetPixels();}
    static Color[] Capture(string name){var data=Read();Save(data,Delivery+"/"+name+".png",false);return data;}
    static void Save(Color[] data,string path,bool jpg){var p=new Texture2D(1536,960,TextureFormat.RGB24,false);p.SetPixels(data.Select(c=>c.gamma).ToArray());p.Apply();File.WriteAllBytes(path,jpg?p.EncodeToJPG(94):p.EncodeToPNG());Object.Destroy(p);}
    static void Fail(Exception e){QingGuiR02MaterialTick.Tick=null;if(Application.isPlaying)QualitySettings.realtimeReflectionProbes=priorRealtime;Directory.CreateDirectory(Output);File.WriteAllText(Output+"/failure.txt",e.ToString()+"\n"+string.Join("\n",report));Debug.LogException(e);EditorApplication.Exit(3);}
}
