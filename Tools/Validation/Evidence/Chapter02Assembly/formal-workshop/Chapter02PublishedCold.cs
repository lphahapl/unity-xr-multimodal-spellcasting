using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Collections.Generic;
using NarutoVR.SealCombat.Rpg;
using NarutoVR.SealCombat.Enemies;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class Chapter02PublishedCold
{
    const string W = "D:/Temp/T27Release1125/";
    const string Official = "D:/UnityProjects/NarutoVR/";
    const string Target = "Assets/Scene/Chapter02_SuspendedPath.unity";
    static string Hash(string path) {
        using(var f=File.OpenRead(path)) using(var sha=SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(f)).Replace("-","").ToLowerInvariant();
    }
    static void Require(bool b,string reason){if(!b)throw new Exception(reason);}
    public static void Run() {
        var log=new List<string>();
        try {
            var s=EditorSceneManager.OpenScene(Target,OpenSceneMode.Single); var active=s;
            Require(Hash(Official+Target)==Hash(Target),"Formal scene differs from reader");
            var files=new SortedSet<string>(StringComparer.Ordinal);
            foreach(var a in AssetDatabase.GetDependencies(Target,true).Where(a=>a.StartsWith("Assets/")))files.Add(a);
            foreach(var l in File.ReadAllLines(W+"runtime-dependencies.tsv").Where(x=>!string.IsNullOrWhiteSpace(x)))files.Add(l.Split('\t')[1]);
            foreach(var asset in files) {
                Require(Hash(Official+asset)==Hash(asset),"Mismatched source "+asset);
                Require(Hash(Official+asset+".meta")==Hash(asset+".meta"),"Mismatched meta "+asset);
            }
            log.Add("productionSource="+Official+Target+"; "+files.Count+" project assets and "+files.Count+" metadata compared with the formal project; all SHA256 matched.");
string targetHash="e82f24bdef5532e0a39fc9551860a00f7de6aa9c7a15277f842b1d1c8a33681f";
                    Require(!s.isDirty && Hash(Target)==targetHash,"Live editor changed");
                    foreach(var line in File.ReadAllLines(W+"runtime-dependencies.tsv").Where(x=>!string.IsNullOrWhiteSpace(x)))
                    {
                        var c=line.Split('\t'); Require(AssetDatabase.AssetPathToGUID(c[1])==c[0] && AssetDatabase.LoadMainAssetAtPath(c[1]),"Missing asset "+c[1]);
                    }
                    bool wasActive=s==active;
                    if(SceneManager.sceneCount==1)s=EditorSceneManager.OpenScene(Target,OpenSceneMode.Single);
                    else
                    {
                        Require(EditorSceneManager.CloseScene(s,true),"Close scene failed");
                        s=EditorSceneManager.OpenScene(Target,OpenSceneMode.Additive);
                        if(wasActive)SceneManager.SetActiveScene(s);
                    }
                    var nodes=s.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
                    var obj=nodes.SelectMany(t=>t.GetComponents<WorkshopTenonObjective>()).Single();
                    var player=nodes.SelectMany(t=>t.GetComponents<LinearRpgController>()).Single();
                    int rooms=nodes.SelectMany(t=>t.GetComponents<CombatRoomEncounter>()).Count(), sockets=nodes.SelectMany(t=>t.GetComponents<WorkshopTenonSocket>()).Count();
                    var camera=nodes.SelectMany(t=>t.GetComponents<Camera>()).Single(c=>c.isActiveAndEnabled&&c.CompareTag("MainCamera"));
                    var prefab=PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(obj.tenon.gameObject);
                    var meshes=obj.tenon.GetComponentsInChildren<MeshFilter>(true);
                    Require(prefab=="Assets/ScriptableObjects/Chapter02Workshop/WhiteboxMasterTenon.prefab" && meshes.Length>0 && meshes.All(m=>m.sharedMesh),"Main tenon or mesh unresolved "+prefab);
                    var renderers=obj.tenon.GetComponentsInChildren<Renderer>(true);
                    Require(renderers.Length>0 && renderers.All(r=>r.sharedMaterials.Length>0 && r.sharedMaterials.All(m=>m)),"Whitebox material unresolved");
                    Require(obj.roof && obj.courtyard && obj.holder && obj.socket && rooms==2 && sockets==1,"Missing encounter binding");
                    foreach(var n in new[]{obj.closedJaws,obj.openJaws}) Require(n && n.transform.childCount==0 && n.GetComponentsInChildren<Renderer>(true).Length==0 && n.GetComponentsInChildren<Collider>(true).Length==0,"Old sample geometry found");
                    var stages=((ObjectiveChapterFlow)player.chapter).stages;
                    Require(stages.Length==3 && stages.All(st=>st!=null && st.stage),"Phase asset missing");
                    Require(nodes.All(t=>!PrefabUtility.IsPrefabAssetMissing(t.gameObject)),"Missing prefab in chapter");
                    var furnace=nodes.Single(t=>t.name=="锻造房正炉 · 拱膛与风道");
                    log.Add("confirmedScene="+s.path+" sha256="+Hash(Target)+" dirty="+s.isDirty);
                    log.Add("player=1 camera="+camera.name+" rooms="+rooms+" objective=1 socket="+sockets+" save="+player.saveFileName);
                    log.Add("tenon="+obj.tenon.name+" source="+prefab+" mesh="+string.Join(",",meshes.Select(m=>AssetDatabase.GetAssetPath(m.sharedMesh)))+" material="+string.Join(",",renderers.SelectMany(r=>r.sharedMaterials).Select(m=>m.name)));
                    log.Add("closedJaws/openJaws: child=0/0 renderer=0/0 collider=0/0; missingPrefabInstances=0; chapter stages="+string.Join(",",stages.Select(st=>st.stage.name)));
                    log.Add("furnace="+furnace.position.ToString("F3")+" navTriangles="+UnityEngine.AI.NavMesh.CalculateTriangulation().indices.Length/3+" socket="+obj.socket.transform.position.ToString("F3"));
                    SceneView.RepaintAll();

            log.Add("OK"); File.WriteAllLines(W+"independent-cold-final.txt",log);
        } catch(Exception e) {log.Add("FAIL "+e);File.WriteAllLines(W+"independent-cold-final.txt",log);throw;}
    }
}