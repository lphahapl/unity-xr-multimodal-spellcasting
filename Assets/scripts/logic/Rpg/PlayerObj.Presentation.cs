using System;
using NarutoVR.Audio;
using UnityEngine;

namespace NarutoVR.SealCombat.Rpg
{
    public sealed partial class PlayerObj
    {
        LinearRpgController session;
        RpgLocomotionController movement;
        FootstepPlayer footsteps;
        float footstepGain = 1;
        public event Action<Vector3> FootstepPlayed;
        public event Action<PlayerDamageResult> DamageSettled;

        void OnEnable() => BindPlayerPresentation();
        void BindPlayerPresentation()
        {
            if (!isActiveAndEnabled || !IsInitialized || !Application.isPlaying) return;
            session = GetComponent<LinearRpgController>();
            if (!session) return;
            if (!GetComponent<PlayerHitVignette>()) gameObject.AddComponent<PlayerHitVignette>();
            var target = session.chapter && session.chapter.UsesExplorationFootsteps ? session.locomotion : null;
            if (movement == target) return;
            UnbindMovement();
            if (!target) return;
            movement = target;
            footsteps = movement.GetComponent<FootstepPlayer>();
            if (!footsteps) return;
            footsteps.UseReportedTravel(true);
            footsteps.SetGain(footstepGain);
            footsteps.Stepped += ForwardFootstep;
            movement.MovementCommitted += OnMovement;
        }

        void OnMovement(MovementResult result)
        {
            if (!footsteps) return;
            if (result.Kind != PlayerMovementKind.Walk) { footsteps.ResetTracking(); return; }
            float distance = session && session.CanAct && !session.UiInputOwned ?
                new Vector2(result.Delta.x, result.Delta.z).magnitude : 0;
            footsteps.ReportTravel(distance, result.Feet, result.Right);
        }
        void ForwardFootstep(Vector3 feet) => FootstepPlayed?.Invoke(feet);
        internal void SetFootstepGain(float gain)
        {
            footstepGain = gain;
            if (footsteps) footsteps.SetGain(gain);
        }
        void PresentDamage(PlayerDamageResult damage)
        {
            if (DamageSettled == null) return;
            foreach (Action<PlayerDamageResult> listener in DamageSettled.GetInvocationList())
                try { listener(damage); } catch (Exception error) { Debug.LogException(error, this); }
        }
        void OnDisable() => UnbindMovement();
        void UnbindMovement()
        {
            if (movement) movement.MovementCommitted -= OnMovement;
            if (footsteps) { footsteps.Stepped -= ForwardFootstep; footsteps.ResetTracking(); }
            movement = null;
            footsteps = null;
        }
    }
}
