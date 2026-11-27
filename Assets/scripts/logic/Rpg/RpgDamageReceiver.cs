using NarutoVR.SealCombat.Enemies;
using UnityEngine;

namespace NarutoVR.SealCombat.Rpg
{
    /// <summary>敌人攻击经控制器的暂停/死亡门控，再进入 PlayerObj 伤害管线；不直接绕过游戏状态。</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(LinearRpgController))]
    public sealed class RpgDamageReceiver : CombatDamageReceiver
    {
        [Header("本章战斗伤害")]
        [SerializeField, Min(0), InspectorName("敌人对玩家的伤害倍率")] float incomingEnemyDamage = 1f;
        [SerializeField, Min(0), InspectorName("玩家对敌人的伤害倍率")] float outgoingPlayerDamage = 1f;

        internal override float IncomingEnemyDamageMultiplier => incomingEnemyDamage;
        internal override float OutgoingDamageMultiplier => outgoingPlayerDamage;

        LinearRpgController player;
        LinearRpgController Player => player ? player : player = GetComponent<LinearRpgController>();
        public override bool CanReceiveDamage => Player && Player.CanAct && Player.skills && Player.skills.effects;
        public override bool HasFireCoreParry => CanReceiveDamage && Player.skills.effects.CanParryFireCore;
        public override void PresentFireCoreParry() => Player.skills.effects.PresentFireCoreParry();
        public override Transform HitTransform => Player && Player.locomotion && Player.locomotion.head ? Player.locomotion.head : transform;
        public override Transform DetectionColliderRoot => Player && Player.locomotion && Player.locomotion.rigRoot ? Player.locomotion.rigRoot : transform;
        public override Vector3 HitPosition => HitTransform.position;
        // The XR floor point and head share the tracked horizontal position, including room-scale movement.
        public override Vector3 RangedHitPosition => Vector3.Lerp(NavigationPosition, HitPosition, .72f);
        public override Vector3 NavigationPosition => Player && Player.locomotion && Player.locomotion.rigRoot && Player.locomotion.head
            ? Player.locomotion.PlayerFloorPosition : base.NavigationPosition;
        public override bool TryReceiveDamage(EnemyAttackHit hit) => CanReceiveDamage && Player.ReceiveDamage(hit.AttackId, hit.Damage, hit);
    }
}
