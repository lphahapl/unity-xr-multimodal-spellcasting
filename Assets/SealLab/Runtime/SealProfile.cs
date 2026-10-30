using System;
using System.Collections.Generic;
using UnityEngine;

namespace NarutoVR.SealLab
{
    [Serializable]
    public sealed class FeatureRule
    {
        [Header("特征与目标范围")]
        [InspectorName("特征标识（固定ID）")]
        [Tooltip("例如 L.Index.Curl；必须与特征提取器输出的ID一致。")]
        public string featureId;
        [InspectorName("目标最小值")]
        [Tooltip("目标区间内得满分；角度单位为度，距离单位以面板标注为准。")]
        public float min;
        [InspectorName("目标最大值")]
        [Tooltip("目标范围的上限，必须大于或等于最小值；与特征的单位一致。")]
        public float max;
        [Header("容差与权重")]
        [InspectorName("区间外软容差"), Min(0.00001f)]
        [Tooltip("超出目标区间后，在该宽度内线性扣分直至0；单位与特征相同。")]
        public float tolerance = 15;
        [InspectorName("评分权重（0禁用）"), Min(0)]
        [Tooltip("越大越重视该特征；0表示不使用。缺失特征的正权重仍留在评分分母中。")]
        public float weight = 1;
        [InspectorName("必需特征")]
        [Tooltip("正权重的必需特征缺失时拒绝识别；不要给容易遮挡的特征全部勾选。")]
        public bool required;
    }

    [CreateAssetMenu(menuName = "NarutoVR/结印模板")]
    public sealed class SealProfile : ScriptableObject
    {
        [Header("一、印名称与启用状态")]
        [InspectorName("印名称")]
        [Tooltip("例如寅、未、巳；用于实验台、头显识别结果和模板列表显示。")]
        public string sealName = "未标定";
        [InspectorName("已确认模板（启用识别）")]
        [Tooltip("从录制生成的是草稿。检查数值范围后勾选，才会参与识别。")]
        public bool calibrated;
        [Header("二、识别通过条件")]
        [InspectorName("加权得分阈值"), Range(0, 1)]
        [Tooltip("默认0.8表示至少80%加权匹配度；不是识别正确率或概率。")]
        public float threshold = 0.8f;
        [InspectorName("有效数据覆盖率下限"), Range(0, 1)]
        [Tooltip("可用特征的正权重占全部正权重的比例；默认至少80%。")]
        public float minimumCoverage = 0.8f;
        [InspectorName("允许低置信特征")]
        [Tooltip("开启可提高可用性，也可能增加误判。默认关闭，优先调整特征选择和权重。")]
        public bool allowLowConfidence;
        [InspectorName("连续达标保持时间（秒）"), Min(0)]
        [Tooltip("默认0.07秒，即70毫秒。候选持续达标这么久后才确认结果。")]
        public float holdSeconds = 0.07f;
        [Header("三、特征规则与手动权重")]
        [InspectorName("特征规则列表")]
        [Tooltip("逐项配置目标范围、软容差和权重。中文名称用于阅读，固定特征标识用于匹配录制数据。")]
        public List<FeatureRule> rules = new();

        public string ValidateRules()
        {
            if (!calibrated) return "Template has not been calibrated";
            if (!float.IsFinite(threshold) || threshold <= 0 || threshold > 1 ||
                !float.IsFinite(minimumCoverage) || minimumCoverage <= 0 || minimumCoverage > 1 ||
                !float.IsFinite(holdSeconds) || holdSeconds < 0) return "Invalid score/coverage/hold setting";
            if (rules == null || rules.Count == 0) return "No feature rules";
            var ids = new HashSet<string>(); float total = 0;
            foreach (var rule in rules)
            {
                if (rule == null || string.IsNullOrWhiteSpace(rule.featureId) || !ids.Add(rule.featureId)) return "Missing/duplicate feature ID";
                if (!float.IsFinite(rule.weight) || rule.weight < 0 || !float.IsFinite(rule.min) || !float.IsFinite(rule.max) ||
                    !float.IsFinite(rule.tolerance) || rule.min > rule.max || rule.tolerance <= 0) return "Invalid range, tolerance or weight";
                total += rule.weight;
            }
            return total > 0 && float.IsFinite(total) ? null : "No positive weights";
        }
    }

    public sealed class RuleScore
    {
        public FeatureRule rule;
        public SealFeature feature;
        public bool available;
        public float match, contribution;
    }
    public sealed class SealScore
    {
        public SealProfile profile;
        public float score, coverage;
        public bool passes;
        public string reason;
        public readonly List<RuleScore> details = new();
    }

    public static class SealScorer
    {
        public static SealScore Evaluate(SealProfile profile, IReadOnlyList<SealFeature> features)
        {
            var result = new SealScore { profile = profile, reason = profile ? profile.ValidateRules() : "No profile" };
            if (result.reason != null) return result;
            float total = 0, available = 0, weighted = 0; bool missingRequired = false;
            foreach (var rule in profile.rules)
            {
                if (rule.weight <= 0) continue;
                SealFeature feature = default; bool found = false;
                for (int i = 0; i < features.Count; i++) if (features[i].id == rule.featureId) { feature = features[i]; found = true; break; }
                bool valid = found && feature.valid && float.IsFinite(feature.value) && (profile.allowLowConfidence || feature.highConfidence);
                float distance = valid ? Mathf.Max(rule.min - feature.value, feature.value - rule.max, 0) : 0;
                float match = valid ? Mathf.Clamp01(1 - distance / rule.tolerance) : 0;
                total += rule.weight;
                if (valid) available += rule.weight;
                else missingRequired |= rule.required;
                weighted += rule.weight * match;
                result.details.Add(new RuleScore { rule = rule, feature = feature, available = valid, match = match, contribution = rule.weight * match });
            }
            result.score = weighted / total; result.coverage = available / total;
            result.passes = !missingRequired && result.coverage >= profile.minimumCoverage && result.score >= profile.threshold;
            result.reason = missingRequired ? "Required feature missing" : result.coverage < profile.minimumCoverage ? "Insufficient tracking coverage" :
                result.passes ? "Score passed; waiting for hold time" : "Below score threshold";
            return result;
        }
    }
}
