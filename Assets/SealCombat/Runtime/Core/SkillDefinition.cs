using System;
using UnityEngine;

namespace NarutoVR.SealCombat
{
    public enum SkillTriggerMode
    {
        [InspectorName("纯结印")] GestureOnly,
        [InspectorName("纯咒语")] VoiceOnly,
        [InspectorName("咒语与结印组合")] VoiceAndGesture
    }
    public enum SkillEffectKind
    {
        [InspectorName("自身加持")] SelfBuff,
        [InspectorName("手持火球")] HeldFireball
    }

    [CreateAssetMenu(menuName = "NarutoVR/技能配置")]
    public sealed class SkillDefinition : ScriptableObject
    {
        [Header("一、技能身份")]
        [InspectorName("技能唯一标识"), Tooltip("稳定且唯一的技能ID；不要用显示名代替。")]
        public string skillId;
        [InspectorName("技能显示名"), Tooltip("用于头显提示和施法日志。")]
        public string displayName;
        [Header("二、输入配方（首版支持单印）")]
        [InspectorName("触发方式"), Tooltip("纯结印、纯咒语，或口令与单印组合。")]
        public SkillTriggerMode triggerMode;
        [InspectorName("完整中文口令"), Tooltip("精确匹配完整口令；禁止重复及完整口令包含冲突。")]
        public string phrase;
        [InspectorName("口令声学拼音 tokens"), Tooltip("用于导出本机KWS词表；修改口令后一起更新。纯结印不需要填写。")]
        public string phraseTokens;
        [InspectorName("需要的单印"), Tooltip("使用已标定模板的印名称。首版仅支持一个印；印序列需要后续路径匹配器。")]
        public string sealName;
        [InspectorName("组合窗口（秒）"), Tooltip("口令结束与整个有效保持区间必须落在此窗口内。10秒缓存和2秒延迟预算允许最大8秒。"), Range(0.1f, 8)]
        public float matchWindow = 8;
        [InspectorName("组合口令分数减免"), Tooltip("仅组合模式有对应口令时生效；不会降低覆盖率、必需特征或竞争印分差要求。"), Range(0, 1)]
        public float voiceDiscount = .05f;
        [InspectorName("减免后阈值下限"), Tooltip("有效阈值为模板阈值减免后与本下限的较大值。"), Range(0, 1)]
        public float thresholdFloor = .65f;
        [Header("三、效果与生命周期")]
        [InspectorName("效果配置资产"), Tooltip("引用护盾或投射物效果SO；新效果可继承SkillEffectDefinition扩展。")]
        public SkillEffectDefinition effectDefinition;
        [HideInInspector]
        public SkillEffectKind effectKind;
        [InspectorName("生成到左手"), Tooltip("关闭时使用右手；仅手持效果使用。")]
        public bool leftHand = false;
        [InspectorName("冷却（秒）"), Tooltip("成功激活后开始冷却；冷却内输入作废，结束后不会自动补发。"), Min(0)]
        public float cooldownSeconds = 3;
        [Header("四、RPG资源（旧训练场保持零消耗）")]
        [InspectorName("法力消耗"), Tooltip("由RPG玩家运行态扣除；普通技能训练场不接管玩家资源。"), Min(0)]
        public float manaCost;
        [InspectorName("共享冷却组"), Tooltip("留空使用技能ID；同一法术的多个配方可填相同组。")]
        public string cooldownGroup;
        [InspectorName("效果或持有时长（秒）"), Tooltip("自身加持持续时间，或手持物未发射的最长持有时间。"), Min(0)]
        public float durationSeconds = 10;

        // A future sequence recipe can replace this single-seal input without changing effect execution.
        public const int SupportedSealCount = 1;

        public string ValidateDefinition()
        {
            if (string.IsNullOrWhiteSpace(skillId)) return "技能标识不能为空";
            if (!Enum.IsDefined(typeof(SkillTriggerMode), triggerMode) || (!effectDefinition && !Enum.IsDefined(typeof(SkillEffectKind), effectKind))) return "技能模式或效果类型无效";
            if (effectDefinition && effectDefinition.ValidateDefinition() is string effectError) return "效果配置无效：" + effectError;
            if (triggerMode != SkillTriggerMode.GestureOnly && string.IsNullOrWhiteSpace(phrase)) return "缺少完整口令";
            if (triggerMode != SkillTriggerMode.VoiceOnly && string.IsNullOrWhiteSpace(sealName)) return "缺少单印名称";
            if (!float.IsFinite(matchWindow) || matchWindow <= 0 || matchWindow > 8) return "组合窗口必须在0至8秒之间（缓存需覆盖2秒语音延迟）";
            if (!float.IsFinite(voiceDiscount) || voiceDiscount < 0 || voiceDiscount > 1 ||
                !float.IsFinite(thresholdFloor) || thresholdFloor < 0 || thresholdFloor > 1) return "分数减免或阈值下限无效";
            if (!float.IsFinite(cooldownSeconds) || cooldownSeconds < 0 || !float.IsFinite(durationSeconds) || durationSeconds < 0) return "冷却或持续时间无效";
            if (!float.IsFinite(manaCost) || manaCost < 0 || manaCost > 10000) return "法力消耗无效";
            if (!string.IsNullOrEmpty(cooldownGroup) && (cooldownGroup != cooldownGroup.Trim() || string.IsNullOrWhiteSpace(cooldownGroup))) return "冷却组不能包含首尾空白";
            return null;
        }
        public string EffectSummary => effectDefinition ? effectDefinition.Summary : effectKind==SkillEffectKind.HeldFireball?"手持火球（旧配置）":"自身护盾（旧配置）";
    }
}
