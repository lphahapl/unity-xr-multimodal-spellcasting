using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace NarutoVR.SealCombat.Editor
{
    /// <summary>构造隔离的旧配置样例，验证迁移规则与兼容行为。</summary>
    public static class SkillMigrationChecks
    {
        private const string FolderPrefix = "__SkillMigrationChecks_";

        [MenuItem("Tools/NarutoVR/技能配置/运行旧场景迁移检查")]
        public static void Menu() => Debug.Log($"[技能迁移] {Run()} 组检查通过。");

        public static int Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("迁移检查需要编辑模式");
            string folderName = FolderPrefix + Guid.NewGuid().ToString("N");
            string directory = "Assets/" + folderName;
            var previous = SceneManager.GetActiveScene();
            Scene fixture = default;
            SkillCatalog fallback = null;
            bool ownsDirectory = false;
            try
            {
                string folderGuid = AssetDatabase.CreateFolder("Assets", folderName);
                Require(AssetDatabase.GUIDToAssetPath(folderGuid) == directory, "临时测试目录创建失败");
                ownsDirectory = true;
                fixture = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                SceneManager.SetActiveScene(fixture);
                var root = new GameObject("旧技能迁移测试");
                var effects = root.AddComponent<SkillEffects>();
                effects.shieldCapacity = 200;
                effects.projectileDamage = 40;
                effects.ballRadius = .09f;
                effects.projectileSpeed = 15;
                effects.projectileLifetime = 6;
                effects.heldLifetime = 7;
                effects.heldOffset = new Vector3(.02f, .04f, .11f);
                effects.armDelay = .4f;
                effects.trackingLossGrace = .7f;
                effects.pushDistance = .2f;
                effects.pushMinimumSeconds = .25f;
                effects.pushMaximumSeconds = .8f;
                var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
                Require(shader, "缺少测试材质着色器");
                var material = new Material(shader) { name = "旧场景材质" };
                AssetDatabase.CreateAsset(material, directory + "/旧场景材质.mat");
                effects.fireMaterial = effects.buffMaterial = material;
                // No visual prefabs: the old basic primitive and material must remain in use.
                effects.fireballPrefab = effects.shieldPrefab = effects.impactPrefab = null;

                var shield = CreateLegacySkill(directory, "旧护盾", "migration.shield", SkillEffectKind.SelfBuff);
                var ball = CreateLegacySkill(directory, "旧火球", "migration.ball", SkillEffectKind.HeldFireball);
                var explicitEffect = ScriptableObject.CreateInstance<ShieldEffectDefinition>();
                explicitEffect.capacity = 333;
                AssetDatabase.CreateAsset(explicitEffect, directory + "/用户显式效果.asset");
                var explicitSkill = CreateLegacySkill(directory, "用户技能", "migration.explicit", SkillEffectKind.SelfBuff);
                explicitSkill.effectDefinition = explicitEffect;
                EditorUtility.SetDirty(explicitSkill);

                var controller = root.AddComponent<SkillTestController>();
                controller.effects = effects;
                controller.skills = new List<SkillDefinition> { shield, ball, explicitSkill };
                var textObject = new GameObject("原文字面板", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                textObject.transform.SetParent(root.transform, false);
                var originalText = textObject.GetComponent<Text>();
                var originalFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/SealLab/Resources/Fonts/NotoSansCJKsc-Regular.otf");
                Require(originalFont, "缺少现有中文字体，无法验收字体接线迁移");
                originalText.font = originalFont;
                controller.statusText = originalText;
                controller.chineseFont = originalFont;
                var view = root.AddComponent<SkillTestView>(); // Deliberately empty view fields.
                view.shortcuts.Add(new SkillTestView.Shortcut { skill = shield, key = Key.F11 });
                view.shortcuts.Add(new SkillTestView.Shortcut { skill = ball, key = Key.F10 });
                Require(EditorSceneManager.SaveScene(fixture, directory + "/旧场景.unity"), "保存隔离测试场景失败");
                fallback = ScriptableObject.CreateInstance<SkillCatalog>(); // Empty default must not replace legacy skills.

                Require(SkillConfigurationSetup.Bind(controller, fallback, directory + "/迁移"), "首次迁移应产生变更");
                var catalog = controller.catalog;
                Require(catalog && catalog != fallback && AssetDatabase.GetAssetPath(catalog).StartsWith(directory + "/迁移/", StringComparison.Ordinal), "应创建独立、稳定的旧场景目录");
                var migratedShield = catalog.skills[0].effectDefinition as ShieldEffectDefinition;
                var migratedBall = catalog.skills[1].effectDefinition as ProjectileEffectDefinition;
                Require(migratedShield && migratedShield.capacity == 200 && migratedShield.material == material && !migratedShield.visualPrefab,
                    "旧护盾容量200、基础材质或无预置体设置丢失");
                Require(migratedBall && migratedBall.damage == 40 && migratedBall.radius == .09f && migratedBall.speed == 15 &&
                    migratedBall.flightLifetime == 6 && migratedBall.heldLifetime == 7 && migratedBall.heldOffset == effects.heldOffset &&
                    migratedBall.armDelay == .4f && migratedBall.trackingLossGrace == .7f && migratedBall.pushDistance == .2f &&
                    migratedBall.pushMinimumSeconds == .25f && migratedBall.pushMaximumSeconds == .8f &&
                    migratedBall.material == material && !migratedBall.visualPrefab && !migratedBall.impactPrefab,
                    "旧投射物伤害40、运动参数或基础视觉未完整保留");
                int passed = 1;

                Require(catalog.skills[0] != shield && catalog.skills[1] != ball && !shield.effectDefinition && !ball.effectDefinition,
                    "迁移不能将场景效果写入共享旧技能");
                Require(catalog.skills[2] == explicitSkill && explicitSkill.effectDefinition == explicitEffect && explicitEffect.capacity == 333,
                    "用户显式效果资产及技能引用必须原样保留");
                passed++;

                Require(controller.view == view && view.controller == controller && view.statusText == originalText && view.chineseFont == originalFont &&
                    controller.statusText == originalText && controller.chineseFont == originalFont,
                    "空View不得清空旧文字或字体接线");
                passed++;

                Require(view.shortcuts[0].skill == catalog.skills[0] && view.shortcuts[0].key == Key.F11 &&
                    view.shortcuts[1].skill == catalog.skills[1] && view.shortcuts[1].key == Key.F10,
                    "技能复制后快捷键必须按稳定ID重接，并保留用户按键");
                passed++;

                // Matching legacy values should reuse an existing catalog instead of forking it.
                var peer = new GameObject("相同配置的第二场景等价对象").AddComponent<SkillTestController>();
                peer.effects = effects; peer.skills = controller.skills;
                SkillConfigurationSetup.Bind(peer, catalog, directory + "/不应创建");
                Require(peer.catalog == catalog, "相同旧效果配置应共享目录。配方对比：\n" +
                    string.Join("\n", controller.skills.Select((old, i) =>
                        $"旧[{i}] sealNameNull={old.sealName == null} {JsonUtility.ToJson(old)}\n" +
                        $"新[{i}] sealNameNull={catalog.skills[i].sealName == null} {JsonUtility.ToJson(catalog.skills[i])}")));
                Require(!AssetDatabase.IsValidFolder(directory + "/不应创建"), "复用共享目录时不应创建多余配置资产");
                // The null/empty equivalence must not allow a different real voice recipe to share.
                string originalPhrase = shield.phrase;
                shield.phrase = "另一个测试口令";
                peer.catalog = null;
                SkillConfigurationSetup.Bind(peer, catalog, directory + "/不同配方");
                Require(peer.catalog != catalog && peer.catalog.skills[0].phrase == "另一个测试口令",
                    "有效口令不同必须保留独立配方，不能因可选字段兼容而复用目录");
                shield.phrase = originalPhrase;
                passed++;

                migratedBall.damage = 55;
                migratedShield.capacity = 250;
                EditorUtility.SetDirty(migratedBall); EditorUtility.SetDirty(migratedShield);
                effects.projectileDamage = 99; effects.shieldCapacity = 999;
                int assetCount = AssetDatabase.FindAssets("", new[] { directory }).Length;
                Require(!SkillConfigurationSetup.Bind(controller, fallback, directory + "/迁移"), "重复迁移不应改写已完成配置");
                Require(controller.catalog == catalog && migratedBall.damage == 55 && migratedShield.capacity == 250, "重复迁移覆盖了用户调过的新效果");
                passed++;

                // A lost scene reference can be rebound without recreating or rewriting its generated assets.
                controller.catalog = null;
                view.shortcuts[1].skill = ball;
                SkillConfigurationSetup.Bind(controller, fallback, directory + "/迁移");
                Require(controller.catalog == catalog && migratedBall.damage == 55 && migratedShield.capacity == 250 &&
                    view.shortcuts[1].skill == catalog.skills[1] && view.shortcuts[1].key == Key.F10 &&
                    AssetDatabase.FindAssets("", new[] { directory }).Length == assetCount,
                    "按稳定场景GUID重绑时应复用资产、保留调参，并恢复快捷键");
                passed++;
                return passed;
            }
            finally
            {
                if (fixture.IsValid() && fixture.isLoaded) EditorSceneManager.CloseScene(fixture, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                if (fallback) Object.DestroyImmediate(fallback);
                if (ownsDirectory) DeleteOwnedDirectory(directory, folderName);
            }
        }

        private static SkillDefinition CreateLegacySkill(string directory, string name, string id, SkillEffectKind kind)
        {
            var skill = ScriptableObject.CreateInstance<SkillDefinition>();
            skill.name = skill.displayName = name;
            skill.skillId = id; skill.effectKind = kind; skill.triggerMode = SkillTriggerMode.VoiceOnly;
            skill.phrase = name; skill.phraseTokens = "test"; skill.effectDefinition = null;
            AssetDatabase.CreateAsset(skill, directory + "/" + name + ".asset");
            return skill;
        }

        private static void DeleteOwnedDirectory(string directory, string folderName)
        {
            string expected = Path.GetFullPath(Path.Combine(Application.dataPath, folderName));
            string actual = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), directory));
            Require(folderName.StartsWith(FolderPrefix, StringComparison.Ordinal) &&
                Guid.TryParseExact(folderName.Substring(FolderPrefix.Length), "N", out _) &&
                directory == "Assets/" + folderName && string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase),
                "拒绝清理测试范围以外的目录");
            if (AssetDatabase.IsValidFolder(directory))
                Require(AssetDatabase.DeleteAsset(directory), "清理独立迁移测试目录失败：" + directory);
        }

        private static void Require(bool condition, string detail)
        {
            if (!condition) throw new InvalidOperationException(detail);
        }
    }
}
