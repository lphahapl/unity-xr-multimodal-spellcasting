using System;
using System.Linq;
using Oculus.Interaction.PoseDetection;
using UnityEngine;

namespace NarutoVR.SealCombat
{
    [CreateAssetMenu(menuName = "NarutoVR/基础手形/官方手指状态配置")]
    public sealed class BasicHandPoseSettings : ScriptableObject
    {
        [Header("一、Meta官方手指状态阈值")]
        [InspectorName("食指阈值")] public FingerFeatureStateThresholds index;
        [InspectorName("中指阈值")] public FingerFeatureStateThresholds middle;
        [InspectorName("无名指与小指阈值")] public FingerFeatureStateThresholds ringAndPinky;
        [Header("二、说明：原始Curl伸直约180度；拇指不参与")]
        [SerializeField, InspectorName("检测规则"), TextArea(2, 4)]
        string description = "左手指向：食指Open，其余三指Closed。右手张掌：四指Open。直接使用SDK状态引擎的滞回和最短确认时间；失效数据立即清空。十二印仍使用原加权评分。";

        public FingerFeatureStateThresholds ForFinger(int finger) => finger == 1 ? index : finger == 2 ? middle : ringAndPinky;

        public string Validate()
        {
            for (int finger = 1; finger <= 4; finger++)
            {
                var asset = ForFinger(finger);
                if (!asset) return "缺少官方手指阈值资产";
                if (!double.IsFinite(asset.MinTimeInState) || asset.MinTimeInState <= 0) return "手指状态确认时间必须为正数";
                var features = asset.FeatureStateThresholds;
                if (features == null) return "手指阈值列表为空";
                foreach (FingerFeature feature in Enum.GetValues(typeof(FingerFeature)))
                {
                    var entries = features.Where(f => f != null && f.Feature == feature).ToArray();
                    if (entries.Length != 1 || entries[0].Thresholds == null || entries[0].Thresholds.Count == 0) return "官方状态阈值缺失或重复";
                    foreach (var threshold in entries[0].Thresholds)
                        if (threshold == null || !float.IsFinite(threshold.ToFirstWhenBelow) || !float.IsFinite(threshold.ToSecondWhenAbove) ||
                            threshold.ToFirstWhenBelow > threshold.ToSecondWhenAbove || string.IsNullOrEmpty(threshold.FirstState) || string.IsNullOrEmpty(threshold.SecondState))
                            return "官方状态阈值无效";
                }
                var curl = features.Single(f => f.Feature == FingerFeature.Curl).Thresholds;
                if (curl.Count != 2 || curl[0].FirstState != "0" || curl[0].SecondState != "1" || curl[1].FirstState != "1" || curl[1].SecondState != "2" ||
                    curl[0].ToFirstWhenBelow <= 90 || curl[1].ToFirstWhenBelow <= curl[0].ToSecondWhenAbove)
                    return "Curl必须使用SDK原始角度的Open/Neutral/Closed阈值，不能使用归零弯曲角";
            }
            return null;
        }
    }
}
