using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace NarutoVR.SealCombat.Rpg
{
    /// <summary>管理一名玩家的技能及效果副本，集中处理共享资产隔离、等级应用和释放。</summary>
    public sealed class RuntimeSkillCatalog : IDisposable
    {
        readonly List<UnityEngine.Object> owned = new();
        readonly Dictionary<SkillDefinition, SkillEffectDefinition> originals = new();
        readonly List<AssetLease<SkillDefinition>> leases = new();
        readonly Dictionary<string, SkillDefinition> byId = new(StringComparer.Ordinal);
        /// <summary>可以交给该玩家技能控制器使用的独立目录。</summary>
        public SkillCatalog Catalog { get; private set; }

        RuntimeSkillCatalog() { }

        public bool TryGet(string id, out SkillDefinition skill)
        {
            skill = null;
            return id != null && byId.TryGetValue(id, out skill);
        }

        /// <summary>复制目录、配方及效果，任何运行时改动都不会污染项目中的共享资产。</summary>
        public RuntimeSkillCatalog(SkillCatalog source)
        {
            try
            {
                Catalog = UnityEngine.Object.Instantiate(source); owned.Add(Catalog);
                Catalog.skills = new List<SkillDefinition>();
                foreach (var asset in source.skills) AddPlayerCopy(asset);
            }
            catch { Dispose(); throw; }
        }

        /// <summary>匹配器与语音都需要完整配方集；只在全部引用和玩家副本准备完毕后交付目录。</summary>
        public static async Task<RuntimeSkillCatalog> LoadAsync(SkillResourceManifest manifest, CancellationToken token)
        {
            if (!manifest) throw new ArgumentNullException(nameof(manifest));
            if (manifest.Validate() is string error) throw new InvalidOperationException(error);
            var result = new RuntimeSkillCatalog();
            try
            {
                result.Catalog = ScriptableObject.CreateInstance<SkillCatalog>();
                result.owned.Add(result.Catalog);
                result.Catalog.name = manifest.name + "（玩家技能）";
                result.Catalog.matching = manifest.matching.Snapshot();
                result.Catalog.cancelPhrase = manifest.cancelPhrase;
                result.Catalog.cancelTokens = manifest.cancelTokens;
                foreach (var entry in manifest.entries)
                {
                    token.ThrowIfCancellationRequested();
                    var lease = await manifest.AcquireAsync(entry.id, token);
                    result.leases.Add(lease);
                    token.ThrowIfCancellationRequested();
                    var definition = lease.Asset;
                    if (!entry.Matches(definition))
                        throw new InvalidOperationException($"资源清单与技能 ID 或冷却组不一致：{entry.id}");
                    result.AddPlayerCopy(definition);
                }
                return result;
            }
            catch { result.Dispose(); throw; }
        }

        void AddPlayerCopy(SkillDefinition asset)
        {
            var copy = UnityEngine.Object.Instantiate(asset); owned.Add(copy);
            if (asset.effectDefinition)
            {
                copy.effectDefinition = UnityEngine.Object.Instantiate(asset.effectDefinition); owned.Add(copy.effectDefinition);
                originals.Add(copy, asset.effectDefinition);
            }
            Catalog.skills.Add(copy);
            byId.Add(copy.skillId, copy);
        }
        /// <summary>各效果从自己的原始资产应用自己技能的等级，重复调用不累计倍率。</summary>
        public void ApplyLevels(Func<string,int> level)
        {
            foreach (var pair in originals)
                pair.Key.effectDefinition.ApplyLevelFrom(pair.Value, level(pair.Key.skillId));
        }
        /// <summary>只释放本会话创建的资产，原始目录和效果不受影响。</summary>
        public void Dispose()
        {
            foreach (var asset in owned) if (asset)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(asset);
                else UnityEngine.Object.DestroyImmediate(asset);
            }
            owned.Clear(); originals.Clear(); byId.Clear();
            foreach (var lease in leases) lease.Dispose();
            leases.Clear();
        }
    }
}
