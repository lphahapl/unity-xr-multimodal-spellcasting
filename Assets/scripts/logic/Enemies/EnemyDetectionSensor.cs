using System.Collections.Generic;
using UnityEngine;

namespace NarutoVR.SealCombat.Enemies
{
    /// <summary>近距探测 Trigger，与身体碰撞分开。角色注册表补足没有物理身体的 XR 玩家。</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(SphereCollider))]
    public sealed class EnemyDetectionSensor : MonoBehaviour
    {
        [Header("近距离兜底探测")]
        [InspectorName("敌人实体")] public EnemyObj owner;
        readonly List<CombatDamageReceiver> candidates = new();
        SphereCollider volume;
        EnemyController controller;
        SphereCollider Volume => volume ? volume : volume = GetComponent<SphereCollider>();
        public float WorldRadius
        {
            get { var scale = transform.lossyScale; return Volume.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)); }
        }
        public Vector3 WorldCenter => transform.TransformPoint(Volume.center);

        void Awake()
        {
            if (!owner) owner = GetComponentInParent<EnemyObj>();
            if (owner) controller = owner.GetComponent<EnemyController>();
            Volume.isTrigger = true;
        }
        void OnTriggerEnter(Collider other) => DetectContact(other);
        void OnTriggerStay(Collider other) => DetectContact(other);
        void DetectContact(Collider other)
        {
            if (!isActiveAndEnabled || !Volume.enabled || !controller || other.transform.IsChildOf(owner.transform)) return;
            var receiver = CombatDamageReceiver.FromCollider(other);
            if (receiver && receiver.isActiveAndEnabled && receiver.CanReceiveDamage)
                controller.DetectProximity(receiver.HitTransform);
        }
        public bool Contains(Transform target)
        {
            if (!isActiveAndEnabled || !Volume.enabled || !owner || !target || !target.gameObject.activeInHierarchy) return false;
            return (target.position - WorldCenter).sqrMagnitude <= WorldRadius * WorldRadius;
        }
        public Transform FindNearby(Transform preferred)
        {
            if (!isActiveAndEnabled || !Volume.enabled || !owner) return null;
            if (Contains(preferred)) return preferred;
            // 注册角色基准点判定，不依赖头/手 Collider 数量，也覆盖玩家出生在圈内。
            CombatDamageReceiver.CopyActive(candidates);
            Transform best = null; float closest = float.PositiveInfinity;
            foreach (var receiver in candidates)
            {
                if (!receiver || !receiver.isActiveAndEnabled || !receiver.CanReceiveDamage ||
                    receiver.transform == owner.transform || receiver.transform.IsChildOf(owner.transform)) continue;
                Transform target = receiver.HitTransform;
                if (!Contains(target)) continue;
                float distance = (target.position - WorldCenter).sqrMagnitude;
                if (distance < closest) { closest = distance; best = target; }
            }
            return best;
        }
        void OnDrawGizmosSelected() { Gizmos.color = Color.cyan; Gizmos.DrawWireSphere(WorldCenter, WorldRadius); }
    }
}
