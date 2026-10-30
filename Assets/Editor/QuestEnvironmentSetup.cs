using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;

namespace NarutoVR.Editor
{
    // Explicit one-shot setup; normal editor sessions never overwrite user settings.
    [InitializeOnLoad]
    public static class QuestEnvironmentSetup
    {
        private const string RequestPath = "Logs/quest-setup.request";
        private const string ReportPath = "Logs/quest-environment-validation.json";

        static QuestEnvironmentSetup()
        {
            if (File.Exists(RequestPath))
                EditorApplication.delayCall += ApplyRequestedSetup;
        }

        private static void ApplyRequestedSetup()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += ApplyRequestedSetup;
                return;
            }
            if (!File.Exists(RequestPath)) return;
            File.Delete(RequestPath);
            ConfigureAndValidate();
        }

        [MenuItem("Tools/NarutoVR/Configure Quest Environment")]
        public static void ConfigureAndValidate()
        {
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel32;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });
            PlayerSettings.colorSpace = ColorSpace.Linear;

            if (!EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey,
                    out XRGeneralSettingsPerBuildTarget settings))
            {
                Directory.CreateDirectory("Assets/XR");
                AssetDatabase.Refresh();
                settings = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                AssetDatabase.CreateAsset(settings, "Assets/XR/XRGeneralSettingsPerBuildTarget.asset");
                EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, settings, true);
            }

            ConfigurePlatform(settings, BuildTargetGroup.Android);
            ConfigurePlatform(settings, BuildTargetGroup.Standalone);
            EditorUtility.SetDirty(settings);

            var meta = OVRProjectConfig.CachedProjectConfig;
            meta.handTrackingSupport = OVRProjectConfig.HandTrackingSupport.ControllersAndHands;
            meta.handTrackingFrequency = OVRProjectConfig.HandTrackingFrequency.HIGH;
            meta.targetDeviceTypes = new() { OVRProjectConfig.DeviceType.Quest3, OVRProjectConfig.DeviceType.Quest3S };
            OVRProjectConfig.CommitProjectConfig(meta);
            AssetDatabase.SaveAssets();
            Validate();
        }

        private static void ConfigurePlatform(XRGeneralSettingsPerBuildTarget settings, BuildTargetGroup group)
        {
            if (!settings.HasManagerSettingsForBuildTarget(group))
                settings.CreateDefaultManagerSettingsForBuildTarget(group);
            var general = settings.SettingsForBuildTarget(group);
            if (!XRPackageMetadataStore.AssignLoader(general.Manager, "UnityEngine.XR.OpenXR.OpenXRLoader", group))
                throw new InvalidOperationException($"Could not assign OpenXR loader for {group}.");
            general.InitManagerOnStart = true;
            EditorUtility.SetDirty(general);
            EditorUtility.SetDirty(general.Manager);

            FeatureHelpers.RefreshFeatures(group);
            OpenXRFeatureSetManager.InitializeFeatureSets();
            var metaSet = OpenXRFeatureSetManager.GetFeatureSetWithId(group, "com.meta.openxr.featureset.metaxr");
            if (metaSet == null) throw new InvalidOperationException($"Meta XR feature set missing for {group}.");
            metaSet.isEnabled = true;
            OpenXRFeatureSetManager.SetFeaturesFromEnabledFeatureSets(group);
            var openXR = OpenXRSettings.GetSettingsForBuildTargetGroup(group);
            foreach (var typeName in new[] { "MetaXRFeature", "HandTracking", "MetaHandTrackingAim", "OculusTouchControllerProfile" })
            {
                var feature = openXR.GetFeatures().FirstOrDefault(f => f != null && f.GetType().Name == typeName);
                if (feature == null) throw new InvalidOperationException($"{typeName} missing for {group}.");
                feature.enabled = true;
                EditorUtility.SetDirty(feature);
            }
            openXR.renderMode = OpenXRSettings.RenderMode.SinglePassInstanced;
            EditorUtility.SetDirty(openXR);
        }

        [Serializable]
        private sealed class ValidationReport
        {
            public string utc;
            public string unityVersion;
            public string nativePluginVersion;
            public string backend;
            public string architecture;
            public int minSdk;
            public string[] graphics;
            public string handSupport;
            public string handFrequency;
            public string[] androidFeatures;
            public bool androidLoader;
            public bool standaloneLoader;
            public bool passed;
        }

        [MenuItem("Tools/NarutoVR/Validate Quest Environment")]
        public static void Validate()
        {
            var android = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Android);
            var standalone = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Standalone);
            var xr = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            var meta = OVRProjectConfig.CachedProjectConfig;
            var report = new ValidationReport
            {
                utc = DateTime.UtcNow.ToString("O"), unityVersion = Application.unityVersion,
                backend = PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android).ToString(),
                architecture = PlayerSettings.Android.targetArchitectures.ToString(),
                minSdk = (int)PlayerSettings.Android.minSdkVersion,
                graphics = PlayerSettings.GetGraphicsAPIs(BuildTarget.Android).Select(x => x.ToString()).ToArray(),
                handSupport = meta.handTrackingSupport.ToString(), handFrequency = meta.handTrackingFrequency.ToString(),
                androidFeatures = xr.GetFeatures().Where(f => f != null && f.enabled).Select(f => f.GetType().Name).ToArray(),
                androidLoader = android != null && android.InitManagerOnStart && android.Manager.activeLoaders.Any(l => l is OpenXRLoader),
                standaloneLoader = standalone != null && standalone.InitManagerOnStart && standalone.Manager.activeLoaders.Any(l => l is OpenXRLoader)
            };
            // This intentionally exercises the native initialization that crashed in the previous project.
            report.nativePluginVersion = OVRPlugin.version.ToString();
            report.passed = report.backend == "IL2CPP" && report.architecture == "ARM64" && report.minSdk >= 32
                && report.androidLoader && report.standaloneLoader && report.handSupport == "ControllersAndHands"
                && report.handFrequency == "HIGH" && report.nativePluginVersion != "0.0.0"
                && new[] { "MetaXRFeature", "HandTracking", "MetaHandTrackingAim", "OculusTouchControllerProfile" }
                    .All(report.androidFeatures.Contains);
            Directory.CreateDirectory("Logs");
            File.WriteAllText(ReportPath, JsonUtility.ToJson(report, true));
            if (!report.passed) throw new InvalidOperationException("Quest environment validation failed. See " + ReportPath);
            Debug.Log("[QuestEnvironment] Configuration and native plugin check passed. Hardware tracking is not tested.");
        }
    }
}
