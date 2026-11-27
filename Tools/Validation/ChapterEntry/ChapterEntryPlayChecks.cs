// Place in the Game.Editor assembly of a dedicated test project and run Run via -executeMethod.
using System;
using System.Collections;
using System.IO;
using System.Linq;
using NarutoVR.SceneArt;
using NarutoVR.SealCombat.Items;
using NarutoVR.SealCombat.Events;
using NarutoVR.SealCombat.Rpg;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace NarutoVR.SealCombat.Editor
{
    public static class ChapterEntryPlayChecks
    {
        const string Chapter2 = "Assets/Scene/Chapter02_SuspendedPath.unity";
        const string Courtyard = "Assets/Scene/Chapter01_Courtyard.unity";
        const string Training = "Assets/Scene/KaogongP1Trial.unity";
        const string Running = "ChapterEntryQA.running";
        static string Case => Environment.GetEnvironmentVariable("ENTRY_QA_CASE");
        static string Output => Environment.GetEnvironmentVariable("ENTRY_QA_OUTPUT");
        static IEnumerator steps;
        static double deadline;
        static string waiting;

        public static void Run()
        {
            try
            {
                Require(PlayerSettings.companyName == "ChapterEntryValidation" &&
                    PlayerSettings.productName.StartsWith("ChapterEntryQA", StringComparison.Ordinal),
                    "isolated company/product");
                string dir = Application.persistentDataPath;
                Require(Path.GetFileName(dir) == PlayerSettings.productName, "isolated persistentDataPath " + dir);
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(Output, "data-path.txt"), dir);
                if (Case == "travel" || Case == "legacy" || Case == "fresh-override")
                {
                    string fixture = Environment.GetEnvironmentVariable("ENTRY_QA_HUB_JSON");
                    var env = JsonUtility.FromJson<SaveEnvelope>(File.ReadAllText(fixture));
                    env.progress.currency = 17;
                    env.progress.checkpoint.health = 79;
                    File.WriteAllText(Path.Combine(dir, "chapter02-suspended-path.json"), JsonUtility.ToJson(env));
                }
                string start = Case == "travel" || Case == "redirect" || Case == "manual" ||
                    Case == "unavailable" || Case == "seed-court" ? Courtyard :
                    Case == "training" ? Training : Chapter2;
                var scene = EditorSceneManager.OpenScene(start, OpenSceneMode.Single);
                var r = Object.FindFirstObjectByType<LinearRpgController>();
                Require(r && scene.path == start, "authored player " + start);
                var settings = new SerializedObject(r);
                if (Case == "unavailable")
                {
                    var buildScenes = EditorBuildSettings.scenes;
                    Require(buildScenes.Any(s => s.path == Chapter2 && s.enabled), "destination initially enabled");
                    EditorBuildSettings.scenes = buildScenes.Select(s =>
                        new EditorBuildSettingsScene(s.path, s.path != Chapter2 && s.enabled)).ToArray();
                }
                string policy = Environment.GetEnvironmentVariable("ENTRY_QA_POLICY");
                if (!string.IsNullOrEmpty(policy))
                {
                    var option = settings.FindProperty("defaultSaveSource");
                    Require(option != null && option.propertyType == SerializedPropertyType.Enum, "Inspector entry policy is serialized");
                    int idx = Array.IndexOf(option.enumNames, policy);
                    Require(idx >= 0, "Inspector has " + policy);
                    option.enumValueIndex = idx;
                }
                settings.FindProperty("autoLoad").boolValue =
                    Case != "manual" && Case != "unavailable" && Case != "fresh-override" && Case != "travel" && Case != "seed-court";
                settings.ApplyModifiedPropertiesWithoutUndo();
                SessionState.SetBool(Running, true);
                EditorApplication.EnterPlaymode();
            }
            catch (Exception e) { End(e); }
        }

        [InitializeOnLoadMethod]
        static void Install() => EditorApplication.update += Tick;

        static void Tick()
        {
            if (!SessionState.GetBool(Running, false) || !EditorApplication.isPlaying) return;
            try
            {
                if (steps == null)
                {
                    steps = Test();
                    deadline = EditorApplication.timeSinceStartup + 180;
                }
                if (EditorApplication.timeSinceStartup > deadline)
                    throw new TimeoutException(waiting + " scene=" + SceneManager.GetActiveScene().path +
                        " status=" + Object.FindFirstObjectByType<LinearRpgController>()?.Status);
                if (!steps.MoveNext()) End(null);
            }
            catch (Exception e) { End(e); }
        }

        static bool Ready(string scene)
        {
            waiting = scene;
            var r = Object.FindFirstObjectByType<LinearRpgController>();
            return r && r.gameObject.scene.path == scene && r.IsReady && r.CanAct;
        }

        static IEnumerator Test()
        {
            if (Case == "seed-court" || Case == "exact-court")
            {
                while (!Ready(Courtyard)) yield return null;
                var courtyard = Object.FindFirstObjectByType<LinearRpgController>();
                if (Case == "seed-court")
                {
                    courtyard.Player.Respawn(62, 33, courtyard.GameTime);
                    Require(courtyard.SaveCheckpoint(), "saved in actual courtyard of shared chapter-one slot");
                }
                else Require(courtyard.Player.Health == 62, "loaded exact courtyard, not the other Chapter One scene");
                yield break;
            }
            if (Case == "travel")
            {
                while (!Ready(Courtyard)) yield return null;
                var source = Object.FindFirstObjectByType<LinearRpgController>();
                int expectedCurrency = source.Model.Currency + 31;
                Require(source.Growth.CreditCurrency(31) == ProgressCommitResult.Saved, "seed runtime currency in courtyard");
                const string permanent = "buff.zi", temporary = "buff.voice";
                Require(!source.IsSkillUnlocked("shield.left"), "fresh chapter one keeps its authored shield progression");
                Require(!source.IsSkillUnlocked(permanent) && source.Growth.GrantSkill(permanent) == ProgressCommitResult.Saved,
                    "grant non-default permanent skill in source");
                source.Model.SetStageGrants(new[] { temporary });
                Require(source.Model.HasStageGrant(temporary) && !source.IsSkillUnlocked(temporary),
                    "add source-only trial permission");
                source.Player.Respawn(43, 25, source.GameTime);
                Require(source.CaptureProgressSnapshot().checkpoint.health != source.Player.Health, "live health differs from last source save");
                var gate = Object.FindObjectsByType<SceneTravelGate>(FindObjectsSortMode.None)
                    .Single(g => g.LoadEnabled && g.DestinationScene == Chapter2);
                var oldPosition = source.locomotion.transform.position;
                EventCenter.Publish(TravelEvents.GateEntered, new GateEnteredNotice(gate));
                while (!Ready(Chapter2)) yield return null;
                var dest = Object.FindFirstObjectByType<LinearRpgController>();
                Require(!source && !gate, "Single scene unload destroys old player and gate");
                Require(dest.StageIndex == 0 && dest.CurrentStageCheckpoint == dest.CheckpointAt(0), "destination starts at its first checkpoint despite old hub save");
                Require(dest.Model.Currency == expectedCurrency && Mathf.Abs(dest.Player.Health - 43) < .01f,
                    "live travel currency and unsaved HP restored; HP=" + dest.Player.Health + " coins=" + dest.Model.Currency);
                Require(dest.Model.Chapter.CompletedStages.Count == 0, "first arrival has destination chapter objectives");
                Require(dest.IsSkillUnlocked(permanent) && !dest.Model.HasStageGrant(temporary) && !dest.IsSkillUnlocked(temporary),
                    "permanent skill survives, source trial permission expires");
                var saved = JsonUtility.FromJson<SaveEnvelope>(File.ReadAllText(dest.SavePath)).progress;
                Require(saved.checkpointId == dest.CheckpointAt(0).nodeId && saved.scenePath == Chapter2 &&
                    saved.currency == expectedCurrency && saved.checkpoint.health == 43, "first landing atomically replaces stale destination hub save");
                Require(saved.unlockedSkillIds.Count(id => id == "shield.left") == 1, "first gate save contains entry shield exactly once");
                var starter = dest.GetComponent<ChapterStakeSupply>();
                Require(starter && starter.pocket, "chapter two has its tool-belt stake supply");
                while (!starter.Item || !starter.Item.Dock) yield return null;
                Require(starter.Item.Dock == starter.pocket && Object.FindObjectsByType<CooldownStake>(FindObjectsSortMode.None).Length == 1,
                    "one starter stake is stowed in the real narrow sheath");
                CheckChapterTwoShield(dest);
                Log("positions source=" + oldPosition + " destination node=" + dest.CurrentStageCheckpoint.transform.position);
                yield break;
            }
            if (Case == "manual")
            {
                while (!Ready(Courtyard)) yield return null;
                var source = Object.FindFirstObjectByType<LinearRpgController>();
                Require(source.LoadDefaultSave(), "same default-load command as UI accepted in wrong scene");
            }
            if (Case == "unavailable")
            {
                while (!Ready(Courtyard)) yield return null;
                var same = Object.FindFirstObjectByType<LinearRpgController>();
                Require(!same.LoadSelectedSave(CampaignSaveSource.ChapterTwo), "unavailable destination rejected");
                for (int i = 0; i < 8; i++) yield return null;
                Require(Ready(Courtyard) && same.IsReady, "rejected load keeps source playable");
                yield break;
            }
            if (Case == "training")
            {
                while (!Ready(Training)) yield return null;
                var trial = Object.FindFirstObjectByType<LinearRpgController>();
                Require(trial.SaveCheckpoint(), "training save uses its own slot");
                yield break;
            }
            while (!Ready(Chapter2)) yield return null;
            var r = Object.FindFirstObjectByType<LinearRpgController>();
            if (Case == "legacy" || Case == "redirect" || Case == "manual" || Case == "backup-owner")
            {
                Require(r.CurrentStageCheckpoint.nodeId == "CH02_HUB" && r.StageIndex == 3, "restored exact hub and stage");
                Require(r.Model.Currency == 17 && r.Player.Health == 79, "restored saved character");
                if (Case == "legacy" || Case == "backup-owner") Require(r.SaveCheckpoint(), "resaved restored checkpoint");
            }
            else if (Case == "manual-missing")
            {
                float hp = r.Player.Health;
                Require(!r.LoadSelectedSave(CampaignSaveSource.ChapterOne), "missing requested save rejected");
                for (int i = 0; i < 8; i++) yield return null;
                Require(Ready(Chapter2) && hp == r.Player.Health && r.StageIndex == 0,
                    "missing manual load preserves current scene and player");
            }
            else if (Case == "corrupt")
            {
                Require(!r.SaveCheckpoint(), "bad local save blocks writes");
            }
            else
            {
                Require(r.StageIndex == 0 && r.CurrentStageCheckpoint == r.CheckpointAt(0) && r.Player.Health == 100 &&
                    r.Model.Currency != 17, "direct fresh uses authored player and first stage");
            }
            if (Case == "legacy" || Case == "fresh-override")
            {
                var starter = r.GetComponent<ChapterStakeSupply>();
                Require(starter && starter.pocket, "chapter two has its tool-belt stake supply");
                while (!starter.Item || !starter.Item.Dock) yield return null;
                Require(starter.Item.Dock == starter.pocket && Object.FindObjectsByType<CooldownStake>(FindObjectsSortMode.None).Length == 1,
                    "one starter stake is stowed in the real narrow sheath");
                CheckChapterTwoShield(r);
            }
        }

        static void CheckChapterTwoShield(LinearRpgController player)
        {
            const string id = "shield.left";
            Require(player.IsSkillUnlocked(id) && player.IsSkillAvailable(id) && player.IsSkillDiscovered(id),
                "chapter two permanently knows the left-hand Heng seal");
            Require(player.Player.ShieldPoints == 0, "entering the chapter does not cast the shield");
            var skill = player.skills.ConfiguredSkills.Single(skill => skill.skillId == id);
            float mana = player.Player.Mana;
            var result = ((ISkillActivationPolicy)player).TryActivate(skill);
            Require(result.Confirmed && player.Player.ShieldPoints > 0 &&
                Mathf.Abs(player.Player.Mana - (mana - skill.manaCost)) < .01f,
                "Heng seal uses the normal cast transaction, activates shield and pays authored mana");
        }
        static void Require(bool ok, string message)
        {
            if (!ok) throw new InvalidOperationException(message);
            Log("PASS " + message);
        }
        static void Log(string s) => File.AppendAllText(Path.Combine(Output, "checks.txt"), s + Environment.NewLine);
        static void End(Exception e)
        {
            SessionState.SetBool(Running, false);
            Log(e == null ? "PASS END " + Case : "FAIL " + e);
            EditorApplication.Exit(e == null ? 0 : 1);
        }
    }
}
