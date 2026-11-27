namespace NarutoVR.SealCombat.Rpg
{
    /// <summary>Optional scene-specific upgrade applied to a loaded snapshot before route validation.</summary>
    internal interface IRpgCheckpointMigration
    {
        void MigrateCheckpoint(RpgProgressData snapshot);
    }
}
