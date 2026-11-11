using System.Linq;
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using NarutoVR.SealCombat.Events;
using NarutoVR.SealCombat.Rpg;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NarutoVR.SealCombat.Editor
{
    /// <summary>在隔离项目的真实 Play 中检查目标组合、操作令牌、重听和音频推进。</summary>
    [InitializeOnLoad]
    public static class ChapterObjectiveBindingChecks
    {
        const string Key = "NarutoVR.ChapterObjectiveBindingChecks";
        const string Report = "Logs/chapter-objective-binding.txt";
        static readonly List<string> results = new();
        static GameObject fixtures;
        static NarrationController operationFirst, audioFirst, continuous;
        static ReachAreaChapterObjective liveArea;
        static LinearRpgController owner;
        static bool started, submittedAfterAudio, reentered;
        static double deadline;

        static ChapterObjectiveBindingChecks()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (!SessionState.GetBool(Key, false)) return;
                if (state == PlayModeStateChange.EnteredPlayMode)
                {
                    deadline = EditorApplication.timeSinceStartup + 90;
                    EditorApplication.update += Tick;
                }
                if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(Key + ".done", false))
                {
                    SessionState.SetBool(Key, false);
                    EditorApplication.Exit(SessionState.GetInt(Key + ".code", 1));
                }
            };
        }

        public static void RunBatch()
        {
            var temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!Application.isBatchMode || !Path.GetFullPath(Application.dataPath).StartsWith(temp, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("请用临时目录中的隔离工程运行专项检查。");
            Directory.CreateDirectory("Logs");
            var scene = EditorSceneManager.OpenScene("Assets/Scene/LinearRpgTest.unity");
            var rpg = Object.FindFirstObjectByType<LinearRpgController>();
            rpg.autoLoad = false;
            rpg.saveFileName = "qa-objective-binding-" + Guid.NewGuid().ToString("N") + ".json";
            rpg.skills.voice.autoStartInEditor = false;
            if (!EditorSceneManager.SaveScene(scene, "Assets/Scene/QA_ObjectiveBinding.unity"))
                throw new IOException("无法保存隔离测试场景。");
            File.WriteAllText(Report, DateTimeOffset.Now.ToString("o") + Environment.NewLine);
            SessionState.SetBool(Key, true);
            SessionState.SetBool(Key + ".done", false);
            EditorApplication.EnterPlaymode();
        }

        static void Tick()
        {
            if (!Application.isPlaying) return;
            try
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("等待就绪或音频自然结束超时。");
                if (!started)
                {
                    var rpg = Object.FindFirstObjectByType<LinearRpgController>();
                    if (!rpg || !rpg.CanAct) return;
                    started = true;
                    owner = rpg;
                    fixtures = new GameObject("章节目标及旁白检查");
                    CheckObjectives(rpg);
                    CheckNarration();
                    StartAudioChecks();
                }
                if (!submittedAfterAudio)
                {
                    if (!audioFirst.AudioCompleted) return;
                    Check(!audioFirst.OperationCompleted && !audioFirst.DialogueCompleted, "音频先自然结束，仍在等待操作");
                    Check(audioFirst.CompleteOperation(audioFirst.CurrentStep), "音频之后的操作被接受");
                    submittedAfterAudio = true;
                }
                if (!reentered)
                {
                    if (!continuous.AudioCompleted) return;
                    Check(!continuous.DialogueCompleted && !continuous.OperationCompleted, "音频结束时玩家已离开，连续目标保持等待");
                    liveArea.area.transform.position = owner.locomotion.PlayerFloorPosition;
                    reentered = true;
                }
                if (!operationFirst.DialogueCompleted || !audioFirst.DialogueCompleted || !continuous.DialogueCompleted) return;
                Check(true, "两种完成顺序均通过双条件并自然结束");
                Check(true, "音频结束后重新入区才通过连续目标");
                Finish(null);
            }
            catch (Exception error) { Finish(error); }
        }

        static void CheckObjectives(LinearRpgController rpg)
        {
            var group = fixtures.AddComponent<CompositeChapterObjective>();
            var first = fixtures.AddComponent<SignalChapterObjective>();
            var second = fixtures.AddComponent<SignalChapterObjective>();
            var area = fixtures.AddComponent<ReachAreaChapterObjective>();
            area.area = fixtures.AddComponent<BoxCollider>();
            area.area.isTrigger = true;
            fixtures.transform.position = rpg.locomotion.PlayerFloorPosition + Vector3.right * 5;
            group.children = new ChapterObjective[] {first, area};
            group.Begin(rpg);
            int changes = 0;
            first.StateChanged += source => { if (source == first) changes++; };
            var old = first.CaptureCompletion();
            first.Complete(); first.Complete();
            Check(changes == 1 && !group.IsComplete, "重复信号只通知一次，AND 仍要求到达");
            fixtures.transform.position = rpg.locomotion.PlayerFloorPosition;
            Check(group.IsComplete, "AND 的所有条件成立");
            fixtures.transform.position += Vector3.right * 5;
            Check(!group.IsComplete, "离开区域使连续状态和 AND 由真回到假");
            group.End();
            group.match = ChapterObjectiveMatch.Any;
            group.children = new ChapterObjective[] {first, second};
            group.Begin(rpg);
            old();
            Check(!group.IsComplete && !first.IsComplete, "重绑同一目标后旧轮异步信号被拒");
            second.Complete();
            Check(group.IsComplete && !first.IsComplete, "OR 只需一项成立");
            var competing = fixtures.AddComponent<CompositeChapterObjective>();
            competing.children = new ChapterObjective[] {first};
            bool conflict = false;
            try { competing.Begin(rpg); } catch (InvalidOperationException) { conflict = true; }
            Check(conflict && first.IsRunning && group.IsComplete && !competing.IsRunning, "第二个根不能夺取第一个根的子目标");
            var beforeDisable = first.CaptureCompletion();
            group.enabled = false;
            beforeDisable();
            group.enabled = true;
            beforeDisable();
            Check(first.IsRunning && !first.IsComplete && !group.IsComplete, "组合启停后旧信号不能影响新一轮");
            group.End();

            void Invalid(ChapterObjective[] children, string label)
            {
                group.children = children;
                try { group.ValidateObjective(); }
                catch (InvalidOperationException) { Check(true, label); return; }
                throw new Exception(label);
            }
            Invalid(Array.Empty<ChapterObjective>(), "空组合被拒");
            Invalid(new ChapterObjective[] {null}, "空子项被拒");
            Invalid(new ChapterObjective[] {group}, "组合自环被拒");
            Invalid(new ChapterObjective[] {first, first}, "同一子目标重复被拒");
            group.children = new ChapterObjective[] {first};
            var used = new HashSet<ChapterObjective>();
            ChapterObjective.ValidateTree(group, used);
            bool rejected = false;
            try { ChapterObjective.ValidateTree(first, used); } catch (InvalidOperationException) { rejected = true; }
            Check(rejected, "共享配置集合拒绝跨根重复目标");

            var saved = fixtures.AddComponent<CompletedStageChapterObjective>();
            saved.stage = rpg.stages[0];
            var original = rpg.Model.Chapter.CompletedStages.ToArray();
            var restore = typeof(ChapterModel).GetMethod("Restore",System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            bool added = !rpg.Model.Chapter.IsComplete(saved.stage.stageId);
            if (added) restore.Invoke(rpg.Model.Chapter,new object[] { original.Concat(new[] { saved.stage.stageId }).ToArray() });
            try
            {
                var dialogue = Narration(NarrationAdvanceMode.Operation);
                dialogue.StartDialogue();
                var captured = dialogue.CurrentStep;
                int accepts = 0;
                void Read(ChapterObjective source) { if (source == saved && saved.IsComplete && dialogue.CompleteOperation(captured)) accepts++; }
                saved.StateChanged += Read;
                try { saved.Begin(rpg); Read(saved); Read(saved); }
                finally { saved.StateChanged -= Read; saved.End(); }
                Check(accepts == 1 && captured.OperationDone, "先订阅再读已完成阶段，初始状态立即满足且只提交一次");
            }
            finally { if (added) restore.Invoke(rpg.Model.Chapter,new object[] { original }); }
        }

        static NarrationController Narration(params NarrationAdvanceMode[] modes)
        {
            var actor = new GameObject("隔离旁白");
            actor.transform.SetParent(fixtures.transform);
            var controller = actor.AddComponent<NarrationController>();
            foreach (var mode in modes)
                controller.lines.Add(new NarrationLine {lineId = "line." + (controller.lines.Count + 1),
                    advanceMode = mode, audioFileName = mode == NarrationAdvanceMode.Operation ? "" : "Chapter01/RS02.wav"});
            return controller;
        }

        static void CheckNarration()
        {
            var controller = Narration(NarrationAdvanceMode.Operation, NarrationAdvanceMode.Operation);
            var foreign = Narration(NarrationAdvanceMode.Operation);
            foreign.StartDialogue();
            var order = new List<string>();
            controller.StepStarted += s => order.Add("+" + s.LineId);
            controller.StepEnded += s => order.Add((controller.IsCurrentStep(s) ? "有效" : "-") + s.LineId);
            int authoredStarts = 0;
            controller.lines[0].onStarted.AddListener(() => authoredStarts++);
            controller.StartDialogue();
            var old = controller.CurrentStep;
            Check(old.Target == controller && old.Index == 0 && old.DisplayNumber == 1 && old.LineId == "line.1", "作者键、显示序号及来源分开");
            var queued = new NarrationOperationNotice(old);
            controller.RestartDialogue();
            var restarted = controller.CurrentStep;
            EventCenter.Publish(NarrationEvents.OperationCompleted, queued);
            Check(old.Generation != restarted.Generation && !restarted.OperationDone && !controller.CompleteOperation(old) &&
                !foreign.CompleteOperation(restarted), "同序号重启及错控制器的旧操作均被拒");
            Check(controller.RefreshOperationBinding(), "重听重绑成功");
            var refreshed = controller.CurrentStep;
            NarrationEvents.PublishOperation(restarted);
            Check(!refreshed.OperationDone && refreshed.Generation == restarted.Generation && refreshed != restarted && authoredStarts == 2,
                "重听保留轮次、拒绝旧监听并避免重放剧情开始事件");
            NarrationEvents.PublishOperation(refreshed);
            Check(refreshed.OperationDone && !controller.CompleteOperation(refreshed), "当前通知只提交一次");
            controller.RefreshOperationBinding();
            Check(controller.CurrentStep.OperationDone, "重听保留已完成的操作");
            controller.SendMessage("Update");
            Check(controller.CurrentLineIndex == 1 && !controller.CompleteOperation(refreshed), "同轮换句拒绝前一句的操作");
            controller.StopDialogue();
            Check(!controller.DialogueCompleted && string.Join(",",order) ==
                "+line.1,-line.1,+line.1,-line.1,+line.1,-line.1,+line.1,-line.1,+line.2,-line.2",
                "重启、重听、推进和主动停止均先解绑，再进入下一句");
            controller.StartDialogue();
            var disabled = controller.CurrentStep;
            controller.enabled = false;
            controller.enabled = true;
            controller.StartDialogue();
            Check(!controller.CompleteOperation(disabled) && !controller.OperationCompleted, "禁用再启用后旧通知被拒");
        }

        static void StartAudioChecks()
        {
            operationFirst = Narration(NarrationAdvanceMode.AudioAndOperation);
            audioFirst = Narration(NarrationAdvanceMode.AudioAndOperation);
            operationFirst.StartDialogue();
            var step = operationFirst.CurrentStep;
            Check(operationFirst.CompleteOperation(step) && !step.AudioDone && !operationFirst.DialogueCompleted, "操作先完成，仍在等待音轨");
            var voice = operationFirst.GetComponent<AudioSource>();
            var clip = voice.clip;
            operationFirst.RefreshOperationBinding();
            Check(clip && voice.clip == clip && operationFirst.OperationCompleted && !operationFirst.AudioCompleted,
                "重听保留音轨与已完成操作");
            audioFirst.StartDialogue();
            continuous = Narration(NarrationAdvanceMode.AudioAndOperation);
            var zone = new GameObject("连续区域测试");
            zone.transform.SetParent(fixtures.transform);
            zone.transform.position = owner.locomotion.PlayerFloorPosition;
            liveArea = zone.AddComponent<ReachAreaChapterObjective>();
            liveArea.area = zone.AddComponent<BoxCollider>();
            liveArea.area.isTrigger = true;
            liveArea.Begin(owner);
            continuous.StartDialogue();
            Check(continuous.BindOperationState(continuous.CurrentStep, () => liveArea.IsComplete) && continuous.OperationCompleted,
                "进入区域立即读取为满足");
            liveArea.area.transform.position += Vector3.right * 20;
            Check(!continuous.CompleteOperation(continuous.CurrentStep), "一次性通知无法越过连续目标");
            var automatic = Narration(NarrationAdvanceMode.AudioFinished);
            automatic.StartDialogue();
            Check(!automatic.CompleteOperation(automatic.CurrentStep), "纯音频句拒绝外部推进");
        }

        static void Check(bool ok, string label)
        {
            if (!ok) throw new InvalidOperationException(label);
            results.Add("PASS " + label);
        }

        static void Finish(Exception failure)
        {
            EditorApplication.update -= Tick;
            if (fixtures) Object.DestroyImmediate(fixtures);
            results.Add(failure == null ? "PASS END" : "FAIL " + failure);
            File.AppendAllLines(Report, results);
            SessionState.SetBool(Key + ".done", true);
            SessionState.SetInt(Key + ".code", failure == null ? 0 : 1);
            EditorApplication.ExitPlaymode();
        }
    }
}
#endif
