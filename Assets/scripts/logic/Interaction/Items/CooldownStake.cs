using System;
using System.Collections.Generic;
using NarutoVR.SealCombat.Rpg;
using UnityEngine;

namespace NarutoVR.SealCombat.Items
{
    public sealed class CooldownStake : GameItemBase
    {
        [Header("试验参数")]
        [Min(1)] public float recoveryRate = 2;
        [Min(.1f)] public float duration = 12;
        [Min(.1f)] public float radius = 1.5f;
        public SkillDefinition[] affectedSkills = Array.Empty<SkillDefinition>();
        public Transform gear;
        [Tooltip("以锥尖为原点、半径为一米的贴地刻度。")]
        public Transform dial;
        public bool FieldActive => owner;
        internal bool Planted { get; private set; }
        internal float SecondsLeft => owner ? (float)Math.Max(0, end - owner.GameTime) : 0;
        public bool InRange { get; private set; }
        readonly HashSet<string> groups = new(StringComparer.Ordinal);
        LinearRpgController owner;
        RpgPlayerState vitals;
        double end, plantedAt;
        float fieldRadius, fieldRate;
        Quaternion gearRest;

        protected override void Awake()
        {
            base.Awake();
            if (gear) gearRest = gear.localRotation;
            if (dial) dial.gameObject.SetActive(false);
        }

        internal override void ReleasedInWorld(LinearRpgController player)
        {
            if (!player || !player.CanAct || !player.locomotion || !insertionTip || Dock || IsHeld ||
                !float.IsFinite(duration) || duration <= 0 || !float.IsFinite(radius) || radius <= 0 ||
                !float.IsFinite(recoveryRate) || recoveryRate < 1 || affectedSkills == null || transform.up.y < .7f) return;
            var origin = insertionTip.position + Vector3.up * .075f;
            var hits = Physics.RaycastAll(origin, Vector3.down, .17f, player.locomotion.floorLayers,
                QueryTriggerInteraction.Ignore);
            var nearest = float.PositiveInfinity;
            RaycastHit hit = default;
            foreach (var candidate in hits)
                if (!candidate.transform.IsChildOf(transform) && candidate.distance < nearest)
                { nearest = candidate.distance; hit = candidate; }
            if (!hit.collider || hit.normal.y < .85f || hit.collider.GetComponentInParent<GameItemBase>() ||
                Mathf.Abs(hit.point.y - player.locomotion.PlayerFloorPosition.y) > .35f) return;

            transform.rotation = Quaternion.FromToRotation(transform.up, hit.normal) * transform.rotation;
            transform.position += hit.point - insertionTip.position;
            Plant(player, duration);
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
            groups.Clear();
            foreach (var skill in affectedSkills)
                if (skill && RpgDataRules.ValidId(skill.skillId))
                {
                    var group = string.IsNullOrEmpty(skill.cooldownGroup) ? skill.skillId : skill.cooldownGroup;
                    if (RpgDataRules.ValidId(group)) groups.Add(group);
                }
            if (groups.Count == 0) return;
            SetUseSize(insertionTip);
            var body = GetComponent<Rigidbody>();
            body.useGravity = false;
            body.isKinematic = true;
            Planted = true;
            if (seconds <= 0) return;
            owner = player;
            vitals = player.Player;
            plantedAt = player.GameTime;
            end = plantedAt + Math.Min(seconds, duration);
            fieldRadius = radius;
            fieldRate = recoveryRate;
            owner.CheckpointRestored += StopField;
            if (dial)
            {
                dial.position = insertionTip.position + transform.up * .008f;
                dial.localScale = Vector3.one * (fieldRadius / CurrentSize);
                dial.gameObject.SetActive(true);
            }
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
            var delta = owner.locomotion.PlayerFloorPosition - insertionTip.position;
            float height = Vector3.Dot(delta, transform.up);
            bool inside = Mathf.Abs(height) <= .35f && (delta - transform.up * height).sqrMagnitude <= fieldRadius * fieldRadius;
            if (inside != InRange) SetInside(inside);
        }

        void SetInside(bool inside)
        {
            foreach (string group in groups)
                vitals.SetCooldownRecovery(this, group, inside ? fieldRate : 1, owner.GameTime, end);
            InRange = inside;
        }

        void StopField()
        {
            if (owner)
            {
                owner.CheckpointRestored -= StopField;
                if (InRange && owner.Player == vitals) SetInside(false);
            }
            InRange = false;
            owner = null;
            vitals = null;
            if (dial) dial.gameObject.SetActive(false);
        }

        protected override void OnTaken() { Planted = false; StopField(); }
        protected override void OnDisable() { StopField(); base.OnDisable(); }
    }
}
