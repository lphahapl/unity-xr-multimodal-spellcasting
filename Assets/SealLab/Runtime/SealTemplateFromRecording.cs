using System;
using System.Collections.Generic;
using UnityEngine;

namespace NarutoVR.SealLab
{
    public sealed class RecordingTemplateDraft
    {
        public int matchingFrames, acceptedFrames;
        public string error;
        public readonly List<FeatureRule> rules = new();
    }

    public static class SealTemplateFromRecording
    {
        public static RecordingTemplateDraft Build(IReadOnlyList<SealFrame> frames, string label)
        {
            var result = new RecordingTemplateDraft();
            if (label == "none" || label == "near_miss" || label == "transition")
            { result.error = "负样本标签不能用来生成印模板"; return result; }
            var extractor = new SealFeatureExtractor(); var features = new List<SealFeature>();
            var samples = new Dictionary<string, List<float>>(); var units = new Dictionary<string, string>();
            foreach (var frame in frames)
            {
                if (frame.label != label) continue;
                result.matchingFrames++;
                if (!Usable(frame.left) || !Usable(frame.right) ||
                    Math.Abs(frame.left.observedAt - frame.right.observedAt) > SealFeatureExtractor.MaximumHandSkew) continue;
                result.acceptedFrames++;
                extractor.Extract(frame, features);
                foreach (var feature in features)
                {
                    if (!feature.valid || !feature.highConfidence) continue;
                    if (!samples.TryGetValue(feature.id, out var values))
                    { values = new List<float>(); samples.Add(feature.id, values); units.Add(feature.id, feature.unit); }
                    values.Add(feature.value);
                }
            }
            if (result.acceptedFrames < 10)
            { result.error = "至少需要10帧双手有效且高置信的正样本；当前 " + result.acceptedFrames; return result; }
            foreach (var sample in samples)
            {
                if (sample.Value.Count < result.acceptedFrames * 0.8f) continue;
                sample.Value.Sort();
                result.rules.Add(new FeatureRule { featureId = sample.Key,
                    min = Percentile(sample.Value, 0.1f), max = Percentile(sample.Value, 0.9f),
                    tolerance = units[sample.Key].StartsWith("m") ? 0.015f : 15, weight = 1 });
            }
            if (result.rules.Count == 0) result.error = "没有持续可用的高置信特征";
            return result;
        }
        private static bool Usable(HandSnapshot hand) => hand != null && hand.connected && hand.tracked && hand.fresh && hand.highConfidence;
        private static float Percentile(List<float> sorted, float q)
        {
            float index = (sorted.Count - 1) * q; int low = Mathf.FloorToInt(index), high = Mathf.CeilToInt(index);
            return Mathf.Lerp(sorted[low], sorted[high], index - low);
        }
    }
}
