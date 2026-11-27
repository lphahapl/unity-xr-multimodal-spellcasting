using NarutoVR.SealCombat.Rpg;
using UnityEngine;
using UnityEngine.Events;

namespace NarutoVR.SealCombat.Enemies
{
    /// <summary>A 篮只接受该出生点在本轮持有的镇石。代表点进入篮体后开放符力，实际受力且抵达槽心才能入槽。</summary>
    [DisallowMultipleComponent]
    public sealed class BallastBasket : MonoBehaviour
    {
        [InspectorName("乘员空间")] public BoxCollider passengerVolume;
        [InspectorName("槽内石块中心")] public Transform slot;
        [InspectorName("承接半径（米）"), Range(.5f,.8f)] public float captureRadius = .65f;
        [InspectorName("镇石就位")] public UnityEvent onDocked = new();
        public bool Boarded { get; private set; }
        internal Chapter02BasketMotion PassengerTransport { get; set; }
        public BallastStone Stone { get; private set; }
        Transform player;
        StoneCarrierPost owner;
        CombatRoomEncounter encounter;
        bool requiresClear;
        Rigidbody waitingForRider;
        bool movingPastRider;
        readonly Collider[] riderContacts = new Collider[24];


        internal bool TryBind(StoneCarrierPost source, BallastStone stone, Transform representative, CombatRoomEncounter room)
        {
            if (owner && owner != source) return false;
            if (Stone != stone) ResetBinding(source);
            owner = source;
            Stone = stone;
            player = representative;
            encounter = room;
            requiresClear = room;
            return true;
        }
        internal void ResetBinding(StoneCarrierPost source)
        {
            if (owner && owner != source) return;
            EndCatch();
            if (Stone && Stone.State != BallastStone.Phase.Carried)
            {
                Stone.gameObject.SetActive(false);
                Destroy(Stone.gameObject);
            }
            Stone = null;
            owner = null;
            player = null;
            encounter = null;
            requiresClear = Boarded = false;
        }
        void OnDisable() => EndCatch();

        void EndCatch()
        {
            ReleaseWaitingStone();
            movingPastRider = false;
        }

        void ReleaseWaitingStone()
        {
            if (waitingForRider && Stone && Stone.State == BallastStone.Phase.Dropped)
                waitingForRider.isKinematic = false;
            waitingForRider = null;
        }

        // The actual stone can tumble before docking. Rotate the rider capsule into the stone's
        // *current physics frame* to test the full real collider at either its current or final pose.
        bool TouchesRider(BoxCollider hull, Vector3 bottom, Vector3 top, float radius)
        {
            int count = Physics.OverlapCapsuleNonAlloc(bottom, top, radius, riderContacts,
                1 << hull.gameObject.layer, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
                if (riderContacts[i] == hull) return true;
            return count == riderContacts.Length;
        }

        bool WaitForSlot(BoxCollider hull, Rigidbody body, RpgLocomotionController loc)
        {
            float radius = Mathf.Max(.05f, loc.bodyRadius);
            Vector3 feet = loc.PlayerFloorPosition;
            float lower = radius + Mathf.Max(.005f, loc.skin);
            float height = Mathf.Max(loc.minimumBodyHeight, loc.head.position.y - feet.y + loc.headClearance);
            Vector3 bottom = feet + Vector3.up * lower;
            Vector3 top = feet + Vector3.up * Mathf.Max(lower, height - radius);
            Quaternion socketToStone = body.rotation * Quaternion.Inverse(slot.rotation);
            if (!TouchesRider(hull, body.position + socketToStone * (bottom - slot.position),
                body.position + socketToStone * (top - slot.position), radius)) return false;

            if (TouchesRider(hull, bottom, top, radius))
            {
                // Tracking can move the player into the catch position after the stone has stopped.
                // Leave this approach dynamic until the stone exits the catch or the rider clears the socket.
                movingPastRider = true;
                ReleaseWaitingStone();
            }
            else if (!movingPastRider && !waitingForRider && !body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.isKinematic = true;
                waitingForRider = body;
            }
            return true;
        }

        // 读回已完成运输的安全落点时，从同一工奴配置还原槽内实物；不重发一次性入槽事件。
        internal void RestoreCheckpointStone(BallastStone prefab, bool docked)
        {
            if (!docked)
            {
                if (!owner && Stone) ResetBinding(null);
                return;
            }
            if (Stone) return;
            Stone = Instantiate(prefab, slot.position, slot.rotation);
            Stone.Dock(slot);
            Boarded = true;
        }

        void FixedUpdate()
        {
            if (!Stone || Stone.State != BallastStone.Phase.Dropped || !passengerVolume ||
                !passengerVolume.enabled || !slot || !player || !player.gameObject.activeInHierarchy ||
                (requiresClear && (!encounter || !encounter.isActiveAndEnabled || !encounter.IsComplete)))
            {
                EndCatch();
                return;
            }
            if (!Boarded)
            {
                if (PassengerTransport)
                {
                    if (!PassengerTransport.isActiveAndEnabled || !PassengerTransport.PlayerInA) return;
                }
                else
                {
                    var local = passengerVolume.transform.InverseTransformPoint(player.position) - passengerVolume.center;
                    var half = passengerVolume.size * .5f;
                    if (Mathf.Abs(local.x) > half.x || Mathf.Abs(local.y) > half.y || Mathf.Abs(local.z) > half.z) return;
                }
                Boarded = true;
                Stone.AllowGlyph();
            }
            if (!Stone.MovedByGlyph || (Stone.transform.position - slot.position).sqrMagnitude > captureRadius * captureRadius)
            {
                EndCatch();
                return;
            }

            var loc = encounter && encounter.rpg ? encounter.rpg.locomotion : null;
            if (loc && loc.head && Stone.TryGetComponent<BoxCollider>(out var hull) &&
                Stone.TryGetComponent<Rigidbody>(out var body) && WaitForSlot(hull, body, loc)) return;
            waitingForRider = null;
            movingPastRider = false;
            Stone.Dock(slot);
            onDocked.Invoke();
        }
    }
}
