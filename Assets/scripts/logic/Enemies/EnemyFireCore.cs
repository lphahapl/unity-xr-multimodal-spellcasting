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
        readonly RaycastHit[] hits = new RaycastHit[16];
        readonly List<CombatDamageReceiver> receivers = new();
        EnemyController shooter;
        EnemyCombatState life;
        string attackId;
        int index;
        float damage, speed, remaining, impactRadius;
        bool finished;

        internal void Launch(EnemyController source, string id, int attackIndex, float amount, float velocity, float distance, float radius)
        {
            shooter = source;
            life = source.Entity.Combat;
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

        void Update()
        {
            if (finished || Time.deltaTime <= 0) return;
            if (!shooter || !shooter.CanReceiveHit || !ReferenceEquals(shooter.Entity.Combat, life))
            { Destroy(gameObject); return; }

            Vector3 start = transform.position, direction = transform.forward;
            float step = Mathf.Min(remaining, speed * Time.deltaTime);
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
            for (int i = 0; i < count; i++)
            {
                var h = buffer[i];
                if (h.distance >= nearest || h.collider.transform.IsChildOf(shooter.transform)) continue;
                var receiver = CombatDamageReceiver.FromCollider(h.collider);
                if (h.collider.isTrigger && !receiver) continue;
                nearest = h.distance;
                direct = receiver;
                floorHit = h.normal.y > .5f;
            }

            // XR rigs can expose a head and a floor point without any body Collider.
            CombatDamageReceiver.CopyActive(receivers);
            foreach (var receiver in receivers)
            {
                if (!receiver.CanReceiveDamage) continue;
                var toHead = receiver.HitPosition - start;
                float along = Vector3.Dot(toHead, direction);
                float radius = collisionRadius + .15f;
                float disc = radius * radius - (toHead - direction * along).sqrMagnitude;
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
            finished = true;
            Destroy(gameObject);
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
