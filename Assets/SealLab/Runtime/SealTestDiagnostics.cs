using System.Linq;
using System.Text;
using UnityEngine;

namespace NarutoVR.SealLab
{
    public static class SealTestDiagnostics
    {
        public static string Explain(SealScore score)
        {
            if (score == null || !score.profile) return "未选择模板";
            var p = score.profile;
            var text = new StringBuilder($"【{p.sealName}】{SealChineseText.Reason(score.reason)}；得分 {score.score * 100:F1}/100，要求 {p.threshold * 100:F1}；覆盖率 {score.coverage:P1}，要求 {p.minimumCoverage:P1}");
            float total = score.details.Sum(d => d.rule.weight);
            foreach (var d in score.details.OrderByDescending(d => d.rule.weight * (1 - d.match)))
            {
                if (d.available && d.match >= 0.99999f) continue;
                string actual = !d.feature.valid || !float.IsFinite(d.feature.value) ? "数据无效或缺失" :
                    !d.available ? $"{d.feature.value:F3}（低质量，被过滤）" : $"{d.feature.value:F3} {SealChineseText.Unit(d.feature.unit)}";
                text.Append($"\n  {SealChineseText.FeatureName(d.rule.featureId)} [{d.rule.featureId}]：{actual}；目标 [{d.rule.min:F3}, {d.rule.max:F3}]，容差 {d.rule.tolerance:F3}；扣 {(total > 0 ? 100 * d.rule.weight * (1 - d.match) / total : 0):F2} 分" +
                    (!d.available && d.rule.required ? "；必需特征缺失，拒绝" : ""));
            }
            return text.ToString();
        }
    }
}
