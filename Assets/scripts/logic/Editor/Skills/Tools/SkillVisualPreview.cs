#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace NarutoVR.SealCombat.Editor
{
    /// <summary>复制现有特效到隔离预览场景进行渲染，输出便于核对的图片。</summary>
    public static class SkillVisualPreview
    {
        private const int Width = 1000, Height = 800, PreviewLayer = 31;

        [MenuItem("Tools/NarutoVR/技能/渲染四种特效预览")]
        public static void RenderAll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("请在编辑模式渲染独立特效预览。");
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("请等待脚本和资源导入完成。");
            // Do not call EnsureAssets here: inspection must not create or change project assets.
            string[] paths = { SkillVisualAssets.FireballPath, SkillVisualAssets.ShieldPath,
                SkillVisualAssets.ImpactPath, SkillVisualAssets.SigilPath };
            string[] outputs = { "Logs/vfx-fireball.png", "Logs/vfx-shield.png", "Logs/vfx-impact.png", "Logs/vfx-sigil.png" };
            foreach (string path in paths)
                if (!AssetDatabase.LoadAssetAtPath<GameObject>(path)) throw new InvalidOperationException("缺少实际特效预置体：" + path);
            Directory.CreateDirectory("Logs");
            bool asynchronous = ShaderUtil.allowAsyncCompilation;
            try
            {
                // Complete the required shader variants before reading pixels from the first preview.
                ShaderUtil.allowAsyncCompilation = false;
                for (int i = 0; i < paths.Length; ++i) Render(paths[i], outputs[i], i);
            }
            finally { ShaderUtil.allowAsyncCompilation = asynchronous; }
            Debug.Log("[技能特效] 已生成四张实际预置体预览：Logs/vfx-*.png；深蓝背景，无额外后处理，非头显性能测量。");
        }

        private static void Render(string prefabPath, string outputPath, int kind)
        {
            Scene preview = default;
            Camera camera = null;
            RenderTexture texture = null;
            Texture2D pixels = null;
            RenderTexture previousTarget = RenderTexture.active;
            try
            {
                preview = EditorSceneManager.NewPreviewScene();
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, preview);
                instance.name = "仅预览_" + prefab.name;
                instance.transform.position = Vector3.zero;
                foreach (var node in instance.GetComponentsInChildren<Transform>(true))
                { node.gameObject.layer = PreviewLayer; node.gameObject.hideFlags = HideFlags.HideAndDontSave; }
                foreach (var behaviour in instance.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
                float sample = kind == 2 ? .18f : kind == 3 ? 1.8f : 1.2f;
                SampleVisual(instance, sample, kind == 0);

                var cameraObject = new GameObject("独立特效预览相机", typeof(Camera));
                SceneManager.MoveGameObjectToScene(cameraObject, preview); cameraObject.hideFlags = HideFlags.HideAndDontSave;
                camera = cameraObject.GetComponent<Camera>(); camera.enabled = false; camera.scene = preview;
                camera.cameraType = CameraType.Game; camera.stereoTargetEye = StereoTargetEyeMask.None;
                camera.cullingMask = 1 << PreviewLayer; camera.useOcclusionCulling = false;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.006f, .012f, .03f, 1);
                camera.orthographic = true; camera.aspect = (float)Width / Height; camera.nearClipPlane = .01f; camera.farClipPlane = 20;
                camera.allowHDR = false; camera.allowMSAA = false;
                EditorUtility.SetCameraAnimateMaterials(camera, true);
                ConfigureCamera(camera, kind);
                var data = camera.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = false; data.renderShadows = false;
                data.volumeLayerMask = 0;
                data.antialiasing = AntialiasingMode.None;
                data.requiresColorOption = CameraOverrideOption.Off; data.requiresDepthOption = CameraOverrideOption.Off;

                texture = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32)
                { name = "临时特效预览", hideFlags = HideFlags.HideAndDontSave, antiAliasing = 1 };
                texture.Create(); camera.targetTexture = texture;
                // The first draw initializes this renderer's shader/particle path. Capture the warmed draw.
                camera.Render(); camera.Render();
                RenderTexture.active = texture;
                pixels = new Texture2D(Width, Height, TextureFormat.RGB24, false, false)
                { name = "临时特效预览像素", hideFlags = HideFlags.HideAndDontSave };
                pixels.ReadPixels(new Rect(0, 0, Width, Height), 0, 0); pixels.Apply(false, false);
                byte[] png = pixels.EncodeToPNG();
                if (png == null || png.Length == 0) throw new InvalidOperationException("预览PNG编码失败：" + prefabPath);
                File.WriteAllBytes(outputPath, png);
            }
            finally
            {
                RenderTexture.active = previousTarget;
                if (camera) camera.targetTexture = null;
                try { if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview); }
                finally
                {
                    if (pixels) Object.DestroyImmediate(pixels);
                    if (texture) { texture.Release(); Object.DestroyImmediate(texture); }
                }
            }
        }

        private static void ConfigureCamera(Camera camera, int kind)
        {
            Vector3 target = Vector3.zero;
            switch (kind)
            {
                case 0:
                    target = new Vector3(-.28f, .03f, 0);
                    camera.transform.position = target + new Vector3(.2f, .12f, -3);
                    camera.orthographicSize = .76f;
                    break;
                case 1:
                    camera.transform.position = new Vector3(.16f, .1f, -.4f);
                    camera.orthographicSize = .1f;
                    break;
                case 2:
                    camera.transform.position = new Vector3(.15f, .1f, -3);
                    camera.orthographicSize = .7f;
                    break;
                default:
                    target = Vector3.up * .06f;
                    camera.transform.position = new Vector3(0, 3.6f, -2.3f);
                    camera.orthographicSize = 1.3f;
                    break;
            }
            camera.transform.LookAt(target);
        }

        private static void SampleVisual(GameObject root, float seconds, bool showTrail)
        {
            var motion = root.GetComponent<SkillVisualMotion>();
            if (motion)
            {
                foreach (var orbit in motion.orbits)
                    if (orbit.node) orbit.node.localRotation *= Quaternion.Euler(orbit.angularVelocity * seconds);
                if (motion.pulseNode) motion.pulseNode.localScale *= 1 + Mathf.Sin(seconds * motion.pulseFrequency * Mathf.PI * 2) * motion.pulseAmount;
                if (motion.fadeSeconds > 0)
                {
                    float progress = Mathf.Clamp01(seconds / motion.fadeSeconds);
                    if (motion.expansionNode) motion.expansionNode.localScale *= Mathf.Lerp(1, motion.expansionMultiplier, 1 - Mathf.Pow(1 - progress, 3));
                    var properties = new MaterialPropertyBlock();
                    foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                    {
                        renderer.GetPropertyBlock(properties); properties.SetFloat("_VisualOpacity", (1 - progress) * (1 - progress));
                        renderer.SetPropertyBlock(properties);
                    }
                }
                if (showTrail)
                {
                    motion.SetFlying(true);
                    foreach (var trail in motion.flightTrails)
                    {
                        if (!trail) continue;
                        // A fixed short flight path makes the real prefab's two tail layers inspectable.
                        trail.time = 60; trail.emitting = false;
                        var points = new Vector3[18];
                        for (int i = 0; i < points.Length; ++i)
                        {
                            float t = (float)i / (points.Length - 1);
                            points[i] = root.transform.position + new Vector3(Mathf.Lerp(-1.05f, 0, t), .12f * Mathf.Sin((1 - t) * Mathf.PI), .06f * (1 - t));
                        }
                        trail.AddPositions(points);
                    }
                }
            }
            uint seed = 1827;
            foreach (var particles in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                particles.useAutoRandomSeed = false; particles.randomSeed = seed++;
                particles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                particles.Simulate(seconds, false, true, true); particles.Pause(false);
            }
        }
    }
}
#endif
