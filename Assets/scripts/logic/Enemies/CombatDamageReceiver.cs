using System.Collections.Generic;
using UnityEngine;

namespace NarutoVR.SealCombat.Enemies
{
    /// <summary>受击角色适配入口。一个角色挂一个接收器，不依赖头/手的 Collider 数量。</summary>
    [DisallowMultipleComponent]
    public abstract class CombatDamageReceiver : MonoBehaviour
    {
        // XR 代表点可能与接收器分属两棵对象树。组件禁用时仍保留身份，防止将其当成旧版裸目标。
        static readonly HashSet<CombatDamageReceiver> Known = new();
        public abstract Vector3 HitPosition { get; }
        public virtual Transform HitTransform => transform;
        public virtual Transform DetectionColliderRoot => transform;
        public virtual Vector3 NavigationPosition => HitTransform.position;
        public abstract bool CanReceiveDamage { get; }
        public abstract bool TryReceiveDamage(EnemyAttackHit hit);
        protected virtual void Awake() => Known.Add(this);
        protected virtual void OnEnable() => Known.Add(this);
        protected virtual void OnDestroy() => Known.Remove(this);
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Known.Clear();
        internal static void CopyActive(List<CombatDamageReceiver> destination)
        { destination.Clear(); foreach (var item in Known) if (item && item.isActiveAndEnabled) destination.Add(item); }

        internal static CombatDamageReceiver FromCollider(Collider collider)
        {
            var attached = collider.GetComponentInParent<CombatDamageReceiver>();
            if (attached) return attached;
            foreach (var receiver in Known)
            {
                if (!receiver || !receiver.isActiveAndEnabled) continue;
                var root = receiver.DetectionColliderRoot;
                if (root && collider.transform.IsChildOf(root)) return receiver;
            }
            return null;
        }

        internal static Vector3 GetNavigationPosition(Transform target)
        {
            foreach (var receiver in Known)
                if (receiver && receiver.isActiveAndEnabled && receiver.HitTransform == target) return receiver.NavigationPosition;
            return target.position;
        }

        internal static bool IsDetectable(Transform target)
        {
            if (!target || !target.gameObject.activeInHierarchy) return false;
            foreach (var receiver in Known)
                if (receiver && receiver.HitTransform == target) return receiver.isActiveAndEnabled && receiver.CanReceiveDamage;
            // 旧场景允许直接指定代表点。挂有接收器的角色仍须遵守其死亡/暂停门控。
            return !target.TryGetComponent<CombatDamageReceiver>(out var attached) ||
                (attached.isActiveAndEnabled && attached.CanReceiveDamage);
        }
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
