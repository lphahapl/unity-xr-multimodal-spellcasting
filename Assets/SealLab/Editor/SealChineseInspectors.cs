using System.Reflection;
using NarutoVR.SealLab;
using UnityEditor;
using UnityEngine;
using Lab = NarutoVR.SealLab.SealLab;

namespace NarutoVR.Editor
{
    // InspectorName is not consistently applied to field labels by the default inspector.
    // Pass the labels explicitly, retaining Unity's Range/Min/Header property drawers.
    public abstract class SealChineseInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var property = serializedObject.GetIterator();
            bool enterChildren = true;
            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (property.propertyPath == "m_Script")
                {
                    using (new EditorGUI.DisabledScope(true)) EditorGUILayout.PropertyField(property, new GUIContent("脚本"));
                    continue;
                }
                var field = target.GetType().GetField(property.name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var name = field?.GetCustomAttribute<InspectorNameAttribute>()?.displayName ?? property.displayName;
                var tooltip = field?.GetCustomAttribute<TooltipAttribute>()?.tooltip ?? property.tooltip;
                var label = new GUIContent(name, tooltip);
                if (property.name == "profiles" && property.isArray)
                {
                    EditorGUILayout.PropertyField(property, label, false);
                    if (!property.isExpanded) continue;
                    int indent = EditorGUI.indentLevel; EditorGUI.indentLevel++;
                    try
                    {
                        EditorGUILayout.PropertyField(property.FindPropertyRelative("Array.size"), new GUIContent("模板数量"));
                        for (int i = 0; i < property.arraySize; ++i)
                        {
                            var element = property.GetArrayElementAtIndex(i);
                            var profile = element.objectReferenceValue as SealProfile;
                            EditorGUILayout.PropertyField(element, new GUIContent($"第 {i + 1} 个印：" + (profile ? profile.sealName : "未设置")));
                        }
                    }
                    finally { EditorGUI.indentLevel = indent; }
                }
                else EditorGUILayout.PropertyField(property, label, true);
            }
            serializedObject.ApplyModifiedProperties();
        }
    }
    [CustomEditor(typeof(Lab)), CanEditMultipleObjects]
    public sealed class SealLabChineseInspector : SealChineseInspector { }
    [CustomEditor(typeof(SealLabView)), CanEditMultipleObjects]
    public sealed class SealLabViewChineseInspector : SealChineseInspector { }
    [CustomEditor(typeof(SealXRHandsSource)), CanEditMultipleObjects]
    public sealed class SealXRHandsSourceChineseInspector : SealChineseInspector { }
    [CustomEditor(typeof(SealProfile)), CanEditMultipleObjects]
    public sealed class SealProfileChineseInspector : SealChineseInspector { }
    [CustomEditor(typeof(SealRecognitionTest)), CanEditMultipleObjects]
    public sealed class SealRecognitionTestChineseInspector : SealChineseInspector { }
}
