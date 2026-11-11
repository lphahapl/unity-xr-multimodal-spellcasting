using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace NarutoVR.SealCombat
{
    /// <summary>正式技能的轻量目录。只序列化稳定 ID、冷却组、地址与匹配参数。</summary>
    [CreateAssetMenu(fileName = "新技能资源清单", menuName = "NarutoVR/技能/技能资源清单")]
    public sealed class SkillResourceManifest : ScriptableObject, IResourceCatalog
    {
        [Serializable]
        public sealed class Entry
        {
            [InspectorName("技能 ID（存档键）"), Tooltip("与技能定义的 skillId 完全一致。已有存档按这个稳定标识恢复技能。")]
            public string id;
            [InspectorName("共享冷却组"), Tooltip("填写实际生效的组名。同组技能共享施法冷却；配方未指定组时，此处填写技能 ID。")]
            public string cooldownGroup;
            [InspectorName("Addressables 地址"), Tooltip("技能定义在本地 Addressables 分组中的地址，可随内容迁移而更新。")]
            public string address;
            public bool Matches(SkillDefinition definition) =>
                definition && definition.skillId == id &&
                (string.IsNullOrEmpty(definition.cooldownGroup) ? definition.skillId : definition.cooldownGroup) == cooldownGroup;
        }

        [Header("技能资源")]
        [InspectorName("技能条目"), Tooltip("每行把一个稳定技能 ID 映射到本版本的技能定义地址。")]
        public Entry[] entries = Array.Empty<Entry>();
        [Header("手势与语音")]
        [InspectorName("匹配参数"), Tooltip("配置输入历史、口令时效与手印消歧范围。")]
        public SkillMatchSettings matching = new();
        [InspectorName("取消施法口令"), Tooltip("语音识别用于取消施法的完整词句。")]
        public string cancelPhrase = SkillPhrase.DefaultCancel;
        [InspectorName("取消口令的拼音音节"), Tooltip("使用模型的拼音 tokens，以空格分隔。")]
        public string cancelTokens = "q ǔ x iāo sh ī f ǎ";

        public string Validate()
        {
            if (entries == null || entries.Length == 0) return "技能资源清单为空";
            if (matching == null) return "缺少匹配规则";
            if (matching.Validate() is string error) return error;
            if (SkillPhrase.Normalize(cancelPhrase).Length == 0 || string.IsNullOrWhiteSpace(cancelTokens))
                return "缺少取消口令或拼音";
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var addresses = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in entries)
            {
                if (entry == null || !Valid(entry.id) || !Valid(entry.cooldownGroup) || !Valid(entry.address))
                    return "技能 ID、冷却组及资源地址必须有效";
                if (!ids.Add(entry.id)) return "重复技能 ID：" + entry.id;
                if (!addresses.Add(entry.address)) return "重复技能资源地址：" + entry.address;
            }
            return null;
        }
        static bool Valid(string s) => !string.IsNullOrWhiteSpace(s) && s == s.Trim();
        public bool TryGet(string id, out Entry entry)
        {
            if (entries != null && id != null)
                foreach (var item in entries)
                    if (item != null && string.Equals(item.id, id, StringComparison.Ordinal))
                    { entry = item; return true; }
            entry = null; return false;
        }
        public bool TryResolve(Type assetType, string id, out string address)
        {
            if (assetType == typeof(SkillDefinition) && TryGet(id, out var entry))
            { address = entry.address; return true; }
            address = null; return false;
        }
        public Task<AssetLease<SkillDefinition>> AcquireAsync(string id, CancellationToken token = default) =>
            SharedAssetLoader.AcquireAsync<SkillDefinition>(this, id, token);
    }
}
