using System;
using NeuGameMRTools.Runtime.Components;
using UnityEngine;
using UnityEngine.UI;

namespace NarutoVR.SealCombat.Rpg
{
    public enum OpeningAction { Listen, OpenWrist, HitTarget, Retreat, ClearRoom, ReachArea, InsertLeaf, CollectLeaf, RecoverMana, AbsorbBolt, VerifyWrist, StopAtMarker }

    [Serializable]
    public sealed class OpeningLesson
    {
        public string id;
        [TextArea] public string subtitle;
        public OpeningAction action;
        public BoxCollider area;
        public SkillTrainingTarget target;
    }

    [Serializable]
    public sealed class OpeningHint
    {
        public string audioFile;
        [Tooltip("留空时沿用本段当前台词的说话者。")] public string speaker;
        [TextArea] public string subtitle;
    }

    [Serializable]
    public sealed class OpeningSection
    {
        public RpgStageDefinition stage;
        public NarrationController narration;
        public OpeningLesson[] lines;
        [Tooltip("此段可用的练习术，不写入角色存档。")] public string[] trialSkills = Array.Empty<string>();
        public GameObject doorOpenedOnCompletion;
        public BoxCollider entryArea;
        public bool alignDiscs;
        public OpeningHint[] hints = Array.Empty<OpeningHint>();
        public GameObject outdoorGoal;
        public bool sidePatrolVisible;
    }

    [DefaultExecutionOrder(320), DisallowMultipleComponent]
    public sealed class OpeningTrialChapter : MonoBehaviour
    {
        public LinearRpgController rpg;
        [InspectorName("首章探索步速（米／秒）"), Min(.05f)] public float explorationWalkSpeed = 1.25f;
        public CombatRoomEncounter encounter;
        public OpeningSection[] sections;
        public Text subtitle;
        public OpeningInteriorGuidance interiorGuidance;
        public OpeningLeafPuzzle puzzle;
        [InspectorName("铜叶成长与教学")] public OpeningTechniqueProgression techniques;
        public SnapZone wristSlot;
        public OpeningDefenseDrill defense;
        public GameObject secondTarget;
        public RpgStageDefinition secondTargetStage;
        public NavigationNode finalCheckpoint;
        public AudioSource promptVoice;
        public GameObject sidePatrol;
        [Range(.1f,1)] public float recoveredManaRatio = .95f;

        OpeningSection section;
        OpeningLesson line;
        OpeningSubtitlePresenter captions;
        bool dialogueFinished, started, puzzleAligned, restoreWorld = true;
        float stopped;
        int hintIndex;
        float waiting;
        public bool Speaking => (promptVoice && promptVoice.isPlaying) ||
            (section?.narration && section.narration.CurrentStep != null && !section.narration.AudioCompleted);
        int index = -1;
        internal bool ObjectiveMet => section != null && index == rpg.StageIndex && dialogueFinished && (!section.alignDiscs || puzzleAligned);
        public string ObjectiveText => rpg.CampaignComplete ? "已抵达测候台，首章完成" :
            line != null ? LineText(section?.narration?.CurrentLine, line) : section?.stage.instructions ?? "";
        public bool AllowsTrialSkill(string id) => section?.trialSkills != null && Array.IndexOf(section.trialSkills, id) >= 0;

        void Awake()
        {
            if (rpg && rpg.locomotion)
                rpg.locomotion.exploreSpeed = Mathf.Max(.05f, explorationWalkSpeed);
            if (subtitle && rpg && rpg.locomotion && rpg.locomotion.head)
                captions = new OpeningSubtitlePresenter(subtitle, rpg.locomotion.head);
        }
        void OnEnable()
        {
            if (rpg) rpg.CheckpointRestored += Restore;
            if (captions != null) Application.onBeforeRender += captions.Place;
        }
        void OnDisable()
        {
            if (captions != null) Application.onBeforeRender -= captions.Place;
            if (rpg) rpg.CheckpointRestored -= Restore;
            LeaveSection();
            index = -1;
            restoreWorld = true;
        }

        void LateUpdate()
        {
            captions?.Place();
            captions?.Tick(!rpg || !rpg.CanAct || Time.timeScale <= 0);
        }

        void OnApplicationPause(bool paused)
        {
            if (paused) captions?.Tick(true);
        }

