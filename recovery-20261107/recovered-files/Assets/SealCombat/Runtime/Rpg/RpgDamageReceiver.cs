using NarutoVR.SealCombat.Enemies;
using UnityEngine;

namespace NarutoVR.SealCombat.Rpg
{
    /// <summary>敌人攻击经控制器的暂停/死亡门控，再进入 PlayerObj 伤害管线；不直接绕过游戏状态。</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(LinearRpgController))]
    public sealed class RpgDamageReceiver : CombatDamageReceiver
    {
        LinearRpgController player;
        LinearRpgController Player => player ? player : player = GetComponent<LinearRpgController>();
        public override bool CanReceiveDamage => Player && Player.CanAct && Player.skills && Player.skills.effects;
        public override Transform HitTransform => Player && Player.locomotion && Player.locomotion.head ? Player.locomotion.head : transform;
        public override Vector3 HitPosition => HitTransform.position;
        public override bool TryReceiveDamage(EnemyAttackHit hit) => CanReceiveDamage && Player.ReceiveDamage(hit.AttackId, hit.Damage);
    }
}
