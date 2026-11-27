using System;
using System.Collections.Generic;
using System.Linq;
using NarutoVR.SealCombat.Rpg;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Run in the Editor assembly of the isolated three-bank lab. Positive local Z points over the break.
public static class BuildPassageBankMarkers
{
    const string Dir = "Assets/Art/Glyphs/";
    const string MeshPath = Dir+"PassageBankSocket.asset";
    const string CrestPath = Dir+"PassageBankCrest.asset";
    const string PrefabPath = Dir+"PassageBankSocket.prefab";
    const string ScenePath = "Assets/Scene/PassageGlyphLab.unity";

    sealed class Stonework
    {
        readonly List<Vector3> vertices = new();
        readonly List<Vector2> uv = new();
        readonly List<int>[] faces = {new(),new(),new()};

        void Face(int material, params Vector3[] points)
        {
            int start = vertices.Count;
            var normal = Vector3.Cross(points[1]-points[0], points[2]-points[0]).normalized;
            foreach (var p in points)
            {
                vertices.Add(p);
                var projected = Mathf.Abs(normal.y)>.65f ? new Vector2(p.x,p.z) :
                    Mathf.Abs(normal.z)>.65f ? new Vector2(p.x,p.y) : new Vector2(p.z,p.y);
                uv.Add(projected * 1.8f + Vector2.one*.5f);
            }
            for (int i=1;i<points.Length-1;i++) faces[material].AddRange(new[]{start,start+i,start+i+1});
        }

        static Vector3[] Ring(float x,float z,float w,float d,float y,float cut)
        {
            float l=x-w/2, r=x+w/2, b=z-d/2, f=z+d/2;
            cut=Mathf.Min(cut,Mathf.Min(w,d)*.35f);
            return new[]{new Vector3(l+cut,y,b),new(l,y,b+cut),new(l,y,f-cut),new(l+cut,y,f),
                new(r-cut,y,f),new(r,y,f-cut),new(r,y,b+cut),new(r-cut,y,b)};
        }

        // A chamfered stone or bronze member with a real bevel visible in silhouette.
        public void Block(int mat,float x,float z,float w,float d,float bottom,float top,float cut,float bevel)
        {
            var low=Ring(x,z,w,d,bottom,cut);
            var shoulder=Ring(x,z,w,d,top-bevel,cut);
            var high=Ring(x,z,w-2*bevel,d-2*bevel,top,cut*.78f);
            Face(mat,low.Reverse().ToArray());
            Face(mat,high);
            for (int i=0;i<8;i++)
            {
                int j=(i+1)%8;
                Face(mat,low[i],low[j],shoulder[j],shoulder[i]);
                Face(mat,shoulder[i],shoulder[j],high[j],high[i]);
            }
        }
        public Mesh Finish()
        {
            var mesh=new Mesh {name="符渡石座与青铜渡榫"};
            mesh.SetVertices(vertices); mesh.SetUVs(0,uv); mesh.subMeshCount=faces.Length;
            for (int i=0;i<faces.Length;i++) mesh.SetTriangles(faces[i],i);
            mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
            return mesh;
        }
    }

    static Mesh BridgeSeat()
    {
        var m=new Stonework();
        const int stone=0, bronze=1, inlay=2;
        foreach (int sign in new[]{-1,1})
        {
            float x=sign*.35f;
            m.Block(stone,x,-.01f,.20f,.61f,-.12f,.070f,.044f,.014f);
            m.Block(stone,x,.14f,.155f,.33f,-.195f,-.070f,.033f,.012f);
            m.Block(bronze,x,.017f,.124f,.44f,.042f,.100f,.022f,.009f);
            m.Block(inlay,x,.092f,.013f,.23f,.097f,.104f,.003f,.001f);
            for (int i=0;i<3;i++)
                m.Block(inlay,x+sign*.023f,-.083f+i*.045f,.034f,.010f,.098f,.104f,.002f,.001f);
        }
        foreach(int sign in new[]{-1,1})
            m.Block(bronze,sign*.075f,-.035f,.016f,.165f,-.007f,.007f,.003f,.002f);
        m.Block(bronze,0,.044f,.162f,.016f,-.006f,.008f,.003f,.002f);
        m.Block(inlay,0,-.034f,.088f,.013f,-.004f,.009f,.003f,.002f);
        return m.Finish();
    }
    static Mesh PiercedCrest()
    {
        var m=new Stonework();
        const int stone=0, bronze=1, inlay=2;
        // One tall bronze silhouette per bank. Secondary sockets repeat the inlaid floor mark.
        const float cx=-.35f, cz=-.125f;
        m.Block(stone,cx,cz,.278f,.244f,.056f,.154f,.048f,.019f);
        m.Block(stone,cx,cz,.201f,.176f,.142f,.327f,.031f,.012f);
        m.Block(bronze,cx,cz,.217f,.186f,.181f,.212f,.032f,.006f);
        m.Block(bronze,cx,cz,.241f,.181f,.315f,.366f,.034f,.010f);
        foreach(int sign in new[]{-1,1})
        {
            float x=cx+sign*.091f;
            m.Block(bronze,x,cz,.053f,.088f,.350f,.573f,.014f,.007f);
            foreach(int face in new[]{-1,1})
                m.Block(inlay,x,cz+face*.044f,.015f,.005f,.375f,.555f,.001f,.001f);
        }
        m.Block(bronze,cx,cz,.255f,.101f,.550f,.603f,.015f,.009f);
        m.Block(bronze,cx-.019f,cz,.148f,.058f,.449f,.481f,.009f,.005f);
        foreach(int face in new[]{-1,1})
        {
            m.Block(inlay,cx,cz+face*.0505f,.194f,.005f,.565f,.578f,.001f,.001f);
            m.Block(inlay,cx-.014f,cz+face*.029f,.109f,.005f,.459f,.471f,.001f,.001f);
        }
        return m.Finish();
    }

