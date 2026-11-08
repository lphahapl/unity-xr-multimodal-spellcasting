using UnityEngine;
using UnityEngine.AddressableAssets;

namespace NarutoVR.SealCombat.Enemies
{
    [CreateAssetMenu(menuName = "NarutoVR/敌人/异步敌人定义")]
    public sealed class EnemyDefinition : ScriptableObject
    {
        [Header("敌人身份")]
        [InspectorName("稳定敌人编号")] public string enemyId;
        [InspectorName("显示名称")] public string displayName;
        [Header("加载与战斗配置")]
        [InspectorName("Addressables 敌人预置体")] public AssetReferenceGameObject prefab;
        [InspectorName("战斗数值覆盖（空则保留预置体配置）")] public EnemyCombatDefinition combatDefinition;
        [InspectorName("最多缓存的闲置实例"), Min(0)] public int maxInactive = 4;

        public bool Validate(out string reason)
        {
            if (string.IsNullOrWhiteSpace(enemyId) || enemyId != enemyId.Trim()) reason = "敌人编号不能为空或含首尾空格";
            else if (prefab == null || !prefab.RuntimeKeyIsValid()) reason = "敌人预置体 Addressables 引用无效：" + enemyId;
            else if (maxInactive < 0) reason = "闲置实例上限不能为负：" + enemyId;
            else if (combatDefinition && !combatDefinition.Valid) reason = "敌人战斗数值无效：" + enemyId;
            else { reason = null; return true; }
            return false;
        }
    }
}
