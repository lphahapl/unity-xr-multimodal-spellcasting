// Install in an isolated Unity project's Editor directory.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NarutoVR.SealCombat.Editor;
using NarutoVR.SealCombat.Rpg;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Profiling;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class MvcSceneAcceptance
{
    const string Key = "NarutoVR.MvcSceneAcceptance";
    static readonly List<double> frames = new();
    static double previous, deadline;
    static int frame = -1, warmup;
    static bool runningChecks;
    static LinearRpgController player;

    [Serializable]
    class Report
    {
        public string scene, unityVersion, mode, checks;
        public int stages, frameSamples, scenesInspected;
        public double medianFrameMs, p95FrameMs, reservedMemoryMiB;
        public bool passed;
    }

    static MvcSceneAcceptance()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                deadline = EditorApplication.timeSinceStartup + 150;
                EditorApplication.update += Tick;
            }
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetBool(Key, false);
                EditorApplication.delayCall += () =>
                {
                    var copy = SessionState.GetString(Key + ".copy", "");
                    if (copy.Length > 0 && (AssetDatabase.LoadAssetAtPath<SceneAsset>(copy) ||
                        AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene) != SessionState.GetString(Key + ".start", "")))
                    {
                        Debug.LogError("播放验收副本未清理或起始场景未恢复。");
                        EditorApplication.Exit(1);
                        return;
                    }
                    EditorApplication.Exit(SessionState.GetInt(Key + ".exit", 1));
                };
            }
        };
    }

    public static void Run()
    {
        string root = Path.GetDirectoryName(Application.dataPath);
        bool isolated = File.Exists(Path.Combine(root, ".mvc-acceptance-project")) && Application.productName == "MvcAcceptanceIsolated";
        isolated |= File.Exists(Path.Combine(root, ".skill-assembly-test-project")) && Application.productName.StartsWith("SkillAssemblyIsolated", StringComparison.Ordinal);
        if (!Application.isBatchMode || !isolated)
            throw new InvalidOperationException("Run in the marked temporary project with its own player settings.");
        string sceneName = Environment.GetEnvironmentVariable("MVC_SCENE");
        if (sceneName == "Chapter01_Awakening")
        {
            int count = 0;
            var scenes = Environment.GetEnvironmentVariable("MVC_AUTHORED_SCENES")?.Split('|');
            if (scenes == null || scenes.Length == 0) throw new InvalidDataException("Missing authored scene list.");
            foreach (string filename in scenes)
            {
                string path = "Assets/Scene/" + filename;
                var authored = EditorSceneManager.OpenScene(path);
                foreach (var transform in authored.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<Transform>(true)))
                    if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) != 0)
                        throw new InvalidDataException($"Missing script: {path}, {transform.name}");
                count++;
            }
            SessionState.SetInt(Key + ".scenes", count);
        }
        var scene = EditorSceneManager.OpenScene("Assets/Scene/" + sceneName + ".unity");
        if (Environment.GetEnvironmentVariable("MVC_VARIANT") != "baseline")
            CheckAuthoredGroups(OnlySession(scene), sceneName);
        if ((sceneName == "LinearRpgTest" || sceneName == "KaogongP1Trial") &&
            Environment.GetEnvironmentVariable("MVC_VARIANT") != "baseline")
        {
            var harness = typeof(EnemyPoolingChecks).Assembly.GetType("NarutoVR.SealCombat.Editor.IsolatedRpgPlayScene", true);
            SessionState.SetString(Key + ".start", AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
            string prefix = sceneName == "KaogongP1Trial" ? "room-check-" : "linear-rpg-probe-";
            harness.GetMethod("Prepare").Invoke(null, new object[] { scene.path, prefix });
            SessionState.SetString(Key + ".copy", AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        }
        else
        {
            var rpg = Object.FindFirstObjectByType<LinearRpgController>();
            rpg.autoLoad = false;
            rpg.saveFileName = "probe-mvc-" + Guid.NewGuid().ToString("N") + ".json";
            rpg.skills.voice.autoStartInEditor = false;
            if (!EditorSceneManager.SaveScene(scene, "Assets/Scene/QA_MvcScene.unity"))
                throw new IOException("Cannot save isolated scene.");
        }
        SessionState.SetBool(Key, true);
        SessionState.SetInt(Key + ".exit", 1);
        EditorApplication.EnterPlaymode();
    }

    static LinearRpgController OnlySession(UnityEngine.SceneManagement.Scene scene)
    {
        var roots = scene.GetRootGameObjects();
        var sessions = roots.SelectMany(root => root.GetComponentsInChildren<LinearRpgController>(true)).ToArray();
        var players = roots.SelectMany(root => root.GetComponentsInChildren<PlayerObj>(true)).ToArray();
        if (sessions.Length != 1 || players.Length != 1 || sessions[0].gameObject != players[0].gameObject)
            throw new InvalidDataException($"{scene.name} 需要同一实体上的唯一会话和玩家，实际为 {sessions.Length}/{players.Length}。");
        return sessions[0];
    }

    static void CheckAuthoredGroups(LinearRpgController session, string sceneName)
    {
        bool opening = sceneName == "Chapter01_Awakening";
        bool training = sceneName == "LinearRpgTest";
        if (session.transform.childCount != (training ? 4 : 5))
            throw new InvalidDataException($"会话根直接子级数不符：{session.transform.childCount}");
        if (session.name != "RPG 会话与玩家")
            throw new InvalidDataException("会话根名称不符：" + session.name);
        void Check(GameObject host, params Type[] expected)
        {
            var actual = host.GetComponents<Component>().Where(c => !(c is Transform))
                .Select(c => c ? c.GetType() : null).ToArray();
            if (expected.Length != actual.Length || expected.Except(actual).Any())
                throw new InvalidDataException(host.name + " 的序列化组件：" + string.Join(", ", actual.Select(t => t?.Name ?? "Missing")));
        }
        GameObject Group(string name)
        {
            var child = session.transform.Find(name);
            if (!child) throw new InvalidDataException("缺少会话分组：" + name);
            return child.gameObject;
        }
        Check(session.gameObject, typeof(LinearRpgController), typeof(PlayerObj));
        var ui = Group("界面与交互反馈");
        Check(ui, typeof(RpgSessionView), typeof(RpgInteractionFeedback));
        var status = ui.GetComponent<RpgSessionView>().statusText;
        if (!status || !status.transform.IsChildOf(ui.transform) || ui.transform.childCount != 1 ||
            !ui.transform.GetChild(0).GetComponent<Canvas>())
            throw new InvalidDataException("世界 HUD 缺少界面组内的原画布或状态文字。");
        var movement = Group("移动与导航");
        if (opening) Check(movement, typeof(RpgLocomotionController), typeof(NarutoVR.Audio.FootstepPlayer));
        else Check(movement, typeof(RpgLocomotionController));
        if (session.locomotion != movement.GetComponent<RpgLocomotionController>())
            throw new InvalidDataException("移动未引用本会话的分组。");
        var courtyard = session.gameObject.scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<NavigationNode>(true))
            .Where(node => node.nodeId?.StartsWith("courtyard.", StringComparison.Ordinal) == true).ToArray();
        if (courtyard.Length != 25 || courtyard.Select(n => n.nodeId).Distinct().Count() != 25 ||
            courtyard.Any(n => n.transform.parent != movement.transform))
            throw new InvalidDataException("25 个不同的庭院点必须都直接隶属移动与导航。");
        var chapter = Group("章节与训练");
        if (training) Check(chapter, typeof(TrainingTargetGroup), typeof(TrainingChapterFlow), typeof(RpgTrainingHazard));
        else if (opening) Check(chapter, typeof(TrainingTargetGroup));
        else Check(chapter, typeof(TrainingTargetGroup), typeof(TrainingChapterFlow));
        var growth = Group("成长与物品");
        if (opening) Check(growth, typeof(RpgSkillShop), typeof(RpgItemInventory));
        else Check(growth, typeof(RpgSkillShop));
        if (!training) Check(Group("场景音频与触觉"), typeof(P1AudioManager), typeof(HapticFeedbackService));
        else if (session.transform.Find("场景音频与触觉"))
            throw new InvalidDataException("训练场出现未配置的音频触觉分组。");
        var hazards = session.gameObject.scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<RpgTrainingHazard>(true)).ToArray();
        if (hazards.Length != (training ? 1 : 0) || (training && hazards[0].controller != session))
            throw new InvalidDataException("场景训练红圈的数量或所属会话错误。");
    }

    static string CheckPresentation(LinearRpgController session, string sceneName)
    {
        if (OnlySession(session.gameObject.scene) != session)
            throw new InvalidDataException("当前场景使用了另一份会话。");
        var view = session.GetComponentsInChildren<RpgSessionView>(true).Single();
        var feedback = session.GetComponentsInChildren<RpgInteractionFeedback>(true).Single();
        if (view.controller != session || feedback.controller != session || view.gameObject != feedback.gameObject ||
            !view.BuildHud().Contains("提示：" + feedback.LastFeedback))
            throw new InvalidDataException("子物体 HUD 没有显示本会话的交互反馈。");
        if (sceneName == "LinearRpgTest") return "会话分组、练习红圈、玩家 HUD 通过。";
        var audio = session.GetComponentsInChildren<P1AudioManager>(true).Single();
        var haptic = session.GetComponentsInChildren<HapticFeedbackService>(true).Single();
        const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
        bool Bound() => audio.MusicReady && ReferenceEquals(typeof(P1AudioManager).GetField("entity", fields).GetValue(audio), session.Entity)
            && ReferenceEquals(typeof(HapticFeedbackService).GetField("effects", fields).GetValue(haptic), session.skills.effects)
            && ReferenceEquals(typeof(HapticFeedbackService).GetField("movement", fields).GetValue(haptic), session.locomotion)
            && ReferenceEquals(typeof(HapticFeedbackService).GetField("wrist", fields).GetValue(haptic), session.wristMenu);
        if (!Bound()) throw new InvalidDataException("子物体未绑定玩家声音、移动、技能或腕匣事件。");
        audio.gameObject.SetActive(false);
        if (typeof(P1AudioManager).GetField("entity", fields).GetValue(audio) != null ||
            typeof(HapticFeedbackService).GetField("movement", fields).GetValue(haptic) != null)
            throw new InvalidDataException("停用音频和触觉后旧订阅仍在。");
        audio.gameObject.SetActive(true);
        if (!Bound()) throw new InvalidDataException("重新启用后音频触觉未绑定当前会话。");
        return "会话分组、无训练红圈、玩家 HUD、场景音频与触觉绑定和重新启用均通过。";
    }

    static async void Tick()
    {
        if (!Application.isPlaying || runningChecks) return;
        Report report = null;
        try
        {
            double now = EditorApplication.timeSinceStartup;
            if (now > deadline) throw new TimeoutException("Scene readiness / frame sampling timed out.");
            if (frame == Time.frameCount) return;
            frame = Time.frameCount;
            if (!player) player = Object.FindFirstObjectByType<LinearRpgController>();
            if (!player || !player.IsReady || !player.CanAct) return;
            if (warmup++ < 20) { previous = now; return; }
            frames.Add((now - previous) * 1000);
            previous = now;
            if (frames.Count < 90) return;
            runningChecks = true;
            report = new Report { scene = Environment.GetEnvironmentVariable("MVC_SCENE"),
                unityVersion = Application.unityVersion, mode = "desktop editor, batch, no XR" };
            frames.Sort();
            report.frameSamples = frames.Count;
            report.medianFrameMs = frames[frames.Count / 2];
            report.p95FrameMs = frames[(int)(frames.Count * .95)];
            report.reservedMemoryMiB = Profiler.GetTotalReservedMemoryLong() / (1024.0 * 1024.0);
            report.stages = player.stages.Length;
            report.scenesInspected = SessionState.GetInt(Key + ".scenes", 0);
            string presentation = Environment.GetEnvironmentVariable("MVC_VARIANT") == "baseline" ? null : CheckPresentation(player, report.scene);
            if (report.scene == "KaogongP1Trial")
            {
                string encounter = await (Task<string>)typeof(P1RoomPlayChecks)
                    .GetMethod("RunActualScene", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
                report.checks = string.Join("\n", encounter, P1RoomEncounterChecks.Run(), await EnemyPoolingChecks.Run(),
                    EnemyAttackChecks.Run(), EnemyCombatChecks.Run(), $"混音 {P1AudioChecks.Run()} 项；触觉 {P1HapticChecks.Run()} 项。");
                if (Environment.GetEnvironmentVariable("MVC_VARIANT") != "baseline")
                {
                    var deathChecks = typeof(EnemyPoolingChecks).Assembly.GetType("NarutoVR.SealCombat.Editor.EnemyRoomDeathChecks", true);
                    var room = Object.FindFirstObjectByType<CombatRoomEncounter>();
                    report.checks += "\n" + await (Task<string>)deathChecks.GetMethod("Run").Invoke(null, new object[] { room });
                }
            }
            if (report.scene == "LinearRpgTest" && Environment.GetEnvironmentVariable("MVC_VARIANT") != "baseline")
            {
                if (player.gameObject.scene.path != SessionState.GetString(Key + ".copy", "") ||
                    !player.saveFileName.StartsWith("linear-rpg-probe-") || player.autoLoad || player.chapter is not TrainingChapterFlow training || training.autoAdvance)
                    throw new InvalidDataException("Play 中的复制场景、存档或推进设置不符。");
                report.checks = "独立场景在 Awake 前使用唯一存档并关闭自动推进。";
            }
            if (report.scene == "Chapter01_Awakening" && Environment.GetEnvironmentVariable("MVC_VARIANT") != "baseline")
            {
                var suite = typeof(EnemyPoolingChecks).Assembly.GetType("NarutoVR.SealCombat.Editor.OpeningPuzzleChecks", true);
                int checks = (int)suite.GetMethod("Run").Invoke(null, null);
                var water = Object.FindFirstObjectByType<NarutoVR.EnvironmentAudio.WaterAudio>();
                if (!water || new SerializedObject(water).FindProperty("duckingCoordinator").objectReferenceValue != player.GetComponentInChildren<P1AudioManager>(true))
                    throw new InvalidDataException("水声未绑定本场景的混音器。");
                var route = typeof(EnemyPoolingChecks).Assembly.GetType("NarutoVR.SealCombat.Editor.OpeningRouteCheckpointChecks", true);
                report.checks = $"方圆仪 {checks} 项通过，正式水声混音引用有效。\n" + route.GetMethod("Run").Invoke(null, new object[] { player });
            }
            if (presentation != null) report.checks += "\n" + presentation;
            report.passed = true;
        }
        catch (Exception error) { report ??= new Report(); report.checks = error.ToString(); }
        EditorApplication.update -= Tick;
        File.WriteAllText(Environment.GetEnvironmentVariable("MVC_REPORT"), JsonUtility.ToJson(report, true));
        SessionState.SetInt(Key + ".exit", report.passed ? 0 : 1);
        EditorApplication.ExitPlaymode();
    }
}
