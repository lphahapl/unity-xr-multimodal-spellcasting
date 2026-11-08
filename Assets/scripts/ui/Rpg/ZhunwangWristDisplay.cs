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

        bool wasVisible;
        float hp, maxHp, mp, maxMp;

        public void SetHealth(float current,float maximum) => health.SetValue(current,maximum);
        public void SetPower(float current,float maximum) => power.SetValue(current,maximum);

        public void Present(RpgPlayerState player,bool tracked,bool recoveryFailed)
        {
            bool visible=tracked && player!=null;
            vitalsAnchor.gameObject.SetActive(visible);
            if (visible)
            {
                if (!wasVisible)
                {
                    health.SetImmediate(player.Health,player.MaxHealth);
                    power.SetImmediate(player.Mana,player.MaxMana);
                }
                else
                {
                    if (player.Health!=hp || player.MaxHealth!=maxHp) SetHealth(player.Health,player.MaxHealth);
                    if (player.Mana!=mp || player.MaxMana!=maxMp) SetPower(player.Mana,player.MaxMana);
                }
                hp=player.Health; maxHp=player.MaxHealth;
                mp=player.Mana; maxMp=player.MaxMana;
                power.gameObject.SetActive(player.Alive);
                recoveryHint.gameObject.SetActive(!player.Alive);
                if (!player.Alive) recoveryHint.text=recoveryFailed ? "恢复失败 · 再按重试" : "按朱砂扣 · 恢复检查点";
            }
            wasVisible=visible;
        }

        void OnDisable() => wasVisible=false;
    }
}
