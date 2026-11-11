using System;
using System.IO;
using System.Linq;
using NarutoVR.SealCombat.Rpg;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace NarutoVR.SealCombat.Editor
{
    // Configure the copy before entering Play so all Awake callbacks see its own save and input settings.
    [InitializeOnLoad]
    internal static class IsolatedRpgPlayScene
    {
        const string Key = "NarutoVR.IsolatedRpgPlayScene.";
        const string CopyPrefix = "Assets/Scene/RpgPlayProbe_";
        public static string Source => SessionState.GetString(Key + "source", "");
        public static string SaveName => SessionState.GetString(Key + "save", "");
        static string Copy => SessionState.GetString(Key + "copy", "");
        public static bool Owns(Scene scene) => scene.IsValid() && Copy.Length > 0 && scene.path == Copy;

        static IsolatedRpgPlayScene()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredEditMode && Copy.Length > 0) Restore();
            };
        }

        public static void Prepare(string source, string savePrefix)
        {
            if (Copy.Length > 0 || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("另一次播放检查仍在进行。");
            string token = Guid.NewGuid().ToString("N");
            string path = CopyPrefix + token + ".unity";
            if (!AssetDatabase.CopyAsset(source, path)) throw new IOException("无法复制验收场景：" + source);
            SessionState.SetString(Key + "start", AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
            SessionState.SetString(Key + "copy", path);
            SessionState.SetString(Key + "source", source);
            SessionState.SetString(Key + "save", savePrefix + token + ".json");
            try
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    RestoreCopiedNavigation(scene, source, path);
                    var roots = scene.GetRootGameObjects();
                    var session = roots.SelectMany(o => o.GetComponentsInChildren<LinearRpgController>(true)).Single();
                    session.saveFileName = SaveName;
                    session.autoLoad = false;
                    Record(session);
                    if (session.skills && session.skills.voice)
                    { session.skills.voice.autoStartInEditor = false; Record(session.skills.voice); }
                    foreach (var flow in roots.SelectMany(o => o.GetComponentsInChildren<TrainingChapterFlow>(true)))
                    { flow.autoAdvance = false; Record(flow); }
                    foreach (var service in roots.SelectMany(o => o.GetComponentsInChildren<HapticFeedbackService>(true)))
                    { service.enableControllerHaptics = false; Record(service); }
                    if (!EditorSceneManager.SaveScene(scene)) throw new IOException("无法保存验收场景：" + path);
                }
                finally { EditorSceneManager.CloseScene(scene, true); }
                EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
            }
            catch { Restore(); throw; }
        }

        static NavMeshSurface[] Surfaces(Scene scene) => scene.GetRootGameObjects().OrderBy(root => root.transform.GetSiblingIndex())
            .SelectMany(root => root.GetComponentsInChildren<NavMeshSurface>(true)).ToArray();

        static void RestoreCopiedNavigation(Scene copy, string source, string path)
        {
            var targets = Surfaces(copy);
            if (targets.All(surface => surface.navMeshData)) return;
            // NavMeshSurface.OnValidate drops a bake shared by two simultaneously open scenes.
            // Read the authored references in an unregistered preview, then give the copy its own bake.
            var preview = EditorSceneManager.OpenPreviewScene(source);
            try
            {
                var originals = Surfaces(preview);
                if (originals.Length != targets.Length) throw new InvalidDataException("验收副本的导航面与原场景不匹配");
                string folder = Path.ChangeExtension(path, null);
                for (int i = 0; i < targets.Length; i++)
                {
                    var baked = originals[i].navMeshData;
                    if (targets[i].navMeshData || !baked) continue;
                    if (!AssetDatabase.IsValidFolder(folder))
                        AssetDatabase.CreateFolder("Assets/Scene", Path.GetFileName(folder));
                    string asset = folder + "/Navigation-" + i + ".asset";
                    if (!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(baked), asset) ||
                        !(targets[i].navMeshData = AssetDatabase.LoadAssetAtPath<NavMeshData>(asset)))
                        throw new IOException("无法复制烘焙导航资源：" + baked.name);
                    Record(targets[i]);
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }

        static void Record(Component component)
        {
            if (PrefabUtility.IsPartOfPrefabInstance(component)) PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            EditorUtility.SetDirty(component);
        }

        static void Restore()
        {
            string path = Copy;
            string previous = SessionState.GetString(Key + "start", "");
            EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(previous) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(previous);
            if (path.StartsWith(CopyPrefix, StringComparison.Ordinal) &&
                Guid.TryParseExact(Path.GetFileNameWithoutExtension(path).Substring("RpgPlayProbe_".Length), "N", out _))
            {
                AssetDatabase.DeleteAsset(path);
                string folder = Path.ChangeExtension(path, null);
                if (AssetDatabase.IsValidFolder(folder)) AssetDatabase.DeleteAsset(folder);
            }
            foreach (string field in new[] { "start", "copy", "source", "save" }) SessionState.EraseString(Key + field);
        }
    }
}
