using System;
using System.Collections.Generic;
using NarutoVR.SealCombat.Events;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NarutoVR.SealCombat.Enemies
{
    public sealed partial class EnemyController
    {
        sealed class DodgeSlow
        {
            public Transform source;
            public float scale, remaining;
            public double endsAt;
        }

        // Keep the authored speed (which need not be 1). Respect an external writer that
        // takes ownership, e.g. a pause or change of movement speed, before restoration.
        struct PlaybackSpeed
        {
            float normal, applied;
            bool owned;

            public float Adjust(float actual, float scale)
            {
                if (!owned || !Mathf.Approximately(actual, applied)) normal = actual;
                return FromNormal(normal, scale);
            }

            public float FromNormal(float speed, float scale)
            {
                normal = speed;
                owned = scale < 1;
                applied = speed * scale;
                return applied;
            }
        }

        IDisposable dodgeLockListener, dodgeSlowListener, dodgeClearListener;
        List<DodgeSlow> dodgeSlows;
        Animator scaledAnimator;
        PlaybackSpeed animationPlayback, movementPlayback;
        public float DodgeTimeScale { get; private set; } = 1;

        void ListenForDodges()
        {
            dodgeLockListener = EventCenter.Subscribe(EnemyDodgeEffects.LockRequested, OnDodgeLock, this);
            dodgeSlowListener = EventCenter.Subscribe(EnemyDodgeEffects.SlowRequested, OnDodgeSlow, this);
            dodgeClearListener = EventCenter.Subscribe(EnemyDodgeEffects.SlowCleared, RemoveDodgeSlow, this);
        }

        void StopListeningForDodges()
        {
            dodgeLockListener?.Dispose();
            dodgeSlowListener?.Dispose();
            dodgeClearListener?.Dispose();
            ResetDodgeTime();
        }

        void OnDodgeLock((Scene scene, Transform head, Transform rig) notice)
        {
            if (notice.scene != gameObject.scene || !CanReceiveHit || !target) return;
            bool matches = target == notice.head || (notice.rig && target.IsChildOf(notice.rig));
            if (!matches && target.TryGetComponent<CombatDamageReceiver>(out var receiver))
                matches = (notice.head && receiver.HitTransform == notice.head) ||
                          (notice.rig && receiver.DetectionColliderRoot == notice.rig);
            if (matches) enemyObj.LockStrikeDirection();
        }

        void OnDodgeSlow((Scene scene, Transform source, float scale, float seconds) notice)
        {
            if (notice.scene != gameObject.scene || !CanReceiveHit || !enemyObj.isActiveAndEnabled) return;
            dodgeSlows ??= new List<DodgeSlow>(1);
            DodgeSlow slow = null;
            foreach (var entry in dodgeSlows)
                if (entry.source == notice.source) { slow = entry; break; }
            if (slow == null)
            {
                slow = new DodgeSlow { source = notice.source };
                dodgeSlows.Add(slow);
            }
            slow.scale = notice.scale;
            slow.remaining = notice.seconds;
            slow.endsAt = Time.unscaledTimeAsDouble + notice.seconds;
            RefreshDodgeRate();
        }

        void RemoveDodgeSlow(Transform source)
        {
            if (dodgeSlows == null || dodgeSlows.Count == 0) return;
            for (int i = dodgeSlows.Count - 1; i >= 0; i--)
                if (dodgeSlows[i].source == source) dodgeSlows.RemoveAt(i);
            RefreshDodgeRate();
        }

        internal void ResetDodgeTime()
        {
            if (dodgeSlows == null || dodgeSlows.Count == 0) return;
            dodgeSlows.Clear();
            RefreshDodgeRate();
        }

        void RefreshDodgeRate()
        {
            float rate = 1;
            foreach (var slow in dodgeSlows) rate = Mathf.Min(rate, slow.scale);
            DodgeTimeScale = rate;
            var current = enemyObj ? enemyObj.Animator : null;
            if (scaledAnimator != current)
            {
                if (scaledAnimator) scaledAnimator.speed = animationPlayback.Adjust(scaledAnimator.speed, 1);
                animationPlayback = default;
            }
            if (current)
            {
                float speed = animationPlayback.Adjust(current.speed, rate);
                if (current.speed != speed) current.speed = speed;
            }
            scaledAnimator = rate < 1 ? current : null;
            if (Agent)
            {
                float speed = movementPlayback.Adjust(Agent.speed, rate);
                if (Agent.speed != speed) Agent.speed = speed;
            }
        }

        float DodgeMovementSpeed(float speed)
        {
            // Mirror EnemyObj's navigation guard: a rejected off-mesh move never writes the native speed.
            return DodgeTimeScale < 1 && Agent && Agent.isActiveAndEnabled && Agent.isOnNavMesh
                ? movementPlayback.FromNormal(speed, DodgeTimeScale) : speed;
        }

        float AdvanceDodgeClock(float deltaTime, bool engineFrame)
        {
            if (dodgeSlows == null || dodgeSlows.Count == 0) return deltaTime;
            if (!engineFrame && (deltaTime <= 0 || Time.timeScale <= 0)) return 0;
            // Unity caps deltaTime on slow frames. The expiry is measured in real seconds,
            // but the enemy may only advance by its share of the game's actual simulation step.
            float frameSeconds = engineFrame ? Time.unscaledDeltaTime : deltaTime / Time.timeScale;
            if (frameSeconds <= 0) return deltaTime * DodgeTimeScale;
            double now = Time.unscaledTimeAsDouble;
            double frameStart = now - frameSeconds;
            float realSeconds = frameSeconds;
            float advanced = 0;
            for (int i = dodgeSlows.Count - 1; i >= 0; i--)
            {
                var entry = dodgeSlows[i];
                if (engineFrame) entry.remaining = (float)(entry.endsAt - frameStart);
                if (!entry.source || entry.remaining <= 0) dodgeSlows.RemoveAt(i);
            }
            RefreshDodgeRate();
            // Split ticks at each expiry so a long frame or a manual Tick cannot skip the full-speed tail.
            while (realSeconds > 0 && dodgeSlows.Count > 0)
            {
                float span = realSeconds;
                foreach (var slow in dodgeSlows) span = Mathf.Min(span, slow.remaining);
                advanced += span * DodgeTimeScale;
                realSeconds -= span;
                int previousCount = dodgeSlows.Count;
                for (int i = dodgeSlows.Count - 1; i >= 0; i--)
                {
                    dodgeSlows[i].remaining -= span;
                    if (dodgeSlows[i].remaining <= 0) dodgeSlows.RemoveAt(i);
                }
                if (previousCount != dodgeSlows.Count) RefreshDodgeRate();
            }
            // Switching an externally stepped actor back to automatic updates keeps the same residual duration.
            if (!engineFrame)
                foreach (var slow in dodgeSlows) slow.endsAt = now + slow.remaining;
            return deltaTime * ((advanced + realSeconds) / frameSeconds);
        }
    }
}
