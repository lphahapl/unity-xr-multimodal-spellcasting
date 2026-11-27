using System;
using NarutoVR.SealCombat.Rpg;
using UnityEngine;

namespace NarutoVR.SealCombat.Items
{
    public sealed partial class ChapterRationSupply : IHearthRationNarrationProgress
    {
        [Header("上层清场后的工坊炉台干粮")]
        [SerializeField] internal CombatRoomEncounter hearthEncounter;
        [SerializeField] internal Transform bagSurface, biscuitSurface;
        RpgHearthFoodCheckpoint hearth = new();
        ConsumableItem looseBiscuit;

        bool HasHearth => hearthEncounter && bagSurface && biscuitSurface;
        public bool HearthOffered => HasHearth && (hearth.offered || hearthEncounter.IsComplete);
        public bool IsNearHearth
        {
            get
            {
                if (!HasHearth || !interaction || !interaction.player || !interaction.player.locomotion)
                    return false;
                var delta = interaction.player.locomotion.PlayerFloorPosition - bagSurface.position;
                return Mathf.Abs(delta.y) <= 2f && delta.x * delta.x + delta.z * delta.z <= 3f * 3f;
            }
        }
        public bool HearthBagOffered => hearth.bagFromShelf;
        public bool HearthBagPickedUp => hearth.bagPickedUp ||
            (HearthBagOffered && HasPouch && interaction.pouch.HasBeenTaken);
        public bool HearthBagStowed => hearth.bagStowed || (HearthBagPickedUp && HasPouch &&
            centralPocket && interaction.pouch.LastDock == centralPocket);
        public int HearthBagConsumedMask => HearthBagOffered && HasPouch ? interaction.pouch.ConsumedMask
            : HearthBagOffered && pending != null ? pending.spentMask : 0;
        public bool HearthBiscuitPickedUp => hearth.biscuitPickedUp || (looseBiscuit && looseBiscuit.HasBeenTaken);
        public bool HearthBiscuitEaten => hearth.biscuitEaten || (looseBiscuit && looseBiscuit.Remaining == 0);

        void RefreshHearth()
        {
            if (!HearthOffered || !Ready || !pouchPrefab || !pouchPrefab.servingPrefab) return;
            hearth.offered = true;
            if (!HasPouch && pending == null && !hearth.bagPickedUp)
            {
                var bag = Spawn(bagSurface.position, bagSurface.rotation);
                RestOnSurface(bag, bagSurface);
                hearth.bagFromShelf = true;
            }
            if (!looseBiscuit && !hearth.biscuitEaten)
            {
                looseBiscuit = Instantiate(pouchPrefab.servingPrefab, biscuitSurface.position, biscuitSurface.rotation);
                if (looseBiscuit.gameObject.scene != gameObject.scene)
                    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(looseBiscuit.gameObject, gameObject.scene);
                interaction.sharedBelt.RegisterItem(looseBiscuit);
                if (hearth.biscuitPickedUp) RestoreBiscuitPlacement(hearth.biscuitWorld);
                else RestOnSurface(looseBiscuit, biscuitSurface);
            }
            hearth.bagPickedUp = HearthBagPickedUp;
            hearth.bagStowed = HearthBagStowed;
            hearth.biscuitPickedUp = HearthBiscuitPickedUp;
            hearth.biscuitEaten = HearthBiscuitEaten;
        }

        // All dimensions come from the actual resting collider; prefab pivots are not contact points.
        static void RestOnSurface(GameItemBase item, Transform surface)
        {
            item.transform.SetParent(null, true);
            item.transform.SetPositionAndRotation(surface.position, surface.rotation);
            item.DropInWorld();
            // The forge ledge is seen from standing height. Taking the item restores its normal grip size.
            item.SetUseSize(displayMultiplier: 2.2f);
            var body = item.GetComponent<Rigidbody>();
            body.useGravity = false;
            body.isKinematic = true;
            Physics.SyncTransforms();
            float bottom = float.PositiveInfinity;
            foreach (var shape in item.GetComponentsInChildren<Collider>())
                if (shape.enabled && !shape.isTrigger) bottom = Mathf.Min(bottom, shape.bounds.min.y);
            if (float.IsFinite(bottom)) item.transform.position += Vector3.up * (surface.position.y - bottom);
        }

        RpgHearthFoodCheckpoint CaptureHearth()
        {
            if (!HasHearth || !HearthOffered) return null;
            var result = new RpgHearthFoodCheckpoint {
                offered = true, bagFromShelf = HearthBagOffered, bagPickedUp = HearthBagPickedUp,
                bagStowed = HearthBagStowed, biscuitPickedUp = HearthBiscuitPickedUp, biscuitEaten = HearthBiscuitEaten
            };
            if (result.biscuitPickedUp && !result.biscuitEaten)
            {
                if (!looseBiscuit) result.biscuitWorld = hearth.biscuitWorld;
                else
                {
                    var slot = looseBiscuit.Dock as ToolBeltSlot;
                    int index = slot ? Array.IndexOf(interaction.sharedBelt.slots, slot) : -1;
                    var p = looseBiscuit.transform.position;
                    var q = index >= 0 ? Quaternion.Inverse(slot.opening.rotation) * slot.LandingPose.rotation : looseBiscuit.transform.rotation;
                    result.biscuitWorld = new RpgHearthBiscuitPlacement {
                        pocket = index + 1, x = p.x, y = p.y, z = p.z, qx = q.x, qy = q.y, qz = q.z, qw = q.w };
                }
            }
            return result;
        }

        void RestoreBiscuitPlacement(RpgHearthBiscuitPlacement p)
        {
            var q = new Quaternion(p.qx, p.qy, p.qz, p.qw);
            var belt = interaction.sharedBelt;
            if (p.pocket > 0 && p.pocket <= belt.slots.Length && belt.slots[p.pocket - 1] is ToolBeltSlot slot && slot.opening)
            {
                var world = slot.opening.rotation * q;
                if (!slot.SnappedObject)
                {
                    looseBiscuit.transform.SetPositionAndRotation(slot.opening.position, world);
                    if (looseBiscuit.TryDock(slot, q)) return;
                }
                q = world;
            }
            looseBiscuit.transform.SetPositionAndRotation(new Vector3(p.x, p.y, p.z), q);
            looseBiscuit.DropInWorld();
        }

        void RestoreHearth(RpgCheckpointData checkpoint)
        {
            RemoveLooseBiscuit();
            var state = checkpoint.hearthFood;
            hearth = state == null ? new() : new RpgHearthFoodCheckpoint {
                offered = state.offered, bagFromShelf = state.bagFromShelf, bagPickedUp = state.bagPickedUp,
                bagStowed = state.bagStowed, biscuitPickedUp = state.biscuitPickedUp, biscuitEaten = state.biscuitEaten,
                biscuitWorld = state.biscuitWorld
            };
        }

        void RemoveLooseBiscuit()
        {
            if (!looseBiscuit) return;
            if (interaction && interaction.sharedBelt) interaction.sharedBelt.UnregisterItem(looseBiscuit);
            looseBiscuit.gameObject.SetActive(false);
            Destroy(looseBiscuit.gameObject);
            looseBiscuit = null;
        }
    }
}
