using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace NarutoVR.SealCombat
{
    /// <summary>从正在使用的技能目录生成原生KWS词表，不依赖开发机导出的JSON。</summary>
    public static class NativeVoiceConfiguration
    {
        public static string BuildKeywordText(SkillCatalog catalog, string tokenPath)
        {
            if (!catalog) throw new InvalidOperationException("原生语音缺少技能目录");
            var errors = catalog.Validate();
            if (errors.Count != 0) throw new InvalidOperationException(string.Join("；", errors));
            var vocabulary = new HashSet<string>(File.ReadLines(tokenPath).Where(l => !string.IsNullOrWhiteSpace(l))
                .Select(l => l.Split((char[])null, StringSplitOptions.RemoveEmptyEntries)[0]), StringComparer.Ordinal);
            var words = new SortedDictionary<string, string>(StringComparer.Ordinal);
            void Add(string rawPhrase, string rawTokens)
            {
                string phrase = SkillPhrase.Normalize(rawPhrase);
                if (phrase.Length == 0 || phrase == "all" || phrase.Any(char.IsControl) || phrase.IndexOfAny(new[] { '@', ':', '#' }) >= 0)
                    throw new InvalidOperationException("原生语音口令无效：" + phrase);
                string[] tokens = (rawTokens ?? "").Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length == 0 || tokens.Any(t => !vocabulary.Contains(t)))
                    throw new InvalidOperationException("口令缺少有效声学拼音：" + phrase);
                string value = string.Join(" ", tokens);
                if (words.TryGetValue(phrase, out string previous) && previous != value)
                    throw new InvalidOperationException("同一口令包含不同声学拼音：" + phrase);
                words[phrase] = value;
            }
            foreach (var skill in catalog.skills)
                if (skill && skill.triggerMode != SkillTriggerMode.GestureOnly) Add(skill.phrase, skill.phraseTokens);
            Add(catalog.cancelPhrase, catalog.cancelTokens);
            return string.Concat(words.Select(pair => pair.Value + " @" + pair.Key + "\n"));
        }
    }
}
