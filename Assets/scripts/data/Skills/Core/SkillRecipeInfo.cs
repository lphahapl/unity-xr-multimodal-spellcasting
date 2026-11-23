namespace NarutoVR.SealCombat
{
    /// <summary>当前印序的只读显示数据。HoldProgress 仅在短术正等待末印确认时有效。</summary>
    public readonly struct SkillSealHint
    {
        public readonly SkillDefinition Skill;
        public readonly int Matched, Total;
        public readonly string NextSeal;
        public readonly float HoldProgress;
        public bool Holding => HoldProgress >= 0;

        internal SkillSealHint(SkillDefinition skill, int matched, int total, string nextSeal, float holdProgress)
        {
            Skill = skill; Matched = matched; Total = total; NextSeal = nextSeal; HoldProgress = holdProgress;
        }
    }

    /// <summary>匹配器初始化时生成的只读配方快照，供界面和诊断展示本轮生效参数。</summary>
    public readonly struct SkillRecipeInfo
    {
        public readonly SkillDefinition Definition;
        public readonly string Id, Name, Phrase, Seal, Effect;
        public readonly System.Collections.Generic.IReadOnlyList<string> Seals;
        public readonly SkillTriggerMode Mode;
        public readonly float Window, Discount, Floor;
        public SkillRecipeInfo(SkillDefinition value)
        {
            Definition=value;Id=value.skillId;Name=string.IsNullOrWhiteSpace(value.displayName)?value.skillId:value.displayName;
            Phrase=SkillPhrase.Normalize(value.phrase);Seals=System.Array.AsReadOnly(value.GetSealSequence());Seal=string.Join(" → ",Seals);Effect=value.EffectSummary;Mode=value.triggerMode;
            Window=value.matchWindow;Discount=value.voiceDiscount;Floor=value.thresholdFloor;
        }
    }
}
