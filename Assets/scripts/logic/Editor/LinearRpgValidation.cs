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
        internal const string P1ScenePath = "Assets/Scene/KaogongP1Trial.unity";
        const string TestKey = "NarutoVR.LinearRpg.PlayProbe";
        static double nextPoll, startProbeAt;
        static bool waitingForPlay;
        [Serializable] sealed class Report
        {
            public bool passed, hardwareTested;
            public string action, utc, detail, scene;
            public int dataChecks, locomotionChecks, coreChecks, effectChecks, integrationChecks;
        }
        [Serializable] sealed class CommandAudit
        {
            public string utc, requestWrittenUtc, action, scene;
            public bool playing, paused;
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
                    int effects = SkillEffectChecks.Run() + SkillEffectConfigurationChecks.Run() + SkillArchitectureChecks.RunController() + SkillShowcaseChecks.ValidateRuntimePrefabs();
                    SkillEffectChecks.Begin((sweepOk, sweepDetail) =>
                    {
                        try
                        {
                            if (!sweepOk) throw new Exception(sweepDetail);
                            LinearRpgPlayChecks.Begin((ok, detail, count) =>
                            {
                                try { if (ok) CaptureHud(); }
                                catch (Exception captureError) { ok = false; detail += "\n中文面板预览失败：" + captureError; }
                                Write(new Report { action = "play-check", passed = ok, detail = detail + "\n" + sweepDetail, integrationChecks = count, effectChecks = effects + 2 });
                                SessionState.EraseString(TestKey); EditorApplication.isPlaying = false;
                            });
                        }
                        catch (Exception e) { Write(new Report { action = "play-check", detail = e.ToString() }); SessionState.EraseString(TestKey); EditorApplication.isPlaying = false; }
                    });
                }
                catch (Exception e) { Write(new Report { action = "play-check", detail = e.ToString() }); SessionState.EraseString(TestKey); EditorApplication.isPlaying = false; }
            }
            if (EditorApplication.timeSinceStartup < nextPoll || !File.Exists(Request)) return;
            nextPoll = EditorApplication.timeSinceStartup + .5;
            string action;
            DateTime requestWritten;
            // A shell may still be closing a request file when EditorApplication.update polls it.
            // Retry on the next tick; never turn a transient file lock into an editor exception.
            try { requestWritten = File.GetLastWriteTimeUtc(Request); action = File.ReadAllText(Request).Trim(); File.Delete(Request); }
            catch (IOException) { return; }
            try
            {
                // Preserve command provenance even when a stop interrupts another editor check.
                try
                {
                    Directory.CreateDirectory("Logs/linear-rpg");
                    File.AppendAllText("Logs/linear-rpg/command-history.jsonl", JsonUtility.ToJson(new CommandAudit
                    {
                        utc = DateTimeOffset.Now.ToString("O"), requestWrittenUtc = requestWritten.ToString("O"),
                        action = action, scene = SceneManager.GetActiveScene().path,
                        playing = EditorApplication.isPlaying, paused = EditorApplication.isPaused
                    }) + Environment.NewLine);
                }
                catch (IOException e) { Debug.LogWarning("编辑器命令记录失败，仍执行命令：" + e.Message); }
                catch (UnauthorizedAccessException e) { Debug.LogWarning("编辑器命令记录不可写，仍执行命令：" + e.Message); }
                switch (action)
                {
                    case "scored-throw-check": Write(new Report { action=action,passed=true,detail="评分投掷："+ScoredThrowChecks.Run()+"；表现："+ThrowPresentationChecks.Run() }); break;
                    case "configure-scored-throw": Write(new Report { action=action,passed=true,detail=ScoredThrowSetup.Configure() }); break;
                    case "score-ui-probe": Write(new Report { action=action,passed=true,detail="腕饰追踪表现检查："+ThrowPresentationChecks.Run() }); break;
                    case "wrist-roots": Write(new Report { action=action,passed=true,detail=WristViewLifecycleChecks.Inspect() }); break;
                    case "wrist-lifecycle": Write(new Report { action=action,passed=true,detail="腕饰生命周期检查："+WristViewLifecycleChecks.Run() }); break;
                    case "throw-ease-probe": Write(new Report { action=action,passed=true,detail="小幅投掷检查："+ThrowEaseChecks.Run() }); break;
                    case "wrist-tap-check": Write(new Report { action=action,passed=true,detail="点腕检查："+WristTapChecks.Run() }); break;
                    case "wrist-tap": Write(new Report { action=action,passed=true,detail=WristMenuSetup.ConfigureTap() }); break;
                    case "configure-wrist": Write(new Report { action=action,passed=true,detail=WristMenuSetup.Configure() }); break;
                    case "wrist-ease": Write(new Report { action=action,passed=true,detail=WristMenuSetup.ConfigureEase() }); break;
                    case "wrist-ray-probe": Write(new Report { action=action,passed=true,detail="腕部射线复现："+WristRayIssueProbe.Run() }); break;
                    case "wrist-check": Write(new Report { action=action,passed=true,detail="腕饰检查："+WristMenuChecks.Run() }); break;
                    case "wrist-preview": WristMenuSetup.Preview(); Write(new Report { action=action,passed=true }); break;
                    case "clock-check": Write(new Report { action = action, passed = true, detail = "口令时钟：" + SkillClockRegressionChecks.Run() }); break;
                    case "feel-check": Write(new Report { action = action, passed = true, detail = "输入手感："+InteractionFeelChecks.Run()+"；表现与调试："+InteractionFeelPresentationChecks.Run() }); break;
                    case "configure-feel": Write(new Report { action = action, passed = true, detail = InteractionFeelSetup.Configure() }); break;
                    case "navigation-check": Write(new Report { action = action, passed = true, detail = "导航评分：" + NavigationScoringChecks.Run() }); break;
                    case "configure-navigation": Write(new Report { action = action, passed = true, detail = RpgNavigationScoringSetup.Configure() }); break;
                    case "record-throws": HandThrowDiagnostics.Begin(); Write(new Report { action = action, passed = true, detail = HandThrowDiagnostics.LastPath }); break;
                    case "refresh": AssetDatabase.Refresh(); break;
                    case "pose-regression": Write(new Report { action = action, passed = true, detail = "骨骼到手形检查：" + RpgHandPoseChecks.Run() }); break;
                    case "configure-hand-poses": OfficialHandPoseSetup.Configure(); Write(new Report { action = action, passed = true }); break;
                    case "configure-interactions": RpgInteractionSetup.Configure(); Write(new Report { action = action, passed = true }); break;
                    case "interaction-check": Write(new Report { action = action, passed = true, detail = "四向交互：" + RpgDirectionalInteractionChecks.Run() + "；投掷：" + HandThrowChecks.Run() }); break;
                    case "hand-stream-probe": EmptyStreamProbe.Begin(true); break;
                    case "debug-text-comparison": DebugTextComparison.Begin(); break;
                    case "cpu-stream-sample": StreamCpuProfile.Capture(); break;
                    case "empty-stream-probe": EmptyStreamProbe.Begin(); break;
                    case "play-user":
                        if (SceneManager.GetActiveScene().path != LinearRpgSceneSetup.ScenePath || SceneManager.GetActiveScene().isDirty) throw new Exception("需要已保存的RPG场景");
                        EditorApplication.isPlaying = true; break;
                    case "open-p1":
                        if(EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().isDirty)
                            throw new InvalidOperationException("请在已保存的编辑模式场景打开 P1 试炼");
                        EditorSceneManager.OpenScene(P1ScenePath);
                        Write(new Report {action=action,passed=true}); break;
                    case "p1-check":
                        Write(new Report {action=action,passed=true,detail=P1TrialChecks.Run()}); break;
                    case "p1-room-inspect": Write(new Report { action=action, passed=true, detail=P1RoomLayoutSetup.Inspect() }); break;
                    case "organize-rpg-config": Write(new Report { action=action, passed=true, detail=ConfigurationAssetOrganizer.Run() }); break;
                    case "enemy-pool-setup": Write(new Report { action=action, passed=true, detail=EnemyPoolingSetup.Configure() }); break;
                    case "enemy-content-build": Write(new Report { action=action, passed=true, detail=EnemyPoolingSetup.BuildContent() }); break;
                    case "p1-room-apply": Write(new Report { action=action, passed=true, detail=P1RoomLayoutSetup.Apply() }); break;
                    case "p1-room-check": Write(new Report { action=action, passed=true, detail=P1RoomLayoutSetup.Check() }); break;
                    case "p1-room-preview": Write(new Report { action=action, passed=true, detail=P1RoomLayoutSetup.Preview() }); break;
                    case "p1-room-play-check": P1RoomPlayChecks.Begin(); Write(new Report { action=action, passed=true, detail="已启动；最终结果见 Logs/p1-room-20261104/play-check.txt" }); break;
                    case "full-editor-check": RpgRegressionSuite.Run(); Write(new Report { action = action, passed = true }); break;
                    case "clear-console":
                        typeof(EditorApplication).Assembly.GetType("UnityEditor.LogEntries")?.GetMethod("Clear", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public)?.Invoke(null, null);
                        break;
                    case "final-state":
                        var counters = new object[] { 0, 0, 0 };
                        var logEntries = typeof(EditorApplication).Assembly.GetType("UnityEditor.LogEntries");
                        var counterMethod = logEntries?.GetMethod("GetCountsByType", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                        counterMethod?.Invoke(null, counters);
                        var activeScene = SceneManager.GetActiveScene();
                        var sceneController = Object.FindFirstObjectByType<LinearRpgController>();
                        bool ready = counterMethod != null && (int)counters[0] == 0 && !EditorApplication.isPlayingOrWillChangePlaymode && !activeScene.isDirty &&
                            (activeScene.path == LinearRpgSceneSetup.ScenePath || activeScene.path == P1ScenePath) && sceneController && sceneController.handPoseSettings && sceneController.handPoseSettings.Validate() == null;
                        Write(new Report { action = action, passed = ready, detail = $"播放={EditorApplication.isPlaying}，未保存={activeScene.isDirty}，错误={counters[0]}，警告={counters[1]}，官方手形配置={(sceneController ? sceneController.handPoseSettings : null)}\n编辑器日志：{Application.consoleLogPath}" });
                        break;
                    case "console":
                        var consoleType = typeof(EditorApplication).Assembly.GetType("UnityEditor.LogEntries");
                        var entryType = typeof(EditorApplication).Assembly.GetType("UnityEditor.LogEntry");
                        var flags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;
                        var lines = new System.Text.StringBuilder();
                        int length = (int)consoleType.GetMethod("StartGettingEntries", flags).Invoke(null, null);
                        try
                        {
                            for (int i = Math.Max(0, length - 1000); i < length; i++)
                            {
                                var entry = Activator.CreateInstance(entryType);
                                consoleType.GetMethod("GetEntryInternal", flags).Invoke(null, new[] { (object)i, entry });
                                foreach (var field in entryType.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance))
                                    if (field.FieldType == typeof(string) && field.GetValue(entry) is string value && !string.IsNullOrEmpty(value)) lines.AppendLine(field.Name + ": " + value);
                            }
                        }
                        finally { consoleType.GetMethod("EndGettingEntries", flags).Invoke(null, null); }
                        Directory.CreateDirectory("Logs/official-hand-poses"); File.WriteAllText("Logs/official-hand-poses/console.txt", lines.ToString());
                        break;
                    case "open-rpg":
                    case "open-shield-trial":
                        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().isDirty) throw new Exception("切换测试场景需要已保存的编辑模式");
                        EditorSceneManager.OpenScene(action == "open-rpg" ? LinearRpgSceneSetup.ScenePath : "Assets/Scene/ShieldTrial.unity");
                        Write(new Report { action = action, passed = true }); break;
                    case "repair-incomplete": LinearRpgSceneSetup.RepairIncomplete(); Write(new Report { action = action, passed = true }); break;
                    case "build-check": BuildAndCheck(); break;
                    case "play-check": StartPlayCheck(); break;
                    case "stop":
                        try { Write(new Report { action = action, passed = true, detail = "收到外部文件停止命令，正在退出 Play。" }); }
                        finally { EditorApplication.isPlaying = false; }
                        break;
                    case "preview": Preview(); Write(new Report { action = action, passed = true }); break;
                    case "tune-help":
                        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().path != LinearRpgSceneSetup.ScenePath || SceneManager.GetActiveScene().isDirty)
                            throw new Exception("说明更新需要已保存的新RPG场景");
                        var help = Object.FindObjectsByType<UnityEngine.UI.Text>(FindObjectsSortMode.None).Single(t => t.name == "导航与施法说明");
                        help.text = help.text.Replace("右手张掌短划闪避；", "导航准备期间，右手张掌短划闪避；");
                        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene()); EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
                        Write(new Report { action = action, passed = true }); break;
                    case "ray-ui-config":
                        if(EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().isDirty) throw new Exception("配置需要已保存的编辑模式");
                        var rayMenu=Object.FindFirstObjectByType<WristMenuController>();
                        rayMenu.settings.rayDistance=3;
                        EditorUtility.SetDirty(rayMenu); EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene()); EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
                        Write(new Report{action=action,passed=true,detail="搜索射线范围3米"}); break;
                    case "storage-timeout-check":
                        Write(new Report{action=action,passed=true,effectChecks=ProjectileStorageChecks.Run()+ScoredThrowChecks.Run()+HandThrowChecks.Run(),detail="储存超时、单发限制、松手投掷、评分及保持、轻动作与误触检查通过"});break;
                    case "architecture-interface-check":
                        if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("接口回归需要编辑模式");
                        int interfaceChecks=SkillActivationPolicyChecks.Run()+SkillCastTraceChecks.Run()+SkillClockRegressionChecks.Run()+SkillCoreChecks.Run();
                        Write(new Report{action=action,passed=true,coreChecks=interfaceChecks,detail="新旧激活路径、日志隔离、口令时钟、核心匹配通过；未保存或切换用户场景"});break;
                    case "performance-status":
                    case "performance-reset":
                        var profiler=typeof(EditorApplication).Assembly.GetType("UnityEditorInternal.ProfilerDriver");
                        var profilingFlags=System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic;
                        string profilerDetail="Profiler录制="+profiler?.GetProperty("enabled",profilingFlags)?.GetValue(null)+
                            "；深度分析="+profiler?.GetProperty("deepProfiling",profilingFlags)?.GetValue(null);
                        if(action=="performance-reset") {
                            profiler?.GetProperty("enabled",profilingFlags)?.SetValue(null,false);
                            profiler?.GetMethod("ClearAllFrames",profilingFlags)?.Invoke(null,null);
                        }
                        Write(new Report{action=action,passed=true,detail=profilerDetail+"；帧间隔="+Time.unscaledDeltaTime.ToString("F4")+
                            "；Unity已分配MB="+(UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong()/1048576)+
                            "；保留MB="+(UnityEngine.Profiling.Profiler.GetTotalReservedMemoryLong()/1048576)}); break;
                    case "record-input": InteractionInputTrace.Begin(); Write(new Report{action=action,passed=true,detail="开始30秒输入诊断"}); break;
                    case "live-input":
                        var live=Object.FindFirstObjectByType<LinearRpgController>();
                        string detail="无主控";
                        if(live) {
                            var menu=live.wristMenu;
                            detail="技能："+live.skills.LastMessage+"\n匹配器："+live.skills.Engine?.Status+"\n"+live.skills.Engine?.ObservationDiagnostics+"\n"+live.GetComponent<RpgSessionView>()?.BuildSealDetails()+"\n"+live.HandPoseStatus+"\n"+(menu?menu.DebugDetails:"无菜单")+"\n"+(menu?JsonUtility.ToJson(menu.LatestInput):"")+"\n"+(live.skills.voice?live.skills.voice.BuildDebugDetails():"无接收器");
                        }
                        Write(new Report{action=action,passed=live,detail=detail}); break;
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
            if ((SceneManager.GetActiveScene().path != LinearRpgSceneSetup.ScenePath && SceneManager.GetActiveScene().path != P1ScenePath) || SceneManager.GetActiveScene().isDirty) throw new Exception("请打开并保存线性RPG或 P1 试炼场景");
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
            var canvas=view.statusText.GetComponentInParent<Canvas>(true);
            if(!canvas)throw new Exception("RPG文字缺少Canvas");
            var panel=canvas.transform;
            bool wasVisible=panel.gameObject.activeSelf;
            var go = new GameObject("RPG中文面板_验收相机") { hideFlags = HideFlags.HideAndDontSave };
            var texture = RenderTexture.GetTemporary(1200, 1500, 24); var previous = RenderTexture.active;
            Texture2D pixels = null;
            try
            {
                panel.gameObject.SetActive(true);
                view.statusText.text=view.BuildHud();Canvas.ForceUpdateCanvases();
                var camera = go.AddComponent<Camera>(); camera.transform.SetPositionAndRotation(panel.position - panel.forward * 1.65f, panel.rotation);
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.02f,.03f,.06f);
                camera.fieldOfView = 52; camera.nearClipPlane = .05f; camera.targetTexture = texture;
                camera.Render(); RenderTexture.active = texture;
                pixels = new Texture2D(1200, 1500, TextureFormat.RGB24, false); pixels.ReadPixels(new Rect(0,0,1200,1500),0,0); pixels.Apply();
                Directory.CreateDirectory("Logs/linear-rpg"); File.WriteAllBytes("Logs/linear-rpg/hud-preview.png", pixels.EncodeToPNG());
            }
            finally { panel.gameObject.SetActive(wasVisible); RenderTexture.active = previous; RenderTexture.ReleaseTemporary(texture); if (pixels) Object.DestroyImmediate(pixels); Object.DestroyImmediate(go); }
        }
    }
}
