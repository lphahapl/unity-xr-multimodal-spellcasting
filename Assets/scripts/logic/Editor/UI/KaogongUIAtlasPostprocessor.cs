#if UNITY_EDITOR
using System;
using NarutoVR.SealCombat.Rpg;
using UnityEditor;
using UnityEngine;

namespace NarutoVR.SealCombat.Editor
{
    /// <summary>将 KaogongUIAtlas.png 切片并写入九宫格边界与 Quest ASTC 4x4 压缩配置。按需调用以避免触发全局纹理重导入。</summary>
    public static class KaogongUIAtlasPostprocessor
    {
        public const string AtlasAssetPath = "Assets/Art/UI/Resources/KaogongUI/KaogongUIAtlas.png";

        public static void EnsureAtlasConfigured()
        {
            var importer = AssetImporter.GetAtPath(AtlasAssetPath) as TextureImporter;
            if (!importer) return;
            var spritesProp = new SerializedObject(importer).FindProperty("m_SpriteSheet.m_Sprites");
            if (importer.textureType == TextureImporterType.Sprite
                && importer.spriteImportMode == SpriteImportMode.Multiple
                && spritesProp != null
                && spritesProp.arraySize == KaogongUISprites.Manifest.Length)
                return;

            ReimportAtlas();
        }

        [MenuItem("Tools/NarutoVR/界面/重建考工九宫格 UI 图集")]
        public static void ReimportAtlas()
        {
            var importer = AssetImporter.GetAtPath(AtlasAssetPath) as TextureImporter;
            if (!importer)
            {
                Debug.LogError("[考工UI] 未找到图集文件：" + AtlasAssetPath);
                return;
            }

            ApplySettings(importer);
            importer.SaveAndReimport();
            KaogongUISprites.Invalidate();
            Debug.Log($"[考工UI] 成功完成 {KaogongUISprites.Manifest.Length} 枚九宫格/徽标切片导入。");
        }

        static void ApplySettings(TextureImporter importer)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.sRGBTexture = true;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 2;
            importer.mipMapBias = -0.25f;
            importer.maxTextureSize = 2048;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.spritePixelsPerUnit = 100f;

            var android = importer.GetPlatformTextureSettings("Android");
            android.overridden = true;
            android.maxTextureSize = 2048;
            android.format = TextureImporterFormat.ASTC_4x4;
            android.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SetPlatformTextureSettings(android);

            var entries = KaogongUISprites.Manifest;
            var serialized = new SerializedObject(importer);
            var spritesProp = serialized.FindProperty("m_SpriteSheet.m_Sprites");
            if (spritesProp != null)
            {
                // Preserve existing spriteID / internalID where possible so prefab GUID+fileID links stay stable.
                var existingIds = new System.Collections.Generic.Dictionary<string, (string spriteId, long internalId)>(StringComparer.Ordinal);
                for (int i = 0; i < spritesProp.arraySize; i++)
                {
                    var elem = spritesProp.GetArrayElementAtIndex(i);
                    string oldName = elem.FindPropertyRelative("m_Name")?.stringValue;
                    string oldSid = elem.FindPropertyRelative("m_SpriteID")?.stringValue;
                    long oldIid = elem.FindPropertyRelative("m_InternalID")?.longValue ?? 0;
                    if (!string.IsNullOrEmpty(oldName))
                        existingIds[oldName] = (oldSid, oldIid);
                }

                spritesProp.arraySize = entries.Length;
                for (int i = 0; i < entries.Length; i++)
                {
                    var entry = entries[i];
                    var elem = spritesProp.GetArrayElementAtIndex(i);
                    elem.FindPropertyRelative("m_Name").stringValue = entry.Name;
                    elem.FindPropertyRelative("m_Rect").rectValue = entry.Rect;
                    elem.FindPropertyRelative("m_Alignment").intValue = (int)SpriteAlignment.Center;
                    elem.FindPropertyRelative("m_Pivot").vector2Value = new Vector2(0.5f, 0.5f);
                    elem.FindPropertyRelative("m_Border").vector4Value = entry.Border;

                    var sidProp = elem.FindPropertyRelative("m_SpriteID");
                    var iidProp = elem.FindPropertyRelative("m_InternalID");
                    if (existingIds.TryGetValue(entry.Name, out var prior) && !string.IsNullOrEmpty(prior.spriteId))
                    {
                        sidProp.stringValue = prior.spriteId;
                        if (iidProp != null && prior.internalId != 0) iidProp.longValue = prior.internalId;
                    }
                    else
                    {
                        sidProp.stringValue = GUID.Generate().ToString();
                        if (iidProp != null && iidProp.longValue == 0)
                            iidProp.longValue = DeterministicFileId(entry.Name);
                    }
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        static long DeterministicFileId(string name)
        {
            unchecked
            {
                uint h1 = 2166136261u;
                uint h2 = 3735928559u;
                foreach (char c in name)
                {
                    h1 = (h1 ^ c) * 16777619u;
                    h2 = (h2 * 33u) ^ c;
                }
                long combined = ((long)h1 << 32) | h2;
                return combined & 0x7FFFFFFFFFFFFFFFL;
            }
        }
    }
}
#endif
