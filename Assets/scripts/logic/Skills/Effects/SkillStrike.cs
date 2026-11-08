using System.Collections.Generic;
using NarutoVR.SealCombat.Enemies;
using UnityEngine;

namespace NarutoVR.SealCombat
{
    public readonly struct SkillHitResult
    {
        public readonly EnemyObj Enemy;
        public readonly SkillTrainingTarget TrainingTarget;
        public readonly Vector3 Point;
        public readonly bool DirectApplied;
        readonly EnemyCombatState contactedLife;

        internal SkillHitResult(SkillHitTarget target, Vector3 point, bool applied, EnemyCombatState life)
        {
            Enemy = target.Enemy;
            TrainingTarget = target.TrainingTarget;
            Point = point;
            DirectApplied = applied;
            contactedLife = life;
        }

        public bool CanTriggerLightning => Enemy && Enemy.isActiveAndEnabled && contactedLife != null &&
            ReferenceEquals(contactedLife, Enemy.Combat) && !contactedLife.Dead &&
            Enemy.TryGetComponent<EnemyController>(out var controller) && controller.CanReceiveHit;
    }

    internal readonly struct SkillHitTarget
    {
        public readonly EnemyHitZone Zone;
        public readonly EnemyObj Enemy;
        public readonly SkillTrainingTarget TrainingTarget;
        public bool IsTarget => Enemy || TrainingTarget;

        public SkillHitTarget(Collider collider)
        {
            Zone = collider.GetComponent<EnemyHitZone>();
            Enemy = Zone && Zone.Owner ? Zone.Owner : collider.GetComponentInParent<EnemyObj>();
            TrainingTarget = Enemy ? null : collider.GetComponentInParent<SkillTrainingTarget>();
        }

        public static bool Ignore(Collider c, Transform owner, Transform playerRoot) =>
            !c || !c.enabled || c.transform.IsChildOf(owner) || (playerRoot && c.transform.IsChildOf(playerRoot)) ||
            (c.isTrigger && (!c.TryGetComponent<EnemyHitZone>(out var zone) || !zone.isActiveAndEnabled || !zone.Owner));

        public bool Apply(string id, float damage, float poiseDamage, Transform attacker) =>
            Enemy ? Enemy.ReceiveHit(id, damage, poiseDamage, Zone, attacker) :
            TrainingTarget && TrainingTarget.ApplyHit(damage);
    }

    /// <summary>One swing, shared by its blade and wave, including when the wave outlives the blade window.</summary>
    public sealed class SkillStrike
    {
        readonly string hitId;
        readonly float damage, poiseDamage;
        internal readonly Transform Attacker;
        readonly HashSet<EnemyCombatState> struckLives = new();
        readonly HashSet<SkillTrainingTarget> struckTargets = new();

        public SkillStrike(string hitId, float damage, float poiseDamage, Transform attacker)
        {
            this.hitId = hitId;
            this.damage = damage;
            this.poiseDamage = poiseDamage;
            Attacker = attacker;
        }

        internal bool AlreadyHit(EnemyObj enemy) => enemy && enemy.Combat != null && struckLives.Contains(enemy.Combat);

        internal SkillHitResult Resolve(SkillHitTarget target, Vector3 point, bool allowLightning = true)
        {
            if (target.Enemy)
            {
                var life = target.Enemy.Combat;
                if (life == null || life.Dead) return new SkillHitResult(target, point, false, null);
                if (struckLives.Contains(life)) return new SkillHitResult(target, point, false, allowLightning ? life : null);
                if (target.Apply(hitId, damage, poiseDamage, Attacker))
                {
                    struckLives.Add(life);
                    return new SkillHitResult(target, point, true, allowLightning ? life : null);
                }
            }
            else if (target.TrainingTarget && struckTargets.Add(target.TrainingTarget))
            {
                if (target.Apply(hitId, damage, poiseDamage, Attacker))
                    return new SkillHitResult(target, point, true, null);
                struckTargets.Remove(target.TrainingTarget);
            }
            return new SkillHitResult(target, point, false, null);
        }
    }
}
