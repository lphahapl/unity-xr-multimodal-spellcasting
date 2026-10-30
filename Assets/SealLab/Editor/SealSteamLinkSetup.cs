using System;
using System.IO;
using System.Linq;
using NarutoVR.SealLab;
using Oculus.Interaction;
using Oculus.Interaction.Input;
using Oculus.Interaction.Input.UnityXR;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management.Metadata;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.Management;
using UnityEditor.XR.Management;
using Lab = NarutoVR.SealLab.SealLab;

namespace NarutoVR.Editor
{
    [InitializeOnLoad]
    public static class SealSteamLinkSetup
    {
        public const string ScenePath = "Assets/SealLab/Scenes/SealLabSteamLink.unity";
        private const string SdkRoot = "Packages/com.meta.xr.sdk.interaction/Runtime/UnityXR/Prefabs/";
        private const string RequestPath = "Logs/steamlink-setup.request";
        private static double nextPoll;
        static SealSteamLinkSetup() => EditorApplication.update += PollRequest;
        private static void PollRequest()
        {
            if (EditorApplication.timeSinceStartup < nextPoll) return;
            nextPoll = EditorApplication.timeSinceStartup + 1;
            if (!File.Exists(RequestPath) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(RequestPath);
            try { BuildAndCheck(); }
            catch (Exception error)
            {
                File.WriteAllText("Logs/steamlink-setup-result.json", JsonUtility.ToJson(new Failure { error = error.ToString() }, true));
                Debug.LogException(error);
            }
        }
        [Serializable] private sealed class Failure { public bool passed; public string error; }

        [MenuItem("Tools/NarutoVR/Steam Link/准备独立实验场景")]
        public static void Prepare()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play before setup.");
            ConfigureWindows();
            if (File.Exists(ScenePath)) { Debug.Log("[SealSteamLink] Existing Steam Link scene preserved."); return; }
            var original = SceneManager.GetSceneByPath(SealLabBuilder.ScenePath);
            bool originalWasOpen = original.IsValid() && original.isLoaded;
            if (!originalWasOpen) original = EditorSceneManager.OpenScene(SealLabBuilder.ScenePath, OpenSceneMode.Additive);
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                var originalLab = original.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Lab>(true)).Single();
                var originalView = originalLab.GetComponent<SealLabView>();
                var rigAsset = AssetDatabase.LoadAssetAtPath<GameObject>(SdkRoot + "XROriginCameraRig.prefab");
                if (!rigAsset) throw new FileNotFoundException("SDK XROriginCameraRig missing.");
                var rig = (GameObject)PrefabUtility.InstantiatePrefab(rigAsset, scene);
                var origin = rig.GetComponent<XROrigin>();
                var tracking = origin.CameraFloorOffsetObject.transform;
                var sources = new GameObject("XR Hands actual tracking");
                sources.transform.SetParent(rig.transform, false);
                var transformer = sources.AddComponent<TransformTrackingToWorldTransformer>();
                Reference(transformer, "TrackingSpace", tracking);
                var input = sources.AddComponent<SealXRHandsSource>();
                input.leftHand = MakeHand("Left", Handedness.Left);
                input.rightHand = MakeHand("Right", Handedness.Right);

                // Copy calibrated profiles, timings and view configuration, never regenerate the native scene.
                var labObject = UnityEngine.Object.Instantiate(originalLab.gameObject);
                labObject.name = "Seal Lab - Steam Link OpenXR";
                var lab = labObject.GetComponent<Lab>();
                lab.leftTrackedHand = null; lab.rightTrackedHand = null;
                lab.xrHandsSource = input; lab.recordingOrigin = tracking;
                var view = lab.GetComponent<SealLabView>();
                if (originalView.statusText)
                {
                    var hud = UnityEngine.Object.Instantiate(originalView.statusText.canvas.gameObject, origin.Camera.transform, false);
                    hud.name = "Tracking HUD - Steam Link";
                    view.statusText = hud.GetComponentInChildren<UnityEngine.UI.Text>(true);
                }
                var light = new GameObject("Directional Light").AddComponent<Light>();
                light.type = LightType.Directional; light.intensity = 0.8f;
                light.transform.rotation = Quaternion.Euler(50, -30, 0);
                EditorSceneManager.SaveScene(scene, ScenePath);
                AssetDatabase.SaveAssets();
                Debug.Log("[SealSteamLink] Separate scene created; Android scene/build list unchanged. Select SteamVR OpenXR runtime before Play.");

