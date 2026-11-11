using System;
using System.Linq;
using NarutoVR.SealCombat.Rpg;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace NarutoVR.SealCombat.Editor
{
    /// <summary>用场景选择的目录校验配方和导出词表。临时视图仅存在于编辑器内存中。</summary>
    internal sealed class AuthoredSkillCatalog : IDisposable
    {
        public SkillCatalog Catalog { get; }
        readonly bool temporary;

        AuthoredSkillCatalog(SkillCatalog catalog, bool temporary = false)
        {
            Catalog = catalog;
            this.temporary = temporary;
        }

        internal static SkillResourceManifest ManifestFor(SkillInputController controller)
        {
            var scene = controller.gameObject.scene;
            if (!scene.IsValid() || !scene.isLoaded) return null;
            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<LinearRpgController>(true))
                .Where(owner => owner.skills == controller && owner.skillResources)
                .Select(owner => owner.skillResources).SingleOrDefault();
        }

        public static AuthoredSkillCatalog Open(SkillInputController controller)
        {
            var manifest = ManifestFor(controller);
            if (manifest) return Open(manifest);
            if (controller.catalog) return new AuthoredSkillCatalog(controller.catalog);
            throw new InvalidOperationException("场景没有绑定技能目录或技能资源清单");
        }

        public static AuthoredSkillCatalog Open(string assetPath)
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
            if (asset is SkillResourceManifest manifest) return Open(manifest);
            if (asset is SkillCatalog catalog) return new AuthoredSkillCatalog(catalog);
            throw new InvalidOperationException("路径不是技能资源清单或技能目录：" + assetPath);
        }

        static AuthoredSkillCatalog Open(SkillResourceManifest manifest)
        {
            if (manifest.Validate() is string error) throw new InvalidOperationException(error);
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (!settings) throw new InvalidOperationException("缺少 Addressables 配置");
            var byAddress = settings.groups.Where(group => group)
                .SelectMany(group => group.entries).ToLookup(entry => entry.address, StringComparer.Ordinal);
            var catalog = ScriptableObject.CreateInstance<SkillCatalog>();
            catalog.name = manifest.name;
            catalog.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                catalog.matching = manifest.matching.Snapshot();
                catalog.cancelPhrase = manifest.cancelPhrase;
                catalog.cancelTokens = manifest.cancelTokens;
                foreach (var row in manifest.entries)
                {
                    var matches = byAddress[row.address].ToArray();
                    if (matches.Length != 1)
                        throw new InvalidOperationException($"技能 {row.id} 地址 {row.address} 应唯一，找到 {matches.Length} 条");
                    var entry = matches[0];
                    var schema = entry.parentGroup.GetSchema<BundledAssetGroupSchema>();
                    if (!schema || !schema.IncludeInBuild || !schema.IncludeAddressInCatalog)
                        throw new InvalidOperationException("技能地址未加入可按地址加载的内容包：" + row.address);
                    var definition = AssetDatabase.LoadAssetAtPath<SkillDefinition>(entry.AssetPath);
                    if (!row.Matches(definition))
                        throw new InvalidOperationException("技能清单与配方的 ID 或冷却组不一致：" + row.id);
                    catalog.skills.Add(definition);
                }
                return new AuthoredSkillCatalog(catalog, true);
            }
            catch { UnityEngine.Object.DestroyImmediate(catalog); throw; }
        }

        public void Dispose()
        {
            if (temporary && Catalog) UnityEngine.Object.DestroyImmediate(Catalog);
        }
    }
}
