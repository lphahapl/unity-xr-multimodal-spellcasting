using System;
using System.IO;
using System.Linq;
using NarutoVR.SealCombat.Rpg;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace NarutoVR.SealCombat.Editor
{
    [InitializeOnLoad]
    public static class LinearRpgValidation
    {
        const string Request = "Logs/linear-rpg.request", Result = "Logs/linear-rpg/result.json";
        const string TestKey = "NarutoVR.LinearRpg.PlayProbe";
        static double nextPoll, startProbeAt;
        static bool waitingForPlay;
        [Serializable] sealed class Report
        {
            public bool passed, hardwareTested;
            public string action, utc, detail, scene;
            public int dataChecks, locomotionChecks, coreChecks, effectChecks, integrationChecks;
        }
        static LinearRpgValidation()
        {
            EditorApplication.update += Poll;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode && !string.IsNullOrEmpty(SessionState.GetString(TestKey, "")))
                { waitingForPlay = true; startProbeAt = EditorApplication.timeSinceStartup + 2; }
                if (state == PlayModeStateChange.EnteredEditMode)
                { SessionState.EraseString(TestKey); LinearRpgController.EditorTestSaveFile = null; waitingForPlay = false; }
            };
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void SetTestSession() => LinearRpgController.EditorTestSaveFile = SessionState.GetString(TestKey, "");

        static void Poll()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (waitingForPlay && Application.isPlaying && EditorApplication.timeSinceStartup >= startProbeAt)
            {
                waitingForPlay = false;
                try
                {
                    int effects = SkillEffectChecks.Run() + SkillEffectConfigurationChecks.Run();
                    LinearRpgPlayChecks.Begin((ok, detail, count) =>
                    {
                        try { if (ok) CaptureHud(); }
                        catch (Exception captureError) { ok = false; detail += "\n中文面板预览失败：" + captureError; }
                        Write(new Report { action = "play-check", passed = ok, detail = detail, integrationChecks = count, effectChecks = effects });
                        SessionState.EraseString(TestKey); EditorApplication.isPlaying = false;
                    });
                }
                catch (Exception e) { Write(new Report { action = "play-check", detail = e.ToString() }); SessionState.EraseString(TestKey); EditorApplication.isPlaying = false; }
            }
            if (EditorApplication.timeSinceStartup < nextPoll || !File.Exists(Request)) return;
            nextPoll = EditorApplication.timeSinceStartup + .5;
            string action = File.ReadAllText(Request).Trim(); File.Delete(Request);
            try
            {
                switch (action)
                {
                    case "refresh": AssetDatabase.Refresh(); break;
                    case "repair-incomplete": LinearRpgSceneSetup.RepairIncomplete(); Write(new Report { action = action, passed = true }); break;
                    case "build-check": BuildAndCheck(); break;
                    case "play-check": StartPlayCheck(); break;
                    case "stop": EditorApplication.isPlaying = false; break;
                    case "preview": Preview(); Write(new Report { action = action, passed = true }); break;
                    case "tune-help":
                        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().path != LinearRpgSceneSetup.ScenePath || SceneManager.GetActiveScene().isDirty)
                            throw new Exception("说明更新需要已保存的新RPG场景");
                        var help = Object.FindObjectsByType<UnityEngine.UI.Text>(FindObjectsSortMode.None).Single(t => t.name == "导航与施法说明");
                        help.text = help.text.Replace("右手张掌短划闪避；", "导航准备期间，右手张掌短划闪避；");
                        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene()); EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
                        Write(new Report { action = action, passed = true }); break;
                    case "state":
                        var rpg = Object.FindFirstObjectByType<LinearRpgController>();
                        Write(new Report { action = action, passed = rpg, detail = rpg ? rpg.Status + "\n" + rpg.ObjectiveStatus : "无RPG控制器" }); break;
                    default: throw new InvalidOperationException("未知RPG操作：" + action);
                }
            }
            catch (Exception e) { Write(new Report { action = action, detail = e.ToString() }); Debug.LogException(e); }
        }
        [MenuItem("Tools/NarutoVR/线性RPG/创建场景并运行编辑器检查")]
        public static void BuildAndCheck()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请先停止播放模式");
            int data = RpgDataChecks.Run(); int locomotion = RpgLocomotionChecks.Run(); int core = SkillCoreChecks.Run();
            LinearRpgSceneSetup.Build();
            var scene = SceneManager.GetActiveScene();
            var rpg = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<LinearRpgController>(true)).Single();
            if (rpg.stages.Length != 3 || rpg.checkpointNodes.Length != 3 || rpg.targets.Length != 3 || !rpg.GetComponent<RpgTrainingHazard>()) throw new Exception("新场景阶段、靶子或攻击接线不全");
            if (rpg.skills.catalog.Validate().Count > 0 || rpg.skills.ConfigurationErrors().Count > 0) throw new Exception("新场景技能配置无效：" + string.Join("；",rpg.skills.ConfigurationErrors()));
            Write(new Report { passed = true, action = "build-check", dataChecks = data, locomotionChecks = locomotion, coreChecks = core,
                detail = "新RPG场景与独立配置创建并验证；未进行头显测试。" });
        }
        [MenuItem("Tools/NarutoVR/线性RPG/运行独立存档播放检查")]
        public static void StartPlayCheck()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("需要从编辑模式启动独立测试");
            if (SceneManager.GetActiveScene().path != LinearRpgSceneSetup.ScenePath || SceneManager.GetActiveScene().isDirty) throw new Exception("请打开并保存线性RPG测试场景");
            SessionState.SetString(TestKey, "linear-rpg-probe-" + Guid.NewGuid().ToString("N") + ".json");
            EditorApplication.isPlaying = true;
        }
        static void Write(Report report)
        {
            report.utc = DateTime.UtcNow.ToString("O"); report.scene = SceneManager.GetActiveScene().path;
            Directory.CreateDirectory("Logs/linear-rpg");
            string json = JsonUtility.ToJson(report, true); File.WriteAllText(Result, json);
            File.WriteAllText("Logs/linear-rpg/" + report.action + ".json", json);
            Debug.Log("[线性RPG验收] " + json);
        }
        public static void Preview()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().path != LinearRpgSceneSetup.ScenePath) throw new Exception("仅在RPG场景编辑模式下生成预览");
            var go = new GameObject("线性RPG_临时预览相机") { hideFlags = HideFlags.HideAndDontSave };
            var texture = RenderTexture.GetTemporary(1600, 1000, 24); var previous = RenderTexture.active;
            Texture2D pixels = null;
            try
            {
                var camera = go.AddComponent<Camera>(); camera.transform.position = new Vector3(7, 5.5f, -7);
                camera.transform.LookAt(new Vector3(0, 1.5f, 3)); camera.fieldOfView = 55;
                camera.clearFlags = CameraClearFlags.Skybox; camera.targetTexture = texture;
                camera.Render(); RenderTexture.active = texture;
                pixels = new Texture2D(1600, 1000, TextureFormat.RGB24, false); pixels.ReadPixels(new Rect(0, 0, 1600, 1000), 0, 0); pixels.Apply();
                Directory.CreateDirectory("Logs/linear-rpg"); File.WriteAllBytes("Logs/linear-rpg/scene-preview.png", pixels.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(texture); if (pixels) Object.DestroyImmediate(pixels); Object.DestroyImmediate(go); }
        }
        static void CaptureHud()
        {
            var view = Object.FindFirstObjectByType<RpgSessionView>();
            if (!view || !view.statusText) throw new Exception("未找到RPG中文面板");
            view.statusText.text = view.BuildHud(); Canvas.ForceUpdateCanvases();
            var panel = view.statusText.canvas.transform;
            var go = new GameObject("RPG中文面板_验收相机") { hideFlags = HideFlags.HideAndDontSave };
            var texture = RenderTexture.GetTemporary(1200, 1500, 24); var previous = RenderTexture.active;
            Texture2D pixels = null;
            try
            {
                var camera = go.AddComponent<Camera>(); camera.transform.SetPositionAndRotation(panel.position - panel.forward * 1.65f, panel.rotation);
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.02f,.03f,.06f);
                camera.fieldOfView = 52; camera.nearClipPlane = .05f; camera.targetTexture = texture;
                camera.Render(); RenderTexture.active = texture;
                pixels = new Texture2D(1200, 1500, TextureFormat.RGB24, false); pixels.ReadPixels(new Rect(0,0,1200,1500),0,0); pixels.Apply();
                Directory.CreateDirectory("Logs/linear-rpg"); File.WriteAllBytes("Logs/linear-rpg/hud-preview.png", pixels.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(texture); if (pixels) Object.DestroyImmediate(pixels); Object.DestroyImmediate(go); }
        }
    }
}
