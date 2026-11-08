using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ModularBoardBatch
{
    public static void BuildAndExport()
    {
        ModularBoardSetup.Build();
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));
        var report = new StringBuilder("Unity " + Application.unityVersion + "\n");
        var prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/ModularBoard/Prefabs" });
        if (prefabs.Length != 10) throw new InvalidOperationException("Expected 10 prefabs");
        foreach (var guid in prefabs)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            var obj = UnityEngine.Object.Instantiate(asset);
            try
            {
                var renderers = obj.GetComponentsInChildren<Renderer>();
                Bounds b = renderers[0].bounds;
                foreach (var r in renderers)
                {
                    b.Encapsulate(r.bounds);
                    foreach (var m in r.sharedMaterials)
                        if (m == null || m.shader == null ||
                            (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null && !m.shader.isSupported))
                            throw new InvalidOperationException("Invalid material: " + asset.name);
                }
                bool board = asset.name.StartsWith("Board_");
                float size = board ? 10f : 1f;
                if (Mathf.Abs(b.size.x-size)>.0001f || Mathf.Abs(b.size.z-size)>.0001f || Mathf.Abs(b.min.y)>.0001f)
                    throw new InvalidOperationException("Invalid Unity bounds: " + asset.name + " " + b);
                int colliders = obj.GetComponentsInChildren<BoxCollider>().Length;
                if (colliders != (board ? 37 : 1)) throw new InvalidOperationException("Invalid colliders: " + asset.name);
                report.AppendLine(asset.name + " | bounds=" + b.size.ToString("F6") + " | colliders=" + colliders + " | PASS");
            }
            finally { UnityEngine.Object.DestroyImmediate(obj); }
        }
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var boardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ModularBoard/Prefabs/Board_10m_36Tiles.prefab");
        PrefabUtility.InstantiatePrefab(boardPrefab, scene);
        var camObj = new GameObject("Main Camera");
        camObj.tag = "MainCamera";
        var cam = camObj.AddComponent<Camera>();
        cam.orthographic = true; cam.orthographicSize = 7.5f;
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.27f,.30f,.35f);
        camObj.transform.position = new Vector3(10,15,-13); camObj.transform.LookAt(Vector3.zero);
        var light = new GameObject("Studio Sun").AddComponent<Light>();
        light.type = LightType.Directional; light.intensity = 1.15f;
        light.transform.rotation = Quaternion.Euler(48,-35,0);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(.55f,.57f,.62f);
        if (!AssetDatabase.IsValidFolder("Assets/ModularBoard/Scenes")) AssetDatabase.CreateFolder("Assets/ModularBoard", "Scenes");
        EditorSceneManager.SaveScene(scene, "Assets/ModularBoard/Scenes/BoardDemo.unity");
        File.WriteAllText(Path.Combine(root, "unity-validation.txt"), report.ToString(), Encoding.UTF8);
        AssetDatabase.SaveAssets();
        AssetDatabase.ExportPackage("Assets/ModularBoard", Path.Combine(root, "ModularBoard_v1.unitypackage"), ExportPackageOptions.Recurse);
        Debug.Log("MODULAR_BOARD_UNITY_PASS");
    }
}
