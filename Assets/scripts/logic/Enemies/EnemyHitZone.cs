using UnityEngine;

namespace NarutoVR.SealCombat.Enemies
{
    /// <summary>身体/弱点碰撞区。弱点使用 Trigger，不给角色附加物理阻挡，不依赖全局 Tag。</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Collider))]
    public sealed class EnemyHitZone : MonoBehaviour
    {
        [Header("命中区域")]
        [InspectorName("所属敌人")] public EnemyObj owner;
        [InspectorName("是弱点")] public bool isWeakPoint;
        [InspectorName("弱点削韧倍率"), Min(1)] public float poiseMultiplier = 3;
        [Header("弱点额外伤害（默认只提高削韧）")]
        [InspectorName("生命伤害倍率"), Min(1)] public float damageMultiplier = 1;
        [InspectorName("额外固定伤害"), Min(0)] public float bonusDamage;
        public EnemyObj Owner => owner ? owner : GetComponentInParent<EnemyObj>();
        public float PoiseMultiplier => isWeakPoint && isActiveAndEnabled && float.IsFinite(poiseMultiplier)
            ? Mathf.Clamp(poiseMultiplier, 1, 100) : 1;
        public float DamageMultiplier => isWeakPoint && isActiveAndEnabled && float.IsFinite(damageMultiplier)
            ? Mathf.Clamp(damageMultiplier, 1, 100) : 1;
        public float BonusDamage => isWeakPoint && isActiveAndEnabled && float.IsFinite(bonusDamage) ? Mathf.Clamp(bonusDamage, 0, 10000) : 0;
        public bool TryApplyHit(string hitId, float damage, float poiseDamage, Transform attacker = null) => Owner && Owner.ReceiveHit(hitId, damage, poiseDamage, this, attacker);
        void OnDrawGizmosSelected()
        {
            var sphere = GetComponent<SphereCollider>();
            if (!sphere) return;
            Gizmos.color = isWeakPoint ? Color.magenta : Color.cyan;
            var scale = transform.lossyScale;
            Gizmos.DrawWireSphere(transform.TransformPoint(sphere.center), sphere.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
        }
    }
}
