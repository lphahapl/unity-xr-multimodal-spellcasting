using System;
using System.IO;
using System.Linq;
using NarutoVR.SealCombat.Rpg;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NarutoVR.SealCombat.Editor
{
    public static class Chapter02BasketBInstaller
    {
        internal const string ScenePath = "Assets/Scene/Chapter02_SuspendedPath.unity";

        public static void Install()
        {
            if (!Application.isBatchMode || !Application.dataPath.Replace('\\', '/').StartsWith("D:/Temp/Ch02", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("只允许在二章隔离工程装配");
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var nodes = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
            Transform Named(string name) => nodes.Single(t => t.name == name);
            T One<T>() where T : Component => nodes.Select(t => t.GetComponent<T>()).Single(c => c);
            var motion = One<Chapter02BasketMotion>();
            var player = One<LinearRpgController>();
            var tenon = One<WorkshopTenonObjective>();
            var flow = (ObjectiveChapterFlow)player.chapter;
            var stage = flow.stages.Single(s => s.stage.stageId == Chapter02BasketBControl.StageId);
            var signal = (SignalChapterObjective)stage.objectives.Single();
            var lever = Named("B 分控杆");
            var mesh = lever.GetComponent<MeshFilter>();
            if (!lever.GetComponent<MeshRenderer>().enabled || !mesh.sharedMesh.isReadable || mesh.sharedMesh.subMeshCount != 4 ||
                !motion.passengerB || !tenon.poweredCircuits.Contains(Named("B 分控杆 · 通电").gameObject) ||
                !player.GetComponent<Chapter02BridgeEquipment>())
                throw new InvalidOperationException("B 分控杆、原通电回路、乘员区或旧档迁移接线缺失");
            var b = lever.GetComponent<Chapter02BasketBControl>();
            if (!b) b = lever.gameObject.AddComponent<Chapter02BasketBControl>();
            b.player = player;
            b.tenon = tenon;
            b.arrival = signal;
            b.motion = motion;
            b.leverMesh = mesh;
            if (!b.leverGrip)
            {
                b.leverGrip = new GameObject("B 分控杆 · 铜握柄中心").transform;
                b.leverGrip.SetParent(lever, false);
            }
            b.leverGrip.localPosition = mesh.sharedMesh.GetSubMesh(3).bounds.center;
            var valley = Named("B 谷底篮口");
            var site = valley.position + Vector3.left * 1.75f;
            Physics.SyncTransforms();
            if (!Physics.Raycast(site + Vector3.up * .6f, Vector3.down, out var hit, .85f, ~0, QueryTriggerInteraction.Ignore) ||
                Mathf.Abs(hit.point.y - valley.position.y) > .12f || hit.normal.y < .94f ||
                hit.collider.transform.IsChildOf(motion.basketB))
                throw new InvalidOperationException("B 谷底出口缺少稳定的同高步行面 " + site);
            if (!flow.finalCheckpoint || (flow.finalCheckpoint.nodeId != "CH02_HUB" && flow.finalCheckpoint.nodeId != Chapter02BasketBControl.ValleyId))
                throw new InvalidOperationException("未知的既有终点，保留现场");
            var destination = nodes.Select(t => t.GetComponent<NavigationNode>()).SingleOrDefault(n => n && n.nodeId == Chapter02BasketBControl.ValleyId);
            if (!destination)
            {
                destination = new GameObject("谷底 · B 篮外安全检查点").AddComponent<NavigationNode>();
                destination.transform.SetParent(valley, false);
                destination.nodeId = Chapter02BasketBControl.ValleyId;
            }
            destination.transform.position = hit.point;
            flow.finalCheckpoint = destination;
            EditorUtility.SetDirty(b);
            EditorUtility.SetDirty(flow);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("B 段隔离场景保存失败");
            Debug.Log($"CH02_B_INSTALLED grip={b.leverGrip.position:F3} cold={destination.floorPosition:F3} surface={hit.collider.name} signal={signal.name}");
        }
    }
}
