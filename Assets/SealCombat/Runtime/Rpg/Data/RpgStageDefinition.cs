using UnityEngine;

namespace NarutoVR.SealCombat.Rpg
{
    [CreateAssetMenu(menuName = "NarutoVR/线性RPG/试炼阶段")]
    public sealed class RpgStageDefinition : ScriptableObject
    {
        [Header("一、阶段身份与目标")]
        [InspectorName("稳定阶段标识")] public string stageId;
        [InspectorName("阶段名称")] public string displayName;
        [InspectorName("阶段说明"), TextArea(2, 5)] public string instructions;
        [InspectorName("每个训练靶生命"), Min(1)] public float targetHealth = 60;
        [InspectorName("开启预警范围攻击")] public bool enableHazards;
        [InspectorName("阶段开始进入战斗移动")] public bool startsInCombat = true;
        [Header("二、首次通关奖励")]
        [InspectorName("奖励货币"), Min(0)] public int rewardCurrency = 40;
        [InspectorName("解锁技能标识")] public string[] unlockSkillIds = System.Array.Empty<string>();
    }
}
