#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NarutoVR.SealCombat.Editor
{
    /// <summary>Explicit Quest build with bundled voice validation. Never saves scenes or rewrites project settings.</summary>
    public static class QuestVoiceBuild
    {
        const string ScenePath = "Assets/Scene/Chapter01_Awakening.unity";
        const string P1ScenePath = "Assets/Scene/KaogongP1Trial.unity";
        const string ModelRoot = "Assets/StreamingAssets/SkillVoice";
        const string NativeRoot = "Assets/Plugins/Android/arm64-v8a";
        const string Output = "Builds/Quest/Kaogong-Quest-Voice.apk";
        const string P1Output = "Builds/Quest/Kaogong-P1-Quest-Voice.apk";
        const string P1ApplicationIdentifier = "com.kaogonglvdu.p1.voicecheck";
        const string LogRoot = "Logs/quest-native-voice-20261108";

        [Serializable] sealed class ModelFile { public string name, sha256; public long bytes; }
        [Serializable] sealed class ModelManifest { public int schema; public string version, encoder, decoder, joiner, tokens; public ModelFile[] files; }
        [Serializable] sealed class VoiceWord { public string phrase, tokens; }
        [Serializable] sealed class VoiceExport { public int schema; public string cancelPhrase; public VoiceWord[] keywords; }
        [Serializable] sealed class Evidence { public string path, sha256; public long bytes; }
        [Serializable] sealed class CatalogEvidence { public string path, controllerFileId, receiverFileId; public int phrases; public string[] voicePhrases; }
        [Serializable] sealed class Report
        {
            public string utc, action, unityVersion, projectPath, scene, applicationIdentifier, modelVersion;
            public string outputPath, result, error, apkSha256;
            public bool preflightPassed, passed, p1ThunderTrialEnabled;
            public int minSdk, totalErrors, totalWarnings;
            public long totalSize;
            public List<Evidence> files = new List<Evidence>();
            public List<CatalogEvidence> catalogs = new List<CatalogEvidence>();
        }

        [MenuItem("Tools/NarutoVR/Validate Quest Voice Build")]
        public static void Validate() => Execute(false, ScenePath, Output, true);

        [MenuItem("Tools/NarutoVR/Build Quest Voice APK")]
        public static void Run() => Execute(true, ScenePath, Output, true);

        [MenuItem("Tools/NarutoVR/Validate P1 Quest Voice Build")]
        public static void ValidateP1() => ExecuteP1(false);

        [MenuItem("Tools/NarutoVR/Build P1 Quest Voice APK")]
        public static void RunP1() => ExecuteP1(true);

        static void ExecuteP1(bool build)
        {
            string original = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
            try
            {
                // The legacy template ID is already used by another headset application.
                // Scope this standalone test ID to the operation and restore project state.
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, P1ApplicationIdentifier);
                Execute(build, P1ScenePath, P1Output, false);
            }
            finally { PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, original); }
        }

        static void Execute(bool build, string scenePath, string outputPath, bool requireEnabledScene)
        {
            var report = new Report
            {
                utc = DateTimeOffset.Now.ToString("O"), action = build ? "build" : "preflight",
                unityVersion = Application.unityVersion, projectPath = Path.GetFullPath("."), scene = scenePath,
                applicationIdentifier = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android),
                minSdk = (int)PlayerSettings.Android.minSdkVersion, outputPath = Path.GetFullPath(outputPath)
            };
            try
            {
                Require(!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling && !EditorApplication.isUpdating,
                    "Wait until the editor is idle in Edit Mode.");
                for (int i = 0; i < SceneManager.sceneCount; i++)
                    Require(!SceneManager.GetSceneAt(i).isDirty, "An open scene has unsaved edits; this tool will not save or discard them.");
                Preflight(report, requireEnabledScene);
                report.preflightPassed = true;
                if (build)
                {
                    Require(EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android,
                        "Select Android before running this command, or pass -buildTarget Android to an isolated batch editor.");
                    Directory.CreateDirectory(Path.GetDirectoryName(report.outputPath));
                    var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                    {
                        scenes = new[] { report.scene }, target = BuildTarget.Android,
                        locationPathName = report.outputPath, options = BuildOptions.Development
                    });
                    var summary = result.summary;
                    report.result = summary.result.ToString();
                    report.totalErrors = (int)summary.totalErrors;
                    report.totalWarnings = (int)summary.totalWarnings;
                    report.totalSize = checked((long)summary.totalSize);
                    Require(summary.result == BuildResult.Succeeded, "Unity BuildPipeline failed; inspect this run's editor log.");
                    Require(File.Exists(report.outputPath), "Build reported success without the requested APK.");
                    report.totalSize = new FileInfo(report.outputPath).Length;
                    report.apkSha256 = Hash(report.outputPath);
                }
                else report.result = "PreflightPassed";
                report.passed = true;
                Debug.Log("[QuestVoiceBuild] " + report.result + "; applicationId=" + report.applicationIdentifier);
            }
            catch (Exception error)
            {
                report.passed = false;
                report.error = error.ToString();
                if (string.IsNullOrEmpty(report.result)) report.result = "Failed";
                throw new BuildFailedException("Quest voice " + report.action + " failed: " + error.Message);
            }
            finally
            {
                Directory.CreateDirectory(LogRoot);
                string name = "build-" + report.action + "-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + ".json";
                File.WriteAllText(Path.Combine(LogRoot, name), JsonUtility.ToJson(report, true));
            }
        }

        static void Preflight(Report report, bool requireEnabledScene)
        {
            Require(PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android) == ScriptingImplementation.IL2CPP, "Android requires IL2CPP.");
            Require(PlayerSettings.Android.targetArchitectures == AndroidArchitecture.ARM64, "Android must target ARM64 only.");
            Require((int)PlayerSettings.Android.minSdkVersion >= 32, "The existing Quest minimum API 32 must be retained.");
            Require(!EditorUserBuildSettings.buildAppBundle, "APK build requested; turn off App Bundle export explicitly first.");
            Require(!string.IsNullOrWhiteSpace(report.applicationIdentifier), "Android application identifier is empty.");
            // P1 is an explicitly selected trial build and need not rewrite the project's
            // normal scene list. The original chapter entry retains its enabled-scene check.
            if (requireEnabledScene)
                Require(EditorBuildSettings.scenes.Any(s => s.enabled && s.path == report.scene), "The selected scene is not enabled in Build Settings.");
            string androidManifest = "Assets/Plugins/Android/AndroidManifest.xml";
            Require(File.Exists(androidManifest), "Android manifest is missing.");
            XNamespace android = "http://schemas.android.com/apk/res/android";
            var document = XDocument.Load(androidManifest);
            Require(document.Root != null && document.Root.Elements("uses-permission").Any(e => (string)e.Attribute(android + "name") == "android.permission.RECORD_AUDIO"),
                "Android manifest must explicitly declare android.permission.RECORD_AUDIO.");
            RecordFile(report, androidManifest);

            foreach (string name in new[] { "libsherpa-onnx-c-api.so", "libonnxruntime.so" })
                Require(File.Exists(NativeRoot + "/" + name), "Missing ARM64 voice dependency: " + name);
            foreach (string path in Directory.GetFiles(NativeRoot, "*.so"))
            {
                byte[] elf = new byte[20];
                using (var stream = File.OpenRead(path)) Require(stream.Read(elf, 0, elf.Length) == elf.Length, "Native library is truncated: " + path);
                Require(elf[0] == 0x7f && elf[1] == 'E' && elf[2] == 'L' && elf[3] == 'F' && elf[4] == 2 && elf[5] == 1 && elf[18] == 183 && elf[19] == 0,
                    "Expected a little-endian ELF64 AArch64 library: " + path);
                string asset = path.Replace('\\', '/');
                var importer = AssetImporter.GetAtPath(asset) as PluginImporter;
                Require(importer != null && importer.GetCompatibleWithPlatform(BuildTarget.Android) && !importer.GetCompatibleWithEditor(),
                    "Voice native plugin must be enabled for Android and disabled for Editor: " + asset);
                Require(importer.GetPlatformData(BuildTarget.Android, "CPU") == "ARM64", "Set Android plugin CPU to ARM64: " + asset);
                RecordFile(report, asset);
            }

            string manifestPath = ModelRoot + "/model-manifest.json";
            Require(File.Exists(manifestPath), "Bundled voice model manifest is missing.");
            var manifest = JsonUtility.FromJson<ModelManifest>(File.ReadAllText(manifestPath));
            Require(manifest != null && manifest.schema == 1 && !string.IsNullOrWhiteSpace(manifest.version), "Unsupported bundled model manifest.");
            var required = new[] { manifest.encoder, manifest.decoder, manifest.joiner, manifest.tokens };
            Require(required.All(FlatName) && required.Distinct(StringComparer.Ordinal).Count() == 4, "Model names must be four unique flat filenames.");
            Require(required.Take(3).All(n => n.EndsWith(".onnx", StringComparison.Ordinal)) && manifest.tokens == "tokens.txt", "Expected encoder/decoder/joiner ONNX and tokens.txt.");
            Require(manifest.files != null && manifest.files.Length == 4 && manifest.files.All(f => f != null && FlatName(f.name)) &&
                    manifest.files.Select(f => f.name).OrderBy(n => n, StringComparer.Ordinal).SequenceEqual(required.OrderBy(n => n, StringComparer.Ordinal)),
                "Manifest file records must match the four configured model files exactly.");
            foreach (var file in manifest.files)
            {
                string path = ModelRoot + "/" + file.name;
                Require(file.bytes > 0 && Regex.IsMatch(file.sha256 ?? "", "\\A[0-9a-fA-F]{64}\\z"), "Invalid size/hash record: " + file.name);
                Require(File.Exists(path) && new FileInfo(path).Length == file.bytes, "Bundled model file missing or wrong size: " + file.name);
                Require(string.Equals(Hash(path), file.sha256, StringComparison.OrdinalIgnoreCase), "Bundled model hash mismatch: " + file.name);
                RecordFile(report, path);
            }
            report.modelVersion = manifest.version;
            RecordFile(report, manifestPath);
            var tokens = new HashSet<string>(File.ReadLines(ModelRoot + "/" + manifest.tokens).Where(l => !string.IsNullOrWhiteSpace(l))
                .Select(l => l.Split((char[])null, StringSplitOptions.RemoveEmptyEntries)[0]), StringComparer.Ordinal);
            Require(tokens.Count > 10, "Bundled acoustic token inventory is empty or truncated.");
            ValidateSceneCatalogs(report, tokens);
        }

        static void ValidateSceneCatalogs(Report report, HashSet<string> tokens)
        {
            Require(File.Exists(report.scene), "Selected scene file is missing: " + report.scene);
            string scene = File.ReadAllText(report.scene);
            // This authored scene stores its controller and receiver directly. Unsupported
            // prefab/stripped wiring fails explicitly instead of silently missing a catalog.
            var blocks = Regex.Matches(scene, @"(?ms)^--- !u!114 &(-?\d+)\r?\n(.*?)(?=^--- |\z)").Cast<Match>()
                .ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value);
            string controllerGuid = ScriptGuid(typeof(SkillInputController));
            string receiverGuid = ScriptGuid(typeof(SkillVoiceReceiver));
            var controllers = blocks.Where(p => p.Value.Contains("guid: " + controllerGuid + ",") && Regex.IsMatch(p.Value, @"(?m)^  m_Enabled: 1\r?$")).ToArray();
            Require(controllers.Length > 0, "No enabled directly authored SkillInputController found in the selected scene.");
            foreach (var controller in controllers)
            {
                var voice = Regex.Match(controller.Value, @"(?m)^  voice: \{fileID: (-?\d+)\}\r?$");
                Require(voice.Success && blocks.TryGetValue(voice.Groups[1].Value, out _), "SkillInputController has no direct voice receiver reference.");
                string receiver = blocks[voice.Groups[1].Value];
                Require(receiver.Contains("guid: " + receiverGuid + ",") && Regex.IsMatch(receiver, @"(?m)^  m_Enabled: 1\r?$"), "Referenced voice receiver is missing, disabled or the wrong component.");
                string path = SceneCatalogPath(blocks, controller);
                Require(AssetDatabase.GetDependencies(report.scene, true).Contains(path), "Scene skill catalog dependency is unavailable.");
                using var source = AuthoredSkillCatalog.Open(path);
                var catalog = source.Catalog;
                string[] voicePhrases = catalog.GetVoicePhrases();
                if (report.scene == P1ScenePath)
                {
                    Require(voicePhrases.Contains("雷光剑") && catalog.skills.Any(skill => skill && skill.skillId == "thunder.sword"),
                        "P1 must expose the thunder.sword recipe and 雷光剑 voice phrase.");
                    ValidateP1TrialBinding(blocks, controller.Key);
                    report.p1ThunderTrialEnabled = true;
                }
                int receivePort = IntField(receiver, "receivePort"), servicePort = IntField(receiver, "servicePort");
                // Reuse the existing exporter without writing Tools or any asset. Its optional
                // desktop token check is supplemented by strict bundled-model token checking.
                string exported = SkillConfigurationSetup.BuildVoiceJson(catalog, receivePort, servicePort);
                var config = JsonUtility.FromJson<VoiceExport>(exported);
                Require(config != null && config.schema == 1 && config.keywords != null && config.keywords.Length > 0, "Voice catalog export is empty.");
                foreach (var word in config.keywords)
                {
                    Require(word != null && !string.IsNullOrWhiteSpace(word.phrase) && !string.IsNullOrWhiteSpace(word.tokens), "Missing phrase or acoustic tokens.");
                    foreach (string token in word.tokens.Split((char[])null, StringSplitOptions.RemoveEmptyEntries))
                        Require(tokens.Contains(token), "Bundled model has no acoustic token '" + token + "' for " + word.phrase);
                }
                Require(config.keywords.Any(w => w.phrase == config.cancelPhrase), "Export lacks the cancel phrase.");
                report.catalogs.Add(new CatalogEvidence { path = path, controllerFileId = controller.Key, receiverFileId = voice.Groups[1].Value, phrases = config.keywords.Length, voicePhrases = voicePhrases });
            }
            RecordFile(report, report.scene);
        }

        static string SceneCatalogPath(Dictionary<string, string> blocks, KeyValuePair<string, string> controller)
        {
            const string assetRef = @": \{fileID: 11400000, guid: ([0-9a-f]{32}), type: 2\}\r?$";
            string rpgGuid = ScriptGuid(typeof(Rpg.LinearRpgController));
            var owner = blocks.Values.SingleOrDefault(block => block.Contains("guid: " + rpgGuid + ",") &&
                Regex.IsMatch(block, @"(?m)^  m_Enabled: 1\r?$") &&
                Regex.IsMatch(block, @"(?m)^  skills: \{fileID: " + Regex.Escape(controller.Key) + @"\}\r?$"));
            var reference = Regex.Match(owner ?? "", @"(?m)^  skillResources" + assetRef);
            if (!reference.Success) reference = Regex.Match(controller.Value, @"(?m)^  catalog" + assetRef);
            Require(reference.Success, "Skill controller has no authored resource manifest or skill catalog.");
            return AssetDatabase.GUIDToAssetPath(reference.Groups[1].Value);
        }

        static void ValidateP1TrialBinding(Dictionary<string, string> blocks, string controllerId)
        {
            string rpgGuid = ScriptGuid(typeof(Rpg.LinearRpgController));
            string chapterGuid = ScriptGuid(typeof(Rpg.TrainingChapterFlow));
            var owners = blocks.Where(p => p.Value.Contains("guid: " + rpgGuid + ",") &&
                Regex.IsMatch(p.Value, @"(?m)^  m_Enabled: 1\r?$") &&
                Regex.IsMatch(p.Value, @"(?m)^  skills: \{fileID: " + Regex.Escape(controllerId) + @"\}\r?$")).ToArray();
            Require(owners.Length == 1, "P1 skill controller must have one enabled RPG owner.");
            var owner = owners[0];
            var chapterRef = Regex.Match(owner.Value, @"(?m)^  chapter: \{fileID: (-?\d+)\}\r?$");
            Require(chapterRef.Success && blocks.ContainsKey(chapterRef.Groups[1].Value), "P1 RPG has no directly authored chapter.");
            string chapter = blocks[chapterRef.Groups[1].Value];
            Require(chapter.Contains("guid: " + chapterGuid + ",") && Regex.IsMatch(chapter, @"(?m)^  m_Enabled: 1\r?$") &&
                Regex.IsMatch(chapter, @"(?m)^  rpg: \{fileID: " + Regex.Escape(owner.Key) + @"\}\r?$"),
                "P1 chapter must be an enabled TrainingChapterFlow bound back to the RPG owner.");
            var trials = Regex.Match(chapter, @"(?m)^  trialSkills:\r?\n((?:  - [^\r\n]*\r?\n)*)");
            Require(trials.Success && Regex.IsMatch(trials.Groups[1].Value, @"(?m)^  - thunder\.sword\r?$"),
                "P1 must authorize thunder.sword as a training skill on entry.");
        }

        static int IntField(string yaml, string field)
        {
            var match = Regex.Match(yaml, @"(?m)^  " + Regex.Escape(field) + @": (\d+)\r?$");
            Require(match.Success, "Missing serialized voice field: " + field);
            return int.Parse(match.Groups[1].Value);
        }
        static string ScriptGuid(Type type)
        {
            var matches = AssetDatabase.FindAssets(type.Name + " t:MonoScript").Where(guid =>
                AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(guid)).GetClass() == type).ToArray();
            Require(matches.Length == 1, "Cannot uniquely locate script for " + type.FullName);
            return matches[0];
        }
        static bool FlatName(string name) => !string.IsNullOrEmpty(name) && name == Path.GetFileName(name) && !name.Contains("..") && name.IndexOfAny(new[] { '/', '\\', ':' }) < 0;
        static void RecordFile(Report report, string path) => report.files.Add(new Evidence { path = path, bytes = new FileInfo(path).Length, sha256 = Hash(path) });
        static string Hash(string path)
        {
            using (var input = File.OpenRead(path)) using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(input)).Replace("-", "").ToLowerInvariant();
        }
        static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
#endif
