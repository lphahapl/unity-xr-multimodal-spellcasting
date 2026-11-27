using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using NarutoVR.QingGuiR02Review;
using Object=UnityEngine.Object;

public static class QingGuiR02FinalLook
{
    public static IEnumerator Run(QingGuiR02LabController lab,Camera camera,string output,string delivery,Action<bool,string> require,string viewFolder="FinalLookReady")
    {
        require(lab.Take(),"Take final R02 for material photography");lab.MoveTo(Vector3.zero,Quaternion.identity);var item=lab.tenon;
        foreach(var g in item.GetComponentsInChildren<LODGroup>())g.ForceLOD(0);
        var all=item.GetComponentsInChildren<MeshRenderer>();foreach(var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))if(!all.Contains(r))r.enabled=false;
        var body=all.Single(r=>r.name=="QingGuiR02_Body_LOD0");var layer=all.Single(r=>r.name=="QingGuiR02_Body_LOD0_LocalJade");var wire=all.Single(r=>r.name=="QingGuiR02_GoldWire_LOD0");
        var metal=body.sharedMaterial;var jade=layer.sharedMaterial;var gold=wire.sharedMaterial;
        require(jade.GetFloat("_JadeTransparency")==.6f&&metal.renderQueue==3001&&jade.renderQueue==3000&&gold.renderQueue==3002&&gold.GetFloat("_Surface")==0,"Actual .60 jade with forward depth, later fully opaque bronze/gold bound");
        var key=GameObject.Find("Review key light").GetComponent<Light>();var fill=GameObject.Find("Review fill").GetComponent<Light>();
        var ambient=RenderSettings.ambientProbe;var ambientMode=RenderSettings.ambientMode;var sky=RenderSettings.ambientSkyColor;var equator=RenderSettings.ambientEquatorColor;var ground=RenderSettings.ambientGroundColor;var flat=RenderSettings.ambientLight;camera.orthographic=true;camera.orthographicSize=.205f;camera.aspect=4f/3;camera.allowHDR=true;camera.allowMSAA=true;camera.backgroundColor=new Color(.08f,.09f,.10f);camera.nearClipPlane=.02f;camera.farClipPlane=3;
        key.color=Color.white;key.shadows=LightShadows.Soft;key.GetUniversalAdditionalLightData().softShadowQuality=SoftShadowQuality.Medium;key.GetUniversalAdditionalLightData().usePipelineSettings=false;key.shadowNormalBias=.05f;key.shadowBias=.02f;RenderSettings.sun=key;
        var target=new RenderTexture(1280,960,24,RenderTextureFormat.ARGBHalf,RenderTextureReadWrite.Linear);target.antiAliasing=4;target.Create();var pixels=new Texture2D(1280,960,TextureFormat.RGBAFloat,false,true);
        string images=delivery+"/"+viewFolder;Directory.CreateDirectory(images);Directory.CreateDirectory(output+"/material-frames");var stats=new List<string>();
        void View(Vector3 p,Vector3 at){camera.transform.position=p;camera.transform.LookAt(at);}
        Color[] Capture(string name,bool save=true){RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});var previous=RenderTexture.active;RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,1280,960),0,0);pixels.Apply(false);RenderTexture.active=previous;var c=pixels.GetPixels();if(save){Save(c,images+"/"+name+".png",false);stats.Add(name+"\tmaximum="+c.Max(Max));}return c;}
        void Front(){key.intensity=.9f;fill.intensity=.22f;key.transform.rotation=Quaternion.LookRotation(-new Vector3(-.4f,.55f,.75f));RenderSettings.ambientMode=ambientMode;RenderSettings.ambientIntensity=1;RenderSettings.ambientSkyColor=sky;RenderSettings.ambientEquatorColor=equator;RenderSettings.ambientGroundColor=ground;RenderSettings.ambientLight=flat;RenderSettings.ambientProbe=ambient;RenderSettings.reflectionIntensity=1;}
        Front();View(new Vector3(0,.175f,.8f),new Vector3(0,.175f,0));for(int i=0;i<5;i++)yield return null;Capture("01_R02_Front_Default60");
        View(new Vector3(-.55f,.205f,.78f),new Vector3(0,.175f,0));Capture("02_R02_Front35_Default60");jade.SetFloat("_JadeTransparency",.5f);Capture("03_R02_Front35_Restrained50");jade.SetFloat("_JadeTransparency",.6f);
        View(new Vector3(-.8f,.175f,0),new Vector3(0,.175f,0));Capture("04_R02_RightSide_Default60");
        key.transform.rotation=Quaternion.LookRotation(-new Vector3(-.9f,.25f,.22f));View(new Vector3(-.55f,.205f,.78f),new Vector3(0,.175f,0));Capture("05_R02_SideLight35_Default60");
        camera.orthographic=false;camera.fieldOfView=32;View(new Vector3(-.12f,.245f,.29f),new Vector3(-.005f,.245f,0));Capture("06_R02_GoldWire_Contact_Close");View(new Vector3(-.14f,.36f,.26f),new Vector3(-.035f,.315f,0));Capture("07_R02_Crown_Transition_Close");
        camera.orthographic=true;camera.orthographicSize=.205f;Front();
        var old=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(QingGuiR02LabBuild.Root+"/ReferenceR01/Model/QingGui_LOD0.fbx"));var oldRotation=old.transform.rotation;
        foreach(var r in old.GetComponentsInChildren<MeshRenderer>())r.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(QingGuiR02LabBuild.Root+"/ReferenceR01/QingGui_R01.mat");
        old.transform.position=new Vector3(.12f,0,0);lab.MoveTo(new Vector3(-.12f,0,0),Quaternion.identity);View(new Vector3(0,.175f,.8f),new Vector3(0,.175f,0));Capture("08_R01_Left_R02_Right_SameLight_Front");
        old.transform.rotation=Quaternion.Euler(0,35,0)*oldRotation;lab.MoveTo(new Vector3(-.12f,0,0),Quaternion.Euler(0,35,0));Capture("09_R01_Left_R02_Right_SameLight35");Object.Destroy(old);lab.MoveTo(Vector3.zero,Quaternion.identity);yield return null;
        File.WriteAllText(output+"/final-first-images-ready.txt","Final frozen R02 mesh/maps; .60 main/.50 alternate; actual front Unity+Z; same-light R01 left/R02 right. Test completion remains result.txt.\n");
        View(new Vector3(-.55f,.205f,.78f),new Vector3(0,.175f,0));
        for(int i=0;i<90;i++){key.transform.rotation=Quaternion.LookRotation(-(Quaternion.Euler(0,Mathf.Lerp(-70,70,i/89f),0)*new Vector3(0,.4f,.92f)));yield return null;Save(Capture("video",false),output+"/material-frames/frame_"+i.ToString("D4")+".jpg",true);}
        target.Release();Object.Destroy(target);target=new RenderTexture(1280,960,24,RenderTextureFormat.ARGBHalf,RenderTextureReadWrite.Linear);target.antiAliasing=1;target.Create();camera.allowMSAA=false;
        Front();View(new Vector3(0,.175f,.8f),new Vector3(0,.175f,0));camera.backgroundColor=Color.black;
        layer.gameObject.SetActive(false);wire.shadowCastingMode=ShadowCastingMode.ShadowsOnly;metal.SetFloat("_JadeLayerMode",0);metal.SetFloat("_TransmissionDebug",4);var mask=Capture("10_JadeMask");metal.SetFloat("_TransmissionDebug",5);var bronzeMask=Capture("11_BronzeMask");
        var jadePixels=Enumerable.Range(0,mask.Length).Where(i=>mask[i].r>.995f).ToArray();var bronzePixels=Enumerable.Range(0,mask.Length).Where(i=>mask[i].r<.001f&&bronzeMask[i].r>.999f).ToArray();require(jadePixels.Length>1000&&bronzePixels.Length>100,"Actual jade/bronze masks contain measurable pixels");
        key.intensity=0;fill.intensity=0;RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientIntensity=0;RenderSettings.ambientLight=Color.black;RenderSettings.ambientProbe=new SphericalHarmonicsL2();RenderSettings.reflectionIntensity=0;metal.SetFloat("_TransmissionDebug",1);
        var white=new Material(Shader.Find("Universal Render Pipeline/Unlit"));white.SetColor("_BaseColor",Color.white);white.renderQueue=3002;wire.sharedMaterial=white;wire.shadowCastingMode=ShadowCastingMode.On;var frontWire=Capture("12_FrontmostWireMask");wire.sharedMaterial=gold;Object.Destroy(white);
        metal.SetFloat("_TransmissionDebug",0);metal.SetFloat("_JadeLayerMode",1);layer.gameObject.SetActive(true);for(int i=0;i<4;i++)yield return null;var off=Capture("13_AllLights_SH_ReflectionOff_BlackBackground");require(off.Max(Max)<.0002f,"Full layered item is zero with Flat black ambient/direct/reflection off; max="+off.Max(Max));
        Front();var bg=GameObject.CreatePrimitive(PrimitiveType.Cube);bg.transform.position=new Vector3(0,.175f,-.12f);bg.transform.localScale=new Vector3(.7f,.6f,.004f);var bgmat=new Material(Shader.Find("Universal Render Pipeline/Unlit"));bg.GetComponent<Renderer>().sharedMaterial=bgmat;bg.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
        bgmat.SetColor("_BaseColor",Color.black);var black=Capture("14_BlackBackgroundWitness");bgmat.SetColor("_BaseColor",Color.white);var light=Capture("15_WhiteBackgroundWitness");
        float alphaResponse=jadePixels.Max(i=>Max(light[i]-black[i]));float bronzeResponse=bronzePixels.Max(i=>Max(light[i]-black[i]));require(alphaResponse>.01f,"Final jade background transparency changes real pixels, max linear="+alphaResponse);require(bronzeResponse<.002f,"Bronze background-response remains zero, max="+bronzeResponse);
        var wirePixels=Enumerable.Range(0,frontWire.Length).Where(i=>frontWire[i].r>.999f).ToArray();require(wirePixels.Length>30,"Actual independent front wire mask exists");require(wirePixels.Max(i=>Max(light[i]-black[i]))<.003f,"Independent gold is fully opaque against changing background");
        var goldOn=Capture("16_GoldVisible");wire.shadowCastingMode=ShadowCastingMode.ShadowsOnly;var hidden=Capture("17_GoldColourHidden_ShadowRetained");
        bool NearWire(int index){int x=index%1280,y=index/1280;for(int yy=Math.Max(0,y-3);yy<=Math.Min(959,y+3);yy++)for(int xx=Math.Max(0,x-3);xx<=Math.Min(1279,x+3);xx++)if(frontWire[yy*1280+xx].r>.01f)return true;return false;}
        float ghost=jadePixels.Where(i=>!NearWire(i)).Max(i=>Max(goldOn[i]-hidden[i]));require(ghost<.003f,"Rear gold produces no colour ghost outside front-wire 3px margin, max="+ghost);wire.shadowCastingMode=ShadowCastingMode.On;
        for(int i=0;i<4;i++)yield return null;var cast=Capture("18_WireShadowOn");wire.shadowCastingMode=ShadowCastingMode.Off;for(int i=0;i<4;i++)yield return null;var noCast=Capture("19_WireShadowOff");float delta=jadePixels.Max(i=>Max(cast[i]-noCast[i]));stats.Add("OBSERVED micro-wire shadow delta="+delta+"; weak subpixel contact shadow is not a hard completion gate");wire.shadowCastingMode=ShadowCastingMode.On;
        stats.Add("backgroundMax="+alphaResponse+";bronzeMax="+bronzeResponse+";rearWireGhostMax="+ghost+";wireShadowDelta="+delta);stats.Add("BOUNDARY: depth-writing local transparency, opaque metal late queues; no scene refraction/copy; sorting against intersecting transparent objects and Quest stereo/performance untested. Desktop review shadows2048/2.5m. Background transparency is not emission.");File.WriteAllLines(output+"/final-layer-readback.tsv",stats);
        Object.Destroy(bg);Object.Destroy(bgmat);yield return null;target.Release();Object.Destroy(target);Object.Destroy(pixels);
    }
    static float Max(Color c)=>Mathf.Max(Mathf.Abs(c.r),Mathf.Max(Mathf.Abs(c.g),Mathf.Abs(c.b)));
    static void Save(Color[] colors,string path,bool jpg){var p=new Texture2D(1280,960,TextureFormat.RGB24,false);p.SetPixels(colors.Select(c=>c.gamma).ToArray());p.Apply();File.WriteAllBytes(path,jpg?p.EncodeToJPG(94):p.EncodeToPNG());Object.Destroy(p);}
}
