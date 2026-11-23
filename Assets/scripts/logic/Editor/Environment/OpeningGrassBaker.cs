using System;
using System.Collections.Generic;
using System.Linq;
using NarutoVR.SceneArt;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Random = System.Random;

namespace NarutoVR.Editor
{
    public static class OpeningGrassBaker
    {
        internal const string ScenePath = "Assets/Scene/Chapter01_Awakening.unity";
        internal const string PrefabPath = "Assets/Art/Environment/Opening/Grass/OpeningGrass.prefab";
        internal const string ShaderPath = "Assets/Shaders/OpeningGrass.shader";
        private const string AssetDirectory = "Assets/Art/Environment/Opening/Grass";
        private const string ValleyMesh = "Assets/Art/Environment/Opening/MountainPolish/ValleyLandscape.asset";
        private const int TuftsPerPatch = 30;
        private const float PatchHalfWidth = 1.05f;
        private const float RootEmbed = 0.012f;
        private const float MaxLodHeightDifference = 0.08f;

        // Outside the two side passages and on the courtyard shoulders, clear of the canal and paving.
        private static readonly Vector2[] PatchCenters =
        {
            new Vector2(-18f, 22.5f), new Vector2(-18f, 25f), new Vector2(-17f, 29f),
            new Vector2(23.5f, 37f), new Vector2(21f, 39.5f), new Vector2(18.7f, 39.5f),
            new Vector2(-16.5f, 43f), new Vector2(-16.5f, 46f)
        };

        [MenuItem("NarutoVR/Environment/Rebake Opening Grass")]
        public static void Rebake()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
                throw new InvalidOperationException("Open Chapter01_Awakening before baking the grass.");

            GameObject exterior = scene.GetRootGameObjects().Single(root => root.name == "首章外景细化");
            MeshFilter valley = exterior.GetComponentsInChildren<MeshFilter>()
                .Single(filter => AssetDatabase.GetAssetPath(filter.sharedMesh) == ValleyMesh);
            LODGroup valleyLod = valley.GetComponentInParent<LODGroup>();
            if (!valleyLod)
                throw new InvalidOperationException("The opening valley LOD group is missing.");

            MeshFilter[] levels = valleyLod.GetLODs()
                .Select(lod => lod.renderers.Single().GetComponent<MeshFilter>()).ToArray();
            if (levels.Length != 3 || levels[0] != valley)
                throw new InvalidOperationException("The opening valley needs its three authored mesh LODs.");

