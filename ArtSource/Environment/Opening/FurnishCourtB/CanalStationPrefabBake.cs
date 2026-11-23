using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
public static class CanalStationPrefabBake {
    const string Root = "Assets/Art/Environment/Opening/FurnishCourtB/";
    static readonly string[] Names = { "CanalMeasuringStation" };
    static readonly float[] Thresholds = { .22f, .09f, .009f };
    public static void Run() {
        var evidence = new List<string>();
        var material=AssetDatabase.LoadAssetAtPath<Material>(Root+"CanalStation_Baked.mat");
        if (material==null || material.shader.name!="Universal Render Pipeline/Lit")
            throw new Exception("Missing URP material");
        foreach (string slot in new[]{"_BaseMap","_BumpMap","_MetallicGlossMap","_OcclusionMap"}) {
            var tex=material.GetTexture(slot);
            if (tex==null || tex.width!=1024 || tex.height!=1024) throw new Exception("Missing 1K texture "+slot);
        }
        foreach (var name in Names) {
            var root=new GameObject(name);
            try {
                var group=root.AddComponent<LODGroup>();
                var lods=new LOD[3];
                for(int tier=0;tier<3;tier++) {
                    var fbx=AssetDatabase.LoadAssetAtPath<GameObject>(Root+name+"_LOD"+tier+".fbx");
                    if(!fbx)throw new Exception("No model "+name+" "+tier);
                    var child=(GameObject)PrefabUtility.InstantiatePrefab(fbx);
                    child.transform.SetParent(root.transform,false);
                    var renderers=child.GetComponentsInChildren<MeshRenderer>();
                    if(renderers.Length!=1 || renderers[0].sharedMaterial!=material)
                        throw new Exception("Unexpected renderer/material "+name+" tier "+tier);
                    lods[tier]=new LOD(Thresholds[tier],renderers);
                }
                group.SetLODs(lods);group.RecalculateBounds();
                var box=root.AddComponent<BoxCollider>();
                var b=lods[0].renderers[0].bounds;
                box.center=root.transform.InverseTransformPoint(b.center);
                box.size=b.size;
                var dst=Root+name+".prefab";
                PrefabUtility.SaveAsPrefabAsset(root,dst);
                var saved=AssetDatabase.LoadAssetAtPath<GameObject>(dst);
                var levels=saved.GetComponent<LODGroup>().GetLODs();
                int last=int.MaxValue;
                if(levels.Length!=3 || saved.GetComponentsInChildren<Collider>().Length!=1)throw new Exception("Missing prefab components");
                var counts=new List<int>();
                foreach(var level in levels){
                    var renderer=level.renderers.Single();
                    var mesh=renderer.GetComponent<MeshFilter>().sharedMesh;
                    int tris=(int)(mesh.GetIndexCount(0)/3);
                    if(tris<=0 || tris>=last || renderer.sharedMaterial!=material)throw new Exception("Invalid LOD on "+name);
                    counts.Add(tris);last=tris;
                }
                evidence.Add(name+" triangles "+string.Join("/",counts)+" size m "+b.size.ToString("F4")+"; LODs: .22/.09/.009; BoxCollider=1");
            } finally {UnityEngine.Object.DestroyImmediate(root);}
        }
        AssetDatabase.SaveAssets();
        File.WriteAllLines("canal-station-unity-result.txt",evidence);
        Debug.Log("CANAL_STATION_VERIFIED\n"+string.Join("\n",evidence));
    }
}
