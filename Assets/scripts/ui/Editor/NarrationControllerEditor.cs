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
            EditorGUILayout.HelpBox("表头：audioFileName、speaker、delayAfter（可选，秒）。有间隔列时按表格覆盖，空白按 0；旧表无此列时保留原间隔。重导入保留同名音频与说话者的推进方式和事件。音频放在 Resources/Narration；操作成功可直接绑定 PublishCurrentOperation。", MessageType.Info);
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
                        // Read 会在整表校验通过后更新原句间隔，必须在它之前记录 Undo。
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
                    EditorGUILayout.IntField("当前步骤 ID（未开始为 -1）", controller.CurrentLineIndex);
                    EditorGUILayout.Toggle("音频已完成", controller.AudioCompleted);
                    EditorGUILayout.Toggle("操作已完成", controller.OperationCompleted);
                    EditorGUILayout.Toggle("正在等待下一句", controller.IsWaitingForNextLine);
                    EditorGUILayout.FloatField("句间剩余秒数", controller.RemainingDelay);
                }
            }
        }
    }
}
