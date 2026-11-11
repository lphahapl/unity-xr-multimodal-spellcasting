namespace NarutoVR.SealCombat.Enemies
{
    /// <summary>记录一次出生的固定身份和击败状态。回收后再次出生会得到新编号。</summary>
    public sealed class EnemyLease
    {
        public long Id { get; }
        public EnemyController Controller { get; }
        public string EnemyId { get; }
        /// <summary>本次出生已经死亡，即使底层物体之后用于另一只怪亦保持不变。</summary>
        public bool Defeated { get; internal set; }
        internal EnemyManager Owner { get; }
        internal long SessionId { get; }
        internal EnemyLease(long id, EnemyController controller, string enemyId, EnemyManager owner, long sessionId)
        { Id = id; Controller = controller; EnemyId = enemyId; Owner = owner; SessionId = sessionId; }
    }
}
