#if UNITY_EDITOR
using System;
using System.Linq;
using NarutoVR.SealCombat.Rpg;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NarutoVR.SealCombat.Editor
{
    public static class RpgInteractionSetup
    {
        [MenuItem("Tools/NarutoVR/线性RPG/配置四向操作与骨骼投掷")]
        public static void Configure()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != LinearRpgSceneSetup.ScenePath || scene.isDirty)
                throw new InvalidOperationException("需要已保存的LinearRpgTest编辑模式场景");
            var controller = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<LinearRpgController>(true)).Single();
            Configure(controller);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new Exception("交互配置保存失败");
        }
        internal static GameObject PresentationGroup(LinearRpgController controller)
        {
            const string groupName = "界面与交互反馈";
            var existing = controller.transform.Find(groupName);
            if (existing) return existing.gameObject;
            var group = new GameObject(groupName);
            group.transform.SetParent(controller.transform, false);
            return group;
        }

        public static void Configure(LinearRpgController controller)
        {
            var movement = controller.locomotion;
            movement.gestures.requireNavigationForDodge = false;
            movement.gestures.dwellSeconds = .65f;
            var feedback = controller.GetComponentInChildren<RpgInteractionFeedback>(true);
            if (!feedback) feedback = PresentationGroup(controller).AddComponent<RpgInteractionFeedback>();
            feedback.controller = controller;
            foreach (var skill in controller.skills.catalog.skills)
            {
                if (!skill || !(skill.effectDefinition is ProjectileEffectDefinition projectile)) continue;
                if (!AssetDatabase.GetAssetPath(projectile).StartsWith(LinearRpgSceneSetup.ConfigurationRoot + "/", StringComparison.Ordinal))
                    throw new Exception("拒绝改写非RPG独立效果：" + skill.name);
                projectile.releaseMode = ProjectileReleaseMode.GripThrow;
                if (projectile.throwing == null) projectile.throwing = new HandThrowSettings();
                skill.leftHand = false;
                EditorUtility.SetDirty(projectile); EditorUtility.SetDirty(skill);
                AssetDatabase.SaveAssetIfDirty(projectile); AssetDatabase.SaveAssetIfDirty(skill);
            }
            foreach (var text in controller.gameObject.scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<UnityEngine.UI.Text>(true)))
                if (text.name == "导航与施法说明")
                    text.text = "左食指指向落点 → 沿蓝色路径看目标 → 金圈填满后传送。移开或放松后再次选择。\n右手张掌四向短划即可闪避，无需左手指向。双手靠近时保护结印，分开稳定后重新划动。\n右手火球：握拳或捏住 → 甩手松开 → 按手的运动方向飞出。持球时投掷优先；双手放下可取消。";
            var first = controller.stages.FirstOrDefault();
            if (first)
            {
                first.instructions = "寅印与“火球术”匹配后，右手握拳或捏住火球，甩手并松开。击破本阶段训练靶，再前往下一阶段。";
                EditorUtility.SetDirty(first); AssetDatabase.SaveAssetIfDirty(first);
            }
            EditorUtility.SetDirty(movement); EditorUtility.SetDirty(feedback);
        }
    }
}
#endif
