using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using NarutoVR.SealLab;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NarutoVR.Editor
{
    [InitializeOnLoad]
    public static class SealChineseUISetup
    {
        public const string FontPath = "Assets/SealLab/Resources/Fonts/NotoSansCJKsc-Regular.otf";
        private const string Request = "Logs/seal-chinese-ui.request";
        private static double nextPoll;
        static SealChineseUISetup() => EditorApplication.update += Poll;
        private static void Poll()
        {
            if (EditorApplication.timeSinceStartup < nextPoll) return;
            nextPoll = EditorApplication.timeSinceStartup + 1;
            if (!File.Exists(Request) || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            File.Delete(Request);
            try { ApplyAndCheck(); }
            catch (Exception error)
            {
                Directory.CreateDirectory("Logs");
                File.WriteAllText("Logs/chinese-ui-result.json", JsonUtility.ToJson(new Report { error = error.ToString() }, true));
                Debug.LogException(error);
            }
        }
        [Serializable] private sealed class Report
        { public bool passed; public string utc, error, font; public string[] scenes, inspectors; public bool hardwareTested; }

        [MenuItem("Tools/NarutoVR/界面/应用中文显示并检查")]
        public static void ApplyAndCheck()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请先退出播放模式，再应用场景中文显示。");
            var font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            if (!font) throw new FileNotFoundException("未找到随包中文字体，请等待资源导入完成。", FontPath);
            var importer = AssetImporter.GetAtPath(FontPath) as TrueTypeFontImporter;
            if (importer && (!importer.includeFontData || importer.fontTextureCase != FontTextureCase.Dynamic))
            {
                importer.includeFontData = true; importer.fontTextureCase = FontTextureCase.Dynamic;
                importer.SaveAndReimport();
                font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            }
            const string glyphs = "结印实验台左右手骨骼追踪录制测试寅卯巳未亥识别质量高低张开屈曲卷曲对指距离米度";
            foreach (char character in glyphs)
                if (!font.HasCharacter(character)) throw new InvalidOperationException("中文字体缺字：" + character);
            var previous = SceneManager.GetActiveScene();
            var paths = new[] { SealLabBuilder.ScenePath, SealSteamLinkSetup.ScenePath };
            var inspectorTypes = new HashSet<string>();
            foreach (string path in paths)
            {
                if (!File.Exists(path)) throw new FileNotFoundException("实验场景不存在。", path);
                var scene = SceneManager.GetSceneByPath(path);
                bool alreadyOpen = scene.IsValid() && scene.isLoaded;
                if (!alreadyOpen) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    var views = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<SealLabView>(true)).ToArray();
                    if (views.Length != 1 || !views[0].statusText) throw new InvalidOperationException("场景缺少唯一的实验台文字显示：" + path);
                    var view = views[0]; view.chineseFont = font;
                    CheckInspector(view); CheckInspector(view.lab);
                    if (view.lab.profiles.Count > 0) CheckInspector(view.lab.profiles[0]);
                    view.gameObject.name = view.lab && view.lab.xrHandsSource ? "结印实验台·Steam Link" : "结印实验台·Quest 原生";
                    view.statusText.canvas.gameObject.name = "头显状态面板";
                    view.statusText.gameObject.name = "实时特征与识别文字";
                    if (view.lab && view.lab.xrHandsSource)
                    {
                        var source = view.lab.xrHandsSource;
                        CheckInspector(source);
                        source.gameObject.name = "通用手部追踪数据源";
                        source.leftHand.gameObject.name = "左手·实际骨骼数据";
                        source.rightHand.gameObject.name = "右手·实际骨骼数据";
                    }
                    view.statusText.font = font;
                    view.statusText.text = "结印实验台\n等待运行，请连接头显后进入播放模式。";
                    EditorUtility.SetDirty(view); EditorUtility.SetDirty(view.statusText);
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
                finally { if (!alreadyOpen) EditorSceneManager.CloseScene(scene, true); }
            }
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            AssetDatabase.SaveAssets();
            SealSteamLinkSetup.BuildAndCheck();
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/chinese-ui-result.json", JsonUtility.ToJson(new Report
            { passed = true, utc = DateTime.UtcNow.ToString("O"), font = FontPath, scenes = paths, inspectors = inspectorTypes.ToArray(), hardwareTested = false }, true));
            var activeLab = UnityEngine.Object.FindFirstObjectByType<NarutoVR.SealLab.SealLab>();
            if (activeLab && !Application.isBatchMode)
            {
                Selection.objects = Array.Empty<UnityEngine.Object>();
                Selection.activeGameObject = activeLab.gameObject;
                ActiveEditorTracker.sharedTracker.ForceRebuild();
            }
            foreach (var window in Resources.FindObjectsOfTypeAll<SealLabWindow>()) window.Repaint();
            Debug.Log("[结印实验台] 两个实验场景的中文字体、常用汉字、输入配置与既有回归检查通过。");
            void CheckInspector(UnityEngine.Object target)
            {
                var editor = UnityEditor.Editor.CreateEditor(target);
                try
                {
                    string name = editor.GetType().Name;
                    if (!(editor is SealChineseInspector)) throw new InvalidOperationException("中文Inspector未生效：" + target.GetType().Name + " → " + name);
                    inspectorTypes.Add(name);
                }
                finally { UnityEngine.Object.DestroyImmediate(editor); }
            }
        }
    }
}
