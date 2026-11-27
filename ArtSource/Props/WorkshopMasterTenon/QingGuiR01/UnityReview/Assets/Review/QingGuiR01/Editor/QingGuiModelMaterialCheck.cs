using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using NarutoVR.QingGuiR01Review;
using Object=UnityEngine.Object;

// Actual final-model readback, distinct from the known-thickness slab controls.
public static class QingGuiModelMaterialCheck
{
    [Serializable] class Readback {public string name;public double luminanceSum;public float maximum;public int nonzeroPixels;}
    [Serializable] class Results {public Readback[] frames;public int fullyMetalPixels;public float maximumMetalTransmission;public float[] jadeThicknessPercentilesMM;public string boundary;}
    [Serializable] class TextureState {public string property,path,textureFormat,importType;public bool sRGB;public int width,height;}
    [Serializable] class Inputs {public TextureState[] textures;public string shader;public float transmissionStrength,minimumLightCosine;public Vector4 absorptionPerMetre,meshBoundsMinOS,meshBoundsMaxOS;}
    public static IEnumerator Run(QingGuiR01LabController lab,Camera camera,string output,string delivery,Action<bool,string> require)
    {
        require(lab.Take(),"Take after physical review to inspect actual model material independently");
        lab.MoveTo(Vector3.zero,Quaternion.identity);
        var item=lab.tenon;var renderers=item.GetComponentsInChildren<MeshRenderer>();
        foreach(var group in item.GetComponentsInChildren<LODGroup>())group.ForceLOD(0);
        foreach(var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))if(!renderers.Contains(r))r.enabled=false;
        var material=renderers.First().sharedMaterial;
        var states=new List<TextureState>();
        foreach(var property in new[]{"_BaseMap","_BumpMap","_MetallicGlossMap","_OcclusionMap","_ThicknessMap","_JadeMask"}) {
            var texture=material.GetTexture(property) as Texture2D;require(texture,"Actual model has bound "+property);
            var path=AssetDatabase.GetAssetPath(texture);var importer=AssetImporter.GetAtPath(path) as TextureImporter;
            states.Add(new TextureState{property=property,path=path,sRGB=importer.sRGBTexture,importType=importer.textureType.ToString(),width=texture.width,height=texture.height,textureFormat=texture.format.ToString()});
            require(importer.sRGBTexture==(property=="_BaseMap"),"Correct colour space on "+property);
            if(property=="_BumpMap")require(importer.textureType==TextureImporterType.NormalMap,"Actual NormalGL is imported as a tangent normal map");
        }
        File.WriteAllText(output+"/actual-material-inputs.json",JsonUtility.ToJson(new Inputs{textures=states.ToArray(),shader=material.shader.name,transmissionStrength=material.GetFloat("_TransmissionStrength"),minimumLightCosine=material.GetFloat("_MinimumLightCosine"),absorptionPerMetre=material.GetVector("_AbsorptionPerMetre"),meshBoundsMinOS=material.GetVector("_BoundsMinOS"),meshBoundsMaxOS=material.GetVector("_BoundsMaxOS")},true));
        var key=GameObject.Find("Review key light").GetComponent<Light>();
        foreach(var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))light.intensity=light==key?1:0;
        key.color=Color.white;key.shadows=LightShadows.Hard;key.shadowStrength=1;key.shadowBias=.05f;key.shadowNormalBias=0;RenderSettings.sun=key;
        RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=Color.black;RenderSettings.reflectionIntensity=0;RenderSettings.skybox=null;
        camera.transform.position=new Vector3(0,.175f,-.8f);camera.transform.LookAt(new Vector3(0,.175f,0));camera.orthographic=true;camera.orthographicSize=.20f;camera.aspect=4f/3;camera.backgroundColor=Color.black;camera.allowHDR=true;camera.nearClipPlane=.02f;camera.farClipPlane=2;
        var target=new RenderTexture(1024,768,24,RenderTextureFormat.ARGBHalf,RenderTextureReadWrite.Linear);target.Create();
        var pixels=new Texture2D(1024,768,TextureFormat.RGBAFloat,false,true);var measurements=new List<Readback>();
        string images=delivery+"/MaterialModel";Directory.CreateDirectory(images);
        Color[] Capture(string name) {
            RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});var old=RenderTexture.active;RenderTexture.active=target;
            pixels.ReadPixels(new Rect(0,0,1024,768),0,0);pixels.Apply(false);RenderTexture.active=old;var colors=pixels.GetPixels();
            var preview=new Texture2D(1024,768,TextureFormat.RGB24,false);preview.SetPixels(colors.Select(c=>c.gamma).ToArray());preview.Apply();File.WriteAllBytes(images+"/"+name+".png",preview.EncodeToPNG());Object.Destroy(preview);
            measurements.Add(new Readback{name=name,luminanceSum=colors.Sum(c=>(double)(.2126f*c.r+.7152f*c.g+.0722f*c.b)),maximum=colors.Max(c=>Mathf.Max(c.r,Mathf.Max(c.g,c.b))),nonzeroPixels=colors.Count(c=>Mathf.Max(c.r,Mathf.Max(c.g,c.b))>.0001f)});return colors;
        }
        key.transform.rotation=Quaternion.LookRotation(new Vector3(-.42261826f,0,.90630779f));material.SetFloat("_TransmissionDebug",0);
        for(int i=0;i<5;i++)yield return null;Capture("01_Front_PBR_SameExposure");
        key.transform.rotation=Quaternion.LookRotation(-new Vector3(.93969262f,0,.34202014f));
        for(int i=0;i<5;i++)yield return null;Capture("02_Side_PBR_SameExposure");
        key.transform.rotation=Quaternion.LookRotation(Vector3.back);
        for(int i=0;i<5;i++)yield return null;Capture("03_Back_PBR_SameExposure");
        material.SetFloat("_TransmissionDebug",1);for(int i=0;i<5;i++)yield return null;var clear=Capture("04_Back_TransmissionDelta");
        double clearSum=measurements.Last().luminanceSum;
        require(clearSum>1 && measurements.Last().maximum>.002f,"Actual frozen model has a light-driven nonzero backlight response");
        material.SetFloat("_TransmissionDebug",4);for(int i=0;i<3;i++)yield return null;var jade=Capture("05_JadeMask_Readback");
        material.SetFloat("_TransmissionDebug",5);for(int i=0;i<3;i++)yield return null;var metal=Capture("06_Metallic_Readback");
        var metalPixels=Enumerable.Range(0,metal.Length).Where(i=>metal[i].r>.999f && jade[i].r<.001f).ToArray();
        require(metalPixels.Length>20,"Actual model contains fully metallic bronze/gold pixels in the camera view");
        float metalDelta=metalPixels.Max(i=>Mathf.Max(clear[i].r,Mathf.Max(clear[i].g,clear[i].b)));
        require(metalDelta<.0002f,"Actual bronze/gold pixels have zero transmission delta");
        material.SetFloat("_TransmissionDebug",3);for(int i=0;i<3;i++)yield return null;var thick=Capture("07_MeasuredThickness_Readback");
        var thickness=Enumerable.Range(0,jade.Length).Where(i=>jade[i].r>.98f).Select(i=>thick[i].r*120).OrderBy(x=>x).ToArray();
        require(thickness.Length>100 && thickness.Last()-thickness.First()>5,"Actual jade map carries varying measured thickness (mm), independent of AO");
        var thicknessPercentiles=new[]{.01,.25,.5,.75,.99}.Select(q=>thickness[(int)((thickness.Length-1)*q)]).ToArray();
        material.SetFloat("_TransmissionDebug",1);key.intensity=0;for(int i=0;i<5;i++)yield return null;Capture("08_LightOff_TransmissionDelta");
        require(measurements.Last().maximum<.0002f,"Actual model transmission vanishes when main light is disabled");
        key.intensity=1;var blocker=GameObject.CreatePrimitive(PrimitiveType.Cube);blocker.name="External blocking slab for actual-model optical test";blocker.transform.position=new Vector3(0,.175f,.12f);blocker.transform.localScale=new Vector3(.6f,.7f,.025f);blocker.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.ShadowsOnly;
        for(int i=0;i<8;i++)yield return null;Capture("09_ExternalBlocker_TransmissionDelta");
        require(measurements.Last().luminanceSum<clearSum*.05,"Actual opaque external slab blocks final-model backlight response");
        require(renderers.All(r=>r.shadowCastingMode==ShadowCastingMode.On && r.receiveShadows),"Actual model continues to cast and receive shadows during optical tests");
        require(material.GetColor("_EmissionColor").maxColorComponent==0,"Actual material does not use emission for jade light response");
        File.WriteAllText(output+"/actual-model-light-measurements.json",JsonUtility.ToJson(new Results{frames=measurements.ToArray(),fullyMetalPixels=metalPixels.Length,maximumMetalTransmission=metalDelta,jadeThicknessPercentilesMM=thicknessPercentiles,boundary="Desktop D3D11 current Mobile URP, final model maps, identical white directional intensity 1 and no post exposure adjustment. PBR plus opaque main-light thickness approximation. Not volumetric/screen-space SSS, refraction, Quest hardware or physical jade optical calibration."},true));
        material.SetFloat("_TransmissionDebug",0);Object.Destroy(blocker);target.Release();Object.Destroy(target);Object.Destroy(pixels);
    }
}
