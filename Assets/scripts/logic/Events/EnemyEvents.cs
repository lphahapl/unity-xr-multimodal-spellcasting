using NarutoVR.SealCombat.Enemies;
using UnityEngine;

namespace NarutoVR.SealCombat.Events
{
    public readonly struct EnemyVitalsNotice
    {
        public readonly EnemyObj Enemy;
        public readonly string HitId;
        public readonly float Health, Poise, HealthDamage, PoiseDamage;
        public readonly bool WeakPoint, Broken, Dead;
        public readonly EnemyHitZone HitZone;
        public readonly Transform Attacker;
        public EnemyVitalsNotice(EnemyObj enemy, string hitId, float hp, float poise, float healthDamage, float poiseDamage, bool weak, bool broken, bool dead,
            EnemyHitZone hitZone = null, Transform attacker = null)
        { Enemy = enemy; HitId = hitId; Health = hp; Poise = poise; HealthDamage = healthDamage; PoiseDamage = poiseDamage; WeakPoint = weak; Broken = broken; Dead = dead; HitZone = hitZone; Attacker = attacker; }
    }
    public readonly struct EnemyAttackNotice
    {
        public readonly EnemyController Enemy;
        public readonly int AttackIndex;
        public readonly string AttackId;
        public EnemyAttackNotice(EnemyController enemy, int index, string id)
        { Enemy = enemy; AttackIndex = index; AttackId = id; }
    }
    public readonly struct EnemyDetectionNotice
    {
        public readonly EnemyController Enemy;
        public readonly Transform Target;
        public EnemyDetectionNotice(EnemyController enemy, Transform target) { Enemy = enemy; Target = target; }
    }
    /// <summary>通知已发生的事实，不用广播发起扣血，避免多个订阅者重复造成伤害。</summary>
    public static class EnemyEvents
    {
        public static readonly EventKey<EnemyDetectionNotice> FirstPlayerDetected = new("敌人首次发现玩家");
        public static readonly EventKey<EnemyAttackNotice> AttackStarted = new("敌人开始攻击");
        public static readonly EventKey<EnemyAttackNotice> AttackWindowOpened = new("敌人攻击进入生效窗口");
        public static readonly EventKey<EnemyAttackNotice> AttackFinished = new("敌人攻击结束或取消");
        public static readonly EventKey<EnemyAttackHit> DamageAccepted = new("敌人命中已进入角色伤害管线");
        public static readonly EventKey<EnemyVitalsNotice> VitalsChanged = new("敌人生命或韧性改变");
        public static readonly EventKey<EnemyVitalsNotice> PoiseBroken = new("敌人韧性归零");
        public static readonly EventKey<EnemyVitalsNotice> Died = new("敌人生命归零");
    }
}
