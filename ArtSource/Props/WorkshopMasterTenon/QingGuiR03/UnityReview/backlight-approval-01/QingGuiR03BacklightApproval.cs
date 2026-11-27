using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using NarutoVR.QingGuiR03Review;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class QingGuiR03BacklightApproval
{
 const string Root="Assets/Review/QingGuiR03";
 const string Key="QingGuiR03BacklightApproval.Run";
 const string Out="D:/UnityProjects/NarutoVR/Logs/workshop-master-tenon-qinggui-r03-20261126/unity/backlight-approval-01";
 const string Delivery="D:/UnityProjects/NarutoVR/ArtSource/Props/WorkshopMasterTenon/QingGuiR03/UnityReview/backlight-approval-01";
 static Camera cam;static Light sun;static GameObject item;static ReflectionProbe probe;static Material jade;
 static RenderTexture rt;static Texture2D pixels;static IEnumerator routine;static int ticks;
 static List<string> timeline=new List<string>();
 static Quaternion normal,side,back;static Vector3 cameraPosition,cameraEuler;static Color[] backOn;
 static QingGuiR03BacklightApproval()=>EditorApplication.update+=Boot;
 static string Hash(string path){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant();}
 static void Require(bool value,string message){if(!value)throw new Exception(message);File.AppendAllText(Out+"/progress.txt","PASS "+message+"\n");}
 public static void Run()
 {
  try {
   Directory.CreateDirectory(Out+"/frames");Directory.CreateDirectory(Delivery);
   Require(Application.dataPath.Replace('\\','/')=="D:/Temp/NarutoVR-QingGuiR01-20261125/Assets","Owned isolated project");
   var fbx=Root+"/Model/RevisionA/QingGuiR03_StructureCandidate.fbx";
   Require(Hash(fbx)=="3052e95f06ac67cfd37a6106e06f7a472b74cbae882f0ff0247268234f2170d3","Frozen Revision A FBX exact SHA");
   Require(Hash(Root+"/Model/RevisionA/CandidateTextures/QingGuiR03_JadeCore_Thickness.png")=="a0642df21fc9aec326f7d386bab04d91f49fa50203f45f189aaa286f481ce582","Frozen real Thickness exact SHA");
   EditorSceneManager.OpenScene(Root+"/QingGuiR03_FirstLook.unity");
   SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
  }catch(Exception e){Fail(e);}
 }
 static void Boot()
 {
  if(!Application.isPlaying||EditorApplication.isCompiling||!SessionState.GetBool(Key,false))return;
  SessionState.SetBool(Key,false);
  try {
   cam=Camera.main;sun=RenderSettings.sun;item=GameObject.Find("R03 Actual three-part object");probe=Object.FindFirstObjectByType<ReflectionProbe>();
   Require(cam&&sun&&item&&probe,"Existing approved scene camera/light/model/probe");
   var parts=item.GetComponentsInChildren<MeshRenderer>();Require(parts.Length==3,"Three frozen opaque parts");
   foreach(var part in parts){Require(AssetDatabase.GetAssetPath(part.GetComponent<MeshFilter>().sharedMesh).Contains("/Model/RevisionA/"),"Actual scene meshes are Revision A: "+part.name);Require(part.sharedMaterial.renderQueue==2000,"Opaque queue unchanged: "+part.name);}
   var core=parts.Single(p=>p.name.Contains("JadeCore"));jade=core.sharedMaterial;
   Require(Mathf.Abs(jade.GetFloat("_TransmissionStrength")-.7f)<.000001f&&Mathf.Abs(jade.GetFloat("_ScatterStrength")-.24f)<.000001f,"Approved transmission/scatter unchanged .7/.24");
   Require(jade.GetFloat("_TransmissionDebug")==0,"Normal material rendering, no debug contribution display");
   Require(Mathf.Abs(sun.intensity-1.65f)<.000001f,"Ordinary chapter main intensity 1.65 unchanged");
   var rp=GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;Require(rp&&AssetDatabase.GetAssetPath(rp)==Root+"/ChapterReference/Mobile_RPAsset.asset","Original ordinary chapter Mobile RP snapshot");
   item.transform.SetPositionAndRotation(Vector3.zero,Quaternion.Euler(0,180,0));
   cam.transform.position=item.transform.TransformPoint(new Vector3(-.16f,.230f,.38f));cam.transform.LookAt(item.transform.TransformPoint(new Vector3(0,.205f,0)));
   cam.fieldOfView=60;cam.aspect=4f/3;cam.GetUniversalAdditionalCameraData().renderPostProcessing=false;cam.enabled=true;
   cameraPosition=cam.transform.position;cameraEuler=cam.transform.eulerAngles;
   normal=sun.transform.rotation;side=item.transform.rotation*Quaternion.LookRotation(new Vector3(-.80f,-.25f,.25f));back=item.transform.rotation*Quaternion.LookRotation(new Vector3(-.20f,-.25f,1));
   rt=new RenderTexture(1280,960,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear){antiAliasing=4};rt.Create();pixels=new Texture2D(1280,960,TextureFormat.RGBA32,false,true);
   QualitySettings.realtimeReflectionProbes=true;Time.captureFramerate=30;Application.runInBackground=true;
   File.WriteAllText(Out+"/capture-settings.txt",$"Fixed camera world={cameraPosition:F6}, euler={cameraEuler:F6}, FOV60; item position0 yaw180, unchanged through195frames.\nLight originalEuler={normal.eulerAngles:F6}; sideEuler={side.eulerAngles:F6}; backEuler={back.eulerAngles:F6}.\nAll states intensity={sun.intensity}, color={sun.color}, shadowStrength={sun.shadowStrength}; source MobileRP. No exposure/tone grading/extra lights.\n195frames/30fps/6.5seconds; original0-1.2s, move1.2-2.7s, sidehold2.7-3.6s, move3.6-4.8s, backhold4.8-6.5s.\nOnly screenshot03 temporarily disables TransmissionStrength on cloned jade; no asset edits.\n");
   new GameObject("R03 backlight capture tick").AddComponent<QingGuiR03ReviewTick>();QingGuiR03ReviewTick.Tick=Step;routine=Capture();
  }catch(Exception e){Fail(e);}
 }
 static IEnumerator Capture()
 {
  int id=probe.RenderProbe();for(int i=0;i<150&&!probe.IsFinishedRendering(id);i++)yield return null;
  Require(probe.IsFinishedRendering(id),"Actual ordinary sky-only reflection captured");cam.enabled=false;
  timeline.Add("frame\ttime_seconds\tphase\tlight_euler\tintensity\tmodel_euler\tcamera_position");
  for(int i=0;i<195;i++){
   float t=i/30f;string phase;
   if(t<1.2f){sun.transform.rotation=normal;phase="normal_hold";}
   else if(t<2.7f){sun.transform.rotation=Quaternion.Slerp(normal,side,Mathf.SmoothStep(0,1,(t-1.2f)/1.5f));phase="normal_to_side";}
   else if(t<3.6f){sun.transform.rotation=side;phase="side_back_hold";}
   else if(t<4.8f){sun.transform.rotation=Quaternion.Slerp(side,back,Mathf.SmoothStep(0,1,(t-3.6f)/1.2f));phase="side_to_back";}
   else {sun.transform.rotation=back;phase="back_hold";}
   yield return null;
   RequireFixed();Render(Out+"/frames/frame_"+i.ToString("D4")+".jpg",true);
   if(i==0)Render(Delivery+"/01_NormalLight_FixedCamera.png",false);
   if(i==90)Render(Delivery+"/02_SideBackLight_FixedCamera.png",false);
   if(i==160){Render(Delivery+"/03_BackLight_FixedCamera.png",false);backOn=pixels.GetPixels();}
   timeline.Add($"{i}\t{t:F6}\t{phase}\t{sun.transform.eulerAngles:F6}\t{sun.intensity:F6}\t{item.transform.eulerAngles:F6}\t{cam.transform.position:F6}");
  }
  File.WriteAllLines(Out+"/timeline.tsv",timeline);
  var core=item.GetComponentsInChildren<Renderer>().Single(r=>r.name.Contains("JadeCore"));var original=core.sharedMaterial;var off=new Material(original);off.SetFloat("_TransmissionStrength",0);core.sharedMaterial=off;
  yield return null;Render(Delivery+"/04_SameBackLight_TransmissionOnlyOff.png",false);
  var disabled=pixels.GetPixels();float maxDiff=0;double mean=0;int changed=0;
  for(int p=0;p<disabled.Length;p++){float d=Mathf.Max(Mathf.Abs(backOn[p].r-disabled[p].r),Mathf.Abs(backOn[p].g-disabled[p].g),Mathf.Abs(backOn[p].b-disabled[p].b));maxDiff=Mathf.Max(maxDiff,d);mean+=d;if(d>.004f)changed++;}
  core.sharedMaterial=original;Object.Destroy(off);
  Require(maxDiff>.005f&&changed>100,"Actual backlight contribution visible without changing approved material: max="+maxDiff+" changedPixels="+changed);
  File.WriteAllText(Out+"/transmission-control.json",$"{{\"maxLinearRGBDifference\":{maxDiff},\"meanFullFrameDifference\":{mean/disabled.Length},\"changedPixelsAbove004\":{changed},\"scope\":\"Same camera/light/background; only cloned jade TransmissionStrength .7 to0; shallow scatter/PBR/metals unchanged. No asset edit.\"}}");
  File.WriteAllText(Out+"/result.txt","PASS Frozen RevisionA actual scene, fixed camera/model, ordinary source intensity and approved materials,195 actual Unity frames, normal/side/back holds, same-backlight transmission-only-off control.\n");
  QingGuiR03ReviewTick.Tick=null;EditorApplication.Exit(0);
 }
 static void RequireFixed(){if(Vector3.Distance(cam.transform.position,cameraPosition)>.000001f||Quaternion.Angle(item.transform.rotation,Quaternion.Euler(0,180,0))>.0001f||item.transform.position.sqrMagnitude>.0000001f||Mathf.Abs(sun.intensity-1.65f)>.000001f)throw new Exception("Fixed capture invariant changed");}
 static void Render(string path,bool jpg){RenderPipeline.SubmitRenderRequest(cam,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});var old=RenderTexture.active;RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,1280,960),0,0);pixels.Apply();RenderTexture.active=old;var gamma=new Texture2D(1280,960,TextureFormat.RGB24,false);gamma.SetPixels(pixels.GetPixels().Select(c=>c.gamma).ToArray());gamma.Apply();File.WriteAllBytes(path,jpg?gamma.EncodeToJPG(94):gamma.EncodeToPNG());Object.Destroy(gamma);}
 static void Step(){try{if(++ticks>400)throw new Exception("Bounded capture timeout");routine.MoveNext();}catch(Exception e){Fail(e);}}
 static void Fail(Exception e){QingGuiR03ReviewTick.Tick=null;Directory.CreateDirectory(Out);File.WriteAllText(Out+"/failure.txt",e.ToString());Debug.LogException(e);EditorApplication.Exit(3);}
}
