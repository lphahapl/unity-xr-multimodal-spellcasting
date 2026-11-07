using NarutoVR.SealLab;
using UnityEditor;
using UnityEngine;

namespace NarutoVR.Editor
{
    [CustomPropertyDrawer(typeof(FeatureRule))]
    public sealed class SealFeatureRuleDrawer : PropertyDrawer
    {
        private static float Row => EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) => Row * (property.isExpanded ? 9 : 1);
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            try
            {
                var id = property.FindPropertyRelative("featureId");
                var row = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
                property.isExpanded = EditorGUI.Foldout(row, property.isExpanded,
                    new GUIContent(SealChineseText.FeatureName(id.stringValue), "固定标识：" + id.stringValue), true);
                if (!property.isExpanded) return;
                int previousIndent = EditorGUI.indentLevel;
                EditorGUI.indentLevel++;
                try
                {
                    Header("一、特征与目标范围");
                    Field("featureId", "固定特征标识", "用于数据匹配；请保留英文标识。上方显示对应中文名称。");
                    Field("min", "目标最小值", "目标区间下限；区间内匹配度为满分。");
                    Field("max", "目标最大值", "目标区间上限，不能小于最小值。");
                    Header("二、容差与手动权重");
                    Field("tolerance", "区间外软容差", "超出目标区间后，在该容差内线性扣分。");
                    Field("weight", "评分权重（零为禁用）", "权重越高，该特征对总分影响越大。");
                    Field("required", "必需特征", "勾选后，该特征缺失会直接拒绝识别。");
                }
                finally { EditorGUI.indentLevel = previousIndent; }
                void Header(string text) { row.y += Row; EditorGUI.LabelField(row, text, EditorStyles.boldLabel); }
                void Field(string name, string text, string tooltip)
                { row.y += Row; EditorGUI.PropertyField(row, property.FindPropertyRelative(name), new GUIContent(text, tooltip)); }
            }
            finally { EditorGUI.EndProperty(); }
        }
    }
}
