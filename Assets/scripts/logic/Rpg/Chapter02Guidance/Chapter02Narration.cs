using System;
using System.Collections.Generic;
using NarutoVR.SealCombat.Enemies;
using UnityEngine;
using UnityEngine.UI;

namespace NarutoVR.SealCombat.Rpg
{
    public interface IHearthRationNarrationProgress
    {
        bool IsNearHearth { get; }
        bool HearthBagOffered { get; }
        bool HearthBagPickedUp { get; }
        bool HearthBagStowed { get; }
    }

    [DisallowMultipleComponent, DefaultExecutionOrder(400)]
    public sealed class Chapter02Narration : MonoBehaviour
    {
        [SerializeField] Chapter02GoldTrail route;
        [SerializeField] PassageApproachHint northBank;
        [SerializeField, Tooltip("可选。接入实现 IHearthRationNarrationProgress 的炉台干粮供给组件。")]
        MonoBehaviour hearthProgress;
        [SerializeField] NarrationLine[] script;

        readonly HashSet<string> spoken = new();
        NarrationController dialogue;
        AudioSource voice;
        OpeningSubtitlePresenter captions;
        GlyphTemplate branchTemplate;
        NarrationLine current;
        string hint;
        float earliestLine, hintAt;
        bool glyphMatched, northBankSeen, branchMatched, basketAStarted, basketAArrived, basketBStarted;
        bool basketBArrived, leverPulled, tenonPicked;

        LinearRpgController Player => route.player;
        bool At(RpgStageDefinition stage) => !Player.CampaignComplete && Player.StageAt(Player.StageIndex) == stage;
        bool AtBridge => At(route.bridge.drawingStage);
        bool AtAltar => At(route.bridge.originalFirstStage) && !route.baskets.AtMiddle && !basketAStarted;
        bool AtWorkshop => At(route.workshop.stage);
        bool AtDescent => !Player.CampaignComplete && Player.StageAt(Player.StageIndex).stageId == Chapter02BasketBControl.StageId;
        bool WorkshopEntered => route.workshop.roof.HasStarted || route.workshop.courtyard.HasStarted;
        IHearthRationNarrationProgress Ration => hearthProgress ? hearthProgress as IHearthRationNarrationProgress : null;
        bool RationPicked => Ration?.HearthBagPickedUp == true;
        bool RationStowed => Ration?.HearthBagStowed == true;
        bool RationLesson => AtWorkshop && Ration?.HearthBagOffered == true;
        bool NearHearth => Ration?.IsNearHearth == true;
        bool BridgeJoined => route.bridge.drawingObjective.IsComplete ||
            Player.Model.Chapter.IsComplete(route.bridge.drawingStage.stageId);

        void OnEnable()
        {
            if (!route || !route.player || !route.bridge || !route.baskets || !route.altar || !route.lever || !route.workshop ||
                script == null || script.Length == 0)
            {
                Debug.LogError("第二幕旁白缺少正式关卡引用或台本。", this);
                enabled = false;
                return;
            }

            if (!dialogue) CreatePlayerVoice();
            Player.CheckpointRestored += ResetPlayback;
            route.bridge.panel.onCompleted.AddListener(OnBridgeGlyph);
            route.baskets.BArrived += OnBasketBArrived;
            var pen = Player.GetComponent<GlyphPenController>();
            if (pen && pen.activeTemplates != null)
                foreach (var template in pen.activeTemplates)
                    if (template && template.effect is PassageGlyphEffect)
                    {
                        branchTemplate = template;
                        branchTemplate.onCompleted.AddListener(OnBranchGlyph);
                        break;
                    }
            ResetPlayback();
        }

        void OnDisable()
        {
            if (!dialogue) return;
            Player.CheckpointRestored -= ResetPlayback;
            route.bridge.panel.onCompleted.RemoveListener(OnBridgeGlyph);
            route.baskets.BArrived -= OnBasketBArrived;
            if (branchTemplate) branchTemplate.onCompleted.RemoveListener(OnBranchGlyph);
            branchTemplate = null;
            ResetPlayback();
        }

        void CreatePlayerVoice()
        {
            var audio = new GameObject("申屠传音", typeof(NarrationController));
            audio.transform.SetParent(transform, false);
            dialogue = audio.GetComponent<NarrationController>();
            voice = audio.GetComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.spatialBlend = 0;
            voice.priority = 48;
            dialogue.StepStarted += OnStepStarted;
            dialogue.StepEnded += _ => captions?.Clear();
        }

