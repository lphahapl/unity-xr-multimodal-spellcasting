using System;
using System.Collections.Generic;

namespace NarutoVR.SealCombat.Rpg
{
    /// <summary>存档字段的公共边界和数值校验，不执行交易或奖励。</summary>
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
        /// <summary>检查资源快照与冷却组的范围、容量和重复项，不判断关卡落点。</summary>
        internal static bool ValidateCheckpoint(RpgCheckpointData data, float maxHealth, float maxMana, out string reason)
        {
            reason = "检查点生命、能量或冷却无效";
            if (data == null || !InRange(data.health, 0, maxHealth) || !InRange(data.mana, 0, maxMana) ||
                !InRange(data.manaRegenDelayRemaining, 0, MaximumDuration) || data.cooldowns == null || data.cooldowns.Count > MaximumEntries) return false;
            var groups = new HashSet<string>(StringComparer.Ordinal);
            foreach (var cd in data.cooldowns)
                if (cd == null || !ValidId(cd.group) || !groups.Add(cd.group) || !InRange(cd.remainingSeconds, 0, MaximumDuration)) return false;
            reason = null; return true;
        }
    }
}
