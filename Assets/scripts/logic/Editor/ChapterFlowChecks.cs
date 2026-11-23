using System;
using System.IO;
using System.Collections.Generic;
using NarutoVR.SealCombat.Events;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using NarutoVR.SealCombat;
using NarutoVR.SealCombat.Rpg;
using NarutoVR.SealCombat.Editor;
using Obj = UnityEngine.Object;

[InitializeOnLoad]
public static class ChapterFlowChecks
{
    const string Key = "ChapterFlowChecks.active";
    const string Report = "Logs/chapter-flow-check.txt";
    static int step, frames;
    static double deadline;
    static readonly List<string> stageChanges = new();
    static IDisposable entered, exited;
    static ChapterFlowChecks()
    {
        EditorApplication.playModeStateChanged += state => {
            if (!SessionState.GetBool(Key, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode) { step = 0; frames = 0; deadline = EditorApplication.timeSinceStartup + 45; EditorApplication.update += Tick; }
            if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(Key+"done",false)) {
                SessionState.SetBool(Key,false); EditorApplication.Exit(SessionState.GetInt(Key+"exit",1));
            }
        };
    }
    static void Check(bool ok,string message) { if (!ok) throw new Exception(message); File.AppendAllText(Report,"PASS "+message+"\n"); }
    static LinearRpgController Controller() => Obj.FindFirstObjectByType<LinearRpgController>();
    static bool MatcherFollowsGrants(LinearRpgController r)
    {
        var allowed = r.skills.ConfiguredSkills.Where(skill => r.IsSkillAvailable(skill.skillId)).ToArray();
        return allowed.SequenceEqual(r.skills.AvailableSkills) &&
            r.skills.Recipes.Select(recipe => recipe.Id).SequenceEqual(allowed.Select(skill => skill.skillId)) &&
            r.skills.ConfiguredSkills.Where(skill => skill.triggerMode != SkillTriggerMode.GestureOnly)
                .All(skill => r.skills.voice.AcceptsPhrase(skill.phrase) == r.IsSkillAvailable(skill.skillId));
    }
    static void Validate(LinearRpgController r) => typeof(LinearRpgController).GetMethod("ValidateSetup",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(r,null);
    static RpgStageDefinition NewStage(string id, string title)
    {
        var stage = ScriptableObject.CreateInstance<RpgStageDefinition>();
        stage.stageId = id;
        stage.displayName = title;
        stage.instructions = "完成" + title;
        stage.startsInCombat = false;
        AssetDatabase.CreateAsset(stage, AssetDatabase.GenerateUniqueAssetPath("Assets/" + title + ".asset"));
        return stage;
    }

    static void ConfigureGuidance(ObjectiveChapterFlow flow)
    {
        var guidance = new GameObject("灯塔场景提示").AddComponent<ChapterSceneGuidance>();
        flow.sceneGuidance = guidance;
        var first = flow.stages[0];
        var second = flow.stages[1];
        guidance.stageVisibility = new[]
        {
            new ChapterStageVisibility { sceneObject = new GameObject("航道指引"), stageIds = new[]{first.stage.stageId, second.stage.stageId} },
            new ChapterStageVisibility { sceneObject = new GameObject("灯室入口"), stageIds = new[]{second.stage.stageId} }
        };
        var active = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Showcase/青色能量线.mat");
        var resting = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Showcase/玄色石面.mat");
        Check(active && resting && active != resting, "independent guidance uses two distinct materials");
        ChapterTargetHighlight Highlight(string name)
        {
            var subject = new GameObject(name, typeof(MeshRenderer));
            return new ChapterTargetHighlight
            {
                target = subject, surface = subject.GetComponent<Renderer>(), marker = new GameObject(name + "标记"),
                activeMaterial = active, restingMaterial = resting
            };
        }
        guidance.targetHighlights = new[]{ Highlight("入口信标"), Highlight("主灯") };
        first.guidanceTarget = guidance.targetHighlights[0].target;
        second.guidanceTarget = guidance.targetHighlights[1].target;
        var route = guidance.gameObject.AddComponent<QuestGuideRoute>();
        route.points = new[] { first.guidanceTarget.transform, second.guidanceTarget.transform };
        first.visuals.route = route;
        first.visuals.destination = route.points[0];
        second.visuals.gestures = new[] { new QuestGestureHint {
            target = second.guidanceTarget.transform, pendingObjective = second.objectives[0],
            motion = AssetDatabase.LoadAssetAtPath<QuestGestureMotion>("Assets/ScriptableObjects/手势示范/PressButton.asset")
        } };
    }

    static void CheckGuidance(ChapterSceneGuidance guidance, bool sharedSignVisible, bool lampRoomVisible, GameObject focus, string context)
    {
        Check(guidance.stageVisibility[0].sceneObject.activeInHierarchy == sharedSignVisible &&
            guidance.stageVisibility[1].sceneObject.activeInHierarchy == lampRoomVisible, context + " stage objects");
        foreach (var highlight in guidance.targetHighlights)
        {
            bool selected = focus && highlight.target == focus;
            Check(highlight.marker.activeInHierarchy == selected &&
                highlight.surface.sharedMaterial == (selected ? highlight.activeMaterial : highlight.restingMaterial),
                context + " marker and material on " + highlight.target.name);
        }
    }

    public static void RunBatch()
    {
        if (!Application.isBatchMode || !Application.dataPath.Replace('\\','/').StartsWith("D:/Temp/", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only run in isolated D:/Temp project in batch mode");
        Directory.CreateDirectory("Logs"); File.WriteAllText(Report,DateTimeOffset.Now.ToString("o")+"\n");
        try {
            foreach (string scene in new[]{"Assets/Scene/LinearRpgTest.unity", "Assets/Scene/KaogongP1Trial.unity"})
            {
                EditorSceneManager.OpenScene(scene);
                var training = Controller();
                Check(training && training.chapter is TrainingChapterFlow && training.chapter.Owner == training, "explicit training flow: " + scene);
                var trainingFlow = (TrainingChapterFlow)training.chapter;
                var hazards = Obj.FindObjectsByType<RpgTrainingHazard>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                Check(trainingFlow.transform.parent == training.transform && trainingFlow.TargetGroup && (training.roomControlsEncounter
                    ? hazards.Length == 0
                    : hazards.Length == 1 && hazards[0] == trainingFlow.Hazard && trainingFlow.Hazard.controller == training),
                    "training facilities follow chapter and encounter mode: " + scene);
                Check(training.StartingSkills.SequenceEqual(new[]{"fireball"}), "training player definition starts with fireball: " + scene);
                Validate(training);
            }
            EditorSceneManager.OpenScene("Assets/Scene/Chapter01_Courtyard.unity");
            var r=Controller(); var opening=Obj.FindFirstObjectByType<OpeningTrialChapter>();
            Check(r && opening && r.chapter==opening,"scene directly binds chapter");
            Check(opening.targetGroup && opening.targetGroup.transform.parent == r.transform &&
                !Obj.FindFirstObjectByType<RpgTrainingHazard>(FindObjectsInactive.Include),
                "opening practice targets bound without training hazard");
            Check(opening.inventory && opening.inventory.catalog && r.wristMenu.teachingCatalog &&
                r.wristMenu.teachingCatalog.entries.Any(entry => entry.skillIds.Contains("buff.voice")), "teaching and inventory use separate assets");
            Check(opening.FinalCheckpoint==opening.finalCheckpoint && opening.UsesExplorationFootsteps && opening.UsesWorldNavigationMarks && opening.GlyphPreviewPages==1,"opening capabilities preserved");
            using var authored = AuthoredSkillCatalog.Open(r.skills);
            var previousCatalog = r.skills.catalog;
            try { r.skills.catalog = authored.Catalog; Validate(r); }
            finally { r.skills.catalog = previousCatalog; }
            Check(true,"original chapter setup validation");
            ProgressionModuleChecks.Run(); Check(true,"current copper-leaf save and no implicit grant regression");
            // 保留旧首章组件启用，验证未绑定流程不会启动旧教学。
            foreach(var b in Obj.FindObjectsByType<OpeningLeafPuzzle>(FindObjectsInactive.Include,FindObjectsSortMode.None)) b.enabled=false;
            foreach(var b in Obj.FindObjectsByType<OpeningDefenseDrill>(FindObjectsInactive.Include,FindObjectsSortMode.None)) b.enabled=false;
            Obj.DestroyImmediate(r.GetComponentInChildren<RpgSkillShop>(true));
            Obj.DestroyImmediate(r.GetComponentInChildren<RpgItemInventory>(true));
            Obj.DestroyImmediate(opening.targetGroup);
            r.wristMenu.teachingCatalog=null;
            var flow=r.gameObject.AddComponent<ObjectiveChapterFlow>(); flow.rpg=r; r.chapter=flow;
            r.ConfigureRoute(new[]{ NewStage("lighthouse.enter", "灯塔入港"), NewStage("lighthouse.light", "灯塔点灯") }, r.checkpointNodes.Take(2).ToArray());
            r.playerDefinition = Obj.Instantiate(r.playerDefinition);
            r.playerDefinition.startingSkills = new[] { authored.Catalog.skills.Single(s => s.skillId == "buff.zi") };
            AssetDatabase.CreateAsset(r.playerDefinition, AssetDatabase.GenerateUniqueAssetPath("Assets/QA_ChapterPlayer.asset"));
            flow.explorationFootsteps=false;
            var a=new GameObject("QA_A").AddComponent<SignalChapterObjective>();
            var b2=new GameObject("QA_B").AddComponent<SignalChapterObjective>();
            var c=new GameObject("QA_C").AddComponent<SignalChapterObjective>();
            var area=new GameObject("QA_Area").AddComponent<ReachAreaChapterObjective>(); area.area=area.gameObject.AddComponent<BoxCollider>();
            area.area.isTrigger=true; area.area.size=new Vector3(2,4,2); area.transform.position=new Vector3(1000,0,1000); area.transform.rotation=Quaternion.Euler(0,45,0);
            flow.stages=new[]{new ChapterObjectiveStage{stage=r.stages[0],objectives=new ChapterObjective[]{a,b2}},new ChapterObjectiveStage{stage=r.stages[1],objectives=new ChapterObjective[]{c,area}}};
            flow.stages[0].trialSkills = new[] { "buff.zi", "buff.voice" };
            flow.stages[1].trialSkills = new[] { "thunder.sword", "buff.voice" };
            ConfigureGuidance(flow);
            r.saveFileName="probe-chapter-flow-"+Guid.NewGuid().ToString("N")+".json"; r.autoLoad=false;
            var cat=Obj.Instantiate(authored.Catalog); cat.hideFlags = HideFlags.None;
            cat.skills=cat.skills.Where(s=>s.skillId!="fireball").ToList();
            r.skillResources = null; // This fixture authors its own reduced catalog in the isolated QA scene.
            AssetDatabase.CreateAsset(cat,AssetDatabase.GenerateUniqueAssetPath("Assets/QA_ChapterCatalog.asset")); r.skills.catalog=cat;
            Validate(r); Check(true,"generic chapter validates without targets, copper-leaf configuration, fireball or hidden skill");
            var firstStage = flow.stages[0];
            var registeredTarget = firstStage.guidanceTarget;
            firstStage.guidanceTarget = a.gameObject;
            bool unknownHighlightRejected = false;
            try { flow.ValidateChapter(r); }
            catch (InvalidOperationException e) when (e.Message.Contains("高亮目标")) { unknownHighlightRejected = true; }
            finally { firstStage.guidanceTarget = registeredTarget; }
            Check(unknownHighlightRejected, "unregistered stage highlight rejected at configuration time");
            flow.stages[1].objectives=new ChapterObjective[]{a};
            bool rejected=false; try{Validate(r);}catch{rejected=true;} Check(rejected,"reused objective rejected");
            flow.stages[1].objectives=new ChapterObjective[]{c,area};
            b2.Complete(); Check(!b2.IsComplete,"early signal ignored");
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),"Assets/Scene/QA_ChapterFlow.unity");
            SessionState.SetBool(Key,true);SessionState.SetBool(Key+"done",false);
            EditorApplication.EnterPlaymode();
        } catch(Exception e) { File.AppendAllText(Report,"FAIL "+e);EditorApplication.Exit(1); }
    }
    static void Tick()
    {
        if (!Application.isPlaying) return;
        try {
            if (EditorApplication.timeSinceStartup>deadline) throw new TimeoutException("step "+step);
            if (++frames<10) return;
            var r=Controller(); var flow=r.chapter as ObjectiveChapterFlow;
            var a=GameObject.Find("QA_A").GetComponent<SignalChapterObjective>();
            var b=GameObject.Find("QA_B").GetComponent<SignalChapterObjective>();
            var c=GameObject.Find("QA_C").GetComponent<SignalChapterObjective>();
            var area=GameObject.Find("QA_Area").GetComponent<ReachAreaChapterObjective>();
            if(step==0) {
                Check(r.CanAct && flow && flow.stages[0].objectives.All(o=>o.IsRunning),"generic chapter initialized in real Play");
                Check(!Obj.FindFirstObjectByType<OpeningTrialChapter>().Speaking &&
                    (int)typeof(OpeningTrialChapter).GetField("index",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(Obj.FindFirstObjectByType<OpeningTrialChapter>())==-1,
                    "unbound opening component does not start teaching");
                Check(r.Model.UnlockedSkills.SequenceEqual(new[]{"buff.zi"}) &&
                    r.PlayerView.AvailableSkills.OrderBy(x=>x).SequenceEqual(new[]{"buff.voice","buff.zi"}) &&
                    !r.PlayerView.CanUseSkill("thunder.sword"), "player asset seeds permanent skills and stage grants add to them");
                stageChanges.Clear();
                Check(MatcherFollowsGrants(r), "matcher starts with the union of permanent and stage skills");
                entered = EventCenter.Subscribe(PlayerEvents.StageEntered, (PlayerStageNotice n) => {
                    if (n.Player == r.Entity) stageChanges.Add("enter " + n.Stage.stageId);
                });
                exited = EventCenter.Subscribe(PlayerEvents.StageExited, (PlayerStageNotice n) => {
                    if (n.Player == r.Entity) stageChanges.Add("exit " + n.Stage.stageId);
                });
                Check(!r.GetComponentInChildren<RpgSkillShop>(true) && !r.GetComponentInChildren<RpgItemInventory>(true),"generic chapter has no implicit shop or inventory module");
                Check(!r.TryAdvanceStage(),"legacy target advance cannot bypass chapter");
                Check(!r.CompleteChapterObjective(Obj.FindFirstObjectByType<OpeningTrialChapter>(FindObjectsInactive.Include)),"foreign chapter completion rejected");
                CheckGuidance(flow.sceneGuidance, true, false, flow.stages[0].guidanceTarget, "lighthouse entry");
                Check(flow.TrailHints.route.EndIndex(flow.TrailHints.destination) == 0 && flow.GestureHints.Length == 0,
                    "ordinary chapter uses first stage's authored route");
                var unboundChapter = Obj.FindFirstObjectByType<OpeningTrialChapter>();
                unboundChapter.sceneGuidance = flow.sceneGuidance;
                unboundChapter.enabled = false;
                CheckGuidance(flow.sceneGuidance, true, false, flow.stages[0].guidanceTarget, "disabling an unbound chapter preserves current cues");
                Obj.Destroy(unboundChapter);
                flow.sceneGuidance.stageVisibility[0].sceneObject.SetActive(false);
                flow.sceneGuidance.SetFocus(flow.stages[1].guidanceTarget);
                CheckGuidance(flow.sceneGuidance, false, false, flow.stages[1].guidanceTarget, "changing focus only updates markers and materials");
                flow.sceneGuidance.Show(flow.stages[0].stage.stageId, flow.stages[0].guidanceTarget);
                flow.enabled = false;
                Check(MatcherFollowsGrants(r), "matcher immediately drops revoked trial recipes and phrases");
                Check(!r.IsSkillAvailable("buff.voice") && r.IsSkillAvailable("buff.zi"), "chapter disable revokes only its grant");
                flow.enabled = true;
                Check(MatcherFollowsGrants(r), "matcher reacquires active trial recipes");
                Check(r.IsSkillAvailable("buff.voice"), "chapter re-enable reacquires its active grant");
                var beforeGrant = r.skills.Engine;
                Check(r.Growth.GrantSkill("buff.voice") == ProgressCommitResult.Saved, "a borrowed skill can be made permanent");
                Check(beforeGrant == r.skills.Engine && MatcherFollowsGrants(r), "making a loan permanent preserves pending input without duplicate recipes");
                step=1;frames=0;
            } else if(step==1) {
                if (!a.IsRunning) return;
                a.Complete();
                Check(r.StageIndex==0 && !flow.ObjectiveMet,"AND goal waits for second signal");
                b.Complete();step=2;frames=0;
            } else if(step==2) {
                if(r.StageIndex!=1 || !c.IsRunning)return;
                Check(r.Model.Chapter.CompletedStages.Count==1 && File.Exists(r.SavePath),"first stage committed and saved once");
                Check(!Obj.FindFirstObjectByType<OpeningTrialChapter>(FindObjectsInactive.Include), "lighthouse progression has no opening chapter component");
                Check(r.IsSkillAvailable("thunder.sword") && !r.IsSkillUnlocked("thunder.sword"), "second stage grants a temporary skill");
                Check(!r.Model.HasStageGrant("buff.zi") && r.PlayerView.CanUseSkill("buff.zi") && r.DebugCast("buff.zi"),
                    "a permanent skill outside the current trial list casts through the real policy");
                Check(r.Model.HasStageGrant("buff.voice") && !r.CaptureProgressSnapshot().unlockedSkillIds.Contains("thunder.sword"),
                    "adjacent stages can lend the same skill and temporary permission stays out of the checkpoint");
                CheckGuidance(flow.sceneGuidance, true, true, flow.stages[1].guidanceTarget, "lighthouse second stage");
                var hint = flow.GestureHints.Single();
                Check(!flow.TrailHints.route && hint.Available(hint.target.position), "new stage replaces route with pending gesture");
                a.Complete();c.Complete();step=3;frames=0;
            } else if(step==3) {
                Check(!r.CampaignComplete && !area.IsComplete,"stale signal and distant area cannot finish next stage");
                var hint = flow.GestureHints.Single();
                Check(!hint.Available(hint.target.position), "individual completed goal retires its own gesture");
                // Make restoration observable even when the checkpoint points to the same stage.
                flow.sceneGuidance.stageVisibility[0].sceneObject.SetActive(false);
                flow.sceneGuidance.targetHighlights[1].marker.SetActive(false);
                flow.sceneGuidance.targetHighlights[1].surface.sharedMaterial = flow.sceneGuidance.targetHighlights[1].restingMaterial;
                // 模拟发布后调整新档初始配置，旧角色必须保留自己的永久列表。
                var originalDefinition = r.playerDefinition;
                r.playerDefinition = Obj.Instantiate(originalDefinition);
                r.playerDefinition.startingSkills = new[]{r.skills.ConfiguredSkills.Single(s => s.skillId == "thunder.sword")};
                Check(r.RestoreCheckpoint() && !r.IsSkillUnlocked("thunder.sword"),"older checkpoint retains its own permanent skills after initial config changes");
                Obj.Destroy(r.playerDefinition);
                r.playerDefinition = originalDefinition;
                Check(r.IsSkillAvailable("thunder.sword"), "trial permission available in the restore frame before the next Update");
                Check(MatcherFollowsGrants(r), "restored skill grants reach the matcher in the restore frame");
                Check(flow.ObjectiveText == flow.stages[1].stage.instructions, "objective text available in the restore frame");
                step=4;frames=0;
            } else if(step==4) {
                Check(c.IsRunning && !c.IsComplete,"restored stage resets partial objective");
                CheckGuidance(flow.sceneGuidance, true, true, flow.stages[1].guidanceTarget, "checkpoint restores lighthouse cues");
                var hint = flow.GestureHints.Single();
                Check(hint.Available(hint.target.position), "restored objective re-enables its gesture");
                c.Complete();area.transform.position=r.locomotion.PlayerFloorPosition;step=5;frames=0;
            } else if(step==5) {
                if(!r.CampaignComplete)return;
                Check(!r.IsSkillAvailable("thunder.sword") && !r.DebugCast("thunder.sword"), "completion removes the grant from both player data and actual casting");
                Check(MatcherFollowsGrants(r), "completed chapter retains only permanent recipes");
                Check(r.PlayerView.CanUseSkill("buff.voice") && !r.Model.HasStageGrant("buff.voice"),
                    "a permanent unlock earned during a loan survives its final exit");
                Check(stageChanges.SequenceEqual(new[]{"exit lighthouse.enter", "enter lighthouse.enter", "exit lighthouse.enter",
                    "enter lighthouse.light", "exit lighthouse.light"}), "stage notifications fire once per exit and entry");
                Check(r.IsSkillUnlocked("buff.zi") && r.IsSkillAvailable("buff.zi"), "permanent shield remains available after stage trials end");
                Check(r.Model.Chapter.CompletedStages.Count==2 && r.Model.ClaimedRewards.Count==2,"all stages completed with exactly two rewards");
                CheckGuidance(flow.sceneGuidance, false, false, null, "lighthouse completion");
                Check(flow.GestureHints == null && flow.TrailHints == null, "completed chapter releases guidance");
                var store=new RpgSaveStore(UnityRpgSaveJson.Instance, r.SavePath,"linear-rpg-player",r.skills.ConfiguredSkills.Select(s=>s.skillId),r.Player.MaxHealth,r.Player.MaxMana,r.skills.ConfiguredSkills.Select(s=>string.IsNullOrEmpty(s.cooldownGroup)?s.skillId:s.cooldownGroup).Distinct());
                Check(store.TryLoad(out var saved,out var reason) && saved.completedStageIds.Count==2,"final save roundtrip");
                Check(r.RestoreCheckpoint() && r.CampaignComplete,"completed chapter restores without replaying objectives");
                File.AppendAllText(Report,"PASS END\n"); Finish(0);
            }
        }catch(Exception e){File.AppendAllText(Report,"FAIL "+e+"\n");Finish(1);}
    }
    static void Finish(int exit) { entered?.Dispose(); exited?.Dispose(); EditorApplication.update-=Tick;SessionState.SetBool(Key+"done",true);SessionState.SetInt(Key+"exit",exit);EditorApplication.ExitPlaymode(); }
}
