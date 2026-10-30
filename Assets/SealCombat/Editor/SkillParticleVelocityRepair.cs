#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NarutoVR.SealCombat.Editor
{
    /// <summary>
    /// Repairs only mismatched constant modes in the four existing skill VFX prefabs.
    /// Constant axes become an equivalent min/max pair; user curves and other settings are preserved.
    /// </summary>
    public static class SkillParticleVelocityRepair
    {
        private static readonly string[] PrefabPaths =
        {
            SkillVisualAssets.FireballPath,
            SkillVisualAssets.ShieldPath,
            SkillVisualAssets.ImpactPath,
            SkillVisualAssets.SigilPath
        };

        [MenuItem("Tools/NarutoVR/技能/修复现有特效的粒子速度模式")]
        private static void RepairFromMenu() => RepairExistingVfxPrefabs();

        /// <returns>The number of particle systems repaired and saved.</returns>
        public static int RepairExistingVfxPrefabs()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("请停止播放，并等待 Unity 编译、导入完成后再修复粒子速度模式。");

            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && Array.IndexOf(PrefabPaths, stage.assetPath) >= 0)
                throw new InvalidOperationException("请先保存并退出当前技能特效的预置体编辑模式，再执行定向修复。");

            int repaired = 0, saved = 0;
            foreach (string path in PrefabPaths)
            {
                if (!AssetDatabase.LoadAssetAtPath<GameObject>(path))
                {
                    Debug.LogWarning($"[粒子速度修复] 跳过缺失的预置体：{path}；不会重新创建。");
                    continue;
                }

                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    int changed = 0;
                    foreach (var particle in root.GetComponentsInChildren<ParticleSystem>(true))
                    {
                        var velocity = particle.velocityOverLifetime;
                        if (!velocity.enabled || HaveSameMode(velocity)) continue;
                        if (!IsConstantMode(velocity.x) || !IsConstantMode(velocity.y) || !IsConstantMode(velocity.z))
                        {
                            Debug.LogWarning($"[粒子速度修复] {path} / {particle.name} 使用自定义曲线，保留原值，请手动统一 X/Y/Z 模式。");
                            continue;
                        }

                        // Promote only Constant axes, preserving the current value and dormant curves.
                        // Do not replace the complete module or regenerate the prefab.
                        if (velocity.x.mode == ParticleSystemCurveMode.Constant) velocity.x = AsConstantRange(velocity.x);
                        if (velocity.y.mode == ParticleSystemCurveMode.Constant) velocity.y = AsConstantRange(velocity.y);
                        if (velocity.z.mode == ParticleSystemCurveMode.Constant) velocity.z = AsConstantRange(velocity.z);
                        if (!HaveSameMode(velocity))
                            throw new InvalidOperationException($"{path} / {particle.name} 的速度曲线模式修复后仍不一致，未保存该预置体。");
                        changed++;
                    }

                    if (changed == 0) continue;
                    PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
                    if (!success) throw new InvalidOperationException($"无法保存粒子速度修复：{path}");
                    repaired += changed;
                    saved++;
                    Debug.Log($"[粒子速度修复] 已保存 {path}，修复 {changed} 个粒子系统；保留速度数值和其它特效参数。");
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }

            int remaining = CountInvalidEnabledVelocityModules();
            Debug.Log($"[粒子速度修复] 完成：保存 {saved} 个预置体，修复 {repaired} 个粒子系统，剩余不一致模块 {remaining} 个。");
            return repaired;
        }

        /// <summary>Read-only validation of enabled linear velocity axes in existing VFX prefabs.</summary>
        public static int CountInvalidEnabledVelocityModules()
        {
            int invalid = 0;
            foreach (string path in PrefabPaths)
            {
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (!root) continue;
                foreach (var particle in root.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var velocity = particle.velocityOverLifetime;
                    if (velocity.enabled && !HaveSameMode(velocity)) invalid++;
                }
            }
            return invalid;
        }

        private static bool HaveSameMode(ParticleSystem.VelocityOverLifetimeModule velocity) =>
            velocity.x.mode == velocity.y.mode && velocity.y.mode == velocity.z.mode;

        private static bool IsConstantMode(ParticleSystem.MinMaxCurve curve) =>
            curve.mode == ParticleSystemCurveMode.Constant || curve.mode == ParticleSystemCurveMode.TwoConstants;

        private static ParticleSystem.MinMaxCurve AsConstantRange(ParticleSystem.MinMaxCurve curve)
        {
            float value = curve.constant;
            curve.mode = ParticleSystemCurveMode.TwoConstants;
            curve.constantMin = value;
            curve.constantMax = value;
            return curve;
        }
    }
}
#endif
