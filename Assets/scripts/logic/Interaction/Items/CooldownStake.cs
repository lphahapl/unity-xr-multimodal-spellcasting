using System;
using NarutoVR.SealCombat.Rpg;
using UnityEngine;

namespace NarutoVR.SealCombat.Items
{
    public sealed class CooldownStake : GameItemBase
    {
        [Header("插地纱屏")]
        [Min(.1f)] public float duration = 12;
        [Min(.1f)] public float radius = 1.5f;
        [InspectorName("纱屏高度（米）"), Min(.1f)] public float screenHeight = 2.3f;
        public Material screenMaterial;
        public Transform gear;
        [Tooltip("以锥尖为原点、半径为一米的贴地刻度。")]
        public Transform dial;
        public bool FieldActive => owner && owner.isActiveAndEnabled && owner.Player == vitals && vitals.Alive &&
            owner.GameTime < end && !IsHeld && !Dock && isActiveAndEnabled;
        internal double FieldTime => owner ? owner.GameTime : 0;
        internal bool Planted { get; private set; }
        internal float SecondsLeft => owner ? (float)Math.Max(0, end - owner.GameTime) : 0;
        public bool InRange { get; private set; }
        StakeScreen screen;
        LinearRpgController owner;
        RpgPlayerState vitals;
        double end, plantedAt;
        Quaternion gearRest;

        protected override void Awake()
        {
            base.Awake();
            if (gear) gearRest = gear.localRotation;
            if (dial) dial.gameObject.SetActive(false);
        }

        internal override bool TryPlaceInWorld(LinearRpgController player)
        {
            if (!player || !player.CanAct || !player.locomotion || !insertionTip || Dock || IsHeld ||
                !float.IsFinite(duration) || duration <= 0 || !float.IsFinite(radius) || radius <= 0 ||
                !float.IsFinite(screenHeight) || screenHeight <= 0 || !screenMaterial || transform.up.y < .7f) return false;
            var origin = insertionTip.position + Vector3.up * .075f;
            var hits = Physics.RaycastAll(origin, Vector3.down, .17f, player.locomotion.floorLayers,
                QueryTriggerInteraction.Ignore);
            var nearest = float.PositiveInfinity;
            RaycastHit hit = default;
            foreach (var candidate in hits)
                if (!candidate.transform.IsChildOf(transform) && candidate.distance < nearest)
                { nearest = candidate.distance; hit = candidate; }
            if (!hit.collider || hit.normal.y < .85f || hit.collider.GetComponentInParent<GameItemBase>() ||
                Mathf.Abs(hit.point.y - player.locomotion.PlayerFloorPosition.y) > .35f) return false;

            transform.rotation = Quaternion.FromToRotation(transform.up, hit.normal) * transform.rotation;
            transform.position += hit.point - insertionTip.position;
            Plant(player, duration);
            return Planted;
        }

        internal override bool MeasureInSlot(Transform opening, Quaternion rotation, out Bounds bounds)
        {
            // A fitted tip envelope describes only the section below the rim.
            if (stowEnvelope && Vector3.Dot(rotation * Vector3.up, opening.up) < .995f)
            { bounds = default; return false; }
            return base.MeasureInSlot(opening, rotation, out bounds);
        }

        internal void Plant(LinearRpgController player, float seconds)
        {
            StopField();
            SetUseSize(insertionTip);
            var body = GetComponent<Rigidbody>();
            body.useGravity = false;
            body.isKinematic = true;
            Planted = true;
            if (seconds <= 0 || !player || !screenMaterial || !insertionTip) return;
            owner = player;
            vitals = player.Player;
            plantedAt = player.GameTime;
            end = plantedAt + Math.Min(seconds, duration);
            owner.CheckpointRestored += StopField;
            if (!screen)
            {
                var child = new GameObject("四面素纱屏");
                child.transform.SetParent(transform, false);
                screen = child.AddComponent<StakeScreen>();
            }
            screen.Open(this, screenMaterial, radius, screenHeight);
            UpdateField();
        }

        protected override void LateUpdate()
        {
            base.LateUpdate();
            if (owner) UpdateField();
        }

        void UpdateField()
        {
            double now = owner.GameTime;
            if (IsHeld || Dock || !owner.isActiveAndEnabled || owner.Player != vitals || !vitals.Alive || now >= end)
            { StopField(); return; }
            if (gear) gear.localRotation = gearRest * Quaternion.Euler(0, (float)((now-plantedAt)*210 % 360), 0);
            var delta = Quaternion.Inverse(transform.rotation) * (owner.locomotion.PlayerFloorPosition - insertionTip.position);
            InRange = Mathf.Abs(delta.y) <= .35f && Mathf.Abs(delta.x) < radius && Mathf.Abs(delta.z) < radius;
            float unfolded = Mathf.SmoothStep(.015f, 1f, Mathf.Clamp01((float)(now-plantedAt)/.48f));
            float opacity = Mathf.Clamp01((float)(end-now)/.20f);
            screen.Present(now, unfolded, opacity);
        }

        void StopField()
        {
            if (owner) owner.CheckpointRestored -= StopField;
            if (screen) screen.gameObject.SetActive(false);
            InRange = false;
            owner = null;
            vitals = null;
            if (dial) dial.gameObject.SetActive(false);
        }

        protected override void OnTaken() { Planted = false; StopField(); }
        protected override void OnDisable() { StopField(); base.OnDisable(); }
    }
}
