using System;
using System.Collections.Generic;
using UnityEngine;

namespace NarutoVR.SealCombat.Enemies
{
    /// <summary>纯数据运行态。先原子结算生命/韧性，再由实体通知 Controller 和外部观察者。</summary>
    public sealed class EnemyCombatState
    {
        readonly HashSet<string> received = new(StringComparer.Ordinal);
        readonly float recoveryDelay, recoveryRate, recoveryFraction;
        float recoveryRemaining;
        public float MaxHealth { get; }
        public float MaxPoise { get; }
        public float Health { get; private set; }
        public float Poise { get; private set; }
        public bool Broken { get; private set; }
        public bool Dead => Health <= 0;
        public EnemyCombatState(EnemyCombatDefinition definition)
        {
            if (definition && !definition.Valid) throw new ArgumentException("敌人生命/韧性配置无效");
            MaxHealth = definition ? definition.maxHealth : 150; MaxPoise = definition ? definition.maxPoise : 100;
            recoveryDelay = definition ? definition.recoveryDelay : 4;
            recoveryRate = definition ? definition.recoveryPerSecond : 15;
            recoveryFraction = definition ? definition.recoveryAfterSeated : 1;
            Health = MaxHealth; Poise = MaxPoise;
        }
        public bool Apply(string id, float damage, float poiseDamage)
        {
            if (Dead || string.IsNullOrWhiteSpace(id) || !float.IsFinite(damage) || damage < 0 || damage > 10000 ||
                !float.IsFinite(poiseDamage) || poiseDamage < 0 || poiseDamage > 100000 ||
                (damage == 0 && poiseDamage == 0) || !received.Add(id)) return false;
            Health = Mathf.Max(0, Health - damage);
            if (!Broken && poiseDamage > 0)
            {
                Poise = Mathf.Max(0, Poise - poiseDamage); recoveryRemaining = recoveryDelay;
                if (Poise <= 0) Broken = true;
            }
            return true;
        }
        public void Tick(float dt)
        {
            if (Dead || Broken || !float.IsFinite(dt) || dt <= 0) return;
            float recoveryTime = Mathf.Max(0, dt - recoveryRemaining);
            recoveryRemaining = Mathf.Max(0, recoveryRemaining - dt);
            Poise = Mathf.Min(MaxPoise, Poise + recoveryRate * recoveryTime);
        }
        public void CompleteSeated()
        {
            if (!Broken || Dead) return;
            Broken = false; Poise = MaxPoise * recoveryFraction; recoveryRemaining = recoveryDelay;
        }
    }
}
