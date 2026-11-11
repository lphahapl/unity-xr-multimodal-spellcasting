using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace NarutoVR.SealCombat
{
    /// <summary>展示技能状态及调试按钮，按钮将明确命令交回控制器处理。</summary>
    [DefaultExecutionOrder(450)]
    public sealed class SkillTestView : MonoBehaviour
    {
        [Serializable] public sealed class Shortcut
        {
            [SerializeField, InspectorName("目标技能 ID"), Tooltip("填写当前技能目录中的稳定 ID，例如 thunder.sword。")]
            string skillId;
            [SerializeField, HideInInspector, FormerlySerializedAs("skill")]
            SkillDefinition legacySkill;
            [InspectorName("模拟口令按键")] public Key key=Key.None;

            public string SkillId
            {
                get => !string.IsNullOrEmpty(skillId) ? skillId : legacySkill ? legacySkill.skillId : null;
                set { skillId = value; legacySkill = null; }
            }
            // 旧场景首次由迁移工具保存时去掉资源硬引用，外部旧场景加载时仍可读取原绑定。
#if UNITY_EDITOR
            public bool MigrateLegacySkillReference()
            {
                if (!legacySkill || string.IsNullOrWhiteSpace(SkillId)) return false;
                skillId = SkillId;
                legacySkill = null;
                return true;
            }
#endif
            internal bool Matches(string skillId) =>
                !string.IsNullOrEmpty(skillId) && string.Equals(SkillId, skillId, StringComparison.Ordinal);
        }
        [Header("一、界面接线")]
        [InspectorName("技能控制器")] public SkillInputController controller;
        [InspectorName("头显文字面板")] public Text statusText;
        [InspectorName("中文字体")] public Font chineseFont;
        [Header("二、显示内容")]
        [InspectorName("界面标题")] public string title="技能训练场 · 输入满足即生效";
        [InspectorName("显示电脑调试按钮")] public bool showDesktopControls=true;
        [InspectorName("最多显示技能数"),Range(1,12)] public int maximumSkillRows=4;
        [InspectorName("最多显示评分数"),Range(1,12)] public int maximumScoreRows=4;
        [Header("三、可配置模拟快捷键")]
        [InspectorName("口令快捷键列表")] public List<Shortcut> shortcuts=new();
        private double nextUI;
        private GUIStyle titleStyle,textStyle,buttonStyle;
        private Vector2 scroll;

        public List<string> ConfigurationErrors(IReadOnlyList<SkillDefinition> definitions)
        {
            var errors=new List<string>();var keys=new HashSet<Key>();
            if(shortcuts==null)return errors;
            foreach(var shortcut in shortcuts)
            {
                if(shortcut==null||shortcut.key==Key.None)continue;
                if(!Enum.IsDefined(typeof(Key),shortcut.key))errors.Add("模拟快捷键无效");
                if(shortcut.key==Key.Escape)errors.Add("Esc 是取消保留键，不能分配给技能");
                if(!keys.Add(shortcut.key))errors.Add($"模拟快捷键 {shortcut.key} 重复，请给不同技能分配不同按键");
                var definition = definitions?.FirstOrDefault(candidate => candidate && shortcut.Matches(candidate.skillId));
                if(!definition||definition.triggerMode==SkillTriggerMode.GestureOnly)
                    errors.Add($"快捷键 {shortcut.key} 需要绑定当前目录中包含咒语的技能");
            }
            return errors;
        }

        public void AssignDefaultShortcuts(IReadOnlyList<SkillDefinition> definitions)
        {
            if(shortcuts.Count!=0||definitions==null)return;
            int index=0;var keys=new[]{Key.F6,Key.F7,Key.F9,Key.F10,Key.F11,Key.F12};
            foreach(var definition in definitions)
                if(definition&&definition.triggerMode!=SkillTriggerMode.GestureOnly&&index<keys.Length)
                    shortcuts.Add(new Shortcut{SkillId=definition.skillId,key=keys[index++]});
        }
        private void Update()
        {
            if(!controller||!controller.isActiveAndEnabled||controller.Engine==null)return;
            if(showDesktopControls&&Keyboard.current!=null)
            {
                if(Keyboard.current.escapeKey.wasPressedThisFrame){controller.Cancel();return;}
                foreach(var shortcut in shortcuts)
                {
                    if(shortcut==null||shortcut.key==Key.None||shortcut.key==Key.Escape||!Enum.IsDefined(typeof(Key),shortcut.key))continue;
                    if(!Keyboard.current[shortcut.key].wasPressedThisFrame||shortcuts.Count(s=>s!=null&&s.key==shortcut.key)>1)continue;
                    var recipe=controller.Recipes.FirstOrDefault(r=>shortcut.Matches(r.Id));
                    if(recipe.Definition&&recipe.Mode!=SkillTriggerMode.GestureOnly)
                        controller.ScheduleVoice(recipe.Phrase);
                }
            }
        }
        private void LateUpdate()
        {
            double now=Time.realtimeSinceStartupAsDouble;
            if(now<nextUI)return;nextUI=now+.1;
            if(statusText&&controller)statusText.text=BuildHUD(now);
        }
        public string BuildHUD(double now)
        {
            if(!controller)return "未绑定技能控制器";
            if(controller.Engine==null)return "技能配置尚未就绪\n"+controller.LastMessage;
            var engine=controller.Engine;var effects=controller.effects;
            var b=new StringBuilder(title+"\n");
            foreach(var recipe in controller.Recipes.Take(maximumSkillRows))
            {
                string input=recipe.Mode==SkillTriggerMode.GestureOnly?$"印：{recipe.Seal}":recipe.Mode==SkillTriggerMode.VoiceOnly?$"咒：{recipe.Phrase}":$"{recipe.Phrase}＋{recipe.Seal}印";
                b.AppendLine($"{recipe.Name} [{input}] → {recipe.Effect}");
            }
            if(controller.Recipes.Count>maximumSkillRows)b.AppendLine($"另有 {controller.Recipes.Count-maximumSkillRows} 个技能，详见目录/电脑操作台");
            b.AppendLine($"骨骼缓存：{engine.BufferCount} 帧 / {engine.BufferSeconds:F1} 秒（上限{engine.HistoryLimitSeconds:0.#}秒）");
            b.AppendLine("匹配："+engine.Status);
            if(!string.IsNullOrEmpty(engine.PendingSkillId))b.AppendLine($"等待：{engine.PendingSkillName} / 剩余 {Math.Max(0,engine.PendingDeadline-now):F1} 秒");
            b.AppendLine("语音："+(controller.voice?controller.voice.Status:"未配置")+"；最近拦截："+controller.LastVoiceBlock);
            if(controller.ScheduledPhrase!=null)b.AppendLine($"【模拟】{controller.ScheduledPhrase}：{Math.Max(0,controller.ScheduledAt-now):F1} 秒后输入");
            if(effects)
            {
                b.AppendLine($"护盾：{effects.ShieldPoints:F0} / {effects.ShieldCapacity:F0} | 剩余 {effects.BuffRemaining:F1} 秒");
                b.AppendLine($"效果：{effects.Status}\n成功激活 {effects.ActivationCount} 次 / 命中 {effects.HitCount} 次");
            }
            if(controller.lab)
            {
                if(controller.lab.Scores.Count==0)b.AppendLine("印评分：等待新的手部追踪数据");
                foreach(var score in controller.lab.Scores.Where(s=>s.profile&&controller.Recipes.Any(r=>r.Seal==s.profile.sealName)).Take(maximumScoreRows))
                    b.AppendLine($"{score.profile.sealName}：{score.score*100:F1}分 / 基础{score.profile.threshold*100:F0} / 覆盖{score.coverage:P0}");
                foreach(var recipe in controller.Recipes.Where(r=>r.Mode==SkillTriggerMode.VoiceAndGesture).Take(maximumSkillRows))
                {
                    var score=controller.lab.Scores.FirstOrDefault(s=>s.profile&&s.profile.sealName==recipe.Seal);
                    if(score!=null)b.AppendLine($"{recipe.Name}组合门槛：{Math.Max(recipe.Floor,score.profile.threshold-recipe.Discount)*100:F0} 分（仍需质量/保持/消歧）");
                }
            }
            b.AppendLine($"取消：“{engine.CancelPhrase}”"+(controller.enableOpenHandCancel?$"，或张开双手分开保持{controller.cancelHold:0.#}秒":""));
            b.AppendLine("提示："+controller.LastMessage);return b.ToString();
        }
        private void OnGUI()
        {
            if(!showDesktopControls||!controller||controller.Engine==null)return;
            if(textStyle==null)
            {
                titleStyle=new GUIStyle(GUI.skin.label){font=chineseFont,fontSize=20};
                textStyle=new GUIStyle(GUI.skin.label){font=chineseFont,fontSize=15,wordWrap=true};
                buttonStyle=new GUIStyle(GUI.skin.button){font=chineseFont,fontSize=16};
            }
            GUILayout.BeginArea(new Rect(10,10,440,Mathf.Min(520,Screen.height-20)),GUI.skin.box);
            scroll=GUILayout.BeginScrollView(scroll);
            GUILayout.Label("技能测试 · 电脑操作台",titleStyle);
            GUILayout.Label("按钮仅注入模拟口令；真实语音状态见下方。",textStyle);
            foreach(var recipe in controller.Recipes.Where(r=>r.Mode!=SkillTriggerMode.GestureOnly))
            {
                var shortcut=shortcuts.FirstOrDefault(s=>s!=null&&s.Matches(recipe.Id)&&s.key!=Key.None);
                string suffix=shortcut==null?"":$"（{shortcut.key}）";
                if(GUILayout.Button($"{Math.Max(1,controller.simulatedDelay):0}秒后模拟“{recipe.Phrase}”{suffix}",buttonStyle))controller.ScheduleVoice(recipe.Phrase);
            }
            if(GUILayout.Button("取消输入 / 收起技能（Esc）",buttonStyle))controller.Cancel();
            GUILayout.Label("语音："+(controller.voice?controller.voice.Status:"未配置"),textStyle);
            foreach(string message in controller.RecentMessages)GUILayout.Label(message,textStyle);
            GUILayout.EndScrollView();GUILayout.EndArea();
        }
    }
}
