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
        [InspectorName("近距离忽略墙体（默认关闭）")] public bool ignoreOcclusion;
        readonly HashSet<CombatDamageReceiver> touching = new();
        readonly List<CombatDamageReceiver> candidates = new();
        SphereCollider volume;
        SphereCollider Volume => volume ? volume : volume = GetComponent<SphereCollider>();
        public float WorldRadius
        {
            get { var scale = transform.lossyScale; return Volume.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)); }
        }
        public Vector3 WorldCenter => transform.TransformPoint(Volume.center);

        void Awake() { if (!owner) owner = GetComponentInParent<EnemyObj>(); Volume.isTrigger = true; }
        void OnTriggerEnter(Collider other) => Track(other);
        void OnTriggerStay(Collider other) => Track(other);
        void Track(Collider other)
        {
            var receiver = other.GetComponentInParent<CombatDamageReceiver>();
            if (receiver) touching.Add(receiver);
        }
        // 不按单个 Collider 的 Exit 清目标；玩家另一只手/身体可能仍在圈内。
        void OnDisable() => touching.Clear();
        public bool Contains(Transform target, bool checkOcclusion, float eyeHeight, LayerMask mask)
        {
            if (!isActiveAndEnabled || !Volume.enabled || !owner || !target || !target.gameObject.activeInHierarchy) return false;
            if ((target.position - WorldCenter).sqrMagnitude > WorldRadius * WorldRadius) return false;
            return ignoreOcclusion || owner.HasLineOfSight(target, checkOcclusion, eyeHeight, mask);
        }
        public Transform FindNearby(Transform preferred, bool checkOcclusion, float eyeHeight, LayerMask mask)
        {
            if (!isActiveAndEnabled || !Volume.enabled || !owner) return null;
            if (Contains(preferred, checkOcclusion, eyeHeight, mask)) return preferred;
            // 注册角色基准点判定，不依赖头/手 Collider 数量，也覆盖玩家出生在圈内。
            CombatDamageReceiver.CopyActive(candidates);
            touching.RemoveWhere(r => !r || !r.isActiveAndEnabled);
            foreach (var receiver in touching) if (!candidates.Contains(receiver)) candidates.Add(receiver);
            Transform best = null; float closest = float.PositiveInfinity;
            foreach (var receiver in candidates)
            {
                if (!receiver || !receiver.isActiveAndEnabled || !receiver.CanReceiveDamage ||
                    receiver.transform == owner.transform || receiver.transform.IsChildOf(owner.transform)) continue;
                Transform target = receiver.HitTransform;
                if (!Contains(target, checkOcclusion, eyeHeight, mask)) continue;
                float distance = (target.position - WorldCenter).sqrMagnitude;
                if (distance < closest) { closest = distance; best = target; }
            }
            return best;
        }
        void OnDrawGizmosSelected() { Gizmos.color = Color.cyan; Gizmos.DrawWireSphere(WorldCenter, WorldRadius); }
    }
}
