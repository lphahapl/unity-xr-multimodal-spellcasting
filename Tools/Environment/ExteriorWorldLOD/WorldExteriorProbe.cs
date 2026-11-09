using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class WorldExteriorProbe
{
    public static string PathOf(Transform t)=>t.parent?PathOf(t.parent)+"/"+t.name:t.name;
    public static void Run()
    {
        var scene=EditorSceneManager.OpenScene("Assets/Scene/Chapter01_Awakening.unity");
        var log="D:/UnityProjects/NarutoVR/Logs/exterior-world-lod-20261109";
        var all=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
        File.WriteAllLines(log+"/colliders.txt",all.SelectMany(t=>t.GetComponents<Collider>()).Select(c=>c.enabled+"|"+c.gameObject.activeInHierarchy+"|"+c.GetType().Name+"|"+PathOf(c.transform)+"|"+c.bounds.ToString("F3")+"|trigger="+c.isTrigger));
        File.WriteAllLines(log+"/renderers.txt",all.SelectMany(t=>t.GetComponents<Renderer>()).Where(r=>r.enabled&&r.gameObject.activeInHierarchy).Select(r=>PathOf(r.transform)+"|"+r.bounds.ToString("F3")));
        File.WriteAllLines(log+"/roots.txt",scene.GetRootGameObjects().Select(g=>g.name+"|"+g.transform.position+"|"+g.transform.lossyScale));
        EditorApplication.Exit(0);
    }
}
