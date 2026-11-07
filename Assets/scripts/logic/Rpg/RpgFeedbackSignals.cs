using System;
using UnityEngine;

namespace NarutoVR.SealCombat.Rpg
{
    public enum RpgActionCue { NavigationReady, TargetSelected, Blocked, Moved, Dodged, Cancelled, CastReady, Launched, Hit, Shield, Damage, Progress,
        ThrowGripReady, ThrowRejected, DwellNearComplete }

    /// <summary>Presentation listeners cannot interrupt gameplay settlement.</summary>
    public static class RpgFeedbackSignals
    {
        public static void Raise(Action<RpgActionCue> listeners, RpgActionCue cue, UnityEngine.Object context)
        {
            if (listeners == null) return;
            foreach (Action<RpgActionCue> listener in listeners.GetInvocationList())
                try { listener(cue); }
                catch (Exception error) { Debug.LogWarning("操作反馈监听失败，不影响动作结算：" + error.Message, context); }
        }
    }
}
