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
            // Authoring order in the lesson and target modules is independent from the campaign route.
            var ordered = player.stages.Select(stage => chapter.sections.Single(row => row.stage.stageId == stage.stageId)).ToArray();
            Array.Reverse(chapter.sections);
            Array.Reverse(chapter.targetGroup.stageHealth);
            chapter.ValidateChapter(player);
            var original = chapter.targetGroup.stageHealth[0];
            try
            {
                var duplicate = chapter.targetGroup.stageHealth[1];
                chapter.targetGroup.stageHealth[0] = duplicate;
                bool rejected = false;
                try { chapter.targetGroup.Validate(player); }
                catch (InvalidOperationException e) { rejected = e.Message.Contains(duplicate.stage.stageId); }
                if (!rejected) throw new InvalidOperationException("重复靶配置未报告阶段身份。");
            }
            finally { chapter.targetGroup.stageHealth[0] = original; chapter.targetGroup.Validate(player); }
            if (Mathf.Abs(player.locomotion.exploreSpeed - 1.25f) > .0001f)
                throw new InvalidOperationException("首章实际探索步速异常");
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
                string stageId = finished ? null : ordered[index].stage.stageId;
                foreach (var cue in chapter.sceneGuidance.stageVisibility)
                    if (cue.sceneObject.activeSelf != (!finished && cue.stageIds.Contains(stageId)))
                        throw new InvalidOperationException($"阶段 {stageId} 的场景物体显隐偏离配置：{cue.sceneObject.name}");
                for (int i = 0; i < chapter.sections.Length; i++)
                {
                    var door = ordered[i].doorOpenedOnCompletion;
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
                var section = ordered[index];
                if (section.alignDiscs)
                {
                    var puzzle = chapter.puzzle;
                    using var hand = new PuzzleHand(player, puzzle);
                    puzzle.EnablePuzzle();
                    var tip = puzzle.outerHandle.position;
                    var before = puzzle.outerDisc.localRotation;
                    hand.Move(tip, false);
                    hand.Move(tip, true);
                    if (!player.Input.InteractionOwnsHand) throw new InvalidOperationException("无腕匣视图时未抓住外盘");
                    hand.Move(tip + puzzle.outerDisc.parent.right * .12f + puzzle.outerDisc.parent.forward * .06f, true);
                    if (Quaternion.Angle(before, puzzle.outerDisc.localRotation) < .1f) throw new InvalidOperationException("手指移动未转动外盘");
                    hand.Move(tip, false);
                    if (player.Input.InteractionOwnsHand) throw new InvalidOperationException("转盘松手后未交还输入");
                }
                if (section.lines.Any(line => line.action == OpeningAction.CollectLeaf))
                {
                    var puzzle = chapter.puzzle;
                    using var hand = new PuzzleHand(player, puzzle);
                    puzzle.EnableReward();
                    hand.Move(puzzle.leaf.transform.position, false);
                    hand.Move(puzzle.leaf.transform.position, true);
                    if (!puzzle.IsEarnedLeaf(puzzle.leaf) || !player.Input.InteractionOwnsHand)
                        throw new InvalidOperationException("无腕匣视图时未取得奖励铜叶");
                    hand.Move(puzzle.prizeRest.position, false);
                    if (player.Input.InteractionOwnsHand) throw new InvalidOperationException("奖励铜叶松手后未交还输入");
                }
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
                    var beforeInsertion = player.CaptureProgressSnapshot();
                    CheckInsertion(player, chapter, false);
                    if (!store.TrySave(beforeInsertion, out string restoreReason) || !player.RestoreCheckpoint())
                        throw new InvalidOperationException("无法恢复入匣前的测试进度：" + restoreReason);
                    CheckInsertion(player, chapter, true);
                    snapshot.items.Single(item => item.itemId == leaf.itemId).activated = true;
                    Merge(snapshot.unlockedSkillIds, leaf.unlockSkillIds);
                    Merge(snapshot.discoveredSkillIds, leaf.unlockSkillIds);
                }
            }
            Array.Reverse(chapter.sections);
            Array.Reverse(chapter.targetGroup.stageHealth);
            chapter.ValidateChapter(player);
            return $"首章 {chapter.sections.Length} 个阶段及通关态均从 v2 恢复。{chapter.sceneGuidance.stageVisibility.Length} 个场景物体依阶段表显隐，门体按完成进度放行，拒绝显隐表或标记与门体重复绑定。主靶保持启用，副靶的显隐及命中结果逐阶段符合配置（{string.Join("、", visibility.stageIds)}）。改配到近射阶段后，两个阶段均按新规则显隐和受击。阶段键独立于教学与靶表顺序，重复配置拒绝。玩家手部真实采样入口在缺少视图时转盘、取得并松开铜叶。LF02 合成捏取、持有、送入及松手经过真实三点吸附，激活一次并写入 v2。";
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

        // Feed the same SealLab observations as the player loop. Nothing is injected into the menu.
        sealed class PuzzleHand : IDisposable
        {
            readonly LinearRpgController player;
            readonly OpeningLeafPuzzle puzzle;
            readonly WristMenuView originalView;
            readonly NarutoVR.SealLab.SealLab tracking;
            readonly NarutoVR.SealLab.SealFrame originalFrame;
            readonly NarutoVR.SealLab.SealFrame frame = new();
            readonly Transform originalOrigin;
            static readonly PropertyInfo Frame = typeof(NarutoVR.SealLab.SealLab).GetProperty("Frame");

            public PuzzleHand(LinearRpgController player, OpeningLeafPuzzle puzzle)
            {
                this.player = player;
                this.puzzle = puzzle;
                tracking = player.handTracking;
                originalOrigin = tracking.recordingOrigin;
                originalView = player.wristMenu.view;
                originalFrame = tracking.Frame;
                frame.source = originalFrame?.source;
                tracking.recordingOrigin = null;
                player.wristMenu.view = null;
            }

            public void Move(Vector3 tip, bool pressed)
            {
                var hand = frame.right;
                void Joint(Oculus.Interaction.Input.HandJointId joint, Vector3 position)
                {
                    hand.jointValid[(int)joint] = true;
                    hand.joints[(int)joint] = new Pose(position, Quaternion.identity);
                }
                hand.connected = hand.tracked = hand.fresh = true;
                frame.time = hand.observedAt = Time.realtimeSinceStartupAsDouble;
                Joint(Oculus.Interaction.Input.HandJointId.HandWristRoot, tip - .10f * Vector3.forward);
                Joint(Oculus.Interaction.Input.HandJointId.HandIndexTip, tip);
                Joint(Oculus.Interaction.Input.HandJointId.HandThumbTip, tip + Vector3.right * (pressed ? .018f : .07f));
                Frame.SetValue(tracking, frame);
                player.Input.SampleHands(Time.realtimeSinceStartupAsDouble);
                puzzle.SendMessage("LateUpdate", SendMessageOptions.RequireReceiver);
            }

            public void Dispose()
            {
                tracking.recordingOrigin = originalOrigin;
                Frame.SetValue(tracking, originalFrame);
                player.wristMenu.view = originalView;
            }
        }

        static void CheckInsertion(LinearRpgController player, OpeningTrialChapter chapter, bool fullyInserted)
        {
            var puzzle = chapter.puzzle;
            using var hand = new PuzzleHand(player, puzzle);
            int reached = 0, completed = 0;
            void Point(GameObject obj, int index) { if (obj == puzzle.leaf) reached++; }
            void Done(GameObject obj) { if (obj == puzzle.leaf) completed++; }
            const BindingFlags member = BindingFlags.Instance | BindingFlags.NonPublic;
            var hidden = new List<GameObject>();
            puzzle.slot.OnPointReached += Point;
            puzzle.slot.OnSnapCompleted += Done;
            try
            {
                for (var parent = puzzle.slot.transform; parent; parent = parent.parent)
                    if (!parent.gameObject.activeSelf) { hidden.Add(parent.gameObject); parent.gameObject.SetActive(true); }
                typeof(OpeningTrialChapter).GetMethod("Update", member).Invoke(chapter, null);
                if (!puzzle.slot.isActiveAndEnabled) throw new InvalidOperationException("LF02 未接入吸附区。");
                var wristTime = Time.realtimeSinceStartupAsDouble;
                var wrist = new PlayerHandObservation {
                    leftValid = true, mountValid = true, leftObserved = wristTime,
                    leftWrist = puzzle.slot.transform.position, mountRotation = Quaternion.identity
                };
                if (fullyInserted) player.wristMenu.PrepareWristTap(wrist, true, wristTime);
                hand.Move(puzzle.leaf.transform.position, false);
                hand.Move(puzzle.leaf.transform.position, true);
                if (!player.Input.InteractionOwnsHand) throw new InvalidOperationException("缺少腕匣视图时真实手部观测未能捏起铜叶");
                if (fullyInserted)
                {
                    var outside = puzzle.slot.transform.position + puzzle.slotEntry.right * (puzzle.slotRadius + .02f);
                    hand.Move(outside, true);
                    hand.Move(outside, false);
                    if (puzzle.slot.SnappedObject || player.Input.InteractionOwnsHand)
                        throw new InvalidOperationException("导轨侧面容差外松手应放下铜叶。");
                    hand.Move(outside, true);
                    if (!player.Input.InteractionOwnsHand) throw new InvalidOperationException("槽外放下后无法重新捏起铜叶。");
                }
                var releaseAt = fullyInserted ? puzzle.slot.transform.position : puzzle.slotEntry.position;
                hand.Move(releaseAt, true);
                if (puzzle.slot.SnappedObject) throw new InvalidOperationException("铜叶持有中提前吸附。");
                hand.Move(releaseAt + puzzle.slotEntry.right * .08f, false);
                if (player.Input.InteractionOwnsHand) throw new InvalidOperationException("松手后机关仍占用输入");
                if (puzzle.slot.SnappedObject != puzzle.leaf)
                    throw new InvalidOperationException($"在{(fullyInserted ? "槽底" : "槽口")}自然松手未入匣：" + puzzle.slot.LastFailure);
                if (fullyInserted)
                {
                    double recoveryTime = wristTime + player.wristMenu.settings.maxAge * .5;
                    var occluded = new PlayerHandObservation {
                        rightValid = true, rightObserved = recoveryTime, leftObserved = recoveryTime,
                        rightTip = player.wristMenu.wornDevice.RestSurface, headPosition = wrist.leftWrist
                    };
                    if (player.wristMenu.PrepareWristTap(occluded, false, recoveryTime) || player.wristMenu.wornDevice.IsPressed ||
                        puzzle.slot.SnappedObject != puzzle.leaf || !puzzle.slot.IsSnapping)
                        throw new InvalidOperationException("松手后左腕短暂失去追踪，中断了已开始的入匣。");
                    wrist.leftObserved = recoveryTime;
                    player.wristMenu.PrepareWristTap(wrist, true, recoveryTime);
                }
                // Advance the production interpolation using one long frame, with all normal point callbacks.
                puzzle.slot.GetType().GetMethod("Advance", member).Invoke(puzzle.slot, new object[] { 10f });
                if (!puzzle.IsSeated || reached != puzzle.slot.PointCount || completed != 1 ||
                    !player.CaptureProgressSnapshot().items.Single(item => item.itemId == puzzle.leafId).activated)
                    throw new InvalidOperationException($"真实吸附或存档失败，路点 {reached}，完成回报 {completed}。");
                if (fullyInserted)
                {
                    player.wristMenu.PrepareWristTap(default, false, wrist.leftObserved + player.wristMenu.settings.maxAge + .01);
                    if (puzzle.slot.gameObject.activeInHierarchy) throw new InvalidOperationException("左腕持续失去追踪后腕匣仍在旧位置。");
                }
            }
            finally
            {
                puzzle.slot.OnPointReached -= Point;
                puzzle.slot.OnSnapCompleted -= Done;
                foreach (var item in hidden) item.SetActive(false);
            }
        }
    }
}
