using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace NarutoVR.SealCombat.Rpg
{
    /// <summary>将当前会话的只读状态投影为世界 HUD 和桌面诊断文字。</summary>
    internal readonly struct RpgSessionPresentation
    {
        readonly LinearRpgController controller;
        readonly RpgInteractionFeedback feedback;
        public RpgSessionPresentation(LinearRpgController controller, RpgInteractionFeedback feedback)
        {
            this.controller = controller;
            this.feedback = feedback;
        }

        public string BuildHud()
        {
            if (!controller) return "线性关卡 · 等待绑定关卡控制器";
            if (!controller.IsReady) return controller.Status;
            var text = new StringBuilder();
            text.AppendLine("线性关卡 · " + (string.IsNullOrEmpty(controller.StageName) ? "等待阶段" : controller.StageName));
            var player = controller.PlayerView;
            var effects = controller.skills ? controller.skills.effects : null;
            if (player != null)
                text.AppendLine($"生命 {player.Health:0.#}/{player.MaxHealth:0.#}  能量 {player.Mana:0.#}/{player.MaxMana:0.#}  护盾 {player.ShieldPoints:0.#}" + (player.Alive ? "" : "【已死亡】"));
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
            var hazard = (controller.chapter as TrainingChapterFlow)?.Hazard;
            if (hazard && hazard.WarningActive) text.AppendLine($"【攻击预警】{hazard.WarningRemaining:0.0}秒，准备闪避或护盾");
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
                "\n最近拦截："+s.LastVoiceBlock+"\n待组合技能："+(s.Engine?.PendingSkillId ?? "无")+"\n结印："+(s.lab && s.lab.Recognized ? s.lab.Recognized.name : "未命中")+
                "\n最近施法："+controller.Status;
        }
        public string BuildMovementDetails(double now) => controller && controller.locomotion
            ? (controller.MovementBlockReason ?? "关卡允许移动") + "\n" + controller.locomotion.EncounterDebugDetails +
                "\n" + controller.locomotion.BuildDebugDetails(now) +
                (controller.LastDebugMove == null ? "" : "\n最近一次模拟移动：" + controller.LastDebugMove) : "尚未绑定移动控制器";

        public string BuildThrowDetails(double now) => controller && controller.skills && controller.skills.effects
            ? controller.skills.effects.BuildThrowDebugDetails(now) : "尚未绑定投掷效果执行器";

        public string BuildDesktopDebug(double now) => "【运行状态】\n" + BuildRuntimeDetails() +
            "\n\n【移动输入与选点】\n" + BuildMovementDetails(now) + "\n\n【投掷输入与拟合】\n" + BuildThrowDetails(now);

        public string BuildRuntimeDetails()
        {
            if (!controller) return "线性关卡 · 等待绑定关卡控制器";
            var builder = new StringBuilder();
            builder.AppendLine("线性关卡 · " + (string.IsNullOrEmpty(controller.StageName) ? "等待阶段" : controller.StageName));
            var player = controller.PlayerView;
            var effects = controller.skills ? controller.skills.effects : null;
            if (player == null) builder.AppendLine("玩家数据：尚未就绪");
            else
            {
                builder.AppendLine($"生命 {player.Health:0.#}/{player.MaxHealth:0.#}  能量 {player.Mana:0.#}/{player.MaxMana:0.#}");
                builder.AppendLine($"护盾 {player.ShieldPoints:0.#}" + (player.Alive ? "" : "  【已死亡，请恢复检查点】"));
                var definitions = controller.skills ? controller.skills.ConfiguredSkills : null;
                if (definitions != null)
                {
                    foreach (var definition in definitions)
                    {
                        if (!definition) continue;
                        string group = string.IsNullOrEmpty(definition.cooldownGroup) ? definition.skillId : definition.cooldownGroup;
                        bool unlocked = player.IsUnlocked(definition.skillId);
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
            if (player != null) builder.AppendLine("货币：" + player.Currency);
            var hazard = (controller.chapter as TrainingChapterFlow)?.Hazard;
            if (hazard && hazard.WarningActive) builder.AppendLine($"【红圈攻击预警】{hazard.WarningRemaining:0.0} 秒后结算，请闪避或用护盾承受");
            builder.AppendLine("最近操作：" + controller.Status);
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
