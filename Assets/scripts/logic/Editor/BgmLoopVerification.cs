#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using NarutoVR.SealCombat.Rpg;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// One owner controls Unity. This entry never saves a scene or changes player settings.
[InitializeOnLoad]
public static class BgmLoopVerification
{
    const string Key = "NarutoVR.BgmLoopVerification.v11";
    const string RootName = "__BgmLoopVerification_";
    const string EmptyScenePath = "Assets/Scene/BgmLoopVerificationEmpty.unity";
    static string Root => Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/yicun-shanhe-continuity-v11"));
    static string RequestPath => Path.Combine(Root, "runtime-request.json");
    static Session session;
    static Report run;
    static AudioSource source;
    static EditorBgmAudioCapture capture;
    static GameObject ownedRoot;
    static AudioListener ownListener;
    static readonly List<AudioListener> disabledListeners = new List<AudioListener>();
    static Stopwatch watch;
    static double nextPoll, nextSample, nextSave;
    static bool finishing, recoveryPending;
    static Func<bool> xrQuitObserver, xrRestartObserver;
    static EventInfo xrQuitEvent, xrRestartEvent;

    [Serializable] public sealed class Request
    {
        public string action, requestId, expectedSha256, expectedGuid, submittedUtc, submittedBy;
        public int submitterPid, fullLoops = 2;
        public string assetPath = "Assets/Audio/kaogong_yicun_shanhe_ambient_loop.wav";
        public double expectedDurationSeconds, diagnosticSeconds, injectReloadAtSeconds = -1;
        public float volume = .28f;
        public bool lockReload = true, capture = true;
    }
    [Serializable] public sealed class SceneInfo { public string path; public bool isLoaded, isActive, isDirty; }
    [Serializable] public sealed class Session
    {
        public Request request;
        public string phase, originalStartScene, rootObjectName;
        public bool blankScene, ownsReloadLock, ownsRefreshLock, originalBackground;
        public List<SceneInfo> scenes = new List<SceneInfo>();
        public List<int> disabledListenerIds = new List<int>();
    }
    [Serializable] public sealed class AssetInfo
    {
        public string path, sha256, guid, loadType, compression, loadState;
        public int samples, frequency, channels;
        public double durationSeconds;
        public bool forceToMono, loadInBackground, preloadAudioData;
    }
    [Serializable] public sealed class ListenerInfo
    {
        public string name, scene, hideFlags;
        public int instanceId;
        public bool enabled, active;
    }
    [Serializable] public sealed class MainSample
    {
        public double editorTime, stopwatchSeconds, dspTime;
        public int cursor, wraps, callbackBuffers;
        public float volume, listenerVolume;
        public bool sourcePlaying, editorPaused, listenerPaused, focused;
    }
    [Serializable] public sealed class EventRecord
    {
        public string utc, requestId, kind, detail, stack;
        public int editorPid;
        public double editorTime, stopwatchSeconds, dspTime;
    }
    [Serializable] public sealed class Report
    {
        public string requestId, status, reason, utc, startedUtc, runtimeEndedUtc, restoredUtc;
        public string captureWav, captureMetadata, bufferTimeline, evidenceLimit;
        public bool complete, passed, diagnosticPassed, fullLoopCompleted, sceneRestored, locksReleased;
        public bool diagnostic, injectionRequested, interrupted, captureReady;
        public int wraps, requiredWraps, identicalMainDspSnapshots, capturedFrames, callbackBuffers, captureErrorFlags;
        public int outputSampleRate, outputChannels = 2, dspBufferSize;
        public double clipSeconds, targetSeconds, scheduledDsp, initialEditorTime, endStopwatchSeconds;
        public double maxMainPollGap, maxCallbackWallOverrun, maxCallbackDspExcess, outputPeak;
        public float nominalVolume, maxVolumeDeviation;
        public AssetInfo asset;
        public Request request;
        public MainSample failureSnapshot;
        public List<SceneInfo> originalScenes, restoredScenes;
        public List<ListenerInfo> listenersBefore = new List<ListenerInfo>();
        public List<ListenerInfo> unexpectedListeners = new List<ListenerInfo>();
        public List<MainSample> mainSamples = new List<MainSample>();
        public List<EventRecord> events = new List<EventRecord>();
    }
    [Serializable] public sealed class Response
    {
        public string requestId, status, detail, utc, scene, consoleFlags, runResult;
        public bool complete = true, passed, playing, dirty, editorPaused, compiling, updating;
        public AssetInfo asset;
        public List<ListenerInfo> listeners;
    }
    [Serializable] sealed class Timeline
    {
        public int sampleRate, channels, bufferCount, capturedFrames;
        public long stopwatchOriginTicks, stopwatchFrequency;
        public EditorBgmAudioCapture.BufferStamp[] buffers;
    }
    [Serializable] sealed class CaptureMetadata
    {
        public string requestId, sourceHash, stage, wavFormat = "IEEE float32, format tag 3";
        public int sampleRate, channels, frames;
        public double sourceScheduledDsp, sourceDurationSeconds;
        public float sourceVolume;
    }
    static string RunFolder => Path.Combine(Root, "runtime-runs", session.request.requestId);
    static string ResultPath => Path.Combine(RunFolder, "result.json");
    static string Now() => DateTimeOffset.Now.ToString("O");
    static bool Active => session != null && session.phase == "running";

