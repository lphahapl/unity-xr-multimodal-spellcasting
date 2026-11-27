using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using NarutoVR.QingGuiR02Review;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class QingGuiR02FinalLookOnly
{
    const string Key="QingGuiR02FinalLookOnly.Run";
    const string Output=QingGuiR02LabBuild.Output+"/final-shadow-ready";
    static IEnumerator routine;static int frame;static readonly List<string> report=new();
    static QingGuiR02FinalLookOnly()=>EditorApplication.update+=Boot;
    public static void Run()
    {
        try {
            if(Application.dataPath.Replace('\\','/')!="D:/Temp/NarutoVR-QingGuiR01-20261125/Assets")throw new Exception("Exclusive project required");
            Directory.CreateDirectory(Output);QingGuiR02LabBuild.ReviewPipeline();AssetDatabase.SaveAssets();EditorSceneManager.OpenScene(QingGuiR02LabBuild.ScenePath);
            var gold=AssetDatabase.LoadAssetAtPath<Material>(QingGuiR02LabBuild.Root+"/Generated/QingGuiR02_GoldWire.mat");gold.shader=Shader.Find("NarutoVR/Review/QingGui R02 Opaque Gold");gold.SetFloat("_ClearCoatMask",0);gold.SetFloat("_Smoothness",.68f);gold.DisableKeyword("_CLEARCOAT");gold.renderQueue=3002;EditorUtility.SetDirty(gold);
            foreach(string name in new[]{"QingGuiR02_OpaqueBronze","QingGuiR02_Jade60","QingGuiR02_Jade50","QingGui_R01","QingGui_R02"}){var mat=AssetDatabase.LoadAssetAtPath<Material>(QingGuiR02LabBuild.Root+"/Generated/"+name+".mat");if(mat){mat.SetFloat("_DepthColorStrength",.08f);EditorUtility.SetDirty(mat);}}
            if(AssetDatabase.LoadAssetAtPath<Material>(QingGuiR02LabBuild.Root+"/Generated/QingGui_R01.mat")&&!AssetDatabase.LoadAssetAtPath<Material>(QingGuiR02LabBuild.Root+"/Generated/QingGui_R02.mat"))AssetDatabase.MoveAsset(QingGuiR02LabBuild.Root+"/Generated/QingGui_R01.mat",QingGuiR02LabBuild.Root+"/Generated/QingGui_R02.mat");AssetDatabase.SaveAssets();
            File.WriteAllText(Output+"/gold-queue-before-play.txt","shader="+gold.shader.name+" queue="+gold.renderQueue+" surface="+gold.GetFloat("_Surface"));
            SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
        }catch(Exception e){Fail(e);}
    }
    static void Boot()
    {
        if(!Application.isPlaying&&!EditorApplication.isCompiling&&SessionState.GetBool("R02.FinalToControls",false)){SessionState.SetBool("R02.FinalToControls",false);QingGuiR02MaterialAcceptance.Run();return;}
        if(!Application.isPlaying||EditorApplication.isCompiling||!SessionState.GetBool(Key,false))return;SessionState.SetBool(Key,false);
        try {var lab=Object.FindFirstObjectByType<QingGuiR02LabController>();lab.keyboardEnabled=false;lab.showPanel=false;var camera=Camera.main;camera.enabled=false;new GameObject("R02 final optics tick").AddComponent<QingGuiR02MaterialTick>();QingGuiR02MaterialTick.Tick=Step;Time.captureFramerate=30;Application.runInBackground=true;routine=Checks(lab,camera);}catch(Exception e){Fail(e);}
    }
    static IEnumerator Checks(QingGuiR02LabController lab,Camera camera)
    {
        for(int i=0;i<10;i++)yield return null;
        // FinalLookReady photos already captured; this entry isolates the shadow configuration repair.
        var optics=QingGuiR02ModelMaterialCheck.Run(lab,camera,Output,QingGuiR02LabBuild.Delivery,Require);while(optics.MoveNext())yield return optics.Current;
    }
    static void Require(bool pass,string message){if(!pass)throw new Exception(message);report.Add("PASS "+message);File.WriteAllLines(Output+"/progress.txt",report);}
    static void Step(){try{if(++frame>700)throw new Exception("Final optics frame bound");if(routine.MoveNext())return;QingGuiR02MaterialTick.Tick=null;File.WriteAllLines(Output+"/result.txt",report);QingGuiR02LabBuild.Export();SessionState.SetBool("R02.FinalToControls",true);EditorApplication.ExitPlaymode();}catch(Exception e){Fail(e);}}
    static void Fail(Exception e){QingGuiR02MaterialTick.Tick=null;Directory.CreateDirectory(Output);File.WriteAllText(Output+"/failure.txt",e+"\n"+string.Join("\n",report));Debug.LogException(e);EditorApplication.Exit(3);}
}
