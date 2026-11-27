using System;
using NarutoVR.SealCombat.Enemies;
using UnityEngine;

namespace NarutoVR.SealCombat.Rpg
{
    /// <summary>同一根衡梁驱动两个吊点，乘员只随篮平移，头部方向由追踪系统提供。</summary>
    [DisallowMultipleComponent]
    public sealed class Chapter02BasketMotion : MonoBehaviour
    {
        const string ReachHub = "CH02_REACH_HUB";
        const string DescendB = "CH02_B_DESCENT";
        const float SecondsA = 8f, SecondsB = 6f;

        public LinearRpgController player;
        public BallastBasket ballast;
        public Transform beam, tipA, tipB, basketB;
        public BoxCollider passengerB;
        public GameObject comfortCurtain;
        public NavigationNode middleCheckpoint;
        public StoneCarrierProfile carrierProfile;

        enum State { Upper, MovingA, Middle, MovingB, Bottom }
        State state;
        Quaternion initialBeam, uprightA, uprightB;
        Vector3 axis, passengerRootOffset;
        float swing, elapsed;
        CanvasGroup curtain;
        bool initialized, restoreStone;
        readonly RaycastHit[] coincidentFloors = new RaycastHit[16];

        public bool IsMoving => state == State.MovingA || state == State.MovingB;
        public bool AtMiddle => state == State.Middle;
        public bool AtBottom => state == State.Bottom;
        public bool PlayerInA => ballast && ContainsPlayer(ballast.passengerVolume, ballast.transform);
        public bool PlayerInB => ContainsPlayer(passengerB, basketB);
        public event Action BArrived;

        void Awake()
        {
            if (!player || !ballast || !ballast.slot || !ballast.passengerVolume || !beam || !tipA || !tipB ||
                !basketB || !passengerB || !middleCheckpoint || !carrierProfile || !carrierProfile.stone ||
                !comfortCurtain || !comfortCurtain.TryGetComponent(out curtain))
            {
                Debug.LogError("二章升降缺少玩家、衡梁吊点、乘员区、静态中层检查点、镇石或定目帘。", this);
                enabled = false;
                return;
            }
            var arm = tipA.position - beam.position;
            axis = Vector3.Cross(arm, Vector3.down).normalized;
            if (axis.sqrMagnitude < .9f || arm.y <= 0 || !tipA.IsChildOf(beam) || !tipB.IsChildOf(beam) ||
                !ballast.transform.IsChildOf(tipA) || !basketB.IsChildOf(tipB))
            {
                Debug.LogError("二章衡梁初始姿态或吊点父级无效。", this);
                enabled = false;
                return;
            }
            initialBeam = beam.rotation;
            uprightA = tipA.rotation;
            uprightB = tipB.rotation;
            swing = 2f * Mathf.Asin(Mathf.Clamp(arm.y / arm.magnitude, -1f, 1f)) * Mathf.Rad2Deg;
            curtain.interactable = curtain.blocksRaycasts = false;
            ballast.PassengerTransport = this;
            SetCurtain(0f);
            initialized = true;
        }

        void OnEnable()
        {
            if (!initialized) return;
            ballast.onDocked.AddListener(TryStartA);
            player.CheckpointRestored += RestoreStation;
            if (player.IsReady) RestoreStation();
        }

        void OnDisable()
        {
            if (!initialized) return;
            if (ballast) ballast.onDocked.RemoveListener(TryStartA);
            if (player)
            {
                player.CheckpointRestored -= RestoreStation;
                if (IsMoving && player.locomotion && player.locomotion.rigRoot && beam && tipA && tipB && ballast && basketB)
                {
                    bool a = state == State.MovingA;
                    MoveWithRider(a ? 0f : 1f, a);
                    state = a ? State.Upper : State.Middle;
                }
                player.Input.EndPassengerTransit(this);
            }
            if (curtain) SetCurtain(0f);
        }

        bool ContainsPlayer(BoxCollider volume, Transform basket)
        {
            if (!basket || !volume || !volume.enabled || !volume.gameObject.activeInHierarchy || !player) return false;
            var loc = player.locomotion;
            if (!loc || !loc.head || !loc.head.gameObject.activeInHierarchy || !loc.rigRoot) return false;
            var feet = loc.PlayerFloorPosition;
            var local = volume.transform.InverseTransformPoint(feet) - volume.center;
            var half = volume.size * .5f;
            var scale = volume.transform.lossyScale;
            float inset = Mathf.Max(.30f, loc.bodyRadius + loc.skin);
            float deckTolerance = Mathf.Max(.06f, 2f * loc.skin);
            if ((half.x - Mathf.Abs(local.x)) * Mathf.Abs(scale.x) < inset ||
                (half.z - Mathf.Abs(local.z)) * Mathf.Abs(scale.z) < inset ||
                Mathf.Abs(local.y + half.y) * Mathf.Abs(scale.y) > deckTolerance) return false;

            // A head or hand reaching across the rail does not establish a rider. The locomotion
            // floor probe follows room-scale and seated tracking, and ignores the player's own body.
            if (!loc.PreviewNavigationOrigin(out var floor) || Mathf.Abs(floor.point.y - feet.y) > deckTolerance)
                return false;
            if (floor.collider.transform.IsChildOf(basket)) return true;

            // At B's valley stop, the valley floor and the deck share the same plane. Raycast order
            // is undefined there. Accept the other support only when it is physically this basket
            // and its surface height matches the chosen floor to within 2 mm.
            const float sameHeight = .002f;
            int count = Physics.RaycastNonAlloc(floor.point + Vector3.up * .02f, Vector3.down,
                coincidentFloors, .02f + sameHeight, loc.floorLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var contact = coincidentFloors[i];
                if (contact.collider.transform.IsChildOf(basket) &&
                    Mathf.Abs(contact.point.y - floor.point.y) <= sameHeight &&
                    Vector3.Angle(contact.normal, Vector3.up) <= loc.maximumSlope) return true;
            }
            return false;
        }

