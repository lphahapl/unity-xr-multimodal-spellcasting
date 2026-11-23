using System;
using System.IO;
using System.Linq;
using NarutoVR.SealCombat.Rpg;
using Oculus.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NarutoVR.Editor
{
    internal static class TrackedAvatarBuilder
    {
        const string Menu = "Tools/玩家手/从人物 Prefab 生成人物跟手变体";

        [MenuItem(Menu, true)]
        static bool CanBuild() => Selection.activeObject is GameObject go && EditorUtility.IsPersistent(go);

        [MenuItem(Menu)]
        static void BuildSelected()
        {
            var model = (GameObject)Selection.activeObject;
            string path = EditorUtility.SaveFilePanelInProject("保存人物跟手变体", model.name + "Tracked",
                "prefab", "请选择新的输出路径");
            if (!string.IsNullOrEmpty(path)) Selection.activeObject = Build(model, path);
        }

        [MenuItem("Tools/玩家手/在当前首章装配所选跟手人物与 V2 双手")]
        static void InstallSelected() => Selection.activeGameObject = Install(
            SceneManager.GetActiveScene(), Selection.activeObject as GameObject);

        internal static GameObject Install(Scene scene, GameObject variant)
        {
            if (Application.isPlaying || (scene.path != "Assets/Scene/Chapter01_Courtyard.unity" &&
                scene.path != "Assets/Scene/Chapter01_Awakening.unity"))
                throw new InvalidOperationException("请在编辑模式打开首章院落或苏醒场景。");
            if (!variant || !EditorUtility.IsPersistent(variant) || !variant.GetComponent<TrackedAvatarPresentation>())
                throw new ArgumentException("请在 Project 中选择由人物跟手工具生成的 Prefab。");
            var roots = scene.GetRootGameObjects();
            var player = roots.SelectMany(r => r.GetComponentsInChildren<LinearRpgController>(true)).Single();
            var prior = roots.SelectMany(r => r.GetComponentsInChildren<TrackedAvatarPresentation>(true))
                .SingleOrDefault(p => p.player == player);
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("装配首章玩家手与人物");
            try
            {
                GameObject Hand(string side) => AssetDatabase.LoadAssetAtPath<GameObject>(
                    PlayerHandRig.Art + "Player" + side + "Hand.prefab");
                PlayerHandAssembly.Install(scene, Hand("Left"), Hand("Right"));
                var observer = prior ? prior.spectator : null;
                if (prior && PrefabUtility.GetCorrespondingObjectFromSource(prior.gameObject) != variant)
                {
                    Undo.DestroyObjectImmediate(prior.gameObject);
                    prior = null;
                }
                if (!prior)
                {
                    var obj = (GameObject)PrefabUtility.InstantiatePrefab(variant, scene);
                    Undo.RegisterCreatedObjectUndo(obj, "加入跟手人物");
                    prior = obj.GetComponent<TrackedAvatarPresentation>();
                }
                Undo.RecordObject(prior, "绑定本地玩家");
                var driver = prior.GetComponent<TrackedAvatarArms>();
                Undo.RecordObject(driver, "绑定现有手部采样");
                prior.player = player;
                prior.spectator = observer;
                prior.BindPlayerVisuals();
                PrefabUtility.RecordPrefabInstancePropertyModifications(prior);
                PrefabUtility.RecordPrefabInstancePropertyModifications(driver);
                EditorSceneManager.MarkSceneDirty(scene);
                Undo.CollapseUndoOperations(group);
                return prior.gameObject;
            }
            catch { Undo.RevertAllDownToGroup(group); throw; }
        }

        internal static GameObject Build(GameObject model, string output)
        {
            if (!model || !EditorUtility.IsPersistent(model) || model.GetComponentInChildren<TrackedAvatarArms>(true))
                throw new ArgumentException("请选择未装配跟手组件的 Humanoid 人物主资产。");
            if (!output.StartsWith("Assets/", StringComparison.Ordinal) || Path.GetExtension(output) != ".prefab" ||
                File.Exists(output)) throw new ArgumentException("请选择 Assets 中尚未占用的 .prefab 路径。");
            string bodyPath=Path.ChangeExtension(output,null)+"Body.asset";
            if(File.Exists(bodyPath)) throw new ArgumentException("第一人称身体网格的输出路径已占用 "+bodyPath);
            Scene preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, preview);
                var animators = instance.GetComponentsInChildren<Animator>(true);
                if (animators.Length != 1 || !animators[0].avatar || !animators[0].avatar.isValid || !animators[0].isHuman)
                    throw new InvalidOperationException("人物需要一个具有有效 Humanoid Avatar 的 Animator。");
                var animator = animators[0];
                animator.applyRootMotion = false;
                PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
                var driver = instance.AddComponent<TrackedAvatarArms>();
                var display=instance.AddComponent<TrackedAvatarPresentation>();
                Calibrate(driver.Side(true), animator, true, preview);
                Calibrate(driver.Side(false), animator, false, preview);
                display.firstPersonSkin=TrackedAvatarBodyMesh.Build(instance,animator,driver,bodyPath);
                var asset = PrefabUtility.SaveAsPrefabAsset(instance, output, out bool saved);
                if (!saved || !asset) throw new IOException("人物变体保存失败 " + output);
                return asset;
            }
            catch
            {
                if(AssetDatabase.LoadMainAssetAtPath(bodyPath)) AssetDatabase.DeleteAsset(bodyPath);
                throw;
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }

        static void Calibrate(TrackedAvatarArms.Arm arm, Animator animator, bool left, Scene preview)
        {
            string side = left ? "Left" : "Right";
            var template = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerHandRig.Visual(side));
            var hand = ((GameObject)PrefabUtility.InstantiatePrefab(template, preview)).GetComponent<HandVisual>();
            Transform Bone(HumanBodyBones id)
            {
                var bone = animator.GetBoneTransform(id);
                return bone ? bone : throw new InvalidOperationException("Avatar 缺少骨骼 " + id);
            }

            arm.upper = Bone(left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm);
            arm.lower = Bone(left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm);
            arm.targets[0] = Bone(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            var reference = new Transform[16];
            int firstFinger = (int)(left ? HumanBodyBones.LeftThumbProximal : HumanBodyBones.RightThumbProximal);
            for (int i = 0; i < reference.Length; ++i)
            {
                reference[i] = hand.GetTransformByHandJointId(TrackedAvatarArms.Joint(i));
                if (!reference[i]) throw new InvalidOperationException("SDK 模板缺少关节 " + TrackedAvatarArms.Joint(i));
                if (i != 0) arm.targets[i] = Bone((HumanBodyBones)(firstFinger + i - 1));
                Transform parent = i == 0 ? arm.lower : i % 3 == 1 ? arm.targets[0] : arm.targets[i - 1];
                if (!arm.targets[i].IsChildOf(parent) || arm.targets[i] == parent)
                    throw new InvalidOperationException("请检查该指节或腕骨的 Avatar 父子映射 " + arm.targets[i].name);
            }
            Quaternion srcPalm = Palm(reference), dstPalm = Palm(arm.targets);
            for (int i = 0; i < reference.Length; ++i)
            {
                // A common palm frame preserves THIS character's neutral finger spread, thumb
                // opposition and bone roll. Source and target may face different world axes.
                arm.axisOffsets[i] = Quaternion.Inverse(reference[i].rotation) * srcPalm *
                    Quaternion.Inverse(dstPalm) * arm.targets[i].rotation;
            }
        }

        static Quaternion Palm(Transform[] bones)
        {
            Vector3 index = bones[4].position - bones[0].position;
            Vector3 little = bones[13].position - bones[0].position;
            Vector3 normal = Vector3.Cross(index, little);
            if (normal.sqrMagnitude < 1e-10f || (index + little).sqrMagnitude < 1e-8f)
                throw new InvalidOperationException("食指根、小指根与腕部无法确定掌面。");
            return Quaternion.LookRotation(index + little, normal);
        }
    }
}
