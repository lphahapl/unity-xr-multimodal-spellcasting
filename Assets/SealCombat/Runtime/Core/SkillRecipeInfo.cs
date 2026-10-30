namespace NarutoVR.SealCombat
{
    /// <summary>Read-only input recipe captured when the matcher starts.</summary>
    public readonly struct SkillRecipeInfo
    {
        public readonly SkillDefinition Definition;
        public readonly string Id, Name, Phrase, Seal, Effect;
        public readonly SkillTriggerMode Mode;
        public readonly float Window, Discount, Floor;
        public SkillRecipeInfo(SkillDefinition value)
        {
            Definition=value;Id=value.skillId;Name=string.IsNullOrWhiteSpace(value.displayName)?value.skillId:value.displayName;
            Phrase=SkillPhrase.Normalize(value.phrase);Seal=value.sealName;Effect=value.EffectSummary;Mode=value.triggerMode;
            Window=value.matchWindow;Discount=value.voiceDiscount;Floor=value.thresholdFloor;
        }
    }
}
