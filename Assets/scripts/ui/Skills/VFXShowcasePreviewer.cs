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
        public enum PreviewMotion { Stationary, BreathPilot, FlyingBreath }

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
            public PreviewMotion motion;
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
        public int currentCategoryIndex = 1;
        public int currentEntryIndex = 0;

        [Header("生成与展示挂点")]
        public Transform spawnRoot;
        public GameObject currentInstance;
        [SerializeField] private BreathEffectDefinition dragonBreath;
        [SerializeField] private BreathEffectDefinition flyingBreath;
        [SerializeField] private Material impactTargetMaterial;
        private GameObject dragonStream;
        private GameObject impactTarget;
        private GameObject flightImpact;
        private readonly SurfaceMarkPool previewMarks = new();
        private const float FlightSpeed = 5.5f;
        private const float FlightHitTime = 1.12f;
        private const float FlightReplayTime = 2.82f;

        [Header("镜头控制")]
        public Camera showcaseCamera;
        public Vector3 cameraPivot = new Vector3(0, 1.0f, 0);
        public float cameraDistance = 3.5f;
        public float cameraPitch = 18.0f;
        public float cameraYaw = 0f;
        public bool autoRotateCamera;
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

        private const float PanelWidth = 430f;
        private float panelPixelWidth = PanelWidth;
        private PreviewMotion previewMotion;
        private Vector3 previewOrigin;
        private float previewTime;
        private string previewPhase;
        private Vector2Int viewportSize;

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
            if (Application.isPlaying && previewMotion == PreviewMotion.FlyingBreath && currentInstance)
                SpawnCurrentEffect();
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

            ResetCamera();
            SpawnCurrentEffect();
        }

        private void Update()
        {
            if (viewportSize.x != Screen.width || viewportSize.y != Screen.height)
            {
                viewportSize = new Vector2Int(Screen.width, Screen.height);
                SetCanvasScale();
                UpdateCameraTransform();
            }
            HandleInputs();
            if (Application.isPlaying) AdvancePreview();

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
            if (dragonBreath == null)
                dragonBreath = AssetDatabase.LoadAssetAtPath<BreathEffectDefinition>("Assets/ScriptableObjects/吐火效果配置/龙火之术效果.asset");
            if (flyingBreath == null)
                flyingBreath = AssetDatabase.LoadAssetAtPath<BreathEffectDefinition>("Assets/ScriptableObjects/吐火效果配置/飞炎术效果_玩家.asset");
            if (impactTargetMaterial == null)
                impactTargetMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Showcase/石板银边.mat");
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
            SetDragonStream(false);
            ClearFlightImpact();
            ReleasePreviewObject(ref currentInstance);

            if (categories == null || categories.Count == 0) return;
            currentCategoryIndex = Mathf.Clamp(currentCategoryIndex, 0, categories.Count - 1);
            var cat = categories[currentCategoryIndex];
            if (cat.entries == null || cat.entries.Count == 0) return;
            currentEntryIndex = Mathf.Clamp(currentEntryIndex, 0, cat.entries.Count - 1);

            var entry = cat.entries[currentEntryIndex];
            previewMotion = entry.motion;
            previewTime = 0f;
            previewPhase = null;
            if (entry.prefab == null)
            {
                EnsureAllPrefabsLoaded();
            }

            if (entry.prefab != null)
            {
                Vector3 pos = (spawnRoot != null ? spawnRoot.position : Vector3.zero) + entry.spawnOffset;
                previewOrigin = pos;
                if (previewMotion == PreviewMotion.FlyingBreath)
                {
                    if (Application.isPlaying) CreateImpactTarget();
                    cameraPivot = pos + Vector3.forward * 0.8f;
                    UpdateCameraTransform();
                }
                currentInstance = Instantiate(entry.prefab, pos, Quaternion.identity);
                currentInstance.name = "[Showcase] " + entry.title;
                if (spawnRoot != null) currentInstance.transform.SetParent(spawnRoot, true);
                currentInstance.transform.localScale = Vector3.one * (entry.spawnScale > 0 ? entry.spawnScale : 1f);

                if (previewMotion != PreviewMotion.Stationary)
                {
                    RefreshUI();
                    return;
                }

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

        private void OnDisable()
        {
            SetDragonStream(false);
            ClearFlightImpact();
            if (Application.isPlaying && previewMotion == PreviewMotion.FlyingBreath && currentInstance)
                currentInstance.SetActive(false);
        }

        private static void ReleasePreviewObject(ref GameObject visual)
        {
            if (!visual) return;
            visual.SetActive(false);
            if (Application.isPlaying) Destroy(visual);
            else DestroyImmediate(visual);
            visual = null;
        }

        private void ClearFlightImpact()
        {
            previewMarks.Clear();
            ReleasePreviewObject(ref flightImpact);
            ReleasePreviewObject(ref impactTarget);
        }

        private void CreateImpactTarget()
        {
            // The fire head extends past its moving origin. Meet its leading edge with the near face.
            var contact = previewOrigin + Vector3.forward * (FlightHitTime * FlightSpeed + .32f);
            var rotation = Quaternion.Euler(0, -18, 0);
            const float thickness = .16f;
            impactTarget = GameObject.CreatePrimitive(PrimitiveType.Cube);
            impactTarget.name = "[Showcase] 飞炎撞击石板";
            impactTarget.transform.SetParent(spawnRoot, false);
            impactTarget.transform.localScale = new Vector3(2.35f, 2.1f, thickness);
            impactTarget.transform.SetPositionAndRotation(contact + rotation * Vector3.forward * (thickness / 2), rotation);
            if (impactTargetMaterial) impactTarget.GetComponent<Renderer>().sharedMaterial = impactTargetMaterial;
        }

        private void PlayFlightImpact()
        {
            if (!impactTarget || !flyingBreath) return;
            var receiver = impactTarget.GetComponent<Collider>();
            var travel = Vector3.forward;
            if (!receiver.Raycast(new Ray(previewOrigin, travel), out var hit, FlightSpeed * FlightHitTime + 1f)) return;
            previewMarks.Stamp(new SurfaceContact(hit), flyingBreath.surfaceMark, travel, previewTime, 1);
            if (!flyingBreath.impactPrefab) return;
            flightImpact = Instantiate(flyingBreath.impactPrefab, hit.point, Quaternion.LookRotation(hit.normal), spawnRoot);
            flightImpact.name = "[Showcase] 飞炎命中爆散";
            // The preview uses scaled time, including its slow-motion and replay controls.
            if (flightImpact.TryGetComponent<SkillVisualLifetime>(out var lifetime)) lifetime.enabled = false;
        }

        private void SetDragonStream(bool spraying)
        {
            if (spraying && dragonStream == null && dragonBreath != null && dragonBreath.visualPrefab != null)
            {
                dragonStream = Instantiate(dragonBreath.visualPrefab, previewOrigin, Quaternion.identity, spawnRoot);
                dragonStream.name = "[Showcase] 龙火吹气喷流";
                dragonStream.SetActive(false);
                dragonBreath.ConfigureVisual(dragonStream);
                dragonStream.SetActive(true);
            }
            else if (!spraying && dragonStream != null)
            {
                dragonStream.SetActive(false);
                if (Application.isPlaying) Destroy(dragonStream);
                else DestroyImmediate(dragonStream);
                dragonStream = null;
            }
            else return;

            UpdateCameraTransform();
        }

        private void AdvancePreview()
        {
            if (previewMotion == PreviewMotion.Stationary || currentInstance == null) return;
            previewTime += Time.deltaTime;
            bool visible;
            string phase;
            if (previewMotion == PreviewMotion.BreathPilot)
            {
                // The showcase owns this clock. In gameplay, breath input owns these transitions.
                if (previewTime >= 6f) { SpawnCurrentEffect(); return; }
                SetDragonStream(previewTime >= 1.5f && previewTime < 3f);
                visible = previewTime < 1.5f || (previewTime >= 3f && previewTime < 4.6f);
                phase = previewTime < 1.5f ? "已确认 · 待吹" : previewTime < 3f ? "吹气中" :
                    previewTime < 4.6f ? "停吹 · 火苗恢复" : "术已结束";
            }
            else
            {
                if (previewTime >= FlightReplayTime) { SpawnCurrentEffect(); return; }
                visible = previewTime < FlightHitTime;
                phase = previewTime < 0.15f ? "吐火" : previewTime < 0.4f ? "火球成形" :
                    visible ? "飞行" : previewTime < FlightHitTime + 1.1f ? "命中 · 爆散" : "命中处余迹";
                float flightTime = Mathf.Min(previewTime, FlightHitTime);
                currentInstance.transform.position = previewOrigin + Vector3.forward * (flightTime * FlightSpeed);
                cameraPivot = previewOrigin + Vector3.forward * (0.8f + flightTime * 4.75f);
                UpdateCameraTransform();
                if (!visible && currentInstance.activeSelf) PlayFlightImpact();
                previewMarks.Tick(previewTime, 1);
            }

            if (currentInstance.activeSelf != visible) currentInstance.SetActive(visible);
            if (previewPhase == phase) return;
            previewPhase = phase;
            RefreshUI();
        }

        public void SwitchCategory(int index)
        {
            if (categories == null || categories.Count == 0) return;
            currentCategoryIndex = Mathf.Clamp(index, 0, categories.Count - 1);
            currentEntryIndex = 0;
            ResetCamera();
            SpawnCurrentEffect();
        }

        public void NextEffect()
        {
            if (categories == null || categories.Count == 0) return;
            var cat = categories[currentCategoryIndex];
            if (cat.entries == null || cat.entries.Count == 0) return;
            currentEntryIndex = (currentEntryIndex + 1) % cat.entries.Count;
            ResetCamera();
            SpawnCurrentEffect();
        }

        public void PreviousEffect()
        {
            if (categories == null || categories.Count == 0) return;
            var cat = categories[currentCategoryIndex];
            if (cat.entries == null || cat.entries.Count == 0) return;
            currentEntryIndex = (currentEntryIndex - 1 + cat.entries.Count) % cat.entries.Count;
            ResetCamera();
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
            var cat = categories.Count > currentCategoryIndex && currentCategoryIndex >= 0 ? categories[currentCategoryIndex] : null;
            var entry = cat != null && cat.entries.Count > currentEntryIndex && currentEntryIndex >= 0 ? cat.entries[currentEntryIndex] : null;
            var motion = entry != null ? entry.motion : PreviewMotion.Stationary;
            cameraDistance = currentCategoryIndex == 4 ? 2.8f : 4.5f;
            cameraPitch = 18f;
            cameraYaw = 0f;
            cameraPivot = new Vector3(0f, 1f, 0f);
            if (motion == PreviewMotion.BreathPilot || motion == PreviewMotion.FlyingBreath)
            {
                bool pilot = motion == PreviewMotion.BreathPilot;
                var origin = spawnRoot == null ? transform.position : spawnRoot.position;
                cameraPivot = origin + entry.spawnOffset + (pilot ? Vector3.up * 0.025f : Vector3.forward * 0.8f);
                cameraDistance = pilot ? 0.38f : 3.35f;
                cameraPitch = 6f;
                cameraYaw = pilot ? -90f : -65f;
            }
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
                    cameraDistance *= scroll > 0f ? 0.85f : 1f / 0.85f;
                    cameraDistance = Mathf.Clamp(cameraDistance, 0.2f, 18f);
                    UpdateCameraTransform();
                }
            }
#endif
        }

        private void UpdateCameraTransform()
        {
            if (showcaseCamera == null) return;
            Quaternion rot = Quaternion.Euler(cameraPitch, cameraYaw, 0);
            // Frame the stage in the area to the right of the control panel.
            float panelPixels = panelPixelWidth + 24f;
            float reserved = Mathf.Min(0.58f, panelPixels / Mathf.Max(1f, Screen.width));
            float distance = cameraDistance;
            Vector3 pivot = cameraPivot;
            if (dragonStream != null)
            {
                // Widen the side view only while the full-size gameplay jet is on screen.
                distance += 1.7f;
                pivot += Vector3.forward * 0.65f;
            }
            float halfWidth = distance * Mathf.Tan(0.5f * showcaseCamera.fieldOfView * Mathf.Deg2Rad) * showcaseCamera.aspect;
            Vector3 center = pivot - rot * Vector3.right * (halfWidth * reserved);
            showcaseCamera.transform.SetPositionAndRotation(center - rot * Vector3.forward * distance, rot);
        }

        #region uGUI 现代化界面构建与更新

        public void EnsureEventSystem()
        {
            var eventSystems = FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            UnityEngine.EventSystems.EventSystem activeEs = null;
            foreach (var candidate in eventSystems)
            {
                if (candidate.gameObject.scene != gameObject.scene) continue;
                if (activeEs == null) activeEs = candidate;
                else if (candidate.transform.IsChildOf(transform))
                {
                    if (Application.isPlaying) Destroy(candidate.gameObject);
                    else DestroyImmediate(candidate.gameObject);
                }
            }
            if (activeEs == null)
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

        [ContextMenu("重建预览面板")]
        public void RebuildUGUIContextMenu()
        {
            BuildUGUIHierarchy();
        }

        public void EnsureUGUI()
        {
            EnsureEventSystem();
            if (uiCanvas != null && infoTitleText != null && categoryButtons.Count > 0 && prevButton != null)
            {
                var mainPanel = uiCanvas.transform.Find("MainControlPanel") as RectTransform;
                if (mainPanel != null && mainPanel.Find("InfoDesc") != null && categoryButtons.Count == categories.Count)
                {
                    SetCanvasScale();
                    BindButtonListeners();
                    return;
                }
            }
            BuildUGUIHierarchy();
        }

        private void SetCanvasScale()
        {
            if (uiCanvas == null) return;
            uiCanvas.pixelPerfect = true;
            var scaler = uiCanvas.GetComponent<CanvasScaler>();
            // Overlay text is rasterised in device pixels; integer scaling keeps CJK strokes aligned.
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = Mathf.Max(1f, Mathf.Floor(Mathf.Min(Screen.width / 1200f, Screen.height / 700f)));
            var panel = uiCanvas.transform.Find("MainControlPanel") as RectTransform;
            if (panel == null || panel.Find("InfoDesc") == null) return;

            bool compact = Screen.width < 900 || Screen.height < 510;
            float width = compact ? 350f : PanelWidth;
            panelPixelWidth = width * scaler.scaleFactor;
            panel.sizeDelta = new Vector2(width, compact ? 264f : 388f);
            panel.Find("HeaderTitle").gameObject.SetActive(!compact);
            panel.Find("TipText").gameObject.SetActive(!compact);
            infoPathText.gameObject.SetActive(!compact);

            for (int i = 0; i < categoryButtons.Count; i++)
                PositionControl((RectTransform)categoryButtons[i].transform, compact ? 8 + i * 67 : 14 + i * 81, compact ? -8 : -79, compact ? 65 : 77, 36);

            PositionControl(infoTitleText.rectTransform, 14, compact ? -48 : -125, width - 28, 52);
            PositionControl(infoDescriptionText.rectTransform, 14, compact ? -99 : -181, width - 28, 67);
            var columns = new[] { prevButton, replayButton, nextButton, autoRotateButton, slowMotionButton, resetCamButton };
            for (int i = 0; i < columns.Length; i++)
            {
                var button = columns[i];
                int col = i % 3;
                float x = compact ? 8 + col * 112 : col == 0 ? 14 : col == 1 ? 145 : 291;
                float y = compact ? -176 - (i / 3) * 42 : -298 - (i / 3) * 42;
                float w = compact ? 108 : col == 1 ? 140 : 125;
                PositionControl((RectTransform)button.transform, x, y, w, 36);
            }

        }

        private static void PositionControl(RectTransform rect, float x, float y, float w, float h)
        {
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(w, h);
            if (rect.TryGetComponent<Button>(out _) && rect.childCount > 0)
                ((RectTransform)rect.GetChild(0)).sizeDelta = rect.sizeDelta;
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

            SetCanvasScale();

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

            if (uiFont == null) uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var panel = new GameObject("MainControlPanel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(canvasGo.transform, false);
            var rect = (RectTransform)panel.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(12f, -12f);
            rect.sizeDelta = new Vector2(PanelWidth, 388f);
            panel.GetComponent<Image>().color = new Color(0.05f, 0.07f, 0.12f, 0.96f);
            var parent = panel.transform;

            CreateText("HeaderTitle", parent, uiFont, 23, FontStyle.Bold, Color.white, new Vector2(14, -8), new Vector2(402, 36))
                .GetComponent<Text>().text = "忍术特效预览";
            CreateText("TipText", parent, uiFont, 16, FontStyle.Normal, new Color(0.79f, 0.86f, 0.95f), new Vector2(14, -45), new Vector2(402, 25))
                .GetComponent<Text>().text = "1–5 切换分类    右键环绕    滚轮缩放";

            categoryButtons.Clear();
            categoryButtonTexts.Clear();
            Color buttonColor = new Color(0.13f, 0.19f, 0.28f);
            for (int i = 0; i < categories.Count; i++)
            {
                string label = categories[i].categoryName.Replace("[", "").Replace("]", "");
                var button = CreateButton($"CatBtn_{i}", parent, uiFont, label, new Vector2(14 + i * 81, -79), new Vector2(77, 36), buttonColor, 16);
                categoryButtons.Add(button.GetComponent<Button>());
                categoryButtonTexts.Add(button.GetComponentInChildren<Text>());
            }

            infoTitleText = CreateText("InfoTitle", parent, uiFont, 20, FontStyle.Bold, Color.white, new Vector2(14, -125), new Vector2(402, 52)).GetComponent<Text>();
            infoDescriptionText = CreateText("InfoDesc", parent, uiFont, 17, FontStyle.Normal, Color.white, new Vector2(14, -181), new Vector2(402, 67)).GetComponent<Text>();
            infoPathText = CreateText("InfoPath", parent, uiFont, 15, FontStyle.Normal, new Color(0.72f, 0.83f, 0.93f), new Vector2(14, -249), new Vector2(402, 41)).GetComponent<Text>();

            prevButton = CreateButton("PrevBtn", parent, uiFont, "上一个 (A)", new Vector2(14, -298), new Vector2(125, 36), buttonColor).GetComponent<Button>();
            replayButton = CreateButton("ReplayBtn", parent, uiFont, "重播 (Space)", new Vector2(145, -298), new Vector2(140, 36), new Color(0.20f, 0.38f, 0.28f)).GetComponent<Button>();
            nextButton = CreateButton("NextBtn", parent, uiFont, "下一个 (D)", new Vector2(291, -298), new Vector2(125, 36), buttonColor).GetComponent<Button>();
            autoRotateButton = CreateButton("AutoRotateBtn", parent, uiFont, "", new Vector2(14, -340), new Vector2(125, 36), buttonColor).GetComponent<Button>();
            slowMotionButton = CreateButton("SlowMotionBtn", parent, uiFont, "", new Vector2(145, -340), new Vector2(140, 36), buttonColor).GetComponent<Button>();
            resetCamButton = CreateButton("ResetCamBtn", parent, uiFont, "重置视角", new Vector2(291, -340), new Vector2(125, 36), buttonColor).GetComponent<Button>();
            autoRotateBtnText = autoRotateButton.GetComponentInChildren<Text>();
            slowMotionBtnText = slowMotionButton.GetComponentInChildren<Text>();

            SetCanvasScale();
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
            txt.lineSpacing = 1f;
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

                infoDescriptionText.text = previewPhase == null ? entry.description : previewPhase + "\n" + entry.description;
                infoPathText.text = "预制体  " + System.IO.Path.GetFileName(entry.assetPath);
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
                    btnTxt.fontSize = 16;
                }
                else
                {
                    btnImg.color = new Color(0.12f, 0.16f, 0.22f, 0.9f);
                    btnTxt.color = new Color(0.75f, 0.8f, 0.85f);
                    btnTxt.fontStyle = FontStyle.Normal;
                    btnTxt.fontSize = 16;
                }
            }

            if (autoRotateBtnText != null)
            {
                autoRotateBtnText.text = autoRotateCamera ? "环绕 开 (R)" : "环绕 关 (R)";
            }

            if (slowMotionBtnText != null)
            {
                slowMotionBtnText.text = slowMotion ? "慢放 开 (C)" : "慢放 关 (C)";
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
                        title = "龙火之术 · 待吹火苗",
                        assetPath = "Assets/Art/VFX/Skills/Prefabs/龙火待吹火苗.prefab",
                        description = "开吹隐藏火苗，停吹复现，施术结束熄灭。",
                        motion = PreviewMotion.BreathPilot,
                        spawnOffset = new Vector3(0f, 1f, 0f)
                    },
                    new VFXEntry {
                        title = "飞炎术 · 吐火成球",
                        assetPath = "Assets/Art/VFX/Skills/Prefabs/飞炎吐火球.prefab",
                        description = "按 C 慢放，观察火球撞上石板后的爆散与焦痕。",
                        motion = PreviewMotion.FlyingBreath,
                        spawnOffset = new Vector3(0f, 1f, -2.8f)
                    },
                    new VFXEntry {
                        title = "火遁·豪火灭却 (烈焰风暴喷涌)",
                        assetPath = "Assets/Imported/MasterMagicFX/ParticlesVer2/FlameThrower/StormFlameThrower/Prefabs/StormFlameThrower.prefab",
                        description = "狂暴多重火舌奔涌铺天盖地，极具斑爷豪火灭却压迫感",
                        spawnOffset = new Vector3(0, 0.5f, 0), spawnScale = 1.0f
                    },
                    new VFXEntry {
                        title = "火遁·龙火之术 (火柱持续喷射)",
                        assetPath = "Assets/Art/VFX/Skills/Prefabs/龙火定向喷流.prefab",
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
