using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NarutoVR.SealCombat.Rpg;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class GaugeA1Scene
{
    const string ExistingArt = "A1 art already present";
    static void RequireUnwired(OpeningLeafPuzzle puzzle)
    {
        var court = puzzle.outerDisc.parent.parent;
        foreach (var node in court.GetComponentsInChildren<Transform>(true))
        {
            var source = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(node);
            if ((!string.IsNullOrEmpty(source) && source.StartsWith(GaugeA1Art.Root+"/Prefabs/A1_",StringComparison.Ordinal)) ||
                GaugeA1Art.Parts.Any(part=>node.name=="A1_"+part))
                throw new InvalidOperationException($"{ExistingArt} in {puzzle.gameObject.scene.name}: {GaugeA1Audit.PathOf(node)}. Stage requires a pre-integration baseline.");
        }
    }
    public static void BuildAndStage()
    {
        var rig = AssetDatabase.LoadAssetAtPath<GameObject>("Packages/com.meta.xr.sdk.interaction/Runtime/UnityXR/Prefabs/XROriginCameraRig.prefab");
        if (!rig) throw new Exception("Missing XR camera rig in the isolated package cache");
        GaugeA1Art.RefreshAll();
        Stage();
    }
    // Outputs separate staged files; transfer just the instances and renderer flags.
    public static void Stage()
    {
        // Check both scenes before staging either one; any partial installation is a conflict.
        foreach (string name in GaugeA1Audit.Scenes) RequireUnwired(GaugeA1Audit.Open(name));
        foreach (string sceneName in GaugeA1Audit.Scenes)
        {
            var p = GaugeA1Audit.Open(sceneName);
            var axis = p.outerDisc.parent;
            var court = axis.parent;
            var edits = new List<string>();
            void Visible(Renderer renderer, bool visible)
            {
                if (renderer.enabled == visible) return;
                var id = GlobalObjectId.GetGlobalObjectIdSlow(renderer);
                edits.Add($"{id.targetObjectId}\t{(visible ? 0 : 1)}\t{(visible ? 1 : 0)}\t{GaugeA1Audit.PathOf(renderer.transform)}");
                renderer.enabled = visible;
            }
            Visible(court.Find("静态合并_CourtDesk").GetComponent<MeshRenderer>(), false);
            Visible(court.Find("通往双盘的白石嵌线").GetComponent<MeshRenderer>(), true);
            foreach (var disc in new[] {p.outerDisc,p.innerDisc})
            foreach (var renderer in disc.GetComponentsInChildren<MeshRenderer>(true))
                Visible(renderer, false);
            Visible(p.lid.GetComponent<MeshRenderer>(), false);

            void Place(string part, Transform parent, Vector3 pos, float yaw=0)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GaugeA1Art.Root + "/Prefabs/A1_" + part + ".prefab");
                if (!prefab) throw new Exception("Missing prefab for " + part);
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab,parent);
                go.transform.localPosition = pos;
                go.transform.localRotation = Quaternion.Euler(0,yaw,0);
                Vector3 scale = parent.lossyScale;
                go.transform.localScale = new Vector3(1/scale.x,1/scale.y,1/scale.z);
                PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
            }
            var box = court.InverseTransformPoint(p.prizeRest.position);
            var tablet = court.Find("线索石刻").localPosition;
            var bench = court.Find("校规台石座").localPosition;
            Place("Bench", court, new Vector3(bench.x,0,bench.z));
            Place("Stele", court, new Vector3(tablet.x,0,tablet.z));
            Place("Plinth", court, new Vector3(box.x,0,box.z));
            Place("Bed", axis, Vector3.zero);
            Place("Outer", p.outerDisc, Vector3.zero);
            Place("Inner", p.innerDisc, Vector3.zero);
            Place("Box", court, new Vector3(box.x,.9f,box.z));
            Place("Lid", p.lid.transform, Vector3.zero);
            if (!EditorSceneManager.SaveScene(p.gameObject.scene,"Assets/Scene/A1Staged_" + sceneName + ".unity",true))
                throw new Exception("Cannot serialize staged " + sceneName);
            File.WriteAllLines(Path.Combine(GaugeA1Audit.Output,sceneName + "-renderer-edits.tsv"),edits);
        }
    }

    public static void CheckRepeatStage()
    {
        var lines = new List<string>();
        void ExpectRefusal(Action run)
        {
            try { run(); }
            catch (InvalidOperationException ex) when (ex.Message.StartsWith(ExistingArt,StringComparison.Ordinal))
            {
                lines.Add("PASS rejected before writes: " + ex.Message);
                return;
            }
            throw new Exception("Repeat stage was accepted.");
        }
        foreach (string name in GaugeA1Audit.Scenes)
        {
            var p=GaugeA1Audit.Open(name);
            ExpectRefusal(()=>RequireUnwired(p));
        }
        ExpectRefusal(Stage);
        GaugeA1Geometry.Verify();
        File.WriteAllLines(Path.Combine(GaugeA1Audit.Output,"repeat-stage.txt"),lines);
    }
}
