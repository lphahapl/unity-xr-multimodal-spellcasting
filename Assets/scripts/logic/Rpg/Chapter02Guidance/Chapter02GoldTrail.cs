using UnityEngine;

namespace NarutoVR.SealCombat.Rpg
{
    [DisallowMultipleComponent]
    public sealed class Chapter02GoldTrail : MonoBehaviour, IQuestTrailSource
    {
        public enum Leg { Hidden, BridgeApproach, BridgeToAltar, AltarToA, AExitWest, AExitEast, AToHub,
            HubToWorkshop, PickupTenon, DroppedTenon, ReturnTenon, HubToLeverB, BoardB }

        public LinearRpgController player;
        public Chapter02BridgeEquipment bridge;
        public Chapter02BasketMotion baskets;
        public Chapter02BasketBControl lever;
        public CombatRoomEncounter altar;
        public WorkshopTenonObjective workshop;
        public QuestVisualHints[] legs;
        public Transform droppedFloor, droppedJoin;
        readonly RaycastHit[] hits = new RaycastHit[16];
        public string ActiveLeg => Resolve().ToString();
        public QuestVisualHints CurrentTrail
        {
            get
            {
                var leg = Resolve();
                return leg != Leg.Hidden && legs != null && (int)leg < legs.Length ? legs[(int)leg] : null;
            }
        }

        Leg Resolve()
        {
            if (!isActiveAndEnabled || !player || !player.CanAct || player.CampaignComplete || player.UiInputOwned ||
                !player.locomotion || player.locomotion.EncounterCombat || !bridge || !baskets || !altar ||
                !workshop || !lever || baskets.IsMoving) return Leg.Hidden;

            var stage = player.StageAt(player.StageIndex);
            if (!stage) return Leg.Hidden;
            if (stage == bridge.approachStage) return Leg.BridgeApproach;
            if (stage == bridge.drawingStage)
            {
                var stand = player.CheckpointAt(player.StageIndex);
                if (!stand || bridge.panel.IsWaiting || bridge.drawingObjective.IsComplete ||
                    (player.locomotion.PlayerFloorPosition - stand.floorPosition).sqrMagnitude < 1f) return Leg.Hidden;
                return Leg.BridgeApproach;
            }
            if (stage == bridge.originalFirstStage)
            {
                if (!bridge.repairedDeck.activeInHierarchy || bridge.brokenBarrier.activeInHierarchy) return Leg.Hidden;
                if (baskets.AtMiddle)
                {
                    if (!baskets.PlayerInA) return Leg.AToHub;
                    return player.locomotion.PlayerFloorPosition.x < baskets.ballast.slot.position.x + .4f ?
                        Leg.AExitWest : Leg.AExitEast;
                }
                if (!altar.IsComplete) return Leg.BridgeToAltar;
                return baskets.PlayerInA ? Leg.Hidden : Leg.AltarToA;
            }
            if (stage == workshop.stage)
            {
                if (!workshop.IsRunning || workshop.IsSeated) return Leg.Hidden;
                if (!workshop.IsUnlocked) return Leg.HubToWorkshop;
                if (workshop.tenon.IsHeld) return Leg.ReturnTenon;
                if ((workshop.tenon.transform.position - workshop.holder.position).sqrMagnitude < .25f * .25f) return Leg.PickupTenon;
                return PrepareDroppedRoute() ? Leg.DroppedTenon : Leg.Hidden;
            }
            if (stage.stageId != Chapter02BasketBControl.StageId || !workshop.IsSeated || !baskets.AtMiddle) return Leg.Hidden;
            return lever.DepartureArmed ? Leg.BoardB : Leg.HubToLeverB;
        }

        // Follow the established workshop corridor from either end, then turn toward the actual dropped item.
        // A tenon dropped over the edge has no valid floor until its existing recovery puts it back on the pedestal.
        bool PrepareDroppedRoute()
        {
            var basePath = legs[(int)Leg.PickupTenon].route.points;
            var dynamicRoute = legs[(int)Leg.DroppedTenon].route;
            var item = workshop.tenon.transform;
            int count = Physics.RaycastNonAlloc(item.position + Vector3.up * .20f, Vector3.down, hits, 2.1f,
                player.locomotion.floorLayers, QueryTriggerInteraction.Ignore);
            if (count == hits.Length) return false;
            float nearestHit = float.PositiveInfinity;
            Vector3 floor = default;
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i];
                if (hit.transform.IsChildOf(item) || hit.transform.IsChildOf(player.locomotion.rigRoot) ||
                    hit.normal.y < .82f || Mathf.Abs(hit.point.y - basePath[0].position.y) > .20f ||
                    hit.distance >= nearestHit) continue;
                nearestHit = hit.distance;
                floor = hit.point;
            }
            if (!float.IsFinite(nearestHit)) return false;

            var foot = player.locomotion.PlayerFloorPosition;
            int itemLeg = Project(basePath, floor, out var join, out float itemArc);
            Project(basePath, foot, out var playerJoin, out float playerArc);
            bool offRoute = (floor - join).sqrMagnitude > 36f || (foot - playerJoin).sqrMagnitude > 36f;
            if (offRoute && (foot - floor).sqrMagnitude > 36f) return false;
            droppedFloor.position = floor;
            droppedJoin.position = offRoute ? foot : join;
            if (dynamicRoute.points.Length != basePath.Length + 2)
                dynamicRoute.points = new Transform[basePath.Length + 2];
            var path = dynamicRoute.points;
            int written = 0;
            if (!offRoute)
            {
                if (playerArc <= itemArc)
                {
                    for (int i = 0; i <= itemLeg; i++) path[written++] = basePath[i];
                }
                else
                {
                    for (int i = basePath.Length - 1; i > itemLeg; i--) path[written++] = basePath[i];
                }
            }
            path[written++] = droppedJoin;
            // EndIndex stops at the first destination. Reuse the same buffer when the item changes sides.
            while (written < path.Length) path[written++] = droppedFloor;
            return true;
        }

        static int Project(Transform[] points, Vector3 point, out Vector3 nearest, out float nearestArc)
        {
            nearest = points[0].position;
            nearestArc = 0f;
            float best = float.PositiveInfinity, length = 0f;
            int index = 0;
            for (int i = 0; i + 1 < points.Length; i++)
            {
                var start = points[i].position;
                var delta = points[i + 1].position - start;
                float t = Mathf.Clamp01(Vector3.Dot(point - start, delta) / Mathf.Max(.000001f, delta.sqrMagnitude));
                var projected = start + delta * t;
                float distance = (point - projected).sqrMagnitude;
                if (distance < best)
                {
                    best = distance;
                    nearest = projected;
                    nearestArc = length + t * delta.magnitude;
                    index = i;
                }
                length += delta.magnitude;
            }
            return index;
        }
    }
}
