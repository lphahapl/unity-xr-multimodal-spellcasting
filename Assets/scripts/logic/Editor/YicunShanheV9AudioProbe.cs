#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Temporary, task-scoped probe. No scene or player asset is saved by this class.
[InitializeOnLoad]
public static class YicunShanheV9AudioProbe
{
    const string AssetPath = "Assets/Audio/kaogong_yicun_shanhe_ambient_loop.wav";
    const string ExpectedGuid = "ad629548de0b8db45a1a011d0f5dcd53";
    const string Key = "NarutoVR.YicunV9AudioProbe.";
    static string Folder => Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/yicun-shanhe-pedalboard-v9"));
    static string RequestPath => Path.Combine(Folder, "unity-probe.request.json");
    static string ResultPath => Path.Combine(Folder, "unity-probe-result.json");
    static string Phase { get => SessionState.GetString(Key + "phase", ""); set => SessionState.SetString(Key + "phase", value); }
    static double nextPoll, nextSample, scheduledDsp, deadline, lastAdvanceAt;
    static AudioSource source;
    static GameObject testRoot;
    static RunReport run;
    static Request request;
    static System.Diagnostics.Stopwatch runtimeWatch;
    static readonly float[] left = new float[1024], right = new float[1024];

    [Serializable] public sealed class Request
    {
        public string action, requestId, expectedSha256;
        public double expectedDurationSeconds;
        public float volume = .28f;
    }
    [Serializable] public sealed class SceneInfo { public string path; public bool isLoaded, isActive; }
    [Serializable] public sealed class Snapshot { public List<SceneInfo> scenes = new List<SceneInfo>(); public bool blank; public string playModeStartScene; }
    [Serializable] public sealed class AssetInfo
    {
        public string path, guid, sha256, loadType, compression, loadState;
        public int samples, frequency, channels, sampleRateOverride;
        public double duration;
        public bool forceToMono, loadInBackground, preloadAudioData;
    }
    [Serializable] public sealed class Status
    {
        public string utc, action, requestId, phase, detail, scene;
        public bool passed, playing, dirty, compiling, updating, editorPaused, applicationFocused, applicationRunInBackground;
        public AssetInfo asset;
    }
    [Serializable] public sealed class Sample
    {
        public double editorTime, dspTime, outputRms, outputPeak, stopwatchElapsedSeconds;
        public int timeSamples, wraps;
        public float volume, listenerVolume;
        public bool isPlaying, loop, muted, listenerPaused, editorPaused, applicationFocused, applicationRunInBackground;
    }
    [Serializable] public sealed class RuntimeObservation
    {
        public string utc;
        public double editorTime, dspTime, deadlineEditorTime, previousEditorTime, previousDspTime, stopwatchElapsedSeconds;
        public int timeSamples, previousTimeSamples, outputSampleRate, dspBufferSize;
        public bool playing, editorPaused, applicationFocused, applicationRunInBackground, listenerPaused, sourcePlaying;
    }
    [Serializable] public sealed class AudioConfigurationEvent
    {
        public string utc;
        public double editorTime, dspTime;
        public bool deviceWasChanged;
        public int sampleRate, dspBufferSize;
    }
    [Serializable] public sealed class Boundary { public double dspTime, editorTime; public int previousSample, currentSample; }
    [Serializable] public sealed class RunReport
    {
        public string requestId, startedUtc, finishedUtc, status, detail, restorationDetail;
        public bool passed, sceneRestored, originalRunInBackground, isIsolatedScene = true;
        public bool physicalListeningPerformed = false;
        public AssetInfo asset;
        public Snapshot originalSceneSetup, restoredSceneSetup;
        public float requestedVolume, maxVolumeDeviation;
        public int requestedStartSample, outputSampleRate, dspBufferSize, nonzeroOutputSamples;
        public double fullLoopDspSeconds, maxPollGapSeconds, firstScheduledDsp;
        public double initialEditorTime, deadlineEditorTime, finalStopwatchElapsedSeconds;
        public RuntimeObservation failureObservation;
        public List<AudioConfigurationEvent> audioConfigurationEvents = new List<AudioConfigurationEvent>();
        public string evidenceLimit = "Engine AudioSource samples and timing only; no physical-device recording or listening. This isolated source tests the imported clip at P1 nominal gain, not the P1 combat/voice lifecycle.";
        public List<Sample> samples = new List<Sample>();
        public List<Boundary> boundaries = new List<Boundary>();
    }

