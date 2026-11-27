using System;
using System.Linq;
using NarutoVR.SealCombat.Items;
using NarutoVR.SealCombat.Rpg;
using NeuGameMRTools.Runtime.Components;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NarutoVR.SealCombat.Editor
{
    public static class ChapterRationInstaller
    {
        const string ScenePath = "Assets/Scene/Chapter02_SuspendedPath.unity";
        const string PouchPath = "Assets/Art/Props/TravelRation/TravelRationPouch.prefab";

        public static void Install()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var lesson = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Chapter02BridgeEquipment>(true)).Single();
            var belt = lesson.equipment.interaction;
            var pocket = belt.slots.OfType<ToolBeltSlot>().Single(slot => slot.opening && slot.opening.name == "SoftPouch");
            if (!pocket || pocket.penHolster || !pocket.opening || pocket.width < .0448f || pocket.thickness < .0158f || pocket.depth < .0603f)
                throw new InvalidOperationException("The central soft pouch cannot accept the approved packed ration.");
            var item = AssetDatabase.LoadAssetAtPath<GameObject>(PouchPath).GetComponent<TravelRationPouch>();
            if (!item || !item.servingPrefab) throw new InvalidOperationException(PouchPath);
            var owner = lesson.player;
            var supply = owner.GetComponent<ChapterRationSupply>();
            if (supply)
            {
                if (supply.pouchPrefab != item || supply.centralPocket != pocket || !supply.interaction ||
                    supply.interaction.player != owner || supply.interaction.sharedBelt != belt ||
                    !supply.interaction.mouth || supply.interaction.mouth.transform.parent != owner.locomotion.head)
                    throw new InvalidOperationException("Existing chapter ration wiring differs; inspect before merging.");
                return;
            }
            var grab = owner.gameObject.AddComponent<TravelRationInteraction>();
            var point = new GameObject("干粮口部判定");
            point.transform.SetParent(owner.locomotion.head, false);
            point.transform.localPosition = grab.mouthOffset;
            var snap = point.AddComponent<SnapZone>();
            var opts = new SerializedObject(snap);
            opts.FindProperty("_requireNameMatch").boolValue = false;
            opts.FindProperty("_followZoneDuringSnap").boolValue = true;
            opts.FindProperty("_snapDuration").floatValue = .18f;
            opts.ApplyModifiedPropertiesWithoutUndo();
            var mouth = point.AddComponent<MouthConsumeZone>();
            mouth.haptics = owner.GetComponent<HapticFeedbackService>();
            var audio = point.GetComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 1f;
            grab.player = owner;
            grab.sharedBelt = belt;
            grab.mouth = mouth;
            supply = owner.gameObject.AddComponent<ChapterRationSupply>();
            supply.interaction = grab;
            supply.centralPocket = pocket;
            supply.pouchPrefab = item;
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Scene save failed.");
            Debug.Log($"Chapter ration bound to {owner.name}, central opening {pocket.opening.name}; initial award disabled.");
        }
    }
}
