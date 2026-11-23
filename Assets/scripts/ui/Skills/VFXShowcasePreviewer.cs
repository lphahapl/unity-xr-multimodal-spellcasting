using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#else
using UnityEngine.EventSystems;
#endif
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace NarutoVR.SealCombat
{
    /// <summary>
    /// 特效独立展厅预览控制器（uGUI版本）：支持风、火、雷、查克拉及小火苗五大分类切换、镜头360度环绕漫游、慢动作与现代化uGUI交互面板。
    /// </summary>
    [ExecuteAlways]
    [DefaultExecutionOrder(400)]
    public class VFXShowcasePreviewer : MonoBehaviour
    {
        [Serializable]
        public class VFXEntry
        {
            public string title;
            public string assetPath;
            [TextArea(2, 4)]
            public string description;
            public Vector3 spawnOffset = Vector3.zero;
            public float spawnScale = 1f;
            public GameObject prefab;
        }

        [Serializable]
        public class VFXCategory
        {
            public string categoryName;
            public Color themeColor = Color.cyan;
            public List<VFXEntry> entries = new List<VFXEntry>();
        }

        [Header("分类与特效配置")]
        public List<VFXCategory> categories = new List<VFXCategory>();
        public int currentCategoryIndex = 4; // 默认选中小火苗
        public int currentEntryIndex = 0;

        [Header("生成与展示挂点")]
        public Transform spawnRoot;
        public GameObject currentInstance;

        [Header("镜头控制")]
        public Camera showcaseCamera;
        public Vector3 cameraPivot = new Vector3(0, 1.0f, 0);
        public float cameraDistance = 3.5f;
        public float cameraPitch = 18.0f;
        public float cameraYaw = 0f;
        public bool autoRotateCamera = true;
        public float autoRotateSpeed = 12.0f;

        [Header("播放设置")]
        public bool slowMotion = false;

        [Header("uGUI 面板引用")]
        public Canvas uiCanvas;
        public Text infoTitleText;
        public Text infoDescriptionText;
        public Text infoPathText;
        public Text autoRotateBtnText;
        public Text slowMotionBtnText;
        public List<Button> categoryButtons = new List<Button>();
        public List<Text> categoryButtonTexts = new List<Text>();
        public Button prevButton;
        public Button replayButton;
        public Button nextButton;
        public Button autoRotateButton;
        public Button slowMotionButton;
        public Button resetCamButton;

        private Vector2 previousMousePos;
        private bool isRightDragging = false;

        private void Awake()
        {
            if (categories == null || categories.Count == 0)
            {
                InitializeCuratedList();
            }
            EnsureAllPrefabsLoaded();
        }

        private void OnEnable()
        {
            EnsureEventSystem();
            EnsureUGUI();
            BindButtonListeners();
        }

        private void Start()
        {
            if (spawnRoot == null)
            {
                var anchor = new GameObject("VFX_SpawnRoot");
                anchor.transform.SetParent(transform, false);
                anchor.transform.position = Vector3.zero;
                spawnRoot = anchor.transform;
            }

            if (showcaseCamera == null)
            {
                showcaseCamera = Camera.main;
            }

            EnsureEventSystem();
            EnsureUGUI();
            BindButtonListeners();

            UpdateCameraTransform();
            SpawnCurrentEffect();
        }

        private void Update()
        {
            HandleInputs();

            if (autoRotateCamera && !isRightDragging)
            {
                cameraYaw += autoRotateSpeed * (slowMotion ? 5f : 1f) * Time.unscaledDeltaTime;
                if (cameraYaw > 360f) cameraYaw -= 360f;
                UpdateCameraTransform();
            }
        }

        public void EnsureAllPrefabsLoaded()
        {
#if UNITY_EDITOR
            if (categories == null) return;
            foreach (var cat in categories)
            {
                if (cat.entries == null) continue;
                foreach (var item in cat.entries)
                {
                    if (item.prefab == null && !string.IsNullOrEmpty(item.assetPath))
                    {
                        item.prefab = AssetDatabase.LoadAssetAtPath<GameObject>(item.assetPath);
                    }
                }
            }
#endif
        }

        public void SpawnCurrentEffect()
        {
            if (currentInstance != null)
            {
                if (Application.isPlaying)
                    Destroy(currentInstance);
                else
                    DestroyImmediate(currentInstance);
                currentInstance = null;
            }

            if (categories == null || categories.Count == 0) return;
            currentCategoryIndex = Mathf.Clamp(currentCategoryIndex, 0, categories.Count - 1);
            var cat = categories[currentCategoryIndex];
            if (cat.entries == null || cat.entries.Count == 0) return;
            currentEntryIndex = Mathf.Clamp(currentEntryIndex, 0, cat.entries.Count - 1);

            var entry = cat.entries[currentEntryIndex];
            if (entry.prefab == null)
            {
                EnsureAllPrefabsLoaded();
            }

            if (entry.prefab != null)
            {
                Vector3 pos = (spawnRoot != null ? spawnRoot.position : Vector3.zero) + entry.spawnOffset;
                currentInstance = Instantiate(entry.prefab, pos, Quaternion.identity);
                currentInstance.name = "[Showcase] " + entry.title;
                if (spawnRoot != null) currentInstance.transform.SetParent(spawnRoot, true);
                currentInstance.transform.localScale = Vector3.one * (entry.spawnScale > 0 ? entry.spawnScale : 1f);

                // 确保粒子系统重新播放，并兼容仅有 rateOverDistance (位移才发射) 的飞弹类特效
                var particles = currentInstance.GetComponentsInChildren<ParticleSystem>(true);
                bool hasTimeEmission = false;
                foreach (var ps in particles)
                {
                    if (ps.emission.rateOverTimeMultiplier > 0 || ps.emission.burstCount > 0)
                    {
                        hasTimeEmission = true;
                        break;
                    }
                }

                foreach (var ps in particles)
                {
                    if (!hasTimeEmission && ps.emission.rateOverDistanceMultiplier > 0 && ps.emission.rateOverTimeMultiplier == 0)
                    {
                        var emission = ps.emission;
                        emission.rateOverTimeMultiplier = Mathf.Max(12f, ps.emission.rateOverDistanceMultiplier * 6f);
                    }

                    ps.Clear();
                    ps.Play(true);
                }
            }

            RefreshUI();
        }

        public void SwitchCategory(int index)
        {
            if (categories == null || categories.Count == 0) return;
            currentCategoryIndex = Mathf.Clamp(index, 0, categories.Count - 1);
            currentEntryIndex = 0;
            // 小火苗时自动微调镜头距离到 2.8m，便于细致观察火苗跳动
            if (currentCategoryIndex == 4)
            {
                cameraDistance = 2.8f;
                cameraPitch = 15f;
            }
            SpawnCurrentEffect();
        }

        public void NextEffect()
        {
            if (categories == null || categories.Count == 0) return;
            var cat = categories[currentCategoryIndex];
            if (cat.entries == null || cat.entries.Count == 0) return;
            currentEntryIndex = (currentEntryIndex + 1) % cat.entries.Count;
            SpawnCurrentEffect();
        }

        public void PreviousEffect()
        {
            if (categories == null || categories.Count == 0) return;
            var cat = categories[currentCategoryIndex];
            if (cat.entries == null || cat.entries.Count == 0) return;
            currentEntryIndex = (currentEntryIndex - 1 + cat.entries.Count) % cat.entries.Count;
            SpawnCurrentEffect();
        }

        public void ReplayCurrent()
        {
            SpawnCurrentEffect();
        }

        public void ToggleSlowMotion()
        {
            slowMotion = !slowMotion;
            if (Application.isPlaying)
            {
                Time.timeScale = slowMotion ? 0.2f : 1.0f;
            }
            RefreshUI();
        }

        public void ToggleAutoRotate()
        {
            autoRotateCamera = !autoRotateCamera;
            RefreshUI();
        }

        public void ResetCamera()
        {
            cameraDistance = (currentCategoryIndex == 4) ? 2.8f : 4.5f;
            cameraPitch = 18.0f;
            cameraYaw = 0f;
            cameraPivot = new Vector3(0, 1.0f, 0);
            UpdateCameraTransform();
        }

        private void HandleInputs()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.digit1Key.wasPressedThisFrame) SwitchCategory(0);
                if (keyboard.digit2Key.wasPressedThisFrame) SwitchCategory(1);
                if (keyboard.digit3Key.wasPressedThisFrame) SwitchCategory(2);
                if (keyboard.digit4Key.wasPressedThisFrame) SwitchCategory(3);
                if (keyboard.digit5Key.wasPressedThisFrame) SwitchCategory(4);

                if (keyboard.aKey.wasPressedThisFrame || keyboard.leftArrowKey.wasPressedThisFrame) PreviousEffect();
                if (keyboard.dKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame) NextEffect();
                if (keyboard.spaceKey.wasPressedThisFrame) ReplayCurrent();
                if (keyboard.rKey.wasPressedThisFrame) ToggleAutoRotate();
                if (keyboard.cKey.wasPressedThisFrame) ToggleSlowMotion();
            }

            var mouse = Mouse.current;
            if (mouse != null)
            {
                if (mouse.rightButton.wasPressedThisFrame)
                {
                    isRightDragging = true;
                    previousMousePos = mouse.position.ReadValue();
                }
                else if (mouse.rightButton.wasReleasedThisFrame)
                {
                    isRightDragging = false;
                }

                if (isRightDragging)
                {
                    Vector2 currentPos = mouse.position.ReadValue();
                    Vector2 delta = currentPos - previousMousePos;
                    previousMousePos = currentPos;

                    cameraYaw += delta.x * 0.35f;
                    cameraPitch -= delta.y * 0.25f;
                    cameraPitch = Mathf.Clamp(cameraPitch, -20f, 85f);
                    UpdateCameraTransform();
                }

                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f)
                {
                    cameraDistance -= Mathf.Sign(scroll) * 0.65f;
                    cameraDistance = Mathf.Clamp(cameraDistance, 1.2f, 18.0f);
                    UpdateCameraTransform();
                }
            }
