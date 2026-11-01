// WristMenuView.cs
// WristUiTarget 由父项目提供；本文件不重复声明契约类型。
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NarutoVR.SealCombat.Rpg
{
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class WristMenuView : MonoBehaviour
    {
        [Header("字体与世界尺寸")]
        [InspectorName("中文字体（留空自动回退）")]
        public Font font;

        [InspectorName("画布单位比例（米／像素）")]
        [Min(0.0001f)]
        public float canvasScale = 0.0005f;

        [Header("腕饰与界面配色")]
        [InspectorName("深色半透明底板")]
        public Color plateColor = new Color(0.025f, 0.040f, 0.065f, 0.96f);

        [InspectorName("按钮底色")]
        public Color buttonColor = new Color(0.055f, 0.085f, 0.120f, 0.98f);

        [InspectorName("正文颜色")]
        public Color textColor = new Color(0.91f, 0.96f, 1f, 1f);

        [InspectorName("辅助文字颜色")]
        public Color mutedTextColor = new Color(0.62f, 0.73f, 0.81f, 1f);

        [InspectorName("青色灵光")]
        public Color cyanColor = new Color(0.22f, 0.88f, 0.95f, 1f);

        [InspectorName("紫色纹路")]
        public Color violetColor = new Color(0.61f, 0.45f, 0.92f, 1f);

        [InspectorName("金色点缀")]
        public Color goldColor = new Color(0.96f, 0.76f, 0.36f, 1f);

        [Header("可选提示音")]
        [InspectorName("启用非语言提示音")]
        public bool audioFeedback = true;

        [InspectorName("提示音音量")]
        [Range(0f, 1f)]
        public float cueVolume = 0.18f;

        public IReadOnlyList<WristUiTarget> Targets => _targets;
        public GameObject OwnedRoot => _ownedRoot;

        private const float LauncherWidth = 720f;
        private const float LauncherHeight = 300f;
        private const float PanelWidth = 1120f;
        private const float PanelHeight = 800f;

        private enum Ink
        {
            Plate,
            Text,
            Muted,
            Cyan,
            Violet,
            Gold
        }

        private sealed class GraphicBinding
        {
            public Graphic graphic;
            public Ink ink;
            public float alpha;
        }

        private sealed class ButtonVisual
        {
            public string id;
            public RectTransform rect;
            public Image background;
            public Outline outline;
            public Text label;
            public Text caption;
            public RectTransform progress;
            public Image progressImage;
        }

        private readonly List<WristUiTarget> _targets = new List<WristUiTarget>(7);
        private readonly List<ButtonVisual> _buttons = new List<ButtonVisual>(7);
        private readonly List<GraphicBinding> _graphics = new List<GraphicBinding>(48);
        private readonly List<Text> _texts = new List<Text>(24);

        private GameObject _ownedRoot;
        private RectTransform _launcher;
        private RectTransform _panel;
        private RectTransform _openingHint, _openingFill;
        private Text _openingText;
        private Transform _decoration;
        private Transform _reticle;
        private LineRenderer _pointer;
        private Text _panelTitle;
        private Text _panelBody;
        private Text _panelFooter;

        private Material _cyanMaterial;
        private Material _violetMaterial;
        private Material _goldMaterial;
        private Material _pointerMaterial;

        private AudioSource _audioSource;
        private AudioClip _acceptedClip;
        private AudioClip _rejectedClip;

        private Font _fallbackFont;
        private bool _fallbackResolved;
        private bool _built;
        private bool _styleDirty = true;
        private void OnValidate() { _styleDirty = true; }
        private bool _pointerPressed;
        private ButtonVisual _hovered;
        private float _hoverProgress;

        public void EnsureBuilt()
        {
            if (!_built || !_ownedRoot || _targets.Count != 9)
            {
                ReleaseVisuals();
                try { Build(); _built = true; }
                catch { ReleaseVisuals(); throw; }
            }

            if (_styleDirty) { RefreshStyle(); _styleDirty = false; }
        }

        public void SetDecoration(Pose pose, bool visible)
        {
            if (!_built && !visible) return;
            EnsureBuilt();
            if (visible) Place(_decoration, pose);
            _decoration.gameObject.SetActive(visible);
        }

        public void ShowLauncher(Pose pose)
        {
            EnsureBuilt();
            Place(_launcher, pose);
            _launcher.gameObject.SetActive(true);
        }

        public void SetOpeningHint(Pose pose, string hint, float progress, bool visible)
        {
            if (!_built && !visible) return;
            EnsureBuilt(); _openingHint.gameObject.SetActive(visible);
            if (!visible) return;
            Place(_openingHint,pose);
            if (_openingText.text != hint) _openingText.text = hint;
            _openingFill.localScale = new Vector3(Mathf.Clamp01(progress),1,1);
        }

        public void HideLauncher()
        {
            if (!_built)
                return;

            ClearHoverWithin(_launcher);
            _launcher.gameObject.SetActive(false);
            // 面板拥有独立的可见性及世界位姿，绝不在此关闭。
        }

        public void ShowPanel(Pose pose, string title, string body, string footer)
        {
            EnsureBuilt();
            _panelTitle.text = title ?? string.Empty;
            _panelBody.text = body ?? string.Empty;
            _panelFooter.text = footer ?? string.Empty;
            Place(_panel, pose);
            _panel.gameObject.SetActive(true);
        }

        public void HidePanel()
        {
            if (!_built)
                return;

            ClearHoverWithin(_panel);
            _panel.gameObject.SetActive(false);
        }

        public void SetHover(string id, float progress)
        {
            if (!_built && string.IsNullOrEmpty(id)) return;
            EnsureBuilt();

            ButtonVisual next = null;
            if (!string.IsNullOrEmpty(id))
            {
                for (int i = 0; i < _buttons.Count; i++)
                {
                    ButtonVisual candidate = _buttons[i];
                    if (candidate.id == id && candidate.rect.gameObject.activeInHierarchy)
                    {
                        next = candidate;
                        break;
                    }
                }
            }

            ButtonVisual previous = _hovered;
            _hovered = next;
            _hoverProgress = float.IsNaN(progress) ? 0f : Mathf.Clamp01(progress);

            if (previous != null && previous != next)
                PaintButton(previous);

            if (next != null)
                PaintButton(next);
        }

        public void SetPointer(Vector3 origin, Vector3 point, bool visible, bool pressed)
        {
            if (!_built && !visible) return;
            EnsureBuilt();
            _pointerPressed = pressed;
            SetMaterialColor(_pointerMaterial, pressed ? goldColor : cyanColor);

            _pointer.gameObject.SetActive(visible);
            _reticle.gameObject.SetActive(visible);

            if (!visible)
                return;

            _pointer.SetPosition(0, origin);
            _pointer.SetPosition(1, point);

            Vector3 direction = point - origin;
            direction = direction.sqrMagnitude > 0.00000001f
                ? direction.normalized
                : Vector3.forward;

            Vector3 up = Mathf.Abs(Vector3.Dot(direction, Vector3.up)) > 0.98f
                ? Vector3.right
                : Vector3.up;

            // 准星位于命中点的观察者一侧，避免与画布共面闪烁。
            _reticle.SetPositionAndRotation(
                point - direction * 0.0007f,
                Quaternion.LookRotation(direction, up));
        }

        public void PlayCue(bool accepted)
        {
            if (!audioFeedback || !Application.isPlaying)
                return;

            EnsureBuilt();
            EnsureAudio();

            Vector3 position = transform.position;
            if (_panel.gameObject.activeInHierarchy)
                position = _panel.position;
            else if (_launcher.gameObject.activeInHierarchy)
                position = _launcher.position;
            else if (_decoration.gameObject.activeInHierarchy)
                position = _decoration.position;

            _audioSource.transform.position = position;
            _audioSource.volume = Mathf.Clamp01(cueVolume);

            // 不叠加无上限的音源或声音。
            _audioSource.Stop();
            _audioSource.clip = accepted ? _acceptedClip : _rejectedClip;
            _audioSource.Play();
        }

        private void Build()
        {
            // 不挂在手、头或本组件的缩放层级下：
            // 两个画布可以独立固定于世界，且物理尺寸不受祖先缩放影响。
            _ownedRoot = NewObject("腕部界面 · 自有世界根", null, false);
            _ownedRoot.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            _ownedRoot.transform.localScale = Vector3.one;

            Scene ownerScene = gameObject.scene;
            if (ownerScene.IsValid() && ownerScene.isLoaded)
                SceneManager.MoveGameObjectToScene(_ownedRoot, ownerScene);

            _ownedRoot.SetActive(false);

            Shader lineShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (lineShader == null)
                lineShader = Shader.Find("Unlit/Color");

            if (lineShader != null)
            {
                _cyanMaterial = NewLineMaterial(lineShader, "腕饰 · 青");
                _violetMaterial = NewLineMaterial(lineShader, "腕饰 · 紫");
                _goldMaterial = NewLineMaterial(lineShader, "腕饰 · 金");
                _pointerMaterial = NewLineMaterial(lineShader, "指向 · 灵光");
            }
            else
            {
                Debug.LogWarning(
                    "WristMenuView：未找到 URP/Unlit 或 Unlit/Color；线条装饰不可见，画布仍可使用。",
                    this);
            }

            BuildLauncher();
            BuildPanel();
            _openingHint = NewCanvas("抬腕提示与进度",600,120,19);
            AddPlate(_openingHint,600,120);
            _openingText = AddText("抬腕提示",_openingHint,300,40,560,64,"",28,TextAnchor.MiddleCenter,Ink.Text);
            AddImage("开启进度底轨",_openingHint,300,92,520,6,Ink.Cyan,.2f);
            _openingFill = AddImage("开启进度",_openingHint,300,92,520,6,Ink.Cyan).rectTransform;
            _openingFill.pivot = new Vector2(0,.5f); _openingFill.anchoredPosition = new Vector2(40,-92);
            _openingHint.gameObject.SetActive(false);
            BuildDecoration();
            BuildPointer();

            _launcher.gameObject.SetActive(false);
            _panel.gameObject.SetActive(false);
            _decoration.gameObject.SetActive(false);
            _pointer.gameObject.SetActive(false);
            _reticle.gameObject.SetActive(false);
            _ownedRoot.SetActive(isActiveAndEnabled);
        }

        private void BuildLauncher()
        {
            _launcher = NewCanvas("术式启动器", LauncherWidth, LauncherHeight, 20);
            AddPlate(_launcher, LauncherWidth, LauncherHeight);

            AddText("启动器标题", _launcher, 202f, 35f, 340f, 54f,
                "腕上术式", 30, TextAnchor.MiddleLeft, Ink.Text);
            AddText("抬腕提示", _launcher, 545f, 35f, 290f, 40f,
                "再点左腕饰关闭", 24, TextAnchor.MiddleRight, Ink.Muted);

            AddButton(_launcher, "seal", 134f, 149f, 210f, 150f, "结印", "手印", 40);
            AddButton(_launcher, "glyph", 360f, 149f, 210f, 150f, "画符", "符纹", 40);
            AddButton(_launcher, "voice", 586f, 149f, 210f, 150f, "咒语", "言灵", 40);

            AddText("交互提示", _launcher, 360f, 263f, 660f, 42f,
                "右手食指轻触 · 远指捏合", 24, TextAnchor.MiddleCenter, Ink.Muted);
        }

        public void SetPanelPose(Pose pose) { if(_panel) Place(_panel,pose); }
        private void BuildPanel()
        {
            _panel = NewCanvas("独立术式面板", PanelWidth, PanelHeight, 30);
            AddPlate(_panel, PanelWidth, PanelHeight);

            _panelTitle = AddText("标题", _panel, 270f, 72f, 430f, 68f,
                string.Empty, 44, TextAnchor.MiddleLeft, Ink.Text);

            AddButton(_panel,"follow",650f,72f,290f,72f,"跟随 / 固定",null,27);
            AddButton(_panel,"drag",950f,72f,260f,72f,"捏住拖动",null,27);
            AddImage("标题分隔线", _panel, 560f, 128f, 1000f, 2f, Ink.Cyan, 0.35f);

            // 固定页高，不自行翻页、不滚动、不修改父系统提供的内容。
            _panelBody = AddText("分页正文", _panel, 560f, 366f, 1000f, 444f,
                string.Empty, 36, TextAnchor.UpperLeft, Ink.Text);
            _panelBody.lineSpacing = 1.05f;

            _panelFooter = AddText("页脚", _panel, 560f, 628f, 1000f, 80f,
                string.Empty, 25, TextAnchor.MiddleLeft, Ink.Muted);

            AddImage("操作分隔线", _panel, 560f, 672f, 1000f, 2f, Ink.Violet, 0.35f);

            AddButton(_panel, "previous", 179f, 731f, 240f, 100f, "上一页", null, 32);
            AddButton(_panel, "next", 433f, 731f, 240f, 100f, "下一页", null, 32);
            AddButton(_panel, "relocate", 687f, 731f, 240f, 100f, "重新定位", null, 32);
            AddButton(_panel, "close", 941f, 731f, 240f, 100f, "关闭", null, 32);
        }

        private RectTransform NewCanvas(string name, float width, float height, int order)
        {
            GameObject go = NewObject(name, _ownedRoot.transform, true);
            RectTransform rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(width, height);

            Canvas canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.pixelPerfect = false;
            canvas.overrideSorting = true;
            canvas.sortingOrder = order;

            // 不添加 Button、GraphicRaycaster、EventSystem 或任何碰撞体。
            return rect;
        }

        private void AddPlate(RectTransform parent, float width, float height)
        {
            Image shadow = AddImage("轻薄背影", parent,
                width * 0.5f + 5f, height * 0.5f + 7f,
                width + 8f, height + 8f, Ink.Plate, 0.5f);
            shadow.rectTransform.localPosition += Vector3.forward * 0.5f;

            AddImage("青色细边", parent, width * 0.5f, height * 0.5f,
                width, height, Ink.Cyan, 0.38f);
            AddImage("深色内板", parent, width * 0.5f, height * 0.5f,
                width - 4f, height - 4f, Ink.Plate);

            AddImage("左上金纹", parent, 66f, 2f, 128f, 3f, Ink.Gold, 0.85f);
            AddImage("右下紫纹", parent, width - 82f, height - 2f,
                160f, 3f, Ink.Violet, 0.8f);

            Image diamond = AddImage("角部菱印", parent,
                width - 13f, 13f, 7f, 7f, Ink.Gold, 0.9f);
            diamond.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        }

        private void AddButton(
            RectTransform parent, string id, float x, float y, float width, float height,
            string label, string caption, int fontSize)
        {
            RectTransform rect = NewRect("目标 · " + id, parent, x, y, width, height);
            Image background = rect.gameObject.AddComponent<Image>();
            background.raycastTarget = false;

            Outline outline = rect.gameObject.AddComponent<Outline>();
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            outline.useGraphicAlpha = false;

            AddImage("青色侧纹", rect, 3f, height * 0.5f,
                3f, height - 24f, Ink.Cyan, 0.45f);

            bool hasCaption = !string.IsNullOrEmpty(caption);
            Text labelText = AddText("按钮文字", rect,
                width * 0.5f, hasCaption ? 58f : height * 0.5f - 4f,
                width - 28f, hasCaption ? 58f : height - 26f,
                label, fontSize, TextAnchor.MiddleCenter, Ink.Text, false);

            Text captionText = null;
            if (hasCaption)
            {
                captionText = AddText("按钮副文", rect, width * 0.5f, 105f,
                    width - 28f, 42f, caption, 22,
                    TextAnchor.MiddleCenter, Ink.Muted, false);
            }

            Image track = AddImage("进度底轨", rect, width * 0.5f, height - 12f,
                width - 28f, 4f, Ink.Cyan, 0.13f);

            RectTransform fill = NewRect("悬停进度", track.rectTransform, 0f, 0f, 0f, 0f);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = Vector2.one;
            fill.pivot = Vector2.zero;
            fill.offsetMin = fill.offsetMax = Vector2.zero;

            Image fillImage = fill.gameObject.AddComponent<Image>();
            fillImage.raycastTarget = false;
            fill.gameObject.SetActive(false);

            ButtonVisual visual = new ButtonVisual
            {
                id = id,
                rect = rect,
                background = background,
                outline = outline,
                label = labelText,
                caption = captionText,
                progress = fill,
                progressImage = fillImage
            };

            _buttons.Add(visual);
            _targets.Add(new WristUiTarget(id, rect));
        }

        private void BuildDecoration()
        {
            _decoration = NewObject("腕饰 · 同心封纹", _ownedRoot.transform, false).transform;

            AddRing("外环", _decoration, 0.019f, 0.00055f, _cyanMaterial, 64);
            AddRing("内环", _decoration, 0.014f, 0.00035f, _violetMaterial, 48);

            LineRenderer diamond = NewLine("金色菱纹", _decoration, _goldMaterial, 0.0005f, 4);
            diamond.loop = true;
            diamond.SetPosition(0, new Vector3(0f, 0.026f, 0f));
            diamond.SetPosition(1, new Vector3(0.026f, 0f, 0f));
            diamond.SetPosition(2, new Vector3(0f, -0.026f, 0f));
            diamond.SetPosition(3, new Vector3(-0.026f, 0f, 0f));

            AddRing("中央微印", _decoration, 0.0035f, 0.00045f, _goldMaterial, 24);
        }

        private void BuildPointer()
        {
            _pointer = NewLine("世界指向线", _ownedRoot.transform, _pointerMaterial, 0.0008f, 2);
            _pointer.useWorldSpace = true;
            _pointer.sortingOrder = 40;

            _reticle = NewObject("命中准星", _ownedRoot.transform, false).transform;
            LineRenderer ring = AddRing("准星细环", _reticle,
                0.003f, 0.00055f, _pointerMaterial, 32);
            ring.sortingOrder = 41;

            LineRenderer center = NewLine("准星中心", _reticle, _pointerMaterial, 0.0006f, 2);
            center.SetPosition(0, new Vector3(-0.0008f, 0f, 0f));
            center.SetPosition(1, new Vector3(0.0008f, 0f, 0f));
            center.sortingOrder = 41;
        }

        private LineRenderer AddRing(
            string name, Transform parent, float radius, float width, Material material, int count)
        {
            LineRenderer line = NewLine(name, parent, material, width, count);
            line.loop = true;
            for (int i = 0; i < count; i++)
            {
                float angle = i * (Mathf.PI * 2f / count);
                line.SetPosition(i, new Vector3(
                    Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
            }
            return line;
        }

        private LineRenderer NewLine(
            string name, Transform parent, Material material, float width, int count)
        {
            GameObject go = NewObject(name, parent, false);
            LineRenderer line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.enabled = material != null;
            line.useWorldSpace = false;
            line.positionCount = count;
            line.startWidth = line.endWidth = width;
            line.startColor = line.endColor = Color.white;
            line.alignment = LineAlignment.View;
            line.numCapVertices = 3;
            line.numCornerVertices = 3;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.lightProbeUsage = LightProbeUsage.Off;
            line.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return line;
        }

        private Material NewLineMaterial(Shader shader, string name)
        {
            Material material = new Material(shader)
            {
                name = name,
                hideFlags = HideFlags.HideAndDontSave
            };

            // 线条刻意使用不透明 Unlit；画布透明由 Unity UI 默认材质处理。
            if (material.HasProperty("_Surface"))
                material.SetFloat("_Surface", 0f);
            if (material.HasProperty("_SrcBlend"))
                material.SetFloat("_SrcBlend", (float)BlendMode.One);
            if (material.HasProperty("_DstBlend"))
                material.SetFloat("_DstBlend", (float)BlendMode.Zero);
            if (material.HasProperty("_ZWrite"))
                material.SetFloat("_ZWrite", 1f);
            if (material.HasProperty("_Cull"))
                material.SetFloat("_Cull", (float)CullMode.Off);

            material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Geometry;
            material.enableInstancing = true;
            return material;
        }

        private static void SetMaterialColor(Material material, Color color)
        {
            if (material == null)
                return;

            color.a = 1f;
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);
        }

        private void RefreshStyle()
        {
            float scale = canvasScale;
            if (float.IsNaN(scale) || float.IsInfinity(scale))
                scale = 0.0005f;
            scale = Mathf.Max(0.0001f, scale);

            Vector3 worldScale = Vector3.one * scale;
            if (_launcher.localScale != worldScale)
                _launcher.localScale = worldScale;
            if (_panel.localScale != worldScale)
                _panel.localScale = worldScale;
            if (_openingHint.localScale != worldScale) _openingHint.localScale = worldScale;

            Font resolved = ResolveFont();
            for (int i = 0; i < _texts.Count; i++)
            {
                Text text = _texts[i];
                if (text != null && text.font != resolved)
                    text.font = resolved;
            }

            for (int i = 0; i < _graphics.Count; i++)
            {
                GraphicBinding binding = _graphics[i];
                if (binding.graphic == null)
                    continue;

                Color color = ResolveInk(binding.ink);
                color.a *= binding.alpha;
                if (binding.graphic.color != color)
                    binding.graphic.color = color;
            }

            if (_hovered != null && !_hovered.rect.gameObject.activeInHierarchy)
            {
                _hovered = null;
                _hoverProgress = 0f;
            }

            for (int i = 0; i < _buttons.Count; i++)
                PaintButton(_buttons[i]);

            SetMaterialColor(_cyanMaterial, cyanColor);
            SetMaterialColor(_violetMaterial, violetColor);
            SetMaterialColor(_goldMaterial, goldColor);
            SetMaterialColor(_pointerMaterial, _pointerPressed ? goldColor : cyanColor);

            if (!audioFeedback && _audioSource != null && _audioSource.isPlaying)
                _audioSource.Stop();
        }

        private Font ResolveFont()
        {
            if (font != null)
                return font;

            if (!_fallbackResolved)
            {
                _fallbackResolved = true;
                _fallbackFont = Resources.Load<Font>("Fonts/NotoSansCJKsc-Regular");
                if (_fallbackFont == null)
                    _fallbackFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
            return _fallbackFont;
        }

        private Color ResolveInk(Ink ink)
        {
            switch (ink)
            {
                case Ink.Plate: return plateColor;
                case Ink.Muted: return mutedTextColor;
                case Ink.Cyan: return cyanColor;
                case Ink.Violet: return violetColor;
                case Ink.Gold: return goldColor;
                default: return textColor;
            }
        }

        private void PaintButton(ButtonVisual button)
        {
            bool hovered = button == _hovered;
            Color background = hovered
                ? Color.Lerp(buttonColor, cyanColor, 0.18f)
                : buttonColor;
            background.a = buttonColor.a;

            if (button.background.color != background)
                button.background.color = background;

            Color edge = cyanColor;
            edge.a *= hovered ? 0.95f : 0.28f;
            if (button.outline.effectColor != edge)
                button.outline.effectColor = edge;

            Color labelColor = hovered ? cyanColor : textColor;
            if (button.label.color != labelColor)
                button.label.color = labelColor;

            if (button.caption != null && button.caption.color != mutedTextColor)
                button.caption.color = mutedTextColor;

            Color progressColor = _hoverProgress >= 1f ? goldColor : cyanColor;
            if (button.progressImage.color != progressColor)
                button.progressImage.color = progressColor;

            float progress = hovered ? _hoverProgress : 0f;
            Vector2 anchorMax = new Vector2(progress, 1f);
            if (button.progress.anchorMax != anchorMax)
                button.progress.anchorMax = anchorMax;

            bool showProgress = progress > 0f;
            if (button.progress.gameObject.activeSelf != showProgress)
                button.progress.gameObject.SetActive(showProgress);
        }

        private void ClearHoverWithin(Transform container)
        {
            if (_hovered == null || !_hovered.rect.IsChildOf(container))
                return;

            ButtonVisual previous = _hovered;
            _hovered = null;
            _hoverProgress = 0f;
            PaintButton(previous);
        }

        private Image AddImage(
            string name, RectTransform parent, float x, float y, float width, float height,
            Ink ink, float alpha = 1f)
        {
            RectTransform rect = NewRect(name, parent, x, y, width, height);
            Image image = rect.gameObject.AddComponent<Image>();
            image.raycastTarget = false;
            _graphics.Add(new GraphicBinding { graphic = image, ink = ink, alpha = alpha });
            return image;
        }

        private Text AddText(
            string name, RectTransform parent, float x, float y, float width, float height,
            string value, int size, TextAnchor alignment, Ink ink, bool bindColor = true)
        {
            RectTransform rect = NewRect(name, parent, x, y, width, height);
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = ResolveFont();
            text.fontSize = size;
            text.fontStyle = FontStyle.Normal;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.resizeTextForBestFit = false;
            text.supportRichText = false;
            text.raycastTarget = false;
            text.text = value;
            _texts.Add(text);

            if (bindColor)
                _graphics.Add(new GraphicBinding { graphic = text, ink = ink, alpha = 1f });
            return text;
        }

        private RectTransform NewRect(
            string name, RectTransform parent, float x, float y, float width, float height)
        {
            RectTransform rect = (RectTransform)NewObject(name, parent, true).transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(x, -y);
            return rect;
        }

        private GameObject NewObject(string name, Transform parent, bool rectTransform)
        {
            GameObject go = rectTransform
                ? new GameObject(name, typeof(RectTransform))
                : new GameObject(name);

            // 每个自建对象均不写入场景或构建；编辑模式临时预览也适用。
            go.hideFlags = HideFlags.DontSave;
            go.layer = gameObject.layer;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            return go;
        }

        private static void Place(Transform target, Pose pose)
        {
            // 不额外旋转 180°：Canvas 可读正面为局部 -Z。
            // 父系统传入的 forward 应由观察者指向画布后方。
            target.SetPositionAndRotation(pose.position, pose.rotation);
        }

        private void EnsureAudio()
        {
            if (_audioSource != null)
                return;

            GameObject go = NewObject("腕饰提示音", _ownedRoot.transform, false);
            _audioSource = go.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.loop = false;
            _audioSource.spatialBlend = 1f;
            _audioSource.dopplerLevel = 0f;
            _audioSource.rolloffMode = AudioRolloffMode.Linear;
            _audioSource.minDistance = 0.15f;
            _audioSource.maxDistance = 3f;

            _acceptedClip = CreateTone("确认 · 清音", true);
            _rejectedClip = CreateTone("未确认 · 低音", false);
        }

        private static AudioClip CreateTone(string name, bool accepted)
        {
            const int sampleRate = 24000;
            float duration = accepted ? 0.11f : 0.085f;
            int count = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[count];

            float startFrequency = accepted ? 660f : 260f;
            float endFrequency = accepted ? 880f : 210f;
            float slope = (endFrequency - startFrequency) / duration;

            for (int i = 0; i < count; i++)
            {
                float time = (float)i / sampleRate;
                float phase = 2f * Mathf.PI *
                    (startFrequency * time + 0.5f * slope * time * time);
                float attack = Mathf.Clamp01(time / 0.008f);
                float decay = Mathf.Clamp01((duration - time) / duration);
                float envelope = attack * decay * decay;
                samples[i] = envelope *
                    (Mathf.Sin(phase) * 0.6f + Mathf.Sin(phase * 2f) * 0.08f);
            }

            AudioClip clip = AudioClip.Create(name, count, 1, sampleRate, false);
            clip.hideFlags = HideFlags.HideAndDontSave;
            clip.SetData(samples, 0);
            return clip;
        }

        private void OnEnable()
        {
            // Managed lists are not restored by a script reload; discard partial old views.
            if (_ownedRoot && (!_built || _targets.Count != 9)) ReleaseVisuals();
            if (_ownedRoot != null)
            {
                _ownedRoot.SetActive(true);
                if (_built)
                    RefreshStyle();
            }
        }

        private void OnDisable()
        {
            if (!Application.isPlaying) { ReleaseVisuals(); return; }
            // 组件整体禁用时暂停全部视觉；保留各面板自身的 activeSelf。
            if (_ownedRoot != null)
                _ownedRoot.SetActive(false);

            if (_audioSource != null)
                _audioSource.Stop();
        }

        private void OnDestroy() => ReleaseVisuals();

        /// <summary>Owns only generated objects/materials/clips. Idempotent for preview teardown and script reload.</summary>
        public void ReleaseVisuals(bool immediately = false)
        {
            if (_audioSource != null)
            {
                _audioSource.Stop();
                _audioSource.clip = null;
            }

            if (_ownedRoot != null)
                _ownedRoot.SetActive(false);

            DisposeOwned(_ownedRoot,immediately);
            DisposeOwned(_cyanMaterial,immediately);
            DisposeOwned(_violetMaterial,immediately);
            DisposeOwned(_goldMaterial,immediately);
            DisposeOwned(_pointerMaterial,immediately);
            DisposeOwned(_acceptedClip,immediately);
            DisposeOwned(_rejectedClip,immediately);
            _ownedRoot=null; _audioSource=null;
            _cyanMaterial=_violetMaterial=_goldMaterial=_pointerMaterial=null;
            _acceptedClip=_rejectedClip=null;
            _launcher=_panel=_openingHint=_openingFill=null;
            _panelTitle=_panelBody=_panelFooter=_openingText=null;
            _decoration=_reticle=null; _pointer=null;

            // UI / LineRenderer 的内部网格由对应组件管理。
            // 不销毁用户字体、Resources 字体、内置字体或 Unity 默认 UI 材质。
            _targets.Clear();
            _buttons.Clear();
            _graphics.Clear();
            _texts.Clear();
            _hovered = null;
            _built = false;
            _styleDirty = true;
        }

        private static void DisposeOwned(Object value,bool immediately = false)
        {
            if (value == null)
                return;

            if (Application.isPlaying && !immediately)
                Object.Destroy(value);
            else
                Object.DestroyImmediate(value);
        }
    }
}
