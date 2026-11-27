using NeuGameMRTools.Runtime.Components;
using NarutoVR.SealCombat.Rpg;
using UnityEngine;

namespace NarutoVR.SealCombat.Items
{
    [RequireComponent(typeof(SnapZone), typeof(AudioSource))]
    public sealed class MouthConsumeZone : MonoBehaviour
    {
        [Min(.01f)] public float radius = .085f;
        public HapticFeedbackService haptics;
        readonly XrControllerImpulseOutput controller = new();
        ConsumableItem candidate, finishing;
        RpgPlayerState expectedPlayer;
        float dwell;
        bool mustWithdraw;
        SnapZone snap;
        AudioSource sound;

        void Awake()
        {
            snap = GetComponent<SnapZone>();
            snap.AutomaticSnapping = false;
            sound = GetComponent<AudioSource>();
        }

        // One observation per frame for the actual held item, independent of its number of colliders.
        internal bool Observe(ConsumableItem food, RpgPlayerState player, bool tracked, float delta)
        {
            if (finishing || !food || !food.IsHeld) { ResetContact(); return false; }
            if (candidate != food) { ResetContact(); candidate = food; }
            if (!tracked || delta <= 0f || !food.config) { dwell = 0; return false; }
            var pose = food.BitePose;
            var toMouth = transform.position - pose.position;
            if (toMouth.sqrMagnitude > radius * radius) { dwell = 0; mustWithdraw = false; return false; }
            if (mustWithdraw || !food.CanEat(player) ||
                (food.config.requireFacing && toMouth.sqrMagnitude > .000001f &&
                 Vector3.Angle(pose.rotation * Vector3.forward, toMouth) > food.config.maxFacingAngle))
            { dwell = 0; return false; }
            dwell += delta;
            if (dwell < Mathf.Clamp(food.config.dwellSeconds, .3f, .5f)) return false;
            dwell = 0;
            if (food.Remaining > 1)
            {
                mustWithdraw = food.Eat(player);
                if (mustWithdraw) Feedback(food.config);
                return false;
            }
            // A rejected final snap leaves the item in hand and its last portion available.
            if (!snap.TrySnapObject(food.gameObject)) return false;
            finishing = food;
            expectedPlayer = player;
            return true;
        }

        internal void UpdateFinish(RpgPlayerState player, bool canAct)
        {
            if (!finishing) return;
            if (!canAct || player != expectedPlayer || !finishing.isActiveAndEnabled || snap.SnappedObject != finishing.gameObject)
            { CancelFinish(); return; }
            if (snap.IsSnapping) return;
            var food = finishing;
            CancelFinish();
            if (!food.Eat(player)) return;
            Feedback(food.config);
            food.gameObject.SetActive(false);
        }

        void Feedback(ConsumableItemConfigSO config)
        {
            if (config.biteSound) sound.PlayOneShot(config.biteSound, config.volume);
            if (!haptics || !haptics.isActiveAndEnabled || !haptics.enableControllerHaptics || !Application.isFocused) return;
            var owner = haptics.GetComponentInParent<LinearRpgController>();
            var lab = owner && owner.skills ? owner.skills.lab : null;
            if (haptics.muteTrackedHands && lab && HapticFeedbackService.FreshHand(lab.Frame?.right, Time.realtimeSinceStartupAsDouble, lab.staleAfterSeconds)) return;
            float strength = Mathf.Clamp01(config.hapticStrength * haptics.intensity);
            if (strength > 0f) controller.TryPulse(false, strength, config.hapticSeconds, out _);
        }

        internal void ResetContact() { candidate = null; dwell = 0; mustWithdraw = false; }
        internal void CancelFinish()
        {
            var food = finishing;
            finishing = null;
            expectedPlayer = null;
            if (food && snap && snap.SnappedObject == food.gameObject) snap.ReleaseObject();
            if (food && food.isActiveAndEnabled) food.Drop();
            ResetContact();
        }
        void OnDisable() => CancelFinish();
    }
}
