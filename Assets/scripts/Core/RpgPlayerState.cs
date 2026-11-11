using System;
using System.Collections.Generic;

namespace NarutoVR.SealCombat.Rpg
{
    public enum CastReservationFailure { None, InvalidRequest, Busy, Dead, Cooldown, InsufficientMana }

    /// <summary>Single-threaded, synchronous player state. Effects may read this object, but must not mutate it during activation.</summary>
    public sealed class RpgPlayerState
    {
        public const int MaximumSessionAttempts = 65536;
        readonly HashSet<string> activations = new HashSet<string>(StringComparer.Ordinal);
        readonly HashSet<string> damages = new HashSet<string>(StringComparer.Ordinal);
        readonly Dictionary<string, double> cooldowns = new Dictionary<string, double>(StringComparer.Ordinal);
        readonly float manaRegen, regenDelay;
        double lastNow, regenAfter;
        bool hasTime;
        CastReservation pendingCast;

        internal bool CastInProgress => pendingCast != null;
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
        public bool TryReserveCast(string activationId, string skillId, string cooldownGroup, float manaCost,
            float cooldownSeconds, double now, out CastReservation reservation, out string reason)
            => TryReserveCast(activationId, skillId, cooldownGroup, manaCost, cooldownSeconds, now, out reservation, out _, out reason);

        public bool TryReserveCast(string activationId, string skillId, string cooldownGroup, float manaCost,
            float cooldownSeconds, double now, out CastReservation reservation, out CastReservationFailure failure, out string reason)
        {
            reservation = null;
            failure = CastReservationFailure.InvalidRequest;
            if (!Claim(activations, activationId, out reason)) return false;
            if (CastInProgress) { failure = CastReservationFailure.Busy; reason = "激活回调中禁止重入"; return false; }
            string group = string.IsNullOrEmpty(cooldownGroup) ? skillId : cooldownGroup;
            if (!RpgDataRules.ValidId(skillId) || !RpgDataRules.ValidId(group) ||
                !RpgDataRules.InRange(manaCost, 0, RpgDataRules.MaximumStat) ||
                !RpgDataRules.InRange(cooldownSeconds, 0, RpgDataRules.MaximumDuration) || !ValidNow(now))
            { reason = "技能参数或游戏时间无效"; return false; }
            Tick(now);
            if (!Alive) { failure = CastReservationFailure.Dead; reason = "玩家已死亡"; return false; }
            if (GetCooldown(group, now) > 0) { failure = CastReservationFailure.Cooldown; reason = "技能冷却中"; return false; }
            if (Mana < manaCost) { failure = CastReservationFailure.InsufficientMana; reason = "能量不足"; return false; }
            pendingCast = reservation = new CastReservation(this, group, manaCost, cooldownSeconds, now);
            failure = CastReservationFailure.None;
            return true;
        }

        public sealed class CastReservation : IDisposable
        {
            RpgPlayerState owner;
            readonly string group;
            readonly float cost, seconds;
            readonly double now;
            float? nextShield;
            double nextShieldExpiry;

            internal CastReservation(RpgPlayerState owner, string group, float cost, float seconds, double now)
            {
                this.owner = owner;
                this.group = group;
                this.cost = cost;
                this.seconds = seconds;
                this.now = now;
            }

            internal void PrepareShield(float capacity, double expiresAt)
            {
                nextShield = capacity;
                nextShieldExpiry = expiresAt;
            }

            public void Commit()
            {
                if (owner == null) return;
                owner.Mana -= cost;
                if (seconds > 0) owner.cooldowns[group] = now + seconds;
                if (cost > 0) owner.regenAfter = now + owner.regenDelay;
                if (nextShield.HasValue)
                {
                    owner.ShieldCapacity = owner.ShieldPoints = nextShield.Value;
                    owner.ShieldExpiresAt = nextShieldExpiry;
                }
                owner.Revision++;
                Dispose();
            }

            public void Dispose()
            {
                if (owner == null) return;
                owner.pendingCast = null;
                owner = null;
            }
        }

