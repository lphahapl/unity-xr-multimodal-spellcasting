using System.Collections.Generic;
using UnityEngine;

namespace NarutoVR.SealCombat
{
    [DisallowMultipleComponent]
    public sealed class SealHintPanel : MonoBehaviour
    {
        [SerializeField, InspectorName("候选卡预制体")] SealHintCard cardPrefab;
        [SerializeField, Min(0)] float rowSpacing = 128;
        [SerializeField] float rowOffsetX = 14;
        [SerializeField, Min(0)] float slideDistance = 170;
        [SerializeField, Min(.01f)] float enterDuration = .23f, exitDuration = .16f;
        [SerializeField, Min(0)] float staggerSeconds = .065f;

        readonly List<SealHintCard> cards = new(6);
        readonly Dictionary<string, Sprite> illustrations = new();

        internal void Present(List<SkillSealHint> hints, double now)
        {
            if (hints.Count > 0 && !gameObject.activeSelf) gameObject.SetActive(true);
            int arriving = 0;
            for (int i = 0; i < hints.Count; i++)
            {
                var hint = hints[i];
                var card = FindCard(hint.Skill) ?? Rent();
                if (!card.Wanted)
                    card.Enter(hint.Skill, now, arriving++ * staggerSeconds);
                var slot = new Vector2(-i * rowOffsetX, -i * rowSpacing);
                card.Bind(hint, slot, Illustration(hint.NextSeal), now);
            }
            foreach (var card in cards)
            {
                if (!card.Wanted) continue;
                bool matched = false;
                for (int i = 0; i < hints.Count; i++)
                    if (hints[i].Skill == card.Skill) { matched = true; break; }
                if (!matched) card.Exit(now);
            }
        }

        SealHintCard FindCard(SkillDefinition skill)
        {
            foreach (var card in cards)
                if (card.Skill == skill) return card;
            return null;
        }

        SealHintCard Rent()
        {
            foreach (var card in cards)
                if (!card.gameObject.activeSelf) return card;
            var instance = Instantiate(cardPrefab, transform, false);
            cards.Add(instance);
            return instance;
        }

        Sprite Illustration(string seal)
        {
            if (string.IsNullOrEmpty(seal)) return null;
            if (!illustrations.TryGetValue(seal, out var sprite))
                illustrations[seal] = sprite = Resources.Load<Sprite>("Teaching/Seals/" + seal);
            return sprite;
        }

        internal void Retract(double now)
        {
            foreach (var card in cards)
                if (card.Wanted) card.Exit(now);
        }

        internal void Animate(double now)
        {
            bool any = false;
            foreach (var card in cards)
            {
                if (!card.gameObject.activeSelf) continue;
                card.Animate(now, enterDuration, exitDuration, slideDistance);
                any |= card.gameObject.activeSelf;
            }
            if (!any) gameObject.SetActive(false);
        }

        internal void HideImmediately()
        {
            foreach (var card in cards) card.Release();
            gameObject.SetActive(false);
        }
    }
}