        void CreateCaptions()
        {
            var font = Resources.Load<Font>("Fonts/NotoSansCJKsc-Regular");
            if (!font) return;
            var canvas = new GameObject("第二幕字幕", typeof(RectTransform), typeof(Canvas)).GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.transform.SetParent(Player.locomotion.head, false);
            canvas.transform.localScale = Vector3.one * .0012f;
            canvas.GetComponent<RectTransform>().sizeDelta = new Vector2(640, 60);
            canvas.sortingOrder = 8;
            var label = new GameObject("申屠字幕", typeof(RectTransform), typeof(Text), typeof(Shadow)).GetComponent<Text>();
            label.transform.SetParent(canvas.transform, false);
            label.font = font;
            label.color = new Color(1f, .94f, .80f);
            label.GetComponent<Shadow>().effectColor = new Color(0, 0, 0, .95f);
            captions = new OpeningSubtitlePresenter(label, Player.locomotion.head);
        }

        void OnBridgeGlyph() => glyphMatched = true;
        void OnBasketBArrived() => basketBArrived = true;

        void OnBranchGlyph(GlyphBoard board)
        {
            if (!AtAltar || !northBankSeen || board != Player.GlyphPen.activeBoard || !northBank) return;
            var feet = Player.locomotion.PlayerFloorPosition;
            foreach (var anchor in northBank.anchors)
                if (anchor && (anchor.landing.position - feet).sqrMagnitude <= northBank.approachDistance * northBank.approachDistance)
                {
                    branchMatched = true;
                    break;
                }
        }

        void ResetPlayback()
        {
            dialogue.StopDialogue();
            spoken.Clear();
            current = null;
            hint = null;
            earliestLine = hintAt = Time.time;
            glyphMatched = northBankSeen = branchMatched = false;
            basketAStarted = basketAArrived = basketBStarted = basketBArrived = leverPulled = false;
            tenonPicked = false;
        }

        void ReadProgress()
        {
            var baskets = route.baskets;
            bool moving = baskets.IsMoving;
            basketAStarted |= moving && At(route.bridge.originalFirstStage);
            basketAArrived |= basketAStarted && baskets.AtMiddle;
            basketBStarted |= moving && AtDescent;
            leverPulled |= route.lever.DepartureArmed || basketBStarted;
            tenonPicked |= AtWorkshop && route.workshop.tenon.IsHeld;
            northBankSeen |= AtAltar && northBank && northBank.HasReadableAnchor;
        }

        void LateUpdate()
        {
            if (!Player.IsReady || !dialogue) return;
            if (captions == null && Player.locomotion && Player.locomotion.head) CreateCaptions();
            captions?.Tick(!Player.CanAct);
            if (!Player.CanAct) return;
            ReadProgress();
            var nextHint = CurrentHint();
            if (hint != nextHint) { hint = nextHint; hintAt = Time.time + 18f; }
            var main = FindEligible(false);
            var next = main ?? FindEligible(true);
            var step = dialogue.CurrentStep;
            if (step != null)
            {
                if (IsRationLine(step.LineId) && (!RationLesson || main != null))
                {
                    if (RationLesson && !step.AudioDone) spoken.Remove(step.LineId);
                    dialogue.StopDialogue();
                    current = null;
                }
                else if (!step.AudioDone && !IsHint(step.LineId))
                {
                    hintAt = Time.time + 18f;
                    return;
                }
                else if (IsHint(step.LineId) && (hint != step.LineId || next != null))
                {
                    dialogue.StopDialogue();
                    current = null;
                }
                else if (step.AudioDone && step.Line.advanceMode == NarrationAdvanceMode.AudioAndOperation &&
                    (next != null || nextHint != null && Time.time >= hintAt && !spoken.Contains(nextHint)))
                {
                    // The tip can fill an otherwise silent wait. The objective belongs to the level.
                    dialogue.StopDialogue();
                    current = null;
                }
                else return;
            }

            if (current != null)
            {
                earliestLine = Time.time + current.delayAfter;
                current = null;
            }
            if (Time.time < earliestLine) return;
            if (next == null && hint != null && Time.time >= hintAt && !spoken.Contains(hint))
                next = Array.Find(script, line => line.lineId == hint);
            if (next != null) Play(next);
        }

