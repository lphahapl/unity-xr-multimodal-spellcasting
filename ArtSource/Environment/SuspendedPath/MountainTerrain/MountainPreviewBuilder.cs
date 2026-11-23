using System;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class MountainPreviewBuilder
{
    const string Terrain = "Assets/Art/Environment/SuspendedPath/MountainTerrain/";
    const string Original = "Assets/Scene/Chapter02_SuspendedPath.unity";
    const string Preview = "Assets/Scene/Chapter02_SuspendedPath_MountainPreview.unity";
    static readonly Vector3 Center = new Vector3(35.6f, 0, 24);
    static readonly Vector3[] FloorProbes = {
        new Vector3(-9.12f, 80, 63.7f), new Vector3(-9.12f, 80, 24),
        new Vector3(35.6f, 0, 12.5f), new Vector3(30.6f, 0, 44.5f),
        new Vector3(72.6f, -80, 28.4f)
    };

    public static void Build()
    {
        string report = Environment.GetEnvironmentVariable("CH2_MOUNTAIN_REPORT");
        if (string.IsNullOrEmpty(report)) throw new InvalidOperationException("Set CH2_MOUNTAIN_REPORT.");
        Directory.CreateDirectory(report);
        using var log = new StreamWriter(Path.Combine(report, "UnityReadback.txt"));
        var scene = EditorSceneManager.OpenScene(Original);
        var oldMountains = scene.GetRootGameObjects().Single(go => go.name == "崖谷 · 周边岩脊");
        var cameras = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<Camera>(true)).ToArray();
        var oldFar = cameras.Select(cam => cam.farClipPlane).ToArray();
        var before = ReadFloors();
        TakeViews(report, "before", 450);

        var reliefTexture = ImportTexture("GorgeRelief_BaseColor.png", false);
        var skyTexture = ImportTexture("GorgeSkyline.png", true);
        var reliefMaterial = MaterialFor("GorgeRelief", "Universal Render Pipeline/Unlit");
        reliefMaterial.SetTexture("_BaseMap", reliefTexture);
        reliefMaterial.SetColor("_BaseColor", Color.white);
        var skyMaterial = MaterialFor("GorgeSkyline", "Skybox/Panoramic");
        skyMaterial.SetTexture("_MainTex", skyTexture);
        skyMaterial.SetColor("_Tint", new Color(.5f, .5f, .5f, 1));
        skyMaterial.SetFloat("_Exposure", 1);
        EditorUtility.SetDirty(reliefMaterial);
        EditorUtility.SetDirty(skyMaterial);

        var imported = AssetDatabase.LoadAssetAtPath<GameObject>(Terrain + "GorgeRelief.fbx");
        if (!imported) throw new InvalidOperationException("FBX failed to import.");
        var cluster = new GameObject("断衡山脉 · 高程网格");
        var model = (GameObject)PrefabUtility.InstantiatePrefab(imported, scene);
        model.transform.SetParent(cluster.transform, false);
        var filter = model.GetComponentInChildren<MeshFilter>();
        var mesh = filter.sharedMesh;
        var worldVertices = mesh.vertices.Select(filter.transform.TransformPoint).ToArray();
        float[] expected = File.ReadAllLines(Path.Combine(report, "corners.txt"))
            .Select(s => float.Parse(s, CultureInfo.InvariantCulture)).ToArray();
        var corners = new[] {
            new Vector2(-610, -610), new Vector2(610, -610),
            new Vector2(-610, 610), new Vector2(610, 610)
        };
        bool aligned = false;
        foreach (int x in new[] { 1, -1 })
        foreach (int z in new[] { 1, -1 })
        {
            var samples = corners.Select(corner => worldVertices.Single(v =>
                Mathf.Abs(v.x * x - corner.x) < .05f && Mathf.Abs(v.z * z - corner.y) < .05f)).ToArray();
            if (!samples.Select((v, i) => Mathf.Abs(v.y - expected[i]) < .05f).All(ok => ok)) continue;
            model.transform.localScale = new Vector3(x, 1, z);
            log.WriteLine($"Axis fit {x}/1/{z}. Four Unity corner elevations in m: {string.Join(", ", samples.Select(v => v.y))}");
            aligned = true;
        }
        if (!aligned) throw new InvalidOperationException("Imported model axes differ from sampled heights.");

        foreach (var renderer in model.GetComponentsInChildren<MeshRenderer>())
        {
            renderer.sharedMaterial = reliefMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }
        if (model.GetComponentsInChildren<Collider>(true).Length != 0 || mesh.uv.Length != mesh.vertexCount)
            throw new InvalidOperationException("Relief must have UVs and no collider.");
        if (mesh.GetIndexCount(0) / 3 != 35912)
            throw new InvalidOperationException("Unexpected triangle count.");
        log.WriteLine($"Unity {Application.unityVersion}; {SystemInfo.graphicsDeviceType}; vertices {mesh.vertexCount}; triangles {mesh.GetIndexCount(0)/3}");
        log.WriteLine($"UV vertices {mesh.uv.Length}; mesh bounds {mesh.bounds}; colliders 0.");
        log.WriteLine($"Textures {reliefTexture.width}×{reliefTexture.height}, {skyTexture.width}×{skyTexture.height}.");
        var prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(cluster, Terrain + "GorgeRelief.prefab", InteractionMode.AutomatedAction);
        if (!prefab) throw new InvalidOperationException("Could not save terrain prefab.");
        cluster.transform.position = Center;

        var oldRenderers = oldMountains.GetComponentsInChildren<Renderer>(true);
        foreach (var renderer in oldRenderers) renderer.enabled = false;
        RenderSettings.skybox = skyMaterial;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogColor = new Color(.61f, .69f, .72f, 1);
        RenderSettings.fogDensity = .00165f;
        foreach (var camera in cameras) camera.farClipPlane = 1050;
        DynamicGI.UpdateEnvironment();
        var after = ReadFloors();
        for (int i = 0; i < before.Length; ++i)
        {
            if (before[i].collider != after[i].collider || Mathf.Abs(before[i].point.y - after[i].point.y) > .001f)
                throw new InvalidOperationException($"Changed floor probe {FloorProbes[i]}.");
            log.WriteLine($"Preserved floor at {FloorProbes[i]} → {after[i].point.y:F3} {after[i].collider.name}");
        }
        log.WriteLine($"Old scenic renderers disabled: {oldRenderers.Length}; cameras {string.Join(", ", oldFar)} → 1050 m.");
        if (!EditorSceneManager.SaveScene(scene, Preview, true))
            throw new InvalidOperationException("Could not save the preview scene.");
        AssetDatabase.SaveAssets();
        TakeViews(report, "after", 1050);
        log.WriteLine("PASS");
    }

    static RaycastHit[] ReadFloors()
    {
        Physics.SyncTransforms();
        return FloorProbes.Select(p => {
            if (!Physics.Raycast(p + Vector3.up * 1.4f, Vector3.down, out var hit, 3,
                                 Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                throw new InvalidOperationException($"No floor at {p}.");
            return hit;
        }).ToArray();
    }

    static Texture2D ImportTexture(string file, bool sky)
    {
        string path = Terrain + file;
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.sRGBTexture = true;
        importer.mipmapEnabled = true;
        importer.wrapModeU = sky ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
        importer.wrapModeV = TextureWrapMode.Clamp;
        importer.maxTextureSize = sky ? 4096 : 2048;
        var android = importer.GetPlatformTextureSettings("Android");
        android.overridden = true;
        android.maxTextureSize = 2048;
        android.format = TextureImporterFormat.ASTC_6x6;
        importer.SetPlatformTextureSettings(android);
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static Material MaterialFor(string name, string shaderName)
    {
        string path = Terrain + name + ".mat";
        var shader = Shader.Find(shaderName);
        if (!shader) throw new InvalidOperationException($"Missing {shaderName}.");
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material) return material;
        material = new Material(shader);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    static void TakeViews(string report, string prefix, float far)
    {
        Capture("upper", new Vector3(-9.1f, 81.62f, 25.9f), new Vector3(62, 94, 44), 58);
        Capture("hub", new Vector3(32.3f, 1.62f, 15.3f), new Vector3(27, 5, 68), 58);
        Capture("lower", new Vector3(76.8f, -78.38f, 26.4f), new Vector3(14, -42, 31), 56);
        Capture("overview", new Vector3(117, 112, 122), new Vector3(-40, 55, 18), 68);

        void Capture(string name, Vector3 eye, Vector3 target, float fov)
        {
            var camera = new GameObject("Mountain validation camera").AddComponent<Camera>();
            camera.transform.position = eye;
            camera.transform.LookAt(target);
            camera.fieldOfView = fov;
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.stereoTargetEye = StereoTargetEyeMask.None;
            camera.nearClipPlane = .05f;
            camera.farClipPlane = far;
            var previous = RenderTexture.active;
            var targetTexture = RenderTexture.GetTemporary(1440, 810, 24, RenderTextureFormat.ARGB32);
            var frame = new Texture2D(1440, 810, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = targetTexture;
                camera.Render();
                RenderTexture.active = targetTexture;
                frame.ReadPixels(new Rect(0, 0, 1440, 810), 0, 0);
                frame.Apply();
                File.WriteAllBytes(Path.Combine(report, $"{prefix}-{name}.jpg"), frame.EncodeToJPG(94));
            }
            finally
            {
                RenderTexture.active = previous;
                camera.targetTexture = null;
                RenderTexture.ReleaseTemporary(targetTexture);
                UnityEngine.Object.DestroyImmediate(frame);
                UnityEngine.Object.DestroyImmediate(camera.gameObject);
            }
        }
    }
}
