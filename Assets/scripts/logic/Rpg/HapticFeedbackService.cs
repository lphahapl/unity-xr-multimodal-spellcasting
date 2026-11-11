using System;
using System.Collections.Generic;
using NarutoVR.SealLab;
using UnityEngine;
using UnityEngine.XR;

namespace NarutoVR.SealCombat.Rpg
{
    public enum ControllerPulse { WristTap, HeldFireball, Launch, ShieldImpact, Dodge }

    public interface IControllerImpulseOutput
    {
        bool TryPulse(bool left, float amplitude, float seconds, out string reason);
        void Stop(bool left);
    }

    /// <summary>Bound and arbitrate impulses independently on each hand. Time is monotonic unscaled seconds.</summary>
    public sealed class ControllerPulseDriver
    {
        const int HoldLimit = 6;
        readonly IControllerImpulseOutput output;
        readonly Func<bool, bool> bareHand;
        readonly double[] last = { double.NegativeInfinity, double.NegativeInfinity };
        readonly double[] stopAt = { double.NegativeInfinity, double.NegativeInfinity };
        readonly bool[] wasHold = new bool[2];
        int heldId, holdAttempts;
        double nextHold, lastClock = double.NegativeInfinity;
        public string Status { get; private set; } = "待命，等待手柄反馈";
        public int SentCount { get; private set; }

        public ControllerPulseDriver(IControllerImpulseOutput output, Func<bool, bool> bareHand)
        { this.output = output ?? throw new ArgumentNullException(nameof(output)); this.bareHand = bareHand; }

        public bool Pulse(ControllerPulse cue, bool left, float strength, double now)
        {
            if (!AcceptTime(now)) return false;
            int hand = left ? 0 : 1;
            if (!float.IsFinite(strength) || strength <= 0) return false;
            bool hold = cue == ControllerPulse.HeldFireball;
            if (now - last[hand] < .08 && (hold || !wasHold[hand])) return false;
            try
            {
                if (bareHand != null && bareHand(left))
                { StopHand(hand); Status = left ? "左手裸手追踪，触觉静默" : "右手裸手追踪，触觉静默"; return false; }
                var (amplitude, duration, label) = cue switch
                {
                    ControllerPulse.WristTap => (.22f, .035f, "点腕"),
                    ControllerPulse.HeldFireball => (.10f, .025f, "持核"),
                    ControllerPulse.Launch => (.48f, .055f, "出手"),
                    ControllerPulse.ShieldImpact => (.58f, .075f, "护盾受击"),
                    ControllerPulse.Dodge => (.20f, .035f, "闪避"),
                    _ => (0f, 0f, "未知反馈")
                };
                if (duration <= 0) return false;
                if (!output.TryPulse(left, amplitude * Mathf.Clamp01(strength), duration, out string reason))
                { StopHand(hand); Status = reason; return false; }
                last[hand] = now; stopAt[hand] = now + duration; wasHold[hand] = hold; SentCount++;
                Status = (left ? "左手柄 " : "右手柄 ") + label;
                return true;
            }
            catch (Exception error) { StopHand(hand); Status = "手柄输出不可用：" + error.Message; return false; }
        }

        public void Tick(double now, int projectileId, bool left, bool canPulseHeld, float strength)
        {
            if (!AcceptTime(now)) return;
            for (int h = 0; h < 2; h++)
            {
                if (!double.IsFinite(stopAt[h])) continue;
                if (now >= stopAt[h] || (bareHand != null && bareHand(h == 0))) StopHand(h);
            }
            if (projectileId != heldId)
            { heldId = projectileId; holdAttempts = 0; nextHold = now + .2; }
            if (heldId == 0 || !canPulseHeld || holdAttempts >= HoldLimit || now < nextHold) return;
            // Count attempts including unsupported hardware. Never catch up bursts after a long frame.
            holdAttempts++; nextHold = now + .35;
            Pulse(ControllerPulse.HeldFireball, left, strength, now);
        }