        static bool IsHint(string id) => id.StartsWith("HT", StringComparison.Ordinal);

        static bool IsRationLine(string id) => id is "RT01" or "RT02" or "RT03" or "HT02";

        NarrationLine FindEligible(bool ration)
        {
            foreach (var line in script)
                if (IsRationLine(line.lineId) == ration && !spoken.Contains(line.lineId) && MayStart(line.lineId))
                    return line;
            return null;
        }

        void Play(NarrationLine line)
        {
            spoken.Add(line.lineId);
            current = line;
            dialogue.lines.Clear();
            dialogue.lines.Add(line);
            dialogue.StartDialogue();
        }

        void OnStepStarted(NarrationController.Step step)
        {
            captions?.Begin(voice, step.Line.speaker, step.Line.text);
            if (step.Line.advanceMode == NarrationAdvanceMode.AudioAndOperation)
                dialogue.BindOperationState(step, () => OperationReached(step.LineId));
        }

        bool OperationReached(string id) => id switch
        {
            "EN03" => !At(route.bridge.approachStage),
            "BR01" => glyphMatched || !AtBridge,
            "BR02" => BridgeJoined || !AtBridge,
            "RT01" => RationPicked,
            "RT03" => RationStowed,
            "AB01" => route.baskets.PlayerInA || !AtAltar,
            "AB02" => basketAStarted || !AtAltar ||
                (route.baskets.ballast.Stone && route.baskets.ballast.Stone.State == BallastStone.Phase.Docked),
            "WS03" => tenonPicked || route.workshop.IsSeated || !AtWorkshop,
            "WS04" => route.workshop.IsSeated || !AtWorkshop,
            "BDN02" => leverPulled || !AtDescent,
            "BDN03" => basketBStarted || route.baskets.PlayerInB || !AtDescent,
            _ => true
        };

        bool MayStart(string id) => id switch
        {
            "EN01" or "EN02" or "EN03" => At(route.bridge.approachStage),
            "BR01" => AtBridge && !glyphMatched && !BridgeJoined,
            "BR02" => AtBridge && glyphMatched && !BridgeJoined,
            "AL01" => AtAltar && BridgeJoined && !route.altar.HasStarted,
            "AL02" => AtAltar && BridgeJoined && !route.altar.IsComplete,
            "RT01" => RationLesson && NearHearth && !RationPicked,
            "RT02" or "RT03" => RationLesson && RationPicked && !RationStowed,
            "AB01" => AtAltar && route.altar.IsComplete && !route.baskets.PlayerInA,
            "AB02" => AtAltar && route.altar.IsComplete && route.baskets.PlayerInA,
            "AD01" => basketAStarted && route.baskets.IsMoving && !basketAArrived && !basketBStarted,
            "AD02" => basketAArrived && route.baskets.AtMiddle && !AtWorkshop && !AtDescent,
            "HB01" or "HB02" => AtWorkshop && !WorkshopEntered && !route.workshop.IsSeated,
            "WS01" or "WS02" => AtWorkshop && WorkshopEntered && !route.workshop.IsUnlocked,
            "WS03" => AtWorkshop && route.workshop.IsUnlocked && !tenonPicked,
            "WS04" => AtWorkshop && route.workshop.IsUnlocked && tenonPicked,
            "BDN01" or "BDN02" => AtDescent && route.workshop.IsSeated && !leverPulled,
            "BDN03" => AtDescent && leverPulled && !basketBStarted && !route.baskets.PlayerInB,
            "BDN04" => basketBStarted && route.baskets.IsMoving,
            "BDN05" => basketBArrived && route.baskets.AtBottom,
            "PG01" => AtAltar && northBankSeen && northBank.HasReadableAnchor && !branchMatched,
            "PG02" => AtAltar && branchMatched,
            _ => false
        };

        string CurrentHint()
        {
            if (AtBridge && !BridgeJoined) return "HT01";
            if (AtAltar && route.altar.IsComplete) return "HT03";
            if (AtWorkshop && WorkshopEntered && !route.workshop.IsUnlocked && !spoken.Contains("HT04")) return "HT04";
            if (RationLesson && (NearHearth || RationPicked) && !RationStowed) return "HT02";
            if (AtDescent && !leverPulled) return "HT05";
            return null;
        }
    }
}
