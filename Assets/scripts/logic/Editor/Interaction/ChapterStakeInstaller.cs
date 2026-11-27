using System;
using System.Linq;
using NarutoVR.SealCombat.Items;
using NarutoVR.SealCombat.Rpg;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NarutoVR.SealCombat.Editor
{
    public static class ChapterStakeInstaller
    {
        public const string ScenePath="Assets/Scene/Chapter02_SuspendedPath.unity";
        public const string PrefabPath="Assets/Samples/ToolBeltItems/ChapterStakeGraybox.prefab";

        [MenuItem("NarutoVR/第二章/接入定律铜橛领取入口")]
        public static void ConfigureActive() => Configure(SceneManager.GetActiveScene());

        public static ChapterStakeSupply Configure(Scene scene)
        {
            if (scene.path != ScenePath) throw new InvalidOperationException("请先打开正式二章或其独立工程副本。");
            var lesson=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<Chapter02BridgeEquipment>(true)).Single();
            var belt=lesson.equipment.interaction;
            var pocket=belt.slots.OfType<ToolBeltSlot>().Single(x=>x.opening && x.opening.name=="TallSleeve");
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)?.GetComponent<CooldownStake>();
            if(!prefab || !prefab.stowEnvelope || pocket.penHolster) throw new InvalidOperationException("缺少匹配的二章铜橛或窄鞘。");
            var supply=lesson.player.GetComponent<ChapterStakeSupply>();
            if(supply)
            {
                if(supply.belt!=belt || supply.pocket!=pocket || !supply.prefab)
                    throw new InvalidOperationException("已有铜橛接线与二章工具带不一致。");
                return supply;
            }
            supply=Undo.AddComponent<ChapterStakeSupply>(lesson.player.gameObject);
            supply.belt=belt;supply.pocket=pocket;supply.prefab=prefab;
            supply.worldFallBoundary=lesson.equipment.pen.worldFallBoundary;
            PrefabUtility.RecordPrefabInstancePropertyModifications(supply);
            EditorSceneManager.MarkSceneDirty(scene);
            return supply;
        }
    }
}