            var preview = EditorSceneManager.NewPreviewScene();
            var ground = new GameObject("Temporary grass ground sampler");
            var prefab = new GameObject("OpeningGrass");
            SceneManager.MoveGameObjectToScene(ground, preview);
            SceneManager.MoveGameObjectToScene(prefab, preview);
            try
            {
                var surfaces = levels.Select(level => CreateGroundCollider(ground.transform, level)).ToArray();
                Physics.SyncTransforms();
                Vector3[][] roots = PatchCenters.Select((center, index) =>
                    FindRoots(scene.GetPhysicsScene(), center, index, surfaces)).ToArray();
                if (!AssetDatabase.IsValidFolder(AssetDirectory))
                    AssetDatabase.CreateFolder(System.IO.Path.GetDirectoryName(AssetDirectory).Replace('\\', '/'), "Grass");
                Material material = LoadOrCreateMaterial();
                var renderers = new MeshRenderer[roots.Length];
                for (int i = 0; i < roots.Length; i++)
                    renderers[i] = CreatePatch(prefab.transform, material, roots[i], i);
                prefab.AddComponent<OpeningGrassWind>().Configure(renderers);
                PrefabUtility.SaveAsPrefabAsset(prefab, PrefabPath, out bool saved);
                if (!saved) throw new InvalidOperationException("Could not save the opening grass prefab.");
                AssetDatabase.SaveAssets();
                Debug.Log($"Opening grass bake complete: {roots.Sum(patch => patch.Length)} tufts in {renderers.Length} patches.");
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        private static MeshCollider CreateGroundCollider(Transform sampler, MeshFilter source)
        {
            var target = new GameObject(source.name + " grass projection");
            SceneManager.MoveGameObjectToScene(target, sampler.gameObject.scene);
            target.transform.SetParent(sampler, false);
            target.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            target.transform.localScale = source.transform.lossyScale;
            var collider = target.AddComponent<MeshCollider>();
            collider.sharedMesh = source.sharedMesh;
            return collider;
        }

        private static Vector3[] FindRoots(PhysicsScene scenePhysics, Vector2 center, int index, MeshCollider[] ground)
        {
            var random = new Random(8731 + index * 113);
            var roots = new List<Vector3>(TuftsPerPatch);
            for (int attempt = 0; attempt < 600 && roots.Count < TuftsPerPatch; attempt++)
            {
                float x = center.x + ((float)random.NextDouble() * 2f - 1f) * PatchHalfWidth;
                float z = center.y + ((float)random.NextDouble() * 2f - 1f) * PatchHalfWidth;
                var ray = new Ray(new Vector3(x, 15f, z), Vector3.down);
                if (!ground[0].Raycast(ray, out RaycastHit near, 35f) || near.normal.y < 0.79f ||
                    !ground[1].Raycast(ray, out RaycastHit mid, 35f) ||
                    !ground[2].Raycast(ray, out RaycastHit far, 35f) ||
                    Mathf.Max(near.point.y, mid.point.y, far.point.y) - Mathf.Min(near.point.y, mid.point.y, far.point.y) >
                    MaxLodHeightDifference)
                    continue;
                if (scenePhysics.Raycast(near.point + Vector3.up * 0.36f, Vector3.down, out RaycastHit obstruction,
                        0.33f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) && obstruction.point.y > near.point.y + 0.03f)
                    continue;
                if (roots.Exists(root => (root - near.point).sqrMagnitude < 0.14f * 0.14f))
                    continue;
                // A single root position must stay planted when the ground changes LOD.
                float lowestSurface = Mathf.Min(near.point.y, mid.point.y, far.point.y);
                roots.Add(new Vector3(x, lowestSurface - RootEmbed, z));
            }
            if (roots.Count != TuftsPerPatch)
                throw new InvalidOperationException($"Grass patch {index + 1} at {center} only found {roots.Count} suitable roots.");
            return roots.ToArray();
        }

        private static Material LoadOrCreateMaterial()
        {
            string path = AssetDirectory + "/OpeningGrass.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material) return material;
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            if (!shader) throw new InvalidOperationException("The opening grass shader is missing.");
            material = new Material(shader) { name = "OpeningGrass" };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static MeshRenderer CreatePatch(Transform parent, Material material, Vector3[] roots, int index)
        {
            string name = $"GrassPatch{index + 1:D2}";
            var patch = new GameObject(name);
            SceneManager.MoveGameObjectToScene(patch, parent.gameObject.scene);
            patch.transform.SetParent(parent, false);
            // The prefab is installed at the scene origin, so these world roots become patch-local positions.
            patch.transform.localPosition = new Vector3(PatchCenters[index].x, roots.Average(root => root.y), PatchCenters[index].y);
            Mesh mesh = GrassPatchMesh.Build(roots.Select(root => root - patch.transform.localPosition).ToArray(), 9863 + index * 227);
            mesh.name = name;
            return SavePatch(patch, mesh, material, AssetDirectory + "/" + name + ".asset");
        }

        internal static MeshRenderer SavePatch(GameObject patch, Mesh mesh, Material material, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing)
            {
                EditorUtility.CopySerialized(mesh, existing);
                UnityEngine.Object.DestroyImmediate(mesh);
                mesh = existing;
            }
            else
            {
                AssetDatabase.CreateAsset(mesh, path);
            }

            patch.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = patch.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            return renderer;
        }
    }
}
