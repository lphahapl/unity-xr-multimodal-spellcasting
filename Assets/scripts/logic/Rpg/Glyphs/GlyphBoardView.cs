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
        static readonly Color PaperInk = new Color(.40f, .115f, .075f);
        static readonly Color PaperGuide = new Color(.20f, .32f, .265f, .81f);
        static readonly Color GoldHighlight = new Color(1f, .97f, .70f);
        static readonly Vector2 WritingSize = new Vector2(GlyphBoard.HalfWidth, GlyphBoard.HalfHeight) * (2f * PixelsPerMetre);
        readonly RectTransform root;
        readonly Font font;
        readonly Text title, status;
        readonly GlyphStrokeGraphic drawing, guide;
        readonly Image nibReticle;
        GlyphStoryStrokeGuide steps;
        GlyphStrokeGraphic brightLine;
        Text instruction, startLabel, endLabel;
        int markedStroke = -1;
        public string StepFeedback => steps?.Feedback;
        public string StepRejection => steps?.Rejection;
        readonly Image[] buttons = new Image[2];
        readonly Sprite buttonNormal, buttonHover;
        readonly Material brushInk;
        readonly Color inkColor, guideColor, gold, hoverGreen, stepColor;
        readonly List<List<Vector2>> ink;
        readonly IReadOnlyList<GlyphBoard.NibSample> nibSamples;
        List<List<Vector2>> snapTargets, movingInk;
        int pointedButton = -1;
        bool showingSuccess, pressingButton;

        public bool ContainsBoardPoint(Vector2 point) => root.rect.Contains(point * PixelsPerMetre);

        public GlyphBoardView(Transform parent, List<List<Vector2>> ink, IReadOnlyList<GlyphBoard.NibSample> nibSamples)
        {
            this.ink = ink;
            this.nibSamples = nibSamples;
            brushInk = Resources.Load<Material>("KaogongUI/GlyphBrushInk");
            var paper = Resources.Load<Sprite>("KaogongUI/GlyphPaper");
            inkColor = paper ? PaperInk : new Color(.96f, .39f, .19f);
            stepColor = paper ? new Color(.04f, .29f, .28f) : new Color(.64f, 1f, .83f);
            guideColor = paper ? PaperGuide : new Color(.41f, .67f, .58f, .74f);
            gold = paper ? new Color(.54f, .30f, .105f) : KaogongTheme.GoldInlay;
            hoverGreen = paper ? PaperGuide : KaogongTheme.JadeAccent;
            root = new GameObject("墨规画板", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler)).GetComponent<RectTransform>();
            root.SetParent(parent, false);
            root.sizeDelta = new Vector2(395f, 421f);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            if (brushInk) canvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1;
            root.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 3f;
            root.localScale = Vector3.one / PixelsPerMetre;
            root.localRotation = Quaternion.identity;
            root.localPosition = Vector3.zero;
            font = Resources.Load<Font>("Fonts/NotoSansCJKsc-Regular");
            if (!font) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var panel = Image("青铜板", Vector2.zero, root.sizeDelta, KaogongTheme.LacquerDark,
                KaogongUISprites.Get("Frame_Bronze_Main"));
            if (panel.sprite) panel.color = KaogongTheme.JadeWarm;
            panel.pixelsPerUnitMultiplier = 5f;
            Image("书写面", Vector2.zero, WritingSize, paper ? Color.white : KaogongTheme.SlatePanel,
                paper, sliced: false);
            Color ruling = paper ? new Color(.42f, .32f, .195f, .29f) : new Color(.56f, .51f, .35f, .30f);
            var rule = Lines("考工度尺", ruling, .65f);
            rule.lines = BuildRuling();
            guide = Lines("范本", guideColor, GuideWidth);
            drawing = Lines("朱砂墨迹", inkColor, InkWidth);
            drawing.lines = ink;
            var plaque = Image("温玉符名匾", new Vector2(0, 191), new Vector2(346, 34), KaogongTheme.JadeWarm,
                KaogongUISprites.Get("Frame_Jade_Plaque"));
            plaque.pixelsPerUnitMultiplier = 8f;
            title = Label("符名", Vector2.zero, new Vector2(324, 32), 23, plaque.rectTransform);
            title.color = KaogongTheme.InkOnJade;
            status = Label("提示", new Vector2(0, 161), new Vector2(374, 22), 16);

            buttonNormal = KaogongUISprites.Get("Button_Normal");
            buttonHover = KaogongUISprites.Get("Button_Hover");
            Button(0, "撤销");
            Button(1, "清空");
            nibReticle = Image("笔尖落点", Vector2.zero, new Vector2(24f, 24f), KaogongTheme.JadeAccent,
                KaogongUISprites.Get("Reticle_Fangyuan"), sliced: false);
            nibReticle.gameObject.SetActive(false);
        }

        public void Open(GlyphTemplate target, bool story)
        {
            root.gameObject.SetActive(true);
            title.text = target ? target.Title : story ? "剧情画符" : "墨规画符";
            ClearPointer();
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
            steps = story && target && lines.Count > 0 && target.storyStrokeHints?.Length == lines.Count
                ? new GlyphStoryStrokeGuide(target.storyStrokeHints, lines) : null;
            markedStroke = -1;
            if (steps != null && !brightLine)
            {
                brightLine = Lines("当前笔与方向", stepColor, 3.8f);
                brightLine.rectTransform.SetSiblingIndex(drawing.rectTransform.GetSiblingIndex());
                instruction = Label("笔顺", new Vector2(0, 134), new Vector2(354, 25), 17);
                startLabel = Label("起笔", Vector2.zero, new Vector2(30, 22), 16);
                endLabel = Label("收笔", Vector2.zero, new Vector2(30, 22), 16);
                startLabel.text = "起";
                endLabel.text = "止";
                instruction.color = startLabel.color = endLabel.color = stepColor;
            }
            SetStepOverlay(steps != null);
            guide.lines = lines;
            guide.SetVerticesDirty();
        }

        void SetStepOverlay(bool show)
        {
            if (!brightLine) return;
            brightLine.gameObject.SetActive(show);
            instruction.gameObject.SetActive(show);
            startLabel.gameObject.SetActive(show);
            endLabel.gameObject.SetActive(show);
        }

        public void ReadSteps()
        {
            if (steps == null) return;
            steps.Read(ink);
            guide.color = new Color(guideColor.r, guideColor.g, guideColor.b, .35f);
            if (markedStroke == steps.Current) return;
            markedStroke = steps.Current;
            instruction.text = steps.Instruction;
            var line = steps.CurrentLine;
            startLabel.gameObject.SetActive(line != null);
            endLabel.gameObject.SetActive(line != null);
            brightLine.gameObject.SetActive(line != null);
            if (line == null) return;
            var marks = new List<List<Vector2>> { line };
            for (int i = 1; i < line.Count; i++)
            {
                var dir = (line[i] - line[i - 1]).normalized;
                var tip = Vector2.Lerp(line[i - 1], line[i], .53f);
                var basePoint = tip - dir * .009f;
                var side = Vector2.Perpendicular(dir) * .005f;
                marks.Add(new() { basePoint + side, tip, basePoint - side });
            }
            startLabel.rectTransform.anchoredPosition = line[0] * PixelsPerMetre + Vector2.down * 14f;
            endLabel.rectTransform.anchoredPosition = line[line.Count - 1] * PixelsPerMetre + Vector2.down * 14f;
            brightLine.lines = marks;
            brightLine.SetVerticesDirty();
        }

        public void Hide() { if (root) root.gameObject.SetActive(false); }
        public void InkChanged()
        {
            if (showingSuccess) ResetSuccess();
            else drawing.UpdateInk(ink, nibSamples, brushInk);
        }
        public void SetStatus(string value) { if (status) status.text = value; }

        public void SetNib(Vector2 point, float proximity, bool touching)
        {
            SetButton(-1, false);
            if (!nibReticle.sprite) return;
            var rect = nibReticle.rectTransform;
            float size = Mathf.Lerp(25f, 12f, proximity);
            rect.anchoredPosition = point * PixelsPerMetre;
            rect.sizeDelta = new Vector2(size, size);
            Color tint = touching ? inkColor : Color.Lerp(hoverGreen, gold, proximity);
            tint.a = Mathf.Lerp(.36f, .88f, proximity);
            nibReticle.color = tint;
            if (!nibReticle.gameObject.activeSelf) nibReticle.gameObject.SetActive(true);
        }

        public void ClearPointer()
        {
            if (nibReticle.gameObject.activeSelf) nibReticle.gameObject.SetActive(false);
            SetButton(-1, false);
        }

        public void ShowButton(int index, bool touching)
        {
            if (nibReticle.gameObject.activeSelf) nibReticle.gameObject.SetActive(false);
            SetButton(index, touching);
        }

        void SetButton(int index, bool pressing)
        {
            if (pointedButton == index && pressingButton == pressing) return;
            if (pointedButton >= 0) PaintButton(pointedButton, false, false);
            pointedButton = index;
            pressingButton = pressing;
            if (index >= 0) PaintButton(index, true, pressing);
        }

        void PaintButton(int index, bool highlighted, bool pressing)
        {
            var button = buttons[index];
            button.sprite = highlighted && buttonHover ? buttonHover : buttonNormal;
            button.color = highlighted ? KaogongTheme.GoldInlay : KaogongTheme.JadeWarm;
            var transform = button.rectTransform;
            transform.anchoredPosition = new Vector2((index - 1) * GlyphBoard.ButtonSpacing * PixelsPerMetre,
                GlyphBoard.ButtonY * PixelsPerMetre - (pressing ? 2f : 0f));
        }

        static List<List<Vector2>> BuildRuling()
        {
            float x = GlyphBoard.HalfWidth - .010f, y = GlyphBoard.HalfHeight - .010f;
            var lines = new List<List<Vector2>>
            {
                new() { new(-x, -y), new(x, -y), new(x, y), new(-x, y), new(-x, -y) },
                new() { new(-x, 0), new(x, 0) },
                new() { new(0, -y), new(0, y) },
            };
            for (int i = -3; i <= 3; i++)
            {
                float col = x * i / 4f, row = y * i / 4f;
                lines.Add(new() { new(col, -y), new(col, -y + .006f) });
                lines.Add(new() { new(col, y - .006f), new(col, y) });
                lines.Add(new() { new(-x, row), new(-x + .006f, row) });
                lines.Add(new() { new(x - .006f, row), new(x, row) });
            }
            return lines;
        }

        public void SetStorySuccessProgress(float snap01, float flash01)
        {
            if (guide.lines == null || guide.lines.Count == 0 || ink.Count == 0) return;
            if (!showingSuccess) SetStepOverlay(false);
            showingSuccess = true;
            float progress = Mathf.SmoothStep(0, 1, Mathf.Clamp01(snap01));
            float flash = progress * Mathf.Clamp01(flash01);
            Color flashColor = Color.Lerp(gold, GoldHighlight, flash);
            float goldWidth = InkWidth * (1 + .50f * flash);
            // 范本补齐分笔、顺序或采样密度不同的部分。结束时墨迹使用范本本身的线段。
            guide.color = Color.Lerp(guideColor, flashColor, progress);
            guide.width = Mathf.Lerp(GuideWidth, goldWidth, progress);
            guide.SetVerticesDirty();
            Color tint = Color.Lerp(inkColor, flashColor, progress);
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
            if (progress > 0f) drawing.RebuildSuccessInk(progress >= 1f);
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
            drawing.color = inkColor;
            drawing.width = InkWidth;
            guide.color = guideColor;
            guide.width = GuideWidth;
            drawing.UpdateInk(ink, nibSamples, brushInk);
            guide.SetVerticesDirty();
        }

        void Button(int index, string caption)
        {
            var center = new Vector2((index - 1) * GlyphBoard.ButtonSpacing, GlyphBoard.ButtonY) * PixelsPerMetre;
            var size = new Vector2(GlyphBoard.ButtonHalfWidth, GlyphBoard.ButtonHalfHeight) * (2 * PixelsPerMetre);
            var bg = Image(caption, center, size, KaogongTheme.JadeWarm, buttonNormal);
            bg.pixelsPerUnitMultiplier = 3f;
            buttons[index] = bg;
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

        Image Image(string name, Vector2 center, Vector2 size, Color color, Sprite sprite = null, bool sliced = true)
        {
            var img = Rect(name, center, size).gameObject.AddComponent<Image>();
            img.color = color;
            img.sprite = sprite;
            if (sprite && sliced) img.type = UnityEngine.UI.Image.Type.Sliced;
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
