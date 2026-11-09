// Copy to the owned clone's Editor tools; execute SkyCameraAcceptance.ValidateThenBuild.
// Keeps QuestExteriorBuild.Run's owner/proof/hash/platform/restoration guards intact.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SkyCameraAcceptance
{
    const string Clone = "D:/Temp/NarutoVR-Exterior-20261108";
    const string Main = "D:/UnityProjects/NarutoVR";
    const string Candidate = "Assets/Scene/QA_WorldExteriorMerged.unity";
    const string SkyGuid = "018fefc9becbb9f48aed86db0c87d04f";
    const string SkyShader = "NarutoVR/Environment/青灰薄云天空";
    const string ReportPath = Main + "/Logs/exterior-world-lod-20261109/sky-camera-fix/unity-acceptance.json";
    [Serializable] public sealed class Check { public string name, detail; public bool passed; }
    [Serializable] public sealed class Report
    {
        public string time, project, unityVersion, scene = Candidate, error;
        public string cameraPath, rigPath, clearFlags, skyMaterialPath, skyMaterialGuid, shader;
        public int clearFlagsValue, mainCameraCount, thinCloudPass;
        public bool passed, shaderSupported, buildDelegated, automaticChecksOnly = true;
        public string[] skyKeywords;
        public Check[] checks;
        public string scope = "Read-only candidate camera/sky validation before existing guarded Quest build. " +
            "Original 168 script-block preservation and flags-only scene diff belong to parent proof. " +
            "No headset visual/performance approval; build outcome is recorded by QuestExteriorBuild.";
    }
    static readonly List<Check> Checks = new List<Check>();

    public static void ValidateThenBuild()
    {
        Checks.Clear();
        var report = new Report { time = DateTimeOffset.Now.ToString("o"), unityVersion = Application.unityVersion,
            project = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/') };
        Scene candidate = default(Scene), previousActive = default(Scene);
        bool openedHere = false;
        try
        {
            Need(Application.isBatchMode && Application.platform == RuntimePlatform.WindowsEditor, "Windows batch Editor required");
            Need(string.Equals(report.project.TrimEnd('/'), Clone, StringComparison.OrdinalIgnoreCase), "Exact owned clone required", report.project);
            Need(Environment.GetEnvironmentVariable("EXTERIOR_QUEST_OWNER") == "EXTERIOR-WORLD-LOD-20261109", "Existing build owner token required");
            Need(!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("EXTERIOR_QUEST_PROOF")) &&
                !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("EXTERIOR_QUEST_PROOF_SHA256")), "Existing reviewed build proof environment required");
            Need(!EditorApplication.isCompiling && !EditorApplication.isUpdating && !EditorApplication.isPlayingOrWillChangePlaymode,
                "Idle Editor outside Play Mode required");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                Need(!SceneManager.GetSceneAt(i).isDirty, "Do not discard dirty scene", SceneManager.GetSceneAt(i).path);
            Need(File.Exists(Path.Combine(Clone, Candidate)), "Fixed candidate scene exists");
            previousActive = SceneManager.GetActiveScene();
            candidate = SceneManager.GetSceneByPath(Candidate);
            if (!candidate.IsValid() || !candidate.isLoaded)
            { candidate = EditorSceneManager.OpenScene(Candidate, OpenSceneMode.Additive); openedHere = true; }
            Need(candidate.IsValid() && candidate.isLoaded && candidate.path == Candidate, "Fixed candidate loaded");
            Need(SceneManager.SetActiveScene(candidate), "Candidate active for scene RenderSettings");

            var roots = candidate.GetRootGameObjects();
            var controllers = roots.SelectMany(x => x.GetComponentsInChildren<MonoBehaviour>(true))
                .Where(x => x && x.GetType().FullName == "NarutoVR.SealCombat.Rpg.RpgLocomotionController").ToArray();
            Need(controllers.Length == 1, "Exactly one candidate RpgLocomotionController", controllers.Length.ToString());
            var controller = controllers[0];
            var rig = controller.GetType().GetField("rigRoot").GetValue(controller) as Transform;
            var head = controller.GetType().GetField("head").GetValue(controller) as Transform;
            Need(rig && head && rig.gameObject.scene == candidate && head.gameObject.scene == candidate &&
                head != rig && head.IsChildOf(rig), "Explicit rigRoot/head belong to candidate hierarchy");
            var camera = head.GetComponent<Camera>();
            Need(camera, "Explicit head has Camera");
            report.rigPath = Hierarchy(rig); report.cameraPath = Hierarchy(camera.transform);
            var mainCameras = roots.SelectMany(x => x.GetComponentsInChildren<Camera>(true)).Where(x => x.CompareTag("MainCamera")).ToArray();
            report.mainCameraCount = mainCameras.Length;
            Need(mainCameras.Length == 1 && mainCameras[0] == camera, "Unique MainCamera is explicit tracked head", report.cameraPath);
            Need(camera.enabled && camera.gameObject.activeInHierarchy, "Tracked MainCamera enabled and active");
            report.clearFlags = camera.clearFlags.ToString(); report.clearFlagsValue = (int)camera.clearFlags;
            Need(camera.clearFlags == CameraClearFlags.Skybox, "Tracked MainCamera draws Skybox", report.clearFlags);

            var sky = RenderSettings.skybox;
            Need(sky, "Candidate RenderSettings.skybox assigned");
            report.skyMaterialPath = AssetDatabase.GetAssetPath(sky);
            report.skyMaterialGuid = AssetDatabase.AssetPathToGUID(report.skyMaterialPath);
            var mainText = File.ReadAllText(Main + "/Assets/Scene/Chapter01_Awakening.unity");
            var mainSky = Regex.Match(mainText, @"(?m)^\s*m_SkyboxMaterial:\s*\{[^\r\n}]*guid:\s*([0-9a-fA-F]{32})");
            Need(mainSky.Success && mainSky.Groups[1].Value == SkyGuid, "Current main scene still binds accepted sky GUID");
            Need(report.skyMaterialGuid == SkyGuid, "Candidate shares current main sky material GUID", report.skyMaterialGuid);
            var cameraSky = camera.GetComponent<Skybox>();
            Need(!cameraSky || !cameraSky.enabled || !cameraSky.material || cameraSky.material == sky,
                "No different per-camera sky material overrides accepted sky");
            Need(sky.shader, "Sky shader assigned");
            report.shader = sky.shader.name; report.shaderSupported = sky.shader.isSupported;
            report.thinCloudPass = sky.FindPass("ThinCloudSky"); report.skyKeywords = sky.shaderKeywords;
            Need(report.shader == SkyShader && report.thinCloudPass >= 0, "Existing ThinCloudSky shader/pass retained", report.shader);
            Need(report.shaderSupported, "Sky shader supported on current Editor build context");
            Need(!candidate.isDirty, "Read-only validation leaves candidate clean");
            report.passed = true;
        }
        catch (Exception e) { report.error = e.ToString(); report.passed = false; }
        finally
        {
            try
            {
                if (previousActive.IsValid() && previousActive.isLoaded)
                    Need(SceneManager.SetActiveScene(previousActive), "Previous active scene restored");
                if (openedHere && candidate.IsValid() && candidate.isLoaded)
                    Need(EditorSceneManager.CloseScene(candidate, true), "Validation-only additive scene closed without saving");
            }
            catch (Exception e) { report.error += "\nScene restoration: " + e; report.passed = false; }
            report.checks = Checks.ToArray();
            report.buildDelegated = report.passed;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
                File.WriteAllText(ReportPath, JsonUtility.ToJson(report, true));
            }
            catch (Exception e) { report.error += "\nReport write: " + e; report.passed = false; }
        }
        if (!report.passed) { Debug.LogError("Sky camera acceptance failed: " + report.error); EditorApplication.Exit(1); return; }
        // Do not replace, bypass or alter the existing build wrapper's proof/owner/hash guards.
        try { QuestExteriorBuild.Run(); }
        catch (Exception e) { Debug.LogError("Guarded Quest build raised an exception: " + e); EditorApplication.Exit(1); }
    }
    static void Need(bool passed, string name, string detail = "")
    { Checks.Add(new Check { name = name, detail = detail, passed = passed }); if (!passed) throw new InvalidOperationException(name + ": " + detail); }
    static string Hierarchy(Transform item)
    { var parts = new List<string>(); for (; item; item = item.parent) parts.Add(item.name); parts.Reverse(); return string.Join("/", parts); }
}
