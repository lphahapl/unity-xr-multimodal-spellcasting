using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace NarutoVR.SealCombat
{
    public enum SkillScenarioKind
    {
        [InspectorName("火球靶场")] FireballRange,
        [InspectorName("护盾试炼")] ShieldTrial,
        [InspectorName("组合施法试炼")] ComboSanctum
    }

    /// <summary>提供训练场说明和有限次数的护盾演示，正式玩家生命由 RPG 运行态管理。</summary>
    public sealed class SkillScenario : MonoBehaviour
    {
        [Header("一、试炼场景")]
        [InspectorName("场景类型")] public SkillScenarioKind kind;
        [InspectorName("场景中文标题")] public string title;
        [InspectorName("试炼说明"), TextArea(3, 8)] public string instructions;
        [InspectorName("技能效果执行器")] public SkillEffects effects;
        [InspectorName("独立世界说明面板")] public Text worldText;
        [InspectorName("中文字体")] public Font chineseFont;
        [Header("二、护盾演示（不修改玩家生命）")]
        [InspectorName("首次获得护盾后自动演示")] public bool autoRunOnFirstShield = true;
        [InspectorName("每次护盾测试伤害"), Min(1)] public float testDamage = 30;
        [InspectorName("测试间隔（秒）"), Min(.5f)] public float pulseInterval = 2.5f;
        [InspectorName("首击准备时间（秒）"), Min(.1f)] public float firstPulseDelay = .5f;
        [InspectorName("每轮最多测试次数"), Range(1, 12)] public int pulsesPerTrial = 4;
        [InspectorName("显示电脑测试按钮")] public bool showDesktopControls = true;

        public string Status { get; private set; } = "等待试炼开始";
        public bool IsTesting => running;
        public int PulseCount { get; private set; }
        public float UnabsorbedDamage { get; private set; }
        private bool running, armed, autoStarted;
        private double nextPulse, nextUI;
        private GUIStyle labelStyle, buttonStyle, toggleStyle;

        private void Start()
        {
            if (!effects) effects = GetComponent<SkillEffects>();
            Status = kind == SkillScenarioKind.ShieldTrial
                ? "先摆子印或念“护体咒”；获得护盾后自动开始一轮测试。"
                : "输入满足即激活；保留原有三种配方与取消方式。";
            RefreshText();
        }

        private void Update()
        {
            double now = Time.realtimeSinceStartupAsDouble;
            if (kind == SkillScenarioKind.ShieldTrial)
            {
                if (Time.timeScale <= 0) { if (running || armed) StopShieldTest(); return; }
                if (Keyboard.current != null && Keyboard.current.f8Key.wasPressedThisFrame)
                { if (running || armed) StopShieldTest(); else BeginShieldTest(); }
                bool hasShield = effects && effects.ShieldPoints > 0;
                if (!running && hasShield && (armed || (autoRunOnFirstShield && !autoStarted))) StartPulses(now);
                if (running && now >= nextPulse) ApplyPulse(now);
            }
            if (now >= nextUI) { nextUI = now + .15; RefreshText(); }
        }

        public void BeginShieldTest()
        {
            if (kind != SkillScenarioKind.ShieldTrial || !Application.isPlaying || !effects) return;
            if (running) return;
            autoStarted = true;
            if (effects.ShieldPoints > 0) StartPulses(Time.realtimeSinceStartupAsDouble);
            else { armed = true; Status = "测试已准备：请摆子印或念“护体咒”，无需再操作电脑。"; RefreshText(); }
        }

        public void StopShieldTest()
        {
            running = armed = false;
            autoStarted = true;
            Status = "护盾测试已停止。F8 可重新准备；没有对玩家生命进行结算。";
            RefreshText();
        }

        private void StartPulses(double now)
        {
            autoStarted = true; armed = false; running = true;
            PulseCount = 0; UnabsorbedDamage = 0;
            nextPulse = now + Mathf.Max(.1f, firstPulseDelay);
            Status = $"护盾测试已启动：{Mathf.Max(.1f, firstPulseDelay):0.#} 秒后首击，随后每 {Mathf.Max(.5f, pulseInterval):0.#} 秒测试 {testDamage:0} 点，最多 {pulsesPerTrial} 次。";
            RefreshText();
        }

        private void ApplyPulse(double now)
        {
            if (!effects || !effects.isActiveAndEnabled || effects.ShieldPoints <= 0)
            {
                running = false;
                Status = "护盾已结束，测试停止；没有在无盾状态持续产生伤害。";
                return;
            }
            float incoming = float.IsFinite(testDamage) ? Mathf.Max(1, testDamage) : 30;
            float remainder = effects.AbsorbDamage(incoming);
            PulseCount++; UnabsorbedDamage += remainder;
            Status = $"第 {PulseCount} 次：护盾吸收 {incoming - remainder:0}；未吸收伤害 {remainder:0}。\n剩余护盾 {effects.ShieldPoints:0}。未接入玩家生命系统。";
            Debug.Log("[护盾试炼] " + Status, this);
            if (PulseCount >= Mathf.Max(1, pulsesPerTrial) || effects.ShieldPoints <= 0)
            { running = false; Status += "\n本轮结束；F8 可重新准备。"; }
            else nextPulse = now + Mathf.Max(.5f, pulseInterval);
            RefreshText();
        }

        private void RefreshText()
        {
            if (!worldText) return;
            string countdown = running ? $"\n下次测试：{System.Math.Max(0, nextPulse - Time.realtimeSinceStartupAsDouble):0.0} 秒" : "";
            string trial = kind == SkillScenarioKind.ShieldTrial
                ? $"\n护盾 {(effects ? effects.ShieldPoints : 0):0}　测试 {PulseCount}/{pulsesPerTrial}{countdown}\n{Status}" : "";
            worldText.text = title + "\n\n" + instructions + trial;
        }

        private void OnGUI()
        {
            if (!showDesktopControls || kind != SkillScenarioKind.ShieldTrial) return;
            if (labelStyle == null)
            {
                labelStyle = new GUIStyle(GUI.skin.label) { font = chineseFont, fontSize = 15, wordWrap = true };
                buttonStyle = new GUIStyle(GUI.skin.button) { font = chineseFont, fontSize = 16 };
                toggleStyle = new GUIStyle(GUI.skin.toggle) { font = chineseFont, fontSize = 15, wordWrap = true };
            }
            GUILayout.BeginArea(new Rect(Mathf.Max(460, Screen.width - 420), 12, 405, 265), GUI.skin.box);
            GUILayout.Label("护盾试炼 · 数值演示", labelStyle);
            GUILayout.Label(Status, labelStyle);
            if (GUILayout.Button(running || armed ? "停止护盾测试（F8）" : "准备 / 开始护盾测试（F8）", buttonStyle))
            { if (running || armed) StopShieldTest(); else BeginShieldTest(); }
            bool auto = GUILayout.Toggle(autoRunOnFirstShield, "首次获得护盾后自动演示", toggleStyle);
            autoRunOnFirstShield = auto;
            GUILayout.Label("每次 30 点；仅调用护盾吸收接口。\n未吸收部分只报告，不伪造玩家生命。", labelStyle);
            GUILayout.EndArea();
        }

        private void OnApplicationPause(bool paused) { if (paused && (running || armed)) StopShieldTest(); }
        private void OnDisable() { running = armed = false; }
    }
}
