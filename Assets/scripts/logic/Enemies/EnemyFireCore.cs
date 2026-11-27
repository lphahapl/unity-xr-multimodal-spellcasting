using System.Collections.Generic;
using NarutoVR.SealCombat.Events;
using UnityEngine;

namespace NarutoVR.SealCombat.Enemies
{
    /// <summary>One straight, dodgeable shot. Resolve the nearest obstruction or character before the marked landing point.</summary>
    public sealed class EnemyFireCore : MonoBehaviour
    {
        [SerializeField, InspectorName("飞行特效配置")] BreathEffectDefinition flightEffect;
        [Min(.01f)] public float collisionRadius = .09f;
        public LayerMask collisionMask = Physics.DefaultRaycastLayers;
        [InspectorName("弹反命中原主伤害"), Min(1)] public float returnDamage = 80;
        [InspectorName("弹反命中硬直（秒）"), Min(0)] public float returnStaggerSeconds = 3;
        readonly RaycastHit[] hits = new RaycastHit[16];
        readonly List<CombatDamageReceiver> receivers = new();
        EnemyController shooter;
        EnemyCombatState life;
        string attackId;
        int index;
        float damage, speed, remaining, impactRadius;
        Vector3 launchTarget;
        CombatDamageReceiver parryingPlayer;
        bool returning, finished;

        internal void Launch(EnemyController source, string id, int attackIndex, float amount, float velocity, float distance, float radius)
        {
            shooter = source;
            life = source.Entity.Combat;
            // Capture the shooter's body at release. A moving shooter can dodge the straight return.
            launchTarget = source.TryGetComponent<CapsuleCollider>(out var body)
                ? body.bounds.center : source.transform.position + Vector3.up * source.Agent.height * .5f;
            attackId = id;
            index = attackIndex;
            damage = amount;
            speed = velocity;
            remaining = distance;
            impactRadius = radius;
            if (flightEffect && flightEffect.visualPrefab)
            {
                var visual = Instantiate(flightEffect.visualPrefab, transform, false);
                visual.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                flightEffect.ConfigureVisual(visual);
            }
        }

        void Update() => Advance(Time.deltaTime);

        internal void Advance(float deltaTime)
        {
            if (finished || deltaTime <= 0) return;
            if (!shooter || !shooter.CanReceiveHit || !ReferenceEquals(shooter.Entity.Combat, life))
            { Finish(); return; }

            Vector3 start = transform.position, direction = transform.forward;
            float step = Mathf.Min(remaining, speed * deltaTime);
            var buffer = hits;
            int count = Physics.SphereCastNonAlloc(start, collisionRadius, direction, hits, step, collisionMask, QueryTriggerInteraction.Collide);
            if (count == hits.Length)
            {
                buffer = Physics.SphereCastAll(start, collisionRadius, direction, step, collisionMask, QueryTriggerInteraction.Collide);
                count = buffer.Length;
            }

            float nearest = float.PositiveInfinity;
            CombatDamageReceiver direct = null;
            bool floorHit = false;
            bool hitShooter = false;
            for (int i = 0; i < count; i++)
            {
                var h = buffer[i];
                bool sourceBody = h.collider.transform.IsChildOf(shooter.transform);
                if (h.distance >= nearest || (!returning && sourceBody)) continue;
                var receiver = CombatDamageReceiver.FromCollider(h.collider);
                if (returning && parryingPlayer && receiver == parryingPlayer) continue;
                if (h.collider.isTrigger && !receiver && !(returning && sourceBody && h.collider.GetComponent<EnemyHitZone>())) continue;
                nearest = h.distance;
                direct = receiver;
                floorHit = h.normal.y > .5f;
                hitShooter = sourceBody;
            }

            // XR rigs can expose a head and a floor point without any body Collider.
            if (!returning) CombatDamageReceiver.CopyActive(receivers);
            foreach (var receiver in receivers)
            {
                if (!receiver.CanReceiveDamage) continue;
                float radius = collisionRadius + .15f;
                Vector3 target = receiver.HitPosition;
                if (receiver.HasFireCoreParry)
                {
                    // Incoming aimed shots terminate at the feet. XR rigs may have no body collider.
                    float height = Mathf.Clamp(target.y - receiver.NavigationPosition.y, .65f, 2.2f);
                    target -= Vector3.up * (height * .5f - .10f);
                    radius = collisionRadius + Mathf.Max(.875f, height * .5f + .225f);
                }
                var offset = target - start;
                float along = Vector3.Dot(offset, direction);
                float disc = radius * radius - (offset - direction * along).sqrMagnitude;
                if (disc < 0) continue;
                float reach = Mathf.Sqrt(disc);
                float contact = Mathf.Max(0, along - reach);
                if (along + reach < 0 || contact > step || contact >= nearest) continue;
                nearest = contact;
                direct = receiver;
                floorHit = false;
            }

            bool collided = nearest <= step;
            transform.position = start + direction * (collided ? nearest : step);
            remaining -= step;
            if (!collided && remaining > .0001f) return;
            if (returning)
            {
                Finish();
                if (hitShooter) shooter.Entity.ReceiveHit(attackId, returnDamage, 0, attacker: parryingPlayer ? parryingPlayer.HitTransform : null,
                    staggerSeconds: returnStaggerSeconds);
                return;
            }
            if (direct && direct.HasFireCoreParry)
            {
                var back = launchTarget - transform.position;
                if (back.sqrMagnitude > .000001f)
                {
                    returning = true;
                    parryingPlayer = direct;
                    receivers.Clear();
                    transform.rotation = Quaternion.LookRotation(back);
                    remaining = back.magnitude + collisionRadius;
                    direct.PresentFireCoreParry();
                    return;
                }
            }
            Finish();
            if (direct) Hit(direct);
            else if (!collided || floorHit)
            {
                Vector3 center = transform.position;
                foreach (var receiver in receivers)
                {
                    if (!receiver || !receiver.CanReceiveDamage || Vector3.Distance(center, receiver.NavigationPosition) > impactRadius) continue;
                    Vector3 end = receiver.NavigationPosition + Vector3.up * .15f;
                    if (Physics.Linecast(center + Vector3.up * .1f, end, out var wall, collisionMask, QueryTriggerInteraction.Ignore)
                        && CombatDamageReceiver.FromCollider(wall.collider) != receiver) continue;
                    Hit(receiver);
                }
            }
        }

        void Finish()
        {
            finished = true;
            Destroy(gameObject);
        }

        void Hit(CombatDamageReceiver receiver)
        {
            var hit = new EnemyAttackHit(shooter.Entity, receiver, attackId, index, damage, transform.position);
            try
            {
                if (receiver.CanReceiveDamage && receiver.TryReceiveDamage(hit)) EventCenter.Publish(EnemyEvents.DamageAccepted, hit);
            }
            catch (System.Exception error)
            {
                Debug.LogWarning("火核命中回调异常：" + error, receiver);
            }
        }
    }
}
