using System.Text;

namespace NarutoVR.SealCombat
{
    /// <summary>Shared label normalization; this is not natural-language intent recognition.</summary>
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
