using System;
using System.Collections.Generic;
using System.Linq;
using NarutoVR.SealCombat;
using NarutoVR.SealLab;
using Oculus.Interaction.Input;
using UnityEditor;
using UnityEngine;

namespace NarutoVR.Editor
{
    public static class PicoOcclusionChecks
    {
        const double Stale = .12;
        static int passed;

        public static void Run()
        {
            passed = 0;
            var lv = LoadSeal("律印");
            var gui = LoadSeal("规印");
            var heng = LoadSeal("衡印");
            var dragon = AssetDatabase.LoadAssetAtPath<SkillDefinition>("Assets/ScriptableObjects/技能定义配置/龙火之术.asset");
            Check(dragon && dragon.sealName == lv.sealName && dragon.castCondition == SkillCastCondition.SealNearMouth,
                "production dragon uses Lv and the mouth condition");
            Check(new[] {lv, gui, heng}.All(p => p.pico != null && p.pico.minimumCoverage > 0),
                "three production profiles opt into PICO");

            var lvFull = Midpoints(lv);
            var guiFull = Midpoints(gui);
            var lvOccluded = new List<SealFeature>(lvFull);
            lvOccluded.RemoveAll(f => (f.id.StartsWith("R.Index.", StringComparison.Ordinal) && f.id != "R.Index.ProximalBend")
                || f.id == "Both.IndexTipDistance" || f.id == "Both.IndexDirectionAngle");
            Check(new[] {"L.Index.ProximalBend", "R.Index.ProximalBend", "Both.IndexProximalDirectionAngle"}
                .All(id => lvOccluded.Any(f => f.id == id)), "Lv has three enabled proximal measurements");
            Check(Score(lv, lvFull).passes && Score(gui, guiFull).passes, "full measurements accept each own sign");
            var lvScore = Score(lv, lvOccluded);
            Check(lvScore.passes && lvScore.coverage < .999f, "Lv single missing fingertip passes on measured proximal evidence");
            Check(!SealScorer.Evaluate(lv, lvOccluded, false).passes, "non-PICO still applies full original coverage");
            Check(!Score(gui, lvFull).passes && !Score(lv, guiFull).passes, "same measurements distinguish straight Lv from bent Gui");
            var guiStraight = guiFull.Select(f => f.id == "R.Index.Curl"
                ? lvFull.Single(v => v.id == f.id) : f).ToList();
            Check(!Score(gui, guiStraight).passes, "Gui rejects a straight right index even when its wrists match");
            var lvBent = lvFull.Select(f => f.id == "R.Index.Curl"
                ? guiFull.Single(v => v.id == f.id) : f).ToList();
            Check(!Score(lv, lvBent).passes,
                "a visible contradictory curl cannot be overridden by the proximal fallback");
            Hysteresis(lv, lvFull);

            var lvLow = lvFull.Select(f => f.id == "Both.PalmAngle"
                ? new SealFeature(f.id, f.unit, f.value, true, false) : f).ToList();
            Check(Score(lv, lvLow).passes, "one low-confidence nonessential measurement does not veto Lv");
            var hengOccluded = Midpoints(heng);
            hengOccluded.RemoveAll(f => f.id.StartsWith("L.Max.", StringComparison.Ordinal)
                || f.id.StartsWith("L.Pinky.", StringComparison.Ordinal) || f.id == "L.Ring.Abduction");
            Check(Score(heng, hengOccluded).passes, "Heng accepts unavailable pinky and adjacent abduction");
            Check(!Score(lv, lvFull.Where(f => f.id.StartsWith("L.", StringComparison.Ordinal)).ToList()).passes,
                "left features alone cannot supply a two-hand seal");

            ExtractMissingTip();
            RecognizerContinuity(lv, lvOccluded, heng, hengOccluded);
            MatchDragon(dragon, lv, lvOccluded);
            Debug.Log($"PICO_OCCLUSION_PASS {passed} synthetic checks; no device samples or XR initialization");
        }

        static SealProfile LoadSeal(string name)
        {
            var p = AssetDatabase.LoadAssetAtPath<SealProfile>($"Assets/ScriptableObjects/手印识别配置/{name}.asset");
            Check(p && p.calibrated && p.sealName == name, $"loaded calibrated {name}");
            return p;
        }

