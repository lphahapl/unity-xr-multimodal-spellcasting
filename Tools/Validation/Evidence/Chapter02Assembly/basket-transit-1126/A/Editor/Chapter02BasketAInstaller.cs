using System;
using System.IO;
using System.Linq;
using NarutoVR.SealCombat.Enemies;
using NarutoVR.SealCombat.Rpg;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace NarutoVR.SealCombat.Editor
{
    public static class Chapter02BasketAInstaller
    {
        const string ScenePath = "Assets/Scene/Chapter02_SuspendedPath.unity";

        public static void Install()
        {
            if (!Application.isBatchMode || !Application.dataPath.Replace('\\', '/').StartsWith("D:/Temp/Ch02", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("只允许在二章隔离工程装配");
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var nodes = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
            Transform Named(string name) => nodes.Single(t => t.name == name);
            var player = nodes.Select(t => t.GetComponent<LinearRpgController>()).Single(c => c);
            var stoneBasket = nodes.Select(t => t.GetComponent<BallastBasket>()).Single(c => c);
            var beam = Named("双臂衡梁 · 120 m");
            if (nodes.Any(t => t.GetComponent<Chapter02BasketMotion>()))
                throw new InvalidOperationException("该副本已装好吊篮");
            var motion = beam.gameObject.AddComponent<Chapter02BasketMotion>();
            motion.player = player;
            motion.ballast = stoneBasket;
            motion.carrierProfile = AssetDatabase.LoadAssetAtPath<StoneCarrierProfile>("Assets/ScriptableObjects/敌人战斗配置/StoneCarrierProfile.asset");
            if (!motion.carrierProfile) throw new InvalidOperationException("缺少原守备工奴镇石配置");
            motion.beam = beam;
            motion.tipA = Named("A 梁端吊点");
            motion.tipB = Named("B 梁端吊点");
            motion.basketB = Named("B · 谷底吊篮");
            var deck = motion.basketB.GetComponents<BoxCollider>().Single(c => !c.isTrigger && c.size.y < .2f && c.size.x > 3f);
            var passenger = motion.basketB.gameObject.AddComponent<BoxCollider>();
            passenger.isTrigger = true;
            passenger.size = new Vector3(deck.size.x, stoneBasket.passengerVolume.size.y, deck.size.z);
            passenger.center = deck.center + Vector3.up * ((deck.size.y + passenger.size.y) * .5f);
            motion.passengerB = passenger;
            motion.comfortCurtain = CreateCurtain(player.locomotion.head);
            var sill = Named("A 中层篮口");
            var landing = new GameObject("西接驳栈道 · A 篮落稳存档").transform;
            landing.SetParent(sill, false);
            var floor = sill.position + Vector3.right * 1.5f;
            if (!Physics.Raycast(floor + Vector3.up * .55f, Vector3.down, out var hit, .8f, ~0, QueryTriggerInteraction.Ignore) ||
                hit.normal.y < .92f || Mathf.Abs(hit.point.y - sill.position.y) > .15f)
                throw new InvalidOperationException("A 篮外存档缺少同高静态栈道碰撞 " + floor);
            landing.position = hit.point;
            var checkpoint = landing.gameObject.AddComponent<NavigationNode>();
            checkpoint.nodeId = "CH02_A_MIDDLE";
            motion.middleCheckpoint = checkpoint;
            var index = Array.FindIndex(player.route, row => row.stage.stageId == "CH02_REACH_HUB");
            if (index < 0) throw new InvalidOperationException("缺少抵达中控阶段");
            player.route[index].alternateCheckpoint = checkpoint;
            EditorUtility.SetDirty(player);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("保存 A 副本失败");
            Debug.Log($"CH02_A_INSTALLED A={stoneBasket.name} B={deck.name} mid={landing.position:F3} floor={hit.collider.name} view={player.locomotion.head.name}");
        }

        static GameObject CreateCurtain(Transform head)
        {
            var go = new GameObject("乘篮定目帘", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            var rect = (RectTransform)go.transform;
            rect.SetParent(head, false);
            rect.localPosition = new Vector3(0, 0, .65f);
            rect.localScale = Vector3.one * .001f;
            rect.sizeDelta = new Vector2(3200, 2800);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 120;
            var group = go.GetComponent<CanvasGroup>();
            group.alpha = 0;
            group.interactable = false;
            group.blocksRaycasts = false;
            Graphic(rect, "遮景", new Color(.075f, .082f, .083f, 1f), rect.sizeDelta);
            var mark = Graphic(rect, "定目铜纹", new Color(.50f, .43f, .31f, 1f), new Vector2(6,6));
            mark.rectTransform.localRotation = Quaternion.Euler(0,0,45);
            go.SetActive(false);
            return go;
        }

        static Image Graphic(RectTransform parent, string label, Color tint, Vector2 size)
        {
            var node = new GameObject(label, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var image = node.GetComponent<Image>();
            image.rectTransform.SetParent(parent, false);
            image.rectTransform.sizeDelta = size;
            image.color = tint;
            image.raycastTarget = false;
            return image;
        }
    }
}
