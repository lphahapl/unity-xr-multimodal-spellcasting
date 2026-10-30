using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NarutoVR.SealLab;
using Oculus.Interaction;
using Oculus.Interaction.Input;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Lab = NarutoVR.SealLab.SealLab;

namespace NarutoVR.Editor
{
    [InitializeOnLoad]
    public static class SealLabBuilder
    {
        public const string ScenePath = "Assets/SealLab/Scenes/SealLab.unity";
        private const string Request = "Logs/seal-lab-build.request";
        static SealLabBuilder() { if (File.Exists(Request)) EditorApplication.delayCall += RequestedBuild; }
        private static void RequestedBuild()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) { EditorApplication.delayCall += RequestedBuild; return; }
            if (!File.Exists(Request)) return;
            File.Delete(Request);
            try { CreateLab(); SealLabChecks.Run(); File.WriteAllText("Logs/seal-lab-build-result.json", "{\"passed\":true}"); }
            catch (Exception ex) { File.WriteAllText("Logs/seal-lab-build-result.json", JsonUtility.ToJson(new Failure { error = ex.ToString() })); Debug.LogException(ex); }
        }
        [Serializable] private class Failure { public bool passed; public string error; }

        [MenuItem("Tools/NarutoVR/Create Seal Lab Scene")]
        public static void CreateLab()
        {
            ConfigureRendering();
            Directory.CreateDirectory("Assets/SealLab/Scenes"); Directory.CreateDirectory("Assets/SealLab/Profiles");
            if (File.Exists(ScenePath)) { Debug.Log("[SealLab] Scene already exists; keeping user changes."); return; }
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                var rigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Packages/com.meta.xr.sdk.core/Prefabs/OVRCameraRig.prefab");
                var handPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Packages/com.meta.xr.sdk.interaction.ovr/Runtime/Prefabs/Hands/OVRHandsDataSource.prefab");
                if (!rigPrefab || !handPrefab) throw new InvalidOperationException("Installed SDK prefabs not found.");
                var rigObject = (GameObject)PrefabUtility.InstantiatePrefab(rigPrefab, scene);
                var rig = rigObject.GetComponent<OVRCameraRig>();
                var manager = rigObject.GetComponent<OVRManager>();
                manager.trackingOriginType = OVRManager.TrackingOrigin.FloorLevel;
                var sources = (GameObject)PrefabUtility.InstantiatePrefab(handPrefab, scene);
                sources.transform.SetParent(rigObject.transform, false);
                var rigRef = sources.AddComponent<OVRCameraRigRef>(); rigRef.InjectAllOVRCameraRigRef(rig, false);
                var transformer = sources.AddComponent<TransformTrackingToWorldTransformer>();
                SetReference(transformer, "TrackingSpace", rig.trackingSpace);
                // The prefab's exposed Hands follow LastKnownGoodHand. Add direct, unmodified
                // branches so a lost/occluded pose cannot be replaced by its last good pose.
                foreach (var source in sources.GetComponentsInChildren<FromOVRHandDataSource>(true))
                {
                    var raw = source.gameObject.AddComponent<Hand>();
                    raw.InjectAllDataModifier(Hand.UpdateModeFlags.AfterPreviousStep, source, source, false);
                }
                var actualHands = sources.GetComponentsInChildren<Hand>(true)
                    .Where(h => h.GetType() == typeof(Hand) && h.GetComponent<FromOVRHandDataSource>()).ToArray();
                var left = actualHands.Single(h => h.name.EndsWith("Left"));
                var right = actualHands.Single(h => h.name.EndsWith("Right"));
                SetReference(rigRef, "_leftHand", left.GetComponent<OVRHand>());
                SetReference(rigRef, "_rightHand", right.GetComponent<OVRHand>());
                foreach (var source in sources.GetComponentsInChildren<FromOVRHandDataSource>(true))
                { SetReference(source, "_cameraRigRef", rigRef); SetReference(source, "_trackingToWorldTransformer", transformer); }
                var root = new GameObject("Seal Lab - actual tracked data");
                var lab = root.AddComponent<Lab>(); lab.leftTrackedHand = left; lab.rightTrackedHand = right; lab.recordingOrigin = rig.trackingSpace;
                string[] names = { "子", "丑", "寅", "卯", "辰", "巳", "午", "未", "申", "酉", "戌", "亥" };
                foreach (var name in names)
                {
                    string path = $"Assets/SealLab/Profiles/{name}.asset";
                    var profile = AssetDatabase.LoadAssetAtPath<SealProfile>(path);
                    if (!profile) { profile = ScriptableObject.CreateInstance<SealProfile>(); profile.sealName = name; AssetDatabase.CreateAsset(profile, path); }
                    lab.profiles.Add(profile);
                }
                var view = root.AddComponent<SealLabView>(); view.lab = lab;
                string materialPath = "Assets/SealLab/RawBones.mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (!material) { material = new Material(Shader.Find("Universal Render Pipeline/Unlit")); material.SetColor("_BaseColor", new Color(0.1f, 1, 0.65f)); AssetDatabase.CreateAsset(material, materialPath); }
                view.boneMaterial = material;
                var canvasObject = new GameObject("Tracking HUD", typeof(RectTransform), typeof(Canvas));
                canvasObject.transform.SetParent(rig.centerEyeAnchor, false);
                canvasObject.transform.localPosition = new Vector3(0, 0.08f, 0.85f);
                canvasObject.transform.localScale = Vector3.one * 0.00065f;
                var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
                var rect = canvasObject.GetComponent<RectTransform>(); rect.sizeDelta = new Vector2(1050, 760);
                var label = new GameObject("Feature values", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                label.transform.SetParent(canvasObject.transform, false);
                var text = label.GetComponent<Text>();
                view.chineseFont = Resources.Load<Font>("Fonts/NotoSansCJKsc-Regular");
                text.font = view.chineseFont ? view.chineseFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                text.fontSize = 25; text.color = Color.white; text.alignment = TextAnchor.UpperLeft; text.raycastTarget = false;
                text.text = "结印实验台 / 等待追踪";
                var textRect = label.GetComponent<RectTransform>(); textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one; textRect.offsetMin = textRect.offsetMax = Vector2.zero;
                view.statusText = text;
                var light = new GameObject("Directional Light").AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 0.8f;
                light.transform.rotation = Quaternion.Euler(50, -30, 0);
                EditorSceneManager.SaveScene(scene, ScenePath);
                var buildScenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).Select(s => new EditorBuildSettingsScene(s.path, false)).ToList();
                buildScenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true)); EditorBuildSettings.scenes = buildScenes.ToArray();
                AssetDatabase.SaveAssets();
                Debug.Log("[SealLab] Scene created. 12 templates are uncalibrated; hardware tracking has not been tested.");
            }
            finally
            {
                if (previous.IsValid()) SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }
        }
        private static void SetReference(UnityEngine.Object target, string field, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target); var property = serialized.FindProperty(field);
            if (property == null) throw new MissingFieldException(target.GetType().Name, field);
            property.objectReferenceValue = value; serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        public static void ConfigureRendering()
        {
            var assets = new List<RenderPipelineAsset>(); QualitySettings.GetAllRenderPipelineAssetsForPlatform("Android", ref assets);
            int samples = OVRPlugin.recommendedMSAALevel;
            if (samples != 2 && samples != 4 && samples != 8) samples = 4;
            foreach (var asset in assets.OfType<UniversalRenderPipelineAsset>())
            { asset.supportsHDR = false; asset.msaaSampleCount = samples; EditorUtility.SetDirty(asset); }
            AssetDatabase.SaveAssets(); Debug.Log($"[SealLab] Android URP: HDR off; MSAA {samples}x. No optional SDK installed.");
        }
        [MenuItem("Tools/NarutoVR/Open Seal Lab Scene")]
        public static void OpenScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(ScenePath)) CreateLab();
            EditorSceneManager.OpenScene(ScenePath); SealLabWindow.Open();
        }
    }
}
