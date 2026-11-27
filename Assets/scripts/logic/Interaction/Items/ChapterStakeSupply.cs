using System;
using NarutoVR.SealCombat.Rpg;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NarutoVR.SealCombat.Items
{
    [DisallowMultipleComponent, DefaultExecutionOrder(367)]
    public sealed class ChapterStakeSupply : MonoBehaviour, IRpgTravelEquipment
    {
        public ToolBeltInteraction belt;
        public ToolBeltSlot pocket;
        public CooldownStake prefab;
        public Transform worldFallBoundary;
        public CooldownStake Item { get; private set; }
        [SerializeField, InspectorName("本章初始配备铜橛")] bool equippedOnEntry;
        RpgStakeCheckpoint pending;
        bool starterIssued;

        bool Ready => belt && belt.isActiveAndEnabled && belt.Initialised && belt.player &&
            belt.player.gameObject == gameObject && belt.player.IsReady && pocket && pocket.opening &&
            Array.IndexOf(belt.slots, pocket) >= 0 && prefab && prefab.Kind == GameItemKind.CooldownStake;

        public void GrantFromReward() => TryGrant();

        public bool TryGrant()
        {
            if (!Ready || Item || pending != null || !belt.player.CanAct ||
                belt.player.StateCommitInProgress) return false;
            var rotation = Quaternion.Euler(prefab.stowEuler);
            bool Fits(ToolBeltSlot slot) => slot && slot.isActiveAndEnabled && !slot.penHolster && !slot.SnappedObject &&
                slot.opening && slot.FitsEnvelope(prefab, slot.opening.rotation * rotation, out _);
            var destination = Fits(pocket) ? pocket : null;
            if (!destination)
                foreach (var candidate in belt.slots)
                    if (candidate is ToolBeltSlot slot && slot != pocket && Fits(slot)) { destination = slot; break; }
            if (!destination) return false;
            Spawn(destination.opening.position, destination.opening.rotation);
            if (Item.TryDock(destination, rotation))
            {
                starterIssued = true;
                return true;
            }
            Remove();
            return false;
        }

        void Spawn(Vector3 position, Quaternion rotation)
        {
            Item = Instantiate(prefab, position, rotation);
            if (Item.gameObject.scene != gameObject.scene) SceneManager.MoveGameObjectToScene(Item.gameObject,gameObject.scene);
            Item.home = pocket;
            Item.worldFallBoundary = worldFallBoundary;
            belt.RegisterItem(Item);
        }

        public void CaptureEquipment(RpgCheckpointData checkpoint)
        {
            if (!Item) { checkpoint.stake = pending; return; }
            var slot = (Item.Dock ? Item.Dock : Item.IsHeld ? Item.PickupSource : null) as ToolBeltSlot;
            int index = slot ? Array.IndexOf(belt.slots,slot) : -1;
            var r = Item.transform.rotation;
            if (index >= 0) r = Item.IsHeld ? Item.PickupRotation : Quaternion.Inverse(slot.opening.rotation) * slot.LandingPose.rotation;
            var p = Item.transform.position;
            // Older saves stored the compact root; preserve that reference at the planted tip.
            if (Item.Planted && Item.insertionTip)
                p += (Item.insertionTip.position - p) * (1f - 1f / Item.CurrentSize);
            checkpoint.stake = new RpgStakeCheckpoint { owned = true, pocket = index + 1, planted = Item.Planted,
                fieldSeconds = Item.SecondsLeft, x=p.x, y=p.y, z=p.z, qx=r.x, qy=r.y, qz=r.z, qw=r.w };
        }

        public bool CaptureCarriedEquipment(RpgCheckpointData checkpoint)
        {
            if (pending != null ? pending.planted || pending.pocket <= 0 :
                !Item || Item.Planted || (!Item.Dock && !Item.IsHeld)) return false;
            CaptureEquipment(checkpoint);
            var stake = checkpoint.stake;
            if (stake.pocket == 0)
            {
                stake.pocket = Array.IndexOf(belt.slots, pocket) + 1;
                if (stake.pocket == 0) throw new InvalidOperationException("所持铜橛缺少可携带的套口");
                var rotation = Quaternion.Euler(Item.stowEuler);
                stake.qx = rotation.x; stake.qy = rotation.y; stake.qz = rotation.z; stake.qw = rotation.w;
            }
            return true;
        }

        public void PrepareArrival(RpgCheckpointData checkpoint, Vector3 landing)
        {
            var stake = checkpoint.stake;
            if (!belt || !prefab || !pocket || stake.pocket < 1 || stake.pocket > belt.slots.Length ||
                !(belt.slots[stake.pocket - 1] is ToolBeltSlot slot) || !slot.opening)
                throw new InvalidOperationException("目标场景缺少铜橛配置或原收纳口");
            stake.x = landing.x; stake.y = landing.y; stake.z = landing.z;
        }

        public void RestoreEquipment(RpgCheckpointData checkpoint)
        {
            Remove();
            pending = checkpoint.stake?.owned == true ? checkpoint.stake : null;
            starterIssued = pending != null;
        }

        void LateUpdate()
        {
            if (!Ready) return;
            if (pending == null)
            {
                if (equippedOnEntry && !starterIssued && !Item) TryGrant();
                return;
            }
            var s = pending;
            pending = null;
            var pos = new Vector3(s.x,s.y,s.z);
            var rot = new Quaternion(s.qx,s.qy,s.qz,s.qw);
            var slot = s.pocket > 0 && s.pocket <= belt.slots.Length ? belt.slots[s.pocket-1] as ToolBeltSlot : null;
            Spawn(pos, slot && slot.opening ? slot.opening.rotation * rot : rot);
            if (slot && slot.opening && !slot.SnappedObject)
            {
                Item.transform.position = slot.opening.position;
                if (Item.TryDock(slot,rot)) return;
                Item.transform.position = pos;
            }
            if (s.planted) Item.Plant(belt.player,s.fieldSeconds);
            else Item.DropInWorld();
        }

        void Remove()
        {
            if (!Item) return;
            if (belt) belt.UnregisterItem(Item);
            Item.gameObject.SetActive(false);
            Destroy(Item.gameObject);
            Item = null;
        }
        void OnDestroy() => Remove();
    }
}
