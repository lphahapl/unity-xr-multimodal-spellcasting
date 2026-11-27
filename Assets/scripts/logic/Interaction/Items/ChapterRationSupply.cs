using System;
using NarutoVR.SealCombat.Rpg;
using UnityEngine;

namespace NarutoVR.SealCombat.Items
{
    [DisallowMultipleComponent, DefaultExecutionOrder(365)]
    public sealed partial class ChapterRationSupply : MonoBehaviour, IRpgTravelEquipment
    {
        [SerializeField] internal TravelRationInteraction interaction;
        [SerializeField] internal TravelRationPouch pouchPrefab;
        [SerializeField] internal ToolBeltSlot centralPocket;
        RpgRationCheckpoint pending;

        public bool HasPouch => interaction && interaction.pouch;
        bool Ready => interaction && interaction.sharedBelt && interaction.sharedBelt.Initialised &&
            interaction.sharedBelt.isActiveAndEnabled && interaction.player.IsReady;

        // Optional quest/pickup UnityEvent entry. No chapter grants a pouch by default.
        public void GrantFromReward() => TryGrant();

        public bool TryGrant()
        {
            if (!Ready || HasPouch || pending != null || !pouchPrefab || !centralPocket ||
                centralPocket.SnappedObject || !interaction.player.CanAct || interaction.player.StateCommitInProgress) return false;
            var pouch = Spawn(centralPocket.opening.position, centralPocket.opening.rotation);
            if (pouch.TryDock(centralPocket, Quaternion.Euler(pouch.stowEuler))) return true;
            RemovePouch();
            return false;
        }

        TravelRationPouch Spawn(Vector3 position, Quaternion rotation)
        {
            var pouch = Instantiate(pouchPrefab, position, rotation);
            if (pouch.gameObject.scene != gameObject.scene)
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(pouch.gameObject, gameObject.scene);
            pouch.transform.SetParent(interaction.player.locomotion.transform, true);
            pouch.home = centralPocket;
            interaction.pouch = pouch;
            return pouch;
        }

        public void CaptureEquipment(RpgCheckpointData checkpoint)
        {
            checkpoint.hearthFood = CaptureHearth();
            CaptureOwnedBag(checkpoint);
        }

        void CaptureOwnedBag(RpgCheckpointData checkpoint)
        {
            if (HearthBagOffered && !HearthBagPickedUp) return;
            if (pending != null) { checkpoint.ration = pending; return; }
            if (!HasPouch) return;
            var pouch = interaction.pouch;
            var source = pouch.Dock ? pouch.Dock : pouch.IsHeld ? pouch.PickupSource : null;
            var slot = source as ToolBeltSlot;
            int index = slot ? Array.IndexOf(interaction.sharedBelt.slots, slot) : -1;
            var rotation = pouch.transform.rotation;
            if (index >= 0)
                rotation = pouch.IsHeld ? pouch.PickupRotation : Quaternion.Inverse(slot.opening.rotation) * slot.LandingPose.rotation;
            var position = pouch.transform.position;
            checkpoint.ration = new RpgRationCheckpoint {
                owned = true, spentMask = pouch.ConsumedMask, pocket = index + 1,
                x = position.x, y = position.y, z = position.z,
                qx = rotation.x, qy = rotation.y, qz = rotation.z, qw = rotation.w
            };
        }

        public bool CaptureCarriedEquipment(RpgCheckpointData checkpoint)
        {
            if (pending != null ? pending.pocket <= 0 :
                !HasPouch || (!interaction.pouch.Dock && !interaction.pouch.IsHeld)) return false;
            CaptureOwnedBag(checkpoint);
            var ration = checkpoint.ration;
            if (ration.pocket == 0)
            {
                // A bag picked up off the floor has no previous belt slot.
                ration.pocket = Array.IndexOf(interaction.sharedBelt.slots, centralPocket) + 1;
                if (ration.pocket == 0) throw new InvalidOperationException("所持干粮缺少可携带的袋口");
                var rotation = Quaternion.Euler(interaction.pouch.stowEuler);
                ration.qx = rotation.x; ration.qy = rotation.y; ration.qz = rotation.z; ration.qw = rotation.w;
            }
            return true;
        }

        public void PrepareArrival(RpgCheckpointData checkpoint, Vector3 landing)
        {
            var ration = checkpoint.ration;
            if (!interaction || !interaction.sharedBelt || !pouchPrefab || !centralPocket ||
                ration.pocket < 1 || ration.pocket > interaction.sharedBelt.slots.Length ||
                !(interaction.sharedBelt.slots[ration.pocket - 1] is ToolBeltSlot slot) || !slot.opening)
                throw new InvalidOperationException("目标场景缺少干粮配置或原收纳口");
            ration.x = landing.x; ration.y = landing.y; ration.z = landing.z;
        }

        public void RestoreEquipment(RpgCheckpointData checkpoint)
        {
            RemovePouch();
            RestoreHearth(checkpoint);
            pending = checkpoint.ration != null && checkpoint.ration.owned ? checkpoint.ration : null;
        }

        void LateUpdate()
        {
            RestorePendingBag();
            RefreshHearth();
        }

        void RestorePendingBag()
        {
            if (pending == null || !Ready) return;
            var saved = pending;
            pending = null;
            var rotation = new Quaternion(saved.qx, saved.qy, saved.qz, saved.qw);
            var position = new Vector3(saved.x, saved.y, saved.z);
            var slot = saved.pocket > 0 && saved.pocket <= interaction.sharedBelt.slots.Length
                ? interaction.sharedBelt.slots[saved.pocket - 1] as ToolBeltSlot : null;
            var pouch = Spawn(position, slot && slot.opening ? slot.opening.rotation * rotation : rotation);
            pouch.RestoreContents(saved.spentMask);
            if (slot && slot.opening && !slot.SnappedObject)
            {
                pouch.transform.position = slot.opening.position;
                if (pouch.TryDock(slot, rotation)) return;
                pouch.transform.position = position;
            }
            pouch.transform.SetParent(null, true);
            pouch.DropInWorld();
        }

        void RemovePouch()
        {
            if (!interaction) return;
            interaction.ResetInteraction();
            var pouch = interaction.pouch;
            if (!pouch) return;
            interaction.pouch = null;
            pouch.gameObject.SetActive(false);
            Destroy(pouch.gameObject);
        }

        void OnDestroy()
        {
            RemovePouch();
            RemoveLooseBiscuit();
        }
    }
}