        // Build one input vector per physical sign. Competing profiles always see the same vector.
        static List<SealFeature> Midpoints(SealProfile p) => p.rules.Concat(p.pico.additionalRules.Select(alternative => alternative.rule).Where(rule => rule.weight > 0))
            .Select(rule => new SealFeature(rule.featureId, "", (rule.min + rule.max) * .5f, true, true)).ToList();

        static SealScore Score(SealProfile p, List<SealFeature> f) => SealScorer.Evaluate(p, f, true);

        static HandSnapshot Hand(bool left, double t, int version)
        {
            var h = new HandSnapshot { connected = true, tracked = true, fresh = true,
                observedAt = t, version = version, highConfidence = false, scale = 1 };
            var root = new Vector3(left ? -.045f : .045f, 0, 0);
            Set(HandJointId.HandWristRoot, Vector3.zero);
            Set(HandJointId.HandIndex1, new Vector3(0, .06f, 0));
            Set(HandJointId.HandIndex2, new Vector3(0, .09f, 0));
            Set(HandJointId.HandIndex3, new Vector3(0, .11f, 0));
            Set(HandJointId.HandPinky1, new Vector3(left ? -.035f : .035f, .05f, 0));
            if (left) Set(HandJointId.HandIndexTip, new Vector3(0, .13f, 0));
            return h;

            void Set(HandJointId id, Vector3 offset)
            {
                h.jointValid[(int)id] = true;
                h.joints[(int)id] = new Pose(root + offset, Quaternion.identity);
            }
        }

        static SealFrame Frame(double observed, int version) => new SealFrame
        {
            time = observed, left = Hand(true, observed, version), right = Hand(false, observed, version)
        };

        static void ExtractMissingTip()
        {
            var raw = Frame(1, 1);
            var features = new List<SealFeature>();
            var extractor = new SealFeatureExtractor();
            extractor.Extract(raw, features, true);
            foreach (var id in new[] {"L.Index.ProximalBend", "R.Index.ProximalBend", "Both.IndexProximalDirectionAngle"})
                Check(features.Exists(f => f.id == id && f.valid && f.highConfidence), $"extract actual measured {id} without right tip");
            Check(!features.Exists(f => f.id == "Both.IndexTipDistance" && f.valid), "no inferred missing tip");
            raw.right.observedAt -= .09;
            extractor.Extract(raw, features, true);
            Check(!features.Exists(f => f.id == "Both.IndexProximalDirectionAngle" && f.valid), "unsynchronized proximal pair rejected");
        }

        static void Hysteresis(SealProfile lv, List<SealFeature> strong)
        {
            var weak = strong.Select(f => f.id == "L.Middle.Curl" || f.id == "R.Middle.Curl" || f.id == "Both.PalmAngle"
                ? new SealFeature(f.id, f.unit, f.value + 100, true, true) : f).ToList();
            var score = Score(lv, weak);
            Check(!score.passes && score.score >= lv.threshold - lv.pico.releaseMargin,
                "test pose falls between enter and sustain thresholds");
            var recognizer = new SealRecognizer();
            for (int i = 0; i <= 5; i++) Observe(1 + i * .02, i + 1, weak);
            Check(!recognizer.Recognized, "sustain-only score cannot start a new seal");
            Observe(2, 8, strong);
            for (int i = 1; i <= 5; i++) Observe(2 + i * .02, 8 + i, weak);
            Check(recognizer.Recognized == lv, "sustain-only score continues a seal started with strong evidence");

            void Observe(double at, int version, List<SealFeature> features)
            {
                var frame = Frame(at, version);
                recognizer.Update(new[] { lv }, features, at, true, .05f, frame.left, Stale, true, frame);
            }
        }

