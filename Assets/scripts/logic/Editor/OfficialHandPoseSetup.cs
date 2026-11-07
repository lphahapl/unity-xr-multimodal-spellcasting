#if UNITY_EDITOR
using System;
using System.Linq;
using NarutoVR.SealCombat.Rpg;
using Oculus.Interaction.PoseDetection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NarutoVR.SealCombat.Editor
{
    public static class OfficialHandPoseSetup
    {
        public const string ProfilePath = LinearRpgSceneSetup.ConfigurationRoot + "/基础手形配置/官方基础手形.asset";
        const string Defaults = "Packages/com.meta.xr.sdk.interaction/Runtime/DefaultSettings/PoseDetection/";

        public static BasicHandPoseSettings EnsureProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<BasicHandPoseSettings>(ProfilePath);
            if (!profile)
            {
                profile = ScriptableObject.CreateInstance<BasicHandPoseSettings>();
                profile.index = Load("IndexFingerFeatureStateThresholds");
                profile.middle = Load("MiddleFingerFeatureStateThresholds");
                profile.ringAndPinky = Load("DefaultFingerFeatureStateThresholds");
                string error = profile.Validate();
                if (error != null) { UnityEngine.Object.DestroyImmediate(profile); throw new Exception(error); }
                AssetDatabase.CreateAsset(profile, ProfilePath);
                AssetDatabase.SaveAssetIfDirty(profile);
            }
            if (profile.Validate() is string problem) throw new Exception(problem);
            return profile;
        }
        static FingerFeatureStateThresholds Load(string name)
        {
            var asset = AssetDatabase.LoadAssetAtPath<FingerFeatureStateThresholds>(Defaults + name + ".asset");
            if (!asset) throw new Exception("未找到当前安装版本的Meta官方阈值：" + name);
            return asset;
        }
        [MenuItem("Tools/NarutoVR/线性RPG/接入官方基础手形")]
        public static void Configure()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != LinearRpgSceneSetup.ScenePath || scene.isDirty)
                throw new Exception("需要已保存的LinearRpgTest场景，且处于编辑模式。");
            var controller = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<LinearRpgController>(true)).Single();
            controller.handPoseSettings = EnsureProfile();
            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new Exception("官方手形配置接线后保存场景失败");
        }
    }
}
#endif