    static YicunShanheV9AudioProbe()
    {
        EditorApplication.update += Poll;
        EditorApplication.playModeStateChanged += StateChanged;
        AudioSettings.OnAudioConfigurationChanged += deviceWasChanged =>
        {
            if (Phase != "running" || run == null) return;
            var config = AudioSettings.GetConfiguration();
            run.audioConfigurationEvents.Add(new AudioConfigurationEvent { utc = Utc(),
                editorTime = EditorApplication.timeSinceStartup, dspTime = AudioSettings.dspTime,
                deviceWasChanged = deviceWasChanged, sampleRate = config.sampleRate, dspBufferSize = config.dspBufferSize });
        };
        EditorApplication.delayCall += () =>
        {
            if (Phase == "finishing" && !EditorApplication.isPlayingOrWillChangePlaymode) Restore();
            else if (Phase == "running" && EditorApplication.isPlaying && !source)
                Finish(false, "Unexpected domain reload interrupted the active probe.");
        };
    }
    static string Utc() => DateTimeOffset.Now.ToString("O");
    static string Hash(string path)
    {
        using (var stream = File.OpenRead(path)) using (var sha = SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
    }
    static void Write(string path, object value)
    {
        Directory.CreateDirectory(Folder);
        string temp = path + ".tmp";
        File.WriteAllText(temp, JsonUtility.ToJson(value, true));
        if (File.Exists(path)) File.Delete(path);
        File.Move(temp, path);
    }
    static AssetInfo InspectAsset()
    {
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetPath);
        var importer = AssetImporter.GetAtPath(AssetPath) as AudioImporter;
        if (!clip || !importer) throw new InvalidOperationException("AudioClip or AudioImporter missing.");
        var settings = importer.defaultSampleSettings;
        return new AssetInfo { path = AssetPath, guid = AssetDatabase.AssetPathToGUID(AssetPath),
            sha256 = Hash(AssetPath), samples = clip.samples, frequency = clip.frequency, channels = clip.channels,
            duration = (double)clip.samples / clip.frequency, loadType = settings.loadType.ToString(),
            compression = settings.compressionFormat.ToString(), loadState = clip.loadState.ToString(),
            sampleRateOverride = (int)settings.sampleRateOverride, preloadAudioData = settings.preloadAudioData,
            forceToMono = importer.forceToMono, loadInBackground = importer.loadInBackground };
    }
    static void Validate(AssetInfo asset, Request req)
    {
        if (string.IsNullOrEmpty(req.requestId) || string.IsNullOrEmpty(req.expectedSha256) || req.expectedSha256.Length != 64)
            throw new InvalidOperationException("requestId and expectedSha256 are required for import/start.");
        if (asset.guid != ExpectedGuid || !string.Equals(asset.sha256, req.expectedSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Asset GUID or expected SHA256 mismatch.");
        if (asset.loadType != "Streaming" || asset.compression != "PCM" || asset.channels != 2 || asset.forceToMono)
            throw new InvalidOperationException("Expected stereo PCM / Streaming importer settings.");
        if (req.expectedDurationSeconds <= 0 || Math.Abs(asset.duration - req.expectedDurationSeconds) > 2d / asset.frequency)
            throw new InvalidOperationException("Imported duration does not match the requested duration.");
        if (req.volume <= 0 || req.volume > 1) throw new InvalidOperationException("Audible volume must be in (0, 1].");
    }
    static bool AnyDirty()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++) if (SceneManager.GetSceneAt(i).isDirty) return true;
        return false;
    }
    static Snapshot Capture()
    {
        var snapshot = new Snapshot { playModeStartScene = AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene) };
        foreach (var scene in EditorSceneManager.GetSceneManagerSetup())
            snapshot.scenes.Add(new SceneInfo { path = scene.path, isLoaded = scene.isLoaded, isActive = scene.isActive });
        snapshot.blank = snapshot.scenes.Count == 1 && string.IsNullOrEmpty(snapshot.scenes[0].path);
        if (!snapshot.blank && snapshot.scenes.Any(s => string.IsNullOrEmpty(s.path)))
            throw new InvalidOperationException("Cannot safely restore multiple scenes containing an untitled scene.");
        return snapshot;
    }
    static void StatusFor(Request req, bool passed, string detail)
    {
        var status = new Status { utc = Utc(), action = req.action, requestId = req.requestId,
            phase = Phase, passed = passed, detail = detail, playing = EditorApplication.isPlaying,
            dirty = AnyDirty(), compiling = EditorApplication.isCompiling, updating = EditorApplication.isUpdating,
            editorPaused = EditorApplication.isPaused, applicationFocused = Application.isFocused,
            applicationRunInBackground = Application.runInBackground,
            scene = SceneManager.GetActiveScene().path };
        try { status.asset = InspectAsset(); } catch (Exception e) { status.detail += "\n" + e.Message; }
        Write(Path.Combine(Folder, "unity-probe-status.json"), status);
    }
    static void Poll()
    {
        if (Phase == "running")
        {
            try { SampleRuntime(); } catch (Exception e) { Finish(false, e.ToString()); }
        }
        if (EditorApplication.timeSinceStartup < nextPoll || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        nextPoll = EditorApplication.timeSinceStartup + .25;
        if (!File.Exists(RequestPath)) return;
        string payload;
        try { payload = File.ReadAllText(RequestPath); File.Delete(RequestPath); }
        catch (IOException) { return; }
        Request req = null;
        try
        {
            req = JsonUtility.FromJson<Request>(payload);
            if (req == null) throw new InvalidOperationException("Invalid request JSON.");
            switch (req.action)
            {
                case "status": StatusFor(req, true, "Read-only status; no Play transition requested."); break;
                case "import":
                    if (EditorApplication.isPlayingOrWillChangePlaymode || !string.IsNullOrEmpty(Phase)) throw new InvalidOperationException("Import requires idle edit mode.");
                    if (!string.Equals(Hash(AssetPath), req.expectedSha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Published file hash mismatch before import.");
                    AssetDatabase.ImportAsset(AssetPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                    Validate(InspectAsset(), req); StatusFor(req, true, "Actual AudioImporter and loaded AudioClip verified."); break;
                case "start": Start(req); break;
                case "cancel":
                    if (!string.IsNullOrEmpty(Phase)) Finish(false, "Cancelled by task owner.");
                    StatusFor(req, true, "Cancellation requested, or probe already idle."); break;
                default: throw new InvalidOperationException("Unknown probe command.");
            }
        }
        catch (Exception e) { StatusFor(req ?? new Request { action = "invalid" }, false, e.ToString()); }
    }
    static void Start(Request req)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || !string.IsNullOrEmpty(Phase) || AnyDirty())
            throw new InvalidOperationException("Start requires idle edit mode and no unsaved scene changes.");
        Validate(InspectAsset(), req);
        Snapshot snapshot = Capture();
        request = req;
        run = new RunReport { requestId = req.requestId, startedUtc = Utc(), status = "armed",
            requestedVolume = req.volume, originalSceneSetup = snapshot, asset = InspectAsset() };
        Write(ResultPath, run);
        SessionState.SetString(Key + "snapshot", JsonUtility.ToJson(snapshot));
        SessionState.SetString(Key + "request", JsonUtility.ToJson(req));
        Phase = "armed";
        try
        {
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            StatusFor(req, true, "Armed isolated unsaved scene; waiting for Play entry.");
            EditorApplication.isPlaying = true;
        }
        catch (Exception e) { Finish(false, e.ToString()); }
    }
    static void StateChanged(PlayModeStateChange state)
    {
        if (string.IsNullOrEmpty(Phase)) return;
        if (state == PlayModeStateChange.EnteredPlayMode && Phase == "armed")
        {
            try { BeginRuntime(); } catch (Exception e) { Finish(false, e.ToString()); }
        }
        if (state == PlayModeStateChange.ExitingPlayMode)
        {
            if (Phase == "running") Finish(false, "Play mode stopped before two natural boundaries were recorded.");
            Application.runInBackground = SessionState.GetBool(Key + "background", Application.runInBackground);
        }
        if (state == PlayModeStateChange.EnteredEditMode && !string.IsNullOrEmpty(Phase)) Restore();
    }
    static void BeginRuntime()
    {
        request = JsonUtility.FromJson<Request>(SessionState.GetString(Key + "request", ""));
        var asset = InspectAsset(); Validate(asset, request);
        var config = AudioSettings.GetConfiguration();
        run = new RunReport { requestId = request.requestId, startedUtc = Utc(), status = "running", asset = asset,
            requestedVolume = request.volume, originalRunInBackground = Application.runInBackground,
            originalSceneSetup = JsonUtility.FromJson<Snapshot>(SessionState.GetString(Key + "snapshot", "")),
            outputSampleRate = config.sampleRate, dspBufferSize = config.dspBufferSize };
        SessionState.SetBool(Key + "background", Application.runInBackground);
        Application.runInBackground = true;
        testRoot = new GameObject("Yicun v9 isolated audio probe") { hideFlags = HideFlags.DontSave };
        testRoot.AddComponent<AudioListener>();
        source = testRoot.AddComponent<AudioSource>();
        source.playOnAwake = false; source.loop = true; source.spatialBlend = 0; source.dopplerLevel = 0;
        source.volume = request.volume; source.pitch = 1;
        source.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetPath);
        run.requestedStartSample = Math.Max(0, asset.samples - 2 * asset.frequency);
        source.timeSamples = run.requestedStartSample;
        scheduledDsp = AudioSettings.dspTime + .25;
        run.firstScheduledDsp = scheduledDsp;
        source.PlayScheduled(scheduledDsp);
        nextSample = EditorApplication.timeSinceStartup;
        deadline = nextSample + asset.duration + 35;
        run.initialEditorTime = nextSample; run.deadlineEditorTime = deadline;
        runtimeWatch = System.Diagnostics.Stopwatch.StartNew();
        lastAdvanceAt = nextSample;
        Phase = "running";
        Write(ResultPath, run);
        StatusFor(request, true, "Isolated AudioSource started near tail; waiting for two natural wraps.");
    }
    static void SampleRuntime()
    {
        double now = EditorApplication.timeSinceStartup;
        if (now > deadline) { Finish(false, "Runtime timeout before the required complete loop."); return; }
        if (!source || run == null || run.asset == null || run.asset.path != AssetPath) { Finish(false, "Probe source/state lost."); return; }
        if (now < nextSample || AudioSettings.dspTime < scheduledDsp + .05) return;
        nextSample = now + .1;
        var sample = new Sample { editorTime = now, dspTime = AudioSettings.dspTime, timeSamples = source.timeSamples,
            stopwatchElapsedSeconds = runtimeWatch?.Elapsed.TotalSeconds ?? -1,
            volume = source.volume, listenerVolume = AudioListener.volume, isPlaying = source.isPlaying,
            loop = source.loop, muted = source.mute, listenerPaused = AudioListener.pause,
            editorPaused = EditorApplication.isPaused, applicationFocused = Application.isFocused,
            applicationRunInBackground = Application.runInBackground };
        if (!sample.isPlaying || !sample.loop || sample.listenerPaused || sample.muted)
        { Finish(false, "Source stopped, loop disabled, or audio is paused/muted."); return; }
        source.GetOutputData(left, 0); source.GetOutputData(right, 1);
        double power = 0, peak = 0;
        for (int i = 0; i < left.Length; i++)
        { power += left[i] * left[i] + right[i] * right[i]; peak = Math.Max(peak, Math.Max(Math.Abs(left[i]), Math.Abs(right[i]))); }
        sample.outputRms = Math.Sqrt(power / (left.Length * 2)); sample.outputPeak = peak;
        if (peak > 1e-7) run.nonzeroOutputSamples++;
        run.maxVolumeDeviation = Math.Max(run.maxVolumeDeviation, Math.Abs(sample.volume - request.volume));
        if (run.samples.Count == 0)
        {
            if (sample.timeSamples < run.requestedStartSample - run.asset.frequency)
            { Finish(false, "Playback did not start near the requested tail position."); return; }
        }
        else
        {
            var previous = run.samples[run.samples.Count - 1];
            run.maxPollGapSeconds = Math.Max(run.maxPollGapSeconds, now - previous.editorTime);
            if (sample.dspTime <= previous.dspTime) { Finish(false, "DSP clock did not advance."); return; }
            if (sample.timeSamples != previous.timeSamples) lastAdvanceAt = now;
            if (now - lastAdvanceAt > 3) { Finish(false, "Audio sample cursor stalled."); return; }
            if (sample.timeSamples < previous.timeSamples)
            {
                if (previous.timeSamples < run.asset.samples * .75 || sample.timeSamples > run.asset.samples * .25)
                { Finish(false, "Unexpected sample cursor reversal away from the loop boundary."); return; }
                run.boundaries.Add(new Boundary { dspTime = sample.dspTime, editorTime = now,
                    previousSample = previous.timeSamples, currentSample = sample.timeSamples });
            }
        }
        sample.wraps = run.boundaries.Count; run.samples.Add(sample);
        if (run.samples.Count % 50 == 0) Write(ResultPath, run);
        if (run.boundaries.Count >= 2)
        {
            run.fullLoopDspSeconds = run.boundaries[1].dspTime - run.boundaries[0].dspTime;
            bool passed = Math.Abs(run.fullLoopDspSeconds - run.asset.duration) <= .5 && run.maxPollGapSeconds < 2 &&
                run.maxVolumeDeviation < .0001 && run.nonzeroOutputSamples > 0;
            Finish(passed, passed ? "Two natural wraps recorded: initial tail passage plus one full loop. Engine output is nonzero and nominal volume is preserved." : "Boundary timing, polling continuity, output energy, or volume check failed.");
        }
    }
    static void Finish(bool passed, string detail)
    {
        if (run == null && File.Exists(ResultPath))
        {
            try { run = JsonUtility.FromJson<RunReport>(File.ReadAllText(ResultPath)); } catch { }
        }
        if (run == null) run = new RunReport { requestId = request?.requestId, startedUtc = Utc() };
        if (!passed)
        {
            var config = AudioSettings.GetConfiguration();
            var previous = run.samples.Count > 0 ? run.samples[run.samples.Count - 1] : null;
            run.failureObservation = new RuntimeObservation { utc = Utc(), editorTime = EditorApplication.timeSinceStartup,
                stopwatchElapsedSeconds = runtimeWatch?.Elapsed.TotalSeconds ?? -1,
                dspTime = AudioSettings.dspTime, deadlineEditorTime = deadline,
                previousEditorTime = previous?.editorTime ?? -1, previousDspTime = previous?.dspTime ?? -1,
                timeSamples = source ? source.timeSamples : -1, previousTimeSamples = previous?.timeSamples ?? -1,
                outputSampleRate = config.sampleRate, dspBufferSize = config.dspBufferSize,
                playing = EditorApplication.isPlaying, editorPaused = EditorApplication.isPaused,
                applicationFocused = Application.isFocused, applicationRunInBackground = Application.runInBackground,
                listenerPaused = AudioListener.pause, sourcePlaying = source && source.isPlaying };
        }
        run.finalStopwatchElapsedSeconds = runtimeWatch?.Elapsed.TotalSeconds ?? -1;
        run.passed = passed; run.status = passed ? "playback_passed_restoration_pending" : "failed_restoration_pending";
        run.detail = detail; run.finishedUtc = Utc();
        Write(ResultPath, run); Phase = "finishing";
        if (source) source.Stop();
        if (testRoot) UnityEngine.Object.Destroy(testRoot);
        Application.runInBackground = SessionState.GetBool(Key + "background", Application.runInBackground);
        if (EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.isPlaying = false;
        else Restore();
    }
    static void Restore()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        try
        {
            var snapshot = JsonUtility.FromJson<Snapshot>(SessionState.GetString(Key + "snapshot", ""));
            if (snapshot == null) throw new InvalidOperationException("Original scene snapshot missing.");
            if (snapshot.blank) EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            else EditorSceneManager.RestoreSceneManagerSetup(snapshot.scenes.Select(s => new SceneSetup { path = s.path, isLoaded = s.isLoaded, isActive = s.isActive }).ToArray());
            EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(snapshot.playModeStartScene) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(snapshot.playModeStartScene);
            if (run == null && File.Exists(ResultPath)) run = JsonUtility.FromJson<RunReport>(File.ReadAllText(ResultPath));
            if (run == null) run = new RunReport { passed = false, detail = "Probe ended before runtime sampling." };
            run.originalSceneSetup = snapshot; run.restoredSceneSetup = Capture();
            run.sceneRestored = !AnyDirty() && JsonUtility.ToJson(snapshot) == JsonUtility.ToJson(run.restoredSceneSetup);
            run.restorationDetail = run.sceneRestored ? "Original scene setup and playModeStartScene restored; no scene was saved." : "Scene snapshot mismatch after restore.";
            run.passed &= run.sceneRestored;
            run.status = run.passed ? "passed" : "failed"; run.finishedUtc = Utc();
            Write(ResultPath, run);
            Phase = "";
            SessionState.EraseString(Key + "snapshot"); SessionState.EraseString(Key + "request");
            SessionState.EraseBool(Key + "background");
        }
        catch (Exception e)
        {
            if (run == null) run = new RunReport();
            run.passed = false; run.status = "restore_failed"; run.restorationDetail = e.ToString();
            Write(ResultPath, run);
        }
    }
}
#endif