        void Update()
        {
            if (!rpg || !rpg.CanAct) return;
            int expected = rpg.CampaignComplete ? sections.Length : rpg.StageIndex;
            if (index != expected) EnterSection(expected);
            if (section != null && !started && (!section.entryArea || ContainsFloor(section.entryArea)))
            {
                started = true;
                if (section.alignDiscs) { puzzle.Solved += Aligned; puzzle.EnablePuzzle(); }
                section.narration.StartDialogue();
            }
            if (line != null)
                switch (line.action)
                {
                    case OpeningAction.ReachArea: if (ContainsFloor(line.area)) Accept(); break;
                    case OpeningAction.StopAtMarker:
                        // A held walk into a wall is stationary but does not complete the release lesson.
                        stopped = ContainsFloor(line.area) && !rpg.locomotion.NavigationActive &&
                            !rpg.locomotion.ContinuousMotionActive ? stopped + Time.deltaTime : 0;
                        if (stopped >= .5f) Accept();
                        break;
                    case OpeningAction.RecoverMana: if (rpg.Player.Mana >= recoveredManaRatio * rpg.Player.MaxMana) Accept(); break;
                    case OpeningAction.VerifyWrist: if (puzzle.VerifiesWrist) Accept(); break;
                }
            TickHints();
            if (ObjectiveMet && rpg.CompleteOpeningObjective(this))
                EnterSection(rpg.CampaignComplete ? sections.Length : rpg.StageIndex);
        }

        bool ContainsFloor(BoxCollider area)
        {
            if (!area || !area.enabled) return false;
            Vector3 p = area.transform.InverseTransformPoint(rpg.locomotion.PlayerFloorPosition) - area.center;
            Vector3 h = area.size * .5f;
            return Mathf.Abs(p.x) <= h.x && Mathf.Abs(p.y) <= h.y && Mathf.Abs(p.z) <= h.z;
        }

        void TickHints()
        {
            if (section == null || !started || hintIndex >= section.hints.Length || !promptVoice ||
                rpg.UiInputOwned || Speaking || (!section.alignDiscs && (line == null || line.action == OpeningAction.Listen)) ||
                (section.entryArea && !ContainsFloor(section.entryArea))) return;
            var hand = rpg.wristMenu.LatestInput;
            if (!WristMenuState.Fresh(hand.rightValid, hand.rightObserved, Time.realtimeSinceStartupAsDouble, rpg.wristMenu.settings.maxAge)) return;
            waiting += Time.deltaTime;
            if (waiting < (hintIndex == 0 ? 18 : 25)) return;
            var hint = section.hints[hintIndex++];
            waiting = 0;
            string speaker = string.IsNullOrWhiteSpace(hint.speaker) ? GuidanceLine().speaker : hint.speaker;
            PlayPrompt(hint.audioFile, speaker, hint.subtitle);
        }

        public void ReplayGuidance()
        {
            if (section == null || !started || Speaking) return;
            int current = section.narration.CurrentLineIndex;
            if (current < 0) current = section.lines.Length - 1;
            var row = section.narration.lines[current];
            PlayPrompt(row.audioFileName, row.speaker, LineText(row, section.lines[current]));
        }

        NarrationLine GuidanceLine()
        {
            return section.narration.CurrentLine ?? section.narration.lines[section.lines.Length - 1];
        }

        static string LineText(NarrationLine row, OpeningLesson lesson) =>
            string.IsNullOrWhiteSpace(row?.text) ? lesson?.subtitle ?? string.Empty : row.text;

        void PlayPrompt(string file, string speaker, string caption)
        {
            if (!promptVoice) return;
            var clip = Resources.Load<AudioClip>("Narration/" + System.IO.Path.ChangeExtension(file, null));
            if (!clip) return;
            promptVoice.Stop();
            promptVoice.clip = clip;
            promptVoice.timeSamples = 0;
            promptVoice.Play();
            captions?.Begin(promptVoice, speaker, caption);
        }

        void Restore()
        {
            restoreWorld = true;
            EnterSection(rpg.CampaignComplete ? sections.Length : rpg.StageIndex);
        }

        void EnterSection(int next)
        {
            rpg.RefreshTechniques();
            LeaveSection();
            index = next;
            if (interiorGuidance) interiorGuidance.Show(next < sections.Length ? sections[next] : null, null);
            for (int i = 0; i < sections.Length; i++)
            {
                if (sections[i].doorOpenedOnCompletion) sections[i].doorOpenedOnCompletion.SetActive(i >= index);
                if (sections[i].outdoorGoal) sections[i].outdoorGoal.SetActive(false);
            }
            if (sidePatrol) sidePatrol.SetActive(next < sections.Length && sections[next].sidePatrolVisible);
            if (restoreWorld)
            {
                puzzle.Restore(rpg.Progress.completedStageIds);
                restoreWorld = false;
            }
            if (secondTarget) secondTarget.SetActive(index < sections.Length && sections[index].stage == secondTargetStage);
            if (index == sections.Length)
            {
                return;
            }
            section = sections[index];
            if (section.outdoorGoal) section.outdoorGoal.SetActive(true);
            if (!section.narration || section.stage != rpg.stages[index] || section.lines == null ||
                section.lines.Length != section.narration.lines.Count || section.lines.Length == 0)
            {
                Debug.LogError("首章步骤与旁白接线不一致：" + index, this);
                enabled = false;
                return;
            }
            foreach (var entry in section.narration.lines)
            {
                entry.onStarted.AddListener(BeginLine);
                entry.onAdvanced.AddListener(EndLine);
            }
            section.narration.onDialogueCompleted.AddListener(FinishSection);
        }

