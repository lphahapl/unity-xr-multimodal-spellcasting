using NarutoVR.SealLab;
using UnityEditor;
using UnityEngine;

namespace NarutoVR.SealCombat.Editor
{
    public static class SealConfirmationPreview
    {
        const string Menu = "Assets/NarutoVR/预览成印反馈";

        [MenuItem(Menu, true)]
        static bool CanPreview() =>
            Selection.activeObject is SealProfile profile && profile.confirmationPrefab;

        [MenuItem(Menu)]
        static void Preview()
        {
            var camera = Camera.main;
            if (!EditorApplication.isPlaying || !camera)
            {
                EditorUtility.DisplayDialog("预览成印反馈", "先在带有 MainCamera 的场景进入 Play，再预览所选印。", "确定");
                return;
            }
            var profile = (SealProfile)Selection.activeObject;
            var eye = camera.transform;
            Object.Instantiate(profile.confirmationPrefab, eye.TransformPoint(0, -.035f, .5f),
                eye.rotation, new InstantiateParameters { scene = camera.gameObject.scene });
        }
    }
}
