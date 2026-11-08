using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace NarutoVR.SealCombat.Rpg
{
    /// <summary>复用首章的世界空间 Canvas；文本分页只在换台词时计算。</summary>
    public sealed class OpeningSubtitlePresenter
    {
        readonly Text label;
        readonly Transform head;
        readonly Transform panel;
        readonly List<string> pages = new List<string>();
        readonly List<int> pageEnds = new List<int>();
        AudioSource voice;
        AudioClip clip;
        int visiblePage = -1;

        public OpeningSubtitlePresenter(Text label, Transform head)
        {
            this.label = label;
            this.head = head;
            var canvas = label.canvas;
            if (canvas && canvas.renderMode == RenderMode.WorldSpace)
            {
                panel = canvas.transform;
                panel.SetParent(null, true);
            }
            label.rectTransform.sizeDelta = new Vector2(640f, 60f);
            label.alignment = TextAnchor.MiddleCenter;
            label.fontSize = 28;
            label.resizeTextForBestFit = false;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.supportRichText = false;
            label.raycastTarget = false;
            label.text = string.Empty;
            Place();
        }

        public void Place()
        {
            if (head && panel)
                panel.SetPositionAndRotation(head.TransformPoint(new Vector3(0f, -.23f, 1.1f)), head.rotation);
        }

        public void Clear()
        {
            voice = null;
            clip = null;
            pages.Clear();
            pageEnds.Clear();
            Display(-1);
        }

        public void Begin(AudioSource source, string speaker, string text)
        {
            Clear();
            if (!source || !source.clip || string.IsNullOrWhiteSpace(text)) return;
            voice = source;
            clip = source.clip;
            string name = (speaker ?? string.Empty).Replace('\n', ' ').Replace('\r', ' ').Trim();
            int qualifier = name.IndexOfAny(new[] { '（', '(' });
            if (qualifier > 0) name = name.Substring(0, qualifier).TrimEnd();
            string prefix = (name.Length > 0 ? name : "旁白") + "：";
            string body = text.Replace('\r', ' ').Replace('\n', ' ').Trim();
            int[] offsets = StringInfo.ParseCombiningCharacters(body);
            // 用实际字体预量整行；Unicode 字素可避免截断扩展汉字或组合符号。
            var settings = label.GetGenerationSettings(label.rectTransform.rect.size);
            float width = label.rectTransform.rect.width * label.pixelsPerUnit;
            int charBudget = Mathf.Max(1, 22 - new StringInfo(prefix).LengthInTextElements);
            int start = 0;
            while (start < offsets.Length)
            {
                int end = start;
                int pause = -1;
                while (end < offsets.Length && end - start < charBudget)
                {
                    int limit = end + 1 < offsets.Length ? offsets[end + 1] : body.Length;
                    string candidate = prefix + body.Substring(offsets[start], limit - offsets[start]);
                    if (end > start && label.cachedTextGeneratorForLayout.GetPreferredWidth(candidate, settings) > width - 2f)
                        break;
                    if ("，。；：！？、,.!?:;…".IndexOf(body[offsets[end]]) >= 0 || char.IsWhiteSpace(body[offsets[end]]))
                        pause = end + 1;
                    end++;
                }
                if (end < offsets.Length && pause > start) end = pause;
                int stop = end < offsets.Length ? offsets[end] : body.Length;
                string part = body.Substring(offsets[start], stop - offsets[start]).Trim();
                if (part.Length > 0)
                {
                    pages.Add(prefix + part);
                    pageEnds.Add(end);
                }
                start = end;
            }
        }

        public void Tick(bool paused)
        {
            if (paused || !voice || !clip || voice.clip != clip || !voice.isPlaying ||
                (AudioListener.pause && !voice.ignoreListenerPause) || pages.Count == 0)
            {
                Display(-1);
                return;
            }
            // WAV 只含整句，没有逐词时间码。按页中字素数分配录音样本。
            long position = (long)voice.timeSamples * pageEnds[pageEnds.Count - 1];
            int page = 0;
            while (page < pageEnds.Count - 1 && position >= (long)pageEnds[page] * clip.samples)
                page++;
            Display(page);
        }

        void Display(int page)
        {
            if (visiblePage == page) return;
            visiblePage = page;
            if (label) label.text = page < 0 ? string.Empty : pages[page];
        }
    }
}
