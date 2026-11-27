using System;
using NarutoVR.SealCombat.Items;
using UnityEngine;

namespace NarutoVR.SealCombat.Rpg
{
    /// <summary>Runs the upper bridge lesson with the chapter's authored belt, pen, board and bridge states.</summary>
    [DefaultExecutionOrder(340), DisallowMultipleComponent]
    public sealed class Chapter02BridgeEquipment : MonoBehaviour, IRpgCheckpointMigration, IRpgStageInteractionContinuity
    {
        public LinearRpgController player;
        public ChapterToolBelt equipment;
        public GlyphPenController glyph;
        public GlyphStoryPanel panel;
        public ZhunwangPressButton button;
        public SignalChapterObjective drawingObjective;
        public RpgStageDefinition approachStage, drawingStage, originalFirstStage;
        public GameObject brokenBarrier, repairedDeck;

        Chapter02PassageCrossing crossing;
        bool checkpointRestored = true;
        bool applied, previouslyUnlocked;

        void OnEnable()
        {
            if (player) player.CheckpointRestored += OnCheckpointRestored;
        }

        void Start()
        {
            if (!player || !equipment || !glyph || !panel || !button || !drawingObjective ||
                !brokenBarrier || !repairedDeck || !approachStage || !drawingStage)
            {
                Debug.LogError("第二章断桥教学缺少工具带、剧情画板或桥面引用。", this);
                enabled = false;
                return;
            }

            if (!(drawingStage is Chapter02PassageStage lesson) || !lesson.passage || !(lesson.passage.effect is PassageGlyphEffect) || !lesson.socketPrefab)
            {
                Debug.LogError("断桥缺少通路符、渡榫或阶段配置。", this);
                enabled = false;
                return;
            }
            crossing = new Chapter02PassageCrossing(this, lesson);
            equipment.Bind(player, glyph);
        }

        void OnDisable()
        {
            if (player) player.CheckpointRestored -= OnCheckpointRestored;
            if (panel) panel.Cancel();
        }

        void OnCheckpointRestored()
        {
            if (panel) panel.Cancel();
            checkpointRestored = true;
        }

        bool InDrawingStage => player && player.IsReady && !player.CampaignComplete &&
            player.StageAt(player.StageIndex) == drawingStage;

        public void OpenPanel()
        {
            if (InDrawingStage && player.CanAct && !player.UiInputOwned && !player.StateCommitInProgress && drawingObjective.IsRunning &&
                !drawingObjective.IsComplete)
                panel.Show();
        }

        public void CompleteDrawing()
        {
            if (!InDrawingStage || player.StateCommitInProgress) return;
            crossing.TeachAim();
        }

        void LateUpdate()
        {
            if (!player.IsReady) return;
            var progress = player.Model.Chapter;
            bool unlocked = progress.IsComplete(approachStage.stageId);
            if (!applied || unlocked != previouslyUnlocked)
            {
                // Chapter two starts with the tool belt already worn. The stone lesson authorizes the pen.
                if (!applied) equipment.SetStorageAvailable(true);
                equipment.SetWritingAvailable(unlocked);
                applied = true;
                previouslyUnlocked = unlocked;
            }

            if (checkpointRestored)
            {
                if (unlocked && !equipment.RestorePenToHolster())
                    Debug.LogError("第二章检查点恢复后，墨规笔无法归入原笔套。", this);
                crossing.Restore(progress.IsComplete(drawingStage.stageId));
                checkpointRestored = false;
            }

            bool draw = InDrawingStage && drawingObjective.IsRunning && !drawingObjective.IsComplete;
            crossing.UpdateStage(draw);
            if (draw && crossing.Joined && !player.StateCommitInProgress)
            {
                drawingObjective.Complete();
                draw = !drawingObjective.IsComplete && InDrawingStage;
            }
            if (button.gameObject.activeSelf != draw) button.gameObject.SetActive(draw);
            if (!draw && panel.IsWaiting) panel.Cancel();
            SetBridge(progress.IsComplete(drawingStage.stageId) || (InDrawingStage && drawingObjective.IsComplete));
        }

        void SetBridge(bool repaired)
        {
            if (repaired)
            {
                if (!repairedDeck.activeSelf) repairedDeck.SetActive(true);
                if (brokenBarrier.activeSelf) brokenBarrier.SetActive(false);
            }
            else
            {
                if (!brokenBarrier.activeSelf) brokenBarrier.SetActive(true);
                if (repairedDeck.activeSelf) repairedDeck.SetActive(false);
            }
        }

        UnityEngine.Object IRpgStageInteractionContinuity.RetainInteractionAfter(RpgStageDefinition completedStage)
        {
            if (!isActiveAndEnabled || completedStage != drawingStage || !drawingObjective ||
                !drawingObjective.IsComplete || !equipment) return null;
            var belt = equipment.interaction;
            return belt && belt.OwnsHeldItem(equipment.pen) ? belt : null;
        }

        // A legacy save past the hub already crossed this route. The added lessons issue no reward.
        void IRpgCheckpointMigration.MigrateCheckpoint(RpgProgressData snapshot)
        {
            Chapter02BasketBControl.MigrateCompletedCheckpoint(player, snapshot);
            if (!approachStage || !drawingStage || !originalFirstStage ||
                approachStage.rewardCurrency != 0 || drawingStage.rewardCurrency != 0 ||
                approachStage.unlockSkillIds.Length != 0 || drawingStage.unlockSkillIds.Length != 0 ||
                player.StageCount < 3 || player.StageAt(0) != approachStage || player.StageAt(1) != drawingStage ||
                player.StageAt(2) != originalFirstStage || snapshot.completedStageIds.Count == 0 ||
                snapshot.completedStageIds[0] != originalFirstStage.stageId ||
                !snapshot.claimedRewardIds.Contains("reward." + originalFirstStage.stageId)) return;

            InsertLesson(drawingStage.stageId);
            InsertLesson(approachStage.stageId);

            void InsertLesson(string id)
            {
                snapshot.completedStageIds.Insert(0, id);
                snapshot.claimedRewardIds.Add("reward." + id);
            }
        }
    }
}
