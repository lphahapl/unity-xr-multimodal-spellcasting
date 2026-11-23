using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using NarutoVR.SealCombat.Rpg;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class GaugeA1Audit
{
    public static readonly string[] Scenes = { "Chapter01_Courtyard", "Chapter01_Awakening" };
    public static string Output => Environment.GetEnvironmentVariable("A1_LOG") ?? "Logs/ConcentricGaugeA1";
    public static OpeningLeafPuzzle Open(string name)
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scene/" + name + ".unity");
        return scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<OpeningLeafPuzzle>(true)).Single();
    }

    public static string PathOf(Transform node) => node.parent ? PathOf(node.parent) + "/" + node.name : node.name;

    public static void Baseline()
    {
        Directory.CreateDirectory(Output);
        foreach (string scene in Scenes)
        {
            var p = Open(scene);
            var axis = p.outerDisc.parent;
            var rows = new List<string>();
            foreach (var t in new[] { axis, p.outerDisc, p.innerDisc, p.outerHandle, p.innerHandle, p.prizeRest,
                                       p.verifier, p.slotEntry, p.leaf.transform, p.lid.transform })
                rows.Add($"Anchor\t{PathOf(t)}\tworld {t.position:F5}\tlocal {t.localPosition:F5}\tscale {t.lossyScale:F5}\trotation {t.localEulerAngles:F2}");
            foreach (var root in axis.gameObject.scene.GetRootGameObjects())
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                var sep = axis.InverseTransformPoint(r.bounds.ClosestPoint(axis.position));
                if (Mathf.Abs(sep.x) > 1.9f || Mathf.Abs(sep.y) > 1.35f || Mathf.Abs(sep.z) > 1.0f) continue;
                var mf = r.GetComponent<MeshFilter>();
                rows.Add($"{GlobalObjectId.GetGlobalObjectIdSlow(r)}\t{r.enabled}/{r.gameObject.activeInHierarchy}\t{PathOf(r.transform)}\tcenter {axis.InverseTransformPoint(r.bounds.center):F3}\tsize {r.bounds.size:F3}\t{(mf ? AssetDatabase.GetAssetPath(mf.sharedMesh) : "")}");
            }
            File.WriteAllLines(Path.Combine(Output, scene + "-baseline.tsv"), rows);
        }
    }
}
