using UnityEngine;
using UnityEngine.UI;

namespace NarutoVR.SealCombat
{
    [DisallowMultipleComponent, RequireComponent(typeof(CanvasGroup))]
    public sealed class SealHintCard : MonoBehaviour
    {
        [SerializeField] Text title, instruction;
        [SerializeField] Image illustration;
        [SerializeField] RectTransform progressTrack;
        [SerializeField] Image progressFill;

        internal SkillDefinition Skill { get; private set; }
        internal bool Wanted { get; private set; }
        CanvasGroup group;
        RectTransform rect;
        Vector2 captionPosition, captionSize, slot, fromSlot, displayedSlot;
        double motionStart, slotStart;
        float visible, fromVisible;
        bool hasSlot, animating;
        SkillSealHint previous;

        void Awake()
        {
            group = GetComponent<CanvasGroup>();
            rect = (RectTransform)transform;
            captionPosition = instruction.rectTransform.anchoredPosition;
            captionSize = instruction.rectTransform.sizeDelta;
        }

        internal void Enter(SkillDefinition skill, double now, float delay)
        {
            Skill = skill;
            Wanted = true;
            animating = true;
            fromVisible = visible;
            motionStart = now + (visible == 0 ? delay : 0);
            gameObject.SetActive(true);
        }

        internal void Exit(double now)
        {
            Wanted = false;
            animating = true;
            fromVisible = visible;
            motionStart = now;
            if (visible <= 0) Release();
        }

        internal void Bind(SkillSealHint hint, Vector2 position, Sprite sprite, double now)
        {
            if (!hasSlot || slot != position)
            {
                fromSlot = hasSlot ? displayedSlot : position;
                slot = position;
                slotStart = now;
                hasSlot = true;
                animating = true;
            }
            if (previous.Skill != hint.Skill || previous.Matched != hint.Matched || previous.Total != hint.Total)
            {
                string name = string.IsNullOrWhiteSpace(hint.Skill.displayName) ? hint.Skill.skillId : hint.Skill.displayName;
                title.text = $"{name}【{hint.Matched}/{hint.Total}】";
            }
            if (previous.Skill != hint.Skill || previous.NextSeal != hint.NextSeal || previous.Holding != hint.Holding)
            {
                instruction.text = hint.Holding ? "稳住释放" :
                    hint.NextSeal != null ? "下一印 " + hint.NextSeal : $"念「{hint.Skill.phrase}」";
                illustration.sprite = sprite;
                illustration.enabled = sprite != null;
                instruction.rectTransform.anchoredPosition = sprite ? captionPosition : Vector2.zero;
                instruction.rectTransform.sizeDelta = sprite ? captionSize :
                    new Vector2(captionSize.x, ((RectTransform)instruction.transform.parent).rect.height);
                progressTrack.gameObject.SetActive(hint.Holding);
            }
            if (hint.Holding) progressFill.fillAmount = Mathf.Clamp01(hint.HoldProgress);
            previous = hint;
        }

        internal void Animate(double now, float enterDuration, float exitDuration, float slideDistance)
        {
            if (!animating) return;
            float duration = Wanted ? enterDuration : exitDuration;
            float t = Mathf.Clamp01((float)(now - motionStart) / Mathf.Max(.01f, duration));
            float eased = Wanted ? 1 - (1 - t) * (1 - t) * (1 - t) : t * t;
            visible = Mathf.Lerp(fromVisible, Wanted ? 1 : 0, eased);
            if (!Wanted && t >= 1) { Release(); return; }

            float move = Mathf.Clamp01((float)(now - slotStart) / Mathf.Max(.01f, enterDuration));
            displayedSlot = Vector2.Lerp(fromSlot, slot, Mathf.SmoothStep(0, 1, move));
            rect.anchoredPosition = displayedSlot + Vector2.right * ((1 - visible) * slideDistance);
            rect.localScale = Vector3.one * Mathf.Lerp(.78f, 1, visible);
            group.alpha = visible;
            animating = t < 1 || move < 1;
        }

        internal void Release()
        {
            Skill = null;
            previous = default;
            Wanted = hasSlot = animating = false;
            visible = 0;
            gameObject.SetActive(false);
        }
    }
}
