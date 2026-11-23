using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace NarutoVR.Editor
{
    public static class CanalReflectionBaker
    {
        private const string CourtFolder = "Assets/Art/Environment/Opening/CourtLayout/";
        private const int Size = 128;

        [MenuItem("NarutoVR/Environment/Bake Canal Gauge Reflection")]
        public static void Bake()
        {
            var scene = SceneManager.GetActiveScene();
            bool courtyard = scene.path == "Assets/Scene/Chapter01_Courtyard.unity";
            if (!courtyard && scene.path != OpeningGrassBaker.ScenePath)
                throw new InvalidOperationException("Open the awakening or courtyard scene to bake its water reflections.");
            var filters = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MeshFilter>(true)).ToArray();
            Renderer Surface(string path)
            {
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (!mesh) throw new InvalidOperationException("Missing canal mesh: " + path);
                return filters.Single(filter => filter.sharedMesh == mesh).GetComponent<Renderer>();
            }
            var gauge = Surface(courtyard ? CourtFolder + "CourtGaugeChannel.asset" : "Assets/Meshes/Chapter01/CanalRibbon.asset");
            var surfaces = courtyard ? new[] { gauge, Surface(CourtFolder + "入庭明渠水面.asset") } : new[] { gauge };
            var hidden = surfaces.Select(s => s.forceRenderingOff).ToArray();
            try
            {
                foreach (var surface in surfaces) surface.forceRenderingOff = true;
                Capture(gauge.transform.TransformPoint(new Vector3(.54f, .39f, 52.6f)), courtyard
                    ? CourtFolder + "CourtGaugeReflection.asset" : "Assets/Materials/Chapter01/CanalGaugeReflection.asset");
                if (courtyard)
                    Capture(surfaces[1].transform.TransformPoint(new Vector3(.539f, .39f, 5.575f)),
                        CourtFolder + "CourtChannelReflection.asset");
                AssetDatabase.SaveAssets();
            }
            finally
            {
                for (int i = 0; i < surfaces.Length; i++) surfaces[i].forceRenderingOff = hidden[i];
            }
        }

        private static void Capture(Vector3 position, string path)
        {
            var cameraObject = new GameObject("Canal reflection capture") { hideFlags = HideFlags.HideAndDontSave };
            var cube = new Cubemap(Size, TextureFormat.RGBA32, true) { name = System.IO.Path.GetFileNameWithoutExtension(path), filterMode = FilterMode.Trilinear };
            try
            {
                var camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false;
                camera.transform.position = position;
                camera.clearFlags = CameraClearFlags.Skybox;
                camera.cullingMask = ~(1 << LayerMask.NameToLayer("UI"));
                camera.nearClipPlane = .06f;
                camera.farClipPlane = 250f;
                camera.allowHDR = false;
                camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
                if (!camera.RenderToCubemap(cube))
                    throw new InvalidOperationException("Unity could not capture the canal reflection.");
                var existing = AssetDatabase.LoadAssetAtPath<Cubemap>(path);
                if (existing)
                {
                    EditorUtility.CopySerialized(cube, existing);
                    EditorUtility.SetDirty(existing);
                }
                else
                {
                    AssetDatabase.CreateAsset(cube, path);
                    cube = null;
                }
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
                if (cube) Object.DestroyImmediate(cube);
            }
        }
    }
}
