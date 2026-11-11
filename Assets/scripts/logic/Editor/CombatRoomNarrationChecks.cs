using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NarutoVR.SealCombat;
using NarutoVR.SealCombat.Rpg;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Obj = UnityEngine.Object;

// Play the authored Chapter01 from the movement lesson through FT02 using an isolated v2 checkpoint.
[InitializeOnLoad]
public static class CombatRoomNarrationChecks
{
    const string Key = "CombatRoomNarrationChecks";
    const string Report = "Logs/combat-room-narration.txt";
    static LinearRpgController player;
    static OpeningTrialChapter opening;
    static OpeningSection fight;
    static int phase, shotStage = -1, warningCount, untilFrame;
    static bool shield, dodged;
    static bool checkedLegacyKeys;
    static bool checkedStoppedEncounter;
    static float markerDisabledUntil;
    static bool checkedInactiveMarker, restoredTargetApproach;
    static double deadline;

    static CombatRoomNarrationChecks()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                deadline = EditorApplication.timeSinceStartup + 140;
                EditorApplication.update += CheckPlay;
            }
            if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(Key + "done", false))
            {
                SessionState.SetBool(Key, false);
                EditorApplication.Exit(SessionState.GetInt(Key + "exit", 1));
            }
        };
    }

    public static void RunBatch()
    {
        var root = Path.GetDirectoryName(Application.dataPath);
        if (!Application.isBatchMode || !File.Exists(Path.Combine(root, ".skill-assembly-test-project")) ||
            !File.ReadAllText("Assets/scripts/logic/Rpg/LinearRpgController.cs").Contains("SkillAssemblyPlayerSave"))
            throw new InvalidOperationException("Requires isolated project and save directory");
        Directory.CreateDirectory("Logs");
        File.WriteAllText(Report, DateTimeOffset.Now.ToString("o") + "\n");
        var scene = EditorSceneManager.OpenScene("Assets/Scene/Chapter01_Awakening.unity");
        var owner = Obj.FindFirstObjectByType<LinearRpgController>();
        var chapter = (OpeningTrialChapter)owner.chapter;
        int movement = Array.FindIndex(chapter.sections, section => section.lines.Any(line => line.action == OpeningAction.StopAtMarker));
        var model = new PlayerModel(new RpgPlayerState());
        model.Discover(chapter.InitialDiscoveries);
        foreach (var stage in owner.stages.Take(movement))
            if (!model.ClaimStage(stage.stageId, stage.rewardCurrency, stage.unlockSkillIds)) throw new InvalidOperationException(stage.stageId);
        var progress = model.Export(model.Vitals.CaptureCheckpoint(0), owner.checkpointNodes[movement].nodeId);
        owner.autoLoad = true;
        owner.saveFileName = "probe-combat-entry-" + Guid.NewGuid().ToString("N") + ".json";
        var dir = Path.Combine(root, "Temp", "SkillAssemblyPlayerSave");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, owner.saveFileName), JsonUtility.ToJson(new SaveEnvelope { profileId = "linear-rpg-player", progress = progress }, true));
        owner.skills.voice.autoStartInEditor = false;
        if (!EditorSceneManager.SaveScene(scene, "Assets/Scene/QA_CombatRoomNarration.unity")) throw new IOException("Cannot save temporary scene");
        SessionState.SetBool(Key, true);
        SessionState.SetBool(Key + "done", false);
        EditorApplication.EnterPlaymode();
    }

    static void CheckPlay()
    {
        if (!Application.isPlaying) return;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline)
                throw new TimeoutException("phase=" + phase + ", stage=" + player?.StageIndex + ", line=" + opening?.sections[player.StageIndex].narration.Status);
            if (!player)
            {
                player = Obj.FindFirstObjectByType<LinearRpgController>();
                opening = (OpeningTrialChapter)player.chapter;
                fight = opening.sections.Single(s => s.lines.Any(line => line.id == "FT01"));
                fight.narration.lines.Single(row => row.audioFileName.EndsWith("/FT01.wav", StringComparison.Ordinal)).onStarted.AddListener(() => warningCount++);
                // Play the real clips at an accelerated pitch until the fight.
                foreach (var section in opening.sections) section.narration.GetComponent<AudioSource>().pitch = 3;
            }
            if (!player.CanAct) return;
            if (!checkedLegacyKeys)
            {
                checkedLegacyKeys = true;
                VerifyLegacyKey();
            }
            if (phase == 9 && opening.sections[player.StageIndex] != fight)
            {
                string reward = "reward." + fight.stage.stageId;
                Check(player.StageIndex == Array.IndexOf(opening.sections, fight) + 1 && player.Model.ClaimedRewards.Count(id => id == reward) == 1,
                    "cleared encounter and FT02 finished; next stage committed and reward granted once");
                File.AppendAllText(Report,"PASS END\n"); Finish(0); return;
            }
            if (opening.sections[player.StageIndex] != fight)
            {
                var section = opening.sections[player.StageIndex];
                int index = section.narration.CurrentLineIndex;
                if (index < 0) return;
                var lesson = section.lines[index];
                if (lesson.action == OpeningAction.StopAtMarker && !checkedInactiveMarker)
                {
                    var area = lesson.area;
                    if (markerDisabledUntil == 0)
                    {
                        area.gameObject.SetActive(false);
                        player.locomotion.rigRoot.position += area.transform.TransformPoint(area.center) - player.locomotion.PlayerFloorPosition;
                        Physics.SyncTransforms();
                        markerDisabledUntil = Time.time + .7f;
                    }
                    if (Time.time < markerDisabledUntil) return;
                    Check(!section.narration.OperationCompleted && Contains(area, player.locomotion.PlayerFloorPosition),
                        "MV02 stays pending after standing inside its inactive marker for more than half a second");
                    area.gameObject.SetActive(true);
                    checkedInactiveMarker = true;
                }
                if (lesson.id == "FR01" && !restoredTargetApproach)
                {
                    Check(checkedInactiveMarker, "MV02 advanced after the marker was enabled and the player stood still");
                    // Resume the ranged and dodge checks at their authored approach, with clearance behind the rig.
                    Check(player.RestoreCheckpoint(), "restored the target training approach");
                    restoredTargetApproach = true;
                    return;
                }
                if (lesson.id == "FR01" && section.narration.IsWaitingForNextLine && shotStage != player.StageIndex)
                {
                    shotStage = player.StageIndex;
                    HitTarget(section.lines.Single(row => row.id == "FR02"), false);
                }
                if (lesson.action == OpeningAction.HitTarget && shotStage != player.StageIndex)
                {
                    if (lesson.id == "FR02") throw new InvalidOperationException("Missed authored FR01 to FR02 inter-line interval");
                    shotStage = player.StageIndex;
                    HitTarget(lesson, true);
                }
                if (lesson.id == "FR02" && !section.narration.OperationCompleted) throw new InvalidOperationException("FR02 did not credit the shot from the preceding gap");
                if (lesson.action == OpeningAction.AbsorbBolt && !shield)
                {
                    shield = player.DebugCast("buff.zi");
                    Check(shield, "shield training cast");
                }
                if (lesson.action == OpeningAction.Retreat && !dodged)
                {
                    if (section.narration.GetType().GetMethod("RefreshOperationBinding") != null)
                    {
                        if (!section.narration.AudioCompleted || opening.Speaking) return;
                        var previous = section.narration.CurrentStep;
                        var oldCallback = (Action)typeof(OpeningDefenseDrill).GetField("passed", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(opening.defense);
                        CheckPrompts(section);
                        Check(previous != section.narration.CurrentStep && section.narration.AudioCompleted, "wrist replay replaces the active binding and retains completed audio");
                        oldCallback();
                        Check(!section.narration.OperationCompleted, "queued pre-replay drill callback cannot complete the new binding");
                    }
                    dodged = player.DebugMove(true, Vector2.down);
                    Check(dodged, "backward dodge training");
                }
                CheckSilent(warningCount == 0, "FT01 ran in an earlier lesson");
                return;
            }
            var box = opening.encounter.area;
            switch (phase)
            {
                case 0:
                    Check(shield && dodged, "target, shield and dodge lessons completed in authored order");
                    Check(!Contains(box, player.locomotion.PlayerFloorPosition), "standing outside fight room after targeting");
                    AdvanceFrames(); break;
                case 1:
                    if (Time.frameCount < untilFrame) return;
                    Check(warningCount == 0, "FT01 must remain silent outside after training; actual starts=" + warningCount);
                    MoveToDoor(box, -.20f); AdvanceFrames(); break;
                case 2:
                    if (Time.frameCount < untilFrame) return;
                    Check(warningCount == 0, "no warning at exterior side of threshold");
                    MoveToDoor(box, .20f); AdvanceFrames(); break;
                case 3:
                    if (Time.frameCount < untilFrame) return;
                    Check(warningCount == 1, "entering room plays FT01 once");
                    MoveToDoor(box, -.20f); AdvanceFrames(); break;
                case 4:
                    if (Time.frameCount < untilFrame) return;
                    MoveToDoor(box, .20f); AdvanceFrames(); break;
                case 5:
                    if (Time.frameCount < untilFrame) return;
                    Check(warningCount == 1, "exiting/re-entering does not replay FT01");
                    Check(player.RestoreCheckpoint(), "restored fight-stage v2");
                    Check(!Contains(box,player.locomotion.PlayerFloorPosition), "restored approach checkpoint is outside");
                    AdvanceFrames(); break;
                case 6:
                    if (Time.frameCount < untilFrame) return;
                    Check(warningCount == 1, "restoring outside does not play FT01");
                    MoveToDoor(box,.20f); AdvanceFrames(); break;
                case 7:
                    if (Time.frameCount < untilFrame) return;
                    Check(warningCount == 2, "new restored chapter round plays FT01 exactly once after entry");
                    AdvanceFrames(); break;
                case 8:
                    if (fight.narration.CurrentLineIndex != 1 || !fight.narration.AudioCompleted) return;
                    var enemies = opening.encounter.enemies;
                    if (!opening.encounter.HasStarted || enemies.Length == 0 || !enemies.All(enemy => enemy && enemy.CanReceiveHit)) return;
                    Check(!opening.encounter.IsComplete && !fight.narration.OperationCompleted && !player.Model.Chapter.CompletedStages.Contains(fight.stage.stageId),
                        "FT02 audio alone leaves fight stage and reward pending");
                    if (!checkedStoppedEncounter)
                    {
                        var observer = (Delegate)typeof(OpeningTrialChapter).GetField("onObjectiveChanged", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(opening);
                        var source = typeof(OpeningLesson).GetField("operationTarget").GetValue(fight.lines.Single(lesson => lesson.id == "FT02"));
                        fight.narration.StopDialogue();
                        observer.DynamicInvoke(source);
                        opening.ReplayGuidance();
                        Check(!opening.encounter.enabled && fight.narration.CurrentStep == null && !player.Model.Chapter.CompletedStages.Contains(fight.stage.stageId),
                            "stopped FT02 rejects its queued target notification and wrist replay cannot restart the encounter");
                        Check(player.RestoreCheckpoint(),"recover stopped encounter using real checkpoint");
                        checkedStoppedEncounter = true;
                        MoveToDoor(box, .20f);
                        return;
                    }
                    foreach (var enemy in enemies)
                        Check(enemy.Entity.ReceiveHit("qa-ft02-" + enemy.GetInstanceID(), 10000, 0), "authored encounter enemy accepted the finishing hit");
                    AdvanceFrames(); break;
            }
        }
        catch (Exception e) { File.AppendAllText(Report,"FAIL " + e + "\n"); Finish(1); }
    }

    static void HitTarget(OpeningLesson lesson, bool activeLine)
    {
        var target = lesson.target;
        var collider = target.GetComponentsInChildren<Collider>().Where(c => c.enabled && !c.isTrigger && c.GetComponentInParent<SkillTrainingTarget>() == target)
            .OrderByDescending(c => c.bounds.size.sqrMagnitude).First();
        var contact = collider.ClosestPoint(player.locomotion.head.position);
        var direction = (collider.bounds.center - contact).normalized;
        var effects = player.skills.effects;
        var reportHit = (Action<SkillTrainingTarget>)Delegate.CreateDelegate(typeof(Action<SkillTrainingTarget>),
            effects, typeof(SkillController).GetMethod("OnTargetHit", BindingFlags.Instance | BindingFlags.NonPublic));
        var projectile = new GameObject("QA target hit").AddComponent<SkillProjectile>();
        projectile.transform.position = contact - direction * .01f;
        projectile.radius = .025f; projectile.damage = 1;
        Physics.SyncTransforms();
        int previous = target.HitCount;
        projectile.Initialize(direction, player.locomotion.rigRoot, 1 << collider.gameObject.layer, 0, reportHit, null);
        Check(target.HitCount == previous + 1, "physical projectile against authored collider " + collider.name + " for " + lesson.id);
        if (activeLine) Check(opening.sections[player.StageIndex].narration.OperationCompleted, "active line received " + lesson.id);
    }

    static void AdvanceFrames() { phase++; untilFrame = Time.frameCount + 4; }

    static void CheckPrompts(OpeningSection section)
    {
        const BindingFlags member = BindingFlags.Instance | BindingFlags.NonPublic;
        var timer = typeof(OpeningTrialChapter).GetField("waiting", member);
        var hintIndex = typeof(OpeningTrialChapter).GetField("hintIndex", member);
        var tick = typeof(OpeningTrialChapter).GetMethod("TickHints", member);
        var input = typeof(WristMenuController).GetField("input", member);
        var hand = player.wristMenu.LatestInput;
        var hints = section.hints;
        var row = section.narration.CurrentLine;
        string file = row.audioFileName;
        var voice = opening.promptVoice;
        var previous = section.narration.CurrentStep;
        bool listenerPaused = AudioListener.pause;
        try
        {
            input.SetValue(player.wristMenu, new WristMenuInput { rightValid = true, rightObserved = Time.realtimeSinceStartupAsDouble });
            timer.SetValue(opening, 18f);
            voice.enabled = false;
            opening.ReplayGuidance();
            tick.Invoke(opening, null);
            Check(section.narration.CurrentStep == previous && (int)hintIndex.GetValue(opening) == 0 && (float)timer.GetValue(opening) == 18f,
                "disabled prompt source preserves the operation binding, pending hint and elapsed wait");

            voice.enabled = true;
            AudioListener.pause = true;
            opening.ReplayGuidance();
            tick.Invoke(opening, null);
            Check(section.narration.CurrentStep == previous && (int)hintIndex.GetValue(opening) == 0,
                "paused listener preserves pending wrist replay and hint");
            AudioListener.pause = listenerPaused;
            section.hints = new[] { new OpeningHint { audioFile = "QA/missing-hint" }, hints[0] };
            tick.Invoke(opening, null);
            Check(voice.clip && voice.clip.name == Path.GetFileNameWithoutExtension(hints[0].audioFile) &&
                (int)hintIndex.GetValue(opening) == 2 && (float)timer.GetValue(opening) == 0,
                "one missing hint does not delay the next playable hint");
            voice.Stop();
            row.audioFileName = "  " + file.Replace('/', '\\') + "  ";
            opening.ReplayGuidance();
            Check(voice.clip && voice.clip.name == Path.GetFileNameWithoutExtension(file),
                "wrist replay plays the same narration clip for normalized paths");
        }
        finally
        {
            voice.enabled = true;
            AudioListener.pause = listenerPaused;
            row.audioFileName = file;
            section.hints = hints;
            input.SetValue(player.wristMenu, hand);
        }
    }

    static void VerifyLegacyKey()
    {
        // Compile this replay against both the frozen old code and the candidate.
        var key = typeof(NarrationLine).GetField("lineId");
        if (key == null) return;
        var row = fight.narration.lines[0];
        string authored = (string)key.GetValue(row), clip = row.audioFileName;
        try
        {
            key.SetValue(row, "");
            opening.ValidateChapter(player);
            Check((string)typeof(NarrationLine).GetProperty("EffectiveLineId").GetValue(row) == "FT01" && (string)key.GetValue(row) == "", "legacy audio basename resolves a known unique action without rewriting authored ID");
            row.audioFileName = "Chapter01/unknown.wav";
            bool rejected = false;
            try { opening.ValidateChapter(player); } catch (InvalidOperationException) { rejected = true; }
            Check(rejected,"unmatched legacy audio basename rejected");
        }
        finally { key.SetValue(row,authored); row.audioFileName = clip; opening.ValidateChapter(player); }
    }
    static void MoveToDoor(BoxCollider box, float offset)
    {
        var local = box.center;
        local.z += -box.size.z * .5f + offset;
        var world = box.transform.TransformPoint(local);
        var floor = player.locomotion.PlayerFloorPosition;
        world.y = floor.y;
        player.locomotion.rigRoot.position += world - floor;
        Physics.SyncTransforms();
    }
    static bool Contains(BoxCollider box, Vector3 world)
    {
        var p = box.transform.InverseTransformPoint(world) - box.center;
        var h = box.size * .5f;
        return Mathf.Abs(p.x)<=h.x && Mathf.Abs(p.y)<=h.y && Mathf.Abs(p.z)<=h.z;
    }
    static void CheckSilent(bool result, string message) { if (!result) throw new InvalidOperationException(message); }
    static void Check(bool result, string message)
    {
        CheckSilent(result,message);
        File.AppendAllText(Report,"PASS " + message + "\n");
    }
    static void Finish(int exit)
    {
        EditorApplication.update -= CheckPlay;
        SessionState.SetBool(Key + "done", true); SessionState.SetInt(Key + "exit", exit); EditorApplication.ExitPlaymode();
    }
}

