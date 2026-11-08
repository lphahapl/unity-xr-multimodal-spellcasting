using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Editor only. Run once after import to build materials and usable prefabs.
public static class ModularBoardSetup
{
    private const string Root = "Assets/ModularBoard";
    private static readonly Dictionary<string, Color> Colors = new Dictionary<string, Color>
    {
        { "MB_Base", new Color(.105f,.12f,.145f) },
        { "MB_Ivory", new Color(.81f,.78f,.70f) },
        { "MB_Red", new Color(.78f,.12f,.065f) },
        { "MB_Green", new Color(.08f,.52f,.22f) },
        { "MB_Blue", new Color(.055f,.31f,.78f) },
        { "MB_Yellow", new Color(.92f,.60f,.045f) },
        { "MB_Purple", new Color(.52f,.16f,.68f) },
        { "MB_Gold", new Color(.86f,.51f,.065f) },
        { "MB_GoldInset", new Color(.63f,.32f,.025f) },
        { "MB_Center", new Color(.155f,.18f,.215f) }
    };

    [MenuItem("Tools/Modular Board/Create Prefabs and Materials")]
    public static void Build()
    {
        EnsureFolder(Root + "/Materials");
        EnsureFolder(Root + "/Prefabs");
        Shader shader;
        var pipeline = GraphicsSettings.currentRenderPipeline;
        if (pipeline == null) shader = Shader.Find("Standard");
        else if (pipeline.GetType().Name.IndexOf("HDRender", StringComparison.OrdinalIgnoreCase) >= 0)
            shader = Shader.Find("HDRP/Lit");
        else shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            throw new InvalidOperationException("No supported Lit shader found. Use Built-in, URP or HDRP.");

        var materials = new Dictionary<string, Material>();
        foreach (var entry in Colors)
        {
            string path = Root + "/Materials/" + entry.Key + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool create = material == null;
            if (create) material = new Material(shader);
            else material.shader = shader;
            // Blender's authored numeric colors are scene-linear; Unity's color API expects sRGB.
            var color = entry.Value.gamma;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            bool gold = entry.Key.StartsWith("MB_Gold", StringComparison.Ordinal);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", gold ? .45f : 0f);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", gold ? .54f : .36f);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", gold ? .54f : .36f);
            if (create) AssetDatabase.CreateAsset(material, path);
            else EditorUtility.SetDirty(material);
            materials.Add(entry.Key, material);
        }

        string[] files = Directory.GetFiles(Root + "/Models", "*.fbx");
        int count = 0;
        foreach (string rawPath in files)
        {
            string path = rawPath.Replace('\\', '/');
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer != null)
            {
                bool changed = importer.globalScale != 1f || !importer.useFileScale || importer.addCollider;
                importer.globalScale = 1f;
                importer.useFileScale = true;
                importer.addCollider = false;
                if (changed) importer.SaveAndReimport();
            }
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (model == null) throw new InvalidOperationException("Cannot load " + path);
            string name = Path.GetFileNameWithoutExtension(path);
            bool board = name.StartsWith("Board_", StringComparison.Ordinal);
            var wrapper = new GameObject(name);
            try
            {
                var instance = UnityEngine.Object.Instantiate(model, wrapper.transform, false);
                instance.name = "Geometry";
                var renderers = instance.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0) throw new InvalidOperationException("Model has no renderers: " + name);
                Bounds bounds = renderers[0].bounds;
                foreach (var renderer in renderers)
                {
                    bounds.Encapsulate(renderer.bounds);
                    var slots = renderer.sharedMaterials;
                    for (int i = 0; i < slots.Length; i++)
                    {
                        string key = slots[i] == null ? "" : slots[i].name;
                        key = key.Replace(" (Instance)", "");
                        Material replacement;
                        if (materials.TryGetValue(key, out replacement)) slots[i] = replacement;
                        else throw new InvalidOperationException("Unrecognized material '" + key + "' in " + name);
                    }
                    renderer.sharedMaterials = slots;
                }
                float expected = board ? 10f : 1f;
                if (bounds.size.x < .000001f) throw new InvalidOperationException("Empty bounds: " + name);
                instance.transform.localScale *= expected / bounds.size.x;
                bounds = renderers[0].bounds;
                foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                if (Mathf.Abs(bounds.size.x - expected) > .001f || Mathf.Abs(bounds.size.z - expected) > .001f)
                    throw new InvalidOperationException("Invalid footprint after conversion: " + name + " " + bounds.size);
                instance.transform.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);

                if (board)
                {
                    var collisions = new GameObject("Route_Colliders");
                    collisions.transform.SetParent(wrapper.transform, false);
                    int index = 0;
                    for (int x = 0; x < 10; x++) AddTileCollider(collisions.transform, index++, x - 4.5f, 4.5f);
                    for (int y = 1; y < 10; y++) AddTileCollider(collisions.transform, index++, 4.5f, 4.5f - y);
                    for (int x = 8; x >= 0; x--) AddTileCollider(collisions.transform, index++, x - 4.5f, -4.5f);
                    for (int y = 8; y >= 1; y--) AddTileCollider(collisions.transform, index++, -4.5f, 4.5f - y);
                    var center = new GameObject("Center_Collider_Removable");
                    center.transform.SetParent(wrapper.transform, false);
                    var box = center.AddComponent<BoxCollider>();
                    box.center = new Vector3(0, .0175f, 0);
                    box.size = new Vector3(8, .035f, 8);
                }
                else
                {
                    var box = wrapper.AddComponent<BoxCollider>();
                    box.center = new Vector3(0, .06f, 0);
                    box.size = new Vector3(1, .12f, 1);
                }
                PrefabUtility.SaveAsPrefabAsset(wrapper, Root + "/Prefabs/" + name + ".prefab");
                count++;
            }
            finally { UnityEngine.Object.DestroyImmediate(wrapper); }
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        string message = "Created " + count + " prefabs. Tile footprint: 1 m x 1 m. Board: 10 m x 10 m.";
        if (Application.isBatchMode) Debug.Log(message);
        else EditorUtility.DisplayDialog("Modular Board", message, "OK");
    }

    private static void AddTileCollider(Transform parent, int index, float x, float z)
    {
        var obj = new GameObject("Tile_" + index.ToString("00"));
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = new Vector3(x, 0, z);
        var box = obj.AddComponent<BoxCollider>();
        box.center = new Vector3(0, .06f, 0);
        box.size = new Vector3(1, .12f, 1);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
