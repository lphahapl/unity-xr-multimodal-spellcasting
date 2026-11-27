using System;
using System.Collections.Generic;
using UnityEngine;

namespace NarutoVR.SealCombat.Rpg
{
    /// <summary>《考工律度》统一天工金石 UI 色板，严格避让纯白眩光与纯黑拖影。</summary>
    public static class KaogongTheme
    {
        public static readonly Color LacquerDark = new(0.080f, 0.105f, 0.100f, 0.96f);
        public static readonly Color SlatePanel = new(0.114f, 0.161f, 0.153f, 0.96f);
        public static readonly Color JadeWarm = new(0.890f, 0.867f, 0.816f, 1f);
        public static readonly Color SilkMuted = new(0.686f, 0.651f, 0.573f, 1f);
        public static readonly Color BronzeAged = new(0.722f, 0.573f, 0.298f, 1f);
        public static readonly Color GoldInlay = new(0.898f, 0.737f, 0.388f, 1f);
        public static readonly Color Cinnabar = new(0.851f, 0.306f, 0.133f, 1f);
        public static readonly Color JadeAccent = new(0.271f, 0.722f, 0.592f, 1f);
        public static readonly Color InkOnJade = new(0.145f, 0.118f, 0.086f, 1f);
    }

    /// <summary>九宫格 UI 图集精灵索引与加载器。优先读取图集子资源，未重导入前提供单图内存切片回退。</summary>
    public static class KaogongUISprites
    {
        public const string AtlasResourcePath = "KaogongUI/KaogongUIAtlas";

        public readonly struct Entry
        {
            public readonly string Name;
            public readonly Rect Rect;
            public readonly Vector4 Border;

            public Entry(string name, float x, float y, float w, float h, float bl, float bb, float br, float bt)
            {
                Name = name;
                Rect = new Rect(x, y, w, h);
                Border = new Vector4(bl, bb, br, bt);
            }
        }

        public static readonly Entry[] Manifest =
        {
            new("Ring_Astrolabe",       12,   592, 420, 420, 0,   0,   0,   0),
            new("Frame_Bronze_Main",    444,  676, 336, 336, 102, 102, 102, 102),
            new("Frame_Jade_Plaque",    792,  676, 336, 336, 102, 102, 102, 102),
            new("Icon_Seals",           1140, 732, 280, 280, 0,   0,   0,   0),
            new("Icon_GlyphBrush",      1432, 732, 280, 280, 0,   0,   0,   0),
            new("Icon_VoiceBell",       1724, 732, 280, 280, 0,   0,   0,   0),
            new("Icon_StatusWheel",     1140, 440, 280, 280, 0,   0,   0,   0),
            new("Reticle_Fangyuan",     1432, 440, 280, 280, 0,   0,   0,   0),
            new("Gauge_Track",          12,   503, 988, 77,  18,  18,  18,  18),
            new("Button_Normal",        12,   335, 379, 156, 42,  34,  42,  34),
            new("Button_Hover",         404,  335, 379, 156, 42,  34,  42,  34),
            new("Gauge_Fill",           796,  373, 988, 55,  18,  12,  18,  12),
            new("Divider_Gold",         796,  313, 983, 48,  0,   0,   0,   0),
            new("Gauge_Fill_Inner",     12,   258, 952, 43,  0,   0,   0,   0),
            new("Gauge_Vertical_Track", 1796, 212, 67,  508, 16,  18,  16,  18),
            new("Subtitle_Smoke_Scrim", 976,  237, 512, 64,  180, 20,  180, 20),
        };

        static readonly Dictionary<string, Sprite> Cache = new(StringComparer.Ordinal);
        static readonly List<Sprite> OwnedFallbacks = new();
        static bool resolved;

        public static Sprite Get(string spriteName)
        {
            if (string.IsNullOrEmpty(spriteName)) return null;
            EnsureLoaded();
            if (Cache.TryGetValue(spriteName, out var sprite) && sprite)
                return sprite;
            // Domain reload / scene unload can clear temporary Sprite.Create objects; retry cleanly once.
            if (sprite != null && !sprite)
            {
                Cache.Clear();
                resolved = false;
                EnsureLoaded();
                Cache.TryGetValue(spriteName, out sprite);
            }
            return sprite;
        }

        public static void Invalidate()
        {
            Cache.Clear();
            for (int i = 0; i < OwnedFallbacks.Count; i++)
            {
                var sp = OwnedFallbacks[i];
                if (!sp) continue;
                if (Application.isPlaying) UnityEngine.Object.Destroy(sp);
                else UnityEngine.Object.DestroyImmediate(sp);
            }
            OwnedFallbacks.Clear();
            resolved = false;
        }

        static void EnsureLoaded()
        {
            if (resolved && Cache.Count > 0) return;
            resolved = true;

            Sprite[] subAssets = Resources.LoadAll<Sprite>(AtlasResourcePath);
            if (subAssets != null && subAssets.Length > 1)
            {
                for (int i = 0; i < subAssets.Length; i++)
                {
                    var s = subAssets[i];
                    if (s) Cache[s.name] = s;
                }
                if (Cache.Count >= Manifest.Length) return;
            }

            Texture2D tex = Resources.Load<Texture2D>(AtlasResourcePath);
            if (!tex) return;

            for (int i = 0; i < Manifest.Length; i++)
            {
                var entry = Manifest[i];
                if (Cache.ContainsKey(entry.Name) && Cache[entry.Name]) continue;
                if (entry.Rect.xMax > tex.width || entry.Rect.yMax > tex.height) continue;
                var sp = Sprite.Create(
                    tex,
                    entry.Rect,
                    new Vector2(0.5f, 0.5f),
                    100f,
                    0,
                    SpriteMeshType.FullRect,
                    entry.Border);
                sp.name = entry.Name;
                sp.hideFlags = HideFlags.HideAndDontSave;
                Cache[entry.Name] = sp;
                OwnedFallbacks.Add(sp);
            }
        }
    }
}