        bool AcceptTime(double now)
        {
            if (!double.IsFinite(now) || now < lastClock)
            { Reset(); Status = "触觉时钟中断"; return false; }
            lastClock = now;
            return true;
        }

        void StopHand(int hand)
        {
            if (!double.IsFinite(stopAt[hand])) return;
            stopAt[hand] = double.NegativeInfinity;
            try { output.Stop(hand == 0); } catch (Exception) { /* Device may already have disconnected. */ }
        }

        public void Reset()
        {
            StopHand(0); StopHand(1);
            last[0] = last[1] = lastClock = double.NegativeInfinity;
            heldId = holdAttempts = 0; nextHold = 0;
        }
    }

    public sealed class XrControllerImpulseOutput : IControllerImpulseOutput
    {
        readonly List<InputDevice> devices = new(4);
        readonly InputDevice[] owned = new InputDevice[2];

        public static bool Eligible(InputDeviceCharacteristics flags, bool left, bool tracked, bool hasImpulse,
            uint channels, bool gripPressed, float grip)
            => PhysicalController(flags, left) && tracked && hasImpulse && channels > 0 &&
               (gripPressed || (float.IsFinite(grip) && grip >= .12f));

        static bool PhysicalController(InputDeviceCharacteristics flags, bool left)
        {
            var required = InputDeviceCharacteristics.Controller | InputDeviceCharacteristics.HeldInHand |
                (left ? InputDeviceCharacteristics.Left : InputDeviceCharacteristics.Right);
            var excluded = InputDeviceCharacteristics.HandTracking | (left ? InputDeviceCharacteristics.Right : InputDeviceCharacteristics.Left);
            return (flags & required) == required && (flags & excluded) == 0;
        }

        public bool TryPulse(bool left, float amplitude, float seconds, out string reason)
        {
            reason = left ? "未找到左侧物理手柄" : "未找到右侧物理手柄";
            devices.Clear();
            InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Controller |
                (left ? InputDeviceCharacteristics.Left : InputDeviceCharacteristics.Right), devices);
            foreach (var device in devices)
            {
                if (!device.isValid || !PhysicalController(device.characteristics, left)) continue;
                if (!device.TryGetHapticCapabilities(out var caps) || !caps.supportsImpulse || caps.numChannels == 0)
                { reason = "当前手柄运行时未报告可用脉冲通道"; continue; }
                device.TryGetFeatureValue(CommonUsages.isTracked, out bool tracked);
                device.TryGetFeatureValue(CommonUsages.gripButton, out bool pressed);
                device.TryGetFeatureValue(CommonUsages.grip, out float grip);
                if (!Eligible(device.characteristics, left, tracked, caps.supportsImpulse, caps.numChannels, pressed, grip))
                { reason = tracked ? "手柄已连接，等待握把按压" : "手柄已连接，追踪不可用"; continue; }
                int hand = left ? 0 : 1;
                if (owned[hand].isValid && !owned[hand].Equals(device)) Stop(left);
                if (!device.SendHapticImpulse(0, amplitude, seconds)) { reason = "手柄运行时拒绝脉冲"; continue; }
                owned[hand] = device;
                return true;
            }
            return false;
        }

