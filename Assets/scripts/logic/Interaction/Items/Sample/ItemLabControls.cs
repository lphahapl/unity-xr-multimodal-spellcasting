using NarutoVR.SealCombat.Rpg;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NarutoVR.SealCombat.Items
{
    // Only the sample scene offers a desktop hand. Production components use the player's tracked hand.
    public sealed class ItemLabControls : ToolBeltInteraction
    {
        bool desktop, seeded, lost;
        HandPickupQuery keyboardPickups;
        int target;
        int pitch, yaw;
        public bool Desktop => desktop;

        // Keyboard observations span frames independently of the missing tracked-hand sample.
        protected override HandPickupQuery RightPickups => desktop ? (keyboardPickups ??= new HandPickupQuery(player)) : base.RightPickups;

        void OnDestroy() => keyboardPickups?.Dispose();

        protected override PlayerHandObservation ReadHand(double now)
        {
            if (!seeded && player.CanAct)
            {
                player.Player.Respawn(20f, 10f, player.GameTime);
                seeded = true;
            }
            var keys = Keyboard.current;
            if (keys == null) return base.ReadHand(now);
            if (keys.f8Key.wasPressedThisFrame) { desktop = !desktop; keyboardPickups?.Reset(); }
            if (!desktop) return base.ReadHand(now);
            if (keys.digit1Key.wasPressedThisFrame) target = 0;
            if (keys.digit2Key.wasPressedThisFrame) target = 1;
            if (keys.digit3Key.wasPressedThisFrame) target = 2;
            if (keys.digit4Key.wasPressedThisFrame) target = 3;
            if (keys.digit5Key.wasPressedThisFrame) target = 4;
            if (keys.kKey.wasPressedThisFrame) target = 5;
            if (keys.mKey.wasPressedThisFrame) target = 6;
            if (keys.gKey.wasPressedThisFrame) target = 7;
            if (keys.nKey.wasPressedThisFrame && Held && Held.PickupSource) target = 8;
            if (keys.rKey.wasPressedThisFrame) pitch = (pitch + 90) % 360;
            if (keys.qKey.wasPressedThisFrame) yaw = (yaw + 90) % 360;
            if (keys.lKey.wasPressedThisFrame) lost = !lost;
            Vector3 position = target switch {
                >= 0 and < 5 => slots[target].GetComponent<ToolBeltSlot>().opening.position,
                5 => items[1].GripPosition,
                6 => mouth.transform.position - mouth.transform.forward * .025f,
                8 when Held && Held.PickupSource => Held.PickupSource.GetComponent<ToolBeltSlot>().opening.position + belt.forward * .14f,
                _ => belt.position + belt.forward * .55f
            };
            return new PlayerHandObservation {
                rightValid = !lost, gripPoseValid = !lost, tipUnavailable = lost, rightObserved = now,
                rightTip = position, gripPosition = position, headPosition = player.locomotion.head.position, gripRotation = (target == 6 ? mouth.transform.rotation :
                    target < 5 ? slots[target].GetComponent<ToolBeltSlot>().opening.rotation : belt.rotation) * Quaternion.Euler(pitch, yaw, 0),
                pinchValid = !lost, systemPinch = true, systemPressed = keys.spaceKey.isPressed
            };
        }

        void OnGUI()
        {
            if (!player || !player.IsReady) return;
            GUI.Box(new Rect(12, Screen.height - 96, 580, 80),
                $"道具试验   气血 {player.Player.Health:F0}   度力 {player.Player.Mana:F0}   余量 {(items[0] as ConsumableItem)?.Remaining}\n" +
                (desktop ? "1 软袋  2 笔套  3 方匣  4 扁袋  5 窄鞘  空格捏取\nR 俯仰  Q 转向  M 嘴边  N 原口附近  G 拉远  K 对准笔  L 失追" :
                 "右手捏取道具，送至嘴边。从嘴边移开后再送回可吃下一口。F8 桌面试验"));
        }
    }
}