    static T Asset<T>(string path) where T:Object =>
        AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new InvalidOperationException(path);

    static Mesh SaveMesh(string path, Mesh mesh)
    {
        var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(existing) { EditorUtility.CopySerialized(mesh,existing); Object.DestroyImmediate(mesh); mesh=existing; }
        else AssetDatabase.CreateAsset(mesh,path);
        return mesh;
    }
    static GameObject Part(string name, Mesh mesh, Material[] materials)
    {
        var art=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer),typeof(MeshCollider));
        art.GetComponent<MeshFilter>().sharedMesh=mesh;
        art.GetComponent<MeshCollider>().sharedMesh=mesh;
        art.GetComponent<MeshRenderer>().sharedMaterials=materials;
        return art;
    }
    public static void Run()
    {
        var scene=EditorSceneManager.OpenScene(ScenePath);
        var mesh=SaveMesh(MeshPath,BridgeSeat());
        var crestMesh=SaveMesh(CrestPath,PiercedCrest());
        var materials=new[]{
            Asset<Material>("Assets/Materials/Chapter01/CourtyardStone.mat"),
            Asset<Material>("Assets/Art/Environment/Opening/PreqinGateFittings/GateHammeredBronze.mat"),
            Asset<Material>(Dir+"PassageCueGold.mat")};
        var art=Part("符渡石座",mesh,materials);
        Part("通路符铜榫",crestMesh,materials).transform.SetParent(art.transform,false);
        var prefab=PrefabUtility.SaveAsPrefabAsset(art,PrefabPath);
        Object.DestroyImmediate(art);
        var anchors=Object.FindObjectsByType<PassageGlyphAnchor>(FindObjectsSortMode.None);
        if(anchors.Length!=4) throw new InvalidOperationException("三岸四个榫座的范围不符");
        foreach(var anchor in anchors)
        {
            var root=anchor.transform;
            var oldMesh=root.GetComponent<MeshFilter>();
            var oldRenderer=root.GetComponent<MeshRenderer>();
            if(oldRenderer) Object.DestroyImmediate(oldRenderer);
            if(oldMesh) Object.DestroyImmediate(oldMesh);
            root.localScale=Vector3.one;
            var trigger=root.GetComponent<BoxCollider>();
            trigger.size=new Vector3(.60f,.012f,.30f);
            trigger.center=root.InverseTransformPoint(anchor.landing.position + Vector3.up*.003f);
            var placed=root.Cast<Transform>().FirstOrDefault(t=>PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject)==prefab);
            if(!placed) placed=((GameObject)PrefabUtility.InstantiatePrefab(prefab,root)).transform;
            Vector3 outward=Vector3.ProjectOnPlane(anchor.landing.position-anchor.bank.position,Vector3.up);
            // The two first-bank sockets share the straight north lip, including the laterally offset one.
            if(anchor.name=="A" || anchor.name=="A2") outward=anchor.bank.forward;
            placed.SetPositionAndRotation(anchor.landing.position,Quaternion.LookRotation(outward.normalized,Vector3.up));
            var crest = placed.Find("通路符铜榫").gameObject;
            crest.SetActive(anchor.name!="A2");
            PrefabUtility.RecordPrefabInstancePropertyModifications(crest);
            PrefabUtility.RecordPrefabInstancePropertyModifications(placed);
        }
        var hint=Object.FindFirstObjectByType<PassageApproachHint>();
        if(!hint) hint=new GameObject("符渡 · 近岸提示").AddComponent<PassageApproachHint>();
        hint.anchors=anchors;
        hint.player=Object.FindFirstObjectByType<LinearRpgController>();
        hint.strokeReference=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true))
            .Single(t=>t.name=="通路符图谱").gameObject;
        hint.strokeReference.SetActive(false);
        if(!hint.player || !EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException(ScenePath);
        AssetDatabase.SaveAssets();
        Debug.Log($"PASSAGE_BANK_BUILT {anchors.Length} sockets, {mesh.triangles.Length/3} triangles per seat, {crestMesh.triangles.Length/3} per optional crest.");
    }
}
