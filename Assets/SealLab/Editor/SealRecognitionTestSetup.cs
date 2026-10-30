using System;
using System.IO;
using System.Linq;
using NarutoVR.SealLab;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Lab = NarutoVR.SealLab.SealLab;

namespace NarutoVR.Editor
{
    [InitializeOnLoad]
    public static class SealRecognitionTestSetup
    {
        public const string ScenePath = "Assets/SealLab/Scenes/SealRecognitionTest.unity";
        private const string Request = "Logs/recognition-test.request";
        private static double nextPoll;
        static SealRecognitionTestSetup() => EditorApplication.update += Poll;
        private static void Poll()
        {
            if (EditorApplication.timeSinceStartup < nextPoll) return;
            nextPoll = EditorApplication.timeSinceStartup + 1;
            if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(Request);
            try { BuildAndCheck(); }
            catch (Exception e) { File.WriteAllText("Logs/recognition-test-result.json", JsonUtility.ToJson(new Report { error = e.ToString() }, true)); Debug.LogException(e); }
        }
        [Serializable] private sealed class Report { public bool passed; public string error, utc, scene; public int profiles; }
        [MenuItem("Tools/NarutoVR/测试/创建并检查十二印测试场景")]
        public static void BuildAndCheck()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请先退出播放模式。");
            if (!File.Exists(ScenePath) && !AssetDatabase.CopyAsset(SealSteamLinkSetup.ScenePath, ScenePath)) throw new IOException("无法复制已验证的 Steam Link 场景。");
            var previous = SceneManager.GetActiveScene();
            var scene = SceneManager.GetSceneByPath(ScenePath);
            bool opened = scene.IsValid() && scene.isLoaded;
            if (!opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                var lab = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Lab>(true)).Single();
                var test = lab.GetComponent<SealRecognitionTest>();
                if (!test)
                {
                    test = lab.gameObject.AddComponent<SealRecognitionTest>(); test.lab = lab;
                    test.weights = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/SealLab/Resources/SealTestWeights.json");
                    var view = lab.GetComponent<SealLabView>();
                    var camera = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Camera>(true)).Single(c => c.enabled);
                    if (view.statusText) UnityEngine.Object.DestroyImmediate(view.statusText.canvas.gameObject);
                    view.statusText = null; // Keep the raw bone renderer; the test component owns the UI.
                    var go = new GameObject("十二印测试面板", typeof(RectTransform), typeof(Canvas));
                    go.transform.SetParent(camera.transform, false);
                    go.transform.localPosition = new Vector3(0, 0.12f, 1.4f);
                    go.transform.localScale = Vector3.one * 0.0008f;
                    go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                    go.GetComponent<RectTransform>().sizeDelta = new Vector2(1800, 900);
                    var font = AssetDatabase.LoadAssetAtPath<Font>(SealChineseUISetup.FontPath);
                    test.summaryText = Panel("识别结果与十二印得分", 0, 540, "十二印 · 识别测试\n等待运行\n\n请完成录制、生成模板草稿并确认模板。\n\n匹配分数并非正确概率。", 26);
                    test.leftText = Panel("左手与双手特征", 550, 620, "实时特征 · 左手\n进入播放模式后显示实际数据。", 22);
                    test.rightText = Panel("右手与双手特征", 1180, 620, "实时特征 · 右手\n失败详情输出到 Console。", 22);
                    Text Panel(string name, float x, float width, string initial, int size)
                    {
                        var panel = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                        panel.transform.SetParent(go.transform, false);
                        var rect = panel.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1);
                        rect.anchoredPosition = new Vector2(x, 0); rect.sizeDelta = new Vector2(width, 900);
                        var bg = panel.GetComponent<Image>(); bg.color = new Color(0.025f, 0.045f, 0.075f, 0.94f); bg.raycastTarget = false;
                        var label = new GameObject("中文数据", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text)); label.transform.SetParent(panel.transform, false);
                        var rt = label.GetComponent<RectTransform>(); rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = new Vector2(18, 16); rt.offsetMax = new Vector2(-18, -16);
                        var text = label.GetComponent<Text>(); text.font = font; text.fontSize = size; text.color = Color.white; text.text = initial; text.raycastTarget = false;
                        return text;
                    }
                    lab.gameObject.name = "十二印识别测试"; lab.recordOnStart = false; lab.SetTestMode(true);
                    EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                }
                if (!test.weights || !test.summaryText || !test.leftText || !test.rightText || !lab.xrHandsSource || !lab.IsTesting || lab.recordOnStart || lab.profiles.Count != 12)
                    throw new InvalidOperationException("测试场景配置不完整。");
                var cfg = JsonUtility.FromJson<SealRecognitionTest.WeightFile>(test.weights.text);
                if (cfg.profiles.Length != 12 || cfg.profiles.Any(p => p.rules.Length != 45 || p.rules.Select(r => r.featureId).Distinct().Count() != 45)) throw new InvalidOperationException("权重表条目不完整。");
                SealLabChecks.Run(); CheckDiagnostics();
                File.WriteAllText("Logs/recognition-test-result.json", JsonUtility.ToJson(new Report { passed = true, utc = DateTime.UtcNow.ToString("O"), scene = ScenePath, profiles = 12 }, true));
                Debug.Log("[结印测试] 测试场景、12×45项权重、49项既有检查及失败诊断检查通过。");
            }
            finally { if (!opened) EditorSceneManager.CloseScene(scene, true); if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous); }
        }
        private static void CheckDiagnostics()
        {
            var p = ScriptableObject.CreateInstance<SealProfile>();
            try
            {
                p.sealName = "测试印"; p.calibrated = true; p.rules.Add(new FeatureRule { featureId = "Both.WristDistance", min = 0, max = 1, tolerance = 1, weight = 1, required = true });
                var values = new[] { new SealFeature("Both.WristDistance", "m", 1.5f, true, true) };
                var score = SealScorer.Evaluate(p, values); string text = SealTestDiagnostics.Explain(score);
                if (score.passes || !text.Contains("Both.WristDistance") || !text.Contains("目标") || !text.Contains("扣") || !text.Contains("得分")) throw new Exception("失败诊断未包含分数及特征。");
                score = SealScorer.Evaluate(p, Array.Empty<SealFeature>());
                if (!SealTestDiagnostics.Explain(score).Contains("必需特征缺失，拒绝")) throw new Exception("必需特征诊断失败。");
                values[0] = new SealFeature("Both.WristDistance", "m", 0.5f, true, false);
                if (!SealTestDiagnostics.Explain(SealScorer.Evaluate(p, values)).Contains("低质量，被过滤")) throw new Exception("低质量诊断失败。");
            }
            finally { UnityEngine.Object.DestroyImmediate(p); }
        }
        [MenuItem("Tools/NarutoVR/测试/打开十二印测试场景")]
        public static void Open()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildAndCheck(); EditorSceneManager.OpenScene(ScenePath); SealLabWindow.Open();
        }
    }
}
