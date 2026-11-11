using System;
using System.Collections.Generic;
using System.Linq;

namespace NarutoVR.SealCombat.Rpg
{
    public enum SkillOfferKind { Unlock, Upgrade }

    public readonly struct PlayerDamageResult
    {
        public readonly float Requested, Absorbed, HealthLost;

        public PlayerDamageResult(float requested, float absorbed, float healthLost)
        {
            Requested = requested;
            Absorbed = absorbed;
            HealthLost = healthLost;
        }
    }

    /// <summary>物品交互方可查询的只读状态，不暴露可变存档记录。</summary>
    public enum ProgressionItemState { Missing, Collected, Activated }

    /// <summary>玩家实体持有的会话状态；存档信封只在入口和出口映射，外部不能改动内部集合。</summary>
    public sealed class PlayerModel : IPlayerReadModel
    {
        readonly List<string> discovered = new();
        readonly List<string> unlocked = new();
        readonly Dictionary<string, int> levels = new(StringComparer.Ordinal);
        readonly Dictionary<string, bool> items = new(StringComparer.Ordinal);
        readonly List<string> rewards = new();
        public RpgPlayerState Vitals { get; }
        public ChapterModel Chapter { get; } = new();
        public int Currency { get; private set; }
        public int UnlockedSkillCount => unlocked.Count;
        public int ItemCount => items.Count;
        public IReadOnlyList<string> DiscoveredSkills { get; }
        public IReadOnlyList<string> UnlockedSkills { get; }
        public IReadOnlyList<string> ClaimedRewards { get; }
        public PlayerModel(RpgPlayerState vitals)
        {
            Vitals = vitals ?? throw new ArgumentNullException(nameof(vitals));
            DiscoveredSkills = discovered.AsReadOnly();
            UnlockedSkills = unlocked.AsReadOnly();
            ClaimedRewards = rewards.AsReadOnly();
        }

        public bool IsDiscovered(string id) => discovered.Contains(id) || IsUnlocked(id);
        public bool IsUnlocked(string id) => unlocked.Contains(id);
        public int SkillLevel(string id) => id != null && levels.TryGetValue(id, out int level) ? level : 0;
        public ProgressionItemState ItemState(string id) => id == null || !items.TryGetValue(id, out bool active)
            ? ProgressionItemState.Missing : active ? ProgressionItemState.Activated : ProgressionItemState.Collected;

        internal PlayerModel ForkGrowth()
        {
            var candidate = new PlayerModel(Vitals);
            candidate.Chapter.CopyRoute(Chapter);
            candidate.AdoptGrowth(this);
            return candidate;
        }

        internal void AdoptGrowth(PlayerModel source)
        {
            Currency = source.Currency;
            discovered.Clear();
            discovered.AddRange(source.discovered);
            unlocked.Clear();
            unlocked.AddRange(source.unlocked);
            rewards.Clear();
            rewards.AddRange(source.rewards);
            levels.Clear();
            foreach (var pair in source.levels) levels.Add(pair.Key, pair.Value);
            items.Clear();
            foreach (var pair in source.items) items.Add(pair.Key, pair.Value);
            Chapter.Restore(source.Chapter.CompletedStages);
        }

        internal bool Credit(int amount)
        {
            if (amount < 0 || amount > RpgDataRules.MaximumCurrency - Currency) return false;
            Currency += amount;
            return true;
        }
        float IPlayerReadModel.Health => Vitals.Health;
        float IPlayerReadModel.MaxHealth => Vitals.MaxHealth;
        float IPlayerReadModel.Mana => Vitals.Mana;
        float IPlayerReadModel.MaxMana => Vitals.MaxMana;
        float IPlayerReadModel.ShieldPoints => Vitals.ShieldPoints;
        float IPlayerReadModel.ShieldCapacity => Vitals.ShieldCapacity;
        double IPlayerReadModel.ShieldExpiresAt => Vitals.ShieldExpiresAt;
        bool IPlayerReadModel.Alive => Vitals.Alive;
        float IPlayerReadModel.GetCooldown(string group, double now) => Vitals.GetCooldown(group, now);

        internal bool Discover(IEnumerable<string> ids) => AddIds(discovered, ids);
        internal bool Unlock(IEnumerable<string> ids) => AddIds(unlocked, ids);
        static bool AddIds(List<string> list, IEnumerable<string> ids)
        {
            if (!TryGetAdditions(list, ids, out var additions)) return false;
            list.AddRange(additions);
            return true;
        }

        static bool TryGetAdditions(List<string> existing, IEnumerable<string> ids, out string[] additions)
        {
            additions = Array.Empty<string>();
            if (ids == null) return false;
            additions = ids.Distinct(StringComparer.Ordinal).Where(id => !existing.Contains(id)).ToArray();
            return existing.Count + additions.Length <= RpgDataRules.MaximumEntries && additions.All(RpgDataRules.ValidId);
        }

