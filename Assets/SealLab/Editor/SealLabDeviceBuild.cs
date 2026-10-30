using System;
using System.IO;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace NarutoVR.Editor
{
    [InitializeOnLoad]
    public static class SealLabDeviceBuild
    {
        private const string RequestPath = "Logs/seal-device-build.request";
        private const string ResultPath = "Logs/seal-device-build-result.json";
        private const string ScenePath = "Assets/SealLab/Scenes/SealLab.unity";
        private const string OutputPath = "Builds/Quest/NarutoVR-HandsProbe.apk";

        private static double _lastPollTime;
        private static bool _isBuilding;

        static SealLabDeviceBuild()
        {
            EditorApplication.update += OnEditorUpdate;
        }

        [MenuItem("Tools/NarutoVR/Build Quest Hand Probe")]
        public static void RequestBuild()
        {
            try
            {
                EnsureDirectory(RequestPath);
                File.WriteAllText(RequestPath, DateTime.UtcNow.ToString("o"));
                Debug.Log($"[SealLabDeviceBuild] Build request written to {RequestPath}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SealLabDeviceBuild] Failed to write build request: {ex.Message}");
            }
        }

        private static void OnEditorUpdate()
        {
            if (_isBuilding)
            {
                return;
            }

            // Wait if compiling, updating, or playing
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            // Poll once per second
            double currentTime = EditorApplication.timeSinceStartup;
            if (currentTime - _lastPollTime < 1.0)
            {
                return;
            }
            _lastPollTime = currentTime;

            if (!File.Exists(RequestPath))
            {
                return;
            }

            ExecuteBuild();
        }

        private static void ExecuteBuild()
        {
            if (_isBuilding)
            {
                return;
            }
            _isBuilding = true;

            try
            {
                // A failure to consume the marker must abort, otherwise the next poll rebuilds.
                File.Delete(RequestPath);

                // Require EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android
                if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                {
                    string err = $"Active build target is {EditorUserBuildSettings.activeBuildTarget}; BuildTarget.Android is required.";
                    Debug.LogError($"[SealLabDeviceBuild] {err}");
                    WriteResult(new BuildResultData
                    {
                        utc = DateTime.UtcNow.ToString("o"),
                        passed = false,
                        outputPath = OutputPath,
                        result = "Failed",
                        totalErrors = 1,
                        totalWarnings = 0,
                        totalSize = 0,
                        error = err
                    });
                    return;
                }

                EnsureDirectory(OutputPath);
                EnsureDirectory(ResultPath);

                BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = OutputPath,
                    target = BuildTarget.Android,
                    options = BuildOptions.Development
                };

                Debug.Log($"[SealLabDeviceBuild] Initiating BuildPlayer for {OutputPath}...");
                // Separate probe identity avoids overwriting an existing template-named app.
                string originalId = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
                string originalGradleHome = Environment.GetEnvironmentVariable("GRADLE_USER_HOME");
                string originalUnityGradleHome = AndroidExternalToolsSettings.Gradle.userHomePath;
                BuildReport report;
                try
                {
                    string gradleHome = Path.GetFullPath(".build-cache/gradle");
                    Directory.CreateDirectory(gradleHome);
                    Environment.SetEnvironmentVariable("GRADLE_USER_HOME", gradleHome);
                    AndroidExternalToolsSettings.Gradle.userHomePath = gradleHome;
                    PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.narutovr.handprobe");
                    report = BuildPipeline.BuildPlayer(buildPlayerOptions);
                }
                finally
                {
                    Environment.SetEnvironmentVariable("GRADLE_USER_HOME", originalGradleHome);
                    AndroidExternalToolsSettings.Gradle.userHomePath = originalUnityGradleHome;
                    PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, originalId);
                    AssetDatabase.SaveAssets();
                }
                BuildSummary summary = report.summary;

                bool passed = summary.result == BuildResult.Succeeded;

                WriteResult(new BuildResultData
                {
                    utc = DateTime.UtcNow.ToString("o"),
                    passed = passed,
                    outputPath = OutputPath,
                    result = summary.result.ToString(),
                    totalErrors = summary.totalErrors,
                    totalWarnings = summary.totalWarnings,
                    totalSize = summary.totalSize,
                    error = passed ? "" : $"Build ended with result: {summary.result}"
                });

                Debug.Log($"[SealLabDeviceBuild] Build completed. Result: {summary.result}, Passed: {passed}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SealLabDeviceBuild] Exception during build: {ex}");
                WriteResult(new BuildResultData
                {
                    utc = DateTime.UtcNow.ToString("o"),
                    passed = false,
                    outputPath = OutputPath,
                    result = "Exception",
                    totalErrors = 1,
                    totalWarnings = 0,
                    totalSize = 0,
                    error = ex.ToString()
                });
            }
            finally
            {
                _isBuilding = false;
            }
        }

        private static void EnsureDirectory(string filePath)
        {
            string dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
        }

        private static void WriteResult(BuildResultData data)
        {
            try
            {
                EnsureDirectory(ResultPath);
                string json = EditorJsonUtility.ToJson(data, true);
                File.WriteAllText(ResultPath, json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SealLabDeviceBuild] Failed to write build result: {ex.Message}");
            }
        }

        [Serializable]
        public class BuildResultData
        {
            public string utc;
            public bool passed;
            public string outputPath;
            public string result;
            public int totalErrors;
            public int totalWarnings;
            public ulong totalSize;
            public string error;
        }
    }
}

