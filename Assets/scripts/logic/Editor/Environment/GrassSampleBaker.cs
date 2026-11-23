using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Random = System.Random;

namespace NarutoVR.Editor
{
    public static class GrassSampleBaker
    {
        private const string GrassFolder = "Assets/Art/Environment/Opening/Grass";
        private const string SampleFolder = GrassFolder + "/Samples";

        [MenuItem("NarutoVR/Environment/Rebuild Grass Samples")]
        public static void Rebuild()
        {
            if (!AssetDatabase.IsValidFolder(SampleFolder))
                AssetDatabase.CreateFolder(GrassFolder, "Samples");

            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var soft = WindMaterial("SoftWind", .015f);
                var breeze = WindMaterial("MeadowWind", .025f);
                Bake(preview, "ShortVerge", 483, 8, 6, 2.6f, 1.05f, 0f, soft,
                    new GrassPatchMesh.Shape(.56f, 1.12f, 1.20f, 2, new Color(.19f, .245f, .102f)), null);
                Bake(preview, "DenseMeadow", 532, 7, 6, 2.10f, 2.10f, 0f, breeze,
                    new GrassPatchMesh.Shape(1.02f, 1.62f, 1.34f, 3, new Color(.192f, .275f, .105f),
                        flowerHeight: 1.14f, footSpread: 5f), Scatter(715, 10, 8, 2.10f, 2.10f, 0f), 5, 12, 16, 27, 36);
                Bake(preview, "FlowerBank", 624, 8, 6, 2.70f, 1.25f, .25f, breeze,
                    new GrassPatchMesh.Shape(1.12f, 1.03f, .92f, 3, new Color(.115f, .235f, .15f),
                        flowerHeight: 1.35f, bloomSize: 1.35f), null, 9, 10, 17, 18, 26, 21, 22, 29, 30, 37);
                AssetDatabase.SaveAssets();
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        private static Material WindMaterial(string name, float amplitude)
        {
            string path = SampleFolder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material)
            {
                var source = AssetDatabase.LoadAssetAtPath<Material>(GrassFolder + "/OpeningGrass.mat");
                if (!source) throw new InvalidOperationException("Opening grass material is missing.");
                material = new Material(source) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetFloat("_WindWaveNumber", 1.15f);
            material.SetFloat("_WindAmplitude", amplitude);
            material.SetFloat("_WindFrequency", .5f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Vector3[] Scatter(int seed, int columns, int rows, float length, float width, float bankCurve)
        {
            var roots = new Vector3[rows * columns];
            var random = new Random(seed);
            for (int i = 0; i < roots.Length; i++)
            {
                // Jitter within cells so contiguous coverage survives each deterministic rebake.
                float u = (i % columns + .1f + .8f * (float)random.NextDouble()) / columns - .5f;
                float v = (i / columns + .1f + .8f * (float)random.NextDouble()) / rows - .5f;
                float taper = 1f - .32f * Mathf.Pow(2f * Mathf.Abs(u), 3f);
                roots[i] = new Vector3(u * length, 0f, v * width * taper + bankCurve * Mathf.Cos(u * Mathf.PI * 2f));
            }

            return roots;
        }

        private static void Bake(Scene preview, string name, int seed, int columns, int rows, float length,
            float width, float bankCurve, Material material, GrassPatchMesh.Shape shape, Vector3[] groundCover, params int[] flowers)
        {
            var roots = Scatter(seed, columns, rows, length, width, bankCurve);
            Mesh mesh = GrassPatchMesh.Build(roots, seed, shape, new HashSet<int>(flowers), groundCover);
            mesh.name = name;
            var patch = new GameObject(name);
            SceneManager.MoveGameObjectToScene(patch, preview);
            string path = SampleFolder + "/" + name;
            OpeningGrassBaker.SavePatch(patch, mesh, material, path + ".asset");
            PrefabUtility.SaveAsPrefabAsset(patch, path + ".prefab", out bool saved);
            if (!saved) throw new InvalidOperationException("Could not save grass sample " + name);
        }
    }
}
