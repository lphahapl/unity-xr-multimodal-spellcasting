using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace NarutoVR.SealCombat
{
    /// <summary>管理一次雷光剑施放：握持与挥动识别、剑波，以及可在收剑后继续消散的视觉。</summary>
    internal sealed class ThunderSwordCast : IReleasableHandEffect
    {
        const float BladeReach = .62f, WaveOrigin = .10f;
        const double MaxSampleGap = .12, MaxSwingWindow = .35;
        readonly SkillEffects owner;
        readonly ThunderSwordEffectDefinition settings;
        readonly string castId = "thunder-sword:" + Guid.NewGuid().ToString("N");
        readonly List<SkillProjectile> waves = new();
        readonly SkillVisualBursts visuals;
        readonly ThunderSwordAimPreview preview;
        readonly ThunderSwordBladeTrail bladeTrail;
        readonly double expires;
        GameObject sword;
        MeleeHitDetector melee;
        SkillStrike strike;
        int swingNumber;
        double previousTime = double.NaN, unavailableSince = double.NaN, anchorTime;
        double nextSwing, closeUntil, armedAt;
        Vector3 previousTip, previousWorldTip, previousWrist, previousHead, anchor, anchorWorldTip, strokeDirection;
        Vector3 anchorHand, anchorWorldHand;
        Quaternion previousRigRotation;
        bool needsRearm, released, cancelled, previousPalm, verticalWave;

        ThunderSwordCast(SkillEffects owner, ThunderSwordEffectDefinition settings, float duration)
        {
            this.owner = owner;
            this.settings = settings;
            visuals = new SkillVisualBursts(owner.gameObject.scene);
            preview = new ThunderSwordAimPreview(owner.gameObject.scene, settings.showWavePreview);
            bladeTrail = new ThunderSwordBladeTrail(owner.gameObject.scene, settings.bladeTrailMaterial, settings.bladeTrailLifetime);
            expires = Now + duration;
        }

        double Now => owner.GameClock?.Invoke() ?? Time.realtimeSinceStartupAsDouble;
        public bool IsAlive => !cancelled && (!released || waves.Count > 0 || visuals.IsAlive);

        public static bool TryStart(SkillEffects owner, ThunderSwordEffectDefinition settings, float duration,
            Pose grip, double stamp, bool hasPalm)
        {
            var cast = new ThunderSwordCast(owner, settings, duration);
            if (!owner.TryRegisterRightHandEffect(cast)) { cast.preview.Dispose(); cast.bladeTrail.Dispose(); return false; }
            var sword = cast.Spawn(settings.swordPrefab, grip.position, grip.rotation);
            if (cast.released || cast.cancelled) { Remove(ref sword); return false; }
            cast.sword = sword;
            cast.melee = cast.sword.GetComponentInChildren<MeleeHitDetector>(true);
            if (cast.melee) cast.melee.HitResolved += cast.OnMeleeHit;
            cast.Sample(grip, stamp, cast.Now, hasPalm);
            owner.SetActivationAudioAnchor(cast.sword.transform);
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
            visuals.Tick(now);
            bladeTrail.Tick(now);
            if (released) return;
            if (now >= expires || !sword || !owner.WeaponAttacker) { ReleaseHand(); return; }
            if (!owner.TryGetSwordGrip(out var grip, out var stamp, out bool hasPalm))
            {
                preview.Hide();
                ResetSamples();
                if (double.IsNaN(unavailableSince)) unavailableSince = now;
                // Tracking loss is not a cancel gesture. Keep the cast until explicit release or expiry.
                // Very brief occlusion holds the last rendered pose; all hit sampling is already stopped.
                if (now - unavailableSince >= .18 && sword.activeSelf) sword.SetActive(false);
                return;
            }
            unavailableSince = double.NaN;
            if (!owner.RightHandActionAllowed)
            {
                if (sword.activeSelf) sword.SetActive(false);
                preview.Hide();
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
            bladeTrail.Clear();
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
                anchorHand = hand;
                anchorWorldHand = gripPose.position;
                anchorTime = stamp;
                armedAt = now + settings.rearmDelay;
            }
            else if (Recognize(tip, worldTip, hand, gripPose.position, rig, stamp, now, (float)dt))
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
            bladeTrail.Sample(melee && melee.bladeBase ? melee.bladeBase.position : grip + rotation * Vector3.forward * .10f,
                melee && melee.bladeTip ? melee.bladeTip.position : worldTip, now);
            Vector3 waveOrigin = grip + rotation * Vector3.forward * WaveOrigin;
            if (owner.TryGetWeaponWavePreview(waveOrigin, settings.waveRadius, out var aim))
                preview.Show(aim, WaveRotation(aim.direction), now >= Math.Max(armedAt, nextSwing));
            else preview.Hide();
            if (now > closeUntil) strike = null;
            if (melee) melee.Sample(strike, owner.WeaponOwnerRoot, owner.WeaponHitMask, owner.playerLayers);
            if (fired && !released && !cancelled)
            {
                owner.PlayAudio(settings.audioProfile, SkillAudioCue.Swing, grip);
                PlaySlash(grip, rotation, rig * strokeDirection, now);
                LaunchWave(waveOrigin);
            }
        }

        bool Recognize(Vector3 tip, Vector3 worldTip, Vector3 hand, Vector3 worldHand,
            Quaternion rig, double stamp, double now, float dt)
        {
            Vector3 step = tip - previousTip;
            bool slow = step.magnitude < settings.minSwingSpeed * .45f * dt;
            bool reversed = needsRearm && Vector3.Dot(step.normalized, strokeDirection) < -.25f;
            if (slow || reversed) needsRearm = false;
            if (slow || reversed || now < Math.Max(armedAt, nextSwing) || stamp - anchorTime > MaxSwingWindow)
            {
                anchor = previousTip;
                anchorWorldTip = previousWorldTip;
                anchorHand = previousWrist;
                anchorWorldHand = previousHead + previousRigRotation * previousWrist;
                anchorTime = previousTime;
            }
            Vector3 handTravel = hand - anchorHand;
            Vector3 horizontal = Vector3.ProjectOnPlane(owner.WeaponAttacker.right, Vector3.up).normalized;
            Vector3 worldTravel = rig * handTravel;
            float x = Mathf.Abs(Vector3.Dot(worldTravel, horizontal)), y = Mathf.Abs(worldTravel.y);
            if (handTravel.magnitude >= settings.minimumHandTravel * .35f)
            {
                if (y > x * settings.directionHysteresis) verticalWave = true;
                else if (x > y * settings.directionHysteresis) verticalWave = false;
            }
            if (slow || needsRearm || now < Math.Max(armedAt, nextSwing)) return false;
            float elapsed = (float)(stamp - anchorTime);
            Vector3 travel = tip - anchor;
            if (elapsed < .06f || travel.magnitude < settings.swingDistance ||
                travel.magnitude < settings.minSwingSpeed * elapsed || Mathf.Max(x,y) < settings.minimumHandTravel) return false;
            // Leaning the head past a stationary blade changes its head-relative position.
            if (Vector3.Dot(worldTip - anchorWorldTip, rig * travel.normalized) < settings.swingDistance) return false;
            if (Vector3.Dot(worldHand - anchorWorldHand, rig * handTravel.normalized) < settings.minimumHandTravel) return false;
            needsRearm = true;
            strokeDirection = travel.normalized;
            return true;
        }

        Quaternion WaveRotation(Vector3 direction)
        {
            Vector3 up = Vector3.ProjectOnPlane(Vector3.up, direction);
            if (up.sqrMagnitude < .001f) up = Vector3.ProjectOnPlane(owner.WeaponAttacker.up, direction);
            if (up.sqrMagnitude < .001f) up = Vector3.ProjectOnPlane(Vector3.forward, direction);
            // Imported arc is in local XY, with its opening toward -Y. Tip it into the
            // travel plane so the concave side faces the caster (-direction), then choose X/Y span.
            return Quaternion.LookRotation(direction, up) * Quaternion.AngleAxis(verticalWave ? 90 : 0, Vector3.forward) *
                Quaternion.AngleAxis(90, Vector3.right);
        }

        /// <summary>让弧光落在剑刃与实际挥动组成的平面；沿剑轴挥动时使用握姿的上方向。</summary>
        void PlaySlash(Vector3 grip, Quaternion rotation, Vector3 travel, double now)
        {
            Vector3 forward = rotation * Vector3.forward;
            Vector3 up = Vector3.Cross(forward, travel);
            if (up.sqrMagnitude < .0001f) up = rotation * Vector3.up;
            visuals.Play(settings.slashPrefab, grip + forward * .36f,
                Quaternion.LookRotation(forward, up), now, settings.slashVisualLifetime);
        }

        /// <summary>近战只在成功直击后生成火花；未命中和重复接触不产生反馈。</summary>
        void OnMeleeHit(SkillHitResult hit)
        {
            if (!released && !cancelled && hit.DirectApplied)
                owner.PlayAudio(settings.audioProfile, SkillAudioCue.Impact, hit.Point);
            if (!released && !cancelled) PlayImpact(hit.Point);
        }

        /// <summary>由已结算的接触通知视觉层；此方法不会再调用伤害逻辑。</summary>
        void PlayImpact(Vector3 point)
        {
            if (!cancelled) visuals.Play(settings.impactPrefab, point, Quaternion.identity,
                Now, settings.impactVisualLifetime);
        }

        void LaunchWave(Vector3 origin)
        {
            if (!owner.TryGetWeaponWaveDirection(origin, out var direction) ||
                !owner.WeaponOriginClear(origin, settings.waveRadius)) return;
            var go = Spawn(settings.wavePrefab, origin, WaveRotation(direction));
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
            wave.SurfaceHit = PlayImpact;
            wave.WorldHit = point => owner.PlayAudio(settings.audioProfile, SkillAudioCue.Impact, point);
            waves.Add(wave);
            owner.PlayAudio(settings.audioProfile, SkillAudioCue.Released, origin);
            wave.Initialize(direction, owner.WeaponOwnerRoot, owner.WeaponHitMask, owner.playerLayers, null, OnWaveFinished);
        }

        void OnWaveFinished(SkillProjectile wave) => waves.Remove(wave);

        void OnWaveHit(SkillHitResult hit)
        {
            if (!cancelled && hit.DirectApplied)
                owner.PlayAudio(settings.audioProfile, SkillAudioCue.Impact, hit.Point);
            if (cancelled || !hit.CanTriggerLightning || !owner.TryApplySwordLightning(hit, settings)) return;
            if (cancelled) return; // Enemy callbacks can synchronously clear the caster.
            owner.PlayAudio(settings.audioProfile, SkillAudioCue.Lightning, hit.Point);
            visuals.Play(settings.lightningPrefab, hit.Point, Quaternion.identity,
                Now, settings.lightningVisualLifetime);
        }

        public void ReleaseHand()
        {
            if (released) return;
            released = true;
            ResetSamples();
            if (melee) melee.HitResolved -= OnMeleeHit;
            preview.Dispose();
            bladeTrail.Dispose();
            if (sword)
            {
                owner.StopHeldAudio(sword.transform);
                if (!cancelled) owner.PlayAudio(settings.audioProfile, SkillAudioCue.Ended, sword.transform.position);
            }
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
            visuals.Clear();
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
