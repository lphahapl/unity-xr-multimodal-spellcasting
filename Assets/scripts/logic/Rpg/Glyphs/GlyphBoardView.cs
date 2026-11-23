using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace NarutoVR.SealCombat.Rpg
{
    /// <summary>局部 XY 上的世界空间 UI；按钮位置与 GlyphBoard 的笔尖命中区共用。</summary>
    internal sealed class GlyphBoardView
    {
        const float PixelsPerMetre = 1000f;
        const float InkWidth = 3.6f, GuideWidth = 2.6f;
        static readonly Color InkColor = new Color(1f, .40f, .20f);
        static readonly Color GuideColor = new Color(.45f, .70f, .64f, .72f);
        static readonly Color Gold = new Color(.99f, .74f, .18f);
        static readonly Color GoldHighlight = new Color(1f, .97f, .70f);
        readonly RectTransform root;
        readonly Font font;
        readonly Text title, status;
        readonly GlyphStrokeGraphic drawing, guide;
        readonly List<List<Vector2>> ink;
        List<List<Vector2>> snapTargets, movingInk;
        bool showingSuccess;

        public bool ContainsBoardPoint(Vector2 point) => root.rect.Contains(point * PixelsPerMetre);

        public GlyphBoardView(Transform parent, List<List<Vector2>> ink)
        {
            this.ink = ink;
            root = new GameObject("墨规画板", typeof(RectTransform), typeof(Canvas)).GetComponent<RectTransform>();
            root.SetParent(parent, false);
            root.sizeDelta = new Vector2(395f, 421f);
            root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            root.localScale = Vector3.one / PixelsPerMetre;
            root.localRotation = Quaternion.identity;
            root.localPosition = Vector3.zero;
            font = Resources.Load<Font>("Fonts/NotoSansCJKsc-Regular");
            if (!font) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var panel = Image("青铜板", Vector2.zero, root.sizeDelta, new Color(.09f, .14f, .14f, .96f));
            panel.transform.SetAsFirstSibling();
            Image("书写面", Vector2.zero, new Vector2(2 * GlyphBoard.HalfWidth, 2 * GlyphBoard.HalfHeight) * PixelsPerMetre,
                new Color(.19f, .25f, .235f));
            guide = Lines("范本", GuideColor, GuideWidth);
            drawing = Lines("朱砂墨迹", InkColor, InkWidth);
            drawing.lines = ink;
            title = Label("符名", new Vector2(0, 188), new Vector2(374, 32), 23);
            status = Label("提示", new Vector2(0, 164), new Vector2(374, 24), 17);

            Button(0, "撤销");
            Button(1, "清空");
        }

        public void Open(GlyphTemplate target, bool story)
        {
            root.gameObject.SetActive(true);
            title.text = target ? target.Title : story ? "剧情画符" : "墨规画符";
            ResetSuccess();
            var lines = new List<List<Vector2>>();
            if (target)
            {
                foreach (var stroke in target.Preview()) lines.Add(new List<Vector2>(stroke));
                var bounds = BoundsOf(lines);
                float scale = Mathf.Min(.27f / Mathf.Max(.0001f, bounds.size.x), .22f / Mathf.Max(.0001f, bounds.size.y));
                foreach (var stroke in lines)
                    for (int i = 0; i < stroke.Count; ++i)
                        stroke[i] = (stroke[i] - (Vector2)bounds.center) * scale;
            }
            guide.lines = lines;
            guide.SetVerticesDirty();
        }

        public void Hide() { if (root) root.gameObject.SetActive(false); }
        public void InkChanged()
        {
            if (showingSuccess) ResetSuccess();
            if (drawing) drawing.UpdateInk(ink);
        }
        public void SetStatus(string value) { if (status) status.text = value; }

        public void SetStorySuccessProgress(float snap01, float flash01)
        {
            if (guide.lines == null || guide.lines.Count == 0 || ink.Count == 0) return;
            showingSuccess = true;
            float progress = Mathf.SmoothStep(0, 1, Mathf.Clamp01(snap01));
            float flash = progress * Mathf.Clamp01(flash01);
            Color gold = Color.Lerp(Gold, GoldHighlight, flash);
            float goldWidth = InkWidth * (1 + .50f * flash);
            // 范本补齐分笔、顺序或采样密度不同的部分。结束时墨迹使用范本本身的线段。
            guide.color = Color.Lerp(GuideColor, gold, progress);
            guide.width = Mathf.Lerp(GuideWidth, goldWidth, progress);
            guide.SetVerticesDirty();
            Color tint = Color.Lerp(InkColor, gold, progress);
            drawing.width = Mathf.Lerp(InkWidth, goldWidth, progress);
            if (progress >= 1f)
                drawing.lines = guide.lines;
            else if (progress <= 0f)
                drawing.lines = ink;
            else
            {
                if (snapTargets == null) PrepareSnap();
                for (int s = 0; s < ink.Count; ++s)
                    for (int i = 0; i < ink[s].Count; ++i)
                        movingInk[s][i] = Vector2.Lerp(ink[s][i], snapTargets[s][i], progress);
                // 多笔跨越同一范本拐角时，最后一小段消隐，避免留下跨角的短连线。
                tint.a = 1f - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.65f, 1f, progress));
                drawing.lines = movingInk;
            }
            drawing.color = tint;
            drawing.RebuildSuccessInk(progress >= 1f);
        }

        void PrepareSnap()
        {
            Bounds inkBounds = BoundsOf(ink), guideBounds = BoundsOf(guide.lines);
            float inkSize = Mathf.Max(inkBounds.size.x, inkBounds.size.y);
            float scale = Mathf.Max(guideBounds.size.x, guideBounds.size.y) / Mathf.Max(.0001f, inkSize);
            snapTargets = new List<List<Vector2>>(ink.Count);
            movingInk = new List<List<Vector2>>(ink.Count);
            foreach (var stroke in ink)
            {
                var targets = new List<Vector2>(stroke.Count);
                foreach (Vector2 point in stroke)
                {
                    Vector2 aligned = (Vector2)guideBounds.center + (point - (Vector2)inkBounds.center) * scale;
                    targets.Add(NearestGuidePoint(aligned));
                }
                snapTargets.Add(targets);
                movingInk.Add(new List<Vector2>(stroke));
            }
        }

        static Bounds BoundsOf(List<List<Vector2>> strokes)
        {
            var bounds = new Bounds();
            bool first = true;
            foreach (var stroke in strokes)
                foreach (var point in stroke)
                {
                    if (first) { bounds.center = point; first = false; }
                    else bounds.Encapsulate(point);
                }
            return bounds;
        }

        Vector2 NearestGuidePoint(Vector2 point)
        {
            Vector2 best = point;
            float distance = float.PositiveInfinity;
            foreach (var stroke in guide.lines)
            {
                for (int i = 0; i < stroke.Count; ++i)
                {
                    Vector2 end = stroke[i];
                    Vector2 start = i == 0 ? end : stroke[i - 1];
                    Vector2 segment = end - start;
                    float lengthSquared = segment.sqrMagnitude;
                    float t = lengthSquared > 0 ? Mathf.Clamp01(Vector2.Dot(point - start, segment) / lengthSquared) : 0;
                    Vector2 projected = start + t * segment;
                    float gap = (projected - point).sqrMagnitude;
                    if (gap < distance) { distance = gap; best = projected; }
                }
            }
            return best;
        }

        void ResetSuccess()
        {
            showingSuccess = false;
            snapTargets = movingInk = null;
            drawing.lines = ink;
            drawing.color = InkColor;
            drawing.width = InkWidth;
            guide.color = GuideColor;
            guide.width = GuideWidth;
            drawing.SetVerticesDirty();
            guide.SetVerticesDirty();
        }

        void Button(int index, string caption)
        {
            var center = new Vector2((index - 1) * GlyphBoard.ButtonSpacing, GlyphBoard.ButtonY) * PixelsPerMetre;
            var size = new Vector2(GlyphBoard.ButtonHalfWidth, GlyphBoard.ButtonHalfHeight) * (2 * PixelsPerMetre);
            var bg = Image(caption, center, size, new Color(.27f, .34f, .29f));
            var label = Label(caption, Vector2.zero, size, 19, bg.rectTransform);
            label.text = caption;
            label.color = new Color(.96f, .92f, .78f);
        }

        RectTransform Rect(string name, Vector2 center, Vector2 size, RectTransform parent = null)
        {
            var child = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            child.SetParent(parent ? parent : root, false);
            child.sizeDelta = size;
            child.anchoredPosition = center;
            return child;
        }

        Image Image(string name, Vector2 center, Vector2 size, Color color)
        {
            var img = Rect(name, center, size).gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        Text Label(string name, Vector2 center, Vector2 size, int fontSize, RectTransform parent = null)
        {
            var label = Rect(name, center, size, parent).gameObject.AddComponent<Text>();
            label.font = font;
            label.fontSize = fontSize;
            label.color = new Color(.93f, .91f, .76f);
            label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            return label;
        }

        GlyphStrokeGraphic Lines(string name, Color color, float width)
        {
            var graphic = Rect(name, Vector2.zero, root.sizeDelta).gameObject.AddComponent<GlyphStrokeGraphic>();
            graphic.color = color;
            graphic.width = width;
            graphic.raycastTarget = false;
            return graphic;
        }
    }
}
