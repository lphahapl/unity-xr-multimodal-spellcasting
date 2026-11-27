using NarutoVR.SealCombat.Rpg;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NarutoVR.SceneArt
{
    [AddComponentMenu("NarutoVR/剧情/场景加载蒙版"), DisallowMultipleComponent, RequireComponent(typeof(Canvas))]
    public sealed class SceneLoadingOverlay : MonoBehaviour
    {
        // Older overlay prefabs have direct references instead of a nested progress bar.
        [SerializeField, HideInInspector] private Image progressFill;
        [SerializeField, HideInInspector] private Text percentText;

        private const float MinimumVisibleSeconds = 0.6f;
        private const float FadeInSeconds = 0.16f;
        private const float PlaqueBaseY = -18f;
        private const float PlaqueWidth = 540f;
        private const float PlaqueHeight = 326f;

        private Canvas surface;
        private CanvasGroup plaqueGroup;
        private RectTransform plaqueRoot;
        private RectTransform astrolabeOuter;
        private RectTransform astrolabeRing;
        private Image astrolabeProgressFill;
        private Image backdropImage;
        private Image gaugeTrackImage;
        private Text statusLabel;
        private Material overlayMaterial;
        private bool chromeBuilt;

        private AsyncOperation operation;
        private int lastPercent = -1;
        private double shownAt, lastSample;
        private float displayedProgress;
        internal bool Finished { get; private set; }

        private void Awake()
        {
            if (Application.isPlaying)
                EnsureVisualChrome();
        }

        internal void Show(Camera view, AsyncOperation load)
        {
            EnsureVisualChrome();
            operation = load;
            surface.renderMode = RenderMode.ScreenSpaceCamera;
            surface.worldCamera = view && view.isActiveAndEnabled ? view : Camera.main;
            shownAt = lastSample = Time.realtimeSinceStartupAsDouble;
            lastPercent = -1;
            Finished = false;
            enabled = true;
            surface.enabled = true;
            if (plaqueGroup) plaqueGroup.alpha = 0f;
            gameObject.SetActive(true);
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Display(0f, 0f);
        }

        /// <summary>在现有 ScreenSpaceCamera Canvas 下组装轻量化空间悬停玉牌与浑仪加载进度框。</summary>
        internal void EnsureVisualChrome()
        {
            if (chromeBuilt && plaqueRoot && progressFill && percentText) return;

            surface = GetComponent<Canvas>();
            surface.renderMode = RenderMode.ScreenSpaceCamera;
            if (surface.planeDistance < 1.2f) surface.planeDistance = 1.5f;

            if (TryGetComponent<CanvasScaler>(out var scaler))
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1280f, 720f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
            }

            var bar = GetComponentInChildren<SceneLoadingProgressBar>(true);
            if (bar)
            {
                if (!progressFill) progressFill = bar.Fill;
                if (!percentText) percentText = bar.Percentage;
            }

            backdropImage = FindBackdropImage(bar);
            overlayMaterial = ResolveOverlayMaterial(backdropImage, progressFill);
            if (!backdropImage) backdropImage = EnsureBackdropImage();
            if (backdropImage)
            {
                backdropImage.color = new Color(0.052f, 0.068f, 0.064f, 0.98f);
                backdropImage.raycastTarget = false;
                if (overlayMaterial) backdropImage.material = overlayMaterial;
            }

            Font font = ResolveFont(bar);
            BuildJadePlaqueAndAstrolabe(bar, font);
            chromeBuilt = true;
        }

        /// <summary>供自动化检查或静态预览同步推进加载视觉状态。</summary>
        internal void SampleForValidation(float progress01, float elapsedSeconds)
        {
            EnsureVisualChrome();
            float clamped = Mathf.Clamp01(progress01);
            if (plaqueGroup)
                plaqueGroup.alpha = elapsedSeconds <= 0f ? 1f : Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsedSeconds / FadeInSeconds));
            Display(clamped, Mathf.Max(0f, elapsedSeconds));
        }

        private void LateUpdate()
        {
            if (operation == null) return;
            if (!surface.worldCamera || !surface.worldCamera.isActiveAndEnabled)
                surface.worldCamera = Camera.main;

            double now = Time.realtimeSinceStartupAsDouble;
            float elapsed = (float)(now - shownAt);
            if (plaqueGroup)
                plaqueGroup.alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / FadeInSeconds));

            float ramp = Mathf.Clamp01(elapsed / MinimumVisibleSeconds);
            float target = operation.isDone ? 1f : Mathf.Clamp01(operation.progress);
            float next = lastSample < shownAt + MinimumVisibleSeconds
                ? Mathf.Lerp(0f, target, ramp)
                : Mathf.MoveTowards(displayedProgress, target, (float)(now - lastSample) / MinimumVisibleSeconds);

            Display(Mathf.Max(displayedProgress, next), elapsed);
            lastSample = now;
            Finished = operation.isDone && ramp >= 1f && displayedProgress >= 1f;
        }

        private void Display(float value, float elapsed)
        {
            displayedProgress = value;
            if (progressFill) progressFill.fillAmount = displayedProgress;
            if (astrolabeProgressFill)
            {
                astrolabeProgressFill.fillAmount = displayedProgress;
                astrolabeProgressFill.color = Color.Lerp(
                    new Color(0.92f, 0.66f, 0.28f, 0.96f),
                    new Color(1.00f, 0.94f, 0.72f, 1.00f),
                    displayedProgress);
            }

            if (plaqueRoot)
            {
                float settle = Mathf.SmoothStep(-8f, 0f, Mathf.Clamp01(elapsed / FadeInSeconds));
                plaqueRoot.anchoredPosition = new Vector2(0f, PlaqueBaseY + settle + Mathf.Sin(elapsed * 1.35f) * 3.5f);
            }
            if (astrolabeOuter)
                astrolabeOuter.localRotation = Quaternion.Euler(0f, 0f, elapsed * 6f);
            if (astrolabeRing)
                astrolabeRing.localRotation = Quaternion.Euler(0f, 0f, -elapsed * 11f);

            int percent = Mathf.FloorToInt(displayedProgress * 100f);
            if (percentText && percent != lastPercent) percentText.text = $"{percent}%";
            lastPercent = percent;
        }

        private void BuildJadePlaqueAndAstrolabe(SceneLoadingProgressBar bar, Font font)
        {
            Transform existingPlaque = transform.Find("考工悬停玉牌");
            if (existingPlaque is RectTransform existingRect)
            {
                plaqueRoot = existingRect;
            }
            else
            {
                var go = new GameObject("考工悬停玉牌", typeof(RectTransform));
                go.layer = gameObject.layer;
                plaqueRoot = (RectTransform)go.transform;
                plaqueRoot.SetParent(transform, false);
            }

            plaqueRoot.anchorMin = plaqueRoot.anchorMax = new Vector2(0.5f, 0.5f);
            plaqueRoot.pivot = new Vector2(0.5f, 0.5f);
            plaqueRoot.anchoredPosition = new Vector2(0f, PlaqueBaseY);
            plaqueRoot.sizeDelta = new Vector2(PlaqueWidth, PlaqueHeight);
            plaqueRoot.localScale = Vector3.one;

            plaqueGroup = plaqueRoot.GetComponent<CanvasGroup>();
            if (!plaqueGroup) plaqueGroup = plaqueRoot.gameObject.AddComponent<CanvasGroup>();
            plaqueGroup.interactable = false;
            plaqueGroup.blocksRaycasts = false;
            if (!Application.isPlaying) plaqueGroup.alpha = 1f;

            var shadow = EnsureChildImage("玉牌晕影", plaqueRoot, Vector2.zero, new Vector2(620f, 384f));
            shadow.sprite = KaogongUISprites.Get("Subtitle_Smoke_Scrim");
            shadow.type = shadow.sprite ? Image.Type.Sliced : Image.Type.Simple;
            shadow.color = new Color(0.03f, 0.04f, 0.04f, 0.72f);

            var plaqueBg = EnsureChildImage("温玉底牌", plaqueRoot, Vector2.zero, new Vector2(PlaqueWidth, PlaqueHeight));
            plaqueBg.sprite = KaogongUISprites.Get("Frame_Jade_Plaque");
            plaqueBg.type = plaqueBg.sprite ? Image.Type.Sliced : Image.Type.Simple;
            plaqueBg.pixelsPerUnitMultiplier = 2.0f;
            plaqueBg.color = plaqueBg.sprite ? Color.white : KaogongTheme.JadeWarm;

            var kicker = EnsureChildText("考工徽题", plaqueRoot, new Vector2(0f, 122f), new Vector2(360f, 26f), font, 15, new Color(0.42f, 0.31f, 0.15f, 0.92f));
            kicker.text = "考 工 律 度 · 境 象 迁 转";

            statusLabel = FindChildByName<Text>(bar ? bar.transform : null, "加载提示")
                ?? FindChildByName<Text>(plaqueRoot, "加载提示")
                ?? EnsureChildText("加载提示", plaqueRoot, new Vector2(0f, 94f), new Vector2(400f, 40f), font, 25, KaogongTheme.InkOnJade);
            ReparentRect(statusLabel.rectTransform, plaqueRoot, new Vector2(0f, 94f), new Vector2(400f, 40f));
            ConfigureText(statusLabel, font, 25, KaogongTheme.InkOnJade);
            if (string.IsNullOrWhiteSpace(statusLabel.text)) statusLabel.text = "正在前往新境";

            var divider = EnsureChildImage("错金分界", plaqueRoot, new Vector2(0f, 68f), new Vector2(332f, 16f));
            divider.sprite = KaogongUISprites.Get("Divider_Gold");
            divider.type = Image.Type.Simple;
            divider.color = divider.sprite ? Color.white : KaogongTheme.BronzeAged;

            var outer = EnsureChildImage("方圆准仪", plaqueRoot, new Vector2(0f, -4f), new Vector2(166f, 166f));
            outer.sprite = KaogongUISprites.Get("Reticle_Fangyuan");
            outer.color = outer.sprite ? new Color(0.56f, 0.43f, 0.22f, 0.68f) : new Color(0.56f, 0.43f, 0.22f, 0f);
            astrolabeOuter = outer.rectTransform;

            var ring = EnsureChildImage("浑仪齿轮环", plaqueRoot, new Vector2(0f, -4f), new Vector2(142f, 142f));
            ring.sprite = KaogongUISprites.Get("Ring_Astrolabe");
            ring.color = ring.sprite ? new Color(0.84f, 0.78f, 0.68f, 0.94f) : new Color(0f, 0f, 0f, 0f);
            astrolabeRing = ring.rectTransform;

            astrolabeProgressFill = EnsureChildImage("浑仪进度弧", plaqueRoot, new Vector2(0f, -4f), new Vector2(142f, 142f));
            astrolabeProgressFill.sprite = KaogongUISprites.Get("Ring_Astrolabe");
            astrolabeProgressFill.type = Image.Type.Filled;
            astrolabeProgressFill.fillMethod = Image.FillMethod.Radial360;
            astrolabeProgressFill.fillOrigin = (int)Image.Origin360.Top;
            astrolabeProgressFill.fillClockwise = true;
            astrolabeProgressFill.fillAmount = 0f;
            astrolabeProgressFill.color = new Color(0.92f, 0.66f, 0.28f, 0.96f);

            percentText ??= FindChildByName<Text>(bar ? bar.transform : null, "进度百分比")
                ?? FindChildByName<Text>(plaqueRoot, "进度百分比")
                ?? EnsureChildText("进度百分比", plaqueRoot, new Vector2(0f, -4f), new Vector2(96f, 44f), font, 27, KaogongTheme.InkOnJade);
            ReparentRect(percentText.rectTransform, plaqueRoot, new Vector2(0f, -4f), new Vector2(96f, 44f));
            ConfigureText(percentText, font, 27, KaogongTheme.InkOnJade);

            gaugeTrackImage = FindChildByName<Image>(bar ? bar.transform : null, "进度条")
                ?? FindChildByName<Image>(plaqueRoot, "进度条")
                ?? EnsureChildImage("进度条", plaqueRoot, new Vector2(0f, -110f), new Vector2(408f, 26f));
            ReparentRect(gaugeTrackImage.rectTransform, plaqueRoot, new Vector2(0f, -110f), new Vector2(408f, 26f));
            gaugeTrackImage.sprite = KaogongUISprites.Get("Gauge_Track");
            gaugeTrackImage.type = gaugeTrackImage.sprite ? Image.Type.Sliced : Image.Type.Simple;
            gaugeTrackImage.pixelsPerUnitMultiplier = 2.0f;
            gaugeTrackImage.color = gaugeTrackImage.sprite ? Color.white : KaogongTheme.LacquerDark;
            gaugeTrackImage.raycastTarget = false;
            if (overlayMaterial) gaugeTrackImage.material = overlayMaterial;

            progressFill ??= FindChildByName<Image>(bar ? bar.transform : null, "填充")
                ?? FindChildByName<Image>(gaugeTrackImage.transform, "填充")
                ?? EnsureChildImage("填充", gaugeTrackImage.rectTransform, Vector2.zero, Vector2.zero);
            var fillRect = progressFill.rectTransform;
            fillRect.SetParent(gaugeTrackImage.rectTransform, false);
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.pivot = new Vector2(0.5f, 0.5f);
            fillRect.offsetMin = new Vector2(8f, 5f);
            fillRect.offsetMax = new Vector2(-8f, -5f);
            fillRect.localScale = Vector3.one;
            progressFill.sprite = KaogongUISprites.Get("Gauge_Fill_Inner") ?? KaogongUISprites.Get("Gauge_Fill") ?? progressFill.sprite;
            progressFill.type = Image.Type.Filled;
            progressFill.fillMethod = Image.FillMethod.Horizontal;
            progressFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            progressFill.color = progressFill.sprite ? Color.white : KaogongTheme.GoldInlay;
            progressFill.raycastTarget = false;
            if (overlayMaterial) progressFill.material = overlayMaterial;
        }

        private Image FindBackdropImage(SceneLoadingProgressBar bar)
        {
            var named = transform.Find("SceneLoadingBackdrop");
            if (named && named.TryGetComponent<Image>(out var direct)) return direct;
            var images = GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                var img = images[i];
                if (!img) continue;
                if (bar && img.transform.IsChildOf(bar.transform)) continue;
                if (img.transform.parent == transform) return img;
            }
            return null;
        }

        private Image EnsureBackdropImage()
        {
            var go = new GameObject("SceneLoadingBackdrop", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.layer = gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(transform, false);
            rect.SetAsFirstSibling();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return go.GetComponent<Image>();
        }

        private Material ResolveOverlayMaterial(Image backdrop, Image fill)
        {
            if (backdrop && backdrop.material && backdrop.material != Canvas.GetDefaultCanvasMaterial())
                return backdrop.material;
            if (fill && fill.material && fill.material != Canvas.GetDefaultCanvasMaterial())
                return fill.material;
            var shader = Shader.Find("NarutoVR/UI/Scene Loading Overlay");
            if (shader)
            {
                var mat = new Material(shader) { name = "SceneLoadingOverlay · Runtime", hideFlags = HideFlags.DontSave };
                return mat;
            }
            return null;
        }

        private Font ResolveFont(SceneLoadingProgressBar bar)
        {
            if (bar && bar.Percentage && bar.Percentage.font) return bar.Percentage.font;
            var texts = GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
                if (texts[i] && texts[i].font) return texts[i].font;
            var cjk = Resources.Load<Font>("Fonts/NotoSansCJKsc-Regular");
            return cjk ? cjk : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private Image EnsureChildImage(string name, RectTransform parent, Vector2 position, Vector2 size)
        {
            var existing = parent.Find(name);
            Image image;
            if (existing && existing.TryGetComponent<Image>(out var found))
            {
                image = found;
            }
            else
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.layer = gameObject.layer;
                go.transform.SetParent(parent, false);
                image = go.GetComponent<Image>();
            }
            ReparentRect(image.rectTransform, parent, position, size);
            image.raycastTarget = false;
            if (overlayMaterial) image.material = overlayMaterial;
            return image;
        }

        private Text EnsureChildText(string name, RectTransform parent, Vector2 position, Vector2 size, Font font, int fontSize, Color color)
        {
            var existing = parent.Find(name);
            Text text;
            if (existing && existing.TryGetComponent<Text>(out var found))
            {
                text = found;
            }
            else
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                go.layer = gameObject.layer;
                go.transform.SetParent(parent, false);
                text = go.GetComponent<Text>();
            }
            ReparentRect(text.rectTransform, parent, position, size);
            ConfigureText(text, font, fontSize, color);
            return text;
        }

        private void ConfigureText(Text text, Font font, int fontSize, Color color)
        {
            if (font) text.font = font;
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Normal;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.resizeTextForBestFit = false;
            text.supportRichText = false;
            text.raycastTarget = false;
            text.color = color;
            if (overlayMaterial) text.material = overlayMaterial;
        }

        private static void ReparentRect(RectTransform rect, RectTransform parent, Vector2 position, Vector2 size)
        {
            if (rect.parent != parent) rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;
        }

        private static T FindChildByName<T>(Transform root, string childName) where T : Component
        {
            if (!root) return null;
            var direct = root.Find(childName);
            return direct ? direct.GetComponent<T>() : null;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!surface.worldCamera || !surface.worldCamera.isActiveAndEnabled)
                surface.worldCamera = Camera.main;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (overlayMaterial && overlayMaterial.hideFlags == HideFlags.DontSave)
            {
                if (Application.isPlaying) Destroy(overlayMaterial);
                else DestroyImmediate(overlayMaterial);
            }
        }
    }
}
