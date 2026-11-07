using System.Text;

namespace NarutoVR.SealCombat
{
    /// <summary>统一口令标签的格式，供配置校验和完整口令匹配共用。</summary>
    public static class SkillPhrase
    {
        public const string DefaultCancel = "取消施法";
        public static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "";
            var result = new StringBuilder(value.Length);
            foreach (char c in value)
                if (!char.IsWhiteSpace(c) && !char.IsPunctuation(c)) result.Append(c);
            return result.ToString();
        }
    }
}
