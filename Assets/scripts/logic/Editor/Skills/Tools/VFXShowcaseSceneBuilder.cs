#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace NarutoVR.SealCombat.Editor
{
    /// <summary>
    /// 一键生成并配置特效展厅测试场景 (Assets/Scene/VFXPreviewTest.unity)，包含完整uGUI面板与五大系特效配置。
    /// </summary>
    public static class VFXShowcaseSceneBuilder
    {
        public const string ScenePath = "Assets/Scene/VFXPreviewTest.unity";

        [MenuItem("Tools/NarutoVR/特效/创建或更新特效展厅场景")]
        public static void BuildOrUpdateScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("提示", "请在编辑模式下生成特效场景。", "确定");
                return;
            }

            var previousScene = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            try
            {
                // 1. 主摄像机
                var cameraGo = new GameObject("ShowcaseCamera", typeof(Camera));
                cameraGo.tag = "MainCamera";
                cameraGo.transform.position = new Vector3(0, 1.8f, -3.8f);
                cameraGo.transform.rotation = Quaternion.Euler(18.0f, 0, 0);

                var cam = cameraGo.GetComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.04f, 0.05f, 0.08f, 1.0f); // 深蓝玄色道场背景，凸显火光与雷光
                cam.fieldOfView = 50f;
                cam.nearClipPlane = 0.1f;
                cam.farClipPlane = 100f;

                var camData = cameraGo.AddComponent<UniversalAdditionalCameraData>();
                camData.renderPostProcessing = true;
                camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;

                // 2. 灯光设置 (柔和漫射，不冲淡自发光粒子)
                var mainLightGo = new GameObject("Directional Light", typeof(Light));
                mainLightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0);
                var mainLight = mainLightGo.GetComponent<Light>();
                mainLight.type = LightType.Directional;
                mainLight.color = new Color(0.9f, 0.92f, 1.0f);
                mainLight.intensity = 0.65f;
                mainLight.shadows = LightShadows.Soft;

                var fillLightGo = new GameObject("Fill Light", typeof(Light));
                fillLightGo.transform.rotation = Quaternion.Euler(30f, 150f, 0);
                var fillLight = fillLightGo.GetComponent<Light>();
                fillLight.type = LightType.Directional;
                fillLight.color = new Color(0.15f, 0.2f, 0.35f);
                fillLight.intensity = 0.35f;
                fillLight.shadows = LightShadows.None;

                // 3. 暗色展示底台 (圆盘地面)
                var stageGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                stageGo.name = "ShowcaseStage_Pedestal";
                stageGo.transform.position = new Vector3(0, -0.05f, 0);
                stageGo.transform.localScale = new Vector3(12.0f, 0.05f, 12.0f);

                var stageMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Showcase/玄色石面.mat");
                stageGo.GetComponent<Renderer>().sharedMaterial = stageMat;

                // 4. 特效生成挂点与控制器
                var managerGo = new GameObject("VFX_ShowcaseManager");
                var previewer = managerGo.AddComponent<VFXShowcasePreviewer>();
                previewer.showcaseCamera = cam;
                previewer.spawnRoot = managerGo.transform;

                // 烘入预览目录的真实预制体引用，让独立播放器也能切换全部条目。
                previewer.InitializeCuratedList();
                previewer.EnsureAllPrefabsLoaded();

                // 构建现代化 uGUI 界面
                previewer.EnsureEventSystem();
                previewer.BuildUGUIHierarchy();

                // 5. 保存场景
                Directory.CreateDirectory("Assets/Scene");
                EditorSceneManager.SaveScene(scene, ScenePath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Debug.Log($"[特效展厅] 成功生成并保存测试场景至：{ScenePath}，包含 uGUI 面板与全部分类特效。");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[特效展厅] 生成场景失败: {ex}");
            }
        }
    }
}
#endif