    static BgmLoopVerification()
    {
        string saved = SessionState.GetString(Key, "");
        if (!string.IsNullOrEmpty(saved))
        {
            try { session = JsonUtility.FromJson<Session>(saved); recoveryPending = session != null; }
            catch { session = null; }
        }
        EditorApplication.update += Poll;
        EditorApplication.playModeStateChanged += PlayState;
        EditorApplication.pauseStateChanged += state => Event("editor_pause", state.ToString());
        AssemblyReloadEvents.beforeAssemblyReload += BeforeReload;
        CompilationPipeline.compilationStarted += context => Event("compilation_started", Convert.ToString(context));
        CompilationPipeline.compilationFinished += context => Event("compilation_finished", Convert.ToString(context));
        AudioSettings.OnAudioConfigurationChanged += deviceChanged =>
        {
            Event("audio_configuration_changed", "deviceChanged=" + deviceChanged + "; rate=" + AudioSettings.outputSampleRate);
            if (Active) Finish(false, "Audio output configuration changed during capture.", true);
        };
        EditorApplication.quitting += () => { if (session != null) { Event("editor_quitting", "Editor is closing."); ReleaseLocks(); } };
        EditorApplication.delayCall += Recover;
    }
    static void SaveSession() { if (session != null) SessionState.SetString(Key, JsonUtility.ToJson(session)); }
    static void AtomicJson(string path, object value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        string temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonUtility.ToJson(value, true));
        if (File.Exists(path)) File.Delete(path);
        File.Move(temporary, path);
    }
    static void Event(string kind, string detail)
    {
        if (session == null) return;
        var item = new EventRecord { utc = Now(), requestId = session.request.requestId, kind = kind,
            detail = detail, editorPid = Process.GetCurrentProcess().Id, editorTime = EditorApplication.timeSinceStartup,
            stopwatchSeconds = watch?.Elapsed.TotalSeconds ?? -1, dspTime = AudioSettings.dspTime, stack = Environment.StackTrace };
        run?.events.Add(item);
        try { Directory.CreateDirectory(RunFolder); File.AppendAllText(Path.Combine(RunFolder, "events.jsonl"), JsonUtility.ToJson(item) + "\n"); }
        catch (IOException) { }
    }
    static string Hash(string path)
    {
        using (var stream = File.OpenRead(path)) using (var sha = SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
    }
    static AssetInfo Inspect(Request request)
    {
        if (string.IsNullOrEmpty(request.assetPath) || !request.assetPath.StartsWith("Assets/", StringComparison.Ordinal) || request.assetPath.Contains(".."))
            throw new InvalidOperationException("An asset path inside Assets is required.");
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(request.assetPath);
        var importer = AssetImporter.GetAtPath(request.assetPath) as AudioImporter;
        if (!clip || !importer) throw new InvalidOperationException("AudioClip/importer is unavailable.");
        var settings = importer.defaultSampleSettings;
        return new AssetInfo { path = request.assetPath, sha256 = Hash(request.assetPath), guid = AssetDatabase.AssetPathToGUID(request.assetPath),
            samples = clip.samples, frequency = clip.frequency, channels = clip.channels, durationSeconds = (double)clip.samples / clip.frequency,
            loadType = settings.loadType.ToString(), compression = settings.compressionFormat.ToString(), loadState = clip.loadState.ToString(),
            forceToMono = importer.forceToMono, loadInBackground = importer.loadInBackground, preloadAudioData = settings.preloadAudioData };
    }
    static void Validate(Request request, AssetInfo asset)
    {
        if (!Regex.IsMatch(request.expectedSha256 ?? "", "\\A[0-9a-fA-F]{64}\\z") || !string.Equals(asset.sha256, request.expectedSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Published audio SHA256 mismatch.");
        if (!string.IsNullOrEmpty(request.expectedGuid) && asset.guid != request.expectedGuid) throw new InvalidOperationException("GUID mismatch.");
        if (asset.channels != 2 || asset.loadType != "Streaming" || asset.compression != "PCM" || asset.forceToMono)
            throw new InvalidOperationException("Expected stereo PCM Streaming audio.");
        if (request.expectedDurationSeconds > 0 && Math.Abs(asset.durationSeconds - request.expectedDurationSeconds) > 2d / asset.frequency)
            throw new InvalidOperationException("Duration mismatch.");
        if (request.volume <= 0 || request.volume > 1 || request.fullLoops < 1 || request.fullLoops > 16 || request.diagnosticSeconds < 0)
            throw new InvalidOperationException("Invalid volume, loop count or diagnostic duration.");
    }
    static bool Dirty()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++) if (SceneManager.GetSceneAt(i).isDirty) return true;
        return false;
    }
    static List<SceneInfo> Scenes()
    {
        var setup = EditorSceneManager.GetSceneManagerSetup();
        var result = new List<SceneInfo>();
        for (int i = 0; i < setup.Length; i++)
        {
            var item = setup[i];
            var scene = string.IsNullOrEmpty(item.path) && i < SceneManager.sceneCount
                ? SceneManager.GetSceneAt(i) : SceneManager.GetSceneByPath(item.path);
            result.Add(new SceneInfo { path = item.path, isLoaded = item.isLoaded, isActive = item.isActive,
                isDirty = scene.IsValid() && scene.isDirty });
        }
        return result;
    }
    static ListenerInfo Listener(AudioListener listener) => new ListenerInfo { name = listener.name, instanceId = listener.GetInstanceID(),
        scene = listener.gameObject.scene.path, hideFlags = listener.gameObject.hideFlags.ToString(), enabled = listener.enabled,
        active = listener.gameObject.activeInHierarchy };
    static AudioListener[] LiveListeners() => Resources.FindObjectsOfTypeAll<AudioListener>().Where(l => l && l.gameObject.scene.IsValid() && l.gameObject.activeInHierarchy && l.enabled).ToArray();
    static string ConsoleFlags()
    {
        try
        {
            var assembly = typeof(EditorApplication).Assembly;
            var type = assembly.GetType("UnityEditor.LogEntries");
            var property = type?.GetProperty("consoleFlags", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            var enumType = assembly.GetType("UnityEditor.ConsoleWindow+ConsoleFlags");
            if (property == null || enumType == null || !enumType.IsEnum) return "Unavailable: flags metadata not found; no bit value guessed.";
            long flags = Convert.ToInt64(property.GetValue(null));
            var names = Enum.GetNames(enumType).Where(name => { long value = Convert.ToInt64(Enum.Parse(enumType, name)); return value != 0 && (flags & value) == value; });
            return flags + ": " + string.Join(",", names);
        }
        catch (Exception e) { return "Unavailable: " + e.Message; }
    }
    static void Respond(Request request, string status, bool passed, string detail, bool complete = true)
    {
        var response = new Response { requestId = request.requestId, status = status, passed = passed, complete = complete,
            detail = detail, utc = Now(), scene = SceneManager.GetActiveScene().path, playing = EditorApplication.isPlaying,
            dirty = Dirty(), editorPaused = EditorApplication.isPaused, compiling = EditorApplication.isCompiling,
            updating = EditorApplication.isUpdating, consoleFlags = ConsoleFlags(), listeners = LiveListeners().Select(Listener).ToList(),
            runResult = Path.Combine(Root, "runtime-runs", request.requestId, "result.json") };
        try { response.asset = Inspect(request); } catch (Exception e) { response.detail += "\n" + e.Message; }
        AtomicJson(Path.Combine(Root, "runtime-responses", request.requestId + ".json"), response);
    }
    static void Poll()
    {
        if (recoveryPending) { Recover(); return; }
        if (Active)
        {
            // Never evaluate a reset/default deadline before checking runtime ownership.
            if (run == null || !source || !capture || watch == null) { InterruptLostRuntime(); return; }
            try { Observe(); } catch (Exception e) { Finish(false, e.ToString(), true); }
        }
        if (EditorApplication.timeSinceStartup < nextPoll || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        nextPoll = EditorApplication.timeSinceStartup + .25;
        if (!File.Exists(RequestPath)) return;
        string text;
        try { text = File.ReadAllText(RequestPath); File.Delete(RequestPath); } catch (IOException) { return; }
        Request request = null;
        try
        {
            request = JsonUtility.FromJson<Request>(text);
            if (request == null || !Regex.IsMatch(request.requestId ?? "", "\\A[A-Za-z0-9][A-Za-z0-9_.-]{0,95}\\z")) throw new InvalidOperationException("Invalid request ID.");
            if (request.action == "status") { Respond(request, "status", true, "Read-only editor status."); return; }
            if (session != null || EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Another run or Play transition owns the editor.");
            if (request.action == "import")
            {
                if (!string.Equals(Hash(request.assetPath), request.expectedSha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Hash mismatch before import.");
                AssetDatabase.ImportAsset(request.assetPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                Validate(request, Inspect(request)); Respond(request, "imported", true, "Actual imported clip and importer verified."); return;
            }
            if (request.action != "run") throw new InvalidOperationException("Unknown command.");
            Start(request);
        }
        catch (Exception e)
        {
            if (request != null && Regex.IsMatch(request.requestId ?? "", "\\A[A-Za-z0-9][A-Za-z0-9_.-]{0,95}\\z")) Respond(request, "rejected", false, e.ToString());
        }
    }
    static void Start(Request request)
    {
        if (EditorApplication.isPaused) throw new InvalidOperationException("Unpaused edit mode is required.");
        var emptyScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(EmptyScenePath);
        if (!emptyScene) throw new InvalidOperationException("The dedicated empty verification scene has not been imported.");
        AssetInfo asset = Inspect(request); Validate(request, asset);
        string folder = Path.Combine(Root, "runtime-runs", request.requestId);
        if (Directory.Exists(folder)) throw new InvalidOperationException("Run ID already exists; poll its result instead.");
        var scenes = Scenes(); bool blank = scenes.Count == 1 && string.IsNullOrEmpty(scenes[0].path);
        if (!blank && scenes.Any(s => string.IsNullOrEmpty(s.path))) throw new InvalidOperationException("Cannot restore mixed untitled scenes safely.");
        session = new Session { request = request, phase = "armed", scenes = scenes, blankScene = blank,
            originalStartScene = AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene), rootObjectName = RootName + request.requestId,
            originalBackground = Application.runInBackground };
        run = new Report { requestId = request.requestId, request = request, asset = asset, status = "armed", utc = Now(),
            clipSeconds = asset.durationSeconds, nominalVolume = request.volume, requiredWraps = request.fullLoops,
            diagnostic = request.diagnosticSeconds > 0, targetSeconds = request.diagnosticSeconds > 0 ? request.diagnosticSeconds : asset.durationSeconds * request.fullLoops + 1,
            originalScenes = scenes, evidenceLimit = "Audio-thread source filter capture plus runtime timing. Physical listening/headset mix and offline PCM comparison are separate acceptance steps." };
        SaveSession(); AtomicJson(ResultPath, run); Event("request_accepted", JsonUtility.ToJson(request));
        try
        {
            // Unity preserves the current editor scenes, including their in-memory
            // unsaved changes, while entering Play with this separate SceneAsset.
            EditorSceneManager.playModeStartScene = emptyScene;
            Respond(request, "armed", true, "Dedicated empty Play scene selected; open editor scenes and dirty state are retained.", false);
            EditorApplication.isPlaying = true;
        }
        catch (Exception e) { Finish(false, e.ToString(), true); }
    }
    static void PlayState(PlayModeStateChange state)
    {
        if (session == null) return;
        Event("play_state", state.ToString());
        if (state == PlayModeStateChange.EnteredPlayMode && session.phase == "armed")
        { try { BeginRuntime(); } catch (Exception e) { Finish(false, e.ToString(), true); } }
        else if (state == PlayModeStateChange.ExitingPlayMode && Active)
            Finish(false, "Play exited before the requested capture completed; initiator not inferred from this callback.", true, false);
        else if (state == PlayModeStateChange.EnteredEditMode) Restore();
    }
    static void BeginRuntime()
    {
        recoveryPending = false;
        if (run == null) run = JsonUtility.FromJson<Report>(File.ReadAllText(ResultPath));
        if (SceneManager.sceneCount != 1 || SceneManager.GetActiveScene().path != EmptyScenePath)
            throw new InvalidOperationException("Unity did not enter the dedicated empty verification scene.");
        Validate(session.request, Inspect(session.request));
        if (session.request.lockReload)
        {
            EditorApplication.LockReloadAssemblies(); session.ownsReloadLock = true; SaveSession();
            AssetDatabase.DisallowAutoRefresh(); session.ownsRefreshLock = true; SaveSession();
        }
        Application.runInBackground = true;
        run.listenersBefore = LiveListeners().Select(Listener).ToList();
        foreach (var listener in LiveListeners())
        {
            disabledListeners.Add(listener); session.disabledListenerIds.Add(listener.GetInstanceID());
            SaveSession(); listener.enabled = false;
        }
        ownedRoot = new GameObject(session.rootObjectName) { hideFlags = HideFlags.DontSave };
        ownListener = ownedRoot.AddComponent<AudioListener>();
        source = ownedRoot.AddComponent<AudioSource>(); source.playOnAwake = false; source.loop = true;
        source.spatialBlend = 0; source.dopplerLevel = 0; source.pitch = 1; source.volume = session.request.volume;
        source.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(session.request.assetPath);
        var config = AudioSettings.GetConfiguration(); run.outputSampleRate = config.sampleRate; run.dspBufferSize = config.dspBufferSize;
        capture = ownedRoot.AddComponent<EditorBgmAudioCapture>();
        capture.Configure(config.sampleRate, 2, run.targetSeconds + 12, config.dspBufferSize);
        watch = Stopwatch.StartNew(); run.initialEditorTime = EditorApplication.timeSinceStartup;
        run.startedUtc = Now(); run.scheduledDsp = AudioSettings.dspTime + .25;
        run.status = "running"; session.phase = "running"; SaveSession();
        AttachXrObservers(); Event("runtime_begin", "From sample zero, natural looping; no seek or restart. Console flags=" + ConsoleFlags());
        source.PlayScheduled(run.scheduledDsp); nextSample = nextSave = 0;
        AtomicJson(ResultPath, run);
    }
    static void AttachXrObservers()
    {
        try
        {
            var type = Type.GetType("UnityEngine.XR.OpenXR.OpenXRRuntime, Unity.XR.OpenXR");
            xrQuitEvent = type?.GetEvent("wantsToQuit", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            xrRestartEvent = type?.GetEvent("wantsToRestart", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            xrQuitObserver = () => { Event("openxr_wants_to_quit", "Observer returns true; does not suppress quit."); return true; };
            xrRestartObserver = () => { Event("openxr_wants_to_restart", "Observer returns true; does not suppress restart."); return true; };
            xrQuitEvent?.AddEventHandler(null, xrQuitObserver); xrRestartEvent?.AddEventHandler(null, xrRestartObserver);
            Event("openxr_observers", "quit=" + (xrQuitEvent != null) + "; restart=" + (xrRestartEvent != null));
        }
        catch (Exception e) { Event("openxr_observer_unavailable", e.Message); }
    }
    static MainSample Current() => new MainSample { editorTime = EditorApplication.timeSinceStartup,
        stopwatchSeconds = watch?.Elapsed.TotalSeconds ?? -1, dspTime = AudioSettings.dspTime,
        cursor = source ? source.timeSamples : -1, sourcePlaying = source && source.isPlaying, wraps = run?.wraps ?? 0,
        callbackBuffers = capture ? capture.BufferCount : 0, volume = source ? source.volume : -1,
        listenerVolume = AudioListener.volume, editorPaused = EditorApplication.isPaused, listenerPaused = AudioListener.pause, focused = Application.isFocused };
    static void Observe()
    {
        double elapsed = watch.Elapsed.TotalSeconds;
        if (elapsed > run.targetSeconds + 30) { Finish(false, "Independent Stopwatch deadline exceeded.", true); return; }
        if (session.request.injectReloadAtSeconds >= 0 && !run.injectionRequested && elapsed >= session.request.injectReloadAtSeconds)
        { run.injectionRequested = true; Event("controlled_reload_request", "EditorUtility.RequestScriptReload; lockReload=" + session.request.lockReload); EditorUtility.RequestScriptReload(); }
        if (elapsed < nextSample) return;
        nextSample = elapsed + .1;
        MainSample current = Current();
        if (run.mainSamples.Count > 0)
        {
            var previous = run.mainSamples[run.mainSamples.Count - 1];
            run.maxMainPollGap = Math.Max(run.maxMainPollGap, current.stopwatchSeconds - previous.stopwatchSeconds);
            if (current.dspTime == previous.dspTime) run.identicalMainDspSnapshots++;
            if (current.cursor < previous.cursor)
            {
                if (previous.cursor < run.asset.samples * .75 || current.cursor > run.asset.samples * .25)
                { run.mainSamples.Add(current); Finish(false, "Unexpected source cursor reversal away from a boundary.", false); return; }
                run.wraps++;
            }
        }
        current.wraps = run.wraps; run.mainSamples.Add(current);
        run.maxVolumeDeviation = Math.Max(run.maxVolumeDeviation, Math.Abs(current.volume - run.nominalVolume));
        if (current.editorPaused || current.listenerPaused) { Finish(false, "Editor or AudioListener paused the test.", true); return; }
        if (elapsed > 2 && !current.sourcePlaying) { Finish(false, "AudioSource stopped unexpectedly.", true); return; }
        foreach (var listener in LiveListeners()) if (listener != ownListener)
        { run.unexpectedListeners.Add(Listener(listener)); Finish(false, "A foreign active AudioListener appeared during isolation.", true); return; }
        if (capture.ErrorFlags != 0) { Finish(false, "Audio callback capture error flags=" + capture.ErrorFlags, false); return; }
        if (elapsed > 3)
        {
            long last = capture.LastCallbackTicks;
            if (last == 0 || (double)(Stopwatch.GetTimestamp() - last) / Stopwatch.Frequency > 1)
            { Finish(false, "Audio-thread callbacks absent for over one second.", false); return; }
        }
        if (elapsed >= nextSave) { nextSave = elapsed + 5; AtomicJson(ResultPath, run); }
        if (run.diagnostic && elapsed >= run.targetSeconds) { Finish(true, "Diagnostic capture completed; this is not full-loop acceptance.", false); return; }
        if (!run.diagnostic && run.wraps >= run.requiredWraps && current.cursor >= run.asset.frequency &&
            current.dspTime - run.scheduledDsp >= run.targetSeconds - .05)
            Finish(true, "Required natural wraps and post-wrap capture completed.", false);
    }
    static void BeforeReload()
    {
        Event("before_assembly_reload", "ownsReloadLock=" + session?.ownsReloadLock);
        if (Active) Finish(false, "Assembly reload interrupted capture; this is not a playback timeout.", true, false);
    }
    static void InterruptLostRuntime()
    {
        if (run == null && File.Exists(ResultPath)) run = JsonUtility.FromJson<Report>(File.ReadAllText(ResultPath));
        Finish(false, "Runtime ownership was lost across reload; capture cannot resume from static defaults.", true);
    }
    static void Recover()
    {
        if (!recoveryPending) return;
        recoveryPending = false;
        if (session == null) return;
        if (session.phase == "armed") return; // Normal domain reload on entering Play.
        if (session.phase == "running") { InterruptLostRuntime(); return; }
        if (EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.isPlaying = false;
        else Restore();
    }
    static void Finish(bool candidate, string reason, bool interrupted, bool requestStop = true)
    {
        if (session == null || finishing) return;
        finishing = true;
        try
        {
            if (run == null && File.Exists(ResultPath)) run = JsonUtility.FromJson<Report>(File.ReadAllText(ResultPath));
            if (run == null) run = new Report { requestId = session.request.requestId, request = session.request };
            run.failureSnapshot = Current(); run.endStopwatchSeconds = watch?.Elapsed.TotalSeconds ?? -1;
            run.runtimeEndedUtc = Now(); run.reason = reason; run.interrupted = interrupted;
            Event("runtime_finishing", reason);
            EditorBgmAudioCapture.Snapshot data = capture ? capture.Seal() : null;
            if (source) source.Stop();
            bool captureValid = false;
            if (data != null && data.bufferCount > 0)
            {
                run.callbackBuffers = data.bufferCount; run.capturedFrames = data.sampleCount / data.channels; run.captureErrorFlags = data.errorFlags;
                for (int i = 0; i < data.bufferCount; i++)
                {
                    var item = data.buffers[i]; run.outputPeak = Math.Max(run.outputPeak, item.peak);
                    if (i == 0) continue;
                    double previousSeconds = (double)data.buffers[i - 1].frameCount / data.sampleRate;
                    run.maxCallbackWallOverrun = Math.Max(run.maxCallbackWallOverrun, item.stopwatchDelta - previousSeconds);
                    run.maxCallbackDspExcess = Math.Max(run.maxCallbackDspExcess, item.dspDelta - previousSeconds);
                }
                // These limits reject sustained audio-thread stalls. Raw timing is
                // retained for stricter offline examination; a main-thread duplicate is not this signal.
                captureValid = data.errorFlags == 0 && run.outputPeak > 1e-7 && run.maxCallbackWallOverrun <= .25 && run.maxCallbackDspExcess <= .10 &&
                    (double)run.capturedFrames / data.sampleRate >= run.targetSeconds - .5;
                if (session.request.capture) WriteCapture(data);
            }
            run.fullLoopCompleted = !run.diagnostic && run.wraps >= run.requiredWraps && candidate && captureValid;
            run.diagnosticPassed = run.diagnostic && candidate && captureValid;
            run.passed = run.fullLoopCompleted && run.maxVolumeDeviation < .0001;
            if (candidate && !captureValid) run.reason += " Audio callback continuity/output checks failed; see buffer timeline.";
            run.status = "restoring"; run.complete = false;
        }
        catch (Exception e)
        {
            if (run == null) run = new Report { requestId = session.request.requestId };
            run.passed = run.diagnosticPassed = false; run.reason = "Finalization error: " + e;
        }
        finally
        {
            session.phase = "restoring"; SaveSession();
            CleanupStep(RestoreListeners, "listener restoration");
            CleanupStep(() => { if (ownedRoot) Object.DestroyImmediate(ownedRoot); }, "owned object removal");
            ownedRoot = null; source = null; capture = null; ownListener = null;
            CleanupStep(() => Application.runInBackground = session.originalBackground, "background restoration");
            try { if (xrQuitEvent != null && xrQuitObserver != null) xrQuitEvent.RemoveEventHandler(null, xrQuitObserver); } catch { }
            try { if (xrRestartEvent != null && xrRestartObserver != null) xrRestartEvent.RemoveEventHandler(null, xrRestartObserver); } catch { }
            ReleaseLocks();
            run.locksReleased = !session.ownsRefreshLock && !session.ownsReloadLock;
            CleanupStep(() => AtomicJson(ResultPath, run), "result persistence");
            finishing = false;
            if (requestStop && EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.isPlaying = false;
            else if (!EditorApplication.isPlayingOrWillChangePlaymode) Restore();
        }
    }
    static void WriteCapture(EditorBgmAudioCapture.Snapshot data)
    {
        string wave = Path.Combine(RunFolder, "capture.wav"); int bytes = checked(data.sampleCount * 4);
        using (var stream = File.Create(wave)) using (var writer = new BinaryWriter(stream))
        {
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + bytes);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16); writer.Write((ushort)3);
            writer.Write((ushort)data.channels); writer.Write(data.sampleRate); writer.Write(data.sampleRate * data.channels * 4);
            writer.Write((ushort)(data.channels * 4)); writer.Write((ushort)32); writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(bytes);
            byte[] block = new byte[65536];
            for (int offset = 0; offset < bytes; offset += block.Length)
            { int count = Math.Min(block.Length, bytes - offset); Buffer.BlockCopy(data.samples, offset, block, 0, count); writer.Write(block, 0, count); }
        }
        var stamps = new EditorBgmAudioCapture.BufferStamp[data.bufferCount]; Array.Copy(data.buffers, stamps, data.bufferCount);
        run.captureWav = wave; run.bufferTimeline = Path.Combine(RunFolder, "buffer-timeline.json");
        run.captureMetadata = Path.Combine(RunFolder, "capture-metadata.json");
        AtomicJson(run.bufferTimeline, new Timeline { sampleRate = data.sampleRate, channels = data.channels,
            capturedFrames = run.capturedFrames, bufferCount = data.bufferCount, stopwatchOriginTicks = data.clockOriginTicks,
            stopwatchFrequency = data.clockFrequency, buffers = stamps });
        AtomicJson(run.captureMetadata, new CaptureMetadata { requestId = run.requestId, sourceHash = run.asset.sha256,
            stage = "OnAudioFilterRead on the isolated source; correlate gain/offset before comparing with source PCM. Not a physical output recording.",
            sampleRate = data.sampleRate, channels = data.channels, frames = run.capturedFrames,
            sourceScheduledDsp = run.scheduledDsp, sourceDurationSeconds = run.asset.durationSeconds, sourceVolume = run.nominalVolume });
        run.captureReady = true;
    }
    static void ReleaseLocks()
    {
        if (session == null) return;
        if (session.ownsRefreshLock)
            CleanupStep(() => { AssetDatabase.AllowAutoRefresh(); session.ownsRefreshLock = false; SaveSession(); Event("auto_refresh_released", "Balanced own DisallowAutoRefresh."); }, "auto-refresh unlock");
        if (session.ownsReloadLock)
            CleanupStep(() => { EditorApplication.UnlockReloadAssemblies(); session.ownsReloadLock = false; SaveSession(); Event("reload_lock_released", "Balanced own LockReloadAssemblies."); }, "assembly unlock");
    }
    static void CleanupStep(Action step, string description)
    {
        try { step(); }
        catch (Exception e)
        {
            if (run != null) { run.passed = run.diagnosticPassed = false; run.reason += "\nCleanup failed (" + description + "): " + e; }
            Event("cleanup_error", description + ": " + e.Message);
        }
    }
    static void RestoreListeners()
    {
        foreach (var listener in disabledListeners) if (listener) listener.enabled = true;
        foreach (int id in session.disabledListenerIds)
        {
            var listener = EditorUtility.InstanceIDToObject(id) as AudioListener;
            if (listener) listener.enabled = true;
        }
        disabledListeners.Clear(); session.disabledListenerIds.Clear(); SaveSession();
    }
    static void Restore()
    {
        if (session == null || EditorApplication.isPlayingOrWillChangePlaymode) return;
        try
        {
            if (run == null && File.Exists(ResultPath)) run = JsonUtility.FromJson<Report>(File.ReadAllText(ResultPath));
            if (run == null) run = new Report { requestId = session.request.requestId, reason = "Interrupted before runtime began." };
            // Reclaim only this run's exact owned object if a reload lost static references.
            foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
                if (go && go.name == session.rootObjectName && go.scene.IsValid()) Object.DestroyImmediate(go);
            RestoreListeners(); Application.runInBackground = session.originalBackground;
            ReleaseLocks();
            EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(session.originalStartScene) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(session.originalStartScene);
            run.restoredScenes = Scenes();
            if (!SameScenes(session.scenes, run.restoredScenes))
            {
                // Never reload over either original or newly-created unsaved work.
                if (session.scenes.Any(s => s.isDirty) || run.restoredScenes.Any(s => s.isDirty))
                    throw new InvalidOperationException("Scene setup differs while unsaved work exists. No scenes were saved, closed, or reloaded; manual inspection is required.");
                if (session.blankScene) EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                else EditorSceneManager.RestoreSceneManagerSetup(session.scenes.Select(s => new SceneSetup { path = s.path, isLoaded = s.isLoaded, isActive = s.isActive }).ToArray());
                run.restoredScenes = Scenes();
            }
            if (!SameScenes(session.scenes, run.restoredScenes) || !SameDirtyState(session.scenes, run.restoredScenes))
                throw new InvalidOperationException("Unity did not restore the original scene setup and dirty flags. No further scene reload was attempted.");
            run.sceneRestored = AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene) == session.originalStartScene;
            run.locksReleased = !session.ownsRefreshLock && !session.ownsReloadLock;
            if (!run.locksReleased) throw new InvalidOperationException("Own reload/refresh lock could not be released; ownership remains persisted for recovery.");
            run.passed &= run.sceneRestored && run.locksReleased;
            run.diagnosticPassed &= run.sceneRestored && run.locksReleased;
            run.status = run.passed ? "passed" : run.diagnosticPassed ? "diagnostic_complete" : "failed";
            run.complete = true; run.restoredUtc = run.utc = Now(); Event("restored", "sceneRestored=" + run.sceneRestored + "; locksReleased=" + run.locksReleased);
            AtomicJson(ResultPath, run); SessionState.EraseString(Key); session = null; run = null; watch = null;
        }
        catch (Exception e)
        {
            ReleaseLocks();
            if (run == null) run = new Report { requestId = session.request.requestId };
            run.passed = run.diagnosticPassed = run.sceneRestored = false; run.complete = true; run.status = "restore_failed"; run.reason += "\n" + e;
            AtomicJson(ResultPath, run);
        }
    }
    static bool SameScenes(List<SceneInfo> left, List<SceneInfo> right) => left.Count == right.Count && left.Zip(right, (a, b) => a.path == b.path && a.isLoaded == b.isLoaded && a.isActive == b.isActive).All(v => v);
    static bool SameDirtyState(List<SceneInfo> left, List<SceneInfo> right) => left.Count == right.Count && left.Zip(right, (a, b) => a.isDirty == b.isDirty).All(v => v);
}
#endif
