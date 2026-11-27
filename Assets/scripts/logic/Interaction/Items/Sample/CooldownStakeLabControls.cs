using NarutoVR.SealCombat.Rpg;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NarutoVR.SealCombat.Items
{
    // Desktop controls for this fixture only. Headsets keep the standard right-hand adapter.
    public sealed class CooldownStakeLabControls : ToolBeltInteraction
    {
        public CooldownStake stake;
        bool desktop;
        int target;

        protected override PlayerHandObservation ReadHand(double now)
        {
            var k = Keyboard.current;
            if (k == null) return base.ReadHand(now);
            if (k.f8Key.wasPressedThisFrame) desktop = !desktop;
            if (!desktop || !Initialised) return base.ReadHand(now);
            if (k.digit5Key.wasPressedThisFrame) target = 0;
            if (k.gKey.wasPressedThisFrame) target = 1;
            if (k.kKey.wasPressedThisFrame) target = 2;
            if (k.leftArrowKey.wasPressedThisFrame) player.locomotion.rigRoot.position -= belt.right * 2;
            if (k.rightArrowKey.wasPressedThisFrame) player.locomotion.rigRoot.position += belt.right * 2;
            var at = target == 0 ? ((ToolBeltSlot)stake.home).opening.position : stake.GripPosition;
            if (target == 1)
                at = player.locomotion.PlayerFloorPosition + belt.forward * .55f +
                    Vector3.up * (.025f + stake.grip.localPosition.y - stake.insertionTip.localPosition.y);
            return new PlayerHandObservation { rightValid = true, gripPoseValid = true, pinchValid = true,
                rightObserved = now, gripPosition = at, rightTip = at, gripRotation = Quaternion.identity,
                systemPinch = true, systemPressed = k.spaceKey.isPressed };
        }

        void OnGUI()
        {
            if (!player || !player.IsReady || !stake) return;
            GUILayout.BeginArea(new Rect(16,16,410,260), GUI.skin.box);
            GUILayout.Label("定律铜橛 · 四面纱屏");
            GUILayout.Label(stake.FieldActive ? $"纱屏已展开　余 {stake.SecondsLeft:F1} 秒" : "纱屏已收起");
            GUILayout.Label(desktop ? "5 窄鞘　空格捏取　G 脚前　松开插地\nK 对准铜橛　5 回鞘\n← / → 模拟横移两米" : "右手捏取并在地面松开。F8 桌面模拟");
            GUILayout.EndArea();
        }
    }
}
