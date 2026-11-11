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
    /// <summary>Explicit presentation/filter setup; preserves timing, safety gates and existing instructions.</summary>
    public static class InteractionFeelSetup
    {
        [MenuItem("Tools/NarutoVR/线性RPG/应用平衡交互手感与详细调试")]
        public static void Menu() => Debug.Log("[交互手感配置] " + Configure());

        public static string Configure()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != LinearRpgSceneSetup.ScenePath)
                throw new InvalidOperationException("需要当前 LinearRpgTest 编辑模式场景");
            var controllers = Components<LinearRpgController>(scene);
            if (controllers.Length != 1) throw new InvalidOperationException("必须只有一个线性关卡主控");
            var controller = controllers[0];
            var movement = controller.locomotion;
            var feedback = controller.GetComponentInChildren<RpgInteractionFeedback>(true);
            var view = Components<RpgSessionView>(scene).SingleOrDefault(v => v.controller == controller);
            if (!movement || movement.gestures == null || movement.smoothing == null || !movement.smoothing.Valid || !feedback || !view ||
                !controller.skills || !controller.skills.catalog)
                throw new InvalidOperationException("缺少移动、滤波、反馈、界面或技能配置，拒绝部分应用");
            var projectiles = controller.skills.catalog.skills.Where(s => s && s.effectDefinition is ProjectileEffectDefinition)
                .Select(s => (ProjectileEffectDefinition)s.effectDefinition).Distinct().ToArray();
            if (projectiles.Length == 0) throw new InvalidOperationException("未找到当前关卡投射物资产");
            foreach (var projectile in projectiles)
            {
                string path = AssetDatabase.GetAssetPath(projectile);
                if (!path.StartsWith(LinearRpgSceneSetup.ConfigurationRoot + "/", StringComparison.Ordinal) ||
                    !File.Exists(path) || projectile.throwing == null || !projectile.throwing.Valid)
                    throw new InvalidOperationException("投掷配置无效或不属于独立RPG目录：" + path);
            }

            string backup = Path.GetFullPath(Path.Combine(Application.dataPath,
                "../../NarutoVR_Backups/interaction-feel-preset-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")));
            Directory.CreateDirectory(backup);
            File.Copy(Path.GetFullPath(scene.path), Path.Combine(backup, "LinearRpgTest.unity"), false);
            foreach (var projectile in projectiles)
                File.Copy(Path.GetFullPath(AssetDatabase.GetAssetPath(projectile)), Path.Combine(backup, Path.GetFileName(AssetDatabase.GetAssetPath(projectile))), false);

            // Preserve both the last disk version and any current in-memory edits before applying our scoped preset.
            if (scene.isDirty && !EditorSceneManager.SaveScene(scene, LinearRpgSceneSetup.ScenePath))
                throw new IOException("保存现有场景修改失败；尚未应用手感设置，磁盘原版已备份：" + backup);
            File.Copy(Path.GetFullPath(scene.path), Path.Combine(backup,"LinearRpgTest-before-preset-with-current-edits.unity"),false);

            movement.smoothing.enabled = true;
            view.desktopPanelWidth = 460;
            view.showMovementSection = view.showThrowSection = true;
            view.showRuntimeSection = view.showSimulationSection = false;
            feedback.showThrowGrip = feedback.showThrowDirection = true;
            feedback.playGripCue = feedback.playDwellCue = feedback.playThrowRejectedCue = true;
            EditorUtility.SetDirty(movement); EditorUtility.SetDirty(feedback); EditorUtility.SetDirty(view);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, LinearRpgSceneSetup.ScenePath)) throw new IOException("保存交互手感场景失败；备份：" + backup);
            return $"已启用导航平滑、手边反馈和详细调试；保留当前导航准备{movement.gestures.readySeconds:0.###}秒、驻留{movement.gestures.dwellSeconds:0.###}秒以及全部投掷确认时长。未修改教学说明。备份：" + backup;
        }

        static T[] Components<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
    }
}
#endif
