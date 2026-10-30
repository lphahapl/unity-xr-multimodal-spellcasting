namespace NarutoVR.SealLab
{
    // Presentation only: recorded feature IDs, units and scoring reason codes stay stable.
    public static class SealChineseText
    {
        public static string FeatureName(string id)
        {
            if (string.IsNullOrEmpty(id)) return "未指定特征";
            string pair = id switch
            {
                "Both.WristDistance" => "双手腕间距",
                "Both.PalmAngle" => "双掌朝向夹角",
                "Both.IndexTipDistance" => "双食指尖间距",
                "Both.MiddleTipDistance" => "双中指尖间距",
                "Both.ThumbTipDistance" => "双拇指尖间距",
                "Both.IndexDirectionAngle" => "双食指方向夹角",
                "Both.RightWristInLeft.X" => "右腕相对左腕·横向偏移",
                "Both.RightWristInLeft.Y" => "右腕相对左腕·纵向偏移",
                "Both.RightWristInLeft.Z" => "右腕相对左腕·前后偏移",
                _ => null
            };
            if (pair != null) return pair;
            var parts = id.Split('.');
            if (parts.Length != 3 || (parts[0] != "L" && parts[0] != "R")) return id;
            string finger = parts[1] switch
            { "Thumb" => "拇指", "Index" => "食指", "Middle" => "中指", "Ring" => "无名指", "Pinky" => "小指", "Max" => "小指", _ => parts[1] };
            string shape = parts[2] switch
            { "Curl" => "卷曲", "Flexion" => "屈曲", "Abduction" => "张开", "Opposition" => "对指距离", _ => parts[2] };
            return (parts[0] == "L" ? "左手·" : "右手·") + finger + "·" + shape;
        }
        public static string Unit(string unit) => unit switch
        { "deg" => "度", "m" => "米", "m@scale1" => "米（标准手尺度）", _ => unit ?? "" };
        public static string SampleLabel(string label) => label switch
        { "unlabelled" => "未标注", "none" => "无印（负样本）", "transition" => "换印过程", "near_miss" => "相近错误印", _ => label ?? "" };
        public static string Reason(string reason) => reason switch
        {
            "Template has not been calibrated" => "模板尚未确认或启用",
            "Invalid score/coverage/hold setting" => "得分、覆盖率或保持时间设置无效",
            "No feature rules" => "模板没有特征规则",
            "Missing/duplicate feature ID" => "特征标识缺失或重复",
            "Invalid range, tolerance or weight" => "特征范围、容差或权重无效",
            "No positive weights" => "没有大于零的权重",
            "No profile" => "尚未选择模板",
            "Required feature missing" => "必需特征缺失",
            "Insufficient tracking coverage" => "有效特征覆盖率不足",
            "Below score threshold" => "低于得分阈值",
            "Score passed; waiting for hold time" => "单帧达标，等待保持时间与歧义检查",
            _ => reason ?? ""
        };
    }
}
