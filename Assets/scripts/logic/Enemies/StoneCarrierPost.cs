using NarutoVR.SealCombat.Rpg;
using UnityEngine;

namespace NarutoVR.SealCombat.Enemies
{
    /// <summary>挂在房间指定的工奴出生点上，将本次出生实例连接到场景巡逻点及对应的配重篮。</summary>
    [DisallowMultipleComponent]
    public sealed class StoneCarrierPost : MonoBehaviour
    {
        public Transform[] patrolPoints;
        public BallastBasket basket;
        public bool Bind(EnemyLease lease, Transform player, CombatRoomEncounter room = null)
        {
            if (lease == null || lease.Defeated || !lease.Owner || !lease.Owner.IsCurrent(lease) ||
                !lease.Controller.TryGetComponent<StoneCarrier>(out var carrier) ||
                !carrier.Stone || !basket || !basket.passengerVolume || !basket.slot || !player) return false;
            if (!basket.TryBind(this, carrier.Stone, player, room)) return false;
            carrier.AssignRoute(patrolPoints);
            return true;
        }
        public void ResetPost() { if (basket) basket.ResetBinding(this); }
    }
}