        bool StageActive(string id) => player.CanAct && player.chapter && player.chapter.isActiveAndEnabled &&
            !player.StateCommitInProgress && !player.CampaignComplete &&
            player.StageAt(player.StageIndex).stageId == id;

        void TryStartA()
        {
            if (state != State.Upper || !StageActive(ReachHub) || !PlayerInA || !ballast.Stone ||
                ballast.Stone.State != BallastStone.Phase.Docked || ballast.Stone.transform.parent != ballast.slot) return;
            Begin(State.MovingA);
        }

        public bool TryBeginB() => isActiveAndEnabled && initialized && AtMiddle && StageActive(DescendB) &&
            PlayerInB && Begin(State.MovingB);

        bool Begin(State travel)
        {
            if (!player.locomotion.rigRoot || player.UiInputOwned || !player.Input.TryBeginPassengerTransit(this)) return false;
            player.locomotion.ResetInput();
            if (player.skills) player.skills.CancelInputOnly();
            var basket = travel == State.MovingA ? ballast.transform : basketB;
            passengerRootOffset = player.locomotion.rigRoot.position - basket.position;
            state = travel;
            elapsed = 0f;
            return true;
        }

        void Update()
        {
            // onDocked fires only once. A rider who stepped out before the stone settled may board later.
            if (state == State.Upper && ballast.Stone && ballast.Stone.State == BallastStone.Phase.Docked)
                TryStartA();

            if (AtMiddle && StageActive(ReachHub) && player.CurrentStageCheckpoint != middleCheckpoint)
                player.TryActivateStageCheckpoint(middleCheckpoint);

            if (!IsMoving || !player.CanAct || player.StateCommitInProgress) return;
            bool a = state == State.MovingA;
            float duration = a ? SecondsA : SecondsB;
            elapsed = Mathf.Min(elapsed + Time.deltaTime, duration);
            float t = elapsed / duration;
            float fraction = Mathf.SmoothStep(0f, 1f, t);
            SetCurtain(Mathf.SmoothStep(0f, 1f, Mathf.Min(t, 1f - t) / .18f));
            MoveWithRider(a ? fraction : 1f - fraction, a);
            if (elapsed < duration) return;
            state = a ? State.Middle : State.Bottom;
            SetCurtain(0f);
            player.Input.EndPassengerTransit(this);
            Physics.SyncTransforms();
            if (a) player.TryActivateStageCheckpoint(middleCheckpoint);
            else BArrived?.Invoke();
        }

        void SetCurtain(float strength)
        {
            curtain.alpha = strength;
            if (comfortCurtain.activeSelf != (strength > 0f))
                comfortCurtain.SetActive(strength > 0f);
        }

        void MoveWithRider(float fraction, bool rideA)
        {
            var basket = rideA ? ballast.transform : basketB;
            // Keep the boarding offset in world space while head and hands remain free in tracking space.
            SetPose(fraction);
            player.locomotion.rigRoot.position = basket.position + passengerRootOffset;
        }

        void SetPose(float fraction)
        {
            beam.rotation = Quaternion.AngleAxis(fraction * swing, axis) * initialBeam;
            tipA.rotation = uprightA;
            tipB.rotation = uprightB;
        }

        void RestoreStation()
        {
            player.Input.EndPassengerTransit(this);
            SetCurtain(0f);
            bool bottom = player.Model.Chapter.IsComplete(DescendB) &&
                player.CurrentStageCheckpoint && player.CurrentStageCheckpoint.nodeId == "CH02_B_VALLEY";
            bool middle = player.Model.Chapter.IsComplete(ReachHub) || player.CurrentStageCheckpoint == middleCheckpoint;
            state = bottom ? State.Bottom : middle ? State.Middle : State.Upper;
            SetPose(AtMiddle ? 1f : 0f);
            Physics.SyncTransforms();
            restoreStone = true;
        }

        void LateUpdate()
        {
            // Room checkpoint listeners release the old run's enemy and stone before this reconstruction.
            if (!restoreStone) return;
            restoreStone = false;
            ballast.RestoreCheckpointStone(carrierProfile.stone, AtMiddle || AtBottom);
        }
    }
}
