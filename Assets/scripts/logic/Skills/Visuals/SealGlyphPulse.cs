using UnityEngine;

namespace NarutoVR.SealCombat
{
    [DisallowMultipleComponent, RequireComponent(typeof(SpriteRenderer))]
    public sealed class SealGlyphPulse : MonoBehaviour
    {
        [SerializeField, Min(.05f)] float duration = .80f;
        [SerializeField, Min(.01f)] float startHeight = .115f;
        [SerializeField, Min(.01f)] float endHeight = .205f;

        SpriteRenderer glyph;
        Color tint;
        float unitHeight;
        double bornAt;

        void Awake()
        {
            glyph = GetComponent<SpriteRenderer>();
            tint = glyph.color;
            unitHeight = glyph.sprite ? Mathf.Max(.001f, glyph.sprite.bounds.size.y) : 1f;
            bornAt = Time.unscaledTimeAsDouble;
            Sample(0);
        }

        void Update()
        {
            float age = (float)(Time.unscaledTimeAsDouble - bornAt);
            if (age >= duration) Destroy(gameObject);
            else Sample(age);
        }

        void Sample(float seconds)
        {
            float t = Mathf.Clamp01(seconds / duration);
            float expansion = 1f - (1f - t) * (1f - t);
            transform.localScale = Vector3.one * (Mathf.Lerp(startHeight, endHeight, expansion) / unitHeight);
            float strike = Mathf.SmoothStep(.12f, 1f, Mathf.Clamp01(t / .045f));
            float fade = 1f - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.13f, 1, t));
            glyph.color = new Color(tint.r, tint.g, tint.b, tint.a * strike * fade);
        }
    }
}
