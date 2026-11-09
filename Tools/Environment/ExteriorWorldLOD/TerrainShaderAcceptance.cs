// Copy into the isolated project's Assets/Editor before invoking Run with -buildTarget Android.
// Builds a content bundle, never an APK. A successful build is compiler evidence, not headset evidence.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

public static class TerrainShaderAcceptance
{
    const string MainProject = "D:/UnityProjects/NarutoVR";
    const string ShaderPath = "Assets/Shaders/ExteriorTerrain.shader";
    const string ShaderName = "NarutoVR/Exterior Terrain";
    const string BundleName = "exterior_terrain_android";
    static readonly string[] Materials = {
        "Assets/Art/Environment/Opening/ExteriorLOD/RidgeStrata.mat",
        "Assets/Art/Environment/Opening/ExteriorLOD/DistantStrata.mat",
        "Assets/Art/Environment/Opening/ExteriorLOD/ValleyMossScree.mat"
    };
    [Serializable] public class FileEvidence { public string path, sha256; public long bytes; }
    [Serializable] public class Check { public string name, detail; public bool passed; }
    [Serializable] public class VariantEvidence
    {
        public string shader, pass, stage, compilerPlatform;
        public int variants, stereoInstancing, stereoMultiview, mainLightShadow, cluster, fog;
    }
    [Serializable] public class Result
    {
        public bool passed, automaticChecksOnly = true, bundleBuildReturnedManifest;
        public string time, unityVersion, project, requestedTarget = "Android", activeTarget;
        public string outputDirectory, bundleDirectory, exception, limitation;
        public string[] graphicsAPIs, explicitAssets, automaticDependencies, warnings, errors;
        public FileEvidence[] inputs, artifacts;
        public Check[] checks;
        public VariantEvidence[] processedShaderVariants;
    }
    static readonly List<Check> Checks = new List<Check>();
    static readonly List<string> Errors = new List<string>(), Warnings = new List<string>();
    internal static readonly List<VariantEvidence> Variants = new List<VariantEvidence>();
    internal static bool Capturing;

