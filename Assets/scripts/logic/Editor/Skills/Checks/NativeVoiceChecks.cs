#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace NarutoVR.SealCombat.Editor
{
    /// <summary>Edit-mode validation only. Synthetic telemetry never opens a microphone;
    /// native fixture decoding runs on its own worker using the real packaged model.</summary>
    public static class NativeVoiceChecks
    {
        public static string FixtureRoot;
        const string Chapter = "Assets/Scene/Chapter01_Awakening.unity";
        const string ModelDirectory = "Assets/StreamingAssets/SkillVoice";
        const string ReportDirectory = "Logs/quest-native-voice-20261108";
        const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        static Report report;

        [Serializable] sealed class Report
        {
            public string utc, unityVersion, error, fixtureRoot, nativeVersion;
            public bool passed, sceneUnchanged, syntheticTelemetry = true, syntheticTtsOnly = true;
            public bool microphoneOpened, headsetTested, androidRuntimeTested;
            public int totalChecks, existingAudioChecks, existingDiagnosticsChecks, existingClockChecks, wristMenuChecks, wristLifecycleChecks;
            public List<string> checks = new List<string>();
            public List<string> catalogPaths = new List<string>();
            public string fixtureCatalogPath;
            public List<FixtureResult> nativeFixtures = new List<FixtureResult>();
        }
        [Serializable] sealed class FixtureResult
        {
            public string name;
            public string[] expected, actual;
            public double[] endedAt, detectedAt;
            public bool passed;
        }
        sealed class SceneSnapshot
        {
            public SceneSetup[] setup;
            public SceneHandle[] handles;
            public int[] roots;
            public bool[] dirty;
            public Dictionary<string, string> hashes;
        }

        public static void ExecuteBatch()
        {
            try { Run(); if (Application.isBatchMode) EditorApplication.Exit(0); }
            catch (Exception error)
            {
                Debug.LogException(error);
                if (Application.isBatchMode) EditorApplication.Exit(1); else throw;
            }
        }

        public static int Run()
        {
            if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Native voice checks require edit mode; no Play transition will be started.");
            report = new Report { utc = DateTimeOffset.Now.ToString("O"), unityVersion = Application.unityVersion };
            var before = CaptureSceneState();
            Exception failure = null;
            try
            {
                report.existingAudioChecks = P1AudioChecks.Run();
                report.existingDiagnosticsChecks = VoiceDiagnosticsChecks.Run();
                report.existingClockChecks = SkillClockRegressionChecks.Run();
                report.wristMenuChecks = WristMenuChecks.Run();
                report.wristLifecycleChecks = WristViewLifecycleChecks.Run();
                report.totalChecks += report.existingAudioChecks + report.existingDiagnosticsChecks + report.existingClockChecks + report.wristMenuChecks + report.wristLifecycleChecks;
                string keywords = CheckConfigurationAndManifest();
                CheckNativeFacade();
                CheckNativeFixtures(keywords);
                report.passed = true;
            }
            catch (Exception error) { failure = error; report.error = error.ToString(); }
            finally
            {
                report.sceneUnchanged = SameSceneState(before, CaptureSceneState());
                if (!report.sceneUnchanged)
                {
                    report.passed = false;
                    report.error += "\nEditor scene identity, dirty state, root objects or disk bytes changed. No scene was saved or reloaded by this tool.";
                    failure = failure ?? new InvalidOperationException(report.error);
                }
                Directory.CreateDirectory(ReportDirectory);
                File.WriteAllText(Path.Combine(ReportDirectory, "unity-native-checks.json"), JsonUtility.ToJson(report, true), new UTF8Encoding(false));
            }
            if (failure != null) throw new InvalidOperationException("Native voice checks failed; see unity-native-checks.json", failure);
            Debug.Log("Native voice checks PASS: " + report.totalChecks + "; synthetic fixtures, no microphone/headset test.");
            return report.totalChecks;
        }

        static void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException(name);
            report.checks.Add(name); report.totalChecks++;
        }
        static void Reject<T>(Action action, string name) where T : Exception
        {
            try { action(); }
            catch (T) { report.checks.Add(name); report.totalChecks++; return; }
            throw new InvalidOperationException("Expected rejection: " + name);
        }
        static object Call(SkillVoiceReceiver receiver, string name, params object[] args)
        {
            MethodInfo method = typeof(SkillVoiceReceiver).GetMethod(name, PrivateInstance);
            if (method == null) throw new MissingMethodException(typeof(SkillVoiceReceiver).FullName, name);
            try { return method.Invoke(receiver, args); }
            catch (TargetInvocationException error) { throw error.InnerException ?? error; }
        }
        static string CheckConfigurationAndManifest()
        {
            string tokens = Path.Combine(ModelDirectory, "tokens.txt");
            var manifest = JsonUtility.FromJson<NativeVoiceModelFiles.Manifest>(File.ReadAllText(Path.Combine(ModelDirectory, "model-manifest.json")));
            NativeVoiceModelFiles.ValidateManifest(manifest);
            Check(manifest.files.Length == 4, "Packaged manifest contains exactly four model/token files");
            foreach (var entry in manifest.files)
            {
                byte[] data = File.ReadAllBytes(Path.Combine(ModelDirectory, entry.name));
                Check(data.LongLength == entry.bytes && NativeVoiceModelFiles.Hash(data) == entry.sha256, "Packaged bytes/hash: " + entry.name);
            }
            NativeVoiceModelFiles.Manifest Copy() => JsonUtility.FromJson<NativeVoiceModelFiles.Manifest>(JsonUtility.ToJson(manifest));
            var bad = Copy(); bad.version = "..";
            Reject<InvalidDataException>(() => NativeVoiceModelFiles.ValidateManifest(bad), "Manifest version cannot escape its cache directory");
            bad = Copy(); bad.files.First(f => f.name == bad.encoder).name = "../encoder.onnx"; bad.encoder = "../encoder.onnx";
            Reject<InvalidDataException>(() => NativeVoiceModelFiles.ValidateManifest(bad), "Manifest entry rejects path traversal");
            bad = Copy(); bad.files[1] = bad.files[0];
            Reject<InvalidDataException>(() => NativeVoiceModelFiles.ValidateManifest(bad), "Manifest rejects duplicated file entries");
            bad = Copy(); bad.decoder = bad.encoder;
            Reject<InvalidDataException>(() => NativeVoiceModelFiles.ValidateManifest(bad), "Manifest rejects duplicated required names");
            bad = Copy(); bad.files[0].sha256 = new string('G', 64);
            Reject<InvalidDataException>(() => NativeVoiceModelFiles.ValidateManifest(bad), "Manifest rejects malformed SHA256");
            bad = Copy(); bad.files[0].bytes = 0;
            Reject<InvalidDataException>(() => NativeVoiceModelFiles.ValidateManifest(bad), "Manifest rejects invalid file length");

            var paths = AssetDatabase.GetDependencies(Chapter, true)
                .Where(path => AssetDatabase.GetMainAssetTypeAtPath(path) == typeof(SkillCatalog) ||
                               AssetDatabase.GetMainAssetTypeAtPath(path) == typeof(SkillResourceManifest))
                .OrderBy(path => path, StringComparer.Ordinal).ToArray();
            Check(paths.Length != 0, "Real Chapter01 dependencies contain an authored skill catalog or resource manifest");
            bool hasManifest = paths.Any(path => AssetDatabase.GetMainAssetTypeAtPath(path) == typeof(SkillResourceManifest));
            string selectedKeywords = null;
            foreach (var path in paths)
            {
                using var authored = AuthoredSkillCatalog.Open(path);
                var catalog = authored.Catalog;
                report.catalogPaths.Add(path);
                string text = NativeVoiceConfiguration.BuildKeywordText(catalog, tokens);
                string[] labels = text.Split('\n').Where(line => !string.IsNullOrWhiteSpace(line)).Select(line => line.Substring(line.LastIndexOf('@') + 1)).ToArray();
                string[] expected = catalog.GetVoicePhrases();
                Check(labels.Length == expected.Length && labels.OrderBy(x => x).SequenceEqual(expected.OrderBy(x => x)), "Native vocabulary matches all voice recipes and cancellation: " + catalog.name);
                Check(labels.Count(label => label == SkillPhrase.Normalize(catalog.cancelPhrase)) == 1, "Cancellation keyword appears exactly once: " + catalog.name);
                if ((!hasManifest || AssetDatabase.GetMainAssetTypeAtPath(path) == typeof(SkillResourceManifest)) &&
                    new[] { "护体咒", "火球术", "取消施法" }.All(expected.Contains))
                { report.fixtureCatalogPath = path; selectedKeywords = text; }
            }
            Check(report.fixtureCatalogPath != null, "Chapter vocabulary contains the existing three WAV fixture labels");
            using var selected = AuthoredSkillCatalog.Open(report.fixtureCatalogPath);
            var clone = Object.Instantiate(selected.Catalog);
            SkillDefinition word = null;
            try
            {
                int index = clone.skills.FindIndex(skill => skill && skill.triggerMode != SkillTriggerMode.GestureOnly);
                word = Object.Instantiate(clone.skills[index]); clone.skills[index] = word;
                word.phraseTokens = "__not_a_model_token__";
                Reject<InvalidOperationException>(() => NativeVoiceConfiguration.BuildKeywordText(clone, tokens), "Acoustic token absent from model is rejected");
                word.phraseTokens = "";
                Reject<InvalidOperationException>(() => NativeVoiceConfiguration.BuildKeywordText(clone, tokens), "Missing acoustic tokens are rejected");
            }
            finally { if (word) Object.DestroyImmediate(word); Object.DestroyImmediate(clone); }
            return selectedKeywords;
        }

        static void CheckNativeFacade()
        {
            var go = new GameObject("Native voice synthetic gate checks") { hideFlags = HideFlags.HideAndDontSave };
            go.SetActive(false);
            try
            {
                var receiver = go.AddComponent<SkillVoiceReceiver>();
                receiver.useNativeInEditor = true; receiver.autoStartInEditor = false; receiver.logPhrases = false;
                receiver.ConfigurePhrases(new[] { "火球术", "取消施法" });
                int received = 0;
                receiver.PhraseReceived += (phrase, ended) => received++;
                go.SetActive(true);
                Check(receiver.UsesNativeBackend && typeof(SkillVoiceReceiver).GetField("socket", PrivateInstance).GetValue(receiver) == null && !go.GetComponent<NativeSkillVoiceSource>(), "Edit-only native facade creates neither UDP nor microphone source");
                long generation = (long)Call(receiver, "NativeBeginSession");
                Call(receiver, "NativeStatus", "Synthetic telemetry fixture; microphone not opened", true, generation);
                Check(!receiver.HasServiceHeartbeat && !receiver.HasRecentMicrophoneSpeech(), "Ready without captured audio is not microphone liveness");
                void Audio(long gen, long count = 1, float rms = .1f) => Call(receiver, "NativeAudio", Time.realtimeSinceStartupAsDouble, rms, .2f, count, count, "synthetic native input", 16000, gen);
                void Phrase(string label, double ended, long gen) => Call(receiver, "NativePhrase", new NativeKeywordHit(label, ended, ended), gen);
                Phrase("火球术", Time.realtimeSinceStartupAsDouble, generation);
                Check(received == 0, "Results before fresh microphone telemetry are rejected");
                Audio(generation);
                Check(receiver.HasServiceHeartbeat && receiver.HasRecentMicrophoneSpeech(), "Fresh synthetic native microphone energy activates the ducking gate");
                double firstEnd = Time.realtimeSinceStartupAsDouble;
                Phrase("火 球术！", firstEnd, generation);
                Check(received == 1 && receiver.LastPhrase == "火球术", "Native result normalizes and passes current catalog whitelist");
                Phrase("火球术", firstEnd, generation);
                Phrase("未知口令", Time.realtimeSinceStartupAsDouble, generation);
                Phrase("火球术", Time.realtimeSinceStartupAsDouble, generation - 1);
                Phrase("火球术", Time.realtimeSinceStartupAsDouble + 1, generation);
                Phrase("火球术", double.NaN, generation);
                Check(received == 1, "Duplicate, unknown, old-generation, future and nonfinite results are rejected");
                Call(receiver, "NativeStatus", "stale generation", false, generation - 1);
                Check(receiver.HasServiceHeartbeat, "Old-generation status cannot stop the current capture session");
                Thread.Sleep(5);
                double staleEnd = Time.realtimeSinceStartupAsDouble;
                receiver.maximumEventAge = .01f;
                Thread.Sleep(40); Audio(generation, 2);
                Phrase("火球术", staleEnd, generation);
                Check(received == 1, "A current-generation keyword older than maximumEventAge is rejected");
                receiver.maximumEventAge = 3;
                Thread.Sleep(1100);
                Check(!receiver.HasServiceHeartbeat && !receiver.HasRecentMicrophoneSpeech(), "Expired capture telemetry releases the ducking gate");
                Phrase("火球术", Time.realtimeSinceStartupAsDouble, generation);
                Check(received == 1, "Fresh-looking result without fresh capture cannot activate a skill");
                long next = (long)Call(receiver, "NativeBeginSession");
                Check(next != generation && !receiver.HasServiceHeartbeat, "New capture session clears liveness and changes generation");
                Call(receiver, "NativeStatus", "Synthetic resumed capture", true, next); Audio(next);
                Phrase("火球术", Time.realtimeSinceStartupAsDouble, generation);
                Check(received == 1, "Late old-worker result cannot cross into a new session");
                Phrase("火球术", Time.realtimeSinceStartupAsDouble, next);
                Check(received == 2, "New-generation result remains usable after restart");
                receiver.ConfigurePhrases(null);
                Phrase("取消施法", Time.realtimeSinceStartupAsDouble, next);
                Check(received == 2, "Clearing configuration closes all keyword acceptance including cancellation");
                receiver.ConfigurePhrases(new[] { "火球术", "取消施法" });
                Call(receiver, "NativeEndSession", "Synthetic session ended");
                Call(receiver, "NativeStatus", "late status", true, next); Audio(next);
                Phrase("火球术", Time.realtimeSinceStartupAsDouble, next);
                Check(received == 2 && !receiver.HasServiceHeartbeat && !receiver.IsRunning, "Ended generation rejects late status, telemetry and results");
            }
            finally { Object.DestroyImmediate(go); }
        }

        static void CheckNativeFixtures(string keywords)
        {
            string fixtureRoot = Environment.GetEnvironmentVariable("NARUTOVR_VOICE_FIXTURE_ROOT");
            if (string.IsNullOrWhiteSpace(fixtureRoot)) fixtureRoot = FixtureRoot;
            if (string.IsNullOrWhiteSpace(fixtureRoot)) fixtureRoot = "D:/UnityProjects/NarutoVR/tools/SkillVoice/fixtures";
            fixtureRoot = Path.GetFullPath(fixtureRoot); report.fixtureRoot = fixtureRoot;
            string model = Path.GetFullPath(ModelDirectory);
            string work = Path.Combine(ReportDirectory, "unity-fixture-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(work);
            string keywordPath = Path.GetFullPath(Path.Combine(work, "当前目录词表.txt"));
            File.WriteAllText(keywordPath, keywords, new UTF8Encoding(false));
            var results = new List<FixtureResult>();
            var joinedHits = new List<NativeKeywordHit>();
            string version = null;
            string source = fixtureRoot;
            Task.Run(() =>
            {
                using (var decoder = new NativeKeywordDecoder(new SherpaKeywordEngine(model, keywordPath)))
                {
                    version = decoder.NativeVersion;
                    var hits = new List<string>(); decoder.Hit += hit => hits.Add(hit.Phrase);
                    foreach (var item in new[] { ("shield", "护体咒"), ("fireball", "火球术"), ("cancel", "取消施法"), ("negative", "") })
                    {
                        decoder.Reset(); hits.Clear();
                        float[] samples = ReadWave(Path.Combine(source, item.Item1 + ".wav"), out int rate);
                        var padded = new float[samples.Length + rate]; Array.Copy(samples, padded, samples.Length);
                        // Acoustic sample offsets are a fixture timeline, not a changed Unity/system clock.
                        decoder.Accept(padded, padded.Length, rate, 10);
                        decoder.Flush(10 + (double)padded.Length / rate);
                        string[] expected = item.Item2.Length == 0 ? Array.Empty<string>() : new[] { item.Item2 };
                        results.Add(new FixtureResult { name = item.Item1, expected = expected, actual = hits.ToArray(), passed = hits.SequenceEqual(expected) });
                    }
                    decoder.Reset(); hits.Clear();
                    var sequence = new List<float>();
                    foreach (string name in new[] { "shield", "fireball", "cancel", "shield" })
                    {
                        float[] samples = ReadWave(Path.Combine(source, name + ".wav"), out int rate);
                        if (rate != 16000) throw new InvalidDataException("Sequence fixtures must be 16kHz.");
                        sequence.AddRange(samples); sequence.AddRange(new float[rate]);
                    }
                    decoder.Accept(sequence.ToArray(), sequence.Count, 16000, 10); decoder.Flush(10 + sequence.Count / 16000d);
                    string[] repeated = { "护体咒", "火球术", "取消施法", "护体咒" };
                    results.Add(new FixtureResult { name = "sequence_and_repeat", expected = repeated, actual = hits.ToArray(), passed = hits.SequenceEqual(repeated) });
                    decoder.Reset(); hits.Clear(); decoder.Accept(new float[48000], 48000, 16000, 10); decoder.Flush(13);
                    results.Add(new FixtureResult { name = "silence", expected = Array.Empty<string>(), actual = hits.ToArray(), passed = hits.Count == 0 });
                    decoder.Reset(); hits.Clear();
                    var joined = new List<float>();
                    foreach (string name in new[] { "shield", "fireball", "cancel" })
                    {
                        float[] samples = ReadWave(Path.Combine(source, name + ".wav"), out int rate);
                        if (rate != 16000) throw new InvalidDataException("Joined fixtures must be 16kHz.");
                        int start = 0, end = samples.Length;
                        while (start < end && Math.Abs(samples[start]) < .001f) start++;
                        while (end > start && Math.Abs(samples[end - 1]) < .001f) end--;
                        joined.AddRange(samples.Skip(start).Take(end - start));
                    }
                    joined.AddRange(new float[16000]);
                    decoder.Hit += joinedHits.Add;
                    decoder.Accept(joined.ToArray(), joined.Count, 16000, 100);
                    decoder.Flush(100 + joined.Count / 16000d);
                    decoder.Hit -= joinedHits.Add;
                    string[] joinedExpected = { "护体咒", "火球术", "取消施法" };
                    results.Add(new FixtureResult { name = "joined_no_gap", expected = joinedExpected, actual = hits.ToArray(),
                        endedAt = joinedHits.Select(h => h.EndedAt).ToArray(), detectedAt = joinedHits.Select(h => h.DetectedAt).ToArray(),
                        passed = hits.SequenceEqual(joinedExpected) });
                }
            }).GetAwaiter().GetResult();
            report.nativeVersion = version; report.nativeFixtures.AddRange(results);
            Check(version == "2.0.1", "Unity Mono loads actual sherpa-onnx 2.0.1 C API with a UTF8 keyword path");
            foreach (var item in results) Check(item.passed, "Actual C API inference on synthetic TTS WAV: " + item.name);
            Check(joinedHits.Count == 3 && Math.Abs(joinedHits[1].EndedAt - joinedHits[2].EndedAt) <= .000001,
                "Joined real inference reproduces equal end estimates for fire and cancellation");
            CheckJoinedDelivery(joinedHits);
        }

        static void CheckJoinedDelivery(List<NativeKeywordHit> hits)
        {
            var go = new GameObject("Joined WAV result delivery checks") { hideFlags = HideFlags.HideAndDontSave };
            go.SetActive(false);
            try
            {
                var receiver = go.AddComponent<SkillVoiceReceiver>();
                receiver.useNativeInEditor = true; receiver.autoStartInEditor = false; receiver.logPhrases = false;
                receiver.ConfigurePhrases(new[] { "护体咒", "火球术", "取消施法" });
                using var selected = AuthoredSkillCatalog.Open(report.fixtureCatalogPath);
                var catalog = selected.Catalog;
                var engine = new SkillMatchEngine(catalog.skills, cancelPhrase: catalog.cancelPhrase);
                engine.Tick(Time.realtimeSinceStartupAsDouble);
                engine.TryActivate = _ => false; // No gameplay effect or microphone is created.
                var delivered = new List<string>();
                receiver.PhraseReceived += (phrase, ended) =>
                {
                    engine.Voice(phrase, ended, Time.realtimeSinceStartupAsDouble);
                    Check(engine.LastVoiceRejection == null, "Joined WAV passes actual match-engine gate: " + phrase);
                    delivered.Add(phrase);
                };
                go.SetActive(true);
                long generation = (long)Call(receiver, "NativeBeginSession");
                double now = Time.realtimeSinceStartupAsDouble;
                // Map the completed synthetic WAV timeline to a fresh synthetic capture epoch;
                // timestamps relative to each other are unchanged, including their equality.
                double offset = now - .05 - hits.Max(h => Math.Max(h.EndedAt, h.DetectedAt));
                typeof(SkillVoiceReceiver).GetField("nativeStartedAt", PrivateInstance).SetValue(receiver, now - 5);
                Call(receiver, "NativeStatus", "Joined WAV delivery fixture; no microphone", true, generation);
                Call(receiver, "NativeAudio", now, .1f, .2f, 1L, 1L, "synthetic joined WAV", 16000, generation);
                long epoch = engine.Epoch;
                foreach (var hit in hits)
                    Call(receiver, "NativePhrase", new NativeKeywordHit(hit.Phrase, hit.EndedAt + offset, hit.DetectedAt + offset), generation);
                Check(delivered.SequenceEqual(new[] { "护体咒", "火球术", "取消施法" }) && engine.Epoch == epoch + 1 && string.IsNullOrEmpty(engine.PendingSkillId),
                    "Equal-end cancellation reaches facade and match engine without retiming audio");
                var last = hits[hits.Count - 1];
                Call(receiver, "NativePhrase", new NativeKeywordHit(last.Phrase, last.EndedAt + offset, last.DetectedAt + offset), generation);
                Check(delivered.Count == 3 && engine.Epoch == epoch + 1, "Replayed identical native keyword is still rejected");
                Call(receiver, "NativePhrase", new NativeKeywordHit("护体咒", last.EndedAt + offset - .1, last.DetectedAt + offset), generation);
                Check(delivered.Count == 3, "Older native end timestamp is still rejected after joined words");
            }
            finally { Object.DestroyImmediate(go); }
        }

        static float[] ReadWave(string path, out int rate)
        {
            using (var reader = new BinaryReader(File.OpenRead(path)))
            {
                if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "RIFF") throw new InvalidDataException("Expected RIFF: " + path);
                reader.ReadUInt32();
                if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "WAVE") throw new InvalidDataException("Expected WAVE: " + path);
                rate = 0; int channels = 0, bits = 0, format = 0; byte[] data = null;
                while (reader.BaseStream.Position + 8 <= reader.BaseStream.Length)
                {
                    string id = Encoding.ASCII.GetString(reader.ReadBytes(4)); int length = reader.ReadInt32();
                    if (length < 0 || reader.BaseStream.Position + length > reader.BaseStream.Length) throw new InvalidDataException("Invalid WAV chunk.");
                    long end = reader.BaseStream.Position + length;
                    if (id == "fmt ") { format = reader.ReadUInt16(); channels = reader.ReadUInt16(); rate = reader.ReadInt32(); reader.ReadInt32(); reader.ReadUInt16(); bits = reader.ReadUInt16(); }
                    else if (id == "data") data = reader.ReadBytes(length);
                    reader.BaseStream.Position = end + (length & 1);
                }
                if (format != 1 || channels != 1 || bits != 16 || rate <= 0 || data == null || (data.Length & 1) != 0) throw new InvalidDataException("Expected mono PCM16 fixture.");
                var samples = new float[data.Length / 2];
                for (int i = 0; i < samples.Length; i++) samples[i] = BitConverter.ToInt16(data, i * 2) / 32768f;
                return samples;
            }
        }

        static SceneSnapshot CaptureSceneState()
        {
            var scenes = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).ToArray();
            var hashes = new Dictionary<string, string>();
            foreach (var scene in scenes)
                if (!string.IsNullOrEmpty(scene.path) && File.Exists(scene.path)) hashes[scene.path] = NativeVoiceModelFiles.Hash(File.ReadAllBytes(scene.path));
            return new SceneSnapshot { setup = EditorSceneManager.GetSceneManagerSetup(), handles = scenes.Select(s => s.handle).ToArray(),
                roots = scenes.SelectMany(s => s.GetRootGameObjects()).Select(g => g.GetInstanceID()).OrderBy(id => id).ToArray(),
                dirty = scenes.Select(s => s.isDirty).ToArray(), hashes = hashes };
        }
        static bool SameSceneState(SceneSnapshot a, SceneSnapshot b)
        {
            return a.handles.SequenceEqual(b.handles) && a.roots.SequenceEqual(b.roots) && a.dirty.SequenceEqual(b.dirty) &&
                a.setup.Select(s => s.path + "|" + s.isLoaded + "|" + s.isActive).SequenceEqual(b.setup.Select(s => s.path + "|" + s.isLoaded + "|" + s.isActive)) &&
                a.hashes.Count == b.hashes.Count && a.hashes.All(pair => b.hashes.TryGetValue(pair.Key, out string value) && value == pair.Value);
        }
    }
}
#endif
