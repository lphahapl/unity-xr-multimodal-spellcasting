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

        double nextRefresh;
        string cachedHud;
        Vector2 scroll;
        GUIStyle titleStyle, textStyle, buttonStyle;

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
            cachedHud = BuildHud();
            if (statusText)
            {
                if (chineseFont && statusText.font != chineseFont) statusText.font = chineseFont;
                statusText.text = cachedHud;
            }
        }

        public string BuildHud()
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
            if (locomotion)
            {
                string candidate = locomotion.CandidateNode != null ? locomotion.CandidateNode.nodeId : "无";
                builder.AppendLine($"导航：{(locomotion.NavigationActive ? "开启" : "关闭")}  候选：{candidate}");
                builder.AppendLine($"驻留：{Mathf.Clamp01(locomotion.DwellProgress) * 100:0}%  闪避冷却：{locomotion.DodgeRemaining:0.0}秒");
                builder.AppendLine(locomotion.OnNode ? "位置：预设点内（" + locomotion.CurrentNodeId + "）" : "位置：预设点外");
                builder.AppendLine("移动：" + locomotion.Status);
            }
            else builder.AppendLine("导航：尚未绑定移动控制器");
            builder.AppendLine("当前目标：" + controller.ObjectiveStatus);
            if (controller.Progress != null) builder.AppendLine("货币：" + controller.Progress.currency);
            var hazard = controller.GetComponent<RpgTrainingHazard>();
            if (hazard && hazard.WarningActive) builder.AppendLine($"【红圈攻击预警】{hazard.WarningRemaining:0.0} 秒后结算，请闪避或用护盾承受");
            builder.AppendLine("最近操作：" + controller.Status);
            builder.Append("存档：" + ShortSavePath(controller.SavePath));
            return builder.ToString();
        }

        void OnGUI()
        {
            if (!showDesktopControls || Screen.width < 160 || Screen.height < 100) return;
            EnsureStyles();
            float width = Mathf.Min(390, Screen.width - 20);
            GUILayout.BeginArea(new Rect(Screen.width - width - 10, 10, width, Screen.height - 20), GUI.skin.box);
            scroll = GUILayout.BeginScrollView(scroll);
            GUILayout.Label("电脑调试（模拟输入）", titleStyle);
            GUILayout.Label("模拟施法绕过结印与口令匹配，仍检查能量、冷却和解锁资格。火球需要有效的真实手部追踪与生成手。", textStyle);
            if (!controller)
            {
                GUILayout.Label("尚未绑定关卡控制器。", textStyle);
                GUILayout.EndScrollView(); GUILayout.EndArea(); return;
            }
            GUILayout.Label(cachedHud ?? BuildHud(), textStyle);
            GUILayout.Space(8);
            DrawSkillButtons();
            if (GUILayout.Button("取消待施法输入", buttonStyle)) controller.CancelCasting();
            GUILayout.Space(8);
            GUILayout.Label("四向模拟步进", textStyle);
            DrawMovementRow(false, Vector2.up, "向前", Vector2.down, "向后");
            DrawMovementRow(false, Vector2.left, "向左", Vector2.right, "向右");
            GUILayout.Label("四向模拟闪避", textStyle);
            DrawMovementRow(true, Vector2.up, "向前闪避", Vector2.down, "向后闪避");
            DrawMovementRow(true, Vector2.left, "向左闪避", Vector2.right, "向右闪避");
            GUILayout.Space(8);
            if (GUILayout.Button("模拟受到25点伤害", buttonStyle)) controller.DebugDamage(25);
            if (GUILayout.Button("恢复检查点", buttonStyle)) controller.RestoreCheckpoint();
            if (GUILayout.Button("保存当前检查点", buttonStyle)) controller.SaveCheckpoint();
            if (GUILayout.Button("尝试进入下一阶段", buttonStyle)) controller.TryAdvanceStage();
            if (GUILayout.Button("训练攻击：" + (controller.HazardsEnabled ? "开启（点击关闭）" : "关闭（点击开启）"), buttonStyle)) controller.ToggleHazards();
            if (GUILayout.Button($"购买隐藏法术（{controller.hiddenSkillPrice}货币）", buttonStyle)) controller.PurchaseHiddenSkill();
            if (GUILayout.Button($"升级火球（{controller.fireballUpgradePrice}货币）", buttonStyle)) controller.UpgradeFireball();
            GUILayout.Space(8);
            GUILayout.Label("作弊测试：下方按钮直接击破当前阶段的训练靶，用于检查关卡推进。", textStyle);
            if (GUILayout.Button("【作弊测试】击破当前阶段全部训练靶", buttonStyle)) controller.DebugDefeatTargets();
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        void DrawSkillButtons()
        {
            var definitions = controller.skills ? controller.skills.ConfiguredSkills : null;
            if (definitions == null || definitions.Count == 0)
            { GUILayout.Label("尚未配置可模拟的法术。", textStyle); return; }
            foreach (var definition in definitions)
            {
                if (!definition) continue;
                bool unlocked = controller.Progress != null && controller.Progress.unlockedSkillIds != null &&
                    controller.Progress.unlockedSkillIds.Contains(definition.skillId);
                if (GUILayout.Button("模拟施放：" + SkillName(definition) + (unlocked ? "（已解锁）" : "（未解锁）"), buttonStyle)) controller.DebugCast(definition.skillId);
            }
        }

        void DrawMovementRow(bool dodge, Vector2 firstDirection, string firstName, Vector2 secondDirection, string secondName)
        {
            bool previousEnabled = GUI.enabled;
            GUI.enabled = previousEnabled && controller.locomotion;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(firstName, buttonStyle)) DebugMove(dodge, firstDirection);
            if (GUILayout.Button(secondName, buttonStyle)) DebugMove(dodge, secondDirection);
            GUILayout.EndHorizontal();
            GUI.enabled = previousEnabled;
        }

        void DebugMove(bool dodge, Vector2 direction)
        {
            if (!controller) return;
            controller.DebugMove(dodge, direction);
        }

        void EnsureStyles()
        {
            if (textStyle != null && textStyle.font == chineseFont) return;
            titleStyle = new GUIStyle(GUI.skin.label) { font = chineseFont, fontSize = 18, wordWrap = true };
            textStyle = new GUIStyle(GUI.skin.label) { font = chineseFont, fontSize = 14, wordWrap = true };
            buttonStyle = new GUIStyle(GUI.skin.button) { font = chineseFont, fontSize = 14, wordWrap = true, padding = new RectOffset(8, 8, 7, 7) };
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
