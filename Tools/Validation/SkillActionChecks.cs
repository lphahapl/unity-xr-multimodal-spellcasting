using System;
using System.Collections;
using System.IO;
using System.Linq;
using NarutoVR.SealCombat;
using NarutoVR.SealCombat.Editor;
using NarutoVR.SealLab;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class SkillActionChecks
{
    const string Key = "NarutoVR.SkillActionChecks";
    const string Report = "Logs/skill-actions.txt";
    static double deadline;

    static SkillActionChecks()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                deadline = EditorApplication.timeSinceStartup + 240;
                EditorApplication.update += Verify;
                EditorApplication.update += Watchdog;
            }
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.update -= Watchdog;
                SessionState.SetBool(Key, false);
                EditorApplication.Exit(SessionState.GetInt(Key + ".exit", 1));
            }
        };
    }

    public static void Run()
    {
        var root = Directory.GetParent(Application.dataPath).FullName;
        if (!Application.isBatchMode || !File.Exists(Path.Combine(root, ".skill-assembly-test-project")))
            throw new InvalidOperationException("仅在隔离验收工程运行");
        Directory.CreateDirectory("Logs");
        File.WriteAllText(Report, DateTimeOffset.Now.ToString("o") + Environment.NewLine);
        Record("PASS authored configuration and shortcut checks: " + SkillArchitectureChecks.Run());
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorSceneManager.playModeStartScene = null;
        SessionState.SetInt(Key + ".exit", 1);
        SessionState.SetBool(Key, true);
        EditorApplication.EnterPlaymode();
    }

    static void Record(string value) => File.AppendAllText(Report, value + Environment.NewLine);

    static void Watchdog()
    {
        if (EditorApplication.timeSinceStartup < deadline) return;
        EditorApplication.update -= Watchdog;
        Record("FAIL action checks timed out");
        EditorApplication.ExitPlaymode();
    }

    static async void Verify()
    {
        EditorApplication.update -= Verify;
        try
        {
            CheckBladeTrail();
            Record("PASS blade trail movement, stationary fade, expiry and fresh segments after tracking loss");
            Record("PASS predicted contacts: " + ThrowPresentationChecks.Run());
            Record("PASS hand slot, legacy push, flight, shield and cancellation: " + SkillEffectChecks.Run());
            Record("PASS effect asset snapshots and cast transactions: " + SkillEffectConfigurationChecks.Run());
            Record("PASS disabled shortcut panel with invalid binding, real matching and cancellation: " + SkillArchitectureChecks.RunController());
            Record("PASS hand trajectory cases: " + HandThrowChecks.Run());
            Record("PASS assisted trajectory cases: " + ScoredThrowChecks.Run());
            Record("PASS visual pool capacity and re-use: " + P1VfxChecks.Run());
            var releasedPulse = P1HapticChecks.CheckShieldEvent(Require);
            await Awaitable.NextFrameAsync();
            Require(!releasedPulse, "destroying the hidden caster must release its shield pulse");
            Record("PASS hidden caster renders its shield pulse and releases the visual on destruction");
            foreach (IEnumerator probe in new[] { HandThrowChecks.PlayProbe(Record), ScoredThrowChecks.PlayProbe(Record) })
            {
                try { while (probe.MoveNext()) await Awaitable.NextFrameAsync(); }
                finally { (probe as IDisposable)?.Dispose(); }
            }
            await CheckAuthoredShowcases();
            Record("PASS END");
            SessionState.SetInt(Key + ".exit", 0);
        }
        catch (Exception error) { Record("FAIL " + error); }
        finally { EditorApplication.update -= Watchdog; EditorApplication.ExitPlaymode(); }
    }

    static void CheckBladeTrail()
    {
        var definition = AssetDatabase.LoadAssetAtPath<ThunderSwordEffectDefinition>(
            "Assets/ScriptableObjects/雷光剑配置/雷光剑效果.asset");
        Require(definition && definition.bladeTrailMaterial, "missing authored blade trail material");
        var scene = SceneManager.GetActiveScene();
        using var trail = new ThunderSwordBladeTrail(scene, definition.bladeTrailMaterial, .15f);
        var root = scene.GetRootGameObjects().Single(item => item.name == "雷光剑 · 刃轨");
        var renderer = root.GetComponent<MeshRenderer>();
        var mesh = root.GetComponent<MeshFilter>().sharedMesh;
        var a = Vector3.right;
        var b = a + Vector3.right * .05f;
        trail.Sample(a, a + Vector3.up, 1);
        Require(!renderer.enabled, "one observation must not display a ribbon");
        trail.Sample(b, b + Vector3.up, 1.03);
        var vertices = mesh.vertices;
        var alpha = mesh.colors32[0].a;
        Require(renderer.enabled && mesh.GetIndexCount(0) == 6, "two observations must display one quad");
        trail.Sample(b + Vector3.right * .001f, b + Vector3.up, 1.09);
        Require(mesh.vertices.SequenceEqual(vertices) && mesh.colors32[0].a < alpha,
            "motion below the sample threshold must fade the same geometry");
        trail.Tick(1.16);
        Require(!renderer.enabled, "expired observations must hide the last quad without a new hand frame");
        trail.Clear();
        trail.Sample(b, b + Vector3.up, 2);
        trail.Sample(a, a + Vector3.up, 2.02);
        Require(renderer.enabled && mesh.GetIndexCount(0) == 6 && mesh.vertices[0] == b &&
            mesh.vertices[2] == a, "reacquisition must begin a new ribbon with valid indices");
        trail.Tick(1.5);
        Require(!renderer.enabled, "a backwards game clock must clear the ribbon");
    }

    static void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    static async Awaitable CheckAuthoredShowcases()
    {
        var original = InputSystem.settings.editorInputBehaviorInPlayMode;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        var keyboard = InputSystem.AddDevice<Keyboard>();
        try
        {
            foreach (var scene in new[] { "SkillTest", "ShieldTrial", "FireballRange", "ComboSanctum" })
            {
                await EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scene/" + scene + ".unity", new LoadSceneParameters(LoadSceneMode.Single));
                await Awaitable.NextFrameAsync();
                var input = UnityEngine.Object.FindFirstObjectByType<SkillInputController>();
                Require(input && input.Engine != null && input.view && input.view.controller == input &&
                    input.view.showDesktopControls && input.Recipes.Count == 3, scene + " scene matcher and authored desktop view");
                double now = Time.realtimeSinceStartupAsDouble + 1;
                input.InputClock = () => now;
                input.effects.WristPoseOverride = _ => new Pose(input.effects.head.position + Vector3.right * .3f, Quaternion.identity);
                keyboard.MakeCurrent();
                foreach (var recipe in input.Recipes)
                {
                    int before = input.effects.ActivationCount;
                    var shortcut = input.view.shortcuts.SingleOrDefault(s => s.SkillId == recipe.Id);
                    if (recipe.Mode != SkillTriggerMode.GestureOnly)
                    {
                        Require(shortcut != null, scene + " missing key for " + recipe.Id);
                        await KeyboardStateInGame(keyboard, shortcut.key);
                        Require(input.ScheduledPhrase == recipe.Phrase, scene + " failed to schedule " + recipe.Id + " with " + shortcut.key);
                        int voices = input.VoiceEventCount;
                        now = input.ScheduledAt + .01;
                        input.SendMessage("LateUpdate");
                        Require(input.VoiceEventCount == voices + 1 && input.ScheduledPhrase == null, scene + " failed to dispatch " + recipe.Phrase);
                        if (recipe.Mode == SkillTriggerMode.VoiceAndGesture)
                            Require(input.Engine.PendingSkillId == recipe.Id && input.effects.ActivationCount == before,
                                scene + " combo must wait for its seal");
                    }
                    if (recipe.Mode != SkillTriggerMode.VoiceOnly)
                    {
                        var profile = input.lab.profiles.Single(p => p && p.sealName == recipe.Seal);
                        var features = profile.rules.Select(rule => new SealFeature(rule.featureId, "", (rule.min + rule.max) * .5f, true, true)).ToArray();
                        var score = SealScorer.Evaluate(profile, features);
                        for (int i = 0; i <= Mathf.CeilToInt(profile.holdSeconds / .02f) + 2; i++)
                        {
                            now += .02;
                            var frame = new SealFrame { time = now };
                            foreach (var hand in new[] { frame.left, frame.right })
                            {
                                hand.connected = hand.tracked = hand.fresh = hand.highConfidence = true;
                                hand.observedAt = now; hand.version = i + 1;
                            }
                            input.Engine.Tick(now);
                            input.Engine.Observe(frame, new[] { score });
                        }
                    }
                    bool held = recipe.Id == "fireball";
                    Require(input.effects.ActivationCount == before + 1 && (held ? input.effects.HasHeldObject : input.effects.ShieldPoints > 0),
                        scene + " no authored effect for " + recipe.Id + ": " + input.Engine.Status + " / " + input.effects.Status);
                    await KeyboardStateInGame(keyboard, UnityEngine.InputSystem.Key.Escape);
                    Require(!input.effects.HasHeldObject && input.effects.ShieldPoints == 0 &&
                        string.IsNullOrEmpty(input.Engine.PendingSkillId), scene + " Escape failed to cancel " + recipe.Id);
                    await KeyboardStateInGame(keyboard);
                    Record($"PASS {scene} {recipe.Id} key={shortcut?.key.ToString() ?? "none"}, phrase={recipe.Phrase}, seal={recipe.Seal}, activated and cancelled by Escape");
                }
            }
        }
        finally
        {
            InputSystem.RemoveDevice(keyboard);
            InputSystem.settings.editorInputBehaviorInPlayMode = original;
        }
    }

    static async Awaitable KeyboardStateInGame(Keyboard keyboard, params UnityEngine.InputSystem.Key[] keys)
    {
        bool delivered = false;
        void Deliver()
        {
            var update = InputState.currentUpdateType;
            if (update != InputUpdateType.Dynamic && update != InputUpdateType.Fixed && update != InputUpdateType.Manual) return;
            InputSystem.onAfterUpdate -= Deliver;
            InputState.Change(keyboard, new KeyboardState(keys));
            delivered = true;
        }
        InputSystem.onAfterUpdate += Deliver;
        try
        {
            for (int frame = 0; !delivered && frame < 30; frame++) await Awaitable.NextFrameAsync();
            Require(delivered, "player input update did not run");
            await Awaitable.NextFrameAsync();
        }
        finally { InputSystem.onAfterUpdate -= Deliver; }
    }
}
