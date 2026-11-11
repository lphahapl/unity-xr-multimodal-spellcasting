using System;
using UnityEngine;
using UnityEngine.UI;

namespace NarutoVR.SealCombat.Rpg
{
    /// <summary>Read-only world HUD and explicit desktop simulation controls. Scene setup owns the world canvas position and scale.</summary>
    [DefaultExecutionOrder(650)]
    public sealed class RpgSessionView : MonoBehaviour
    {
        [Header("一、线性关卡界面接线")]
        [InspectorName("关卡控制器"), Tooltip("读取玩家与关卡状态；按钮通过控制器发出命令，不直接改玩家数值。")]
        public LinearRpgController controller;
        [InspectorName("世界空间状态文字"), Tooltip("用于关卡中的世界空间文字面板；位置、缩放与字体大小由场景负责。")]
        public Text statusText;
        [InspectorName("中文字体"), Tooltip("世界文字和电脑调试面板使用的中文字体；为空时尝试加载项目中的中文字体。")]
        public Font chineseFont;
        [Header("二、电脑调试显示")]
        [InspectorName("显示电脑调试按钮"), Tooltip("显示右侧可滚动的模拟输入面板；不会注册键盘快捷键。模拟施法仍检查能量、冷却和资格。")]
        public bool showDesktopControls = true;
        [InspectorName("电脑调试面板宽度（像素）"), Range(320, 720)] public float desktopPanelWidth = 460;
        [Header("三、调试分区（只影响电脑面板）")]
        [InspectorName("展开运行状态")] public bool showRuntimeSection;
        [InspectorName("展开移动输入与选点")] public bool showMovementSection = true;
        [InspectorName("展开投掷输入与拟合")] public bool showThrowSection = true;
        [InspectorName("展开麦克风与咒语诊断")] public bool showVoiceSection = true;
        [InspectorName("展开结印评分与特征明细")] public bool showSealSection = true;
        [InspectorName("展开模拟操作按钮")] public bool showSimulationSection;
        [Header("四、投掷记录和世界调试面板")]
        [InspectorName("投掷诊断记录器")] public ThrowDiagnosticRecorder throwRecorder;
        [InspectorName("显示世界调试面板")] public bool showWorldDiagnostics;
        [InspectorName("可开关的世界调试面板（不含腕饰子菜单）")] public GameObject[] diagnosticWorldPanels=Array.Empty<GameObject>();

        void Start() => SetWorldDiagnostics(showWorldDiagnostics);
        public void SetWorldDiagnostics(bool visible)
        {
            showWorldDiagnostics=visible;
            foreach(var panel in diagnosticWorldPanels) if(panel) panel.SetActive(visible);
        }

        double nextRefresh;
        RpgDesktopPanel desktop;

        void OnEnable()
        {
            presentationOwner = null;
            nextRefresh = 0;
            if (!chineseFont) chineseFont = Resources.Load<Font>("Fonts/NotoSansCJKsc-Regular");
        }

        void LateUpdate()
        {
            double now = Time.realtimeSinceStartupAsDouble;
            if (now < nextRefresh) return;
            nextRefresh = now + .1;
            if(statusText && statusText.isActiveAndEnabled) {
                if(chineseFont && statusText.font!=chineseFont)statusText.font=chineseFont;
                statusText.text=BuildHud();
            }
            if(showDesktopControls && controller && Screen.width>=160 && Screen.height>=100){
                if (desktop != null && desktop.ReadyAtCreation != controller.IsReady) ReleaseDesktop();
                desktop??=new RpgDesktopPanel(this);
                desktop.Refresh();
            } else ReleaseDesktop();
        }
        void OnDisable()=>ReleaseDesktop();
        void ReleaseDesktop(){desktop?.Dispose();desktop=null;}

        LinearRpgController presentationOwner;
        RpgInteractionFeedback feedback;

        internal RpgSessionPresentation Presentation
        {
            get
            {
                if (presentationOwner != controller)
                {
                    presentationOwner = controller;
                    feedback = controller ? controller.GetComponentInChildren<RpgInteractionFeedback>(true) : null;
                }
                return new RpgSessionPresentation(controller, feedback);
            }
        }
        public string BuildHud() => Presentation.BuildHud();
        public string BuildSealDetails() => Presentation.BuildSealDetails();
        public string BuildDesktopDebug(double now) => Presentation.BuildDesktopDebug(now);
    }
}
