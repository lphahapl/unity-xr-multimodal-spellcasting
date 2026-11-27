using UnityEngine;
using UnityEngine.InputSystem;

namespace NarutoVR.SealCombat.Items
{
    public sealed class TravelRationDesktopLab : TravelRationInteraction
    {
        bool desktop, rightHoldsBag, pinchBag, pinchBiscuit, towardMouth, seeded;
        public bool Desktop => desktop;
        protected override bool LocalPickupInput => desktop;

        protected override void LateUpdate()
        {
            if (player && player.CanAct && !seeded)
            {
                player.Player.Respawn(20f, 10f, player.GameTime);
                seeded = true;
            }
            var keys = Keyboard.current;
            if (keys != null)
            {
                if (keys.f9Key.wasPressedThisFrame && BagHand < 0) desktop = !desktop;
                if (desktop)
                {
                    if (!HasServing && towardMouth) { pinchBiscuit = false; towardMouth = false; }
                    if (keys.rKey.wasPressedThisFrame && BagHand < 0) rightHoldsBag = !rightHoldsBag;
                    if (keys.jKey.wasPressedThisFrame) { pinchBag = !pinchBag; pinchBiscuit = false; towardMouth = false; }
                    if (keys.kKey.wasPressedThisFrame && pinchBag) { pinchBiscuit = !pinchBiscuit; towardMouth = false; }
                    if (keys.mKey.wasPressedThisFrame && HasServing) towardMouth = !towardMouth;

                }
            }
            base.LateUpdate();
        }

        protected override PinchSample ReadHand(bool left, double now)
        {
            if (!desktop) return base.ReadHand(left, now);

            // Exactly one hand controls the bag. The other first pinches its displayed biscuit.
            bool bag = left == !rightHoldsBag;
            var rotation = mouth.transform.rotation;
            Vector3 position;
            if (bag)
            {
                position = pouch.GripPosition;
                if (BagHand >= 0)
                    position = pinchBag
                        ? mouth.transform.position + rotation * new Vector3(-.095f, -.13f, .16f)
                        : pouch.home is ToolBeltSlot pocket && pocket.opening ? pocket.opening.position : position;
            }
            else
            {
                position = towardMouth ? mouth.transform.position - mouth.transform.forward * .022f : pouch.NextBiscuitPosition;
            }
            return new PinchSample(position, rotation, (bag ? pinchBag : pinchBiscuit) ? .01f : .07f);
        }

        void OnGUI()
        {
            if (!player || !player.IsReady) return;
            GUI.Box(new Rect(12, 12, 550, 72), desktop
                ? $"干粮包 · 余量 {pouch.InPouch} · 气血 {player.Player.Health:0} · {(rightHoldsBag ? "右" : "左")}手持袋\nR 换手（空手时）   J 取袋／收袋   K 取饼／放回   M 送至嘴边"
                : "行旅麦枣干粮包　F9 开启桌面试食");
        }
    }
}