    public static void Run()
    {
        var report = new Result {
            time = DateTimeOffset.Now.ToString("o"), unityVersion = Application.unityVersion,
            project = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/'),
            activeTarget = EditorUserBuildSettings.activeBuildTarget.ToString(),
            limitation = "Android AssetBundle compiler validation only; shader preprocessing records are not GPU execution. " +
                "ForceRebuildAssetBundle may reuse Unity's shader compiler cache. No headset, driver, actual XR eyes, APK, visual or timing acceptance. " +
                "Only three materials and their shader are explicit bundle roots; Unity includes their serialized dependencies. " +
                "URP settings are inspected and hashed, not changed or bundled: this shader needs package include files, not a duplicated runtime pipeline asset."
        };
        string output = Environment.GetEnvironmentVariable("EXTERIOR_SHADER_OUT");
        if (string.IsNullOrWhiteSpace(output)) output = MainProject + "/Logs/exterior-world-lod-20261109/shader-android";
        report.outputDirectory = Path.GetFullPath(output);
        Checks.Clear(); Errors.Clear(); Warnings.Clear(); Variants.Clear();
        var before = new Dictionary<string, string>();
        bool subscribed = false;
        try
        {
            Require(Application.isBatchMode, "Batch invocation", "Use an isolated Unity batch process.");
            Require(!string.Equals(report.project.TrimEnd('/'), MainProject, StringComparison.OrdinalIgnoreCase),
                "Main project refused", report.project);
            Require(!Inside(report.outputDirectory, Application.dataPath), "Outputs outside Assets", report.outputDirectory);
            Directory.CreateDirectory(report.outputDirectory);
            Directory.SetCurrentDirectory(report.project);
            Application.logMessageReceived += Log; subscribed = true;
            before = ConfigHashes();
            Require(BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android),
                "Android module installed", "BuildPipeline.IsBuildTargetSupported(Android)");
            Require(EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android,
                "Actual active Android target", "Launch with -buildTarget Android; this tool does not switch targets or reload the domain.");
            report.graphicsAPIs = PlayerSettings.GetGraphicsAPIs(BuildTarget.Android).Select(a => a.ToString()).ToArray();
            Require(report.graphicsAPIs.Contains("Vulkan"), "Android Vulkan configured", string.Join(",", report.graphicsAPIs));
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            Require(shader && shader.name == ShaderName, "Terrain shader resolves", ShaderPath);
            string source = File.ReadAllText(ShaderPath);
            Require(Regex.Matches(source, @"\bSAMPLE_TEXTURE2D\s*\(").Count == 1,
                "One detail texture sample in authored source", "Shadow-map sampling inside URP is additional and intentional.");
            foreach (string macro in new[] { "UNITY_VERTEX_INPUT_INSTANCE_ID", "UNITY_SETUP_INSTANCE_ID(input)",
                "UNITY_VERTEX_OUTPUT_STEREO", "UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output)", "UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input)" })
                Require(source.Contains(macro), "Stereo source contract", macro);
            Require(source.Contains("#pragma multi_compile _ _CLUSTER_LIGHT_LOOP"), "URP Forward+ main-light variant", "_CLUSTER_LIGHT_LOOP");
            Require(Regex.IsMatch(source, @"GetMainLight\s*\(\s*shadowCoord\s*,\s*input\.positionWS\s*,\s*half4\s*\(\s*1\s*,\s*1\s*,\s*1\s*,\s*1\s*\)\s*\)"),
                "Main light distance-fade overload", "GetMainLight(shadowCoord, positionWS, white shadow mask)");
            Require(!Regex.IsMatch(source, @"ShadowCaster|DepthNormals|DepthOnly|_CameraOpaqueTexture|_CameraDepthTexture|GrabPass|\bBlend\s"),
                "Intended opaque pass scope", "No extra camera textures, transparency, caster/depth/normals pass.");
            foreach (string path in Materials)
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                Require(material && material.shader == shader, "Terrain material shader", path);
                Require(material.passCount == 1, "One material pass", path + ": " + material.passCount);
                Require(material.GetTexture("_BaseMap"), "Detail texture present", path);
                Require(material.GetFloat("_TextureMetres") > 0 && material.GetFloat("_MacroMetres") > 0,
                    "Valid world projection scale", path);
            }
            ShaderMessages(shader, "Before build");
            Require(Errors.Count == 0, "No pre-existing terrain shader errors", Errors.Count.ToString());
            report.explicitAssets = Materials.Concat(new[] { ShaderPath }).ToArray();
            report.automaticDependencies = AssetDatabase.GetDependencies(report.explicitAssets, true)
                .Except(report.explicitAssets).OrderBy(p => p, StringComparer.Ordinal).ToArray();
            var inputPaths = report.explicitAssets.Concat(report.automaticDependencies)
                .Concat(new[] { "Assets/Settings/Mobile_RPAsset.asset", "Assets/Settings/Mobile_Renderer.asset" })
                .Distinct().Where(File.Exists).OrderBy(p => p, StringComparer.Ordinal);
            report.inputs = inputPaths.Select(Evidence).ToArray();
            report.bundleDirectory = Path.Combine(report.outputDirectory, "bundle-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff"));
            Directory.CreateDirectory(report.bundleDirectory);
            Capturing = true;
            var manifest = BuildPipeline.BuildAssetBundles(report.bundleDirectory,
                new[] { new AssetBundleBuild { assetBundleName = BundleName, assetNames = report.explicitAssets } },
                BuildAssetBundleOptions.StrictMode | BuildAssetBundleOptions.ForceRebuildAssetBundle,
                BuildTarget.Android);
            Capturing = false;
            report.bundleBuildReturnedManifest = manifest != null;
            Require(manifest != null, "Android bundle manifest returned", report.bundleDirectory);
            Require(manifest.GetAllAssetBundles().SequenceEqual(new[] { BundleName }), "Only requested content bundle", string.Join(",", manifest.GetAllAssetBundles()));
            Require(File.Exists(Path.Combine(report.bundleDirectory, BundleName)) && new FileInfo(Path.Combine(report.bundleDirectory, BundleName)).Length > 0,
                "Android content bundle written", BundleName);
            ShaderMessages(shader, "After build");
            var vulkan = Variants.Where(v => v.compilerPlatform.IndexOf("Vulkan", StringComparison.OrdinalIgnoreCase) >= 0).ToArray();
            Require(vulkan.Sum(v => v.variants) > 0, "Terrain Android Vulkan compiler input observed", "Final IPreprocessShaders callback; successful bundle compilation is separately required.");
            Require(vulkan.Sum(v => v.stereoInstancing + v.stereoMultiview) > 0,
                "Vulkan XR stereo variants observed", "Absent/stripped stereo variants fail this check rather than claiming XR coverage.");
            Require(vulkan.Sum(v => v.mainLightShadow) > 0, "Vulkan main-light shadow variants observed", "Includes enabled main-light shadow keyword variants.");
            Require(Errors.Count == 0, "No logged build or terrain shader errors", Errors.Count.ToString());
        }
        catch (Exception ex) { report.exception = ex.ToString(); Errors.Add(ex.Message); }
        finally
        {
            Capturing = false;
            if (subscribed) Application.logMessageReceived -= Log;
            if (before.Count > 0)
            {
                var after = ConfigHashes();
                bool same = before.Count == after.Count && before.All(p => after.TryGetValue(p.Key, out string h) && h == p.Value);
                Checks.Add(new Check { name = "Project and URP configuration hashes unchanged", passed = same,
                    detail = string.Join(";", before.Where(p => !after.TryGetValue(p.Key, out string h) || h != p.Value).Select(p => p.Key)) });
                if (!same) Errors.Add("Project or URP configuration changed during validation; this tool does not restore or publish settings.");
            }
            report.processedShaderVariants = Variants.ToArray(); report.checks = Checks.ToArray();
            report.errors = Errors.ToArray(); report.warnings = Warnings.ToArray();
            report.passed = report.exception == null && Errors.Count == 0 && Checks.Count > 0 && Checks.All(c => c.passed);
            if (report.bundleDirectory != null && Directory.Exists(report.bundleDirectory))
                report.artifacts = Directory.GetFiles(report.bundleDirectory).OrderBy(p => p, StringComparer.Ordinal).Select(Evidence).ToArray();
            // A refused main-project invocation may write its failure report only under Logs, never Assets.
            if (!Inside(report.outputDirectory, Application.dataPath))
            {
                Directory.CreateDirectory(report.outputDirectory);
                string json = JsonUtility.ToJson(report, true);
                File.WriteAllText(Path.Combine(report.outputDirectory, "shader-android-result.json"), json);
                File.WriteAllText(Path.Combine(report.outputDirectory, "shader-android-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff") + ".json"), json);
                File.WriteAllLines(Path.Combine(report.outputDirectory, "shader-android-messages.txt"),
                    Checks.Select(c => (c.passed ? "PASS " : "FAIL ") + c.name + " | " + c.detail)
                        .Concat(Warnings.Select(m => "WARNING " + m)).Concat(Errors.Select(m => "ERROR " + m)));
            }
        }
        Debug.Log("TERRAIN_SHADER_ANDROID_RESULT=" + report.passed + " | " + report.outputDirectory);
        if (Application.isBatchMode) EditorApplication.Exit(report.passed ? 0 : 1);
    }

    static void Require(bool passed, string name, string detail)
    {
        Checks.Add(new Check { name = name, passed = passed, detail = detail });
        if (!passed) throw new InvalidOperationException(name + ": " + detail);
    }
    static bool Inside(string path, string parent)
    {
        string p = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string root = Path.GetFullPath(parent).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return p.StartsWith(root, StringComparison.OrdinalIgnoreCase);
    }
    static void Log(string text, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) Errors.Add(text + "\n" + stack);
        else if (type == LogType.Warning) Warnings.Add(text);
    }
    static void ShaderMessages(Shader shader, string phase)
    {
        foreach (var message in ShaderUtil.GetShaderMessages(shader))
        {
            string text = phase + " | " + message.platform + " | " + message.file + ":" + message.line + " | " + message.message;
            if (message.severity.ToString() == "Error") Errors.Add(text);
            else if (message.severity.ToString() == "Warning") Warnings.Add(text);
        }
    }
    static FileEvidence Evidence(string path)
    {
        using (var hash = SHA256.Create())
        using (var stream = File.OpenRead(path))
            return new FileEvidence { path = path, bytes = stream.Length, sha256 = BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "").ToLowerInvariant() };
    }
    static Dictionary<string, string> ConfigHashes()
    {
        return new[] { "ProjectSettings", "Assets/Settings", "Assets/XR/Settings", "Packages" }
            .Where(Directory.Exists).SelectMany(p => Directory.GetFiles(p, "*", SearchOption.AllDirectories))
            .Where(p => !p.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p, StringComparer.Ordinal).ToDictionary(p => p, p => Evidence(p).sha256);
    }
}

