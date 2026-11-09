using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using NarutoVR.SealCombat.Rpg;
using Obj = UnityEngine.Object;

/// <summary>在隔离工程中验证训练场章节的真实 Play 推进、靶复位与保存。</summary>
[InitializeOnLoad]
public static class TrainingChapterChecks
{
    const string Key = "TrainingChapterChecks.active";
    const string Report = "Logs/training-chapter-check.txt";
    static int step, frames;
    static double deadline;

    /// <summary>域重载后仅恢复显式启动的验收会话，不自动改变正常编辑器状态。</summary>
    static TrainingChapterChecks()
    {
        EditorApplication.playModeStateChanged += state => {
            if (!SessionState.GetBool(Key, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            { step=0;frames=0;deadline=EditorApplication.timeSinceStartup+45;EditorApplication.update+=Tick; }
            if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(Key+"done",false))
            { SessionState.SetBool(Key,false);EditorApplication.Exit(SessionState.GetInt(Key+"exit",1)); }
        };
    }
    /// <summary>只接受临时隔离副本，创建唯一 probe 档，正式玩家存档不参与测试。</summary>
    public static void RunBatch()
    {
        if (!Application.isBatchMode || !Application.dataPath.Replace('\\','/').StartsWith("D:/Temp/",StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("仅在 D:/Temp 的 batchmode 隔离工程运行");
        Directory.CreateDirectory("Logs");File.WriteAllText(Report,DateTimeOffset.Now.ToString("o")+"\n");
        // 这些现有检查的夹具也已迁到显式训练设施，重跑以确认调用点没有只改到能编译。
        File.AppendAllText(Report,"REGRESSION data="+NarutoVR.SealCombat.Editor.RpgDataChecks.Run()+"\n");
        File.AppendAllText(Report,"REGRESSION wrist-menu="+NarutoVR.SealCombat.Editor.WristMenuChecks.Run()+"\n");
        File.AppendAllText(Report,"REGRESSION wrist-tap="+NarutoVR.SealCombat.Editor.WristTapChecks.Run()+"\n");
        File.AppendAllText(Report,"REGRESSION presentation="+NarutoVR.SealCombat.Editor.InteractionFeelPresentationChecks.Run()+"\n");
        EditorSceneManager.OpenScene("Assets/Scene/LinearRpgTest.unity");
        var r=Obj.FindFirstObjectByType<LinearRpgController>();
        r.autoLoad=false;r.GetComponent<TrainingChapterFlow>().autoAdvance=false;r.GetComponent<TrainingChapterFlow>().advanceDelay=.5f;
        r.saveFileName="probe-training-chapter-"+Guid.NewGuid().ToString("N")+".json";
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        SessionState.SetBool(Key,true);SessionState.SetBool(Key+"done",false);EditorApplication.EnterPlaymode();
    }
    /// <summary>断言失败中止该轮；成功记录到独立文本报告。</summary>
    static void Check(bool ok,string message)
    { if(!ok)throw new Exception(message);File.AppendAllText(Report,"PASS "+message+"\n"); }
    /// <summary>逐帧验证真实控制器及训练靶，使用现有调试命中入口，不模拟存档结果。</summary>
    static void Tick()
    {
        if (!Application.isPlaying) return;
        try {
            if(EditorApplication.timeSinceStartup>deadline)throw new TimeoutException("step "+step);
            if(++frames<10)return;
            var r=Obj.FindFirstObjectByType<LinearRpgController>();
            if(step==0) {
                File.AppendAllText(Report,"REGRESSION "+NarutoVR.SealCombat.Editor.P1RoomEncounterChecks.Run()+"\n");
                Check(r.CanAct && r.chapter is TrainingChapterFlow,"training scene initializes with explicit chapter");
                Check(r.Entity && r.Player!=null,"explicit PlayerObj initializes");
                Check(r.Progress.unlockedSkillIds.Contains("fireball") && r.wristMenu.showUndiscoveredEntries,"training starting skill and course policy");
                Check(!r.TryAdvanceStage(),"unbeaten targets reject manual advance");
                r.GetComponent<TrainingTargetGroup>().DebugDefeatAll();
                Check(r.TryAdvanceStage() && r.StageIndex==1,"manual completion moves to next stage");
                Check(r.GetComponent<TrainingTargetGroup>().targets.All(t=>!t.IsDefeated),"new stage resets training targets");
                Check(File.Exists(r.SavePath) && r.Progress.claimedRewardIds.Count==1,"manual stage reward saved exactly once");
                r.GetComponent<TrainingTargetGroup>().DebugDefeatAll();
                Check(r.RestoreCheckpoint() && r.StageIndex==1 && r.GetComponent<TrainingTargetGroup>().targets.All(t=>!t.IsDefeated),"restore resets current targets");
                r.GetComponent<TrainingChapterFlow>().autoAdvance=true;r.GetComponent<TrainingTargetGroup>().DebugDefeatAll();step=1;frames=0;
            } else if(step==1) {
                if(r.StageIndex!=2)return;
                Check(r.Progress.completedStageIds.Count==2 && r.GetComponent<TrainingTargetGroup>().targets.All(t=>!t.IsDefeated),"timed automatic completion and target reset");
                r.GetComponent<TrainingTargetGroup>().DebugDefeatAll();step=2;frames=0;
            } else if(step==2) {
                if(!r.CampaignComplete)return;
                Check(r.Progress.completedStageIds.Count==3 && r.Progress.claimedRewardIds.Count==3,"all training stages complete once");
                Check(!r.TryAdvanceStage(),"completed campaign cannot award again");
                Check(r.RestoreCheckpoint() && r.CampaignComplete,"completed training checkpoint reloads");
                File.AppendAllText(Report,"PASS END\n");Finish(0);
            }
        }catch(Exception e){File.AppendAllText(Report,"FAIL "+e+"\n");Finish(1);}
    }
    /// <summary>退出本轮 Play，待编辑模式恢复后以真实通过状态退出 batchmode。</summary>
    static void Finish(int exit)
    {EditorApplication.update-=Tick;SessionState.SetBool(Key+"done",true);SessionState.SetInt(Key+"exit",exit);EditorApplication.ExitPlaymode();}
}
