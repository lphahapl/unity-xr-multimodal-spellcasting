#if UNITY_EDITOR
using System;
using System.Linq;
using NarutoVR.SealCombat.Rpg;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NarutoVR.SealCombat.Editor
{
    public static class P1TrialChecks
    {
        [MenuItem("Tools/NarutoVR/P1试炼/验证当前试炼场景")]
        public static void RunMenu() => Debug.Log(Run());

        public static string Run()
        {
            var scene = SceneManager.GetActiveScene();
            if (Application.isPlaying || scene.path != LinearRpgValidation.P1ScenePath)
                throw new InvalidOperationException("请在编辑模式打开 KaogongP1Trial。");
            foreach (var root in scene.GetRootGameObjects())
                foreach (var item in root.GetComponentsInChildren<Transform>(true))
                    if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(item.gameObject) != 0)
                        throw new Exception("场景有缺失脚本：" + item.name);
            var rpg = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<LinearRpgController>(true)).Single();
            if (rpg.saveFileName != "kaogong-p1-trial.json" || rpg.autoLoad || !rpg.skills ||
                !rpg.skills.lab || !rpg.skills.voice || !rpg.skills.effects || !rpg.locomotion || !rpg.wristMenu)
                throw new Exception("P0 接线或试炼存档隔离失败");
            var pool = rpg.skills.effects.GetComponent<VfxPoolManager>();
            var projectile = rpg.skills.ConfiguredSkills.Single(s => s.skillId == "fireball").effectDefinition as ProjectileEffectDefinition;
            if (!pool || !projectile || !pool.impactPrefab || pool.impactPrefab != projectile.impactPrefab ||
                !rpg.GetComponent<P1AudioManager>() || !rpg.GetComponent<HapticFeedbackService>())
                throw new Exception("P1 服务或命中预制体接线失败");
            return $"场景接线与存档通过；特效 {P1VfxChecks.Run()}、音频 {P1AudioChecks.Run()}、触觉 {P1HapticChecks.Run()} 项通过。";
        }
    }
}
#endif