// Observe the variants remaining after normal SRP stripping, immediately before shader compilation.
// No keywords are inserted, removed, or changed by this callback.
public sealed class TerrainShaderCompilerEvidence : IPreprocessShaders
{
    public int callbackOrder => int.MaxValue;
    public void OnProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data)
    {
        if (!TerrainShaderAcceptance.Capturing || shader.name != "NarutoVR/Exterior Terrain") return;
        foreach (var group in data.GroupBy(d => d.shaderCompilerPlatform.ToString()))
        {
            var record = new TerrainShaderAcceptance.VariantEvidence {
                shader = shader.name, pass = snippet.passName, stage = snippet.shaderType.ToString(),
                compilerPlatform = group.Key, variants = group.Count()
            };
            foreach (var variant in group)
            {
                var names = variant.shaderKeywordSet.GetShaderKeywords().Select(k => k.name).ToArray();
                if (names.Contains("STEREO_INSTANCING_ON")) record.stereoInstancing++;
                if (names.Contains("STEREO_MULTIVIEW_ON")) record.stereoMultiview++;
                if (names.Any(k => k.StartsWith("_MAIN_LIGHT_SHADOWS", StringComparison.Ordinal))) record.mainLightShadow++;
                if (names.Contains("_CLUSTER_LIGHT_LOOP")) record.cluster++;
                if (names.Any(k => k.StartsWith("FOG_", StringComparison.Ordinal))) record.fog++;
            }
            TerrainShaderAcceptance.Variants.Add(record);
        }
    }
}
