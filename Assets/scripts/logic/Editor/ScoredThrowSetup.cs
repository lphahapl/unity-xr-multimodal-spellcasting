#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using NarutoVR.SealCombat.Rpg;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NarutoVR.SealCombat.Editor
{
    public static class ScoredThrowSetup
    {
        [MenuItem("Tools/NarutoVR/线性RPG/配置评分投掷与自动诊断")]
        public static void Menu()=>Debug.Log(Configure());
        public static string Configure()
        {
            var scene=SceneManager.GetActiveScene();
            if(EditorApplication.isPlayingOrWillChangePlaymode||scene.path!=LinearRpgSceneSetup.ScenePath||scene.isDirty)
                throw new InvalidOperationException("请在已保存的LinearRpgTest编辑场景配置");
            var controller=All<LinearRpgController>(scene).Single();
            var view=All<RpgSessionView>(scene).Single(v=>v.controller==controller);
            var effects=controller.skills.effects;
            var fire=controller.skills.catalog.skills.Single(s=>s&&s.skillId=="fireball").effectDefinition as ProjectileEffectDefinition;
            if(!effects||!fire||fire.releaseMode!=ProjectileReleaseMode.GripThrow||fire.throwing==null||!fire.throwing.Valid)
                throw new InvalidOperationException("火球投掷配置或接线无效");
            string backup=Path.GetFullPath("../NarutoVR_Backups/scored-throw-config-"+DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"));Directory.CreateDirectory(backup);
            File.Copy(scene.path,Path.Combine(backup,"LinearRpgTest.unity"));File.Copy(AssetDatabase.GetAssetPath(fire),Path.Combine(backup,"fireball.asset"));
            fire.throwing.assisted=new AssistedThrowSettings{enabled=true};EditorUtility.SetDirty(fire);
            var recorder=effects.GetComponent<ThrowDiagnosticRecorder>();if(!recorder)recorder=effects.gameObject.AddComponent<ThrowDiagnosticRecorder>();
            recorder.effects=effects;recorder.autoRecord=true;EditorUtility.SetDirty(recorder);
            view.throwRecorder=recorder;view.showThrowSection=true;view.showMovementSection=false;view.showWorldDiagnostics=false;
            // Only referenced diagnostic canvases; wrist children and world gameplay cues are excluded.
            var panels=new System.Collections.Generic.HashSet<GameObject>();
            void Add(UnityEngine.UI.Text text) { if(text&&text.canvas&&text.canvas.renderMode==RenderMode.WorldSpace)panels.Add(text.canvas.gameObject); }
            Add(view.statusText);Add(controller.skills.statusText);
            foreach(var labView in All<NarutoVR.SealLab.SealLabView>(scene))Add(labView.statusText);
            view.diagnosticWorldPanels=panels.ToArray();EditorUtility.SetDirty(view);
            var inventory=All<Canvas>(scene).Select(c=>$"{c.name} | active={c.gameObject.activeSelf} | controlled={panels.Contains(c.gameObject)}");
            Directory.CreateDirectory("Logs/scored-throw-20261031");File.WriteAllLines("Logs/scored-throw-20261031/world-panels.txt",inventory);
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);
            if(!EditorSceneManager.SaveScene(scene))throw new IOException("场景保存失败");
            return $"火球启用72分后拉前送与0.6秒伸手保持；自动记录15分钟；{panels.Count}个世界调试面板可开关，默认关闭；备份：{backup}";
        }
        static T[] All<T>(Scene scene) where T:Component=>scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<T>(true)).ToArray();
    }
}
#endif
