namespace NarutoVR.SealCombat.Enemies
{
    /// <summary>一次出生的不可变租约。回收后再次出生会得到新编号；禁止用旧租约回收新的敌人。</summary>
    public sealed class EnemyLease
    {
        public long Id { get; }
        public EnemyController Controller { get; }
        public string EnemyId { get; }
        internal EnemyManager Owner { get; }
        internal long SessionId { get; }
        internal EnemyLease(long id, EnemyController controller, string enemyId, EnemyManager owner, long sessionId)
        { Id = id; Controller = controller; EnemyId = enemyId; Owner = owner; SessionId = sessionId; }
    }
}
