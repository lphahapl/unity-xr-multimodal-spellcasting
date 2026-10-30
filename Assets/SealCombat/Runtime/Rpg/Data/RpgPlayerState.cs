using System;
using System.Collections.Generic;

namespace NarutoVR.SealCombat.Rpg
{
    /// <summary>Single-threaded, synchronous player state. Effects may read this object, but must not mutate it during activation.</summary>
    public sealed class RpgPlayerState
    {
        public const int MaximumSessionAttempts = 65536;
        readonly HashSet<string> activations = new HashSet<string>(StringComparer.Ordinal);
        readonly HashSet<string> damages = new HashSet<string>(StringComparer.Ordinal);
        readonly Dictionary<string, double> cooldowns = new Dictionary<string, double>(StringComparer.Ordinal);
        readonly float manaRegen, regenDelay;
        double lastNow, regenAfter;
        bool hasTime, activating;

        public float Health { get; private set; }
        public float Mana { get; private set; }
        public float MaxHealth { get; }
        public float MaxMana { get; }
        public bool Alive => Health > 0;
        public long Revision { get; private set; }

        public RpgPlayerState(float maxHealth = 100, float maxMana = 100, float manaRegen = 5, float regenDelay = 2)
        {
            if (!RpgDataRules.InRange(maxHealth, 1, RpgDataRules.MaximumStat) ||
                !RpgDataRules.InRange(maxMana, 1, RpgDataRules.MaximumStat) ||
                !RpgDataRules.InRange(manaRegen, 0, RpgDataRules.MaximumStat) ||
                !RpgDataRules.InRange(regenDelay, 0, RpgDataRules.MaximumDuration))
                throw new ArgumentOutOfRangeException(nameof(maxHealth), "玩家定义数值必须有限且在合法范围内。");
            MaxHealth = Health = maxHealth;
            MaxMana = Mana = maxMana;
            this.manaRegen = manaRegen;
            this.regenDelay = regenDelay;
        }

        public void Tick(double gameNow)
        {
            RequireMutable();
            if (!ValidNow(gameNow)) throw new ArgumentOutOfRangeException(nameof(gameNow), "游戏时间必须有限、非负且不能倒退。");
            if (hasTime && Alive && Mana < MaxMana)
            {
                double elapsed = Math.Max(0, gameNow - Math.Max(lastNow, regenAfter));
                float next = (float)Math.Min(MaxMana, Mana + elapsed * manaRegen);
                if (next != Mana) { Mana = next; Revision++; }
            }
            lastNow = gameNow;
            hasTime = true;
        }

        public float GetCooldown(string group, double now)
        {
            if (!RpgDataRules.Finite(now) || now < 0 || string.IsNullOrEmpty(group)) return 0;
            return cooldowns.TryGetValue(group, out double until) ? (float)Math.Max(0, until - now) : 0;
        }

        /// <summary>IDs are retained for the entire session. At the explicit attempt cap, all new requests fail closed; create a new session to continue.</summary>
        public bool TryActivate(string activationId, string skillId, string cooldownGroup, float manaCost,
            float cooldownSeconds, double now, Func<bool> activateEffect, out string reason)
        {
            if (!Claim(activations, activationId, out reason)) return false;
            if (activating) { reason = "激活回调中禁止重入"; return false; }
            string group = string.IsNullOrEmpty(cooldownGroup) ? skillId : cooldownGroup;
            if (!RpgDataRules.ValidId(skillId) || !RpgDataRules.ValidId(group) || activateEffect == null ||
                !RpgDataRules.InRange(manaCost, 0, RpgDataRules.MaximumStat) ||
                !RpgDataRules.InRange(cooldownSeconds, 0, RpgDataRules.MaximumDuration) || !ValidNow(now))
            { reason = "技能参数或游戏时间无效"; return false; }
            Tick(now);
            if (!Alive) { reason = "玩家已死亡"; return false; }
            if (GetCooldown(group, now) > 0) { reason = "技能冷却中"; return false; }
            if (Mana < manaCost) { reason = "能量不足"; return false; }
            float previousMana = Mana;
            Mana -= manaCost;
            activating = true;
            bool succeeded;
            try { succeeded = activateEffect(); }
            catch (Exception ex)
            {
                Mana = previousMana;
                reason = "效果执行异常，已退还本次能量：" + ex.GetType().Name;
                return false;
            }
            finally { activating = false; }
            if (!succeeded)
            {
                Mana = previousMana;
                reason = "效果未激活，已退还本次能量";
                return false;
            }
            if (cooldownSeconds > 0) cooldowns[group] = now + cooldownSeconds;
            if (manaCost > 0) regenAfter = now + regenDelay;
            Revision++;
            reason = null;
            return true;
        }

