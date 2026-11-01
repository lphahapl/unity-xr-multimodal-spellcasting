using System;
using System.IO;
using System.Text;
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
                desktop??=new RpgDesktopPanel(this);
                desktop.Refresh();
            } else ReleaseDesktop();
        }
        void OnDisable()=>ReleaseDesktop();
        void ReleaseDesktop(){desktop?.Dispose();desktop=null;}

        public string BuildHud()
        {
            if (!controller) return "线性关卡 · 等待绑定关卡控制器";
            var text = new StringBuilder();
            text.AppendLine("线性关卡 · " + (string.IsNullOrEmpty(controller.StageName) ? "等待阶段" : controller.StageName));
            var player = controller.Player;
            var effects = controller.skills ? controller.skills.effects : null;
            if (player != null)
                text.AppendLine($"生命 {player.Health:0.#}/{player.MaxHealth:0.#}  能量 {player.Mana:0.#}/{player.MaxMana:0.#}  护盾 {(effects ? effects.ShieldPoints : 0):0.#}" + (player.Alive ? "" : "【已死亡】"));
            else text.AppendLine("玩家数据：尚未就绪");
            var movement = controller.locomotion;
            if (movement)
            {
                text.AppendLine($"落点：{(movement.CandidateNode ? movement.CandidateNode.nodeId : "未选择")}  确认 {Mathf.Clamp01(movement.DwellProgress) * 100:0}%");
                text.AppendLine($"闪避冷却 {movement.DodgeRemaining:0.0}秒｜{(movement.OnNode ? "预设点内" : "预设点外")}");
                text.AppendLine(movement.SealProtectionActive ? "结印保护：分开双手并稳定" : movement.Status);
            }
            if (effects && effects.HasHeldObject) {
                text.AppendLine($"持球剩余 {effects.HeldRemainingSeconds:0.0}s：{effects.ThrowStatus}");
                text.AppendLine($"前送评分 {effects.CurrentThrowScore.total:0}/{effects.CurrentThrowScore.threshold:0}｜保持 {effects.CurrentThrowScore.holdProgress:P0}｜最近发射：{effects.LastReleaseMode}");
            }
            text.AppendLine("目标：" + controller.ObjectiveStatus);
            var hazard = controller.GetComponent<RpgTrainingHazard>();
            if (hazard && hazard.WarningActive) text.AppendLine($"【攻击预警】{hazard.WarningRemaining:0.0}秒，准备闪避或护盾");
            var feedback = controller.GetComponent<RpgInteractionFeedback>();
            text.AppendLine("提示：" + (feedback ? feedback.LastFeedback : controller.Status));
            text.Append("详细输入、评分和投掷数据见电脑调试面板");
            return text.ToString();
        }

        public string BuildSealDetails() {
            var lab=controller && controller.skills ? controller.skills.lab : null;
            if(!lab) return "尚未绑定结印实验台";
            var b=new StringBuilder();
            b.AppendLine("技能匹配："+controller.skills.Engine?.Status);
            b.AppendLine(controller.skills.Engine?.ObservationDiagnostics);
            b.AppendLine($"结印状态：{lab.RecognitionState}；已识别：{(lab.Recognized ? lab.Recognized.sealName : "无")}；保持 {lab.HoldProgress:P0}");
            foreach(var score in lab.Scores) {
                b.AppendLine($"{score.profile.sealName}：得分 {score.score:P1} / 门槛 {score.profile.threshold:P1}；覆盖 {score.coverage:P0}；{score.reason}");
                foreach(var d in score.details) b.AppendLine($"  {d.rule.featureId}：{(d.available ? d.feature.value.ToString("F2") : "不可用")}，范围 {d.rule.min:F2}~{d.rule.max:F2}，权重 {d.rule.weight:F2}，匹配 {d.match:P0}{(d.rule.required ? " [必需]" : "")}");
            }
            if(lab.Scores.Count==0)b.AppendLine("尚无可评分数据："+lab.Status);
            return b.ToString();
        }
        public string BuildVoiceDetails() {
            if(!controller || !controller.skills) return "尚未绑定技能系统";
            var s=controller.skills;
            return (s.voice ? s.voice.BuildDebugDetails() : "未绑定语音接收器")+
                "\n输入占用："+(controller.UiInputOwned?"UI操作中":controller.locomotion && controller.locomotion.NavigationActive?"移动中":"无")+
                "\n待组合技能："+(s.Engine?.PendingSkillId ?? "无")+"\n结印："+(s.lab && s.lab.Recognized ? s.lab.Recognized.name : "未命中")+
                "\n最近施法："+controller.Status;
        }
        public string BuildMovementDetails(double now) => controller && controller.locomotion
            ? controller.locomotion.BuildDebugDetails(now) : "尚未绑定移动控制器";

        public string BuildThrowDetails(double now) => controller && controller.skills && controller.skills.effects
            ? controller.skills.effects.BuildThrowDebugDetails(now) : "尚未绑定投掷效果执行器";

        public string BuildDesktopDebug(double now) => "【运行状态】\n" + BuildRuntimeDetails() +
            "\n\n【移动输入与选点】\n" + BuildMovementDetails(now) + "\n\n【投掷输入与拟合】\n" + BuildThrowDetails(now);

        public string BuildRuntimeDetails()
        {
            if (!controller) return "线性关卡 · 等待绑定关卡控制器";
            var builder = new StringBuilder();
            builder.AppendLine("线性关卡 · " + (string.IsNullOrEmpty(controller.StageName) ? "等待阶段" : controller.StageName));
            var player = controller.Player;
            var effects = controller.skills ? controller.skills.effects : null;
            if (player == null) builder.AppendLine("玩家数据：尚未就绪");
            else
            {
                builder.AppendLine($"生命 {player.Health:0.#}/{player.MaxHealth:0.#}  能量 {player.Mana:0.#}/{player.MaxMana:0.#}");
                builder.AppendLine($"护盾 {(effects ? effects.ShieldPoints : 0):0.#}" + (player.Alive ? "" : "  【已死亡，请恢复检查点】"));
                var definitions = controller.skills ? controller.skills.ConfiguredSkills : null;
                if (definitions != null)
                {
                    foreach (var definition in definitions)
                    {
                        if (!definition) continue;
                        string group = string.IsNullOrEmpty(definition.cooldownGroup) ? definition.skillId : definition.cooldownGroup;
                        bool unlocked = controller.Progress != null && controller.Progress.unlockedSkillIds != null &&
                            controller.Progress.unlockedSkillIds.Contains(definition.skillId);
                        builder.AppendLine($"{SkillName(definition)}（{(unlocked ? "已解锁" : "未解锁")}）冷却 {player.GetCooldown(group, controller.GameTime):0.0}秒");
                    }
                }
            }
            var locomotion = controller.locomotion;
            builder.AppendLine(controller.HandPoseStatus);
            if (controller.wristMenu) builder.AppendLine(controller.wristMenu.DebugDetails);
            if (effects && effects.HasHeldObject && !string.IsNullOrEmpty(effects.ThrowStatus)) builder.AppendLine("持球：" + effects.ThrowStatus);
            if (locomotion)
            {
                string candidate = locomotion.CandidateNode != null ? locomotion.CandidateNode.nodeId : "无";
                builder.AppendLine($"导航：{(locomotion.NavigationActive ? "开启" : "关闭")}  候选：{candidate}");
                builder.AppendLine($"驻留：{Mathf.Clamp01(locomotion.DwellProgress) * 100:0}%  闪避冷却：{locomotion.DodgeRemaining:0.0}秒");
                builder.AppendLine(locomotion.OnNode ? "位置：预设点内（" + locomotion.CurrentNodeId + "）" : "位置：预设点外");
                builder.AppendLine("移动：" + locomotion.Status);
                if (locomotion.CandidateNode != null) builder.AppendLine(locomotion.SelectionDetail);
                builder.AppendLine(locomotion.SealProtectionActive ? "【结印保护】分开双手后重新划动" : "右掌张开＋四向划动＝闪避（无需左手指向）");
            }
            else builder.AppendLine("导航：尚未绑定移动控制器");
            builder.AppendLine("当前目标：" + controller.ObjectiveStatus);
            if (controller.Progress != null) builder.AppendLine("货币：" + controller.Progress.currency);
            var hazard = controller.GetComponent<RpgTrainingHazard>();
            if (hazard && hazard.WarningActive) builder.AppendLine($"【红圈攻击预警】{hazard.WarningRemaining:0.0} 秒后结算，请闪避或用护盾承受");
            builder.AppendLine("最近操作：" + controller.Status);
            var feedback = controller.GetComponent<RpgInteractionFeedback>();
            if (feedback) builder.AppendLine("操作提示：" + feedback.LastFeedback);
            builder.Append("存档：" + ShortSavePath(controller.SavePath));
            return builder.ToString();
        }

        static string SkillName(SkillDefinition definition) => string.IsNullOrEmpty(definition.displayName) ? definition.skillId : definition.displayName;
        static string ShortSavePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return "尚未设置本地路径";
            try { return Path.GetFileName(path) + "（本地检查点）"; }
            catch (ArgumentException) { return "本地检查点路径无效"; }
        }
    }
}
