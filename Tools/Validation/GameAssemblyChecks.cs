// Place in Assets/scripts/logic/Editor of an isolated workspace, then run
// Unity -batchmode -nographics -quit -projectPath <copy> -executeMethod GameAssemblyChecks.Run
using System;
using System.IO;
using System.Linq;
using NarutoVR.SealCombat;
using NarutoVR.SealCombat.Rpg;
using UnityEditor;
using UnityEditor.Build.Player;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class GameAssemblyChecks
{
    public static void Run()
    {
        string temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!Application.isBatchMode || !Path.GetFullPath(Application.dataPath).StartsWith(temp, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("在临时项目的批处理模式运行此检查。");

        const string output = "Logs/assembly-check";
        Directory.CreateDirectory(output);
        using var report = new StreamWriter(Path.Combine(output, "results.txt")) { AutoFlush = true };
        report.WriteLine(DateTimeOffset.Now.ToString("o"));
        void Check(bool condition, string label)
        {
            report.WriteLine((condition ? "PASS " : "FAIL ") + label);
            if (!condition) throw new InvalidOperationException(label);
        }

        Check(typeof(SkillInputController).Assembly.GetName().Name == "NarutoVR.Game.Runtime", "Runtime");
        Check(typeof(PlayerModel).Assembly.GetName().Name == "NarutoVR.Game.Core", "Core");
        Check(typeof(RpgSaveStore).Assembly.GetName().Name == "NarutoVR.Game.Persistence", "Persistence");
        Check(typeof(GameAssemblyChecks).Assembly.GetName().Name == "NarutoVR.Game.Editor", "Editor");
        foreach (string file in Directory.GetFiles("Assets/Scene", "*.unity").OrderBy(i => i, StringComparer.Ordinal))
        {
            string name = Path.GetFileName(file);
            if (name.StartsWith("QA_", StringComparison.Ordinal)) continue;
            var scene = EditorSceneManager.OpenScene(file);
            var objects = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<Transform>(true))
                .Select(transform => transform.gameObject).ToArray();
            var missing = objects.Where(go => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go) > 0).ToArray();
            Check(missing.Length == 0, $"{name}: {objects.Length} objects, missing: {string.Join(", ", missing.Select(go => go.name))}");
            if (name != "Chapter01_Awakening.unity" && name != "LinearRpgTest.unity") continue;
            var player = objects.Select(go => go.GetComponent<LinearRpgController>()).FirstOrDefault(rpg => rpg);
            Check(player && player.skills && player.chapter && player.locomotion, $"{name} serialized player references");
            string script = AssetDatabase.GetAssetPath(MonoScript.FromMonoBehaviour(player.skills));
            Check(AssetDatabase.AssetPathToGUID(script) == "fc8640a823ad5204985044235f5ebb78", $"{name} input MonoScript GUID");
        }

        string scripts = Path.Combine(output, "AndroidScripts");
        Directory.CreateDirectory(scripts);
        var settings = new ScriptCompilationSettings { group = BuildTargetGroup.Android, target = BuildTarget.Android };
        var build = PlayerBuildInterface.CompilePlayerScripts(settings, scripts);
        var names = build.assemblies.Select(Path.GetFileName).ToArray();
        File.WriteAllLines(Path.Combine(output, "android-assemblies.txt"), names);
        foreach (string role in new[]{"Core", "Persistence", "Runtime"})
            Check(names.Contains($"NarutoVR.Game.{role}.dll"), $"Android player contains {role}");
        Check(!names.Contains("NarutoVR.Game.Editor.dll"), $"Android player excludes Editor ({names.Length} assemblies)");
        report.WriteLine("PASS END");
    }
}
