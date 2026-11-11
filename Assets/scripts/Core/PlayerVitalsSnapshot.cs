using System;
using System.Collections.Generic;

namespace NarutoVR.SealCombat.Rpg
{
    internal readonly struct PlayerVitalsSnapshot
    {
        internal readonly float Health, Mana, RegenDelayRemaining;
        internal readonly IReadOnlyList<KeyValuePair<string, float>> Cooldowns;

        internal PlayerVitalsSnapshot(float health, float mana, float regenDelay, IReadOnlyList<KeyValuePair<string, float>> cooldowns)
        {
            Health = health;
            Mana = mana;
            RegenDelayRemaining = regenDelay;
            Cooldowns = cooldowns;
        }

        internal bool IsValid(float maxHealth, float maxMana)
        {
            if (!RpgDataRules.InRange(Health, 0, maxHealth) || !RpgDataRules.InRange(Mana, 0, maxMana) ||
                !RpgDataRules.InRange(RegenDelayRemaining, 0, RpgDataRules.MaximumDuration) ||
                Cooldowns == null || Cooldowns.Count > RpgDataRules.MaximumEntries) return false;
            var groups = new HashSet<string>(StringComparer.Ordinal);
            foreach (var pair in Cooldowns)
                if (!RpgDataRules.ValidId(pair.Key) || !groups.Add(pair.Key) ||
                    !RpgDataRules.InRange(pair.Value, 0, RpgDataRules.MaximumDuration)) return false;
            return true;
        }
    }
}