        static void RecognizerContinuity(SealProfile lv, List<SealFeature> lvFeatures,
            SealProfile heng, List<SealFeature> hengFeatures)
        {
            var recognizer = new SealRecognizer();
            var profiles = new[] {lv};
            var start = Frame(1, 1);
            Feed(start, 1);
            var next = Frame(1.03, 2);
            Feed(next, 1.03);
            float progress = recognizer.HoldProgress;
            Check(progress > 0 && progress < 1, "partial pair hold started");
            Feed(next, 1.11);
            Check(recognizer.Recognized == null && recognizer.HoldProgress <= progress + .001f,
                "repeated XR sample never completes or advances hold");
            Feed(Frame(1.35, 3), 1.35);
            Check(!recognizer.Recognized && recognizer.HoldProgress < .01f, "fresh frame after a gap starts a new hold");
            for (int i = 1; i <= 6; i++) Feed(Frame(1.35 + i*.02, 3+i), 1.35 + i*.02);
            Check(recognizer.Recognized == lv, "continuous new pairs complete Lv after reacquisition");

            recognizer.Reset();
            for (int i = 0; i <= 8; i++)
            {
                var frame = Frame(2 + i*.02, i + 1);
                frame.right.tracked = false;
                recognizer.Update(profiles, lvFeatures, frame.time, false, .04f, frame.left, Stale, true, frame);
            }
            Check(!recognizer.Recognized, "whole right hand missing cannot initiate Lv even with prior features");
            recognizer.Reset();
            for (int i = 0; i <= 20; i++)
            {
                var frame = Frame(3 + i*.02, i + 1);
                frame.right.tracked = false;
                recognizer.Update(new[] {heng}, hengFeatures, frame.time, false, .04f, frame.left, Stale, true, frame);
            }
            Check(recognizer.Recognized == heng, "Heng uses fresh left-hand evidence with a missing pinky and no right hand");
            void Feed(SealFrame f, double now) => recognizer.Update(profiles, lvFeatures, now, true, .04f, f.left, Stale, true, f);
        }

        static void MatchDragon(SkillDefinition dragon, SealProfile lv, List<SealFeature> occluded)
        {
            var engine = new SkillMatchEngine(new[] {dragon});
            int casts = 0, confirmations = 0;
            engine.IsCastConditionMet = skill => skill == dragon;
            engine.TryActivate = skill => { if (skill == dragon) casts++; return true; };
            engine.SealConfirmed += (seal, _) => { if (seal == lv.sealName) confirmations++; };
            var scores = new[] {Score(lv, occluded)};
            engine.Tick(1);
            engine.Voice(dragon.phrase, 1.01, 1.01);
            for (int i = 0; i <= 10; i++)
            {
                var f = Frame(1.02 + i*.02, i+1);
                f.right.tracked = false;
                Sample(f);
            }
            Check(casts == 0 && confirmations == 0, "armed dragon cannot recognize or cast without a full right hand");
            // Left keeps updating, right only reports its original sample. Render time must not fill the hold.
            for (int i = 0; i <= 4; i++)
            {
                var f = Frame(1.25 + i*.02, 12+i);
                f.right.version = 12;
                f.right.observedAt = 1.25;
                Sample(f);
            }
            Check(casts == 0 && confirmations == 0, "repeated right-hand sample cannot advance the dragon hold");
            Sample(Frame(1.59, 30));
            Check(casts == 0 && confirmations == 0, "first complete pair after a gap cannot complete the preceding partial hold");
            for (int i = 1; i <= 12; i++) Sample(Frame(1.59 + i*.02, 30+i));
            Check(casts == 1 && confirmations == 1, "real dragon recipe confirms and activates once after recovery: " + engine.Status);
            for (int i = 0; i < 3; i++)
            {
                var gap = Frame(1.86 + i*.02, 43+i);
                gap.right.tracked = false;
                Sample(gap);
            }
            for (int i = 0; i <= 20; i++) Sample(Frame(1.93 + i*.02, 46+i));
            Check(casts == 1, "recovering the held pose after firing cannot fire the consumed voice again");
            void Sample(SealFrame f) { engine.Tick(f.time); engine.Observe(f, scores); }
        }

        static void Check(bool ok, string test)
        {
            if (!ok) throw new InvalidOperationException("PICO_OCCLUSION_FAIL " + test);
            passed++;
            Debug.Log("PICO_OCCLUSION_OK " + test);
        }
    }
}
