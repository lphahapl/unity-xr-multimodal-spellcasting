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
public static class QingGuiR02BVulkanShaderCheck
{
    public const string Output="D:/UnityProjects/NarutoVR/Logs/workshop-master-tenon-qinggui-r02-20261126/unity/revision-b/android-vulkan";
    public static readonly List<string> CompiledInputs=new();
    public static void Run()
    {
        try {
            if(Application.dataPath.Replace('\\','/')!="D:/Temp/NarutoVR-QingGuiR01-20261125/Assets")throw new Exception("Isolated project only");
            Directory.CreateDirectory(Output);CompiledInputs.Clear();
            var importerRows=new List<string>();
            foreach(var name in new[]{"BaseColor","NormalGL","MetallicSmoothness","AO","Thickness","JadeMask"}) {
                string path="Assets/Review/QingGuiR02/RevisionB/Model/Textures/QingGui_"+name+".png";var imp=(TextureImporter)AssetImporter.GetAtPath(path);
                var setting=imp.GetPlatformTextureSettings("Android");setting.name="Android";setting.overridden=true;setting.maxTextureSize=2048;setting.format=(name=="Thickness"||name=="JadeMask")?TextureImporterFormat.R8:TextureImporterFormat.ASTC_6x6;setting.compressionQuality=100;imp.SetPlatformTextureSettings(setting);imp.SaveAndReimport();
                importerRows.Add(path+"\tAndroid="+setting.format+"\tsRGB="+imp.sRGBTexture+"\tDesktop remains uncompressed; override is not Quest runtime measurement");
            }
            File.WriteAllLines(Output+"/android-texture-import-settings.tsv",importerRows);
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android,false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android,new[]{GraphicsDeviceType.Vulkan});
            const string shaderPath="Assets/Review/QingGuiR02/Shaders/QingGuiJadeLit.shader";
            var shader=AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
            var collection=new ShaderVariantCollection();
            foreach(var current in new[]{shader,AssetDatabase.LoadAssetAtPath<Shader>("Assets/Review/QingGuiR02/Shaders/QingGuiJadeTransparent.shader")})
            foreach(var keywords in new[]{new[]{"_MAIN_LIGHT_SHADOWS","_CLEARCOAT"},new[]{"_MAIN_LIGHT_SHADOWS","_SHADOWS_SOFT","_METALLICSPECGLOSSMAP","_OCCLUSIONMAP","_CLEARCOAT"},new[]{"_MAIN_LIGHT_SHADOWS","_METALLICSPECGLOSSMAP","_OCCLUSIONMAP","_CLEARCOAT"},new[]{"_MAIN_LIGHT_SHADOWS_CASCADE","_METALLICSPECGLOSSMAP","_OCCLUSIONMAP","_CLEARCOAT","_REFLECTION_PROBE_ATLAS"}})
                collection.Add(new ShaderVariantCollection.ShaderVariant(current,PassType.ScriptableRenderPipeline,keywords));
            const string variantPath="Assets/Review/QingGuiR02/RevisionB/Generated/QingGuiMainLightVariants.shadervariants";
            var previous=AssetDatabase.LoadAssetAtPath<ShaderVariantCollection>(variantPath);
            if(previous)EditorUtility.CopySerialized(collection,previous);else AssetDatabase.CreateAsset(collection,variantPath);
            AssetDatabase.SaveAssets();
            var build=new AssetBundleBuild{assetBundleName="qinggui-material-vulkan",assetNames=new[]{"Assets/Review/QingGuiR02/RevisionB/Generated/QingGuiR02_OpaqueBronze.mat","Assets/Review/QingGuiR02/RevisionB/Generated/QingGuiR02_Jade60.mat","Assets/Review/QingGuiR02/RevisionB/Generated/QingGuiR02_Jade50.mat","Assets/Review/QingGuiR02/RevisionB/Generated/QingGuiR02_GoldWire.mat",variantPath}};
            var manifest=BuildPipeline.BuildAssetBundles(Output,new[]{build},BuildAssetBundleOptions.ForceRebuildAssetBundle|BuildAssetBundleOptions.ChunkBasedCompression,BuildTarget.Android);
            File.WriteAllLines(Output+"/compiler-inputs.tsv",CompiledInputs);
            var messages=new[]{shader,Shader.Find("NarutoVR/Review/QingGui R02 Local Jade Transparency"),Shader.Find("NarutoVR/Review/QingGui R02 Opaque Gold")}.SelectMany(s=>ShaderUtil.GetShaderMessages(s)).ToArray();File.WriteAllLines(Output+"/shader-messages.txt",messages.Select(m=>m.severity+" "+m.message));
            if(!manifest || !File.Exists(Output+"/qinggui-material-vulkan"))throw new Exception("Android asset-bundle build failed");
            if(messages.Any(m=>m.severity.ToString()=="Error"))throw new Exception("Shader compiler reported errors");
            if(!new[]{"Polished Jade","Local Jade Transparency","Opaque Gold"}.All(n=>CompiledInputs.Any(x=>x.Contains(n)&&x.Contains("Vulkan"))))throw new Exception("No actual Vulkan shader compiler input was observed");
            File.WriteAllText(Output+"/result.txt","PASS Android/Vulkan material shader asset bundle.\nObserved IPreprocessShaders inputs and successful BuildPipeline result; see compiler-inputs.tsv.\nNot an installed APK, headset rendering, XR stereo validation, or Quest GPU benchmark.\n");
            QingGuiR02BLabBuild.Export();EditorApplication.Exit(0);
        }catch(Exception e){Directory.CreateDirectory(Output);File.WriteAllText(Output+"/failure.txt",e.ToString());Debug.LogException(e);EditorApplication.Exit(3);}
    }
}
public sealed class QingGuiR02BShaderCompileObserver:IPreprocessShaders
{
    public int callbackOrder=>0;
    public void OnProcessShader(Shader shader,ShaderSnippetData snippet,IList<ShaderCompilerData> data)
    {
        if(shader.name!="NarutoVR/Review/QingGui R02 Polished Jade"&&shader.name!="NarutoVR/Review/QingGui R02 Local Jade Transparency"&&shader.name!="NarutoVR/Review/QingGui R02 Opaque Gold")return;
        foreach(var group in data.GroupBy(x=>x.shaderCompilerPlatform.ToString()))
            QingGuiR02BVulkanShaderCheck.CompiledInputs.Add(shader.name+"\t"+snippet.passName+"\t"+snippet.shaderType+"\t"+group.Key+"\t"+group.Count());
    }
}