        public float ShieldPoints { get; private set; }
        public float ShieldCapacity { get; private set; }
        public double ShieldExpiresAt { get; private set; }

        public void SetShield(float capacity, double expiresAt)
        {
            if (!RpgDataRules.InRange(capacity, 0, RpgDataRules.MaximumStat) || !RpgDataRules.Finite(expiresAt) || expiresAt < 0)
                throw new ArgumentOutOfRangeException(nameof(capacity));
            if (pendingCast != null) pendingCast.PrepareShield(capacity, expiresAt);
            else
            {
                ShieldCapacity = ShieldPoints = capacity;
                ShieldExpiresAt = expiresAt;
            }
        }

        public void ClearShield() => SetShield(0, 0);

        public float AbsorbShield(float amount, double now)
        {
            if (CastInProgress || !RpgDataRules.InRange(amount, 0, RpgDataRules.MaximumStat) || !RpgDataRules.Finite(now) || now < 0) return 0;
            if (now >= ShieldExpiresAt) { ClearShield(); return 0; }
            float absorbed = Math.Min(ShieldPoints, amount);
            ShieldPoints -= absorbed;
            if (ShieldPoints == 0) ClearShield();
            return absorbed;
        }

        public int ReceivedHitCount => damages.Count;
        public bool HasReceivedDamage(string id) => id != null && damages.Contains(id);
        internal bool SettleHit(string id, float amount, double now, out PlayerDamageResult result)
        {
            result = default;
            if (!Alive || !RpgDataRules.InRange(amount, float.Epsilon, RpgDataRules.MaximumStat) || !ValidNow(now) || !Claim(damages, id, out _) || CastInProgress) return false;
            Tick(now);
            float absorbed = AbsorbShield(amount, now);
            float lost = Math.Min(Health, amount - absorbed);
            Health -= lost;
            Revision++;
            result = new PlayerDamageResult(amount, absorbed, lost);
            return true;
        }

        public bool ApplyDamage(string damageId, float amount, double now, out float applied)
        {
            bool settled = SettleHit(damageId, amount, now, out var result);
            applied = result.HealthLost;
            return settled;
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
            ClearShield();
            Health = health; Mana = mana; lastNow = now; hasTime = true; regenAfter = now + regenDelay;
            if (clearCooldowns) cooldowns.Clear();
            Revision++;
        }

        public void Respawn(double now, bool clearCooldowns = false) => Respawn(MaxHealth, MaxMana, now, clearCooldowns);

        internal PlayerVitalsSnapshot CaptureVitals(double now)
        {
            Tick(now);
            var remaining = new List<KeyValuePair<string, float>>();
            foreach (var pair in cooldowns)
                if (pair.Value > now) remaining.Add(new KeyValuePair<string, float>(pair.Key, (float)(pair.Value - now)));
            remaining.Sort((a, b) => StringComparer.Ordinal.Compare(a.Key, b.Key));
            return new PlayerVitalsSnapshot(Health, Mana, (float)Math.Max(0, regenAfter - now), remaining);
        }

        internal bool RestoreVitals(PlayerVitalsSnapshot snapshot, double now, out string reason)
        {
            if (CastInProgress) { reason = "激活回调中禁止修改玩家数值"; return false; }
            if (!ValidNow(now) || !snapshot.IsValid(MaxHealth, MaxMana))
            { reason = "检查点数值或时间无效"; return false; }
            cooldowns.Clear();
            foreach (var pair in snapshot.Cooldowns) cooldowns.Add(pair.Key, now + pair.Value);
            ClearShield();
            Health = snapshot.Health;
            Mana = snapshot.Mana;
            regenAfter = now + snapshot.RegenDelayRemaining;
            lastNow = now;
            hasTime = true;
            Revision++;
            reason = null;
            return true;
        }

        bool ValidNow(double now) => RpgDataRules.Finite(now) && now >= 0 && now <= 1e12 && (!hasTime || now >= lastNow);
        void RequireMutable()
        {
            if (CastInProgress) throw new InvalidOperationException("激活回调返回前禁止修改玩家数值。");
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
