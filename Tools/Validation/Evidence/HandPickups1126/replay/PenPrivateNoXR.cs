using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.XR.Management;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Management;

namespace NarutoVR.SealCombat.Editor
{
    [InitializeOnLoad]
    internal static class PenPrivateNoXR
    {
        const string Report = "Logs/GroundPenXR.txt";
        static PenPrivateNoXR()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.ExitingEditMode && state != PlayModeStateChange.EnteredPlayMode) return;
                try { Assert(state.ToString()); }
                catch (Exception e) { File.AppendAllText(Report,"FAIL "+e+"\n"); EditorApplication.Exit(17); }
            };
        }
        internal static void Assert(string phase)
        {
            var root=Path.GetFullPath(Path.Combine(Application.dataPath,".."));
            var expected=Path.GetFullPath("D:/Temp/HandPenPickup-1126");
            if (!Application.isBatchMode || !String.Equals(root,expected,StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Only the named private batch editor can run.");
            var json=Environment.GetEnvironmentVariable("XR_RUNTIME_JSON");
            var blocked=Path.Combine(expected,"NO_XR_RUNTIME_EXISTS_HERE.json");
            if (String.IsNullOrEmpty(json) || !String.Equals(Path.GetFullPath(json),blocked,StringComparison.OrdinalIgnoreCase) || File.Exists(json))
                throw new InvalidOperationException("Private nonexistent XR runtime is required.");
            var cfg=XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Standalone);
            var mgr=cfg ? cfg.Manager : null;
            var ds=new List<XRDisplaySubsystem>();
            SubsystemManager.GetSubsystems(ds);
            var running=ds.Count(d=> d.running);
            if (!cfg || !mgr || cfg.InitManagerOnStart || mgr.activeLoaders.Count!=0 || mgr.activeLoader || running!=0 ||
                (XRGeneralSettings.Instance && XRGeneralSettings.Instance.Manager && XRGeneralSettings.Instance.Manager.activeLoader))
                throw new InvalidOperationException("XR configuration or runtime is active.");
            File.AppendAllText(Report,$"{DateTimeOffset.Now:o} {phase} root={root} InitManagerOnStart={cfg.InitManagerOnStart} loaders={mgr.activeLoaders.Count} activeLoader=null runningDisplays={running} XR_RUNTIME_JSON={json}\n");
        }
    }
}
