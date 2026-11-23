using System.Collections.Generic;
using System.IO;
using System.Linq;
using NarutoVR.SealCombat.Rpg;
using NarutoVR.SealLab;
using Oculus.Interaction;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Lab = NarutoVR.SealLab.SealLab;
using Object = UnityEngine.Object;

namespace NarutoVR.Editor
{
    [InitializeOnLoad]
    internal static class ShenHandPreview
    {
        const string MenuPath = "Tools/NarutoVR/玩家手/试戴沈商 V3";
        const string Setting = "UserSettings/ShenHandV3Preview.enabled";
        const string OfficialSetting = "UserSettings/MetaOfficialHandsPreview.enabled";
        static readonly List<Preview> previews = new();

        static ShenHandPreview()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode) ApplyToLoadedScenes();
                if (state == PlayModeStateChange.ExitingPlayMode) RestoreAll();
            };
            SceneManager.sceneLoaded += (scene, _) => Apply(scene);
            AssemblyReloadEvents.beforeAssemblyReload += RestoreAll;
            EditorApplication.delayCall += ApplyToLoadedScenes;
        }

        [MenuItem(MenuPath)]
        static void Toggle()
        {
            RestoreAll();
            if (File.Exists(Setting))
            {
                File.Delete(Setting);
                File.WriteAllText(OfficialSetting, "Meta SDK");
            }
            else
            {
                Directory.CreateDirectory("UserSettings");
                File.Delete(OfficialSetting);
                File.WriteAllText(Setting, "V3");
            }
            ApplyToLoadedScenes();
        }

        [MenuItem(MenuPath, true)]
        static bool ShowSelection()
        {
            Menu.SetChecked(MenuPath, File.Exists(Setting));
            return true;
        }

        static void ApplyToLoadedScenes()
        {
            if (!EditorApplication.isPlaying) return;
            for (int i = 0; i < SceneManager.sceneCount; i++) Apply(SceneManager.GetSceneAt(i));
        }

        static void Apply(Scene scene)
        {
            bool useV3 = File.Exists(Setting);
            if (!EditorApplication.isPlaying || !scene.isLoaded || (!useV3 && !File.Exists(OfficialSetting))) return;
            if (scene.path != "Assets/Scene/Chapter01_Courtyard.unity" && scene.path != "Assets/Scene/Chapter01_Awakening.unity" &&
                scene.path != "Assets/Scene/SealLabSteamLink.unity" && scene.path != "Assets/Scene/SkillTest.unity" &&
                scene.path != "Assets/Scene/SealRecognitionTest.unity") return;

            previews.RemoveAll(preview => !preview.Lab);
            var roots = scene.GetRootGameObjects();
            foreach (var lab in roots.SelectMany(root => root.GetComponentsInChildren<Lab>(true)))
            {
                if (!lab.xrHandsSource || !lab.xrHandsSource.IsConfigured || previews.Any(p => p.Lab == lab)) continue;
                if (!useV3)
                {
                    var hands = lab.xrHandsSource.GetComponentsInChildren<HandVisual>(true);
                    bool Installed(string side) => hands.Any(v =>
                        PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(v.gameObject) == PlayerHandRig.Visual(side));
                    if (Installed("Left") && Installed("Right")) continue;
                }
                var player = roots.SelectMany(root => root.GetComponentsInChildren<LinearRpgController>(true))
                    .FirstOrDefault(p => p.handTracking == lab || (p.skills && p.skills.lab == lab));
                var preview = new Preview(lab, player, useV3);
                previews.Add(preview);
                try { preview.Install(); }
                catch
                {
                    preview.Restore();
                    previews.Remove(preview);
                    throw;
                }
            }
        }

        static void RestoreAll()
        {
            foreach (var preview in previews) preview.Restore();
            previews.Clear();
        }

        sealed class Preview
        {
            internal readonly Lab Lab;
            readonly LinearRpgController player;
            readonly bool useV3;
            readonly SealLabView view;
            readonly bool boneLines;
            readonly WristMenuController menu;
            readonly WatchMount previousMount;
            readonly Transform deviceParent;
            readonly List<SkinnedMeshRenderer> hidden = new();
            GameObject left, right;

            internal Preview(Lab lab, LinearRpgController owner, bool useV3)
            {
                this.useV3 = useV3;
                Lab = lab;
                player = owner;
                view = lab.GetComponent<SealLabView>();
                boneLines = view && view.showBoneLines;
                menu = owner ? owner.wristMenu : null;
                previousMount = menu ? menu.playerWatchMount : null;
                deviceParent = menu && menu.wornDevice ? menu.wornDevice.transform.parent : null;
            }

            internal void Install()
            {
                var input = Lab.xrHandsSource;
                foreach (var visual in input.GetComponentsInChildren<HandVisual>(true))
                {
                    if (!ReferenceEquals(visual.Hand, input.leftHand) && !ReferenceEquals(visual.Hand, input.rightHand)) continue;
                    foreach (var mesh in visual.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                        if (!mesh.forceRenderingOff) { mesh.forceRenderingOff = true; hidden.Add(mesh); }
                }
                left = Attach("Left", "左手", input.leftHand);
                right = Attach("Right", "右手", input.rightHand);
                if (view) view.showBoneLines = false;
                if (useV3 && menu && menu.wornDevice)
                {
                    var mount = left.GetComponentInChildren<WatchMount>(true);
                    if (!mount) throw new InvalidDataException("V3 左手缺少准望匣佩戴位");
                    if (mount.gaze && player.locomotion) mount.gaze.viewer = player.locomotion.head;
                    menu.playerWatchMount = mount;
                }
                string meshPath(GameObject hand) => string.Join(", ", hand.GetComponentsInChildren<SkinnedMeshRenderer>()
                    .Where(mesh => mesh.sharedMesh).Select(mesh => AssetDatabase.GetAssetPath(mesh.sharedMesh)));
                Debug.Log($"[玩家手] {(useV3 ? "沈商 V3" : "Meta 官方手")} {Lab.gameObject.scene.name} | {meshPath(left)} | {meshPath(right)}", Lab);
            }

            GameObject Attach(string side, string label, Oculus.Interaction.Input.Hand hand)
            {
                string path = useV3 ? PlayerHandRig.Art + "V3/Player" + side + "Hand.prefab" : PlayerHandRig.Visual(side);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (!prefab || !prefab.TryGetComponent<HandVisual>(out var template))
                    throw new FileNotFoundException("试戴手模缺少 HandVisual", path);
                var visual = Object.Instantiate(template, Lab.xrHandsSource.transform, false);
                visual.name = (useV3 ? "沈商 V3 " : "Meta 官方") + label;
                visual.InjectHand(hand);
                return visual.gameObject;
            }

            internal void Restore()
            {
                if (menu && left)
                {
                    if (menu.wornDevice && menu.wornDevice.transform.IsChildOf(left.transform))
                        menu.wornDevice.transform.SetParent(deviceParent, true);
                    menu.playerWatchMount = previousMount;
                }
                if (view) view.showBoneLines = boneLines;
                foreach (var mesh in hidden) if (mesh) mesh.forceRenderingOff = false;
                if (left) Object.DestroyImmediate(left);
                if (right) Object.DestroyImmediate(right);
            }
        }
    }
}