        internal bool ChangeItem(string itemId, IEnumerable<string> discoveries, IEnumerable<string> grants, bool activate)
        {
            if (!RpgDataRules.ValidId(itemId) || (!items.ContainsKey(itemId) && (activate || items.Count >= RpgDataRules.MaximumEntries))) return false;
            if (!TryGetAdditions(discovered, discoveries, out var newKnowledge)) return false;
            string[] newSkills = Array.Empty<string>();
            if (activate && !TryGetAdditions(unlocked, grants, out newSkills)) return false;
            discovered.AddRange(newKnowledge);
            unlocked.AddRange(newSkills);
            items[itemId] = activate || ItemState(itemId) == ProgressionItemState.Activated;
            return true;
        }

        internal bool Purchase(string skillId, SkillOfferKind kind, int cost, int maximumLevel, string requiredStage, out string reason)
        {
            reason = "商品不存在、门槛未达或余额不足";
            if (!RpgDataRules.ValidId(skillId) || cost < 0 || Currency < cost ||
                (requiredStage != null && !Chapter.IsComplete(requiredStage))) return false;
            reason = "已经解锁，或技能未解锁／已达到等级上限";
            if (kind == SkillOfferKind.Unlock)
            {
                if (IsUnlocked(skillId) || !Unlock(new[] { skillId })) return false;
            }
            else
            {
                if (kind != SkillOfferKind.Upgrade || !IsUnlocked(skillId) ||
                    maximumLevel < 1 || maximumLevel > RpgDataRules.MaximumSkillLevel ||
                    SkillLevel(skillId) >= maximumLevel || (!levels.ContainsKey(skillId) && levels.Count >= RpgDataRules.MaximumEntries)) return false;
                levels[skillId] = SkillLevel(skillId) + 1;
            }
            Currency -= cost;
            reason = null;
            return true;
        }

        internal bool ClaimStage(string stageId, int currency, IEnumerable<string> grants)
        {
            string reward = "reward." + stageId;
            if (!RpgDataRules.ValidId(stageId) || !RpgDataRules.ValidId(reward) || rewards.Contains(reward) ||
                rewards.Count >= RpgDataRules.MaximumEntries || Chapter.IsComplete(stageId) ||
                !TryGetAdditions(unlocked, grants, out var newSkills) || !Credit(currency)) return false;
            unlocked.AddRange(newSkills);
            rewards.Add(reward);
            Chapter.Complete(stageId);
            return true;
        }

        internal IEnumerable<KeyValuePair<string, int>> SkillLevels => levels;
        internal IEnumerable<KeyValuePair<string, bool>> Inventory => items;

        internal void LoadGrowth(int currency, IEnumerable<string> discoveries, IEnumerable<string> grants,
            IEnumerable<KeyValuePair<string, int>> skillLevels, IEnumerable<KeyValuePair<string, bool>> inventory,
            IEnumerable<string> claimedRewards, IEnumerable<string> completedStages)
        {
            Currency = currency;
            discovered.Clear();
            discovered.AddRange(discoveries);
            unlocked.Clear();
            unlocked.AddRange(grants);
            rewards.Clear();
            rewards.AddRange(claimedRewards);
            levels.Clear();
            foreach (var pair in skillLevels) levels.Add(pair.Key, pair.Value);
            items.Clear();
            foreach (var pair in inventory) items.Add(pair.Key, pair.Value);
            Chapter.Restore(completedStages);
        }
    }

    public sealed class ChapterModel
    {
        readonly List<string> completed = new();
        public IReadOnlyList<string> CompletedStages { get; }
        public ChapterModel() => CompletedStages = completed.AsReadOnly();
        string[] route = Array.Empty<string>();
        public int StageIndex { get; private set; }
        public bool CampaignComplete { get; private set; }
        internal void Configure(IEnumerable<string> stageIds) => route = stageIds.ToArray();
        public bool IsComplete(string id) => completed.Contains(id);
        internal void CopyRoute(ChapterModel source) => route = source.route;
        internal void Complete(string id)
        {
            completed.Add(id);
            SelectStage();
        }
        internal void Restore(IEnumerable<string> stages)
        {
            completed.Clear();
            completed.AddRange(stages);
            SelectStage();
        }
        void SelectStage()
        {
            int firstIncomplete = Array.FindIndex(route, id => !completed.Contains(id));
            CampaignComplete = route.Length > 0 && firstIncomplete < 0;
            StageIndex = firstIncomplete >= 0 ? firstIncomplete : Math.Max(0, route.Length - 1);
        }
    }
}
