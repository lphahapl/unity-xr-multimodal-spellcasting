using System;
using System.Linq;
using NeuGameMRTools.Runtime.Components;
using NarutoVR.SealCombat.Items;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NarutoVR.SealCombat.Editor
{
    public static class InkRubbingLabBuilder
    {
        const string Root = "Assets/Samples/ToolBeltItems/";
        public const string ScenePath = Root + "InkRubbingLab.unity";

        public static void Build()
        {
            var scene = EditorSceneManager.OpenScene(Root + "ItemInteractionLab.unity");
            if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new InvalidOperationException("Cannot create isolated sample.");
            var lab = Object.FindFirstObjectByType<ItemLabControls>();
            foreach (var item in lab.items.Where(i => i && i.Kind != GameItemKind.GlyphPen))
                item.gameObject.SetActive(false);
            var pen = lab.items.Single(i => i && i.Kind == GameItemKind.GlyphPen);
            var ink = Create<PineInkPrototype>("松墨锭（独立灰盒）", "PineInkItem", lab, 2, new Vector3(.018f,.056f,.012f));
            ink.interaction = lab;
            ink.recovery = AssetDatabase.LoadAssetAtPath<RestoreDuLiItemEffectSO>(Root+"RestoreMana.asset");
            ink.testRecoveryOnBreak = false;
            var wood = Load("Bronze"); var black = Load("Charcoal");
            Part(ink.transform, "下托", new Vector3(0,-.020f,0), new Vector3(.018f,.014f,.012f), wood);
            Part(ink.transform, "套背", new Vector3(0,.002f,.0053f), new Vector3(.018f,.049f,.0014f), wood);
            ink.segments = new Transform[2];
            for (int i=0;i<2;i++) ink.segments[i] = Part(ink.transform, "预分墨段 "+(i+1),
                new Vector3(0,-.008f+i*.019f,0), new Vector3(.014f,.018f,.008f),black);
            ink.sleeve = Part(ink.transform,"推拉盖",new Vector3(0,.002f,-.0054f),new Vector3(.018f,.049f,.0012f),wood);
            var plate = Create<GameItemBase>("冷铸拓片（独立灰盒）","BronzeRubbingItem",lab,3,new Vector3(.030f,.045f,.002f));
            Part(plate.transform,"冷铸薄板",Vector3.zero,new Vector3(.030f,.045f,.002f),wood);
            lab.items = new GameItemBase[] { ink, pen, plate };
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Cannot save greybox.");
        }

        static T Create<T>(string name,string config,ToolBeltInteraction belt,int slot,Vector3 size) where T : GameItemBase
        {
            var obj = new GameObject(name);
            var box = obj.AddComponent<BoxCollider>();
            box.size = size;
            var item = obj.AddComponent<T>();
            item.definition = AssetDatabase.LoadAssetAtPath<GameItemConfigSO>(Root+config+".asset");
            item.home = belt.slots[slot];
            item.stowEnvelope = box;
            item.grip = obj.transform;
            var opening = ((ToolBeltSlot)item.home).opening;
            obj.transform.SetPositionAndRotation(opening.position,opening.rotation);
            return item;
        }

        static Material Load(string id) => AssetDatabase.LoadAssetAtPath<Material>(Root+id+".mat");
        static Transform Part(Transform parent,string name,Vector3 pos,Vector3 size,Material mat)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name=name;
            obj.transform.SetParent(parent,false);
            obj.transform.localPosition=pos;
            obj.transform.localScale=size;
            Object.DestroyImmediate(obj.GetComponent<Collider>());
            obj.GetComponent<Renderer>().sharedMaterial=mat;
            return obj.transform;
        }
    }
}
