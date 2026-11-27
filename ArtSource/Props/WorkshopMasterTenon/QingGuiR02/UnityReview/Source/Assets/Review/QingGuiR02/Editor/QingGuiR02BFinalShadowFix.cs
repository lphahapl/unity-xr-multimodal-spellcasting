using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using NarutoVR.QingGuiR02Review;
using Object=UnityEngine.Object;
[InitializeOnLoad]
public static class QingGuiR02BFinalShadowFix
{
    const string Key="QingGuiR02BFinalShadowFix.Run";
    const string Output=QingGuiR02LabBuild.Output+"/revision-b-shadow-final-r2";
    static IEnumerator routine;static int frame;static readonly List<string> report=new();
    static QingGuiR02BFinalShadowFix()=>EditorApplication.update+=Boot;
    public static void Run(){try{Directory.CreateDirectory(Output+"/rear/material-frames");EditorSceneManager.OpenScene(QingGuiR02BLabBuild.ScenePath);var light=GameObject.Find("Review key light").GetComponent<Light>();light.shadowBias=.5f;light.shadowNormalBias=.05f;light.shadows=LightShadows.Soft;light.GetUniversalAdditionalLightData().usePipelineSettings=false;light.GetUniversalAdditionalLightData().softShadowQuality=SoftShadowQuality.Medium;EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();}catch(Exception e){Fail(e);}}
    static void Boot(){if(!Application.isPlaying||EditorApplication.isCompiling||!SessionState.GetBool(Key,false))return;SessionState.SetBool(Key,false);var lab=Object.FindFirstObjectByType<QingGuiR02LabController>();lab.keyboardEnabled=false;lab.showPanel=false;var camera=Camera.main;camera.enabled=false;new GameObject("final shadow-only optics tick").AddComponent<QingGuiR02MaterialTick>();QingGuiR02MaterialTick.Tick=Step;Time.captureFramerate=30;routine=Checks(lab,camera);}
    static IEnumerator Checks(QingGuiR02LabController lab,Camera camera){for(int i=0;i<10;i++)yield return null;var photo=QingGuiR02BFinalLook.Run(lab,camera,Output,QingGuiR02BLabBuild.Delivery,Require);while(photo.MoveNext())yield return photo.Current;QingGuiR02BRearPhotography.Output=Output+"/rear";var rear=QingGuiR02BRearPhotography.Capture(camera);while(rear.MoveNext())yield return rear.Current;var optics=QingGuiR02ModelMaterialCheck.Run(lab,camera,Output,QingGuiR02BLabBuild.Delivery,Require);while(optics.MoveNext())yield return optics.Current;}
    static void Require(bool pass,string message){if(!pass)throw new Exception(message);report.Add("PASS "+message);File.WriteAllLines(Output+"/progress.txt",report);}
    static void Step(){try{if(++frame>700)throw new Exception("Final optical frame bound");if(routine.MoveNext())return;QingGuiR02MaterialTick.Tick=null;File.WriteAllLines(Output+"/result.txt",report);QingGuiR02BLabBuild.Export();EditorApplication.Exit(0);}catch(Exception e){Fail(e);}}
    static void Fail(Exception e){QingGuiR02MaterialTick.Tick=null;Directory.CreateDirectory(Output);File.WriteAllText(Output+"/failure.txt",e+"\n"+string.Join("\n",report));Debug.LogException(e);EditorApplication.Exit(3);}
}
