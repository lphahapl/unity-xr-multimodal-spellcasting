// Isolated Windows Unity batch entry: QuestExteriorBuild.Run. Never installs or starts the APK.
// Required env: EXTERIOR_QUEST_OWNER=EXTERIOR-WORLD-LOD-20261109,
// EXTERIOR_QUEST_PROOF=<absolute merge-proof.json>, EXTERIOR_QUEST_PROOF_SHA256=<reviewed file hash>.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.Win32.SafeHandles;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

public static class QuestExteriorBuild
{
    const string Main = "D:/UnityProjects/NarutoVR";
    const string Clone = "D:/Temp/NarutoVR-Exterior-20261108";
    const string Owner = "EXTERIOR-WORLD-LOD-20261109";
    const string Marker = "Owned by root exterior expansion task 20261108. Physical copies; no hardlinks.";
    const string ScenePath = "Assets/Scene/Chapter01_Awakening.unity";
    const string Candidate = "Assets/Scene/QA_WorldExteriorMerged.unity";
    const string Prefab = "Assets/Art/Environment/Opening/MountainPolish/EnvironmentPolish.prefab";
    const string PackageId = "com.kaogonglvdu.chapter01.exteriorperf20261109";
    [Serializable] public class Proof
    {
        public string baseSha256, candidateSha256, prefabSHA256, candidate, sourceScene, modifiedPrefab;
        public bool originalMonoBehavioursByteIdentical;
        public int originalMonoBehaviours;
    }
    [Serializable] public class FileRecord { public string path, backupPath, beforeSha256, afterSha256; public long beforeBytes, backupBytes; public bool restored; }
    [Serializable] public class SceneSetupState { public string path; public bool loaded, active; }
    [Serializable] public class EditorSceneState { public string path; public bool loaded, active, dirty; public int rootCount; }
    [Serializable] public class HashRecord { public string path, sha256; }
    [Serializable] public class Message { public string type, text; }
    [Serializable] public class Step { public string name; public int depth; public double durationMs; public Message[] messages; }
    [Serializable] public class BuildFileRecord { public string path, role; public ulong size; }
    [Serializable] public class BuildSceneState { public string path; public bool enabled; }
    [Serializable] public class PlayerState
    {
        public string applicationIdentifier, productName;
        public bool development, connectProfiler, allowDebugging, buildAppBundle, exportAsProject;
        public BuildSceneState[] editorBuildScenes;
    }
    [Serializable] public class Report
    {
        public string time, project, unityVersion, ownerMarker, ownerMarkerSha256, proofPath, proofSha256;
        public string outputDirectory, apk, apkSha256, expectedPackageId = PackageId, actualPackageId;
        public string actualVersionCode, launchableActivity, aapt, aaptOutput, aaptError;
        public string baseSha256, candidateSha256, prefabSha256, stagedSceneSha256, stagedSceneMetaSha256;
        public string mainSceneBeforeSha256, mainSceneAfterSha256, candidateAfterSha256, prefabAfterSha256;
        public string activeTarget, backend, architecture, colorSpace, qualityName, pipelineName;
        public string[] graphicsAPIs, builtScenes, untrackedConfigurationFiles, changedProtectedInputs;
        public string buildResult, buildGuid, buildOptions, buildPlatform, error;
        public int buildErrors, buildWarnings; public ulong buildBytes; public double buildSeconds;
        public bool passed, buildSucceeded, settingsRestored, sceneAndMetaRestored, mainSceneUnchanged;
        public bool installedOrLaunched, permissionsOrSigningOverridden, originalScenesRestored;
        public bool sceneSetupExactMatch, sceneRestoreVerified;
        public string sceneRestoreMode;
        public string gradleHome, gradleEnvironmentBefore, gradleApiBefore;
        public string gradleEnvironmentDuring, gradleApiDuring, gradleEnvironmentAfter, gradleApiAfter;
        public bool gradleStateCaptured, gradleEnvironmentOriginallyUnset, gradleTemporaryValuesVerified, gradleRestored;
        public SceneSetupState[] sceneSetupBefore, sceneSetupAfter;
        public EditorSceneState[] editorScenesBefore, editorScenesAfter;
        public bool automaticBuildChecksOnly = true;
        public PlayerState settingsBefore, settingsAfter;
        public FileRecord[] backupAndRestoration;
        public HashRecord[] protectedAssetInputs;
        public Step[] buildSteps; public BuildFileRecord[] buildFiles;
        public Message[] logs;
        public string limitation = "Release APK build and manifest/hash/restoration evidence only. No device install, launch, refresh-rate request, Quest performance, full playthrough, input or visual acceptance. Build callbacks are allowed to fail; build errors are not silently suppressed.";
    }
    static readonly List<Message> Logs = new List<Message>();
    static readonly List<FileRecord> Files = new List<FileRecord>();
    static bool recording;

