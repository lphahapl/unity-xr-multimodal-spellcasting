#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using NarutoVR.SealCombat.Enemies;
using NarutoVR.SealCombat.Rpg;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace NarutoVR.SealCombat.Editor
{
    public static class EnemyPoolingSetup
    {
        public const string Log = "Logs/enemy-pooling-20261104";
        public const string CatalogPath = "Assets/ScriptableObjects/敌人目录配置/本关怪物目录.asset";
        public const string DefinitionPath = "Assets/ScriptableObjects/敌人定义配置/残机偶.asset";
        public const string PrefabPath = "Assets/Art/Enemies/CanJiOu/CanJiOu_Room.prefab";
        static T[] Components<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<T>(true)).ToArray();
        public static string Configure()
        {
            var scene = SceneManager.GetActiveScene();
            if (Application.isPlaying || scene.path != LinearRpgValidation.P1ScenePath) throw new Exception("需要编辑模式的 P1 场景");
            var room = Components<CombatRoomEncounter>(scene).Single();
            if (room.UsesEnemyManager) return Validate();
            if (room.enemies.Length != 1 || !room.enemies[0]) throw new Exception("自动接入针对当前单怪房间；已有布局变化时请手动配置出生点");
            Directory.CreateDirectory(Log);
            string backup = Log + "/KaogongP1Trial_beforePool_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".unity";
            if (!EditorSceneManager.SaveScene(scene, backup, true)) throw new Exception("未能备份内存场景");
            var old = room.enemies[0]; Vector3 position = old.transform.position; Quaternion rotation = old.transform.rotation;
            var combat = old.GetComponent<EnemyObj>().CombatDefinition;
            Folder("Assets/ScriptableObjects/敌人定义配置"); Folder("Assets/ScriptableObjects/敌人目录配置");
            if (AssetDatabase.LoadMainAssetAtPath(PrefabPath)) throw new Exception("房间变体已存在，拒绝覆盖：" + PrefabPath);
            // Preserve all scene-specific tuning in an Addressable prefab variant before removing its scene instance.
            var prefab = PrefabUtility.SaveAsPrefabAsset(old.gameObject, PrefabPath);
            if (!prefab) throw new Exception("房间敌人变体保存失败");
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            if (!settings) throw new Exception("Addressables settings创建失败");
            var group = settings.FindGroup("Enemies_Local") ?? settings.CreateGroup("Enemies_Local", false, false, true, null,
                typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            var schema = group.GetSchema<BundledAssetGroupSchema>();
            schema.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kLocalBuildPath);
            schema.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kLocalLoadPath);
            schema.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackSeparately;
            var entry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(PrefabPath), group);
            entry.address = "enemies/canjiou"; entry.SetLabel("EnemyPrefab", true, true);
            settings.ActivePlayModeDataBuilderIndex = 0; // Editor AssetDatabase mode; player uses built local bundles.
            settings.BuildAddressablesWithPlayerBuild = AddressableAssetSettings.PlayerBuildOption.BuildWithPlayer;
            EditorUtility.SetDirty(settings); EditorUtility.SetDirty(schema);
            var definition = ScriptableObject.CreateInstance<EnemyDefinition>(); definition.enemyId = "canjiou";
            definition.displayName = "残机偶"; definition.prefab = new AssetReferenceGameObject(entry.guid);
            definition.combatDefinition = combat; definition.maxInactive = 4;
            AssetDatabase.CreateAsset(definition, DefinitionPath);
            var catalog = ScriptableObject.CreateInstance<EnemyCatalog>(); catalog.enemies = new[] { definition };
            AssetDatabase.CreateAsset(catalog, CatalogPath);
            var services = new GameObject("游戏会话 · 异步敌人服务");
            var manager = services.AddComponent<EnemyManager>(); manager.catalog = catalog;
            var game = services.AddComponent<GameManager>(); game.enemyManager = manager;
            game.prepareEnemies = new[] { new GameManager.EnemyPreparation { enemyId = "canjiou", count = 1 } };
            var point = new GameObject("出生点 · 残机偶").transform; point.SetParent(room.transform, false); point.SetPositionAndRotation(position, rotation);
            room.enemyManager = manager;
            room.spawnPoints = new[] { new CombatRoomEncounter.SpawnPoint { enemyId = "canjiou", point = point, isSummoned = old.isSpawnedEnemy } };
            room.enemies = Array.Empty<EnemyController>();
            Object.DestroyImmediate(old.gameObject);
            EditorUtility.SetDirty(room); AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new Exception("保存异步生成场景失败");
            var detail = Validate(); File.WriteAllText(Log + "/setup.txt", detail + "\nbackup=" + backup); return detail;
        }
        static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/'); Folder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
        public static string Validate()
        {
            var scene = SceneManager.GetActiveScene(); var room = Components<CombatRoomEncounter>(scene).Single();
            if (!room.UsesEnemyManager || room.enemies.Length != 0 || room.spawnPoints.Length != 1 || !room.spawnPoints[0].point)
                throw new Exception("房间还存在预置敌人或缺少出生点");
            if (!room.enemyManager.catalog || !room.enemyManager.catalog.Validate(out string reason)) throw new Exception("怪物目录无效");
            if (Components<EnemyController>(scene).Length != 0) throw new Exception("场景仍有直接敌人模型实例");
            var def = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(DefinitionPath);
            if (!def || !def.prefab.RuntimeKeyIsValid() || !AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(def.prefab.AssetGUID))) throw new Exception("模型异步引用无效");
            var dependencies = AssetDatabase.GetDependencies(scene.path, true);
            if (dependencies.Contains(PrefabPath)) throw new Exception("场景仍直接依赖敌人模型");
            return "P1 已绑定 GameManager + EnemyManager + 怪物目录；单出生点使用canjiou；场景不直接依赖怪物模型；旧参数保留于预制体变体。";
        }
        public static string BuildContent()
        {
            Directory.CreateDirectory(Log);
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(false);
            if (!settings) throw new Exception("缺少Addressables配置");
            settings.BuildAddressablesWithPlayerBuild = AddressableAssetSettings.PlayerBuildOption.BuildWithPlayer;
            EditorUtility.SetDirty(settings); AssetDatabase.SaveAssets();
            AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult result);
            if (result == null || !string.IsNullOrEmpty(result.Error)) throw new Exception("Addressables构建失败：" + result?.Error);
            string text = DateTimeOffset.Now.ToString("O") + "\nPASS " + EditorUserBuildSettings.activeBuildTarget + " Addressables本地内容构建\n" + result.OutputPath;
            File.WriteAllText(Log + "/content-build.txt", text); return text;
        }
    }
}
#endif
