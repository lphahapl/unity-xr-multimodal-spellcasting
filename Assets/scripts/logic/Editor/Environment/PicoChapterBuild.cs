using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NarutoVR.SceneArt;
using NarutoVR.SealCombat.Rpg;
using Unity.XR.OpenXR.Features.PICOSupport;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR;
using UnityEngine.XR.OpenXR;

namespace NarutoVR.Editor
{
    public static class PicoChapterBuild
    {
        const string Courtyard = "Assets/Scene/Chapter01_Courtyard.unity";
        const string Chapter02 = "Assets/Scene/Chapter02_SuspendedPath.unity";
        const string Output = "Builds/Pico/NarutoVR-PICO-Chapters.apk";

        [MenuItem("Tools/NarutoVR/PICO/Build Chapters Test APK")]
        public static void Build()
        {
            Validate();
            Directory.CreateDirectory(Path.GetDirectoryName(Output));
            EditorUserBuildSettings.buildAppBundle = false;
            EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = EnabledScenes(),
                locationPathName = Output,
                target = BuildTarget.Android,
                options = BuildOptions.Development
            });
            Require(report.summary.result == BuildResult.Succeeded && File.Exists(Output), "Unity did not produce a chapter APK.");
            Debug.Log($"[PICO Chapters] APK built at {Output}, {report.summary.totalSize} bytes.");
        }

        public static void Validate()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode && EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android,
                "The chapter build requires Android Edit Mode.");
            Require(OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android)?.GetFeature<PICOFeature>()?.enabled == true,
                "PICO OpenXR must be enabled before building the chapter test.");
            var displays = new List<XRDisplaySubsystem>();
            SubsystemManager.GetSubsystems(displays);
            Require(displays.All(display => !display.running), "Close the active editor XR session before building.");

            var scenes = EnabledScenes();
            Require(scenes.Length > 1 && scenes[0] == Courtyard && scenes.Contains(Chapter02),
                "The chapter APK must launch the courtyard and include the second chapter.");
            var shaderErrors = AssetDatabase.GetDependencies(scenes, true)
                .Where(path => path.EndsWith(".shader", StringComparison.OrdinalIgnoreCase))
                .Where(path => AssetDatabase.LoadAssetAtPath<Shader>(path) is Shader shader && ShaderUtil.ShaderHasError(shader))
                .ToArray();
            Require(shaderErrors.Length == 0, "Resolve chapter shader errors before building: " + string.Join(", ", shaderErrors));
            for (int i = 0; i < SceneManager.sceneCount; ++i)
                Require(!SceneManager.GetSceneAt(i).isDirty, "Save open scenes before validating the chapter APK.");

            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                foreach (var path in scenes)
                {
                    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    var roots = scene.GetRootGameObjects();
                    var players = InScene<LinearRpgController>(roots).Where(player => player.isActiveAndEnabled).ToArray();
                    Require(players.Length == 1, $"{path} requires one active chapter player.");
                    var player = players[0];
                    var lab = player.handTracking ? player.handTracking : player.skills ? player.skills.lab : null;
                    Require(lab && lab.isActiveAndEnabled && lab.recordingOrigin && lab.xrHandsSource && lab.xrHandsSource.IsConfigured,
                        $"{path} has no valid XR Hands sources on its player.");
                    Require(player.locomotion && player.locomotion.head, $"{path} has no player tracking head.");

                    if (path == Courtyard)
                        Require(InScene<SceneTravelGate>(roots).Any(gate => gate.LoadEnabled && gate.DestinationScene == Chapter02),
                            "The courtyard gate does not load the second chapter.");

                    var pickupCount = InScene<MonoBehaviour>(roots).Count(behaviour => behaviour is IHandPickupSource);
                    Require(pickupCount > 0, $"{path} has no chapter pickup providers.");
                    Debug.Log($"[PICO Chapters] Verified {path}, XR Hands, gameplay head, {pickupCount} pickup providers.");
                }
            }
            finally
            {
                if (setup.Length > 0 && setup.All(scene => !string.IsNullOrEmpty(scene.path)))
                    EditorSceneManager.RestoreSceneManagerSetup(setup);
                else
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        static T[] InScene<T>(GameObject[] roots) where T : Component =>
            roots.SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();

        static string[] EnabledScenes() => EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();

        static void Require(bool condition, string message)
        {
            if (!condition) throw new BuildFailedException(message);
        }
    }
}
