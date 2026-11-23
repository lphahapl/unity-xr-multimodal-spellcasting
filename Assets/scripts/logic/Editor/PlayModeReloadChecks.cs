using System;
using System.IO;
using NarutoVR.SealCombat.Rpg;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NarutoVR.SealCombat.Editor
{
    [InitializeOnLoad]
    public static class PlayModeReloadChecks
    {
        const string Key = "NarutoVR.PlayModeReloadChecks.";
        const string VisualName = "QA_ReloadVisual";
        const string Source = "Assets/Scene/Chapter01_Courtyard.unity";
        enum Phase { Idle, Starting, Requested, Reloaded, Exiting }

        static Phase Current
        {
            get => (Phase)SessionState.GetInt(Key + "phase", 0);
            set => SessionState.SetInt(Key + "phase", (int)value);
        }

        static PlayModeReloadChecks()
        {
            if (Current != Phase.Idle) Listen();
            AssemblyReloadEvents.afterAssemblyReload += () =>
            {
                if (EditorApplication.isPlaying && Current == Phase.Requested) Current = Phase.Reloaded;
            };
        }

        public static void Run()
        {
            if (!Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode || Current != Phase.Idle)
                throw new InvalidOperationException("用独立批处理编辑器从编辑模式启动热重载回归。");
            IsolatedRpgPlayScene.Prepare(Source, "qa-script-reload-");
            SessionState.SetString(Key + "save", IsolatedRpgPlayScene.SaveName);
            SessionState.SetFloat(Key + "deadline", (float)EditorApplication.timeSinceStartup + 150f);
            SessionState.SetBool(Key + "failed", false);
            Current = Phase.Starting;
            Listen();
            EditorApplication.EnterPlaymode();
        }

        static void Listen()
        {
            EditorApplication.update += Tick;
            Application.logMessageReceived += ObserveErrors;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredEditMode && Current == Phase.Exiting)
                    EditorApplication.delayCall += Finish;
            };
        }

        static void ObserveErrors(string message, string stack, LogType type)
        {
            if ((type == LogType.Exception || type == LogType.Error) &&
                (stack.Contains("NarutoVR.") || message.StartsWith("技能装配失败", StringComparison.Ordinal)))
                SessionState.SetBool(Key + "failed", true);
        }

        static void Tick()
        {
            if (Current == Phase.Idle || Current == Phase.Exiting) return;
            try
            {
                if (SessionState.GetBool(Key + "failed", false)) throw new InvalidOperationException("项目代码记录了异常，见上方调用栈。");
                if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + "deadline", 0))
                    throw new TimeoutException("等待章节或脚本重载超时，阶段 " + Current);
                if (!EditorApplication.isPlaying || Current == Phase.Requested) return;
                var player = Object.FindFirstObjectByType<LinearRpgController>();
                if (!player || !IsolatedRpgPlayScene.Owns(player.gameObject.scene) || !player.CanAct) return;
                if (Current == Phase.Starting)
                {
                    var visual = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    visual.name = VisualName;
                    visual.AddComponent<SkillVisualMotion>();
                    var block = new MaterialPropertyBlock();
                    block.SetFloat("_VisualOpacity", .25f);
                    visual.GetComponent<Renderer>().SetPropertyBlock(block);
                    SessionState.SetInt(Key + "stage", player.StageIndex);
                    Current = Phase.Requested;
                    EditorUtility.RequestScriptReload();
                    return;
                }
                var restored = GameObject.Find(VisualName);
                var opacity = new MaterialPropertyBlock();
                restored.GetComponent<Renderer>().GetPropertyBlock(opacity);
                if (!Mathf.Approximately(opacity.GetFloat("_VisualOpacity"), 1f) ||
                    player.Model == null || player.CaptureProgressSnapshot() == null ||
                    player.StageIndex != SessionState.GetInt(Key + "stage", -1))
                    throw new InvalidOperationException("章节、检查点或特效透明度未恢复。");
            }
            catch (Exception e)
            {
                SessionState.SetBool(Key + "failed", true);
                Debug.LogException(e);
            }
            Current = Phase.Exiting;
            EditorApplication.ExitPlaymode();
        }

        static void Finish()
        {
            Current = Phase.Idle;
            string save = SessionState.GetString(Key + "save", "");
            if (Path.GetFileName(save) == save && save.StartsWith("qa-script-reload-", StringComparison.Ordinal))
                foreach (string suffix in new[] { "", ".bak", ".tmp" })
                    File.Delete(Path.Combine(Application.persistentDataPath, save + suffix));
            bool failed = SessionState.GetBool(Key + "failed", false);
            if (!failed) Debug.Log("PlayModeReloadChecks PASS: 章节技能、检查点、特效属性和退出清理通过一次真实脚本重载。");
            EditorApplication.Exit(failed ? 1 : 0);
        }
    }
}
