using System;
using UnityEditor;
using UnityEngine;

namespace NarutoVR.SealCombat.Editor
{
    [CustomEditor(typeof(NarrationController))]
    public sealed class NarrationControllerEditor : UnityEditor.Editor
    {
        double nextRepaint;
        void OnEnable() => EditorApplication.update += RefreshStatus;
        void OnDisable() => EditorApplication.update -= RefreshStatus;
        void RefreshStatus()
        {
            if (!Application.isPlaying || EditorApplication.timeSinceStartup < nextRepaint) return;
            nextRepaint = EditorApplication.timeSinceStartup + .2; Repaint();
        }
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.HelpBox("音频和说话者使用 audioFileName、speaker 列。可选 text 填写台词正文，delayAfter 填写间隔秒数。有该列时按单元格更新，空正文清空，空间隔按 0。缺列保留原值。按同名音频和说话者保留已绑事件及推进方式。", MessageType.Info);
            var controller = (NarrationController)target;
            using (new EditorGUI.DisabledScope(Application.isPlaying))
            {
                if (GUILayout.Button("从 CSV / Excel 导入旁白"))
                {
                    string path = EditorUtility.OpenFilePanelWithFilters("选择旁白表（Excel 读取第一张工作表）", "",
                        new[] { "旁白表格", "csv,xlsx" });
                    if (path.Length == 0) return;
                    try
                    {
                        // Read 在全表校验成功后更新复用的句子，先登记 Undo。
                        Undo.RecordObject(controller, "导入旁白表");
                        var imported = NarrationTable.Read(path, controller.lines);
                        controller.lines = imported;
                        PrefabUtility.RecordPrefabInstancePropertyModifications(controller);
                        EditorUtility.SetDirty(controller);
                    }
                    catch (Exception e) { EditorUtility.DisplayDialog("旁白导入失败", e.Message, "确定"); }
                }
            }
            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                if (GUILayout.Button("开始旁白")) controller.StartDialogue();
                if (GUILayout.Button("重新开始")) controller.RestartDialogue();
                if (GUILayout.Button("停止旁白")) controller.StopDialogue();
                if (GUILayout.Button("调试：完成当前句操作")) controller.CompleteCurrentOperation();
                EditorGUILayout.HelpBox(controller.Status, MessageType.None);
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.IntField("当前句序号（从 1 起）", controller.CurrentStep?.DisplayNumber ?? 0);
                    EditorGUILayout.TextField("当前台词 ID", controller.CurrentStep?.LineId ?? "");
                    EditorGUILayout.LongField("当前播放轮次", controller.CurrentStep?.Generation ?? 0);
                    EditorGUILayout.Toggle("音频已完成", controller.AudioCompleted);
                    EditorGUILayout.Toggle("操作已完成", controller.OperationCompleted);
                    EditorGUILayout.Toggle("正在等待下一句", controller.IsWaitingForNextLine);
                    EditorGUILayout.FloatField("句间剩余秒数", controller.RemainingDelay);
                }
            }
        }
    }

    [CustomPropertyDrawer(typeof(NarrationLine))]
    public sealed class NarrationLineDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float height = EditorGUIUtility.singleLineHeight;
            if (!property.isExpanded) return height;
            foreach (var child in Children(property))
                height += EditorGUIUtility.standardVerticalSpacing + EditorGUI.GetPropertyHeight(child, true);
            return height;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            using (new EditorGUI.PropertyScope(position, label, property))
            {
                Rect rect = position;
                rect.height = EditorGUIUtility.singleLineHeight;
                string who = property.FindPropertyRelative("speaker").stringValue;
                string file = property.FindPropertyRelative("audioFileName").stringValue;
                string title = string.IsNullOrEmpty(file) ? label.text : file;
                property.isExpanded = EditorGUI.Foldout(rect, property.isExpanded,
                    string.IsNullOrEmpty(who) ? title : who + " · " + title, true);
                if (!property.isExpanded) return;
                using (new EditorGUI.IndentLevelScope())
                {
                    foreach (var child in Children(property))
                    {
                        rect.y += rect.height + EditorGUIUtility.standardVerticalSpacing;
                        rect.height = EditorGUI.GetPropertyHeight(child, true);
                        EditorGUI.PropertyField(rect, child, true);
                    }
                }
            }
        }

        static System.Collections.Generic.IEnumerable<SerializedProperty> Children(SerializedProperty parent)
        {
            using var child = parent.Copy();
            using var end = child.GetEndProperty();
            if (!child.NextVisible(true)) yield break;
            do
            {
                if (SerializedProperty.EqualContents(child, end)) yield break;
                yield return child;
            } while (child.NextVisible(false));
        }
    }
}
