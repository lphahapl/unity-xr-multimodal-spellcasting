namespace NarutoVR.SealCombat
{
    public enum SkillCastFailure
    {
        None, InvalidRequest, NotReady, InputBlocked, NotPermitted, Busy, Deferred,
        PreparationRejected, Invalidated, Dead, Cooldown, InsufficientMana
    }

    /// <summary>单个请求的回执；确认以后发生的表现故障只记入 Detail。</summary>
    public readonly struct SkillCastResult
    {
        public readonly bool Confirmed;
        public readonly SkillCastFailure Failure;
        public readonly string Detail;
        SkillCastResult(bool confirmed, SkillCastFailure failure, string detail)
        { Confirmed = confirmed; Failure = failure; Detail = detail; }
        public static SkillCastResult Accepted(string detail = null) => new(true, SkillCastFailure.None, detail);
        public static SkillCastResult Rejected(SkillCastFailure failure, string detail) => new(false, failure, detail);
    }
}