        public bool ApplyDamage(string damageId, float amount, double now, out float applied)
        {
            applied = 0;
            if (!Claim(damages, damageId, out _)) return false;
            if (activating || !RpgDataRules.InRange(amount, 0, RpgDataRules.MaximumStat) || !ValidNow(now)) return false;
            Tick(now);
            applied = Math.Min(Health, amount);
            if (applied > 0) { Health -= applied; Revision++; }
            return true;
        }

        public float Heal(float amount)
        {
            RequireMutable();
            if (!RpgDataRules.InRange(amount, 0, RpgDataRules.MaximumStat)) throw new ArgumentOutOfRangeException(nameof(amount));
            if (!Alive) return 0; // Only explicit respawn revives a dead player.
            float applied = Math.Min(MaxHealth - Health, amount);
            if (applied > 0) { Health += applied; Revision++; }
            return applied;
        }

        public float RestoreMana(float amount)
        {
            RequireMutable();
            if (!RpgDataRules.InRange(amount, 0, RpgDataRules.MaximumStat)) throw new ArgumentOutOfRangeException(nameof(amount));
            float applied = Math.Min(MaxMana - Mana, amount);
            if (applied > 0) { Mana += applied; Revision++; }
            return applied;
        }

        public void Respawn(float health, float mana, double now, bool clearCooldowns = false)
        {
            RequireMutable();
            if (!RpgDataRules.InRange(health, 0.001f, MaxHealth) || !RpgDataRules.InRange(mana, 0, MaxMana) || !ValidNow(now))
                throw new ArgumentOutOfRangeException(nameof(health), "重生数值或时间无效。");
            Health = health; Mana = mana; lastNow = now; hasTime = true; regenAfter = now + regenDelay;
            if (clearCooldowns) cooldowns.Clear();
            Revision++;
        }

        public void Respawn(double now, bool clearCooldowns = false) => Respawn(MaxHealth, MaxMana, now, clearCooldowns);

        public RpgCheckpointData CaptureCheckpoint(double now)
        {
            RequireMutable();
            Tick(now);
            var result = new RpgCheckpointData { health = Health, mana = Mana,
                manaRegenDelayRemaining = (float)Math.Max(0, regenAfter - now) };
            foreach (var pair in cooldowns)
                if (pair.Value > now) result.cooldowns.Add(new RpgCooldownData { group = pair.Key, remainingSeconds = (float)(pair.Value - now) });
            result.cooldowns.Sort((a, b) => StringComparer.Ordinal.Compare(a.group, b.group));
            return result;
        }

        public bool RestoreCheckpoint(RpgCheckpointData checkpoint, double now, out string reason)
        {
            if (activating) { reason = "激活回调中禁止修改玩家数值"; return false; }
            if (!ValidNow(now) || !RpgDataRules.ValidateCheckpoint(checkpoint, MaxHealth, MaxMana, out reason))
            { reason = "检查点数值或时间无效"; return false; }
            cooldowns.Clear();
            foreach (var item in checkpoint.cooldowns) cooldowns.Add(item.group, now + item.remainingSeconds);
            Health = checkpoint.health; Mana = checkpoint.mana;
            regenAfter = now + checkpoint.manaRegenDelayRemaining; lastNow = now; hasTime = true; Revision++;
            reason = null;
            return true;
        }

        bool ValidNow(double now) => RpgDataRules.Finite(now) && now >= 0 && now <= 1e12 && (!hasTime || now >= lastNow);
        void RequireMutable()
        {
            if (activating) throw new InvalidOperationException("激活回调返回前禁止修改玩家数值。");
        }
        static bool Claim(HashSet<string> ledger, string id, out string reason)
        {
            if (!RpgDataRules.ValidId(id)) { reason = "请求ID无效"; return false; }
            if (ledger.Contains(id)) { reason = "重复请求ID"; return false; }
            if (ledger.Count >= MaximumSessionAttempts) { reason = "会话请求数量已达上限，请建立新会话"; return false; }
            ledger.Add(id); reason = null; return true;
        }
    }
}
