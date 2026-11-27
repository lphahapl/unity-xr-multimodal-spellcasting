using System;
using UnityEngine;

namespace NarutoVR.SealCombat
{
    /// <summary>技能配方采用纯结印、纯口令或口令加有序结印三种触发方式。</summary>
    public enum SkillTriggerMode
    {
        [InspectorName("纯结印")] GestureOnly,
        [InspectorName("纯咒语")] VoiceOnly,
        [InspectorName("咒语与结印组合")] VoiceAndGesture
    }

    public enum SkillCastCondition
    {
        [InspectorName("无")] None,
        [InspectorName("手印靠近嘴部")] SealNearMouth
    }
    /// <summary>单个技能的共享配置，描述触发配方、效果资产、资源消耗与冷却；运行时实例另行保存状态。</summary>
    [CreateAssetMenu(menuName = "NarutoVR/技能配置")]
    public sealed class SkillDefinition : ScriptableObject
    {
        [Header("一、技能身份")]
        [InspectorName("技能唯一标识"), Tooltip("稳定且唯一的技能ID；不要用显示名代替。")]
        public string skillId;
        [InspectorName("技能显示名"), Tooltip("用于头显提示和施法日志。")]
        public string displayName;
        [Header("二、输入配方")]
        [InspectorName("触发方式"), Tooltip("纯结印、纯咒语，或口令与结印组合。")]
        public SkillTriggerMode triggerMode;
        [InspectorName("完整中文口令"), Tooltip("精确匹配完整口令；禁止重复及完整口令包含冲突。")]
        public string phrase;
        [InspectorName("口令声学拼音 tokens"), Tooltip("用于导出本机KWS词表；修改口令后一起更新。纯结印不需要填写。")]
        public string phraseTokens;
        [InspectorName("第一印"), Tooltip("填写模板的完整印名。")]
        public string sealName;
        [InspectorName("后续手印（按顺序）"), Tooltip("单印留空。重复同印需要先退出手势再结。")]
        public string[] followingSeals = Array.Empty<string>();
        [InspectorName("蓄势后占用末印"), Tooltip("已通过口令和前置手印后，此次末印用于本术。单独结末印仍可触发原单印术。")]
        public bool reserveFinalSeal;
        [InspectorName("组合窗口（秒）"), Tooltip("口令结束与整个有效保持区间必须落在此窗口内。10秒缓存和2秒延迟预算允许最大8秒。"), Range(0.1f, 8)]
        public float matchWindow = 8;
        [InspectorName("组合口令分数减免"), Tooltip("仅组合模式有对应口令时生效；不会降低覆盖率、必需特征或竞争印分差要求。"), Range(0, 1)]
        public float voiceDiscount = .05f;
        [InspectorName("减免后阈值下限"), Tooltip("有效阈值为模板阈值减免后与本下限的较大值。"), Range(0, 1)]
        public float thresholdFloor = .65f;
        [InspectorName("施法条件")] public SkillCastCondition castCondition;
        [InspectorName("嘴部起手距离（米）"), Min(.01f)] public float mouthStartDistance = .22f;
        [InspectorName("嘴部维持距离（米）"), Min(.01f)] public float mouthStopDistance = .30f;
        [InspectorName("认印后双腕松印距离（米）"), Min(0), Tooltip("正确结印后，双腕间距达到此值即松印。设为 0 时持续检查原手印。")]
        public float sealReleaseWristDistance;
        [Header("三、效果与生命周期")]
        [InspectorName("效果配置资产"), Tooltip("引用护盾或投射物效果SO；新效果可继承SkillEffectDefinition扩展。")]
        public SkillEffectDefinition effectDefinition;
        [InspectorName("瞄准配置资产"), Tooltip("本术的准心、方向线和瞄准参数。")]
        public SkillAimProfile aimProfile;
        [InspectorName("生成到左手"), Tooltip("关闭时使用右手；仅手持效果使用。")]
        public bool leftHand = false;
        [InspectorName("起手冷却（秒）"), Tooltip("普通技能成功释放后开始计时。持续喷射的冷却在吐息效果资产中配置，停喷时开始计时。"), Min(0)]
        public float cooldownSeconds = 3;
        [Header("四、玩家施法资源")]
        [InspectorName("度力消耗"), Tooltip("由RPG玩家运行态扣除；普通技能训练场不接管玩家资源。"), Min(0)]
        public float manaCost;
        [InspectorName("共享冷却组"), Tooltip("留空使用技能ID；同一法术的多个配方可填相同组。")]
        public string cooldownGroup;
        [InspectorName("效果持续时间（秒）"), Tooltip("自身加持等效果的生效时长；扩展武器可按此值定义持续时间。投射物未发射的储存超时由投射物效果单独配置。"), Min(0)]
        public float durationSeconds = 10;

