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
            Gold,
            Lacquer,
            Brass
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

        private readonly List<WristUiTarget> _targets = new List<WristUiTarget>(10);
        private readonly List<ButtonVisual> _buttons = new List<ButtonVisual>(10);
        private readonly List<GraphicBinding> _graphics = new List<GraphicBinding>(48);
        private readonly List<Text> _texts = new List<Text>(24);

        private GameObject _ownedRoot;
        private RectTransform _launcher;
        private RectTransform _panel;
        private RectTransform _microphoneRetry;
        private RectTransform _openingHint, _openingFill;
        private Text _openingText;
        private Transform _decoration;
        private Transform _reticle;
        private LineRenderer _pointer;
        private Text _launcherVitals, _launcherProgress;
        private string _launcherIdleHint;
        int shownHp, shownMp;
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

        public void EnsureBuilt()
        {
            if (!_built || !_ownedRoot || _targets.Count != 10)
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

        /// <summary>控制抬腕开启提示的呈现，与首章剧情无关；隐藏时不隐式创建 UI。</summary>
        public void SetWristActivationHint(Pose pose, string hint, float progress, bool visible)
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
            SetMicrophoneRetry(false);
            _panelBody.gameObject.SetActive(false);
            _lessonSurface.gameObject.SetActive(true);
            _panelTitle.text = lesson.Name;
            _panelFooter.text = footer;

            var reference = voiceIndex && lesson.Voice ? lesson.Voice : lesson.Reference;
            // Voice+gesture recipes still require the seal when opened from the voice index.
            bool hasImage = lesson.Seals.Count > 0 && (!voiceIndex || reference.triggerMode == SkillTriggerMode.VoiceAndGesture);
            _lessonState.text = available ? (permanent ? "已解锁" : "临时可用") :
                permanent ? "已解锁\n当前受限" : discovered ? "已了解\n待解锁" : "尚未了解";
            if (savePending) _lessonState.text += "\n待存档";
            _statusMark.localRotation = Quaternion.Euler(0, 0, permanent ? 45 : 0);
            _statusHollow.gameObject.SetActive(!available);
            _lessonMethod.text = lesson.HasAlternative ? "结印 或 念咒" : reference.triggerMode == SkillTriggerMode.VoiceAndGesture ? "结印 ＋ 念咒" : hasImage ? "双手结印" : "念出口令";
            _lessonCost.text = $"度力  {reference.manaCost:0.#}\n冷却  {reference.cooldownSeconds:0.#} 秒";
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
                _sealArt.rectTransform.sizeDelta = ratio > 430f/320f ? new Vector2(430,430/ratio) : new Vector2(320*ratio,320);
            }
            _wordFace.gameObject.SetActive(!texture);
            _wordFace.text = hasImage ? SealName(name) : lesson.Voice ? lesson.Voice.phrase : "";
            _sealCaption.text = hasImage ? SealName(name) : "口令";
            _sealSequence.text = hasImage && lesson.Seals.Count > 1 ? Sequence(lesson.Seals, step) : "";
            Place(_panel, pose); _panel.gameObject.SetActive(true);
        }

        static string SealName(string value) => string.IsNullOrEmpty(value) || value.EndsWith("印") ? value : value+"印";
        static string Sequence(IReadOnlyList<string> seals, int active)
        {
            var s = new System.Text.StringBuilder();
            int first = Mathf.Clamp(active-1,0,Mathf.Max(0,seals.Count-3));
            if (first > 0) s.Append("…  ");
            for (int i=first;i<Mathf.Min(seals.Count,first+3);i++)
            {
                if (i>first) s.Append("  →  ");
                if (i==active) s.Append('【');
                s.Append(SealName(seals[i]));
                if (i==active) s.Append('】');
            }
            if (first+3<seals.Count) s.Append("  …");
            return s.ToString();
        }

        Texture LoadSeal(string seal)
        {
            string key=(seal??string.Empty).Trim();
            if (_sealTextures.TryGetValue(key,out var result)) return result;
            if (key.Length>0) result=Resources.Load<Texture2D>("Teaching/Seals/"+key);
            _sealTextures[key]=result;
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

            Vector3 position = transform.position;
            if (_panel.gameObject.activeInHierarchy)
                position = _panel.position;
            else if (_launcher.gameObject.activeInHierarchy)
                position = _launcher.position;
            else if (_decoration.gameObject.activeInHierarchy)
                position = _decoration.position;

            EnsureAudio();
            _audioSource.transform.position = position;
            _audioSource.volume = Mathf.Clamp01(cueVolume);

            // 不叠加无上限的音源或声音。
            _audioSource.Stop();
            _audioSource.clip = accepted ? (acceptedSound ? acceptedSound : _acceptedClip) :
                (rejectedSound ? rejectedSound : _rejectedClip);
            AudioCueCount++;
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

            bool usesCase = TryGetComponent<WristMenuController>(out var controller) && controller.wornDevice;
            AddText("启动器标题", _launcher, 142f, 35f, 225f, 54f,
                usesCase ? "准望匣" : "腕上术式", 33, TextAnchor.MiddleLeft, Ink.Text);
            _launcherIdleHint=usesCase ? "再按朱砂扣收起" : "再点左腕饰关闭";
            _launcherProgress=AddText("腕匣课数与铜叶", _launcher, 459f, 35f, 465f, 56f,
                _launcherIdleHint, 26, TextAnchor.MiddleRight, Ink.Gold);

            AddButton(_launcher, "seal", 134f, 149f, 210f, 150f, "术式教学", null, 36);
            AddButton(_launcher, "glyph", 360f, 149f, 210f, 150f, "画符", "符纹", 40);
            AddButton(_launcher, "voice", 586f, 149f, 210f, 150f, "咒语", "言灵", 40);

            if (usesCase)
                _launcherVitals=AddText("气血与度力 · 大字读数",_launcher,360f,262f,670f,68f,
                    "",46,TextAnchor.MiddleCenter,Ink.Text);
            else
                AddText("交互提示", _launcher, 360f, 263f, 660f, 42f,
                    "右手食指轻触 · 远指捏合", 24, TextAnchor.MiddleCenter, Ink.Muted);
        }

        public void SetLauncherVitals(WristGauge health,WristGauge power)
        {
            if (health && power) SetLauncherVitals(health.Value,power.Value);
        }

        internal void SetLauncherVitals(float health, float mana)
        {
            if (!_launcherVitals) return;
            int hp=Mathf.CeilToInt(health),mp=Mathf.CeilToInt(mana);
            if (hp==shownHp && mp==shownMp && _launcherVitals.text.Length!=0) return;
            shownHp=hp; shownMp=mp;
            _launcherVitals.text=$"气血 {hp}    度力 {mp}";
        }
        internal void SetLauncherProgress(string progress)
        {
            string value=string.IsNullOrEmpty(progress)?_launcherIdleHint:progress;
            if (_launcherProgress && _launcherProgress.text!=value) _launcherProgress.text=value;
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
            AddImage("标题分隔线", _panel, 560f, 128f, 1000f, 2f, Ink.Brass, 0.7f);

            // 固定页高，不自行翻页、不滚动、不修改父系统提供的内容。
            _panelBody = AddText("分页正文", _panel, 560f, 366f, 1000f, 444f,
                string.Empty, 36, TextAnchor.UpperLeft, Ink.Text);
            _panelBody.lineSpacing = 1.05f;
            BuildLessonSurface();

            _panelFooter = AddText("页脚", _panel, 560f, 628f, 1000f, 80f,
                string.Empty, 25, TextAnchor.MiddleLeft, Ink.Muted);

            _microphoneRetry = NewRect("麦克风恢复提示", _panel, 560f, 628f, 1000f, 80f);
            AddText("麦克风状态", _microphoneRetry, 340f, 40f, 660f, 80f,
                "麦克风暂不可用\n若未授权，请在系统允许麦克风后返回", 25, TextAnchor.MiddleLeft, Ink.Muted);
            AddButton(_microphoneRetry, "retry-microphone", 850f, 40f, 280f, 76f, "重试麦克风", null, 29);
            _microphoneRetry.gameObject.SetActive(false);

            AddImage("操作分隔线", _panel, 560f, 672f, 1000f, 2f, Ink.Brass, 0.55f);

            AddButton(_panel, "previous", 179f, 731f, 240f, 100f, "上一页", null, 32);
            AddButton(_panel, "next", 433f, 731f, 240f, 100f, "下一页", null, 32);
            AddButton(_panel, "relocate", 687f, 731f, 240f, 100f, "重新定位", null, 32);
            AddButton(_panel, "close", 941f, 731f, 240f, 100f, "关闭", null, 32);
        }

        private void BuildLessonSurface()
        {
            _lessonSurface=NewRect("铜叶教学卡",_panel,560,400,1120,800);
            var paper=_lessonSurface;
            AddImage("图版底",paper,560,369,470,426,Ink.Muted,.11f);
            AddImage("图版上沿",paper,560,157,470,3,Ink.Gold,.72f);
            AddImage("图版下沿",paper,560,581,470,2,Ink.Gold,.48f);
            var artRect=NewRect("本印手形",paper,560,337,430,320);
            _sealArt=artRect.gameObject.AddComponent<RawImage>(); _sealArt.raycastTarget=false;
            _wordFace=AddText("中心口令或印名",paper,560,342,440,166,"",60,TextAnchor.MiddleCenter,Ink.Text);
            _sealCaption=AddText("图注",paper,560,517,444,64,"",41,TextAnchor.MiddleCenter,Ink.Gold);
            _sealSequence=AddText("印序",paper,560,557,454,40,"",24,TextAnchor.MiddleCenter,Ink.Text);
            _statusMark=AddImage("状态印外形",paper,70,201,18,18,Ink.Gold).rectTransform;
            _statusHollow=AddImage("空心状态",_statusMark,9,9,10,10,Ink.Plate).rectTransform;
            _lessonState=AddText("习得状态",paper,201,210,216,136,"",28,TextAnchor.MiddleLeft,Ink.Gold);
            AddText("术式方式标签",paper,176,306,230,45,"触发方式",25,TextAnchor.MiddleLeft,Ink.Muted);
            _lessonMethod=AddText("术式方式",paper,181,356,240,66,"",34,TextAnchor.MiddleLeft,Ink.Text);
            _lessonCost=AddText("消耗与冷却",paper,180,483,240,120,"",28,TextAnchor.MiddleLeft,Ink.Muted);
            _lessonVoiceLabel=AddText("提示标题",paper,951,247,230,55,"",26,TextAnchor.MiddleLeft,Ink.Gold);
            _lessonVoice=AddText("提示内容",paper,951,307,238,78,"",39,TextAnchor.MiddleLeft,Ink.Text);
            _lessonAction=AddText("施法动作",paper,951,453,240,120,"",28,TextAnchor.MiddleLeft,Ink.Muted);
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

            // 不添加 Button、GraphicRaycaster、EventSystem 或任何碰撞体。
            return rect;
        }

        private void AddPlate(RectTransform parent, float width, float height)
        {
            bool card = parent == _panel;
            Image shadow = AddImage("轻薄背影", parent,
                width * 0.5f + 5f, height * 0.5f + 7f,
                width + 8f, height + 8f, Ink.Plate, 0.5f);
            shadow.rectTransform.localPosition += Vector3.forward * 0.5f;

            AddImage("青色细边", parent, width * 0.5f, height * 0.5f,
                width, height, card ? Ink.Brass : Ink.Cyan, card ? .85f : .38f);
            AddImage("深色内板", parent, width * 0.5f, height * 0.5f,
                width - 4f, height - 4f, card ? Ink.Lacquer : Ink.Plate);

            AddImage("左上金纹", parent, 66f, 2f, 128f, 3f, Ink.Gold, 0.85f);
            AddImage("右下紫纹", parent, width - 82f, height - 2f,
                160f, 3f, card ? Ink.Brass : Ink.Violet, 0.8f);

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
                3f, height - 24f, parent == _panel ? Ink.Brass : Ink.Cyan, 0.45f);

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
                case Ink.Lacquer: return new Color(.055f,.098f,.094f,.98f);
                case Ink.Brass: return new Color(.74f,.63f,.39f,1f);
                default: return textColor;
            }
        }

        private void PaintButton(ButtonVisual button)
        {
            bool hovered = button == _hovered;
            bool card = _panel && button.rect.IsChildOf(_panel);
            var accent=card ? ResolveInk(Ink.Brass) : cyanColor;
            var regular=card ? new Color(.11f,.165f,.15f,buttonColor.a) : buttonColor;
            Color background = hovered
                ? Color.Lerp(regular, accent, 0.28f)
                : regular;
            background.a = buttonColor.a;

            if (button.background.color != background)
                button.background.color = background;

            Color edge = accent;
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
            if (_ownedRoot && (!_built || _targets.Count != 10)) ReleaseVisuals();
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
            _launcher=_panel=_microphoneRetry=_openingHint=_openingFill=null;
            _launcherVitals=_panelTitle=_panelBody=_panelFooter=_openingText=null;
            _decoration=_reticle=null; _pointer=null;

            // UI / LineRenderer 的内部网格由对应组件管理。
            // 不销毁用户字体、Resources 字体、内置字体或 Unity 默认 UI 材质。
            _targets.Clear();
            _sealTextures.Clear();
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
