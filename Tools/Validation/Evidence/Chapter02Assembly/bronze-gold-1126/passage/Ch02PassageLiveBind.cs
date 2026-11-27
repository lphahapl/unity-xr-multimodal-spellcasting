using System;
using System.Linq;
using NarutoVR.SealCombat.Rpg;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NarutoVR.SealCombat.Editor
{
    public static class Ch02PassageLiveBind
    {
        public static bool Apply(Scene scene)
        {
            if (scene.path != "Assets/Scene/Chapter02_SuspendedPath.unity" || !scene.isLoaded || Application.isPlaying)
                throw new InvalidOperationException("需要二章编辑现场。");
            var bridge = scene.GetRootGameObjects().SelectMany(root =>
                root.GetComponentsInChildren<Chapter02BridgeEquipment>(true)).Single();
            if (!(bridge.drawingStage is Chapter02PassageStage stage) || !stage.passage ||
                !(stage.passage.effect is PassageGlyphEffect) || !stage.socketPrefab ||
                bridge.drawingStage != bridge.player.StageAt(1) || !bridge.panel || !bridge.panel.board)
                throw new InvalidOperationException("通路符、画板或铜榫未接入。");
            bool rerouted = Chapter02GoldTrailInstaller.Apply(scene);
            var rootObject = bridge.panel.transform.parent.gameObject;
            if (rootObject.name != "断桥通路符" && rootObject.name != "断桥方直之矩")
                throw new InvalidOperationException("石案画板层级已变化。");
            var old = bridge.panel.template;
            if (!old || (old != stage.passage && old.symbolId != "glyph.square-rule"))
                throw new InvalidOperationException("剧情板范本已有另一项修改。");
            if (old == stage.passage && rootObject.name == "断桥通路符") return rerouted;
            Undo.RecordObject(bridge.panel, "设置首桥通路符范本");
            bridge.panel.template = stage.passage;
            EditorUtility.SetDirty(bridge.panel);
            Undo.RecordObject(rootObject, "更新首桥教学名称");
            rootObject.name = "断桥通路符";
            EditorUtility.SetDirty(rootObject);
            EditorSceneManager.MarkSceneDirty(scene);
            return true;
        }
    }
}
