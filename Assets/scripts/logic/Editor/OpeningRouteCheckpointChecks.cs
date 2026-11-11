using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NarutoVR.SealCombat.Rpg;
using UnityEngine;

namespace NarutoVR.SealCombat.Editor
{
    public static class OpeningRouteCheckpointChecks
    {
        // Seed successive v2 files, then go through the real restore, Unity scene listeners and landing checks.
        public static string Run(LinearRpgController player)
        {
            if (!Application.isBatchMode || player.autoLoad || !player.saveFileName.StartsWith("probe-mvc-", StringComparison.Ordinal))
                throw new InvalidOperationException("需要独立场景与专用测试存档。");
            var chapter = (OpeningTrialChapter)player.chapter;
            CheckDoorOwnership(chapter, player);
            const string nearShot = "opening.ROOM_FIRE", sideShot = "opening.ROOM_FIRE2";
            SkillTrainingTarget TargetForStage(string stageId) => chapter.sections.Single(section => section.stage.stageId == stageId)
                .lines.Where(line => line.action == OpeningAction.HitTarget).Select(line => line.target).Distinct().Single();
            var mainTarget = TargetForStage(nearShot);
            var sideTarget = TargetForStage(sideShot);
            var visibility = chapter.sceneGuidance.stageVisibility.Single(cue => cue.sceneObject == sideTarget.gameObject);
            if (mainTarget == sideTarget) throw new InvalidOperationException("近射与侧靶教学错误地使用了同一个训练靶。");
            var skills = player.skills.ConfiguredSkills.Where(skill => skill).ToArray();
            var store = new RpgSaveStore(UnityRpgSaveJson.Instance, player.SavePath, "linear-rpg-player", skills.Select(skill => skill.skillId),
                player.PlayerView.MaxHealth, player.PlayerView.MaxMana,
                skills.Select(skill => string.IsNullOrEmpty(skill.cooldownGroup) ? skill.skillId : skill.cooldownGroup).Distinct());
            var snapshot = player.CaptureProgressSnapshot();
            var leaf = chapter.inventory.catalog.Find(chapter.puzzle.leafId);
            void Merge(List<string> target, IEnumerable<string> ids)
            {
                foreach (string id in ids) if (!target.Contains(id)) target.Add(id);
            }
            for (int completed = 0; completed <= chapter.sections.Length; completed++)
            {
                bool finished = completed == chapter.sections.Length;
                int index = Math.Min(completed, chapter.sections.Length - 1);
                snapshot.checkpointId = finished ? chapter.FinalCheckpoint.nodeId : player.checkpointNodes[index].nodeId;
                if (!store.TrySave(snapshot, out string reason)) throw new InvalidOperationException(reason);
                if (!player.RestoreCheckpoint() || player.StageIndex != index || player.CampaignComplete != finished ||
                    player.Model.ClaimedRewards.Count != completed || player.PlayerView.Currency != snapshot.currency ||
                    chapter.puzzle.IsSeated != snapshot.items.Any(item => item.itemId == leaf.itemId && item.activated))
                    throw new InvalidOperationException("恢复正式阶段 " + completed + " 失败：" + player.Status);
                string stageId = finished ? null : chapter.sections[index].stage.stageId;
                foreach (var cue in chapter.sceneGuidance.stageVisibility)
                    if (cue.sceneObject.activeSelf != (!finished && cue.stageIds.Contains(stageId)))
                        throw new InvalidOperationException($"阶段 {stageId} 的场景物体显隐偏离配置：{cue.sceneObject.name}");
                for (int i = 0; i < chapter.sections.Length; i++)
                {
                    var door = chapter.sections[i].doorOpenedOnCompletion;
                    if (door && door.activeSelf != (i >= completed))
                        throw new InvalidOperationException($"完成 {completed} 段后门体状态错误：{door.name}");
                }
                if (!mainTarget.gameObject.activeSelf)
                    throw new InvalidOperationException("恢复阶段后主靶被关闭：" + stageId);
                CheckSideTarget(sideTarget, !finished && visibility.stageIds.Contains(stageId), stageId ?? "通关");
                if (stageId == nearShot || stageId == sideShot)
                {
                    // Change only the isolated Play instance. Restoring the actual checkpoint exercises all stage writers.
                    var authored = visibility.stageIds;
                    try
                    {
                        visibility.stageIds = new[] { nearShot };
                        if (!player.RestoreCheckpoint()) throw new InvalidOperationException(player.Status);
                        CheckSideTarget(sideTarget, stageId == nearShot, "改配后 " + stageId);
                    }
                    finally
                    {
                        visibility.stageIds = authored;
                        if (!player.RestoreCheckpoint()) throw new InvalidOperationException(player.Status);
                    }
                }
                if (finished) break;
                var section = chapter.sections[index];
                snapshot.completedStageIds.Add(section.stage.stageId);
                snapshot.claimedRewardIds.Add("reward." + section.stage.stageId);
                snapshot.currency += section.stage.rewardCurrency;
                Merge(snapshot.unlockedSkillIds, section.stage.unlockSkillIds);
                Merge(snapshot.discoveredSkillIds, section.stage.unlockSkillIds);
                if (section.lines.Any(line => line.action == OpeningAction.CollectLeaf))
                {
                    snapshot.items.Add(new RpgItemProgressData { itemId = leaf.itemId });
                    Merge(snapshot.discoveredSkillIds, leaf.discoverSkillIds);
                }
                if (section.lines.Any(line => line.action == OpeningAction.InsertLeaf))
                {
                    CheckInsertion(player, chapter);
                    snapshot.items.Single(item => item.itemId == leaf.itemId).activated = true;
                    Merge(snapshot.unlockedSkillIds, leaf.unlockSkillIds);
                    Merge(snapshot.discoveredSkillIds, leaf.unlockSkillIds);
                }
            }
            return $"首章 {chapter.sections.Length} 个阶段及通关态均从 v2 恢复。{chapter.sceneGuidance.stageVisibility.Length} 个场景物体依阶段表显隐，门体按完成进度放行，拒绝显隐表或标记与门体重复绑定。主靶保持启用，副靶的显隐及命中结果逐阶段符合配置（{string.Join("、", visibility.stageIds)}）。改配到近射阶段后，两个阶段均按新规则显隐和受击。LF02 合成捏取、持有、送入及松手经过真实三点吸附，激活一次并写入 v2。";
        }

