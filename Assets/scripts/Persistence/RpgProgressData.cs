using System;
using System.Collections.Generic;

namespace NarutoVR.SealCombat.Rpg
{
    /// <summary>单个冷却组在检查点保存时的剩余秒数。</summary>
    [Serializable] public sealed class RpgCooldownData
    {
        public string group;
        public float remainingSeconds;
    }
    /// <summary>检查点资源快照，不包含临时效果或敌人状态。</summary>
    [Serializable] public sealed class RpgCheckpointData
    {
        public float health = 100;
        public float mana = 100;
        public float manaRegenDelayRemaining;
        public List<RpgCooldownData> cooldowns = new();
    }
    /// <summary>技能 ID 与已获得等级；实际数值由该技能效果解释。</summary>
    [Serializable] public sealed class RpgSkillLevelData
    {
        public string skillId;
        public int level;
    }
    /// <summary>成长物品的实际持有记录；记录存在表示已收集，activated 表示激活完成。</summary>
    [Serializable] public sealed class RpgItemProgressData
    {
        public string itemId;
        public bool activated;
    }
    /// <summary>磁盘信封，仅包含格式版本、档案身份及进度数据。</summary>
    [Serializable] public sealed class SaveEnvelope
    {
        public int version = RpgSaveStore.CurrentVersion;
        public string profileId;
        public RpgProgressData progress;
    }
    /// <summary>纯序列化进度 DTO。交易、奖励和技能规则由专门模块执行，本类不包含业务操作。</summary>
    [Serializable] public sealed class RpgProgressData
    {
        /// <summary>已了解但不一定能施放的技能；可用于开放教学内容。</summary>
        public List<string> discoveredSkillIds = new();
        /// <summary>永久解锁技能；当前能否施放还需章节许可和资源检查。</summary>
        public List<string> unlockedSkillIds = new();
        /// <summary>通用成长物品的收集与激活记录，不编码铜叶等具体物品类型。</summary>
        public List<RpgItemProgressData> items = new();
        /// <summary>技能升级等级。</summary>
        public List<RpgSkillLevelData> skillLevels = new();
        /// <summary>可用于交易的当前货币。</summary>
        public int currency;
        /// <summary>按实际完成顺序记录的阶段 ID。</summary>
        public List<string> completedStageIds = new();
        /// <summary>已经领取的一次性奖励 ID，用于防止重复发奖。</summary>
        public List<string> claimedRewardIds = new();
        /// <summary>保存时对应的安全落点 ID。</summary>
        public string checkpointId;
        /// <summary>该落点的角色资源快照。</summary>
        public RpgCheckpointData checkpoint = new();
    }

}