#endif
        }

        private void UpdateCameraTransform()
        {
            if (showcaseCamera == null) return;
            Quaternion rot = Quaternion.Euler(cameraPitch, cameraYaw, 0);
            Vector3 targetPos = cameraPivot - (rot * Vector3.forward * cameraDistance);
            showcaseCamera.transform.position = targetPos;
            showcaseCamera.transform.LookAt(cameraPivot);
        }

        #region uGUI 现代化界面构建与更新

        public void EnsureEventSystem()
        {
            var eventSystems = FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            UnityEngine.EventSystems.EventSystem activeEs = null;
            if (eventSystems != null && eventSystems.Length > 0)
            {
                activeEs = eventSystems[0];
                for (int i = 1; i < eventSystems.Length; i++)
                {
                    if (Application.isPlaying) Destroy(eventSystems[i].gameObject);
                    else DestroyImmediate(eventSystems[i].gameObject);
                }
            }
            else
            {
                var esGo = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem));
                esGo.transform.SetParent(transform, false);
                activeEs = esGo.GetComponent<UnityEngine.EventSystems.EventSystem>();
            }

            if (activeEs != null)
            {
                activeEs.gameObject.SetActive(true);
                activeEs.enabled = true;

#if ENABLE_INPUT_SYSTEM
                var oldModules = activeEs.GetComponents<UnityEngine.EventSystems.StandaloneInputModule>();
                foreach (var oldMod in oldModules)
                {
                    if (Application.isPlaying) Destroy(oldMod);
                    else DestroyImmediate(oldMod);
                }

                var inputModule = activeEs.GetComponent<InputSystemUIInputModule>();
                if (inputModule == null)
                {
                    inputModule = activeEs.gameObject.AddComponent<InputSystemUIInputModule>();
                }
                if (inputModule.actionsAsset == null)
                {
                    inputModule.AssignDefaultActions();
                }
                inputModule.enabled = true;
#else
                var inputModule = activeEs.GetComponent<UnityEngine.EventSystems.StandaloneInputModule>();
                if (inputModule == null)
                {
                    inputModule = activeEs.gameObject.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
                }
                inputModule.enabled = true;
#endif
            }
        }

        [ContextMenu("重建并刷新 uGUI 面板 (880x500高分清晰版)")]
        public void RebuildUGUIContextMenu()
        {
            BuildUGUIHierarchy();
        }

        public void EnsureUGUI()
        {
            EnsureEventSystem();
            if (uiCanvas != null && infoTitleText != null && categoryButtons.Count > 0 && prevButton != null)
            {
                // 检查主面板尺寸是否为旧版 (580x360)，如果是则强制重建为高分清晰版 (880x500)
                var mainPanel = uiCanvas.transform.Find("MainControlPanel") as RectTransform;
                if (mainPanel != null && mainPanel.sizeDelta.x >= 800f)
                {
                    BindButtonListeners();
                    return;
                }
            }
            BuildUGUIHierarchy();
        }

        public void BindButtonListeners()
        {
            for (int i = 0; i < categoryButtons.Count; i++)
            {
                if (categoryButtons[i] == null) continue;
                int catIdx = i;
                categoryButtons[i].onClick.RemoveAllListeners();
                categoryButtons[i].onClick.AddListener(() => SwitchCategory(catIdx));
            }

            if (prevButton != null)
            {
                prevButton.onClick.RemoveAllListeners();
                prevButton.onClick.AddListener(PreviousEffect);
            }
            if (replayButton != null)
            {
                replayButton.onClick.RemoveAllListeners();
                replayButton.onClick.AddListener(ReplayCurrent);
            }
            if (nextButton != null)
            {
                nextButton.onClick.RemoveAllListeners();
                nextButton.onClick.AddListener(NextEffect);
            }
            if (autoRotateButton != null)
            {
                autoRotateButton.onClick.RemoveAllListeners();
                autoRotateButton.onClick.AddListener(ToggleAutoRotate);
            }
            if (slowMotionButton != null)
            {
                slowMotionButton.onClick.RemoveAllListeners();
                slowMotionButton.onClick.AddListener(ToggleSlowMotion);
            }
            if (resetCamButton != null)
            {
                resetCamButton.onClick.RemoveAllListeners();
                resetCamButton.onClick.AddListener(ResetCamera);
            }
        }

        public void BuildUGUIHierarchy()
        {
            EnsureEventSystem();

            // 清理旧残留 Canvas，防止重复叠加或未绑定的旧面板干扰
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (child.name.StartsWith("VFX_ShowcaseCanvas") || child.GetComponent<Canvas>() != null)
                {
                    if (Application.isPlaying)
                        Destroy(child.gameObject);
                    else
                        DestroyImmediate(child.gameObject);
                }
            }

            // 1. 创建 Canvas
            var canvasGo = new GameObject("VFX_ShowcaseCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            uiCanvas = canvasGo.GetComponent<Canvas>();
            uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            uiCanvas.sortingOrder = 50;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            scaler.dynamicPixelsPerUnit = 2.0f; // 动态字体纹理按双倍精细度采样光栅化，彻底杜绝汉字模糊

            var raycaster = canvasGo.GetComponent<GraphicRaycaster>();
            if (raycaster != null) raycaster.enabled = true;

            // 字体：优先加载内置完整中文字体 NotoSansCJKsc-Regular.otf
            Font uiFont = Resources.Load<Font>("Fonts/NotoSansCJKsc-Regular");
#if UNITY_EDITOR
            if (uiFont == null)
            {
                uiFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/SealLab/Resources/Fonts/NotoSansCJKsc-Regular.otf");
            }
#endif
            if (uiFont == null) uiFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (uiFont == null) uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // 2. 主控制面板（尺寸从 580x360 放大至 880x500，提供充裕排版空间）
            var panelGo = new GameObject("MainControlPanel", typeof(RectTransform), typeof(Image));
            panelGo.transform.SetParent(canvasGo.transform, false);
            var panelRect = panelGo.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0, 1);
            panelRect.anchorMax = new Vector2(0, 1);
            panelRect.pivot = new Vector2(0, 1);
            panelRect.anchoredPosition = new Vector2(30, -30);
            panelRect.sizeDelta = new Vector2(880, 500);

            var panelImg = panelGo.GetComponent<Image>();
            panelImg.color = new Color(0.05f, 0.07f, 0.12f, 0.94f); // 深色高级半透明磨砂质感
            panelImg.raycastTarget = true;

            // 3. 标题与提示
            var headerGo = CreateText("HeaderTitle", panelGo.transform, uiFont, 28, FontStyle.Bold, Color.white, new Vector2(25, -20), new Vector2(830, 38));
            headerGo.GetComponent<Text>().text = "火影忍术特效精选展厅 · 预览控制台";

            var tipGo = CreateText("TipText", panelGo.transform, uiFont, 15, FontStyle.Normal, new Color(0.7f, 0.82f, 0.95f), new Vector2(25, -64), new Vector2(830, 26));
            tipGo.GetComponent<Text>().text = "快捷键: 1-5 换系 | A/D 切换 | Space 重播 | R 环绕 | C 慢动作 | 鼠标右键旋转，滚轮缩放";

            // 4. 分类横排按钮栏 (5个分类均衡铺满，按钮加宽加高，文字清晰)
            categoryButtons.Clear();
            categoryButtonTexts.Clear();
            float btnWidth = 156f;
            float btnHeight = 46f;
            float startX = 25f;
            float gap = 12f;
            for (int i = 0; i < categories.Count; i++)
            {
                int catIdx = i;
                var cat = categories[i];
                var catBtnGo = CreateButton($"CatBtn_{i}", panelGo.transform, uiFont, cat.categoryName, new Vector2(startX + i * (btnWidth + gap), -100), new Vector2(btnWidth, btnHeight), new Color(0.12f, 0.16f, 0.22f), 17);
                var btn = catBtnGo.GetComponent<Button>();
                var btnText = catBtnGo.GetComponentInChildren<Text>();

                categoryButtons.Add(btn);
                categoryButtonTexts.Add(btnText);
            }

            // 5. 当前特效信息卡片 (背景框扩大至 830x180，文字层级分明、字号大幅提升)
            var cardGo = new GameObject("InfoCard", typeof(RectTransform), typeof(Image));
            cardGo.transform.SetParent(panelGo.transform, false);
            var cardRect = cardGo.GetComponent<RectTransform>();
            cardRect.anchorMin = new Vector2(0, 1);
            cardRect.anchorMax = new Vector2(0, 1);
            cardRect.pivot = new Vector2(0, 1);
            cardRect.anchoredPosition = new Vector2(25, -156);
            cardRect.sizeDelta = new Vector2(830, 185);
            var cardImg = cardGo.GetComponent<Image>();
            cardImg.color = new Color(0.08f, 0.11f, 0.17f, 0.94f);
            cardImg.raycastTarget = false; // 严禁卡片底图拦截子控件或外层事件

            var titleTextGo = CreateText("InfoTitle", cardGo.transform, uiFont, 24, FontStyle.Bold, Color.yellow, new Vector2(20, -14), new Vector2(790, 34));
            infoTitleText = titleTextGo.GetComponent<Text>();

            var descTextGo = CreateText("InfoDesc", cardGo.transform, uiFont, 17, FontStyle.Normal, new Color(0.92f, 0.94f, 0.98f), new Vector2(20, -52), new Vector2(790, 78));
            infoDescriptionText = descTextGo.GetComponent<Text>();

            var pathTextGo = CreateText("InfoPath", cardGo.transform, uiFont, 14, FontStyle.Normal, new Color(0.5f, 0.72f, 0.9f), new Vector2(20, -144), new Vector2(790, 26));
            infoPathText = pathTextGo.GetComponent<Text>();

            // 6. 切换翻页控制栏 (三个大操作按钮)
            float actionBtnWidth1 = 255f;
            float actionBtnWidth2 = 288f;
            float actionBtnHeight = 52f;
            float actionY = -356f;

            var prevBtnGo = CreateButton("PrevBtn", panelGo.transform, uiFont, "◀ 上一个 (A)", new Vector2(25, actionY), new Vector2(actionBtnWidth1, actionBtnHeight), new Color(0.16f, 0.24f, 0.34f), 18);
            prevButton = prevBtnGo.GetComponent<Button>();

            var replayBtnGo = CreateButton("ReplayBtn", panelGo.transform, uiFont, "重新释放 (Space)", new Vector2(25 + actionBtnWidth1 + 16, actionY), new Vector2(actionBtnWidth2, actionBtnHeight), new Color(0.2f, 0.38f, 0.28f), 18);
            replayButton = replayBtnGo.GetComponent<Button>();

            var nextBtnGo = CreateButton("NextBtn", panelGo.transform, uiFont, "下一个 (D) ▶", new Vector2(25 + actionBtnWidth1 + 16 + actionBtnWidth2 + 16, actionY), new Vector2(actionBtnWidth1, actionBtnHeight), new Color(0.16f, 0.24f, 0.34f), 18);
            nextButton = nextBtnGo.GetComponent<Button>();

            // 7. 工具栏 (环绕、慢动作、重置视角)
            float toolBtnHeight = 44f;
            float toolY = -422f;

            var autoRotateBtnGo = CreateButton("AutoRotateBtn", panelGo.transform, uiFont, "镜头环绕: [开] (R)", new Vector2(25, toolY), new Vector2(actionBtnWidth1, toolBtnHeight), new Color(0.14f, 0.18f, 0.26f), 16);
            autoRotateButton = autoRotateBtnGo.GetComponent<Button>();
            autoRotateBtnText = autoRotateBtnGo.GetComponentInChildren<Text>();

            var slowMotionBtnGo = CreateButton("SlowMotionBtn", panelGo.transform, uiFont, "慢动作: [关] (C)", new Vector2(25 + actionBtnWidth1 + 16, toolY), new Vector2(actionBtnWidth2, toolBtnHeight), new Color(0.14f, 0.18f, 0.26f), 16);
            slowMotionButton = slowMotionBtnGo.GetComponent<Button>();
            slowMotionBtnText = slowMotionBtnGo.GetComponentInChildren<Text>();

            var resetCamBtnGo = CreateButton("ResetCamBtn", panelGo.transform, uiFont, "重置视角", new Vector2(25 + actionBtnWidth1 + 16 + actionBtnWidth2 + 16, toolY), new Vector2(actionBtnWidth1, toolBtnHeight), new Color(0.14f, 0.18f, 0.26f), 16);
            resetCamButton = resetCamBtnGo.GetComponent<Button>();

            BindButtonListeners();
            RefreshUI();
        }

        private GameObject CreateText(string name, Transform parent, Font font, int size, FontStyle style, Color color, Vector2 pos, Vector2 sizeDelta)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = pos;
            rect.sizeDelta = sizeDelta;

            var txt = go.GetComponent<Text>();
            txt.font = font;
            txt.fontSize = size;
            txt.fontStyle = style;
            txt.color = color;
            txt.alignment = TextAnchor.MiddleLeft;
            txt.horizontalOverflow = HorizontalWrapMode.Wrap;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            txt.lineSpacing = 1.25f;
            txt.raycastTarget = false;
            return go;
        }

        private GameObject CreateButton(string name, Transform parent, Font font, string text, Vector2 pos, Vector2 sizeDelta, Color bgColor, int fontSize = 16)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = pos;
            rect.sizeDelta = sizeDelta;

            var img = go.GetComponent<Image>();
            img.color = bgColor;
            img.raycastTarget = true; // 确保图片拦截射线并派发点击事件

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img; // 明确绑定 targetGraphic
            btn.interactable = true;
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.3f, 1.3f, 1.3f, 1f);
            colors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
            colors.selectedColor = Color.white;
            colors.colorMultiplier = 1f;
            btn.colors = colors;
            btn.navigation = new Navigation { mode = Navigation.Mode.None };

            var textGo = CreateText("Label", go.transform, font, fontSize, FontStyle.Normal, Color.white, Vector2.zero, sizeDelta);
            var txt = textGo.GetComponent<Text>();
            txt.text = text;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.raycastTarget = false; // 严禁文本抢占阻挡父级 Button 的射线

            return go;
        }

        public void RefreshUI()
        {
            if (infoTitleText == null || categories == null || categories.Count == 0) return;

            var cat = categories[Mathf.Clamp(currentCategoryIndex, 0, categories.Count - 1)];
            if (cat.entries != null && cat.entries.Count > 0)
            {
                var entry = cat.entries[Mathf.Clamp(currentEntryIndex, 0, cat.entries.Count - 1)];
                infoTitleText.text = $"[{currentEntryIndex + 1}/{cat.entries.Count}]   {entry.title}";
                infoTitleText.color = cat.themeColor;

                infoDescriptionText.text = entry.description;
                infoPathText.text = "资源: " + entry.assetPath;
            }

            // 更新分类按钮高亮
            for (int i = 0; i < categoryButtons.Count; i++)
            {
                if (i >= categories.Count) break;
                bool isSelected = (i == currentCategoryIndex);
                var btnImg = categoryButtons[i].GetComponent<Image>();
                var btnTxt = categoryButtonTexts[i];

                if (isSelected)
                {
                    btnImg.color = categories[i].themeColor * 0.75f;
                    btnTxt.color = Color.white;
                    btnTxt.fontStyle = FontStyle.Bold;
                    btnTxt.fontSize = 17;
                }
                else
                {
                    btnImg.color = new Color(0.12f, 0.16f, 0.22f, 0.9f);
                    btnTxt.color = new Color(0.75f, 0.8f, 0.85f);
                    btnTxt.fontStyle = FontStyle.Normal;
                    btnTxt.fontSize = 17;
                }
            }

            if (autoRotateBtnText != null)
            {
                autoRotateBtnText.text = autoRotateCamera ? "镜头环绕: [开] (R)" : "镜头环绕: [关] (R)";
            }

            if (slowMotionBtnText != null)
            {
                slowMotionBtnText.text = slowMotion ? "慢动作: [开 0.2x] (C)" : "慢动作: [关] (C)";
            }
        }

        #endregion

        public void InitializeCuratedList()
        {
            categories = new List<VFXCategory>();

            // 1. 风遁
            var wind = new VFXCategory
            {
                categoryName = "[1] 风遁",
                themeColor = new Color(0.35f, 1f, 0.85f),
                entries = new List<VFXEntry>
                {
                    new VFXEntry {
                        title = "风遁·龙卷飓风 (持续风暴)",
                        assetPath = "Assets/Imported/MasterMagicFX/ParticlesVer3/Winds/LargeTornado/Prefabs/WindContinous.prefab",
                        description = "持续性多层旋转飓风，气流升腾与外围旋转风环，极度震撼 (风遁·龙卷飓风)",
                        spawnOffset = Vector3.zero, spawnScale = 1.0f
                    },
                    new VFXEntry {
                        title = "风遁·大龙卷 (瞬间爆发)",
                        assetPath = "Assets/Imported/MasterMagicFX/ParticlesVer3/Winds/TornadoOneShotLarge/Prefabs/Par_TornadoOneShotLarge.prefab",
                        description = "单发瞬发巨型风暴爆发，地面气浪狂暴外扩冲击",
                        spawnOffset = Vector3.zero, spawnScale = 1.0f
                    },
                    new VFXEntry {
                        title = "风遁·大突破 (横向推进风压)",
                        assetPath = "Assets/Imported/MasterMagicFX/ParticlesVer3/Winds/HorizontalWind/Prefabs/Par_HorizontalWind.prefab",
                        description = "强力横向气浪喷涌，经典风遁大突破推进",
                        spawnOffset = new Vector3(0, 0.8f, 0), spawnScale = 1.0f
                    },
                    new VFXEntry {
                        title = "风遁·螺旋聚气 (气旋蓄力)",
                        assetPath = "Assets/Imported/MasterMagicFX/ParticlesVer3/Winds/WindCharge/Prefabs/WindCharge.prefab",
                        description = "中心高速内吸汇聚气流，极度贴合螺旋丸/风遁聚气蓄力",
                        spawnOffset = new Vector3(0, 1.2f, 0), spawnScale = 1.0f
                    },
                    new VFXEntry {
                        title = "风遁·真空连波 (疾风剑气飞刃)",
                        assetPath = "Assets/Imported/MasterMagicFX/ParticlesVer3/Slashes/WindSlashes/Prefabs/Par_WindSwordBeam.prefab",
                        description = "凌厉青风刀光呼啸前斩，镰鼬之术/真空风刃",
                        spawnOffset = new Vector3(0, 1.0f, 0), spawnScale = 1.0f
                    },
                    new VFXEntry {
                        title = "风遁·压害 (压缩空气风弹)",
                        assetPath = "Assets/Imported/MasterMagicFX/ParticlesVer3/Shoots/WindShoots/Prefabs/Par_WindBullet.prefab",
                        description = "高速压缩空气炮/风遁查克拉风弹",
                        spawnOffset = new Vector3(0, 1.0f, 0), spawnScale = 1.0f
                    },
                    new VFXEntry {
                        title = "风遁·瞬身气浪 (气旋瞬爆)",
                        assetPath = "Assets/Imported/MasterMagicFX/ParticlesVer3/Winds/WindFlash/Prefabs/Par_WindFlash.prefab",
                        description = "瞬身移动或短距冲刺时的环形气流爆散",
                        spawnOffset = new Vector3(0, 0.5f, 0), spawnScale = 1.0f
                    },
                    new VFXEntry {
                        title = "风遁·剑舞回天 (白色旋风气壁)",
                        assetPath = "Assets/Imported/EpicToonFX/Prefabs/Combat/Sword/Whirlwind/SwordWhirlwindWhite.prefab",
                        description = "周身环绕白色真空风壁，回天式剑舞防御",
                        spawnOffset = new Vector3(0, 0.2f, 0), spawnScale = 1.0f
                    }
                }
            };
            categories.Add(wind);

            // 2. 火遁
            var fire = new VFXCategory
            {
                categoryName = "[2] 火遁",
                themeColor = new Color(1.0f, 0.45f, 0.15f),
                entries = new List<VFXEntry>
                {
                    new VFXEntry {
                        title = "火遁·豪火灭却 (烈焰风暴喷涌)",
                        assetPath = "Assets/Imported/MasterMagicFX/ParticlesVer2/FlameThrower/StormFlameThrower/Prefabs/StormFlameThrower.prefab",
                        description = "狂暴多重火舌奔涌铺天盖地，极具斑爷豪火灭却压迫感",
                        spawnOffset = new Vector3(0, 0.5f, 0), spawnScale = 1.0f
                    },
                    new VFXEntry {
                        title = "火遁·龙火之术 (火柱持续喷射)",
                        assetPath = "Assets/Imported/MasterMagicFX/ParticlesVer2/FlameThrower/NormalFlameThrower/Prefabs/FlameThrower.prefab",
                        description = "标准前方持续炽烈喷吐，火柱吞噬前方目标",
                        spawnOffset = new Vector3(0, 0.5f, 0), spawnScale = 1.0f
                    },
                    new VFXEntry {
                        title = "火遁·天照幽炎 (紫炎喷射)",
                        assetPath = "Assets/Imported/MasterMagicFX/ParticlesVer2/FlameThrower/PurpleFlameThrower/Prefabs/FlameThrower.prefab",
                        description = "异色幽冥紫炎/天照黑紫烈火，神秘冷酷且视觉张力极强",
                        spawnOffset = new Vector3(0, 0.5f, 0), spawnScale = 1.0f
                    },
                    new VFXEntry {
                        title = "火遁·烈焰冲击 (贯穿粗火柱)",
                        assetPath = "Assets/Imported/MasterMagicFX/ParticlesVer2/Lasers/FireLaser/Prefabs/Par_Laser_FireThickLaser.prefab",
                        description = "高聚能高亮粗火柱贯穿轰击，热浪冲击",
                        spawnOffset = new Vector3(0, 0.8f, 0), spawnScale = 1.0f
                    },
                    new VFXEntry {
                        title = "火遁·豪火球大爆炸 (起爆炎爆)",
                        assetPath = "Assets/Imported/MasterMagicFX/ParticlesVer1/Explosions/Explosion_Fire/Prefabs/Par_FireExplosion3_Explosion.prefab",
                        description = "起爆符/巨型火球命中的剧烈冲击波与火焰爆散",
                        spawnOffset = new Vector3(0, 0.2f, 0), spawnScale = 1.0f
                    },
                    new VFXEntry {
                        title = "火遁·焦土残焰 (地面燃烧)",
                        assetPath = "Assets/Imported/MasterMagicFX/ParticlesVer1/Shoots/Shoot_Fire3/Prefabs/Par_GroundFire.prefab",
                        description = "火遁掠过后的地表火焰持续灼烧",
                        spawnOffset = Vector3.zero, spawnScale = 1.0f
                    },
                    new VFXEntry {
                        title = "火遁·豪火球飞弹 (经典火球)",
                        assetPath = "Assets/Imported/EpicToonFX/Prefabs/Combat/Missiles/Fireball/FireballMissileFire.prefab",
                        description = "经典巨大橘红火焰球向前呼啸飞行（已补偿静态预览发射率）",
                        spawnOffset = new Vector3(0, 1.2f, 0), spawnScale = 1.0f
                    },
                    new VFXEntry {
                        title = "火遁·豪火球爆散",
                        assetPath = "Assets/Imported/EpicToonFX/Prefabs/Combat/Explosions/FireballExplosion/ExplosionFireballFire.prefab",
                        description = "火球撞击炸裂，向四周溅射烈火与浓烟",
                        spawnOffset = new Vector3(0, 0.5f, 0), spawnScale = 1.0f
                    }
                }
            };
            categories.Add(fire);

            // 3. 雷遁
            var lightning = new VFXCategory
            {
                categoryName = "[3] 雷遁",
                themeColor = new Color(0.95f, 0.95f, 0.3f),
                entries = new List<VFXEntry>
                {
                    new VFXEntry {
                        title = "雷遁·天罚麒麟 (天降重雷轰顶)",
                        assetPath = "Assets/Imported/MasterMagicFX/ParticlesVer3/Lightnings/ThickLightningExp/Prefabs/Par_LightExplosion_Explosion.prefab",
                        description = "天降极粗雷蟒贯地，狂暴电光四散炸裂 (雷遁·麒麟)",
                        spawnOffset = Vector3.zero, spawnScale = 1.0f
                    },
                    new VFXEntry {
                        title = "雷遁·雷电球 (千鸟球/雷遁聚能)",
                        assetPath = "Assets/Imported/MasterMagicFX/ParticlesVer3/Lightnings/LightningBalls/Prefabs/Par_LightningBall.prefab",
                        description = "高聚能电芒跃动的雷球，适合雷遁掌心聚能",
                        spawnOffset = new Vector3(0, 1.0f, 0), spawnScale = 1.0f
                    },
                    new VFXEntry {
                        title = "雷遁·伪暗 (青白雷光贯穿束)",
                        assetPath = "Assets/Imported/MasterMagicFX/ParticlesVer3/Lightnings/LightningLaser/Prefabs/Par_Laser_CyanLaser.prefab",
                        description = "青白色高能雷光贯穿射线，角都雷遁·伪暗风格",
                        spawnOffset = new Vector3(0, 0.8f, 0), spawnScale = 1.0f
                    },
                    new VFXEntry {
                        title = "雷遁·金雷霹雳 (金光雷击)",
                        assetPath = "Assets/Imported/MasterMagicFX/ParticlesVer3/Lightnings/YellowLightning/Prefabs/Par_LightExplosion_Explosion.prefab",
                        description = "金黄色霹雳破空劈下，高能电火花四溅",
                        spawnOffset = Vector3.zero, spawnScale = 1.0f
                    },
                    new VFXEntry {
                        title = "雷遁·雷环爆发 (周身电磁环)",
                        assetPath = "Assets/Imported/MasterMagicFX/ParticlesVer3/Lightnings/LightningRing/Prefabs/Par_LightningRing.prefab",
                        description = "周身扩散的多道雷电环，适合雷遁护体/防反",
                        spawnOffset = new Vector3(0, 0.5f, 0), spawnScale = 1.0f
                    },
                    new VFXEntry {
                        title = "雷遁·雷切牵引 (电弧连线残影)",
                        assetPath = "Assets/Imported/MasterMagicFX/ParticlesVer3/Lightnings/LightningLine/Prefabs/LightningLine.prefab",
                        description = "雷切高速突刺时的拉扯电流残影",
                        spawnOffset = new Vector3(0, 1.0f, 0), spawnScale = 1.0f
                    },
                    new VFXEntry {
                        title = "雷遁·青电爆散 (千鸟电芒)",
                        assetPath = "Assets/Imported/EpicToonFX/Prefabs/Combat/Explosions/LightningExplosion/LightningExplosionBlue.prefab",
                        description = "千鸟击中时爆散出的青蓝电芒残弧",
                        spawnOffset = new Vector3(0, 0.8f, 0), spawnScale = 1.0f
                    },
                    new VFXEntry {
                        title = "雷遁·雷光剑 (项目现行雷剑)",
                        assetPath = "Assets/Art/VFX/ThunderSword/雷光剑.prefab",
                        description = "项目内置雷剑附魔与挥砍青弧",
                        spawnOffset = new Vector3(0, 1.0f, 0), spawnScale = 1.0f
                    }
                }
            };
            categories.Add(lightning);

            // 4. 查克拉 / 奥义 / 体术
            var chakra = new VFXCategory
            {
                categoryName = "[4] 查克拉",
                themeColor = new Color(0.3f, 0.65f, 1.0f),
                entries = new List<VFXEntry>
                {
                    new VFXEntry {
                        title = "查克拉·常态聚气外衣 (纯蓝查克拉)",
                        assetPath = "Assets/Imported/EpicToonFX/Prefabs/Combat/Magic/Aura/MagicAuraBlue.prefab",
                        description = "湛蓝纯净查克拉蒸腾升华外衣，无西洋符号",
                        spawnOffset = Vector3.zero, spawnScale = 1.0f
                    },
                    new VFXEntry {
                        title = "查克拉·九尾/死门血红外衣",
                        assetPath = "Assets/Imported/MasterMagicFX/ParticlesVer2/Auras/RedAura/Prefabs/Par_RedAura.prefab",
                        description = "狂暴猩红尾兽外衣 / 八门遁甲死门蒸腾血气",
                        spawnOffset = Vector3.zero, spawnScale = 1.0f
                    },
                    new VFXEntry {
                        title = "查克拉·八门遁甲景门绿气",
                        assetPath = "Assets/Imported/MasterMagicFX/ParticlesVer2/Auras/GreenAura/Prefabs/Par_GreenAura.prefab",
                        description = "八门遁甲景门全开蒸腾绿色汗气气浪",
                        spawnOffset = Vector3.zero, spawnScale = 1.0f
                    },
                    new VFXEntry {
                        title = "奥义·尾兽玉光炮 (粗能量柱)",
                        assetPath = "Assets/Imported/MasterMagicFX/ParticlesVer2/Lasers/ThickBlueLaser/Prefabs/Par_Laser_BlueThickLaser.prefab",
                        description = "震撼的纯蓝查克拉光炮，尾兽玉发射贯穿波",
                        spawnOffset = new Vector3(0, 0.8f, 0), spawnScale = 1.0f
                    },
                    new VFXEntry {
                        title = "查克拉·螺旋能量弹",
                        assetPath = "Assets/Imported/MasterMagicFX/ParticlesVer2/Shoots/BlueEnergyShoot/Prefabs/Par_BlueShoot_Bullet.prefab",
                        description = "高密度查克拉球弹向前飞出",
                        spawnOffset = new Vector3(0, 1.0f, 0), spawnScale = 1.0f
                    },
                    new VFXEntry {
                        title = "体术·八卦空掌 (纯空气冲击环)",
                        assetPath = "Assets/Imported/EpicToonFX/Prefabs/Combat/Brawling/RoundHit/RoundHitBlue.prefab",
                        description = "日向柔拳·八卦空掌/怪力打出的纯空气冲击环",
                        spawnOffset = new Vector3(0, 1.0f, 0), spawnScale = 1.0f
                    },
                    new VFXEntry {
                        title = "剑术·查克拉月牙飞刃",
                        assetPath = "Assets/Imported/EpicToonFX/Prefabs/Combat/Sword/Wave/SwordWaveBlue.prefab",
                        description = "武士刀挥出的月牙形湛蓝查克拉剑波",
                        spawnOffset = new Vector3(0, 1.0f, 0), spawnScale = 1.0f
                    },
                    new VFXEntry {
                        title = "封印术·八卦金光阵法 (项目自制)",
                        assetPath = "Assets/Art/VFX/Skills/Prefabs/地面阵法.prefab",
                        description = "项目自制八卦封印道家金光符文阵，绝无六芒星",
                        spawnOffset = Vector3.zero, spawnScale = 1.0f
                    }
                }
            };
            categories.Add(chakra);

            // 5. 微火与小火苗 (Tiny Flame)
            var tinyFlames = new VFXCategory
            {
                categoryName = "[5] 小火苗",
                themeColor = new Color(1.0f, 0.72f, 0.22f),
                entries = new List<VFXEntry>
                {
                    new VFXEntry {
                        title = "烛光微火·金黄 (柔和摇曳)",
                        assetPath = "Assets/Imported/EpicToonFX/Prefabs/Environment/Fire/Candle/CandleLightSoft/CandleLightSoftYellow.prefab",
                        description = "仅十余厘米高的微型烛火，带柔和光晕与微小火星跳跃，极度适合指尖起火、点燃起爆符或微型查克拉火苗",
                        spawnOffset = new Vector3(0, 0.8f, 0), spawnScale = 1.0f
                    },
                    new VFXEntry {
                        title = "烛光微火·赤红 (指尖明火)",
                        assetPath = "Assets/Imported/EpicToonFX/Prefabs/Environment/Fire/Candle/CandleLightSoft/CandleLightSoftRed.prefab",
                        description = "纯正赤红小火苗，微弱点光源与细腻火星跳动",
                        spawnOffset = new Vector3(0, 0.8f, 0), spawnScale = 1.0f
                    },
                    new VFXEntry {
                        title = "幽冥鬼火·幽紫 (异色微火)",
                        assetPath = "Assets/Imported/EpicToonFX/Prefabs/Environment/Fire/Candle/CandleLightSoft/CandleLightSoftPurple.prefab",
                        description = "幽暗紫火小火苗，天照余烬/暗夜通灵鬼火风格",
                        spawnOffset = new Vector3(0, 0.8f, 0), spawnScale = 1.0f
                    },
                    new VFXEntry {
                        title = "查克拉冷火·青蓝 (冷色火苗)",
                        assetPath = "Assets/Imported/EpicToonFX/Prefabs/Environment/Fire/Candle/CandleLightSoft/CandleLightSoftBlue.prefab",
                        description = "青蓝色冰焰/二尾又旅青火小火苗，冷色调极具神秘感",
                        spawnOffset = new Vector3(0, 0.8f, 0), spawnScale = 1.0f
                    },
                    new VFXEntry {
                        title = "柔和火苗·金黄 (团状小篝火)",
                        assetPath = "Assets/Imported/EpicToonFX/Prefabs/Environment/Fire/Soft/SoftFireADD/SoftFireAdditiveYellow.prefab",
                        description = "约半米高的团状柔和跳动小火簇，带热空气微光与火星上升，适合掌心托火或忍具附火",
                        spawnOffset = new Vector3(0, 0.5f, 0), spawnScale = 1.0f
                    },
                    new VFXEntry {
                        title = "柔和微火·赤红 (短簇明焰)",
                        assetPath = "Assets/Imported/EpicToonFX/Prefabs/Environment/Fire/Soft/SoftFireAB/SoftFireABRed.prefab",
                        description = "经典赤红小火团，半透明混合，火焰升腾自然",
                        spawnOffset = new Vector3(0, 0.5f, 0), spawnScale = 1.0f
                    },
                    new VFXEntry {
                        title = "火把火苗·卡通 (火炬跳焰)",
                        assetPath = "Assets/Imported/EpicToonFX/Prefabs/Environment/Fire/Cartoon/Torch/CartoonFireTorchRed.prefab",
                        description = "赛璐璐动漫风格的火把火苗，边缘轮廓鲜明，带卡通跳跃火星",
                        spawnOffset = new Vector3(0, 0.8f, 0), spawnScale = 1.0f
                    },
                    new VFXEntry {
                        title = "刺状火苗·查克拉 (烈炎锐火)",
                        assetPath = "Assets/Imported/EpicToonFX/Prefabs/Environment/Fire/Spiky/Additive/SpikyFireAdditiveYellow.prefab",
                        description = "跳动剧烈的尖刺火舌与火星青烟，具有强烈的查克拉灼烧感",
                        spawnOffset = new Vector3(0, 0.6f, 0), spawnScale = 1.0f
                    }
                }
            };
            categories.Add(tinyFlames);
        }
    }
}
