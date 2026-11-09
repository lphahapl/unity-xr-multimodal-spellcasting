// Copy to the isolated project's Assets/Editor, then use -executeMethod WaterPolishAcceptance.Run.
// This imports and inspects assets, exercises polyline and duck gain logic, and never saves a scene.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NarutoVR.EnvironmentAudio;
using NarutoVR.SealCombat.Rpg;
using UnityEditor;
using UnityEngine;

public static class WaterPolishAcceptance
{
    const string Root = "Assets/Art/Environment/Opening/WaterPolish/";
    static readonly List<string> Evidence = new List<string>();
    static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    public static void Run()
    {
        string output = Environment.GetEnvironmentVariable("WATER_POLISH_RESULT")
            ?? "Logs/water-polish-acceptance.txt";
        int code = 0;
        try { Validate(); }
        catch (Exception e) { Evidence.Add("FAIL " + e); code = 1; }
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
        File.WriteAllLines(output, Evidence);
        EditorApplication.Exit(code);
    }

    public static void Validate()
    {
        Evidence.Clear();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/CanalFlow.shader");
        var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Chapter01/CanalFlow.mat");
        Check(shader && material && material.shader == shader, "CanalFlow material/shader reference");
        foreach (var message in ShaderUtil.GetShaderMessages(shader))
            Check(message.severity.ToString() != "Error", "Shader import: " + message.message);
        Check(material.passCount == 1, "Exactly one shader pass");
        Check(material.GetTexture("_FlowMap") != null, "Existing RGBA flow texture retained");
        Check(material.GetFloat("_FoamStrength") > 0f && material.GetFloat("_GlintStrength") > 0f,
            "Polish controls serialized");
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Root + "Canal_GentleFlow_Loop.wav");
        Check(clip && clip.channels == 1 && clip.frequency == 32000 && Mathf.Abs(clip.length - 18f) < .01f,
            "Mono 32kHz 18 second clip imported");
        var importer = (AudioImporter)AssetImporter.GetAtPath(Root + "Canal_GentleFlow_Loop.wav");
        Check(importer.defaultSampleSettings.compressionFormat == AudioCompressionFormat.PCM,
            "Gapless PCM, not codec loop padding");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "WaterAudio.prefab");
        Check(prefab, "Prefab imported");
        var instance = UnityEngine.Object.Instantiate(prefab);
        var ear = new GameObject("WaterValidationEar");
        var managerObject = new GameObject("WaterValidationDuck");
        try
        {
            var component = instance.GetComponent<WaterAudio>();
            var sources = instance.GetComponentsInChildren<AudioSource>();
            Check(component && sources.Length == 1, "One WaterAudio and exactly one voice");
            var source = sources[0];
            Check(source.clip == clip && source.loop && !source.playOnAwake,
                "Loop clip assigned, component owns playback");
            Check(source.spatialBlend == 1f && source.dopplerLevel == 0f,
                "Fully spatial, no emitter-follow Doppler");
            Check(source.rolloffMode == AudioRolloffMode.Linear && source.maxDistance == 7.5f,
                "Local linear audibility ends at 7.5m");
            var listener = ear.AddComponent<AudioListener>();
            Set(component, "listener", listener);
            ear.transform.position = new Vector3(2f, 1.6f, 55f);
            Call(component, "UpdatePosition", true);
            Check(Vector3.Distance(source.transform.position, new Vector3(.539f, .13f, 55f)) < .001f,
                "Voice projects to nearest canal point");
            ear.transform.position = new Vector3(2f, 1.6f, 68f);
            Call(component, "UpdatePosition", true);
            Check(Mathf.Abs(source.transform.position.z - 60.9f) < .001f, "Projection clamps at canal endpoint");
            var listeners = UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
            var listenerEnabled = new bool[listeners.Length];
            try
            {
                for (int i = 0; i < listeners.Length; i++) { listenerEnabled[i] = listeners[i].enabled; listeners[i].enabled = false; }
                Set(component, "nextSearch", 0f);
                Call(component, "UpdatePosition", true);
                Check(!(AudioListener)component.GetType().GetField("listener", Private).GetValue(component),
                    "Disabled AudioListeners are not selected during listener handoff");
            }
            finally { for (int i = 0; i < listeners.Length; i++) if (listeners[i]) listeners[i].enabled = listenerEnabled[i]; }
            var manager = managerObject.AddComponent<P1AudioManager>();
            typeof(P1AudioManager).GetField("<DuckGain>k__BackingField", Private).SetValue(manager, .4f);
            Set(component, "duckingCoordinator", manager);
            Set(component, "fade", 1f);
            Call(component, "Update");
            Check(Mathf.Abs(source.volume - .19f * .4f) < .001f,
                "Reads existing DuckGain .4 without new mic consumer");
            Call(component, "OnDisable");
            Check(!source.isPlaying && source.volume == 0f, "Disable stops and silences voice");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(instance);
            UnityEngine.Object.DestroyImmediate(ear);
            UnityEngine.Object.DestroyImmediate(managerObject);
        }
        Evidence.Add("Import and logic checks only; audible mix, platform shader rendering, Quest timing require separate acceptance.");
        string output = Environment.GetEnvironmentVariable("WATER_POLISH_RESULT")
            ?? "Logs/water-polish-acceptance.txt";
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
        File.WriteAllLines(output, Evidence);
    }

    static void Check(bool result, string label)
    {
        Evidence.Add((result ? "PASS " : "FAIL ") + label);
        if (!result) throw new InvalidOperationException(label);
    }
    static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
    static void Call(object target, string name, params object[] values) => target.GetType().GetMethod(name, Private).Invoke(target, values);
}
