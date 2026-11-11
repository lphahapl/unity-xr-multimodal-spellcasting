using System;
using System.Collections.Generic;
using UnityEngine;

namespace NarutoVR.SealCombat.Enemies
{
    [CreateAssetMenu(menuName = "NarutoVR/敌人/敌人目录")]
    public sealed class EnemyCatalog : ScriptableObject, IResourceCatalog
    {
        [Header("敌人目录")]
        [InspectorName("敌人定义列表")] public EnemyDefinition[] enemies = Array.Empty<EnemyDefinition>();
        Dictionary<string, EnemyDefinition> index;

        /// <summary>验证全部定义并重建索引；运行中主动改变目录后需再次调用。</summary>
        public bool Validate(out string reason)
        {
            index = null;
            if (enemies == null) { reason = "敌人定义列表为空引用"; return false; }
            var next = new Dictionary<string, EnemyDefinition>(StringComparer.Ordinal);
            foreach (var definition in enemies)
            {
                if (!definition) { reason = "敌人目录包含空定义"; return false; }
                if (!definition.Validate(out reason)) return false;
                if (next.ContainsKey(definition.enemyId)) { reason = "重复敌人编号：" + definition.enemyId; return false; }
                next.Add(definition.enemyId, definition);
            }
            index = next; reason = null; return true;
        }
        public bool TryGet(string id, out EnemyDefinition definition)
        {
            definition = null;
            if (id == null || index == null && !Validate(out _)) return false;
            return index.TryGetValue(id, out definition) && definition;
        }
        void OnValidate() => index = null;

        public bool TryResolve(Type assetType, string id, out string address)
        {
            address = null;
            if (assetType != typeof(GameObject) || !TryGet(id, out var enemy)) return false;
            address = enemy.prefab.RuntimeKey as string;
            return !string.IsNullOrEmpty(address);
        }
    }
}
