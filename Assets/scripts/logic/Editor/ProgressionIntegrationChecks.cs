using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using NarutoVR.SealCombat.Rpg;
using Obj = UnityEngine.Object;

/// <summary>在隔离工程的真实首章 Play 中验收物品、商店、保存重试和章节授权协作。</summary>
[InitializeOnLoad]
public static class ProgressionIntegrationChecks
{
    const string Key = "ProgressionIntegrationChecks.active";
    const string Report = "Logs/progression-integration-check.txt";
    static int step, frames;
    static double deadline;
    static ProgressionIntegrationChecks()
    {
        EditorApplication.playModeStateChanged += state => {
            if (!SessionState.GetBool(Key, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode) {
                step=frames=0; deadline=EditorApplication.timeSinceStartup+60; EditorApplication.update+=Tick;
            }
            if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(Key+"done",false)) {
                SessionState.SetBool(Key,false); EditorApplication.Exit(SessionState.GetInt(Key+"exit",1));
            }
        };
    }
    /// <summary>创建唯一 probe 存档与 QA 场景，不覆盖正式场景或玩家存档。</summary>
    public static void RunBatch()
    {
        if (!Application.isBatchMode || !Application.dataPath.Replace('\\','/').StartsWith("D:/Temp/",StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("仅允许在隔离工程 batchmode 执行");
        Directory.CreateDirectory("Logs");File.WriteAllText(Report,DateTimeOffset.Now.ToString("o")+"\n");
        File.AppendAllText(Report,"REGRESSION final wrist-menu="+NarutoVR.SealCombat.Editor.WristMenuChecks.Run()+"\n");
        EditorSceneManager.OpenScene("Assets/Scene/Chapter01_Awakening.unity");
        NarutoVR.SealCombat.Editor.ProgressionModuleChecks.Run();
        var r=Obj.FindFirstObjectByType<LinearRpgController>();r.autoLoad=false;
        r.saveFileName="probe-progression-integration-"+Guid.NewGuid().ToString("N")+".json";
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),"Assets/Scene/QA_ProgressionIntegration.unity");
        SessionState.SetBool(Key,true);SessionState.SetBool(Key+"done",false);EditorApplication.EnterPlaymode();
    }
    /// <summary>逐帧执行真实模块请求；物品交互和校验门槛使用显式测试输入，不声称是手势通关。</summary>
    static void Tick()
    {
        if(!Application.isPlaying)return;
        try {
            if(EditorApplication.timeSinceStartup>deadline)throw new TimeoutException("step "+step);
            if(++frames<15)return;
            var r=Obj.FindFirstObjectByType<LinearRpgController>();
            var opening=(OpeningTrialChapter)r.chapter;
            var inventory=r.GetComponent<RpgItemInventory>();var shop=r.GetComponent<RpgSkillShop>();
            string item=opening.puzzle.leafId;
            if(step==0) {
                Check(r.CanAct && opening.inventory==inventory && opening.puzzle.inventory==inventory,"opening initializes with explicit separate modules");
                Check(r.Progress.discoveredSkillIds.Count==3 && r.Progress.unlockedSkillIds.Count==0,"initial knowledge does not unlock skills");
                Check(!r.TryAdvanceStage(),"manual input cannot skip opening teaching");
                Check(inventory.Activate(item)==ProgressCommitResult.Rejected && inventory.Collect("missing")==ProgressCommitResult.Rejected,"uncollected or unknown item rejected");
                var invalid=r.CreateProgressDraft();invalid.currency=-1;
                Check(r.CommitProgressChange(invalid)==ProgressCommitResult.Rejected && r.Progress.currency==0,"invalid draft leaves session unchanged");
                var jump=r.CreateProgressDraft();RpgRewards.Grant(jump,"reward."+r.stages[0].stageId,r.stages[0].rewardCurrency,r.stages[0].unlockSkillIds);
                jump.completedStageIds.Add(r.stages[0].stageId);jump.checkpointId=r.checkpointNodes[1].nodeId;
                Check(r.CommitProgressChange(jump)==ProgressCommitResult.Rejected && r.StageIndex==0,"module transaction cannot advance stages or rewards");
                Directory.CreateDirectory(Path.GetDirectoryName(r.SavePath));
                using(var locked=new FileStream(r.SavePath+".tmp",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None)) {
                    Check(inventory.Collect(item)==ProgressCommitResult.PendingSave && r.CheckpointSavePending,"collection survives real disk write failure");
                    Check(r.IsSkillDiscovered("buff.hidden") && !r.IsSkillUnlocked("buff.hidden"),"collection reveals teaching without cast unlock");
                    Check(r.RestoreCheckpoint() && inventory.GetState(item)==ProgressionItemState.Collected,"restore keeps unsaved successful collection");
                    Check(inventory.Activate(item)==ProgressCommitResult.PendingSave && r.IsSkillUnlocked("buff.hidden"),"activation grants permanent skill with pending save");
                    Check(!r.IsSkillAvailable("buff.hidden"),"opening gate remains independent of item activation");
                    var newer=r.CreateProgressDraft();newer.currency=50;
                    Check(r.CommitProgressChange(newer)==ProgressCommitResult.PendingSave,"later transaction replaces pending snapshot");
                }
                typeof(LinearRpgController).GetField("nextSaveRetry",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(r,0d);
                step=1;frames=0;
            } else if(step==1) {
                if(r.CheckpointSavePending)return;
                var store=new RpgSaveStore(r.SavePath,"linear-rpg-player",r.skills.ConfiguredSkills.Select(s=>s.skillId),r.Player.MaxHealth,r.Player.MaxMana,
                    r.skills.ConfiguredSkills.Select(s=>string.IsNullOrEmpty(s.cooldownGroup)?s.skillId:s.cooldownGroup).Distinct());
                Check(store.TryLoad(out var saved,out _) && saved.currency==50 && saved.items.Single().activated,"retry persists the latest combined progress");
                Check(inventory.Activate(item)==ProgressCommitResult.Saved && r.Progress.items.Count==1 && r.Progress.unlockedSkillIds.Count==1,"repeated activation grants nothing twice");
                Check(r.RestoreCheckpoint() && opening.puzzle.IsSeated,"chapter reconstructs seated prop from generic inventory");
                r.Progress.completedStageIds.Add(opening.permanentSkillGate.stageId);
                Check(r.IsSkillAvailable("buff.hidden"),"injected gate completion enables the already unlocked skill");
                r.Progress.completedStageIds.Remove(opening.permanentSkillGate.stageId);
                Check(!r.IsSkillAvailable("buff.hidden"),"removing gate permission does not remove permanent ownership");
                Check(!shop.TryPurchase("missing") && r.Progress.currency==50,"unknown product cannot charge currency");
                var unlock=r.CreateProgressDraft();SkillProgression.Unlock(unlock,new[]{"fireball"});
                Check(r.CommitProgressChange(unlock)==ProgressCommitResult.Saved,"generic skill grant commits without teaching dependency");
                var fire=r.skills.ConfiguredSkills.Single(s=>s.skillId=="fireball");
                var effect=(NarutoVR.SealCombat.ProjectileEffectDefinition)fire.effectDefinition;
                float expectedDamage=effect.damage*(1+effect.damagePerLevel);
                Check(shop.TryPurchase("upgrade.fireball") && r.Progress.currency==20 && SkillProgression.Level(r.Progress,"fireball")==1,"real configured shop upgrade deducts correct cost");
                Check(Mathf.Approximately(effect.damage,expectedDamage),"runtime effect receives its own purchased level");
                Check(!shop.TryPurchase("upgrade.fireball") && r.Progress.currency==20,"insufficient currency cannot purchase another level");
                Check(r.RestoreCheckpoint() && SkillProgression.Level(r.Progress,"fireball")==1 && r.Progress.currency==20,"item skill and shop level restore together");
                Check(r.StageIndex==0 && r.Progress.claimedRewardIds.Count==0,"module changes never complete a chapter stage");
                File.AppendAllText(Report,"PASS END\n");Finish(0);
            }
        } catch(Exception e) {File.AppendAllText(Report,"FAIL "+e+"\n");Finish(1);}
    }
    /// <summary>断言失败立即结束本轮，成功写入独立报告。</summary>
    static void Check(bool condition,string name) {if(!condition)throw new Exception(name);File.AppendAllText(Report,"PASS "+name+"\n");}
    /// <summary>先退出 Play，再以真实结果退出隔离编辑器。</summary>
    static void Finish(int code) {EditorApplication.update-=Tick;SessionState.SetBool(Key+"done",true);SessionState.SetInt(Key+"exit",code);EditorApplication.ExitPlaymode();}
}
