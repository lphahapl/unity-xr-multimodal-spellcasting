using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NarutoVR.SealCombat.Rpg;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Management;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static partial class ChapterWalkCheck
{
    static string Output => Environment.GetEnvironmentVariable("CH02_WALK_REPORT");
    const string Key = "ChapterWalkCheck.run";
    static int frame;
    static ChapterWalkCheck() { EditorApplication.update += Update; }
    static void NoXR(XRGeneralSettings settings)
    {
        var displays = new List<XRDisplaySubsystem>();
        SubsystemManager.GetSubsystems(displays);
        if (!settings || settings.InitManagerOnStart || !settings.Manager || settings.Manager.activeLoader ||
            settings.Manager.activeLoaders.Count != 0 || displays.Any(d => d.running))
            throw new Exception("XR isolation failed");
    }
    public static void Run()
    {
        string replica = Environment.GetEnvironmentVariable("CH02_WALK_PROJECT");
        string source = Environment.GetEnvironmentVariable("CH02_WALK_SOURCE");
        if (!Application.isBatchMode || string.IsNullOrWhiteSpace(replica) || string.IsNullOrWhiteSpace(source) ||
            string.Equals(Path.GetFullPath(replica), Path.GetFullPath(source), StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(Path.GetFullPath(Application.dataPath), Path.GetFullPath(Path.Combine(replica, "Assets")), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Run only in the dedicated desktop replica.");
        NoXR(XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Standalone));
        EditorSceneManager.OpenScene("Assets/Scene/Chapter02_SuspendedPath.unity");
        var rpg = UnityEngine.Object.FindFirstObjectByType<LinearRpgController>();
        rpg.autoLoad=false;
        rpg.saveFileName="walk-qa-"+Guid.NewGuid().ToString("N")+".json";
        SessionState.SetBool(Key,true);
        EditorApplication.EnterPlaymode();
    }
    static void Update()
    {
        if (!EditorApplication.isPlaying || !SessionState.GetBool(Key,false)) return;
        try
        {
            NoXR(XRGeneralSettings.Instance);
            if (++frame < 40) return;
            SessionState.EraseBool(Key);
            var colliders = UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsSortMode.None);
            var lines=colliders.Where(c=>c.enabled && !c.isTrigger).Select(c=>string.Join("\t", Hierarchy(c.transform),c.GetType().Name,c.bounds.center.ToString("F3"),c.bounds.size.ToString("F3"),c.transform.eulerAngles.ToString("F1"))).OrderBy(x=>x);
            File.WriteAllLines(Path.Combine(Output,"geometry.tsv"),lines);
            BeginRoutes();
        }
        catch(Exception e) { SessionState.EraseBool(Key); File.WriteAllText(Path.Combine(Output,"error.txt"),e.ToString());EditorApplication.Exit(2); }
    }
    static string Hierarchy(Transform t) => t.parent ? Hierarchy(t.parent)+"/"+t.name : t.name;
    static partial void Routes();
    static void BeginRoutes()
    {
        if (Environment.GetEnvironmentVariable("CH02_WALK_MODE")=="inspect") EditorApplication.Exit(0);
        else Routes();
    }
}