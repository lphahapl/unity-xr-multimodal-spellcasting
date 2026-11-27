using System;
using NarutoVR.SealCombat.Rpg;
using UnityEngine;

namespace NarutoVR.SealCombat.Items
{
    public sealed class ConsumableItem : GameItemBase
    {
        public ConsumableItemConfigSO config { get => definition as ConsumableItemConfigSO; set => definition = value; }
        public Transform bitePoint;
        [Tooltip("依次为剩一口、两口、三口以上的外观。")] public GameObject[] stages = new GameObject[3];
        public int Remaining { get; private set; }
        public Pose BitePose => new((bitePoint ? bitePoint : transform).position, (bitePoint ? bitePoint : transform).rotation);

        protected override void Awake()
        {
            base.Awake();
            Remaining = config ? Mathf.Max(1, config.maxUses) : 0;
            RefreshAppearance();
        }

        internal bool CanEat(RpgPlayerState player)
        {
            if (!config || Remaining <= 0 || player == null || !player.Alive || player.CastInProgress || config.effects == null) return false;
            foreach (var effect in config.effects) if (effect && effect.CanApply(player)) return true;
            return false;
        }

        internal bool Eat(RpgPlayerState player)
        {
            if (!CanEat(player)) return false;
            var effects = config.effects;
            for (int i = 0; i < effects.Length; ++i)
                if (effects[i] && Array.IndexOf(effects, effects[i], 0, i) < 0 && effects[i].CanApply(player))
                    effects[i].Apply(player);
            Remaining--;
            RefreshAppearance();
            return true;
        }

        void RefreshAppearance()
        {
            int active = Mathf.Min(Remaining, 3) - 1;
            for (int i = 0; i < stages.Length; i++)
                if (stages[i] && stages[i] != gameObject) stages[i].SetActive(i == active);
        }
    }
}
