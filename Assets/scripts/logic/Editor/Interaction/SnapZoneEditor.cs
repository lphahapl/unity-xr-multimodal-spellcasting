// 基于 NeuGameMRTools 的吸附 Inspector；保留原字段绑定，适配本项目列表路径和 Undo。
using NeuGameMRTools.Runtime.Components;
using UnityEditor;
using UnityEngine;

namespace NeuGameMRTools.Editor.Editors
{
    [CustomEditor(typeof(SnapZone))]
    public sealed class SnapZoneEditor : UnityEditor.Editor
    {
        GameObject testObject;
        SerializedProperty P(string name) => serializedObject.FindProperty(name);
        void Field(string name, string label) => EditorGUILayout.PropertyField(P(name), new GUIContent(label), true);

        public override void OnInspectorGUI()
        {
            var zone = (SnapZone)target;
            serializedObject.Update();
            EditorGUILayout.LabelField("吸附与路径", EditorStyles.boldLabel);
            Field("_snapMode", "吸附模式");
            if (P("_snapMode").enumValueIndex == (int)SnapMode.SinglePoint)
            {
                Field("_snapTarget", "目标位置（可空）");
                Field("_snapRotationTarget", "目标旋转（可空）");
                EditorGUILayout.HelpBox("单点模式沿用原版：位置/旋转未设置时分别使用区域自身的世界姿态。", MessageType.Info);
                if (GUILayout.Button("自动绑定子节点 SnapTarget"))
                {
                    var point = zone.transform.Find("SnapTarget");
                    P("_snapTarget").objectReferenceValue = point ? point : zone.transform;
                    P("_snapRotationTarget").objectReferenceValue = zone.transform.Find("SnapRotationTarget") ?? point ?? zone.transform;
                }
            }
            else
            {
                Field("_snapPoints", "吸附点 List（由上到下）");
                EditorGUILayout.HelpBox("同一个道具按列表顺序经过各点；默认时长作用于每段，勾选单独设置时长可覆盖。整条完成事件只在最后一点触发。", MessageType.Info);
                if (GUILayout.Button("新建并追加路径点"))
                {
                    serializedObject.ApplyModifiedProperties();
                    var point = new GameObject("SnapPoint_" + (P("_snapPoints").arraySize + 1));
                    Undo.RegisterCreatedObjectUndo(point, "新建吸附路径点");
                    Undo.SetTransformParent(point.transform, zone.transform, "设置路径点父级");
                    point.transform.localPosition = Vector3.right * (.2f * P("_snapPoints").arraySize);
                    point.transform.localRotation = Quaternion.identity;
                    serializedObject.Update();
                    var points = P("_snapPoints"); int i = points.arraySize; points.InsertArrayElementAtIndex(i);
                    var entry = points.GetArrayElementAtIndex(i);
                    entry.FindPropertyRelative("target").objectReferenceValue = point.transform;
                    entry.FindPropertyRelative("rotationTarget").objectReferenceValue = null;
                    entry.FindPropertyRelative("overrideDuration").boolValue = false;
                    entry.FindPropertyRelative("duration").floatValue = .5f;
                    EditorGUIUtility.PingObject(point);
                }
            }
            Field("_snapDuration", "默认每段时长（秒）");
            Field("_followZoneDuringSnap", "吸附过程中跟随区域移动");
            Field("_snapCurve", "原版动画曲线（每段使用）");
            EditorGUILayout.Space();
            Field("_requireSnapItem", "自动触发要求 SnapItem 标记");
            Field("_waitForRelease", "等待 Meta 抓取松手");
            Field("_requireNameMatch", "要求名称匹配");
            if (P("_requireNameMatch").boolValue) Field("_targetObjectName", "根物体名称包含（空为不限）");
            Field("_disableCollidersOnSnap", "整条完成后禁用道具碰撞体");
            EditorGUILayout.HelpBox("路径点与曲线在启动时冻结。启用跟随时，整段路径随区域移动。释放后恢复道具的物理与抓取驱动。", MessageType.Info);
            EditorGUILayout.Space();
            Field("_onEnterEvent", "开始吸附");
            Field("_onPointReachedEvent", "到达路径点（物体、零起始序号）");
            Field("_onSnapCompletedEvent", "整条吸附完成");
            Field("_onExitEvent", "释放 / 取消吸附");
            serializedObject.ApplyModifiedProperties();

            var collider = zone.GetComponent<Collider>();
            if (!collider || !collider.isTrigger)
            {
                EditorGUILayout.HelpBox("自动吸附需要区域根节点的 Trigger Collider。手动 SnapObject 调用不依赖触发器。", MessageType.Warning);
                using (new EditorGUI.DisabledScope(Application.isPlaying))
                    if (GUILayout.Button("添加 / 设置区域触发器"))
                    {
                        if (!collider) { collider = Undo.AddComponent<BoxCollider>(zone.gameObject); ((BoxCollider)collider).size = Vector3.one * .25f; }
                        Undo.RecordObject(collider, "配置吸附触发器"); collider.isTrigger = true;
                        PrefabUtility.RecordPrefabInstancePropertyModifications(collider);
                    }
            }
            EditorGUILayout.Space();
            testObject = (GameObject)EditorGUILayout.ObjectField("测试道具", testObject, typeof(GameObject), true);
            using (new EditorGUI.DisabledScope(!Application.isPlaying || !testObject || zone.SnappedObject))
                if (GUILayout.Button("播放吸附路径")) zone.SnapObject(testObject);
            using (new EditorGUI.DisabledScope(!Application.isPlaying || !zone.SnappedObject))
                if (GUILayout.Button("停止并释放（当前位置）")) zone.ReleaseObject();
            if (Application.isPlaying)
            {
                EditorGUILayout.LabelField("当前道具", zone.SnappedObject ? zone.SnappedObject.name : "无");
                EditorGUILayout.LabelField("进度", zone.IsSnapping ? $"第 {zone.CurrentPointIndex + 1}/{zone.PointCount} 段" : zone.SnappedObject ? "已到达，等待释放" : "空闲");
                if (!string.IsNullOrEmpty(zone.LastFailure)) EditorGUILayout.HelpBox(zone.LastFailure, MessageType.Info);
                Repaint();
            }
        }

        [MenuItem("GameObject/NeuGameMRTools/交互/创建吸附区域", false, 200)]
        static void CreateZone(MenuCommand command)
        {
            var go = new GameObject("SnapZone"); Undo.RegisterCreatedObjectUndo(go, "创建吸附区域");
            GameObjectUtility.SetParentAndAlign(go, command.context as GameObject);
            Undo.AddComponent<SnapZone>(go);
            var collider = Undo.AddComponent<BoxCollider>(go); collider.isTrigger = true; collider.size = Vector3.one * .25f;
            Selection.activeGameObject = go;
        }

        [MenuItem("Tools/NarutoVR/交互/为选中道具添加吸附标记")]
        static void AddItem()
        {
            var go = Selection.activeGameObject;
            if (!go) return;
            if (!go.GetComponent<SnapItem>()) Undo.AddComponent<SnapItem>(go);
            if (!go.GetComponent<Rigidbody>())
            {
                var rb = Undo.AddComponent<Rigidbody>(go); rb.isKinematic = true; rb.useGravity = false;
            }
            EditorGUIUtility.PingObject(go);
        }
    }
}