    public static void Run()
    {
        var r = new Report { time = DateTimeOffset.Now.ToString("o"), unityVersion = Application.unityVersion,
            project = Full(Path.Combine(Application.dataPath, "..")), activeTarget = EditorUserBuildSettings.activeBuildTarget.ToString() };
        string outputRoot = Environment.GetEnvironmentVariable("EXTERIOR_QUEST_OUT");
        if (string.IsNullOrWhiteSpace(outputRoot)) outputRoot = Main + "/Logs/exterior-world-lod-20261109/quest-build";
        r.outputDirectory = Full(Path.Combine(outputRoot, "build-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff")));
        Files.Clear(); Logs.Clear();
        bool mayRestore = false;
        SceneSetup[] originalScenes = null;
        Dictionary<string, string> protectedInputs = null;
        string[] configRoots = { "ProjectSettings", "Assets/Settings", "Assets/XR",
            "Assets/Oculus/OculusProjectConfig.asset", "Assets/Oculus/OculusProjectConfig.asset.meta" };
        try
        {
            Need(Application.isBatchMode && Application.platform == RuntimePlatform.WindowsEditor, "Windows batch process required");
            Need(EqualPath(r.project, Clone) && !EqualPath(r.project, Main), "Exact owned clone required: " + Clone);
            Need(Environment.GetEnvironmentVariable("EXTERIOR_QUEST_OWNER") == Owner, "Explicit clone-owner token missing/wrong");
            Need(Path.IsPathRooted(outputRoot), "Output root must be absolute");
            Need(!Inside(r.outputDirectory, r.project + "/Assets") && !Inside(r.outputDirectory, Main + "/Assets"), "Output must stay outside both Assets trees");
            Need(!Directory.Exists(r.outputDirectory), "Refusing to reuse a build output directory");
            Directory.SetCurrentDirectory(r.project);
            NoReparse(r.project);
            string ownerFile = r.project + "/exterior-task-owner.txt";
            PhysicalFile(ownerFile);
            r.ownerMarker = File.ReadAllText(ownerFile).Trim(); r.ownerMarkerSha256 = Sha(ownerFile);
            Need(r.ownerMarker == Marker, "Unexpected clone ownership marker");
            Need(!EditorApplication.isCompiling && !EditorApplication.isUpdating && !EditorApplication.isPlayingOrWillChangePlaymode,
                "Clone must be idle and out of Play Mode");
            Need(EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android && BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android), "Android must already be active and installed");
            originalScenes = EditorSceneManager.GetSceneManagerSetup();
            Need(originalScenes != null, "Scene setup snapshot returned null");
            r.sceneSetupBefore = SetupState(originalScenes); r.editorScenesBefore = EditorScenes();
            for (int i = 0; i < SceneManager.sceneCount; i++) Need(!SceneManager.GetSceneAt(i).isDirty, "Refusing to discard a dirty loaded scene");
            if (originalScenes.Length == 0)
                Need(r.editorScenesBefore.All(s => !s.loaded || string.IsNullOrEmpty(s.path) && s.rootCount == 0),
                    "Empty setup snapshot does not preserve loaded scene content; refusing to discard it");
            else
            {
                Need(originalScenes.All(s => !string.IsNullOrWhiteSpace(s.path) && File.Exists(Owned(s.path))), "Named setup contains an empty/missing scene backup path");
                Need(originalScenes.Any(s => s.isLoaded && s.isActive), "Named setup has no loaded active scene to restore");
            }
            r.proofPath = Full(Required("EXTERIOR_QUEST_PROOF"));
            Need(Path.IsPathRooted(Required("EXTERIOR_QUEST_PROOF")), "Absolute proof path required");
            r.proofSha256 = Sha(r.proofPath);
            Need(HashEqual(r.proofSha256, Required("EXTERIOR_QUEST_PROOF_SHA256")), "Reviewed proof-file hash mismatch");
            var proof = JsonUtility.FromJson<Proof>(File.ReadAllText(r.proofPath));
            Need(proof != null && proof.originalMonoBehavioursByteIdentical && proof.originalMonoBehaviours == 168, "Proof does not preserve original script blocks");
            Need(EqualPath(proof.candidate, r.project + "/" + Candidate) && EqualPath(proof.sourceScene, Main + "/" + ScenePath), "Proof scene paths do not match task ownership");
            Need(proof.modifiedPrefab.Replace('\\', '/') == Prefab, "Proof prefab path mismatch");
            foreach (string path in new[] { ScenePath, ScenePath + ".meta", Candidate, Prefab, "ProjectSettings/ProjectSettings.asset", "ProjectSettings/EditorBuildSettings.asset" })
            { PhysicalFile(path); Need(new FileInfo(Owned(path)).Length > 0, "Critical scene/config input is empty: " + path); }
            r.baseSha256 = Sha(ScenePath); r.candidateSha256 = Sha(Candidate); r.prefabSha256 = Sha(Prefab);
            r.mainSceneBeforeSha256 = Sha(Main + "/" + ScenePath);
            Need(HashEqual(proof.baseSha256, r.baseSha256) && HashEqual(proof.baseSha256, r.mainSceneBeforeSha256), "Formal/base scene hash mismatch");
            Need(HashEqual(proof.candidateSha256, r.candidateSha256) && HashEqual(proof.prefabSHA256, r.prefabSha256), "Candidate or updated art prefab is not bound to proof");
            r.backend = PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android).ToString();
            r.architecture = PlayerSettings.Android.targetArchitectures.ToString();
            r.graphicsAPIs = PlayerSettings.GetGraphicsAPIs(BuildTarget.Android).Select(g => g.ToString()).ToArray();
            r.colorSpace = PlayerSettings.colorSpace.ToString();
            // Unity 6000.3 has no public GetActiveQualityLevelForPlatform API.
            // Read the actual serialized platform-default map, without changing the editor quality.
            string qualityText = File.ReadAllText("ProjectSettings/QualitySettings.asset");
            int mapAt = qualityText.IndexOf("  m_PerPlatformDefaultQuality:", StringComparison.Ordinal);
            Need(mapAt >= 0, "Quality platform-default map missing");
            var qualityMatch = Regex.Match(qualityText.Substring(mapAt), @"(?m)^    Android: ([0-9]+)\s*$");
            Need(qualityMatch.Success, "Explicit Android default quality missing");
            int androidQuality = int.Parse(qualityMatch.Groups[1].Value);
            Need(androidQuality >= 0 && androidQuality < QualitySettings.names.Length, "No explicit Android quality level");
            r.qualityName = QualitySettings.names[androidQuality];
            var pipeline = QualitySettings.GetRenderPipelineAssetAt(androidQuality);
            r.pipelineName = pipeline ? pipeline.name : (UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline ? UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline.name : "none");
            Need(r.backend == "IL2CPP" && PlayerSettings.Android.targetArchitectures == AndroidArchitecture.ARM64 && r.graphicsAPIs.Contains("Vulkan"), "Expected Android IL2CPP/ARM64/Vulkan configuration is not present; tool does not repair it");
            Need(r.qualityName == "Mobile" && r.pipelineName == "Mobile_RPAsset", "Expected Mobile quality/pipeline is not selected for Android");
            Directory.CreateDirectory(r.outputDirectory);
            File.Copy(r.proofPath, r.outputDirectory + "/input-merge-proof.json", false);
            r.settingsBefore = ReadSettings();
            // Backup all project settings plus relevant XR/URP/Meta resources before any mutation.
            foreach (string path in ConfigFiles(configRoots).Concat(new[] { ScenePath, ScenePath + ".meta" }).Distinct().OrderBy(p => p, StringComparer.Ordinal))
            {
                PhysicalFile(path);
                var f = new FileRecord { path = path, backupPath = r.outputDirectory + "/backup/" + path,
                    beforeSha256 = Sha(Owned(path)), beforeBytes = new FileInfo(Owned(path)).Length };
                Directory.CreateDirectory(Path.GetDirectoryName(f.backupPath)); File.Copy(Owned(path), f.backupPath, false);
                f.backupBytes = new FileInfo(f.backupPath).Length;
                Need(f.backupBytes == f.beforeBytes && Sha(f.backupPath) == f.beforeSha256, "Backup size/hash verification failed: " + path);
                Files.Add(f);
            }
            protectedInputs = AssetDatabase.GetDependencies(Candidate, true).Concat(new[] { Candidate, Candidate + ".meta", Prefab })
                .Where(p => p.StartsWith("Assets/", StringComparison.Ordinal) && p != ScenePath && File.Exists(p))
                .Distinct().ToDictionary(p => p, Sha);
            r.protectedAssetInputs = protectedInputs.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => new HashRecord { path = p.Key, sha256 = p.Value }).ToArray();
            r.gradleHome = Owned(".build-cache/gradle");
            r.gradleEnvironmentBefore = Environment.GetEnvironmentVariable("GRADLE_USER_HOME");
            r.gradleEnvironmentOriginallyUnset = r.gradleEnvironmentBefore == null;
            r.gradleApiBefore = AndroidExternalToolsSettings.Gradle.userHomePath;
            r.gradleStateCaptured = true;
            File.WriteAllText(r.outputDirectory + "/recovery-before-build.json", JsonUtility.ToJson(new Report {
                project = r.project, proofPath = r.proofPath, proofSha256 = r.proofSha256,
                baseSha256 = r.baseSha256, candidateSha256 = r.candidateSha256,
                settingsBefore = r.settingsBefore, backupAndRestoration = Files.ToArray(),
                sceneSetupBefore = r.sceneSetupBefore, editorScenesBefore = r.editorScenesBefore,
                gradleHome = r.gradleHome, gradleEnvironmentBefore = r.gradleEnvironmentBefore,
                gradleEnvironmentOriginallyUnset = r.gradleEnvironmentOriginallyUnset,
                gradleApiBefore = r.gradleApiBefore, gradleStateCaptured = r.gradleStateCaptured
            }, true));
            // From here onward finally restores settings and byte backups, including failure paths.
            mayRestore = true;
            UseOwnedAsciiGradleHome(r);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, PackageId);
            PlayerSettings.productName = "首章外景性能测试";
            EditorUserBuildSettings.development = false; EditorUserBuildSettings.connectProfiler = false;
            EditorUserBuildSettings.allowDebugging = false; EditorUserBuildSettings.buildAppBundle = false;
            EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
            AssetDatabase.SaveAssets();
            File.WriteAllBytes(Owned(ScenePath), File.ReadAllBytes(Owned(Candidate)));
            AssetDatabase.ImportAsset(ScenePath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            r.stagedSceneSha256 = Sha(ScenePath); r.stagedSceneMetaSha256 = Sha(ScenePath + ".meta");
            Need(HashEqual(r.stagedSceneSha256, proof.candidateSha256), "Staged formal-name scene differs from candidate");
            Need(r.stagedSceneMetaSha256 == Files.Single(f => f.path == ScenePath + ".meta").beforeSha256, "Formal scene GUID/meta changed while staging");
            Need(Sha(r.proofPath) == r.proofSha256 && Sha(Candidate) == r.candidateSha256 && Sha(Prefab) == r.prefabSha256, "Build inputs changed after evidence capture");
            r.apk = r.outputDirectory + "/NarutoVR-Chapter01-ExteriorPerf-20261109.apk";
            Need(!File.Exists(r.apk), "APK output already exists");
            r.builtScenes = new[] { ScenePath };
            Application.logMessageReceived += Log; recording = true;
            BuildReport built;
            try
            {
                built = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                    scenes = r.builtScenes, target = BuildTarget.Android, locationPathName = r.apk, options = BuildOptions.None
                });
            }
            finally { recording = false; Application.logMessageReceived -= Log; }
            Need(built != null, "BuildPlayer returned no report");
            CaptureBuildReport(built, r);
            File.WriteAllText(r.outputDirectory + "/build-report.json", JsonUtility.ToJson(r, true));
            if (File.Exists(Owned("Library/LastBuild.buildreport"))) File.Copy(Owned("Library/LastBuild.buildreport"), r.outputDirectory + "/LastBuild.buildreport", false);
            Need(r.buildSucceeded && r.buildErrors == 0, "Android BuildPlayer failed: " + r.buildResult);
            Need(!Logs.Any(m => m.type == "Error" || m.type == "Exception" || m.type == "Assert"), "Build emitted errors; see captured build log");
            Need(File.Exists(r.apk) && new FileInfo(r.apk).Length > 0, "Successful build produced no APK");
            r.apkSha256 = Sha(r.apk);
            InspectApk(r);
        }
        catch (Exception e) { r.error = e.ToString(); }
        finally
        {
            recording = false; Application.logMessageReceived -= Log;
            if (mayRestore)
            {
                // Restore each channel independently even if setting the temporary API path failed.
                if (r.gradleStateCaptured)
                {
                    try { Environment.SetEnvironmentVariable("GRADLE_USER_HOME", r.gradleEnvironmentOriginallyUnset ? null : r.gradleEnvironmentBefore); }
                    catch (Exception e) { AddFailure(r, "Gradle environment restoration", e); }
                    try { AndroidExternalToolsSettings.Gradle.userHomePath = r.gradleApiBefore; }
                    catch (Exception e) { AddFailure(r, "Unity Gradle API restoration", e); }
                    try
                    {
                        r.gradleEnvironmentAfter = Environment.GetEnvironmentVariable("GRADLE_USER_HOME");
                        r.gradleApiAfter = AndroidExternalToolsSettings.Gradle.userHomePath;
                        r.gradleRestored = string.Equals(r.gradleEnvironmentBefore, r.gradleEnvironmentAfter, StringComparison.Ordinal)
                            && string.Equals(r.gradleApiBefore, r.gradleApiAfter, StringComparison.Ordinal);
                        Need(r.gradleRestored, "Original Gradle environment/API values did not read back exactly");
                    }
                    catch (Exception e) { AddFailure(r, "Verify Gradle restoration", e); }
                }
                try { RestoreSettings(r.settingsBefore); AssetDatabase.SaveAssets(); }
                catch (Exception e) { AddFailure(r, "Settings API restoration", e); }
                // Do not let one failed restore prevent attempts to restore the other files.
                foreach (var f in Files)
                {
                    try
                    {
                        Need(Sha(f.backupPath) == f.beforeSha256, "Recovery backup hash mismatch: " + f.path);
                        if (!File.Exists(Owned(f.path)) || Sha(Owned(f.path)) != f.beforeSha256) File.Copy(f.backupPath, Owned(f.path), true);
                    }
                    catch (Exception e) { AddFailure(r, "Byte restore " + f.path, e); }
                }
                try
                {
                    AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                    if (originalScenes != null)
                    {
                        if (originalScenes.Length == 0)
                        {
                            // RestoreSceneManagerSetup([]) throws "No loaded scene found" in cold batch.
                            // Represent that no-asset starting state with one clean, empty untitled scene.
                            // Exact setup equality is reported separately; this is not a fabricated exact restore.
                            r.sceneRestoreMode = "empty-start-recreated-clean-untitled";
                            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                        }
                        else
                        {
                            r.sceneRestoreMode = "restore-original-named-setup";
                            EditorSceneManager.RestoreSceneManagerSetup(originalScenes);
                        }
                        r.sceneSetupAfter = SetupState(EditorSceneManager.GetSceneManagerSetup());
                        r.editorScenesAfter = EditorScenes();
                        r.sceneSetupExactMatch = SameSetup(r.sceneSetupBefore, r.sceneSetupAfter);
                        r.originalScenesRestored = r.sceneSetupExactMatch; // Legacy flag now means exact equality.
                        r.sceneRestoreVerified = originalScenes.Length == 0
                            ? r.editorScenesAfter.Length == 1 && r.editorScenesAfter.All(s => s.loaded && s.active && !s.dirty && string.IsNullOrEmpty(s.path) && s.rootCount == 0)
                              && r.sceneSetupAfter.All(s => string.IsNullOrEmpty(s.path))
                            : r.sceneSetupExactMatch;
                        Need(r.sceneRestoreVerified, "Editor scene state did not satisfy its recorded restoration mode");
                    }
                }
                catch (Exception e) { AddFailure(r, "Restore scene/import state", e); }
                foreach (var f in Files)
                {
                    try { f.afterSha256 = Sha(Owned(f.path)); f.restored = f.afterSha256 == f.beforeSha256; }
                    catch (Exception e) { AddFailure(r, "Verify restored " + f.path, e); }
                }
                try
                {
                    r.settingsAfter = ReadSettings();
                    r.untrackedConfigurationFiles = ConfigFiles(configRoots).Except(Files.Select(f => f.path)).ToArray();
                    r.changedProtectedInputs = protectedInputs.Where(p => !File.Exists(Owned(p.Key)) || Sha(Owned(p.Key)) != p.Value).Select(p => p.Key).ToArray();
                    r.settingsRestored = Files.Where(f => f.path.StartsWith("ProjectSettings/") || f.path.StartsWith("Assets/") && f.path != ScenePath && f.path != ScenePath + ".meta").All(f => f.restored)
                        && JsonUtility.ToJson(r.settingsBefore) == JsonUtility.ToJson(r.settingsAfter) && r.untrackedConfigurationFiles.Length == 0;
                    r.sceneAndMetaRestored = Files.Where(f => f.path == ScenePath || f.path == ScenePath + ".meta").All(f => f.restored);
                    r.mainSceneAfterSha256 = Sha(Main + "/" + ScenePath);
                    r.mainSceneUnchanged = r.mainSceneAfterSha256 == r.mainSceneBeforeSha256;
                    r.candidateAfterSha256 = Sha(Owned(Candidate)); r.prefabAfterSha256 = Sha(Owned(Prefab));
                }
                catch (Exception e) { AddFailure(r, "Post-build evidence", e); }
            }
            r.backupAndRestoration = Files.ToArray(); r.logs = Logs.ToArray();
            r.passed = r.error == null && r.buildSucceeded && r.buildErrors == 0 && r.actualPackageId == PackageId
                && r.settingsRestored && r.sceneAndMetaRestored && r.mainSceneUnchanged && r.sceneRestoreVerified
                && r.gradleTemporaryValuesVerified && r.gradleRestored
                && r.changedProtectedInputs != null && r.changedProtectedInputs.Length == 0;
            // Failures leave recovery backups intact; no installs, clears, uninstalls or file deletions.
            if (!Inside(r.outputDirectory, Application.dataPath) && !Inside(r.outputDirectory, Main + "/Assets"))
            {
                Directory.CreateDirectory(r.outputDirectory);
                File.WriteAllText(r.outputDirectory + "/quest-exterior-build.json", JsonUtility.ToJson(r, true));
                File.WriteAllLines(r.outputDirectory + "/build-messages.txt", Logs.Select(m => m.type + " | " + m.text).Concat(new[] { r.error ?? "No captured exception." }));
            }
        }
        Debug.Log("QUEST_EXTERIOR_BUILD_RESULT=" + r.passed + " | " + r.outputDirectory);
        if (Application.isBatchMode) EditorApplication.Exit(r.passed ? 0 : 1);
    }

    static void CaptureBuildReport(BuildReport b, Report r)
    {
        var s = b.summary;
        r.buildResult = s.result.ToString(); r.buildSucceeded = s.result == BuildResult.Succeeded;
        r.buildGuid = s.guid.ToString(); r.buildOptions = s.options.ToString(); r.buildPlatform = s.platform.ToString();
        r.buildErrors = s.totalErrors; r.buildWarnings = s.totalWarnings; r.buildBytes = s.totalSize; r.buildSeconds = s.totalTime.TotalSeconds;
        r.buildSteps = b.steps.Select(step => new Step { name = step.name, depth = step.depth, durationMs = step.duration.TotalMilliseconds,
            messages = step.messages.Select(m => new Message { type = m.type.ToString(), text = m.content }).ToArray() }).ToArray();
        r.buildFiles = b.GetFiles().Select(f => new BuildFileRecord { path = f.path, role = f.role, size = f.size }).ToArray();
        Need(s.platform == BuildTarget.Android && s.options == BuildOptions.None, "Report is not a Release Android build");
    }
    static void UseOwnedAsciiGradleHome(Report r)
    {
        // Never rewrite, copy, clean or redirect through the user's ~/.gradle directory.
        Need(EqualPath(r.project, Clone) && EqualPath(r.gradleHome, Clone + "/.build-cache/gradle")
            && Inside(r.gradleHome, Clone + "/.build-cache"), "Gradle cache must be the exact owned clone path");
        Need(r.gradleHome.All(c => c >= 32 && c < 127), "Gradle cache path must be ASCII");
        string existing = r.gradleHome;
        while (!Directory.Exists(existing) && !File.Exists(existing)) existing = Path.GetDirectoryName(existing);
        NoReparse(existing);
        Directory.CreateDirectory(r.gradleHome);
        NoReparse(r.gradleHome);
        Environment.SetEnvironmentVariable("GRADLE_USER_HOME", r.gradleHome);
        AndroidExternalToolsSettings.Gradle.userHomePath = r.gradleHome;
        r.gradleEnvironmentDuring = Environment.GetEnvironmentVariable("GRADLE_USER_HOME");
        r.gradleApiDuring = AndroidExternalToolsSettings.Gradle.userHomePath;
        r.gradleTemporaryValuesVerified = EqualPath(r.gradleEnvironmentDuring, r.gradleHome) && EqualPath(r.gradleApiDuring, r.gradleHome);
        Need(r.gradleTemporaryValuesVerified, "Temporary ASCII Gradle environment/API values did not read back");
    }
    static SceneSetupState[] SetupState(SceneSetup[] setup) => setup.Select(s => new SceneSetupState {
        path = s.path, loaded = s.isLoaded, active = s.isActive }).ToArray();
    static EditorSceneState[] EditorScenes()
    {
        var active = SceneManager.GetActiveScene();
        return Enumerable.Range(0, SceneManager.sceneCount).Select(i => {
            var s = SceneManager.GetSceneAt(i);
            return new EditorSceneState { path = s.path, loaded = s.isLoaded, active = s == active,
                dirty = s.isDirty, rootCount = s.isLoaded ? s.rootCount : 0 };
        }).ToArray();
    }
    static bool SameSetup(SceneSetupState[] before, SceneSetupState[] after) => before != null && after != null
        && before.Length == after.Length && before.Zip(after, (a, b) =>
            string.Equals((a.path ?? "").Replace('\\', '/'), (b.path ?? "").Replace('\\', '/'), StringComparison.OrdinalIgnoreCase)
            && a.loaded == b.loaded && a.active == b.active).All(same => same);
    static void InspectApk(Report r)
    {
        string buildTools = Path.Combine(AndroidExternalToolsSettings.sdkRootPath, "build-tools");
        Need(Directory.Exists(buildTools), "Android SDK build-tools unavailable for manifest verification");
        r.aapt = Directory.GetFiles(buildTools, "aapt.exe", SearchOption.AllDirectories).OrderByDescending(p => p, StringComparer.Ordinal).FirstOrDefault();
        Need(r.aapt != null, "aapt.exe unavailable; cannot verify actual APK identity");
        using (var process = new Process { StartInfo = new ProcessStartInfo {
            FileName = r.aapt, Arguments = "dump badging \"" + r.apk + "\"", UseShellExecute = false,
            CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true
        } })
        {
            process.Start();
            var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(45000)) { try { process.Kill(); } catch { } throw new TimeoutException("aapt manifest check timed out"); }
            r.aaptOutput = stdout.GetAwaiter().GetResult(); r.aaptError = stderr.GetAwaiter().GetResult();
            File.WriteAllText(r.outputDirectory + "/apk-badging.txt", r.aaptOutput + "\nSTDERR:\n" + r.aaptError);
            Need(process.ExitCode == 0, "aapt manifest read failed");
        }
        var id = Regex.Match(r.aaptOutput, @"(?m)^package: name='([^']+)' versionCode='([^']+)'" );
        Need(id.Success, "APK manifest package/version not found");
        r.actualPackageId = id.Groups[1].Value; r.actualVersionCode = id.Groups[2].Value;
        var activity = Regex.Match(r.aaptOutput, @"(?m)^launchable-activity: name='([^']+)'" );
        r.launchableActivity = activity.Success ? activity.Groups[1].Value : "";
        Need(r.actualPackageId == PackageId && activity.Success, "APK actual identity/activity does not match the independent test app");
        Need(!r.aaptOutput.Contains("application-debuggable"), "APK is debuggable despite Release request");
        Need(r.aaptOutput.Contains("'arm64-v8a'") && !r.aaptOutput.Contains("'armeabi-v7a'"), "APK does not contain the expected ARM64-only native build");
    }
    static PlayerState ReadSettings() => new PlayerState {
        applicationIdentifier = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android), productName = PlayerSettings.productName,
        development = EditorUserBuildSettings.development, connectProfiler = EditorUserBuildSettings.connectProfiler,
        allowDebugging = EditorUserBuildSettings.allowDebugging, buildAppBundle = EditorUserBuildSettings.buildAppBundle,
        exportAsProject = EditorUserBuildSettings.exportAsGoogleAndroidProject,
        editorBuildScenes = EditorBuildSettings.scenes.Select(s => new BuildSceneState { path = s.path, enabled = s.enabled }).ToArray()
    };
    static void RestoreSettings(PlayerState s)
    {
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, s.applicationIdentifier); PlayerSettings.productName = s.productName;
        EditorUserBuildSettings.development = s.development; EditorUserBuildSettings.connectProfiler = s.connectProfiler;
        EditorUserBuildSettings.allowDebugging = s.allowDebugging; EditorUserBuildSettings.buildAppBundle = s.buildAppBundle;
        EditorUserBuildSettings.exportAsGoogleAndroidProject = s.exportAsProject;
        EditorBuildSettings.scenes = s.editorBuildScenes.Select(scene => new EditorBuildSettingsScene(scene.path, scene.enabled)).ToArray();
    }
    static string[] ConfigFiles(string[] roots) => roots.Select(Owned)
        .SelectMany(p => File.Exists(p) ? new[] { p } : Directory.Exists(p) ? Directory.GetFiles(p, "*", SearchOption.AllDirectories) : new string[0])
        .Select(p => Full(p).Substring(Full(Clone).Length + 1)).OrderBy(p => p, StringComparer.Ordinal).ToArray();
    static void Log(string text, string stack, LogType type)
    {
        if (recording && (type == LogType.Warning || type == LogType.Error || type == LogType.Exception || type == LogType.Assert))
            Logs.Add(new Message { type = type.ToString(), text = text + "\n" + stack });
    }
    static void AddFailure(Report r, string label, Exception e) { r.error = (r.error ?? "") + "\n" + label + ": " + e; }
    static void Need(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    static string Required(string key) { string s = Environment.GetEnvironmentVariable(key); Need(!string.IsNullOrWhiteSpace(s), "Missing " + key); return s; }
    static string Full(string p) => Path.GetFullPath(p).Replace('\\', '/').TrimEnd('/');
    static string Owned(string p) => Full(Path.Combine(Clone, p));
    static bool EqualPath(string a, string b) => !string.IsNullOrWhiteSpace(a) && string.Equals(Full(a), Full(b), StringComparison.OrdinalIgnoreCase);
    static bool Inside(string p, string root) => (Full(p) + "/").StartsWith(Full(root) + "/", StringComparison.OrdinalIgnoreCase);
    static bool HashEqual(string a, string b) => a != null && Regex.IsMatch(a, "^[a-fA-F0-9]{64}$") && string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    static string Sha(string path) { using (var hash = SHA256.Create()) using (var stream = File.OpenRead(path)) return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "").ToLowerInvariant(); }
    static void NoReparse(string path)
    {
        string current = Full(path);
        while (!string.IsNullOrEmpty(current))
        {
            Need((File.GetAttributes(current) & FileAttributes.ReparsePoint) == 0, "Refusing reparse path: " + current);
            var parent = Path.GetDirectoryName(current); if (string.IsNullOrEmpty(parent) || EqualPath(current, parent)) break; current = parent;
        }
    }
    static void PhysicalFile(string path)
    {
        Need(File.Exists(path), "Required file missing: " + path); NoReparse(path);
        using (var stream = File.OpenRead(path))
        {
            Need(GetFileInformationByHandle(stream.SafeFileHandle, out FileInfoNative info), "Cannot verify physical file ownership: " + path);
            Need(info.links == 1, "Refusing multiply-linked file: " + path);
        }
    }
    [StructLayout(LayoutKind.Sequential)] struct FileInfoNative
    {
        public uint attributes; public System.Runtime.InteropServices.ComTypes.FILETIME creation, access, write;
        public uint volume, sizeHigh, sizeLow, links, indexHigh, indexLow;
    }
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool GetFileInformationByHandle(SafeFileHandle handle, out FileInfoNative info);
}
