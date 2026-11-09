using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace NarutoVR.SealCombat.Editor
{
    /// <summary>在独立预览场景中查看天空。只操作材质副本，不更改正式场景、灯光或材质资产。</summary>
    public sealed class ThinCloudSkyPreview : EditorWindow
    {
        public const string MaterialPath = "Assets/Art/Environment/Opening/Skybox/青灰薄云.mat";
        Material source, instance;
        Scene previewScene;
        Camera camera;
        Skybox cameraSky;
        RenderTexture target;
        int mode = 2;
        float yaw, pitch = 22;
        double epoch, nextFrame;
        bool secondLayer = true;
        string failure;
        static readonly string[] Modes = { "纯底色", "静态薄云", "动态薄云" };

        [MenuItem("Tools/NarutoVR/环境/青灰薄云预览")]
        public static void Open() => GetWindow<ThinCloudSkyPreview>("青灰薄云预览");

        void OnEnable()
        {
            minSize = new Vector2(520, 400);
            source = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            epoch = EditorApplication.timeSinceStartup;
            EditorApplication.update += Tick;
        }

        void OnDisable()
        {
            EditorApplication.update -= Tick;
            if (target) { target.Release(); DestroyImmediate(target); }
            if (instance) DestroyImmediate(instance);
            if (previewScene.IsValid()) EditorSceneManager.ClosePreviewScene(previewScene);
            camera = null; cameraSky = null; target = null; instance = null;
        }

        void Tick()
        {
            if (EditorApplication.timeSinceStartup < nextFrame) return;
            // 独立窗口最多 20 次/秒；不要求游戏相机额外渲染，也不创建运行时 Update 组件。
            nextFrame = EditorApplication.timeSinceStartup + 0.05;
            if (mode == 2 && !EditorApplication.isPlayingOrWillChangePlaymode) Repaint();
        }

        void OnGUI()
        {
            EditorGUILayout.HelpBox("左键拖动画面转头，滚轮调整仰角。这里只预览，不应用到场景。参数在材质 Inspector 调整；真机测性能前关闭本窗口。", MessageType.Info);
            source = (Material)EditorGUILayout.ObjectField("天空材质", source, typeof(Material), false);
            mode = GUILayout.Toolbar(mode, Modes);
            secondLayer = EditorGUILayout.Toggle("预览第二层", secondLayer);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("选中材质调整参数") && source) Selection.activeObject = source;
                if (GUILayout.Button("重置视角")) { yaw = 0; pitch = 22; epoch = EditorApplication.timeSinceStartup; }
            }
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            { EditorGUILayout.HelpBox("Play 时暂停独立预览，避免干扰游戏性能。退出 Play 后继续。", MessageType.Info); return; }
            if (!source) { EditorGUILayout.HelpBox("请选择青灰薄云材质。", MessageType.Warning); return; }
            if (source.shader.name != "NarutoVR/Environment/青灰薄云天空")
            { EditorGUILayout.HelpBox("请选择本工具配套的青灰薄云天空 Shader。", MessageType.Warning); return; }
            var rect = GUILayoutUtility.GetRect(32, 10000, 200, 10000, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            HandleLook(rect);
            if (Event.current.type == EventType.Repaint)
            {
                try
                {
                    float scale = Mathf.Min(1f, 1280f / Mathf.Max(rect.width, 1), 720f / Mathf.Max(rect.height, 1));
                    EnsurePreview(Mathf.Max(32, (int)(rect.width * scale)), Mathf.Max(32, (int)(rect.height * scale)));
                    instance.CopyPropertiesFromMaterial(source);
                    SetMode(instance, mode, secondLayer);
                    instance.SetFloat("_PreviewTime", mode == 2 ? (float)(EditorApplication.timeSinceStartup - epoch) : 0);
                    camera.transform.rotation = Quaternion.Euler(-pitch, yaw, 0);
                    Render(camera, target);
                    GUI.DrawTexture(rect, target, ScaleMode.StretchToFill, false);
                    failure = null;
                }
                catch (Exception e) { failure = e.Message; }
            }
            if (failure != null) EditorGUILayout.HelpBox("预览失败：" + failure, MessageType.Error);
        }

        void HandleLook(Rect rect)
        {
            Event e = Event.current;
            int id = GUIUtility.GetControlID(FocusType.Passive);
            if (e.type == EventType.MouseDown && e.button == 0 && rect.Contains(e.mousePosition))
            { GUIUtility.hotControl = id; e.Use(); }
            if (GUIUtility.hotControl == id && e.type == EventType.MouseDrag)
            { yaw += e.delta.x * 0.2f; pitch = Mathf.Clamp(pitch + e.delta.y * 0.2f, -85, 85); Repaint(); e.Use(); }
            if (GUIUtility.hotControl == id && e.type == EventType.MouseUp)
            { GUIUtility.hotControl = 0; e.Use(); }
            if (e.type == EventType.ScrollWheel && rect.Contains(e.mousePosition))
            { pitch = Mathf.Clamp(pitch + e.delta.y, -85, 85); Repaint(); e.Use(); }
        }

        void EnsurePreview(int width, int height)
        {
            if (!previewScene.IsValid())
            {
                previewScene = EditorSceneManager.NewPreviewScene();
                var root = new GameObject("仅天空预览相机") { hideFlags = HideFlags.HideAndDontSave };
                SceneManager.MoveGameObjectToScene(root, previewScene);
                camera = root.AddComponent<Camera>();
                camera.scene = previewScene;
                camera.enabled = false;
                camera.clearFlags = CameraClearFlags.Skybox;
                camera.cullingMask = 0;
                camera.fieldOfView = 80;
                camera.nearClipPlane = 0.1f; camera.farClipPlane = 100;
                camera.allowHDR = false; camera.allowMSAA = false;
                camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
                cameraSky = root.AddComponent<Skybox>();
            }
            if (!instance)
                instance = new Material(source) { hideFlags = HideFlags.HideAndDontSave };
            cameraSky.material = instance;
            if (target && target.width == width && target.height == height) return;
            if (target) { target.Release(); DestroyImmediate(target); }
            target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
                { hideFlags = HideFlags.HideAndDontSave };
            target.Create();
            camera.aspect = (float)width / height;
        }

        /// <summary>模式只作用于传入材质。纯底色关闭云变体；静态仍保留云采样。</summary>
        internal static void SetMode(Material material, int mode, bool secondLayer)
        {
            if (mode == 0) material.DisableKeyword("_CLOUDS_ON"); else material.EnableKeyword("_CLOUDS_ON");
            if (secondLayer) material.EnableKeyword("_SECOND_LAYER"); else material.DisableKeyword("_SECOND_LAYER");
            material.SetFloat("_Clouds", mode == 0 ? 0 : 1);
            material.SetFloat("_SecondLayer", secondLayer ? 1 : 0);
            material.SetFloat("_Animate", mode == 2 ? 1 : 0);
        }

        /// <summary>走 URP 的单相机请求；不修改全局 RenderSettings.skybox。</summary>
        internal static void Render(Camera camera, RenderTexture target)
        {
            var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
            if (!RenderPipeline.SupportsRenderRequest(camera, request))
                throw new InvalidOperationException("当前管线尚未就绪或不支持 URP 单相机预览。");
            RenderPipeline.SubmitRenderRequest(camera, request);
        }
    }
}