        public string[] GetSealSequence()
        {
            if (triggerMode == SkillTriggerMode.VoiceOnly) return Array.Empty<string>();
            var sequence = new string[1 + (followingSeals?.Length ?? 0)];
            sequence[0] = sealName;
            if (followingSeals != null) followingSeals.CopyTo(sequence, 1);
            return sequence;
        }

        /// <summary>校验单个技能字段及效果配置，返回首个错误；返回 null 表示合法。</summary>
        public string ValidateDefinition()
        {
            if (string.IsNullOrWhiteSpace(skillId)) return "技能标识不能为空";
            if (!Enum.IsDefined(typeof(SkillTriggerMode), triggerMode)) return "技能触发方式无效";
            if (!Enum.IsDefined(typeof(SkillCastCondition), castCondition)) return "施法条件无效";
            if (castCondition == SkillCastCondition.SealNearMouth &&
                (triggerMode == SkillTriggerMode.VoiceOnly || !float.IsFinite(mouthStartDistance) ||
                 !float.IsFinite(mouthStopDistance) || mouthStartDistance <= 0 || mouthStopDistance < mouthStartDistance))
                return "嘴部条件需要手印配方、正的起手距离，以及不小于起手距离的维持距离";
            if (!float.IsFinite(sealReleaseWristDistance) || sealReleaseWristDistance < 0)
                return "双腕松印距离必须为非负有限数值";
            if (!effectDefinition) return "缺少技能效果资产";
            if (effectDefinition.ValidateDefinition() is string effectError) return "效果配置无效：" + effectError;
            if (triggerMode != SkillTriggerMode.GestureOnly && string.IsNullOrWhiteSpace(phrase)) return "缺少完整口令";
            foreach (var seal in GetSealSequence())
                if (string.IsNullOrWhiteSpace(seal) || seal != seal.Trim()) return "印序中有空印名或首尾空白";
            if (!float.IsFinite(matchWindow) || matchWindow <= 0 || matchWindow > 8) return "组合窗口必须在0至8秒之间（缓存需覆盖2秒语音延迟）";
            if (!float.IsFinite(voiceDiscount) || voiceDiscount < 0 || voiceDiscount > 1 ||
                !float.IsFinite(thresholdFloor) || thresholdFloor < 0 || thresholdFloor > 1) return "分数减免或阈值下限无效";
            if (!float.IsFinite(cooldownSeconds) || cooldownSeconds < 0 || !float.IsFinite(durationSeconds) || durationSeconds < 0) return "冷却或持续时间无效";
            if (!float.IsFinite(manaCost) || manaCost < 0 || manaCost > 10000) return "度力消耗无效";
            if (!string.IsNullOrEmpty(cooldownGroup) && (cooldownGroup != cooldownGroup.Trim() || string.IsNullOrWhiteSpace(cooldownGroup))) return "冷却组不能包含首尾空白";
            return null;
        }
        public string EffectSummary => effectDefinition ? effectDefinition.Summary : "缺少效果配置";
    }
}