        void LeaveSection()
        {
            captions?.Clear();
            EndLine();
            if (promptVoice) promptVoice.Stop();
            if (puzzle) puzzle.Solved -= Aligned;
            if (section?.narration)
            {
                foreach (var entry in section.narration.lines)
                {
                    entry.onStarted.RemoveListener(BeginLine);
                    entry.onAdvanced.RemoveListener(EndLine);
                }
                section.narration.onDialogueCompleted.RemoveListener(FinishSection);
                section.narration.StopDialogue();
            }
            section = null;
            dialogueFinished = false;
            puzzleAligned = started = false;
            hintIndex = 0; waiting = 0;
        }

        void BeginLine()
        {
            EndLine();
            if (promptVoice) promptVoice.Stop();
            line = section.lines[section.narration.CurrentLineIndex];
            if (interiorGuidance) interiorGuidance.Show(section, line);
            var row = section.narration.CurrentLine;
            captions?.Begin(section.narration.GetComponent<AudioSource>(), row.speaker, LineText(row, line));
            switch (line.action)
            {
                case OpeningAction.OpenWrist:
                    rpg.wristMenu.WristTapConfirmed += WristChanged;
                    if (rpg.wristMenu.State.LauncherOpen) WristChanged(true);
                    break;
                case OpeningAction.HitTarget: rpg.skills.effects.ProjectileHitTarget += Hit; break;
                case OpeningAction.StopAtMarker: stopped = 0; break;
                case OpeningAction.AbsorbBolt: defense.BeginShield(Accept); break;
                case OpeningAction.Retreat:
                    rpg.SetEncounterCombat(true);
                    defense.BeginRetreat(Accept);
                    break;
                case OpeningAction.ClearRoom:
                    encounter.Cleared += Accept;
                    encounter.enabled = true;
                    if (encounter.IsComplete) Accept();
                    break;
                case OpeningAction.CollectLeaf:
                    puzzle.Collected += CollectedLeaf;
                    puzzle.EnableReward();
                    if (puzzle.IsEarnedLeaf(puzzle.leaf)) CollectedLeaf();
                    break;
                case OpeningAction.InsertLeaf:
                    wristSlot.OnSnapCompleted += Inserted;
                    wristSlot.enabled = true;
                    puzzle.EnableInsertion();
                    if (puzzle.IsSeated) Inserted(puzzle.leaf);
                    break;
            }
        }

        void EndLine()
        {
            if (line == null) return;
            if (rpg && rpg.wristMenu) rpg.wristMenu.WristTapConfirmed -= WristChanged;
            if (rpg && rpg.skills && rpg.skills.effects) rpg.skills.effects.ProjectileHitTarget -= Hit;
            if (defense && (line.action == OpeningAction.AbsorbBolt || line.action == OpeningAction.Retreat)) defense.StopDrill();
            if (rpg && rpg.locomotion)
            {
                if (line.action == OpeningAction.Retreat) rpg.SetEncounterCombat(false);
            }
            if (encounter && line.action == OpeningAction.ClearRoom)
            {
                encounter.Cleared -= Accept;
                encounter.enabled = false;
            }
            if (puzzle) puzzle.Collected -= CollectedLeaf;
            if (wristSlot) wristSlot.OnSnapCompleted -= Inserted;
            line = null;
        }

        void WristChanged(bool open) { if (open && rpg.wristMenu.OpenTeachingFromLauncher()) Accept(); }
        void Hit(SkillTrainingTarget target) { if (line?.target == target) Accept(); }
        void Aligned() => puzzleAligned = true;
        void CollectedLeaf()
        {
            if (puzzle.IsEarnedLeaf(puzzle.leaf) && rpg.RecordLeaf(puzzle.leafId, false) != LeafRecordResult.Rejected) Accept();
        }
        void Inserted(GameObject item)
        {
            // A valid physical completion stays usable in this play session even while the controller retries its pending save.
            if (!puzzle.IsEarnedLeaf(item) || rpg.RecordLeaf(puzzle.leafId, true) == LeafRecordResult.Rejected) return;
            puzzle.Seat(item);
            Accept();
        }
        void Accept()
        {
            if (line != null && rpg.CanAct) section.narration.CompleteOperation(section.narration.CurrentLineIndex);
        }
        void FinishSection() => dialogueFinished = true;
    }
}