                Hand MakeHand(string name, Handedness handedness)
                {
                    var go = new GameObject(name + " raw UnityXR hand"); go.transform.SetParent(sources.transform, false);
                    var data = go.AddComponent<FromUnityXRHandDataSource>();
                    var serialized = new SerializedObject(data);
                    serialized.FindProperty("_handedness").enumValueIndex = (int)handedness;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    data.InjectTrackingToWorldTransformer(transformer);
                    var hand = go.AddComponent<Hand>();
                    hand.InjectAllDataModifier(Hand.UpdateModeFlags.AfterPreviousStep, data, data, false);
                    return hand;
                }
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
                if (!originalWasOpen) EditorSceneManager.CloseScene(original, true);
            }
        }

        private static void ConfigureWindows()
        {
            var group = BuildTargetGroup.Standalone;
            var perTarget = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(group);
            if (perTarget == null) throw new InvalidOperationException("Windows XR settings missing; configure XR Plug-in Management first.");
            if (!XRPackageMetadataStore.AssignLoader(perTarget.Manager, "UnityEngine.XR.OpenXR.OpenXRLoader", group))
                throw new InvalidOperationException("Cannot assign Windows OpenXR loader.");
            perTarget.InitManagerOnStart = true;
            FeatureHelpers.RefreshFeatures(group);
            // The group must agree with the individual flags. Leaving it selected lets
            // Unity/Meta restore its required features during the next Play domain reload.
            var metaGroup = OpenXRFeatureSetManager.GetFeatureSetWithId(group, "com.meta.openxr.featureset.metaxr");
            if (metaGroup != null)
            {
                metaGroup.isEnabled = false;
                OpenXRFeatureSetManager.SetFeaturesFromEnabledFeatureSets(group);
            }
            var metaConfig = OVRProjectConfig.CachedProjectConfig;
            if (metaConfig != null && !metaConfig.metaXrFeaturePromptDeclined)
            {
                // Keep the intentional Windows choice from prompting to re-enable both
                // groups on reload. Android's group and feature flags stay enabled.
                metaConfig.metaXrFeaturePromptDeclined = true;
                OVRProjectConfig.CommitProjectConfig(metaConfig);
            }
            var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(group);
            bool found = false;
            foreach (var feature in settings.GetFeatures())
            {
                if (!feature) continue;
                switch (feature.GetType().Name)
                {
                    case "HandTracking": feature.enabled = true; found = true; break;
                    // Meta-specific session setup and aim are not required by XR_EXT_hand_tracking.
                    case "MetaXRFeature":
                    case "MetaHandTrackingAim": feature.enabled = false; break;
                    default: continue;
                }
                EditorUtility.SetDirty(feature);
            }
            if (!found) throw new InvalidOperationException("XR Hands HandTracking feature missing.");
            EditorUtility.SetDirty(perTarget); EditorUtility.SetDirty(perTarget.Manager); EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Tools/NarutoVR/Steam Link/打开独立实验场景")]
        public static void Open()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Prepare();
            EditorSceneManager.OpenScene(ScenePath); SealLabWindow.Open();
        }

        public static void BuildAndCheck()
        {
            Prepare(); SealLabChecks.Run(); CheckScene();
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/steamlink-setup-result.json", "{\"passed\":true,\"hardwareTested\":false,\"scene\":\"" + ScenePath + "\"}");
        }
        [MenuItem("Tools/NarutoVR/Steam Link/检查独立实验场景")]
        public static void CheckScene()
        {
            var metaGroup = OpenXRFeatureSetManager.GetFeatureSetWithId(BuildTargetGroup.Standalone, "com.meta.openxr.featureset.metaxr");
            if (metaGroup != null && metaGroup.isEnabled)
                throw new InvalidOperationException("Windows Meta feature group conflicts with generic OpenXR mode.");
            var windows = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Standalone);
            if (!windows.GetFeatures().Any(f => f && f.GetType().Name == "HandTracking" && f.enabled) ||
                windows.GetFeatures().Any(f => f && (f.GetType().Name == "MetaXRFeature" || f.GetType().Name == "MetaHandTrackingAim") && f.enabled))
                throw new InvalidOperationException("Windows OpenXR hand features are not configured for the Steam Link route.");
            var scene = SceneManager.GetSceneByPath(ScenePath);
            bool wasOpen = scene.IsValid() && scene.isLoaded;
            if (!wasOpen) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects();
                var lab = roots.SelectMany(g => g.GetComponentsInChildren<Lab>(true)).Single();
                if (!lab.xrHandsSource || !lab.xrHandsSource.IsConfigured || !lab.recordingOrigin || lab.profiles.Count != 12)
                    throw new InvalidOperationException("Invalid Steam Link tracking/configuration references.");
                foreach (var hand in new[] { lab.xrHandsSource.leftHand, lab.xrHandsSource.rightHand })
                    if (new SerializedObject(hand).FindProperty("_iModifyDataFromSourceMono").objectReferenceValue != hand.GetComponent<FromUnityXRHandDataSource>())
                        throw new InvalidOperationException("Hand must branch directly from UnityXR source.");
                var leftData = new SerializedObject(lab.xrHandsSource.leftHand.GetComponent<FromUnityXRHandDataSource>());
                var rightData = new SerializedObject(lab.xrHandsSource.rightHand.GetComponent<FromUnityXRHandDataSource>());
                if (leftData.FindProperty("_handedness").enumValueIndex != (int)Handedness.Left ||
                    rightData.FindProperty("_handedness").enumValueIndex != (int)Handedness.Right)
                    throw new InvalidOperationException("Steam Link hand sides reversed.");
                if (roots.Any(g => g.GetComponentInChildren<OVRManager>(true)) || roots.Any(g => g.GetComponentInChildren<FromOVRHandDataSource>(true)))
                    throw new InvalidOperationException("Steam Link scene must not depend on OVR runtime sources.");
                if (!lab.GetComponent<SealLabView>().statusText || roots.SelectMany(g => g.GetComponentsInChildren<Camera>(true)).Count(c => c.enabled) != 1)
                    throw new InvalidOperationException("Expected one camera and a wired HUD.");
                Debug.Log("[SealSteamLink] Scene references and raw hand branches verified; device tracking not yet tested.");
            }
            finally { if (!wasOpen) EditorSceneManager.CloseScene(scene, true); }
        }
        private static void Reference(UnityEngine.Object target, string field, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field);
            if (property == null) throw new MissingFieldException(target.GetType().Name, field);
            property.objectReferenceValue = value; serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
