using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class WorkshopTenonGeometryProbe
{
    const string Root="Assets/Review/WorkshopMasterTenonR01";
    const string Output="D:/UnityProjects/NarutoVR/Logs/workshop-master-tenon-r01-20261125/unity";
    [Serializable] public class Geometry { public Part[] parts; public Sample[] rays; }
    [Serializable] public class Part { public string name; public bool active; public int vertices, triangles; public Vector3 min,max; public Vector3[] worldVertices; public int[] indices; }
    [Serializable] public class Sample { public string name; public Vector3 from,direction; public float distance; public string collider; public Vector3 point,normal; }
    public static void Run()
    {
        try {
            Directory.CreateDirectory(Output);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/ReferenceConsole/TenonConsole.prefab");
            if(!prefab)throw new Exception("Actual console missing");
            var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab);
            var parts=new List<Part>();
            foreach(var f in go.GetComponentsInChildren<MeshFilter>(true)) {
                var m=f.sharedMesh;if(!m)continue;
                var v=m.vertices.Select(p=>f.transform.TransformPoint(p)).ToArray();
                var b=new Bounds(v[0],Vector3.zero);foreach(var p in v)b.Encapsulate(p);
                parts.Add(new Part{name=f.name,active=f.gameObject.activeInHierarchy,vertices=v.Length,triangles=m.triangles.Length/3,min=b.min,max=b.max,worldVertices=v,indices=m.triangles});
                if(f.gameObject.activeInHierarchy && f.name.Contains("LOD0")) {
                    var c=f.GetComponent<MeshCollider>();if(!c)c=f.gameObject.AddComponent<MeshCollider>();c.sharedMesh=m;
                }
            }
            Physics.queriesHitBackfaces=true;Physics.SyncTransforms();
            var rays=new List<Sample>();
            void Ray(string name,Vector3 from,Vector3 direction) {
                bool hit=Physics.Raycast(from,direction,out var r,3,~0,QueryTriggerInteraction.Ignore);
                rays.Add(new Sample{name=name,from=from,direction=direction,distance=hit?r.distance:-1,point=r.point,normal=r.normal,collider=hit?r.collider.name:"none"});
            }
            Ray("center downward",new Vector3(-.12f,1.7f,0),Vector3.down);
            foreach(float y in new[]{.86f,.90f,.92f,.94f,.96f,.98f,1f,1.02f,1.04f,1.06f,1.08f,1.10f}) {
                var c=new Vector3(-.12f,y,0); Ray($"x+ at {y}",c,Vector3.right);Ray($"x- at {y}",c,Vector3.left);Ray($"z+ at {y}",c,Vector3.forward);Ray($"z- at {y}",c,Vector3.back);
            }
            File.WriteAllText(Output+"/console-geometry.json",JsonUtility.ToJson(new Geometry{parts=parts.ToArray(),rays=rays.ToArray()},true));
            File.WriteAllText(Output+"/geometry-result.txt","PASS actual console geometry extracted\n");
            EditorApplication.Exit(0);
        } catch(Exception e) {File.WriteAllText(Output+"/geometry-result.txt","FAIL "+e);EditorApplication.Exit(2);}
    }
}
