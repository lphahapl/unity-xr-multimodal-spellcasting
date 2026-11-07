using System.Collections.Generic;
using UnityEngine;

namespace NarutoVR.SealCombat.Enemies
{
    /// <summary>受击角色适配入口。一个角色挂一个接收器，不依赖头/手的 Collider 数量。</summary>
    [DisallowMultipleComponent]
    public abstract class CombatDamageReceiver : MonoBehaviour
    {
        static readonly HashSet<CombatDamageReceiver> Active = new();
        public abstract Vector3 HitPosition { get; }
        public virtual Transform HitTransform => transform;
        public abstract bool CanReceiveDamage { get; }
        public abstract bool TryReceiveDamage(EnemyAttackHit hit);
        protected virtual void OnEnable() => Active.Add(this);
        protected virtual void OnDisable() => Active.Remove(this);
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Active.Clear();
        internal static void CopyActive(List<CombatDamageReceiver> destination)
        { destination.Clear(); foreach (var item in Active) if (item && item.isActiveAndEnabled) destination.Add(item); }
    }

    public readonly struct EnemyAttackHit
    {
        public readonly EnemyObj Source;
        public readonly CombatDamageReceiver Receiver;
        public readonly string AttackId;
        public readonly int AttackIndex;
        /// <summary>送入伤害管线的原始伤害，不是扣除护盾后的 HP 损失。</summary>
        public readonly float Damage;
        public readonly Vector3 Position;
        public EnemyAttackHit(EnemyObj source, CombatDamageReceiver receiver, string id, int index, float damage, Vector3 position)
        { Source = source; Receiver = receiver; AttackId = id; AttackIndex = index; Damage = damage; Position = position; }
    }
}