        static void CheckDoorOwnership(OpeningTrialChapter chapter, LinearRpgController player)
        {
            var section = chapter.sections.First(s => s.doorOpenedOnCompletion);
            var door = section.doorOpenedOnCompletion;
            void ExpectRejected(GameObject duplicate)
            {
                section.doorOpenedOnCompletion = duplicate;
                try { chapter.ValidateChapter(player); }
                catch (InvalidOperationException e) when (e.Message.Contains("只能由一个阶段的完成条件控制显隐")) { return; }
                finally { section.doorOpenedOnCompletion = door; }
                throw new InvalidOperationException($"门体重复所有权未被拒绝：{duplicate.name}");
            }
            ExpectRejected(chapter.sceneGuidance.stageVisibility[0].sceneObject);
            ExpectRejected(chapter.sceneGuidance.targetHighlights.First(cue => cue.marker).marker);
            ExpectRejected(chapter.sections.First(s => s.doorOpenedOnCompletion && s.doorOpenedOnCompletion != door).doorOpenedOnCompletion);
        }

        static void CheckSideTarget(SkillTrainingTarget target, bool expectedVisible, string context)
        {
            if (target.gameObject.activeInHierarchy != expectedVisible || target.ApplyHit(1f) != expectedVisible)
                throw new InvalidOperationException($"{context}：副靶本体显隐或命中结果不符合预期 {expectedVisible}。");
        }

        static void CheckInsertion(LinearRpgController player, OpeningTrialChapter chapter)
        {
            var puzzle = chapter.puzzle;
            var menu = player.wristMenu;
            bool wasEnabled = menu.enabled;
            int reached = 0, completed = 0;
            void Point(GameObject obj, int index) { if (obj == puzzle.leaf) reached++; }
            void Done(GameObject obj) { if (obj == puzzle.leaf) completed++; }
            const BindingFlags member = BindingFlags.Instance | BindingFlags.NonPublic;
            var input = typeof(WristMenuController).GetField("input", member);
            var tickPuzzle = typeof(OpeningLeafPuzzle).GetMethod("LateUpdate", member);
            var hidden = new List<GameObject>();
            void Hand(Vector3 tip, bool pressed)
            {
                input.SetValue(menu, new WristMenuInput { rightValid = true, rightObserved = Time.realtimeSinceStartupAsDouble,
                    rightTip = tip, pinchValid = true, systemPinch = true, systemPressed = pressed });
                tickPuzzle.Invoke(puzzle, null);
            }
            puzzle.slot.OnPointReached += Point;
            puzzle.slot.OnSnapCompleted += Done;
            try
            {
                menu.enabled = false;
                for (var parent = puzzle.slot.transform; parent; parent = parent.parent)
                    if (!parent.gameObject.activeSelf) { hidden.Add(parent.gameObject); parent.gameObject.SetActive(true); }
                typeof(OpeningTrialChapter).GetMethod("Update", member).Invoke(chapter, null);
                if (!puzzle.slot.isActiveAndEnabled) throw new InvalidOperationException("LF02 未接入吸附区。");
                Hand(puzzle.leaf.transform.position, false);
                Hand(puzzle.leaf.transform.position, true);
                Hand(puzzle.slotEntry.position, true);
                if (puzzle.slot.SnappedObject) throw new InvalidOperationException("铜叶持有中提前吸附。");
                Hand(puzzle.slotEntry.position + puzzle.slotEntry.right * .08f, false);
                if (puzzle.slot.SnappedObject != puzzle.leaf) throw new InvalidOperationException("在槽口自然松手未入匣：" + puzzle.slot.LastFailure);
                // Advance the production interpolation using one long frame, with all normal point callbacks.
                puzzle.slot.GetType().GetMethod("Advance", member).Invoke(puzzle.slot, new object[] { 10f });
                if (!puzzle.IsSeated || reached != puzzle.slot.PointCount || completed != 1 ||
                    !player.CaptureProgressSnapshot().items.Single(item => item.itemId == puzzle.leafId).activated)
                    throw new InvalidOperationException($"真实吸附或存档失败，路点 {reached}，完成回报 {completed}。");
            }
            finally
            {
                puzzle.slot.OnPointReached -= Point;
                puzzle.slot.OnSnapCompleted -= Done;
                foreach (var item in hidden) item.SetActive(false);
                menu.enabled = wasEnabled;
            }
        }
    }
}
