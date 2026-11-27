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
        [InspectorName("确认音效（可选）")] public AudioClip acceptedSound;
        [InspectorName("拒绝音效（可选）")] public AudioClip rejectedSound;
        public int AudioCueCount { get; private set; }

        [Header("字体与世界尺寸")]
        [InspectorName("中文字体（留空自动回退）")]
        public Font font;

        [InspectorName("画布单位比例（米／像素）")]
        [Min(0.0001f)]
        public float canvasScale = 0.0005f;

        [Header("天工金石腕匣配色")]
        [InspectorName("统一采用天工金石图集与配色"), Tooltip("忽略旧场景保留的赛博蓝紫序列化色值，统一呈现青铜错金与汉白温玉风格。")]
        public bool useKaogongTheme = true;

        [InspectorName("玄漆底板色")]
        public Color plateColor = KaogongTheme.LacquerDark;

        [InspectorName("圭牌按钮底色")]
        public Color buttonColor = KaogongTheme.SlatePanel;

        [InspectorName("温玉正文色")]
        public Color textColor = KaogongTheme.JadeWarm;

        [InspectorName("素缣辅助文字色")]
        public Color mutedTextColor = KaogongTheme.SilkMuted;

        [InspectorName("青玉灵光色")]
        public Color cyanColor = KaogongTheme.JadeAccent;

        [InspectorName("老青铜点缀色")]
        public Color violetColor = KaogongTheme.BronzeAged;

        [InspectorName("鎏金错金点缀")]
        public Color goldColor = KaogongTheme.GoldInlay;

        [Header("可选提示音")]
        [InspectorName("启用非语言提示音")]
        public bool audioFeedback = true;

        [InspectorName("提示音音量")]
        [Range(0f, 1f)]
        public float cueVolume = 0.18f;

        public IReadOnlyList<WristUiTarget> Targets => _targets;
        public GameObject OwnedRoot => _ownedRoot;

        private const float LauncherWidth = 720f;
        private const float LauncherHeight = 650f;
        private const float PanelWidth = 1120f;
        private const float PanelHeight = 824f;
        private const int TargetCount = 16;
        private const float GlanceWidth = 516f;
        private const float GlanceHeight = 276f;

        private enum Ink
        {
            Plate,
            Text,
            Muted,
            Cyan,
            Violet,
            Gold,
            Lacquer,
            Brass,
            InkOnJade,
            JadeAccent
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
            public Image track;
            public RectTransform progress;
            public Image progressImage;
            public Text label;
            public Text caption;
            public Image emblem;
        }

        private readonly List<WristUiTarget> _targets = new List<WristUiTarget>(TargetCount);
        private readonly List<ButtonVisual> _buttons = new List<ButtonVisual>(TargetCount);
        private readonly List<GraphicBinding> _graphics = new List<GraphicBinding>(64);
        private readonly List<Text> _texts = new List<Text>(32);

        private GameObject _ownedRoot;
        private RectTransform _launcher;
        private RectTransform _panel;
        private RectTransform _statusPage, _rightGlance;
        private SkillStatusRows _pageRows, _glanceRows;
        private ButtonVisual _previous, _next, _glanceToggle, _sealHintsToggle, _exploration, _combatMovement, _aimAssist;
        private Text _emptyStatus;
        public bool RightHandStatusVisible => _rightGlance && _rightGlance.gameObject.activeInHierarchy;
        private RectTransform _microphoneRetry;
        private RectTransform _openingHint, _openingFill;
        private Text _openingText;
        private Transform _decoration;
        private Transform _reticle;
        private Image _reticleImage;
        private LineRenderer _pointer;
        private Text _launcherVitals, _launcherProgress;
        private string _launcherIdleHint;
        private int shownHp, shownMp;
        private Text _panelTitle;
        private Text _panelBody;
        private Text _panelFooter;
        private RectTransform _lessonSurface, _statusMark, _statusHollow;
        private Text _lessonState, _lessonMethod, _lessonCost, _lessonVoiceLabel, _lessonVoice, _lessonAction, _sealCaption, _sealSequence, _wordFace;
        private RawImage _sealArt;
        private readonly Dictionary<string, Texture> _sealTextures = new();

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

        private Sprite _frameBronze;
        private Sprite _frameJade;
        private Sprite _buttonNormal;
        private Sprite _buttonHover;
        private Sprite _gaugeTrack;
        private Sprite _gaugeFill;
        private Sprite _dividerGold;
        private Sprite _reticleSprite;

        public void EnsureBuilt()
        {
            if (!_built || !_ownedRoot || _targets.Count != TargetCount)
            {
                ReleaseVisuals();
                try { Build(); _built = true; }
                catch { ReleaseVisuals(); throw; }
            }

            if (_styleDirty) { RefreshStyle(); _styleDirty = false; }
        }

        private void ResolveAtlasSprites()
        {
            _frameBronze = KaogongUISprites.Get("Frame_Bronze_Main");
            _frameJade = KaogongUISprites.Get("Frame_Jade_Plaque");
            _buttonNormal = KaogongUISprites.Get("Button_Normal");
            _buttonHover = KaogongUISprites.Get("Button_Hover");
            _gaugeTrack = KaogongUISprites.Get("Gauge_Track");
            _gaugeFill = KaogongUISprites.Get("Gauge_Fill_Inner") ?? KaogongUISprites.Get("Gauge_Fill");
            _dividerGold = KaogongUISprites.Get("Divider_Gold");
            _reticleSprite = KaogongUISprites.Get("Reticle_Fangyuan");
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

        /// <summary>控制抬腕开启提示的呈现，与首章剧情无关；隐藏时不隐式创建 UI。</summary>
        public void SetWristActivationHint(Pose pose, string hint, float progress, bool visible)
        {
            if (!_built && !visible) return;
            EnsureBuilt();
            _openingHint.gameObject.SetActive(visible);
            if (!visible) return;
            Place(_openingHint, pose);
            if (_openingText.text != hint) _openingText.text = hint;
            if (_openingFill.TryGetComponent<Image>(out var fillImg) && fillImg.type == Image.Type.Filled)
                fillImg.fillAmount = Mathf.Clamp01(progress);
            else
                _openingFill.localScale = new Vector3(Mathf.Clamp01(progress), 1f, 1f);
        }

        public void HideLauncher()
        {
            if (!_built) return;
            ClearHoverWithin(_launcher);
            _launcher.gameObject.SetActive(false);
        }

        public void ShowPanel(Pose pose, string title, string body, string footer)
        {
            EnsureBuilt();
            SetStatusPage(false);
            SetMicrophoneRetry(false);
            _lessonSurface.gameObject.SetActive(false);
            _panelBody.gameObject.SetActive(true);
            _panelTitle.text = title ?? string.Empty;
            _panelBody.text = body ?? string.Empty;
            _panelFooter.text = footer ?? string.Empty;
            Place(_panel, pose);
            _panel.gameObject.SetActive(true);
        }

        /// <summary>显示已解析的教学条目和通用技能状态，不推断奖励物品或章节业务。</summary>
        internal void ShowLesson(Pose pose, WristTeachingLesson lesson, int step, bool voiceIndex,
            bool discovered, bool available, bool permanent, bool savePending, string footer)
        {
            EnsureBuilt();
            SetStatusPage(false);
            SetMicrophoneRetry(false);
            _panelBody.gameObject.SetActive(false);
            _lessonSurface.gameObject.SetActive(true);
            _panelTitle.text = lesson.Name;
            _panelFooter.text = footer;

            var reference = voiceIndex && lesson.Voice ? lesson.Voice : lesson.Reference;
            bool hasImage = lesson.Seals.Count > 0 && (!voiceIndex || reference.triggerMode == SkillTriggerMode.VoiceAndGesture);
            _lessonState.text = permanent ? "已解锁" : available ? "临时可用" :
                discovered ? "已了解\n待解锁" : "尚未了解";
            if (savePending) _lessonState.text += "\n待存档";
            _statusMark.localRotation = Quaternion.Euler(0, 0, permanent ? 45 : 0);
            _statusHollow.gameObject.SetActive(!available);
            _lessonMethod.text = lesson.HasAlternative ? "结印 或 念咒" : reference.triggerMode == SkillTriggerMode.VoiceAndGesture ? "结印 ＋ 念咒" : hasImage ? "双手结印" : "念出口令";
            _lessonCost.text = reference.effectDefinition is BreathEffectDefinition { mode: BreathCastMode.Sustained } breath
                ? $"度力  {reference.manaCost:0.#}＋{breath.manaPerSecond:0.#}/秒\n停喷冷却  {breath.minCooldownSeconds:0.#}–{breath.maxCooldownSeconds:0.#} 秒"
                : $"度力  {reference.manaCost:0.#}\n冷却  {reference.cooldownSeconds:0.#} 秒";
            _lessonVoiceLabel.text = hasImage && lesson.Voice ? (lesson.HasAlternative ? "另一种施放方式" : "配合口令") : hasImage ? "施放" : "施放条件";
            _lessonVoice.text = hasImage && lesson.Voice ? lesson.Voice.phrase : hasImage ? "保持手印" : lesson.Gesture && !lesson.HasAlternative ? SealName(lesson.Seals[0]) : "完整念出";
            _lessonAction.text = !string.IsNullOrEmpty(lesson.ActionHint) ? lesson.ActionHint : lesson.HasAlternative ? "两种方式任选其一" : "";
            string name = hasImage ? lesson.Seals[step] : null;
            Texture texture = hasImage ? LoadSeal(name) : null;
            _sealArt.texture = texture;
            _sealArt.gameObject.SetActive(texture);
            if (texture)
            {
                float ratio = (float)texture.width / texture.height;
                _sealArt.rectTransform.sizeDelta = ratio > 420f / 300f ? new Vector2(420, 420 / ratio) : new Vector2(300 * ratio, 300);
            }
            _wordFace.gameObject.SetActive(!texture);
            _wordFace.text = hasImage ? SealName(name) : lesson.Voice ? lesson.Voice.phrase : "";
            _sealCaption.text = hasImage ? SealName(name) : "口令";
            _sealSequence.text = hasImage && lesson.Seals.Count > 1 ? Sequence(lesson.Seals, step) : "";
            Place(_panel, pose);
            _panel.gameObject.SetActive(true);
        }

        static string SealName(string value) => string.IsNullOrEmpty(value) || value.EndsWith("印") ? value : value + "印";

        static string Sequence(IReadOnlyList<string> seals, int active)
        {
            var s = new System.Text.StringBuilder();
            int first = Mathf.Clamp(active - 1, 0, Mathf.Max(0, seals.Count - 3));
            if (first > 0) s.Append("…  ");
            for (int i = first; i < Mathf.Min(seals.Count, first + 3); i++)
            {
                if (i > first) s.Append("  →  ");
                if (i == active) s.Append('【');
                s.Append(SealName(seals[i]));
                if (i == active) s.Append('】');
            }
            if (first + 3 < seals.Count) s.Append("  …");
            return s.ToString();
        }

        Texture LoadSeal(string seal)
        {
            string key = (seal ?? string.Empty).Trim();
            if (_sealTextures.TryGetValue(key, out var result)) return result;
            if (key.Length > 0) result = Resources.Load<Texture2D>("Teaching/Seals/" + key);
            _sealTextures[key] = result;
            return result;
        }

        /// <summary>Transient player-facing recovery prompt; no diagnostics or raw error details.</summary>
        public void SetMicrophoneRetry(bool visible)
        {
            if (!_microphoneRetry) return;
            if (!visible) ClearHoverWithin(_microphoneRetry);
            _microphoneRetry.gameObject.SetActive(visible);
            _panelFooter.gameObject.SetActive(!visible);
        }

        public void HidePanel()
        {
            if (!_built) return;
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
            SetMaterialColor(_pointerMaterial, pressed ? ThemeGold() : ThemeAccent());

            _pointer.gameObject.SetActive(visible);
            _reticle.gameObject.SetActive(visible);

            if (!visible) return;

            _pointer.SetPosition(0, origin);
            _pointer.SetPosition(1, point);

            Vector3 direction = point - origin;
            direction = direction.sqrMagnitude > 0.00000001f ? direction.normalized : Vector3.forward;

            Vector3 up = Mathf.Abs(Vector3.Dot(direction, Vector3.up)) > 0.98f ? Vector3.right : Vector3.up;
            _reticle.SetPositionAndRotation(point - direction * 0.0008f, Quaternion.LookRotation(direction, up));
            if (_reticleImage)
            {
                _reticleImage.color = pressed ? KaogongTheme.Cinnabar : Color.white;
                _reticle.localScale = Vector3.one * (pressed ? 0.000088f : 0.000072f);
            }
        }

        public void PlayCue(bool accepted)
        {
            if (!audioFeedback || !Application.isPlaying) return;

            EnsureBuilt();

            Vector3 position = transform.position;
            if (_panel.gameObject.activeInHierarchy) position = _panel.position;
            else if (_launcher.gameObject.activeInHierarchy) position = _launcher.position;
            else if (_decoration.gameObject.activeInHierarchy) position = _decoration.position;

            EnsureAudio();
            _audioSource.transform.position = position;
            _audioSource.volume = Mathf.Clamp01(cueVolume);

            _audioSource.Stop();
            _audioSource.clip = accepted ? (acceptedSound ? acceptedSound : _acceptedClip) :
                (rejectedSound ? rejectedSound : _rejectedClip);
            AudioCueCount++;
            _audioSource.Play();
        }

        private void Build()
        {
            ResolveAtlasSprites();

            _ownedRoot = NewObject("腕部界面 · 自有世界根", null, false);
            _ownedRoot.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            _ownedRoot.transform.localScale = Vector3.one;

            Scene ownerScene = gameObject.scene;
            if (ownerScene.IsValid() && ownerScene.isLoaded)
                SceneManager.MoveGameObjectToScene(_ownedRoot, ownerScene);

            _ownedRoot.SetActive(false);

            Shader lineShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (lineShader == null) lineShader = Shader.Find("Unlit/Color");

            if (lineShader != null)
            {
                _cyanMaterial = NewLineMaterial(lineShader, "腕饰 · 青玉");
                _violetMaterial = NewLineMaterial(lineShader, "腕饰 · 古铜");
                _goldMaterial = NewLineMaterial(lineShader, "腕饰 · 鎏金");
                _pointerMaterial = NewLineMaterial(lineShader, "指向 · 错金");
            }
            else
            {
                Debug.LogWarning("WristMenuView：未找到 URP/Unlit 或 Unlit/Color；线条装饰不可见，画布仍可使用。", this);
            }

            BuildLauncher();
            BuildPanel();

            _openingHint = NewCanvas("抬腕提示与进度", 600f, 134f, 19);
            AddPlate(_openingHint, 600f, 134f, 1.8f);
            _openingText = AddText("抬腕提示", _openingHint, 300f, 44f, 520f, 56f, "", 27, TextAnchor.MiddleCenter, Ink.Text);
            AddProgressBar("开启进度", _openingHint, 300f, 96f, 480f, 14f, out _openingFill);
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
            AddPlate(_launcher, LauncherWidth, LauncherHeight, 1.45f);

            bool usesCase = TryGetComponent<WristMenuController>(out var controller) && controller.wornDevice;
            AddText("启动器标题", _launcher, 166f, 50f, 210f, 60f,
                usesCase ? "准望匣" : "腕上术式", 32, TextAnchor.MiddleLeft, Ink.Text);
            _launcherIdleHint = usesCase ? "再按朱砂扣收起" : "再点左腕饰关闭";
            _launcherProgress = AddText("腕匣课数与铜叶", _launcher, 472f, 50f, 388f, 46f,
                _launcherIdleHint, 23, TextAnchor.MiddleRight, Ink.Gold);

            AddDivider("标题错金线", _launcher, LauncherWidth * 0.5f, 80f, 616f, 20f);

            float btnY = 172f;
            float btnW = 148f;
            float btnH = 152f;
            AddButton(_launcher, "seal",   124f, btnY, btnW, btnH, "术式教学", null, 25, "Icon_Seals");
            AddButton(_launcher, "glyph",  281f, btnY, btnW, btnH, "画符", null, 28, "Icon_GlyphBrush");
            AddButton(_launcher, "voice",  439f, btnY, btnW, btnH, "咒语", null, 28, "Icon_VoiceBell");
            AddButton(_launcher, "status", 596f, btnY, btnW, btnH, "术法状态", null, 25, "Icon_StatusWheel");

            if (usesCase)
                _launcherVitals = AddText("气血与度力 · 大字读数", _launcher, 360f, 280f, 620f, 56f,
                    "", 38, TextAnchor.MiddleCenter, Ink.Text);
            else
                AddText("交互提示", _launcher, 360f, 280f, 620f, 40f,
                    "右食指点按圭牌 · 远距射线轻捏", 23, TextAnchor.MiddleCenter, Ink.Muted);

            _exploration = AddButton(_launcher, "exploration", 360f, 354f, 620f, 76f, "探索移动", null, 28);
            _combatMovement = AddButton(_launcher, "combat-movement", 360f, 440f, 620f, 76f, "战斗移动", null, 28);
            _aimAssist = AddButton(_launcher, "aim-assist", 360f, 526f, 620f, 76f, "轻辅助瞄准", null, 28);
            movementHelp = AddText("当前移动操作", _launcher, 360f, 600f, 620f, 66f, "", 21, TextAnchor.MiddleCenter, Ink.Muted);
        }

        internal void SetAimAssist(bool enabled)
        {
            _aimAssist.label.text = enabled ? "轻辅助瞄准 · 开" : "轻辅助瞄准 · 关";
        }

        private Text movementHelp;
        private string shownExplorationMode, shownCombatMode;

        internal void SetMovementChoices(RpgLocomotionController movement)
        {
            bool available = movement && movement.UsesEncounterModes;
            if (_exploration.rect.gameObject.activeSelf != available)
            {
                _exploration.rect.gameObject.SetActive(available);
                _combatMovement.rect.gameObject.SetActive(available);
            }
            string hint = available ? (movement.EncounterCombat ? movement.CombatHint : movement.ExplorationHint) : "";
            if (movementHelp.text != hint) movementHelp.text = hint;
            if (!available) return;
            if (shownExplorationMode != movement.ExplorationModeName)
                _exploration.label.text = "探索移动 · " + (shownExplorationMode = movement.ExplorationModeName);
            if (shownCombatMode != movement.CombatModeName)
                _combatMovement.label.text = "战斗移动 · " + (shownCombatMode = movement.CombatModeName);
        }

        public void SetLauncherVitals(WristGauge health, WristGauge power)
        {
            if (health && power) SetLauncherVitals(health.Value, power.Value);
        }

        internal void SetLauncherVitals(float health, float mana)
        {
            if (!_launcherVitals) return;
            int hp = Mathf.CeilToInt(health), mp = Mathf.CeilToInt(mana);
            if (hp == shownHp && mp == shownMp && _launcherVitals.text.Length != 0) return;
            shownHp = hp; shownMp = mp;
            _launcherVitals.text = $"气血 {hp}    ·    度力 {mp}";
        }

        internal void SetLauncherProgress(string progress)
        {
            string value = string.IsNullOrEmpty(progress) ? _launcherIdleHint : progress;
            if (_launcherProgress && _launcherProgress.text != value) _launcherProgress.text = value;
        }

        public void SetPanelPose(Pose pose) { if (_panel) Place(_panel, pose); }

        private void SetStatusPage(bool visible)
        {
            _statusPage.gameObject.SetActive(visible);
            _glanceToggle.rect.gameObject.SetActive(visible);
            _sealHintsToggle.rect.gameObject.SetActive(visible);
            _previous.rect.gameObject.SetActive(!visible);
            _next.rect.gameObject.SetActive(!visible);
        }

        internal void ShowSkillStatus(Pose pose, LinearRpgController player, bool enableGlance, bool follow)
        {
            EnsureBuilt();
            SetStatusPage(true);
            SetMicrophoneRetry(false);
            _panelBody.gameObject.SetActive(false);
            _lessonSurface.gameObject.SetActive(false);
            _panelTitle.text = "术法状态";
            _panelFooter.text = follow ? "当前跟随玩家 · 顶部可切固定" : "当前固定于世界 · 顶部可切跟随";
            _glanceToggle.label.text = enableGlance ? "抬右手查看 · 开" : "抬右手查看 · 关";
            _sealHintsToggle.label.text = player && player.skills && player.skills.SealHintsEnabled ? "结印提示 · 开" : "结印提示 · 关";
            _pageRows ??= new SkillStatusRows(_statusPage, 976f, 412f, ResolveFont());
            _emptyStatus.gameObject.SetActive(!_pageRows.Refresh(player));
            Place(_panel, pose);
            _panel.gameObject.SetActive(true);
        }

        internal void ShowRightHandStatus(Pose pose, LinearRpgController player, bool refreshContent)
        {
            EnsureBuilt();
            if (!_rightGlance)
            {
                _rightGlance = NewCanvas("右手术法快览", GlanceWidth, GlanceHeight, 18);
                AddPlate(_rightGlance, GlanceWidth, GlanceHeight, 2.2f);
                var innerArea = NewRect("快览槽区", _rightGlance, GlanceWidth * 0.5f, GlanceHeight * 0.5f, GlanceWidth - 52f, GlanceHeight - 48f);
                _glanceRows = new SkillStatusRows(innerArea, GlanceWidth - 52f, GlanceHeight - 48f, ResolveFont());
                _rightGlance.gameObject.SetActive(false);
                _rightGlance.localScale = _launcher.localScale * 0.84f;
                refreshContent = true;
            }
            Place(_rightGlance, pose);
            if (refreshContent)
                _rightGlance.gameObject.SetActive(_glanceRows.Refresh(player));
        }

        internal void HideRightHandStatus()
        {
            if (_rightGlance && _rightGlance.gameObject.activeSelf) _rightGlance.gameObject.SetActive(false);
        }

        private void BuildPanel()
        {
            _panel = NewCanvas("独立术式面板", PanelWidth, PanelHeight, 30);
            AddPlate(_panel, PanelWidth, PanelHeight, 1.15f);

            _panelTitle = AddText("标题", _panel, 286f, 70f, 412f, 62f,
                string.Empty, 42, TextAnchor.MiddleLeft, Ink.Text);

            AddButton(_panel, "follow", 688f, 70f, 256f, 68f, "跟随 / 固定", null, 25);
            AddButton(_panel, "drag", 954f, 70f, 232f, 68f, "捏住拖动", null, 25);
            AddDivider("标题错金线", _panel, PanelWidth * 0.5f, 118f, 996f, 22f);

            _panelBody = AddText("分页正文", _panel, PanelWidth * 0.5f, 376f, 976f, 444f,
                string.Empty, 35, TextAnchor.UpperLeft, Ink.Text);
            _panelBody.lineSpacing = 1.12f;
            BuildLessonSurface();

            _panelFooter = AddText("页脚", _panel, PanelWidth * 0.5f, 634f, 976f, 68f,
                string.Empty, 24, TextAnchor.MiddleLeft, Ink.Muted);

            _microphoneRetry = NewRect("麦克风恢复提示", _panel, PanelWidth * 0.5f, 634f, 976f, 68f);
            AddText("麦克风状态", _microphoneRetry, 326f, 34f, 636f, 68f,
                "麦克风暂不可用\n若未授权，请在系统允许麦克风后返回", 24, TextAnchor.MiddleLeft, Ink.Muted);
            AddButton(_microphoneRetry, "retry-microphone", 834f, 34f, 268f, 66f, "重试麦克风", null, 27);
            _microphoneRetry.gameObject.SetActive(false);

            AddDivider("操作错金线", _panel, PanelWidth * 0.5f, 680f, 996f, 20f);

            float botY = 740f;
            float botH = 88f;
            _previous        = AddButton(_panel, "previous",     180f, botY, 232f, botH, "上一页", null, 30);
            _next            = AddButton(_panel, "next",         434f, botY, 232f, botH, "下一页", null, 30);
            AddButton(_panel,                    "relocate",     688f, botY, 232f, botH, "重新定位", null, 30);
            AddButton(_panel,                    "close",        942f, botY, 232f, botH, "关闭", null, 30);
            _glanceToggle    = AddButton(_panel, "right-status", 180f, botY, 232f, botH, "右手快览", null, 27);
            _sealHintsToggle = AddButton(_panel, "seal-hints",   434f, botY, 232f, botH, "结印提示", null, 27);

            _statusPage = NewRect("术法状态内容", _panel, PanelWidth * 0.5f, 376f, 976f, 412f);
            _emptyStatus = AddText("空列表", _statusPage, 488f, 206f, 880f, 100f,
                "当前没有可用术法", 38, TextAnchor.MiddleCenter, Ink.Muted);
            SetStatusPage(false);
        }

        private void BuildLessonSurface()
        {
            _lessonSurface = NewRect("铜叶教学卡", _panel, PanelWidth * 0.5f, PanelHeight * 0.5f, PanelWidth, PanelHeight);
            var paper = _lessonSurface;

            // 中央图版：使用九宫格青铜主框作为独立展示拓版，内衬手印照片或口令温玉名牌
            var stageBg = AddSlicedSprite("图版青铜衬框", paper, PanelWidth * 0.5f, 378f, 466f, 440f, _frameBronze, 1.55f);
            stageBg.color = Color.white;

            var artRect = NewRect("本印手形", paper, PanelWidth * 0.5f, 340f, 420f, 300f);
            _sealArt = artRect.gameObject.AddComponent<RawImage>();
            _sealArt.raycastTarget = false;
            _sealArt.material = Resources.Load<Material>("Teaching/SealPhoto");

            _wordFace = AddText("中心口令或印名", paper, PanelWidth * 0.5f, 344f, 410f, 166f, "", 58, TextAnchor.MiddleCenter, Ink.Text);
            _sealCaption = AddText("图注", paper, PanelWidth * 0.5f, 520f, 416f, 58f, "", 40, TextAnchor.MiddleCenter, Ink.Gold);
            _sealSequence = AddText("印序", paper, PanelWidth * 0.5f, 560f, 430f, 38f, "", 23, TextAnchor.MiddleCenter, Ink.Text);

            // 左栏信息托盘（温玉牌状态卡头 + 触发方式 + 消耗）
            AddSlicedSprite("习得玉标", paper, 186f, 210f, 232f, 96f, _frameJade, 2.25f);
            _statusMark = AddImage("状态印外形", paper, 98f, 210f, 18f, 18f, Ink.InkOnJade).rectTransform;
            _statusHollow = AddImage("空心状态", _statusMark, 9f, 9f, 10f, 10f, Ink.JadeAccent).rectTransform;
            _lessonState = AddText("习得状态", paper, 198f, 210f, 152f, 78f, "", 25, TextAnchor.MiddleLeft, Ink.InkOnJade);

            AddText("术式方式标签", paper, 186f, 314f, 232f, 40f, "触发方式", 24, TextAnchor.MiddleLeft, Ink.Gold);
            _lessonMethod = AddText("术式方式", paper, 186f, 360f, 232f, 64f, "", 33, TextAnchor.MiddleLeft, Ink.Text);
            AddDivider("左栏分割线", paper, 182f, 410f, 224f, 14f);
            _lessonCost = AddText("消耗与冷却", paper, 186f, 480f, 232f, 114f, "", 26, TextAnchor.MiddleLeft, Ink.Muted);

            // 右栏信息区（口令 + 操作提醒）
            AddSlicedSprite("口令提示垫", paper, 934f, 286f, 232f, 176f, _buttonNormal, 2.0f);
            _lessonVoiceLabel = AddText("提示标题", paper, 934f, 240f, 196f, 44f, "", 25, TextAnchor.MiddleLeft, Ink.Gold);
            _lessonVoice = AddText("提示内容", paper, 934f, 304f, 196f, 84f, "", 37, TextAnchor.MiddleLeft, Ink.Text);
            AddDivider("右栏分割线", paper, 934f, 402f, 224f, 14f);
            _lessonAction = AddText("施法动作", paper, 934f, 474f, 232f, 122f, "", 26, TextAnchor.MiddleLeft, Ink.Muted);

            paper.gameObject.SetActive(false);
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

            CanvasScaler scaler = go.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 2.5f;
            scaler.referencePixelsPerUnit = 100f;

            // 不添加 Button、GraphicRaycaster、EventSystem 或任何碰撞体。
            return rect;
        }

        private void AddPlate(RectTransform parent, float width, float height, float ppuMultiplier = 1.25f)
        {
            // 轻微立体背垫，防止强背光穿透
            Image shadow = AddImage("轻薄背影", parent, width * 0.5f + 4f, height * 0.5f + 6f, width - 16f, height - 16f, Ink.Plate, 0.45f);
            shadow.rectTransform.localPosition += Vector3.forward * 1.2f;

            if (_frameBronze)
            {
                Image frame = AddSlicedSprite("青铜错金主框", parent, width * 0.5f, height * 0.5f, width, height, _frameBronze, ppuMultiplier);
                frame.color = Color.white;
            }
            else
            {
                AddImage("青铜细边", parent, width * 0.5f, height * 0.5f, width, height, Ink.Brass, 0.85f);
                AddImage("玄漆内板", parent, width * 0.5f, height * 0.5f, width - 6f, height - 6f, Ink.Lacquer, 0.97f);
            }
        }

        private Image AddSlicedSprite(string name, RectTransform parent, float x, float y, float width, float height, Sprite sprite, float ppuMultiplier)
        {
            RectTransform rect = NewRect(name, parent, x, y, width, height);
            Image img = rect.gameObject.AddComponent<Image>();
            img.raycastTarget = false;
            if (sprite)
            {
                img.sprite = sprite;
                img.type = sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
                img.pixelsPerUnitMultiplier = ppuMultiplier;
                img.color = Color.white;
            }
            else
            {
                img.color = ThemeLacquer();
            }
            return img;
        }

        private void AddDivider(string name, RectTransform parent, float x, float y, float width, float height)
        {
            if (_dividerGold)
            {
                RectTransform rect = NewRect(name, parent, x, y, width, height);
                Image img = rect.gameObject.AddComponent<Image>();
                img.sprite = _dividerGold;
                img.preserveAspect = true;
                img.raycastTarget = false;
                img.color = Color.white;
            }
            else
            {
                AddImage(name, parent, x, y, width, 2f, Ink.Brass, 0.72f);
            }
        }

        private void AddProgressBar(string name, RectTransform parent, float x, float y, float width, float height, out RectTransform fillRect)
        {
            Image track;
            if (_gaugeTrack)
            {
                track = AddSlicedSprite(name + "轨槽", parent, x, y, width, height, _gaugeTrack, 1.5f);
            }
            else
            {
                track = AddImage(name + "轨槽", parent, x, y, width, height, Ink.Cyan, 0.2f);
            }

            RectTransform fill = NewRect(name, track.rectTransform, 0f, 0f, 0f, 0f);
            fill.anchorMin = new Vector2(0.02f, 0.22f);
            fill.anchorMax = new Vector2(0.98f, 0.78f);
            fill.pivot = new Vector2(0f, 0.5f);
            fill.offsetMin = fill.offsetMax = Vector2.zero;

            Image fillImg = fill.gameObject.AddComponent<Image>();
            fillImg.raycastTarget = false;
            if (_gaugeFill)
            {
                fillImg.sprite = _gaugeFill;
                fillImg.type = Image.Type.Filled;
                fillImg.fillMethod = Image.FillMethod.Horizontal;
                fillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
                fillImg.color = Color.white;
            }
            else
            {
                fillImg.color = ThemeGold();
            }
            fillRect = fill;
        }

        private ButtonVisual AddButton(
            RectTransform parent, string id, float x, float y, float width, float height,
            string label, string caption, int fontSize, string iconSpriteName = null)
        {
            RectTransform rect = NewRect("目标 · " + id, parent, x, y, width, height);
            Image background = rect.gameObject.AddComponent<Image>();
            background.raycastTarget = false;
            if (_buttonNormal)
            {
                background.sprite = _buttonNormal;
                background.type = Image.Type.Sliced;
                background.pixelsPerUnitMultiplier = height < 90f ? 2.0f : 1.6f;
                background.color = Color.white;
            }
            else
            {
                background.color = ThemeButton();
            }

            Image emblemImg = null;
            Sprite iconSprite = !string.IsNullOrEmpty(iconSpriteName) ? KaogongUISprites.Get(iconSpriteName) : null;
            bool hasIcon = iconSprite != null;
            if (hasIcon)
            {
                float iconSize = Mathf.Min(width * 0.44f, height * 0.42f);
                RectTransform iconRect = NewRect("按钮铜纹", rect, width * 0.5f, height * 0.34f, iconSize, iconSize);
                emblemImg = iconRect.gameObject.AddComponent<Image>();
                emblemImg.sprite = iconSprite;
                emblemImg.preserveAspect = true;
                emblemImg.raycastTarget = false;
                emblemImg.color = Color.white;
            }

            bool hasCaption = !string.IsNullOrEmpty(caption);
            float labelY = hasIcon ? height * 0.72f : hasCaption ? 54f : height * 0.49f;
            float labelH = hasIcon ? height * 0.28f : hasCaption ? 54f : height - 22f;
            Text labelText = AddText("按钮文字", rect,
                width * 0.5f, labelY,
                width - 24f, labelH,
                label, fontSize, TextAnchor.MiddleCenter, Ink.Text, false);

            Text captionText = null;
            if (hasCaption && !hasIcon)
            {
                captionText = AddText("按钮副文", rect, width * 0.5f, 100f,
                    width - 24f, 38f, caption, 21,
                    TextAnchor.MiddleCenter, Ink.Muted, false);
            }

            // 游标悬停填充槽
            Image track = AddSlicedSprite("进度底轨", rect, width * 0.5f, height - 11f, width - 32f, 8f, _gaugeTrack, 2.2f);
            if (!_gaugeTrack) track.color = new Color(ThemeAccent().r, ThemeAccent().g, ThemeAccent().b, 0.18f);
            else track.color = new Color(1f, 1f, 1f, 0.55f);

            RectTransform fill = NewRect("悬停进度", track.rectTransform, 0f, 0f, 0f, 0f);
            fill.anchorMin = new Vector2(0.02f, 0.18f);
            fill.anchorMax = new Vector2(0.02f, 0.82f);
            fill.pivot = Vector2.zero;
            fill.offsetMin = fill.offsetMax = Vector2.zero;

            Image fillImage = fill.gameObject.AddComponent<Image>();
            fillImage.raycastTarget = false;
            if (_gaugeFill)
            {
                fillImage.sprite = _gaugeFill;
                fillImage.type = Image.Type.Simple;
                fillImage.color = Color.white;
            }
            fill.gameObject.SetActive(false);

            ButtonVisual visual = new ButtonVisual
            {
                id = id,
                rect = rect,
                background = background,
                track = track,
                label = labelText,
                caption = captionText,
                emblem = emblemImg,
                progress = fill,
                progressImage = fillImage
            };

            _buttons.Add(visual);
            _targets.Add(new WristUiTarget(id, rect));
            return visual;
        }

        private void BuildDecoration()
        {
            _decoration = NewObject("腕饰 · 同心封纹", _ownedRoot.transform, false).transform;

            AddRing("外环", _decoration, 0.019f, 0.00055f, _violetMaterial, 64);
            AddRing("内环", _decoration, 0.014f, 0.00035f, _cyanMaterial, 48);

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
            _pointer = NewLine("世界指向线", _ownedRoot.transform, _pointerMaterial, 0.0007f, 2);
            _pointer.useWorldSpace = true;
            _pointer.sortingOrder = 40;

            if (_reticleSprite)
            {
                RectTransform retRect = NewCanvas("命中准星", 260f, 260f, 45);
                retRect.localScale = Vector3.one * 0.000072f;
                Image img = AddImage("方圆准星图", retRect, 130f, 130f, 250f, 250f, Ink.Text, 1f);
                _graphics.RemoveAt(_graphics.Count - 1);
                img.sprite = _reticleSprite;
                img.preserveAspect = true;
                img.color = Color.white;
                _reticleImage = img;
                _reticle = retRect;
            }
            else
            {
                _reticle = NewObject("命中准星", _ownedRoot.transform, false).transform;
                LineRenderer ring = AddRing("准星细环", _reticle, 0.003f, 0.00055f, _pointerMaterial, 32);
                ring.sortingOrder = 45;
                LineRenderer center = NewLine("准星中心", _reticle, _pointerMaterial, 0.0006f, 2);
                center.SetPosition(0, new Vector3(-0.0008f, 0f, 0f));
                center.SetPosition(1, new Vector3(0.0008f, 0f, 0f));
                center.sortingOrder = 45;
            }
        }

        private LineRenderer AddRing(string name, Transform parent, float radius, float width, Material material, int count)
        {
            LineRenderer line = NewLine(name, parent, material, width, count);
            line.loop = true;
            for (int i = 0; i < count; i++)
            {
                float angle = i * (Mathf.PI * 2f / count);
                line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
            }
            return line;
        }

        private LineRenderer NewLine(string name, Transform parent, Material material, float width, int count)
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
            Material material = new Material(shader) { name = name, hideFlags = HideFlags.HideAndDontSave };
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 0f);
            if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)BlendMode.One);
            if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)BlendMode.Zero);
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 1f);
            if (material.HasProperty("_Cull")) material.SetFloat("_Cull", (float)CullMode.Off);
            material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Geometry;
            material.enableInstancing = true;
            return material;
        }

        private static void SetMaterialColor(Material material, Color color)
        {
            if (material == null) return;
            color.a = 1f;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        }

        private void RefreshStyle()
        {
            float scale = canvasScale;
            if (float.IsNaN(scale) || float.IsInfinity(scale)) scale = 0.0005f;
            scale = Mathf.Max(0.0001f, scale);

            Vector3 worldScale = Vector3.one * scale;
            if (_launcher.localScale != worldScale) _launcher.localScale = worldScale;
            if (_panel.localScale != worldScale) _panel.localScale = worldScale;
            if (_openingHint.localScale != worldScale) _openingHint.localScale = worldScale;
            if (_rightGlance) _rightGlance.localScale = worldScale * 0.84f;

            Font resolved = ResolveFont();
            for (int i = 0; i < _texts.Count; i++)
            {
                Text text = _texts[i];
                if (text != null && text.font != resolved) text.font = resolved;
            }

            for (int i = 0; i < _graphics.Count; i++)
            {
                GraphicBinding binding = _graphics[i];
                if (binding.graphic == null) continue;
                Color color = ResolveInk(binding.ink);
                color.a *= binding.alpha;
                if (binding.graphic.color != color) binding.graphic.color = color;
            }

            if (_hovered != null && !_hovered.rect.gameObject.activeInHierarchy)
            {
                _hovered = null;
                _hoverProgress = 0f;
            }

            for (int i = 0; i < _buttons.Count; i++)
                PaintButton(_buttons[i]);

            SetMaterialColor(_cyanMaterial, ThemeAccent());
            SetMaterialColor(_violetMaterial, ThemeBronze());
            SetMaterialColor(_goldMaterial, ThemeGold());
            SetMaterialColor(_pointerMaterial, _pointerPressed ? KaogongTheme.Cinnabar : ThemeGold());

            if (!audioFeedback && _audioSource != null && _audioSource.isPlaying)
                _audioSource.Stop();
        }

        private Font ResolveFont()
        {
            if (font != null) return font;
            if (!_fallbackResolved)
            {
                _fallbackResolved = true;
                _fallbackFont = Resources.Load<Font>("Fonts/NotoSansCJKsc-Regular");
                if (_fallbackFont == null)
                    _fallbackFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
            return _fallbackFont;
        }

        private Color ThemePlate() => useKaogongTheme ? KaogongTheme.LacquerDark : plateColor;
        private Color ThemeLacquer() => useKaogongTheme ? KaogongTheme.SlatePanel : new Color(.055f, .098f, .094f, .98f);
        private Color ThemeButton() => useKaogongTheme ? KaogongTheme.SlatePanel : buttonColor;
        private Color ThemeText() => useKaogongTheme ? KaogongTheme.JadeWarm : textColor;
        private Color ThemeMuted() => useKaogongTheme ? KaogongTheme.SilkMuted : mutedTextColor;
        private Color ThemeAccent() => useKaogongTheme ? KaogongTheme.JadeAccent : cyanColor;
        private Color ThemeBronze() => useKaogongTheme ? KaogongTheme.BronzeAged : violetColor;
        private Color ThemeGold() => useKaogongTheme ? KaogongTheme.GoldInlay : goldColor;

        private Color ResolveInk(Ink ink)
        {
            switch (ink)
            {
                case Ink.Plate: return ThemePlate();
                case Ink.Muted: return ThemeMuted();
                case Ink.Cyan: return ThemeAccent();
                case Ink.Violet: return ThemeBronze();
                case Ink.Gold: return ThemeGold();
                case Ink.Lacquer: return ThemeLacquer();
                case Ink.Brass: return ThemeBronze();
                case Ink.InkOnJade: return KaogongTheme.InkOnJade;
                case Ink.JadeAccent: return ThemeAccent();
                default: return ThemeText();
            }
        }

        private void PaintButton(ButtonVisual button)
        {
            bool hovered = button == _hovered;

            if (_buttonNormal && _buttonHover)
            {
                Sprite targetSprite = hovered ? _buttonHover : _buttonNormal;
                if (button.background.sprite != targetSprite) button.background.sprite = targetSprite;
                Color tint = hovered ? Color.white : new Color(0.95f, 0.94f, 0.92f, 1f);
                if (button.background.color != tint) button.background.color = tint;
            }
            else
            {
                Color regular = ThemeButton();
                Color background = hovered ? Color.Lerp(regular, ThemeGold(), 0.25f) : regular;
                background.a = regular.a;
                if (button.background.color != background) button.background.color = background;
            }

            if (button.emblem != null)
            {
                Color emblemColor = hovered ? new Color(1.0f, 0.98f, 0.92f, 1f) : new Color(0.92f, 0.90f, 0.86f, 1f);
                if (button.emblem.color != emblemColor) button.emblem.color = emblemColor;
            }

            Color labelColor = hovered ? ThemeGold() : ThemeText();
            if (button.label.color != labelColor) button.label.color = labelColor;

            if (button.caption != null && button.caption.color != ThemeMuted())
                button.caption.color = ThemeMuted();

            Color progressColor = _gaugeFill ? Color.white : (_hoverProgress >= 1f ? ThemeGold() : ThemeAccent());
            if (button.progressImage.color != progressColor) button.progressImage.color = progressColor;

            float progress = hovered ? _hoverProgress : 0f;
            float clampedEnd = Mathf.Lerp(0.02f, 0.98f, progress);
            Vector2 anchorMax = new Vector2(clampedEnd, 0.82f);
            if (button.progress.anchorMax != anchorMax) button.progress.anchorMax = anchorMax;

            bool showProgress = progress > 0f;
            if (button.progress.gameObject.activeSelf != showProgress)
                button.progress.gameObject.SetActive(showProgress);
        }

        private void ClearHoverWithin(Transform container)
        {
            if (_hovered == null || !_hovered.rect.IsChildOf(container)) return;
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

        private RectTransform NewRect(string name, RectTransform parent, float x, float y, float width, float height)
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
            GameObject go = rectTransform ? new GameObject(name, typeof(RectTransform)) : new GameObject(name);
            go.hideFlags = HideFlags.DontSave;
            go.layer = gameObject.layer;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            return go;
        }

        private static void Place(Transform target, Pose pose) => target.SetPositionAndRotation(pose.position, pose.rotation);

        private void EnsureAudio()
        {
            if (_audioSource != null) return;
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
                float phase = 2f * Mathf.PI * (startFrequency * time + 0.5f * slope * time * time);
                float attack = Mathf.Clamp01(time / 0.008f);
                float decay = Mathf.Clamp01((duration - time) / duration);
                float envelope = attack * decay * decay;
                samples[i] = envelope * (Mathf.Sin(phase) * 0.6f + Mathf.Sin(phase * 2f) * 0.08f);
            }

            AudioClip clip = AudioClip.Create(name, count, 1, sampleRate, false);
            clip.hideFlags = HideFlags.HideAndDontSave;
            clip.SetData(samples, 0);
            return clip;
        }

        private void OnEnable()
        {
            if (_ownedRoot && (!_built || _targets.Count != TargetCount)) ReleaseVisuals();
            if (_ownedRoot != null)
            {
                _ownedRoot.SetActive(true);
                if (_built) RefreshStyle();
            }
        }

        private void OnDisable()
        {
            if (!Application.isPlaying) { ReleaseVisuals(); return; }
            if (_ownedRoot != null) _ownedRoot.SetActive(false);
            if (_audioSource != null) _audioSource.Stop();
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

            if (_ownedRoot != null) _ownedRoot.SetActive(false);

            DisposeOwned(_ownedRoot, immediately);
            DisposeOwned(_cyanMaterial, immediately);
            DisposeOwned(_violetMaterial, immediately);
            DisposeOwned(_goldMaterial, immediately);
            DisposeOwned(_pointerMaterial, immediately);
            DisposeOwned(_acceptedClip, immediately);
            DisposeOwned(_rejectedClip, immediately);

            _ownedRoot = null;
            _audioSource = null;
            _cyanMaterial = _violetMaterial = _goldMaterial = _pointerMaterial = null;
            _acceptedClip = _rejectedClip = null;
            _launcher = _panel = _microphoneRetry = _openingHint = _openingFill = null;
            _statusPage = _rightGlance = null;
            _pageRows = _glanceRows = null;
            _previous = _next = _glanceToggle = _sealHintsToggle = _exploration = _combatMovement = _aimAssist = null;
            shownExplorationMode = shownCombatMode = null;
            _emptyStatus = null;
            _launcherVitals = _panelTitle = _panelBody = _panelFooter = _openingText = null;
            _decoration = _reticle = null;
            _reticleImage = null;
            _pointer = null;

            _targets.Clear();
            _sealTextures.Clear();
            _buttons.Clear();
            _graphics.Clear();
            _texts.Clear();
            _hovered = null;
            _built = false;
            _styleDirty = true;
        }

        private static void DisposeOwned(Object value, bool immediately = false)
        {
            if (value == null) return;
            if (Application.isPlaying && !immediately) Object.Destroy(value);
            else Object.DestroyImmediate(value);
        }
    }
}
