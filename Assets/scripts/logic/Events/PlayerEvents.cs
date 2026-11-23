using NarutoVR.SealCombat.Rpg;

namespace NarutoVR.SealCombat.Events
{
    public readonly struct SealConfirmedNotice
    {
        public readonly SkillInputController Source;
        public readonly string SealName;
        public readonly double CapturedAt;

        public SealConfirmedNotice(SkillInputController source, string sealName, double capturedAt)
        {
            Source = source;
            SealName = sealName;
            CapturedAt = capturedAt;
        }
    }

    public readonly struct PlayerDamageNotice
    {
        public readonly PlayerObj Player;
        public readonly string HitId;
        public readonly PlayerDamageResult Result;

        public PlayerDamageNotice(PlayerObj player, string hitId, PlayerDamageResult result)
        {
            Player = player;
            HitId = hitId;
            Result = result;
        }
    }

    public readonly struct PlayerStageNotice
    {
        public readonly PlayerObj Player;
        public readonly RpgStageDefinition Stage;

        public PlayerStageNotice(PlayerObj player, RpgStageDefinition stage)
        {
            Player = player;
            Stage = stage;
        }
    }

    public static class PlayerEvents
    {
        // 同步通知，监听者读取的玩家许可已反映本次进入或退出。
        public static readonly EventKey<PlayerStageNotice> StageEntered = new("玩家进入阶段");
        public static readonly EventKey<PlayerStageNotice> StageExited = new("玩家退出阶段");
        public static readonly EventKey<SealConfirmedNotice> SealConfirmed = new("玩家稳定结成一个手印");
        public static readonly EventKey<PlayerDamageNotice> DamageSettled = new("玩家受击已结算");
    }
}
