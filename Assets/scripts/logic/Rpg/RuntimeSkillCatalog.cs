using System;
using System.Collections.Generic;
using UnityEngine;

namespace NarutoVR.SealCombat.Rpg
{
    /// <summary>管理一名玩家的技能及效果副本，集中处理共享资产隔离、等级应用和释放。</summary>
    public sealed class RuntimeSkillCatalog : IDisposable
    {
        readonly List<UnityEngine.Object> owned = new();
        readonly Dictionary<SkillDefinition, SkillEffectDefinition> originals = new();
        /// <summary>可以交给该玩家技能控制器使用的独立目录。</summary>
        public SkillCatalog Catalog { get; }

        /// <summary>复制目录、配方及效果，任何运行时改动都不会污染项目中的共享资产。</summary>
        public RuntimeSkillCatalog(SkillCatalog source)
        {
            try
            {
                Catalog = UnityEngine.Object.Instantiate(source); owned.Add(Catalog);
                Catalog.skills = new List<SkillDefinition>();
                foreach (var asset in source.skills)
                {
                    var copy = UnityEngine.Object.Instantiate(asset); owned.Add(copy);
                    if (asset.effectDefinition)
                    {
                        copy.effectDefinition = UnityEngine.Object.Instantiate(asset.effectDefinition); owned.Add(copy.effectDefinition);
                        originals.Add(copy, asset.effectDefinition);
                    }
                    Catalog.skills.Add(copy);
                }
            }
            catch { Dispose(); throw; }
        }
        /// <summary>各效果从自己的原始资产应用自己技能的等级，重复调用不累计倍率。</summary>
        public void ApplyLevels(RpgProgressData progress)
        {
            foreach (var pair in originals)
                pair.Key.effectDefinition.ApplyLevelFrom(pair.Value, SkillProgression.Level(progress, pair.Key.skillId));
        }
        /// <summary>只释放本会话创建的资产，原始目录和效果不受影响。</summary>
        public void Dispose()
        {
            foreach (var asset in owned) if (asset)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(asset);
                else UnityEngine.Object.DestroyImmediate(asset);
            }
            owned.Clear(); originals.Clear();
        }
    }
}
