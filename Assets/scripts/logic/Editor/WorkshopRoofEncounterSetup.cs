using System;
using System.Linq;
using NarutoVR.SealCombat.Enemies;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace NarutoVR.Editor
{
    public static class WorkshopRoofEncounterSetup
    {
        internal const string ScenePath = "Assets/Scene/Chapter02_SuspendedPath.unity";
        internal const string Folder = "Assets/Prefabs/WorkshopRoofEncounter";
        internal static readonly (string building, string asset, string area)[] Posts =
        {
            ("F1 · 锻造房", "ForgeRoofPost", "WorkshopForgeRoof"),
            ("F2 · 炭棚", "ShedRoofPost", "WorkshopShedRoof")
        };

        [MenuItem("Tools/NarutoVR/第二章断衡悬道/安装工坊屋顶双射手")]
        public static void Install() => InstallPosts(SceneManager.GetSceneByPath(ScenePath));

        /// <summary>Returns the placed scene enemies in forge/shed order, including previously installed posts.</summary>
        public static EnemyController[] InstallPosts(Scene scene)
        {
            if (!scene.isLoaded || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("请在编辑模式打开第二章正式场景。");

            var nodes = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<Transform>(true)).ToArray();
            // Resolve both buildings and assets before changing the scene.
            var placements = Posts.Select(post =>
            {
                var parent = nodes.Single(t => t.name == post.building);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{Folder}/{post.asset}.prefab");
                int area = NavMesh.GetAreaFromName(post.area);
                var agent = prefab ? prefab.GetComponentInChildren<NavMeshAgent>(true) : null;
                if (!agent || area < 0 || agent.areaMask != (1 << area))
                    throw new InvalidOperationException($"请检查 {post.asset} 和导航区域 {post.area}。");
                var existing = parent.Cast<Transform>().SingleOrDefault(child =>
                    PrefabUtility.GetCorrespondingObjectFromSource(child.gameObject) == prefab);
                return (parent, prefab, existing);
            }).ToArray();

            return placements.Select(placement =>
            {
                if (placement.existing)
                    return placement.existing.GetComponentsInChildren<EnemyController>(true).Single();
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(placement.prefab, placement.parent);
                instance.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                Undo.RegisterCreatedObjectUndo(instance, "安装工坊屋顶射手");
                EditorSceneManager.MarkSceneDirty(scene);
                return instance.GetComponentsInChildren<EnemyController>(true).Single();
            }).ToArray();
        }
    }
}
