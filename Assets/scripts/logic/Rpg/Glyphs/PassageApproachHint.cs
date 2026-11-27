using UnityEngine;
using UnityEngine.UI;

namespace NarutoVR.SealCombat.Rpg
{
    // One instruction for a group of landings; the physical bridge sockets remain visible on their own.
    public sealed class PassageApproachHint : MonoBehaviour
    {
        public LinearRpgController player;
        public PassageGlyphAnchor[] anchors;
        [Tooltip("可选的试绘范本。取笔展开画板后可见。")] public GameObject strokeReference;
        [Min(.5f)] public float approachDistance = 3.25f;
        RectTransform label;
        CanvasGroup group;
        PassageGlyphAnchor near;
        bool learned;
        float nextScan;
        public bool HasReadableAnchor => near;

        void Awake()
        {
            label = new GameObject("近岸行路提示", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup),
                typeof(Text), typeof(Shadow)).GetComponent<RectTransform>();
            label.SetParent(transform, false);
            label.sizeDelta = new Vector2(500, 64);
            label.localScale = Vector3.one * .0016f;
            label.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var text = label.GetComponent<Text>();
            text.font = Resources.Load<Font>("Fonts/NotoSansCJKsc-Regular");
            text.fontSize = 42;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            text.color = new Color(1f, .92f, .72f);
            text.text = "画通路符，可连两岸";
            var shadow = label.GetComponent<Shadow>();
            shadow.effectColor = new Color(.065f, .08f, .07f, .92f);
            shadow.effectDistance = new Vector2(1.5f, -2f);
            group = label.GetComponent<CanvasGroup>();
            group.alpha = 0;
            group.interactable = group.blocksRaycasts = false;
            label.gameObject.SetActive(false);
        }

        void OnDisable()
        {
            near = null;
            if (label) { group.alpha = 0; label.gameObject.SetActive(false); }
            if (strokeReference) strokeReference.SetActive(false);
        }

        void Update()
        {
            var pen = player ? player.GlyphPen : null;
            var head = player && player.locomotion ? player.locomotion.head : null;
            bool writing = pen && pen.IsHeld && pen.activeBoard && pen.activeBoard.IsOpen;
            if (strokeReference && strokeReference.activeSelf != writing) strokeReference.SetActive(writing);
            if (learned || !head || !pen || !player.CanAct || pen.IsHeld)
            {
                near = null;
                if (label.gameObject.activeSelf) { group.alpha = 0; label.gameObject.SetActive(false); }
                return;
            }
            if (Time.unscaledTime >= nextScan)
            {
                nextScan = Time.unscaledTime + .10f;
                near = NearestReadable(head, pen);
            }
            if (near)
            {
                var position = near.landing.position + Vector3.up * .87f;
                label.SetPositionAndRotation(position, Quaternion.LookRotation(position - head.position, Vector3.up));
                label.gameObject.SetActive(true);
            }
            group.alpha = Mathf.MoveTowards(group.alpha, near ? 1f : 0f, 5f * Time.unscaledDeltaTime);
            if (!near && group.alpha <= 0) label.gameObject.SetActive(false);
        }

        PassageGlyphAnchor NearestReadable(Transform head, GlyphPenController pen)
        {
            if (anchors == null || pen.activeTemplates == null) return null;
            float span = 0;
            foreach (var glyph in pen.activeTemplates)
                if (glyph && glyph.effect is PassageGlyphEffect effect) span = Mathf.Max(span, effect.maxSpan);
            if (span <= 0) return null;
            PassageGlyphAnchor best = null;
            float closest = approachDistance * approachDistance;
            foreach (var anchor in anchors)
            {
                if (!anchor || !anchor.Available) continue;
                if (anchor.HasRoad) { learned = true; return null; }
                var focus = anchor.landing.position + Vector3.up * .40f;
                var direction = focus - head.position;
                if (direction.sqrMagnitude >= closest || Vector3.Dot(head.forward, direction.normalized) < .52f) continue;
                if (Physics.Linecast(head.position, focus, out var hit, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                    && hit.collider.GetComponentInParent<PassageGlyphAnchor>() != anchor) continue;
                foreach (var other in anchors)
                    if (anchor.CanLinkTo(other, span)) { best = anchor; closest = direction.sqrMagnitude; break; }
            }
            return best;
        }
    }
}
