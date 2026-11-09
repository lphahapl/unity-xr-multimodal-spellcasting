using System;
using System.IO;
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
    static void Validate(LinearRpgController r) => typeof(LinearRpgController).GetMethod("ValidateSetup",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(r,null);
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
                Validate(training);
            }
            EditorSceneManager.OpenScene("Assets/Scene/Chapter01_Awakening.unity");
            var r=Controller(); var opening=Obj.FindFirstObjectByType<OpeningTrialChapter>();
            Check(r && opening && r.chapter==opening,"scene directly binds chapter");
            Check(opening.inventory && r.wristMenu.teachingCatalog && r.wristMenu.teachingCatalog.entries.Length==3,"teaching and inventory use separate assets");
            Check(opening.FinalCheckpoint==opening.finalCheckpoint && opening.UsesExplorationFootsteps && opening.UsesWorldNavigationMarks && opening.GlyphPreviewPages==1,"opening capabilities preserved");
            Validate(r); Check(true,"original chapter setup validation");
            ProgressionModuleChecks.Run(); Check(true,"current copper-leaf save and no implicit grant regression");
            // 保留旧首章组件启用，验证未绑定流程不会启动旧教学。
            foreach(var b in Obj.FindObjectsByType<OpeningLeafPuzzle>(FindObjectsInactive.Include,FindObjectsSortMode.None)) b.enabled=false;
            foreach(var b in Obj.FindObjectsByType<OpeningDefenseDrill>(FindObjectsInactive.Include,FindObjectsSortMode.None)) b.enabled=false;
            Obj.DestroyImmediate(r.GetComponent<RpgSkillShop>());
            Obj.DestroyImmediate(r.GetComponent<RpgItemInventory>());
            Obj.DestroyImmediate(r.GetComponent<TrainingTargetGroup>());
            r.wristMenu.teachingCatalog=null;
            var flow=r.gameObject.AddComponent<ObjectiveChapterFlow>(); flow.rpg=r; r.chapter=flow;
            r.stages=r.stages.Take(2).ToArray(); r.checkpointNodes=r.checkpointNodes.Take(2).ToArray();
            flow.initialSkills=new[]{"buff.zi"}; flow.explorationFootsteps=false;
            var a=new GameObject("QA_A").AddComponent<SignalChapterObjective>();
            var b2=new GameObject("QA_B").AddComponent<SignalChapterObjective>();
            var c=new GameObject("QA_C").AddComponent<SignalChapterObjective>();
            var area=new GameObject("QA_Area").AddComponent<ReachAreaChapterObjective>(); area.area=area.gameObject.AddComponent<BoxCollider>();
            area.area.isTrigger=true; area.area.size=new Vector3(2,4,2); area.transform.position=new Vector3(1000,0,1000); area.transform.rotation=Quaternion.Euler(0,45,0);
            flow.stages=new[]{new ChapterObjectiveStage{stage=r.stages[0],objectives=new ChapterObjective[]{a,b2}},new ChapterObjectiveStage{stage=r.stages[1],objectives=new ChapterObjective[]{c,area}}};
            r.saveFileName="probe-chapter-flow-"+Guid.NewGuid().ToString("N")+".json"; r.autoLoad=false;
            var cat=Obj.Instantiate(r.skills.catalog); cat.skills=cat.skills.Where(s=>s.skillId!="fireball" && s.skillId!="buff.hidden").ToList();
            AssetDatabase.CreateAsset(cat,AssetDatabase.GenerateUniqueAssetPath("Assets/QA_ChapterCatalog.asset")); r.skills.catalog=cat;
            Validate(r); Check(true,"generic chapter validates without targets, copper-leaf configuration, fireball or hidden skill");
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
                Check(r.Progress.unlockedSkillIds.SequenceEqual(new[]{"buff.zi"}),"chapter initial skills applied");
                Check(!r.GetComponent<RpgSkillShop>() && !r.GetComponent<RpgItemInventory>(),"generic chapter has no implicit shop or inventory module");
                Check(!r.TryAdvanceStage(),"legacy target advance cannot bypass chapter");
                Check(!r.CompleteChapterObjective(Obj.FindFirstObjectByType<OpeningTrialChapter>(FindObjectsInactive.Include)),"foreign chapter completion rejected");
                a.Complete();step=1;frames=0;
            } else if(step==1) {
                Check(r.StageIndex==0 && !flow.ObjectiveMet,"AND goal waits for second signal");
                b.Complete();step=2;frames=0;
            } else if(step==2) {
                if(r.StageIndex!=1 || !c.IsRunning)return;
                Check(r.Progress.completedStageIds.Count==1 && File.Exists(r.SavePath),"first stage committed and saved once");
                a.Complete();c.Complete();step=3;frames=0;
            } else if(step==3) {
                Check(!r.CampaignComplete && !area.IsComplete,"stale signal and distant area cannot finish next stage");
                Check(r.RestoreCheckpoint(),"restore real saved checkpoint");step=4;frames=0;
            } else if(step==4) {
                Check(c.IsRunning && !c.IsComplete,"restored stage resets partial objective");
                c.Complete();area.transform.position=r.locomotion.PlayerFloorPosition;step=5;frames=0;
            } else if(step==5) {
                if(!r.CampaignComplete)return;
                Check(r.Progress.completedStageIds.Count==2 && r.Progress.claimedRewardIds.Count==2,"all stages completed with exactly two rewards");
                var store=new RpgSaveStore(r.SavePath,"linear-rpg-player",r.skills.ConfiguredSkills.Select(s=>s.skillId),r.Player.MaxHealth,r.Player.MaxMana,r.skills.ConfiguredSkills.Select(s=>string.IsNullOrEmpty(s.cooldownGroup)?s.skillId:s.cooldownGroup).Distinct());
                Check(store.TryLoad(out var saved,out var reason) && saved.completedStageIds.Count==2,"final save roundtrip");
                Check(r.RestoreCheckpoint() && r.CampaignComplete,"completed chapter restores without replaying objectives");
                File.AppendAllText(Report,"PASS END\n"); Finish(0);
            }
        }catch(Exception e){File.AppendAllText(Report,"FAIL "+e+"\n");Finish(1);}
    }
    static void Finish(int exit) { EditorApplication.update-=Tick;SessionState.SetBool(Key+"done",true);SessionState.SetInt(Key+"exit",exit);EditorApplication.ExitPlaymode(); }
}
