using System;
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

        internal void StartTimers()
        {
            foreach (var skill in stake.affectedSkills)
            {
                // Seed the actual cast ledger without equipping four conflicting hand effects.
                if (player.Player.TryReserveCast(Guid.NewGuid().ToString("N"), skill.skillId, skill.cooldownGroup,
                    0, skill.effectDefinition.CooldownOnActivation(skill.cooldownSeconds), player.GameTime,
                    out var cast, out _)) cast.Commit();
            }
        }

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
            if (k.cKey.wasPressedThisFrame && player.CanAct) StartTimers();
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
            GUILayout.Label("定律铜橛 · 冷却试验");
            GUILayout.Label(stake.FieldActive ? (stake.InRange ? "场内 加速" : "场外 常速") : "力场关闭");
            foreach (var skill in stake.affectedSkills)
            {
                var group = string.IsNullOrEmpty(skill.cooldownGroup) ? skill.skillId : skill.cooldownGroup;
                GUILayout.Label($"{skill.displayName}   {player.Player.GetCooldown(group,player.GameTime):F2} 秒");
            }
            GUILayout.Label(desktop ? "5 窄鞘　空格捏取　G 脚前　松开插地\nK 对准铜橛　5 回鞘　C 启动就绪的冷却计时\n← / → 模拟横移两米" : "右手捏取并在地面松开。F8 桌面模拟");
            GUILayout.EndArea();
        }
    }
}
