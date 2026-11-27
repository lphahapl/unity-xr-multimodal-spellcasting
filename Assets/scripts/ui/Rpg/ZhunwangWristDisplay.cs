using UnityEngine;
using UnityEngine.UI;

namespace NarutoVR.SealCombat.Rpg
{
    public sealed class ZhunwangWristDisplay : MonoBehaviour
    {
        [InspectorName("匣面读数位")] public RectTransform vitalsAnchor;
        [InspectorName("教学面板投影位")] public Transform menuAnchor;
        [InspectorName("气血刻尺")] public WristGauge health;
        [InspectorName("度力刻尺")] public WristGauge power;
        [InspectorName("倒下提示")] public Text recoveryHint;

        bool wasVisible, styled;
        float hp, maxHp, mp, maxMp;

        public void SetHealth(float current, float maximum) => health.SetValue(current, maximum);
        public void SetPower(float current, float maximum) => power.SetValue(current, maximum);

        void EnsureDisplayStyling()
        {
            if (styled) return;
            styled = true;
            if (vitalsAnchor)
            {
                var scaler = vitalsAnchor.GetComponent<CanvasScaler>();
                if (!scaler && vitalsAnchor.GetComponent<Canvas>())
                    scaler = vitalsAnchor.gameObject.AddComponent<CanvasScaler>();
                if (scaler)
                {
                    scaler.dynamicPixelsPerUnit = 3.0f;
                    scaler.referencePixelsPerUnit = 100f;
                }
            }
            if (recoveryHint)
            {
                recoveryHint.color = KaogongTheme.GoldInlay;
                var plaque = KaogongUISprites.Get("Button_Hover");
                if (plaque && (!recoveryHint.transform.parent || recoveryHint.transform.parent.name != "倒下提示牌底"))
                {
                    var bgGo = new GameObject("倒下提示牌底", typeof(RectTransform), typeof(Image));
                    bgGo.layer = recoveryHint.gameObject.layer;
                    var bgRect = (RectTransform)bgGo.transform;
                    bgRect.SetParent(recoveryHint.rectTransform.parent, false);
                    bgRect.SetSiblingIndex(recoveryHint.rectTransform.GetSiblingIndex());
                    bgRect.anchorMin = recoveryHint.rectTransform.anchorMin;
                    bgRect.anchorMax = recoveryHint.rectTransform.anchorMax;
                    bgRect.pivot = recoveryHint.rectTransform.pivot;
                    bgRect.anchoredPosition = recoveryHint.rectTransform.anchoredPosition;
                    bgRect.localRotation = recoveryHint.rectTransform.localRotation;
                    bgRect.localScale = recoveryHint.rectTransform.localScale;
                    bgRect.sizeDelta = recoveryHint.rectTransform.sizeDelta + new Vector2(28f, 18f);
                    var bgImg = bgGo.GetComponent<Image>();
                    bgImg.sprite = plaque;
                    bgImg.type = Image.Type.Sliced;
                    bgImg.pixelsPerUnitMultiplier = 2.0f;
                    bgImg.color = Color.white;
                    bgImg.raycastTarget = false;
                    recoveryHint.rectTransform.SetParent(bgRect, false);
                    recoveryHint.rectTransform.anchorMin = recoveryHint.rectTransform.anchorMax = recoveryHint.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                    recoveryHint.rectTransform.anchoredPosition = Vector2.zero;
                    recoveryHint.rectTransform.localRotation = Quaternion.identity;
                    recoveryHint.rectTransform.localScale = Vector3.one;
                }
            }
        }

        public void Present(IPlayerReadModel player, bool tracked, bool recoveryFailed)
        {
            if (!styled) EnsureDisplayStyling();
            bool visible = tracked && player != null;
            vitalsAnchor.gameObject.SetActive(visible);
            if (visible)
            {
                if (!wasVisible)
                {
                    health.SetImmediate(player.Health, player.MaxHealth);
                    power.SetImmediate(player.Mana, player.MaxMana);
                }
                else
                {
                    if (player.Health != hp || player.MaxHealth != maxHp) SetHealth(player.Health, player.MaxHealth);
                    if (player.Mana != mp || player.MaxMana != maxMp) SetPower(player.Mana, player.MaxMana);
                }
                hp = player.Health; maxHp = player.MaxHealth;
                mp = player.Mana; maxMp = player.MaxMana;
                power.gameObject.SetActive(player.Alive);
                if (recoveryHint)
                {
                    var holder = recoveryHint.transform.parent && recoveryHint.transform.parent.name == "倒下提示牌底"
                        ? recoveryHint.transform.parent.gameObject
                        : recoveryHint.gameObject;
                    holder.SetActive(!player.Alive);
                    if (!player.Alive)
                        recoveryHint.text = recoveryFailed ? "恢复失败 · 再按重试" : "按朱砂扣 · 恢复检查点";
                }
            }
            wasVisible = visible;
        }

        void OnDisable() => wasVisible = false;
    }
}
