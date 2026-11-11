using System;

namespace NarutoVR.SealCombat.Rpg
{
    /// <summary>玩家、成长和章节使用的稳定标识及数值范围。</summary>
    internal static class RpgDataRules
    {
        internal const float MaximumStat = 10000, MaximumDuration = 86400;
        internal const int MaximumEntries = 2048, MaximumCurrency = 1000000, MaximumSkillLevel = 10;
        /// <summary>拒绝无法保存为正常数值的 NaN 和无穷大。</summary>
        internal static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        /// <summary>检查有限浮点值是否位于含边界的区间内。</summary>
        internal static bool InRange(float value, float min, float max) => Finite(value) && value >= min && value <= max;
        /// <summary>稳定标识不能为空、超长、含控制字符或带首尾空白。</summary>
        internal static bool ValidId(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || id.Length > 128 || id != id.Trim()) return false;
            foreach (char c in id) if (char.IsControl(c)) return false;
            return true;
        }
    }
}
