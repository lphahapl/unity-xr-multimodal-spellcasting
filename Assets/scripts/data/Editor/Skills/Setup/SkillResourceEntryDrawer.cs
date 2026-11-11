using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;

namespace NarutoVR.SealCombat.Editor
{
    /// <summary>按实际资产文件名标记清单条目，业务 ID 保留在标题中供核对。</summary>
    [CustomPropertyDrawer(typeof(SkillResourceManifest.Entry))]
    public sealed class SkillResourceEntryDrawer : PropertyDrawer
    {
        static readonly GUIContent IdLabel = new("技能 ID（存档键）", "须与技能定义的 skillId 一致。");
        static readonly GUIContent GroupLabel = new("共享冷却组", "配方未指定冷却组时，填写技能 ID。");
        static readonly GUIContent AddressLabel = new("Addressables 地址", "使用技能定义的 Addressables 地址。");
        static Dictionary<string, string> assetNames;
        static SkillResourceEntryDrawer() => EditorApplication.projectChanged += () => assetNames = null;
        static float Row => EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

        static string Title(string address, string id)
        {
            if (assetNames == null)
            {
                assetNames = new Dictionary<string, string>(StringComparer.Ordinal);
                var settings = AddressableAssetSettingsDefaultObject.Settings;
                if (settings)
                    foreach (var group in settings.groups)
                        if (group)
                            foreach (var entry in group.entries)
                                if (!string.IsNullOrEmpty(entry.address))
                                    assetNames[entry.address] = assetNames.ContainsKey(entry.address)
                                        ? "地址重复" : Path.GetFileNameWithoutExtension(entry.AssetPath);
            }
            if (string.IsNullOrEmpty(id) && string.IsNullOrEmpty(address)) return "新增技能配方";
            string name = !string.IsNullOrEmpty(address) && assetNames.TryGetValue(address, out var found) ? found : "未找到配方";
            return string.IsNullOrEmpty(id) ? name : $"{name}（{id}）";
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
            EditorGUIUtility.singleLineHeight + (property.isExpanded ? 3 * Row : 0);

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            try
            {
                var id = property.FindPropertyRelative("id");
                var address = property.FindPropertyRelative("address");
                position.height = EditorGUIUtility.singleLineHeight;
                string title = Title(address.stringValue, id.stringValue);
                property.isExpanded = EditorGUI.Foldout(position, property.isExpanded, title, true);
                if (!property.isExpanded) return;
                using (new EditorGUI.IndentLevelScope())
                {
                    position.y += Row; EditorGUI.PropertyField(position, id, IdLabel);
                    position.y += Row; EditorGUI.PropertyField(position, property.FindPropertyRelative("cooldownGroup"), GroupLabel);
                    position.y += Row; EditorGUI.PropertyField(position, address, AddressLabel);
                }
            }
            finally { EditorGUI.EndProperty(); }
        }
    }
}
