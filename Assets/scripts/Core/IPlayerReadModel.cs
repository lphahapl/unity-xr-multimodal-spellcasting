namespace NarutoVR.SealCombat.Rpg
{
    /// <summary>面板和腕匣可读取的玩家数值及知识，不提供改变角色状态的命令。</summary>
    public interface IPlayerReadModel
    {
        float Health { get; }
        float MaxHealth { get; }
        float Mana { get; }
        float MaxMana { get; }
        float ShieldPoints { get; }
        float ShieldCapacity { get; }
        double ShieldExpiresAt { get; }
        bool Alive { get; }
        int Currency { get; }
        int UnlockedSkillCount { get; }
        float GetCooldown(string group, double now);
        int SkillLevel(string id);
        bool IsUnlocked(string id);
        bool IsDiscovered(string id);
    }
}
