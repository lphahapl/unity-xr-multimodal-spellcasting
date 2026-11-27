namespace NarutoVR.SealCombat.Rpg
{
    public interface IRpgCheckpointEquipment
    {
        void CaptureEquipment(RpgCheckpointData checkpoint);
        void RestoreEquipment(RpgCheckpointData checkpoint);
    }

    /// <summary>跨场景时只捕获随身装备。在目标场景恢复前，将落点换成当地的安全位置。</summary>
    public interface IRpgTravelEquipment : IRpgCheckpointEquipment
    {
        bool CaptureCarriedEquipment(RpgCheckpointData checkpoint);
        void PrepareArrival(RpgCheckpointData checkpoint, UnityEngine.Vector3 landing);
    }
}