        public void Stop(bool left)
        {
            int hand = left ? 0 : 1;
            var device = owned[hand]; owned[hand] = default;
            if (device.isValid) device.StopHaptics();
        }
    }

    [DisallowMultipleComponent, DefaultExecutionOrder(620)]
    public sealed class HapticFeedbackService : MonoBehaviour
    {
        [Header("可选物理手柄触觉")]
        [InspectorName("启用手柄反馈")] public bool enableControllerHaptics = true;
        [InspectorName("总强度"), Range(0,1)] public float intensity = .7f;
        [InspectorName("同侧裸手追踪时静默")] public bool muteTrackedHands = true;
        [InspectorName("持核间歇微震")] public bool heldPulse = true;
        [InspectorName("闪避成功震动")] public bool dodgePulse;

        public string Status => !isActiveAndEnabled || !enableControllerHaptics || intensity <= 0 ? "手柄触觉关闭" :
            suspended || !focused ? "手柄触觉暂停" : driver?.Status ?? "等待触觉服务";
        public int SentCount => driver?.SentCount ?? 0;
        LinearRpgController owner;
        SkillController effects;
        WristMenuController wrist;
        RpgLocomotionController movement;
        ControllerPulseDriver driver;
        bool suspended, focused = true;
        bool Active => isActiveAndEnabled && enableControllerHaptics && !suspended && focused && intensity > 0 && Time.timeScale > 0;

        void OnEnable()
        {
            owner = GetComponentInParent<LinearRpgController>();
            driver ??= new ControllerPulseDriver(new XrControllerImpulseOutput(), BareHand);
            focused = Application.isFocused; suspended = false;
            Bind();
        }

        void Bind()
        {
            var newEffects = owner && owner.skills ? owner.skills.effects : null;
            var newWrist = owner ? owner.wristMenu : null;
            var newMovement = owner ? owner.locomotion : null;
            if (newEffects != effects)
            {
                if (effects) { effects.ProjectileLaunched -= Launched; effects.ShieldImpact -= ShieldHit; }
                effects = newEffects;
                if (effects) { effects.ProjectileLaunched += Launched; effects.ShieldImpact += ShieldHit; }
            }
            if (newWrist != wrist)
            {
                if (wrist) wrist.WristTapConfirmed -= Tapped;
                wrist = newWrist;
                if (wrist) wrist.WristTapConfirmed += Tapped;
            }
            if (newMovement != movement)
            {
                if (movement) movement.Feedback -= MovementCue;
                movement = newMovement;
                if (movement) movement.Feedback += MovementCue;
            }
        }

        public static bool FreshHand(HandSnapshot hand, double now, double maxAge) => hand != null &&
            hand.connected && hand.tracked && hand.fresh && double.IsFinite(hand.observedAt) && double.IsFinite(now) &&
            now >= hand.observedAt && now - hand.observedAt <= maxAge;

        bool BareHand(bool left)
        {
            var lab = owner && owner.skills ? owner.skills.lab : null;
            var frame = lab ? lab.Frame : null;
            return muteTrackedHands && frame != null && FreshHand(left ? frame.left : frame.right,
                Time.realtimeSinceStartupAsDouble, lab.staleAfterSeconds);
        }

        void LateUpdate()
        {
            Bind();
            if (!Active || !owner || !owner.CanAct) { driver.Reset(); return; }
            driver.Tick(Time.realtimeSinceStartupAsDouble, effects ? effects.HeldProjectileInstanceId : 0,
                effects && effects.ProjectileHandIsLeft, heldPulse && effects && effects.ThrowInputValid, intensity);
        }

        void Send(ControllerPulse cue, bool left)
        {
            if (Active && owner && owner.CanAct) driver.Pulse(cue, left, intensity, Time.realtimeSinceStartupAsDouble);
        }
        void Tapped(bool open) => Send(ControllerPulse.WristTap, true);
        void Launched() => Send(ControllerPulse.Launch, effects.ProjectileHandIsLeft);
        void ShieldHit(float absorbed)
        {
            if (absorbed <= 0) return;
            Send(ControllerPulse.ShieldImpact, true); Send(ControllerPulse.ShieldImpact, false);
        }
        void MovementCue(RpgActionCue cue) { if (dodgePulse && cue == RpgActionCue.Dodged) Send(ControllerPulse.Dodge, false); }
        void OnApplicationPause(bool paused) { suspended = paused; if (paused) driver?.Reset(); }
        void OnApplicationFocus(bool hasFocus) { focused = hasFocus; if (!hasFocus) driver?.Reset(); }
        void OnDisable() { owner = null; Bind(); driver?.Reset(); }
    }
}
