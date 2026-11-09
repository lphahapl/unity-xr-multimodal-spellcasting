// 本文件是隔离工程验证入口。复制到隔离工程 Assets/Editor 后运行，正式 Assets 不加载此脚本。
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NarutoVR.SealCombat.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class ThinCloudSkyChecks
{
    static readonly List<string> Report = new();
    const string Out = "Logs/skybox-check";
    static Camera camera;
    static Material material;
    static RenderTexture target;
    static Scene scene;
    static bool oldAsync;

    /// <summary>只构建包含天空材质的 Android 资源包，验证移动 GPU Shader 编译；不安装到设备。</summary>
    public static void BuildAndroid()
    {
        if (!Application.dataPath.Replace('\\', '/').StartsWith("D:/Temp/NarutoVR-Skybox-", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("仅允许专属隔离工程。");
        Directory.CreateDirectory("Logs/skybox-android");
        try
        {
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan, GraphicsDeviceType.OpenGLES3 });
            Directory.CreateDirectory("Assets/SkyVariantChecks");
            AssetDatabase.Refresh();
            var source = AssetDatabase.LoadAssetAtPath<Material>(ThinCloudSkyPreview.MaterialPath);
            var paths = new List<string>();
            for (int i = 0; i < 3; i++)
            {
                string path = $"Assets/SkyVariantChecks/Mode{i}.mat";
                var variant = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!variant) { variant = new Material(source); AssetDatabase.CreateAsset(variant, path); }
                ThinCloudSkyPreview.SetMode(variant, i == 0 ? 0 : 2, i == 2);
                EditorUtility.SetDirty(variant);
                paths.Add(path);
            }
            AssetDatabase.SaveAssets();
            var bundle = new AssetBundleBuild { assetBundleName = "thin-cloud-sky", assetNames = paths.ToArray() };
            var manifest = BuildPipeline.BuildAssetBundles("Logs/skybox-android", new[] { bundle },
                BuildAssetBundleOptions.ForceRebuildAssetBundle, BuildTarget.Android);
            if (!manifest) throw new Exception("Android 天空资源包构建失败。");
            var messages = ShaderUtil.GetShaderMessages(source.shader);
            if (messages.Any(m => m.severity.ToString() == "Error")) throw new Exception("移动 Shader 编译存在错误。");
            File.WriteAllText("Logs/skybox-android/report.txt", "PASS Android asset bundle; Vulkan + OpenGLES3; gradient/single/double cloud materials.\nNot an APK, XR session or device performance measurement.\n");
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            File.WriteAllText("Logs/skybox-android/report.txt", e.ToString());
            Debug.LogException(e); EditorApplication.Exit(1);
        }
    }

    public static void Run()
    {
        if (!Application.dataPath.Replace('\\', '/').StartsWith("D:/Temp/NarutoVR-Skybox-", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("此检查仅允许在专属隔离工程运行。");
        Directory.CreateDirectory(Out);
        try
        {
            ThinCloudSkyAssets.CreateMissing();
            var rp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/Mobile_RPAsset.asset");
            GraphicsSettings.defaultRenderPipeline = rp;
            QualitySettings.renderPipeline = rp;
            oldAsync = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;
            // 此入口已经锁定独立测试工程；可以安全替换它的空启动场景。
            scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("天空预览相机（桌面美术预览，不含 XR Rig）");
            camera = root.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.fieldOfView = 80; camera.nearClipPlane = 0.1f; camera.farClipPlane = 100;
            camera.allowHDR = false;
            camera.transform.rotation = Quaternion.Euler(-22, 0, 0);
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            root.AddComponent<Skybox>().material = AssetDatabase.LoadAssetAtPath<Material>(ThinCloudSkyPreview.MaterialPath);
            root.AddComponent<AudioListener>();
            Directory.CreateDirectory("Assets/Scene");
            if (!EditorSceneManager.SaveScene(scene, ThinCloudSkyAssets.ScenePath)) throw new IOException("保存独立天空预览场景失败。");
            camera.enabled = false;
            material = new Material(camera.GetComponent<Skybox>().material);
            camera.GetComponent<Skybox>().material = material;
            target = new RenderTexture(960, 600, 24, RenderTextureFormat.ARGB32);
            target.Create();
            camera.aspect = 1.6f;
            // 首次相机渲染负责初始化 SRP，随后所有样本使用同一单相机入口。
            camera.targetTexture = target;
            camera.Render();
            camera.targetTexture = null;
            EditorApplication.delayCall += Check;
        }
        catch (Exception e) { Fail(e); }
    }

    static void Check()
    {
        try
        {
            Report.Add($"Unity={Application.unityVersion}; GPU={SystemInfo.graphicsDeviceName}; API={SystemInfo.graphicsDeviceType}");
            var beforeSky = RenderSettings.skybox;
            var source = AssetDatabase.LoadAssetAtPath<Material>(ThinCloudSkyPreview.MaterialPath);
            string beforeMaterial = File.ReadAllText(ThinCloudSkyPreview.MaterialPath);
            var texture = (Texture2D)source.GetTexture("_CloudMap");
            Require(texture.width == 1024 && texture.height == 1024, "实际导入 1024x1024");
            Require(!texture.isReadable && texture.mipmapCount > 1 && texture.wrapMode == TextureWrapMode.Mirror,
                "纹理不可读、有 mip、镜像平铺");
            Report.Add($"Texture={texture.format}; runtimeMemory={UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(texture)} bytes");

            var flat = Capture("01-gradient", 0, 0);
            var still = Capture("02-static", 1, 0);
            var moving0 = Capture("03-moving-t0", 2, 0);
            var moving30 = Capture("04-moving-t30", 2, 30);
            Require(Difference(flat, still) > 0.3, "云层确实可见");
            Require(Difference(still, moving0) < 0.05, "静态与动态零时刻一致");
            Require(Difference(moving0, moving30) > 0.2, "云在 30 秒后移动");
            var frozen30 = Capture("05-static-t30", 1, 30);
            Require(Difference(still, frozen30) < 0.05, "静态模式不受时间影响");
            camera.transform.position = new Vector3(10, 2, -8);
            var translated = Capture("06-translated", 2, 0);
            Require(Difference(moving0, translated) < 0.05, "天空不随头部平移产生近景视差");
            camera.transform.position = Vector3.zero;
            material.DisableKeyword("_SECOND_LAYER");
            var single = Capture("07-single-layer", 2, 0, false);
            Require(Difference(single, moving0) > 0.1, "单层与双层降级开关有效");
            // 覆盖真正编辑器预览窗口所用的 PreviewScene，而不只测普通场景相机。
            Camera originalCamera = camera;
            Scene isolated = EditorSceneManager.NewPreviewScene();
            try
            {
                var previewRoot = Object.Instantiate(originalCamera.gameObject);
                SceneManager.MoveGameObjectToScene(previewRoot, isolated);
                camera = previewRoot.GetComponent<Camera>();
                camera.scene = isolated; camera.cullingMask = 0;
                var isolatedPixels = Capture("08-isolated-preview", 2, 0);
                Require(Difference(isolatedPixels, moving0) < 0.05, "独立预览场景与普通相机画面一致");
            }
            finally { camera = originalCamera; EditorSceneManager.ClosePreviewScene(isolated); }
            foreach (float yaw in new[] { 90f, 180f, 270f })
            {
                camera.transform.rotation = Quaternion.Euler(-22, yaw, 0);
                Capture("direction-" + yaw, 2, 0);
            }
            camera.transform.rotation = Quaternion.Euler(-89, 0, 0);
            Capture("zenith", 2, 0);
            camera.transform.rotation = Quaternion.Euler(89, 0, 0);
            Capture("nadir", 2, 0);
            Require(RenderSettings.skybox == beforeSky && File.ReadAllText(ThinCloudSkyPreview.MaterialPath) == beforeMaterial,
                "预览未修改全局天空或源材质");
            var messages = ShaderUtil.GetShaderMessages(source.shader);
            foreach (var m in messages) Report.Add($"SHADER {m.severity}: {m.message} ({m.line})");
            Require(!messages.Any(m => m.severity.ToString() == "Error"), "已渲染 Shader 变体无编译错误");
            Report.Add("PASS END；不是 Quest/PICO 双眼实测或 GPU 性能计时。");
            Finish(0);
        }
        catch (Exception e) { Fail(e); }
    }

    static Color32[] Capture(string name, int mode, float seconds, bool second = true)
    {
        ThinCloudSkyPreview.SetMode(material, mode, second);
        material.SetFloat("_PreviewTime", seconds);
        ThinCloudSkyPreview.Render(camera, target);
        var previous = RenderTexture.active;
        Texture2D image = null;
        try
        {
            RenderTexture.active = target;
            image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); image.Apply();
            File.WriteAllBytes(Out + "/" + name + ".png", image.EncodeToPNG());
            var pixels = image.GetPixels32();
            Require(pixels.Count(p => p.r > 240 && p.b > 240 && p.g < 30) < 10, name + " 无粉色错误像素");
            return pixels;
        }
        finally { RenderTexture.active = previous; if (image) Object.DestroyImmediate(image); }
    }

    static double Difference(Color32[] a, Color32[] b)
    {
        double sum = 0;
        for (int i = 0; i < a.Length; i++) sum += Math.Abs(a[i].r - b[i].r) + Math.Abs(a[i].g - b[i].g) + Math.Abs(a[i].b - b[i].b);
        return sum / (a.Length * 3);
    }
    static void Require(bool value, string label)
    { Report.Add((value ? "PASS " : "FAIL ") + label); if (!value) throw new Exception(label); }
    static void Fail(Exception e) { Report.Add(e.ToString()); Debug.LogException(e); Finish(1); }
    static void Finish(int code)
    {
        File.WriteAllLines(Out + "/report.txt", Report);
        ShaderUtil.allowAsyncCompilation = oldAsync;
        if (target) { target.Release(); Object.DestroyImmediate(target); }
        if (material) Object.DestroyImmediate(material);
        EditorApplication.Exit(code);
    }
}
