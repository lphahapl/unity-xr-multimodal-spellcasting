using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace NarutoVR.SealCombat
{
    internal sealed class ThunderSwordCast : IReleasableHandEffect
    {
        const float BladeReach = .62f, WaveOrigin = .10f;
        const double MaxSampleGap = .12, LossGrace = .5, MaxSwingWindow = .35;
        readonly SkillEffects owner;
        readonly ThunderSwordEffectDefinition settings;
        readonly string castId = "thunder-sword:" + Guid.NewGuid().ToString("N");
        readonly List<SkillProjectile> waves = new();
        readonly double expires;
        GameObject sword, flash;
        MeleeHitDetector melee;
        SkillStrike strike;
        int swingNumber;
        double previousTime = double.NaN, unavailableSince = double.NaN, anchorTime;
        double nextSwing, closeUntil, flashUntil;
        Vector3 previousTip, previousWorldTip, previousWrist, previousHead, anchor, anchorWorldTip, strokeDirection;
        Quaternion previousRigRotation;
        bool needsRearm, released, cancelled, previousPalm;

        ThunderSwordCast(SkillEffects owner, ThunderSwordEffectDefinition settings, float duration)
        {
            this.owner = owner;
            this.settings = settings;
            expires = Now + duration;
        }

        double Now => owner.GameClock?.Invoke() ?? Time.realtimeSinceStartupAsDouble;
        public bool IsAlive => !cancelled && (!released || waves.Count > 0 || flash);

        public static bool TryStart(SkillEffects owner, ThunderSwordEffectDefinition settings, float duration,
            Pose grip, double stamp, bool hasPalm)
        {
            var cast = new ThunderSwordCast(owner, settings, duration);
            if (!owner.TryRegisterRightHandEffect(cast)) return false;
            var sword = cast.Spawn(settings.swordPrefab, grip.position, grip.rotation);
            if (cast.released || cast.cancelled) { Remove(ref sword); return false; }
            cast.sword = sword;
            cast.melee = cast.sword.GetComponentInChildren<MeleeHitDetector>(true);
            cast.Sample(grip, stamp, cast.Now, hasPalm);
            return true;
        }

        GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            var instance = Object.Instantiate(prefab, position, rotation);
            if (instance.scene != owner.gameObject.scene)
                SceneManager.MoveGameObjectToScene(instance, owner.gameObject.scene);
            instance.SetActive(true);
            return instance;
        }

        public void Tick(double now)
        {
            if (flash && now >= flashUntil) Remove(ref flash);
            if (released) return;
            if (now >= expires || !sword || !owner.WeaponAttacker) { ReleaseHand(); return; }
            if (!owner.TryGetSwordGrip(out var grip, out var stamp, out bool hasPalm))
            {
                if (sword.activeSelf) sword.SetActive(false);
                ResetSamples();
                if (double.IsNaN(unavailableSince)) unavailableSince = now;
                else if (now - unavailableSince >= LossGrace) ReleaseHand();
                return;
            }
            unavailableSince = double.NaN;
            if (!owner.RightHandActionAllowed)
            {
                if (sword.activeSelf) sword.SetActive(false);
                ResetSamples();
                return;
            }
            if (!sword.activeSelf) sword.SetActive(true);
            Sample(grip, stamp, now, hasPalm);
        }

        void ResetSamples()
        {
            previousTime = double.NaN;
            needsRearm = false;
            strike = null;
            if (melee) melee.ResetTracking();
        }

        void Sample(Pose gripPose, double stamp, double now, bool hasPalm)
        {
            var grip = gripPose.position + gripPose.rotation * settings.gripOffset;
            var rotation = gripPose.rotation * Quaternion.Euler(settings.gripEuler);
            sword.transform.SetPositionAndRotation(grip, rotation);
            if (hasPalm != previousPalm) ResetSamples();
            previousPalm = hasPalm;
            if (stamp == previousTime) return;

            // Measure motion relative to the player's position, without using gaze rotation.
            // Room movement and snap turns must never create a swing through world geometry.
            Vector3 head = owner.WeaponAttacker.position;
            var root = owner.WeaponOwnerRoot;
            Quaternion rig = root && root != owner.WeaponAttacker ? root.rotation : Quaternion.identity;
            Quaternion toLocal = Quaternion.Inverse(rig);
            Vector3 worldTip = grip + rotation * Vector3.forward * BladeReach;
            Vector3 tip = toLocal * (worldTip - head);
            Vector3 hand = toLocal * (gripPose.position - head);
            double dt = stamp - previousTime;
            bool gap = !(dt > 0 && dt <= MaxSampleGap) || (hand - previousWrist).sqrMagnitude > .26f * .26f ||
                (tip - previousTip).sqrMagnitude > .5f * .5f || (head - previousHead).sqrMagnitude > .25f * .25f ||
                Quaternion.Angle(previousRigRotation, rig) > 25f;
            bool fired = false;
            if (gap)
            {
                ResetSamples();
                anchor = tip;
                anchorWorldTip = worldTip;
                anchorTime = stamp;
            }
            else if (Recognize(tip, worldTip, rig, stamp, now, (float)dt))
            {
                nextSwing = now + settings.waveInterval;
                closeUntil = now + settings.contactWindow;
                strike = new SkillStrike(castId + ":" + ++swingNumber, settings.directDamage, settings.directPoise, owner.WeaponAttacker);
                fired = true;
            }
            previousTip = tip;
            previousWorldTip = worldTip;
            previousWrist = hand;
            previousHead = head;
            previousRigRotation = rig;
            previousTime = stamp;
            if (now > closeUntil) strike = null;
            if (melee) melee.Sample(strike, owner.WeaponOwnerRoot, owner.WeaponHitMask, owner.playerLayers);
            if (fired && !released && !cancelled) LaunchWave(grip + rotation * Vector3.forward * WaveOrigin);
        }

        bool Recognize(Vector3 tip, Vector3 worldTip, Quaternion rig, double stamp, double now, float dt)
        {
            Vector3 step = tip - previousTip;
            bool slow = step.magnitude < settings.minSwingSpeed * .45f * dt;
            bool reversed = needsRearm && Vector3.Dot(step.normalized, strokeDirection) < -.25f;
            if (slow || reversed) needsRearm = false;
            if (slow || reversed || now < nextSwing || stamp - anchorTime > MaxSwingWindow)
            {
                anchor = previousTip;
                anchorWorldTip = previousWorldTip;
                anchorTime = previousTime;
            }
            if (slow || needsRearm || now < nextSwing) return false;
            float elapsed = (float)(stamp - anchorTime);
            Vector3 travel = tip - anchor;
            if (elapsed < .04f || travel.magnitude < settings.swingDistance ||
                travel.magnitude < settings.minSwingSpeed * elapsed) return false;
            // Leaning the head past a stationary blade changes its head-relative position.
            if (Vector3.Dot(worldTip - anchorWorldTip, rig * travel.normalized) < settings.swingDistance) return false;
            needsRearm = true;
            strokeDirection = travel.normalized;
            return true;
        }

        void LaunchWave(Vector3 origin)
        {
            if (!owner.TryGetWeaponWaveDirection(origin, out var direction) ||
                !owner.WeaponOriginClear(origin, settings.waveRadius)) return;
            var go = Spawn(settings.wavePrefab, origin, Quaternion.LookRotation(direction));
            if (released || cancelled) { Remove(ref go); return; }
            if (!go.TryGetComponent<SkillProjectile>(out var wave)) wave = go.AddComponent<SkillProjectile>();
            wave.speed = settings.waveSpeed;
            wave.radius = settings.waveRadius;
            wave.lifetime = settings.waveLifetime;
            wave.damage = settings.directDamage;
            wave.poiseDamage = settings.directPoise;
            wave.GameClock = owner.GameClock;
            wave.Strike = strike;
            wave.StrikeResolved = OnWaveHit;
            waves.Add(wave);
            wave.Initialize(direction, owner.WeaponOwnerRoot, owner.WeaponHitMask, owner.playerLayers, null, OnWaveFinished);
        }

        void OnWaveFinished(SkillProjectile wave) => waves.Remove(wave);

        void OnWaveHit(SkillHitResult hit)
        {
            if (cancelled || !hit.CanTriggerLightning || !owner.TryApplySwordLightning(hit, settings)) return;
            if (cancelled) return; // Enemy callbacks can synchronously clear the caster.
            Remove(ref flash);
            var bolt = Spawn(settings.lightningPrefab, hit.Point, Quaternion.identity);
            if (cancelled) { Remove(ref bolt); return; }
            flash = bolt;
            flashUntil = Now + .16;
        }

        public void ReleaseHand()
        {
            if (released) return;
            released = true;
            ResetSamples();
            Remove(ref sword);
            owner.ReleaseRightHandEffect(this);
        }

        public void Cancel()
        {
            if (cancelled) return;
            cancelled = true;
            ReleaseHand();
            for (int i = waves.Count - 1; i >= 0; --i)
            {
                var wave = waves[i];
                waves.RemoveAt(i);
                if (wave) wave.Finish();
            }
            Remove(ref flash);
        }

        static void Remove(ref GameObject obj)
        {
            if (!obj) return;
            var doomed = obj;
            obj = null;
            doomed.SetActive(false);
            if (Application.isPlaying) Object.Destroy(doomed); else Object.DestroyImmediate(doomed);
        }
    }
}
