using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

// Copied into the isolated project's Editor folder only after the graphics probe exits.
public static class QingGuiVulkanShaderCheck
{
    public const string Output="D:/UnityProjects/NarutoVR/Logs/workshop-master-tenon-qinggui-r01-20261125/unity/android-vulkan-bounded";
    public static readonly List<string> CompiledInputs=new();
    public static void Run()
    {
        try {
            if(Application.dataPath.Replace('\\','/')!="D:/Temp/NarutoVR-QingGuiR01-20261125/Assets")throw new Exception("Isolated project only");
            Directory.CreateDirectory(Output);CompiledInputs.Clear();
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android,false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android,new[]{GraphicsDeviceType.Vulkan});
            const string shaderPath="Assets/Review/QingGuiR01/Shaders/QingGuiJadeLit.shader";
            var shader=AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
            var collection=new ShaderVariantCollection();
            foreach(var keywords in new[]{new[]{"_MAIN_LIGHT_SHADOWS"},new[]{"_MAIN_LIGHT_SHADOWS","_METALLICSPECGLOSSMAP","_OCCLUSIONMAP"},new[]{"_MAIN_LIGHT_SHADOWS_CASCADE","_METALLICSPECGLOSSMAP","_OCCLUSIONMAP"}})
                collection.Add(new ShaderVariantCollection.ShaderVariant(shader,PassType.ScriptableRenderPipeline,keywords));
            const string variantPath="Assets/Review/QingGuiR01/Generated/QingGuiMainLightVariants.shadervariants";
            var previous=AssetDatabase.LoadAssetAtPath<ShaderVariantCollection>(variantPath);
            if(previous)EditorUtility.CopySerialized(collection,previous);else AssetDatabase.CreateAsset(collection,variantPath);
            AssetDatabase.SaveAssets();
            var build=new AssetBundleBuild{assetBundleName="qinggui-material-vulkan",assetNames=Enumerable.Range(0,4).Select(i=>"Assets/Review/QingGuiR01/Generated/Slab_"+i+".mat").Concat(new[]{variantPath}).ToArray()};
            var manifest=BuildPipeline.BuildAssetBundles(Output,new[]{build},BuildAssetBundleOptions.ForceRebuildAssetBundle|BuildAssetBundleOptions.ChunkBasedCompression,BuildTarget.Android);
            File.WriteAllLines(Output+"/compiler-inputs.tsv",CompiledInputs);
            var messages=ShaderUtil.GetShaderMessages(shader);File.WriteAllLines(Output+"/shader-messages.txt",messages.Select(m=>m.severity+" "+m.message));
            if(!manifest || !File.Exists(Output+"/qinggui-material-vulkan"))throw new Exception("Android asset-bundle build failed");
            if(messages.Any(m=>m.severity.ToString()=="Error"))throw new Exception("Shader compiler reported errors");
            if(!CompiledInputs.Any(x=>x.Contains("Vulkan")))throw new Exception("No actual Vulkan shader compiler input was observed");
            File.WriteAllText(Output+"/result.txt","PASS Android/Vulkan material shader asset bundle.\nObserved IPreprocessShaders inputs and successful BuildPipeline result; see compiler-inputs.tsv.\nNot an installed APK, headset rendering, XR stereo validation, or Quest GPU benchmark.\n");
            EditorApplication.Exit(0);
        }catch(Exception e){Directory.CreateDirectory(Output);File.WriteAllText(Output+"/failure.txt",e.ToString());Debug.LogException(e);EditorApplication.Exit(3);}
    }
}
public sealed class QingGuiShaderCompileObserver:IPreprocessShaders
{
    public int callbackOrder=>0;
    public void OnProcessShader(Shader shader,ShaderSnippetData snippet,IList<ShaderCompilerData> data)
    {
        if(shader.name!="NarutoVR/Review/QingGui R01 Opaque Jade")return;
        foreach(var group in data.GroupBy(x=>x.shaderCompilerPlatform.ToString()))
            QingGuiVulkanShaderCheck.CompiledInputs.Add(snippet.passName+"\t"+snippet.shaderType+"\t"+group.Key+"\t"+group.Count());
    }
}
