using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

public static class ExteriorDressing
{
    const string Art="Assets/Art/Environment/Opening/MountainPolish/";
    public static void Build(Transform root, Scene scene, MeshFilter oldLand)
    {
        // Preserve the exact water-impact fragment before replacing its combined terrace mesh.
        if(oldLand) KeepImpact(root,oldLand);
        Model(root,"Workshop/Workshop.fbx","西窗外 · 天工偏坊",new Vector3(-22,-.60f,8),90,1.25f);
        Model(root,"Workshop/Workshop.fbx","东窗外 · 山间工坊",new Vector3(26,-.60f,25),-90,1.35f);
        Model(root,"Workshop/Workshop.fbx","测候台远景 · 校器坊",new Vector3(28,-.60f,66),-115,1.6f);
        Rock(root,new Vector3(-8,-1.65f,42),30,new Vector3(2.1f,2.0f,2.2f));
        Rock(root,new Vector3(-8.5f,-1.65f,53),75,new Vector3(2.4f,2.7f,1.8f));
        Rock(root,new Vector3(-5.7f,-1.65f,63),-25,new Vector3(1.7f,2.0f,1.5f));
        Rock(root,new Vector3(13.5f,-1.65f,53),-40,new Vector3(2.2f,2.0f,2.1f));
        Rock(root,new Vector3(14,-1.1f,68.5f),180,new Vector3(1.9f,2.1f,1.7f));
        Rock(root,new Vector3(2,-1.2f,82),0,new Vector3(2.8f,2.7f,2.6f));
        Rock(root,new Vector3(-17,-1.2f,5),45,new Vector3(1.8f,1.3f,2.0f));
        Rock(root,new Vector3(17,-1.2f,22),-60,new Vector3(2.1f,1.7f,2.1f));
        var pine=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Environment/Opening/MountainPine/MountainPine.fbx");
        if(pine)
        {
            Model(root,"MountainPine/MountainPine.fbx","西窗外山松",Grounded(-16,11,.15f),105,1f);
            Model(root,"MountainPine/MountainPine.fbx","门庭侧松",Grounded(19,61,.15f),-75,1.05f);
            Model(root,"MountainPine/MountainPine.fbx","东坡山松",Grounded(20,30,.15f),30,.85f);
        }
        var stone=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Chapter01/WorkshopStoneWall.mat");
        foreach(float side in new[]{-1f,1f})
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);SceneManager.MoveGameObjectToScene(go,scene);
            go.name="工坊砌石台基";go.transform.SetParent(root,false);go.transform.localPosition=new Vector3(side*8.8f,-2f,14);
            go.transform.localScale=new Vector3(4.5f,3.75f,44);go.GetComponent<Renderer>().sharedMaterial=FoundationMaterial(stone);
            Object.DestroyImmediate(go.GetComponent<Collider>());
        }
        AssetDatabase.SaveAssets();
    }
    static GameObject Model(Transform root,string path,string name,Vector3 p,float yaw,float scale)
    {
        var model=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Environment/Opening/"+path);
        if(!model)throw new Exception("Missing dressing asset "+path);
        var instance=(GameObject)PrefabUtility.InstantiatePrefab(model,root.gameObject.scene);
        instance.name=name;instance.transform.SetParent(root,false);instance.transform.localPosition=p;
        instance.transform.localRotation=Quaternion.Euler(0,yaw,0);instance.transform.localScale=Vector3.one*scale;
        foreach(var r in instance.GetComponentsInChildren<MeshRenderer>())
        {r.shadowCastingMode=ShadowCastingMode.On;r.receiveShadows=true;}
        return instance;
    }
    static void Rock(Transform root,Vector3 p,float yaw,Vector3 scale)
    {
        p=Grounded(p.x,p.z,.30f);
        var go=Model(root,"StrataRock/StrataRock.fbx","层岩 · 岸壁",p,yaw,1);
        go.transform.localScale=scale;
        var source=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Environment/Opening/StrataRock/StrataRock_Baked.mat");
        var mat=AssetDatabase.LoadAssetAtPath<Material>(Art+"StrataRock_Context.mat");
        if(!mat){mat=new Material(source);mat.name="StrataRock_Context";mat.SetColor("_BaseColor",new Color(.7f,.75f,.79f));AssetDatabase.CreateAsset(mat,Art+"StrataRock_Context.mat");}
        foreach(var renderer in go.GetComponentsInChildren<Renderer>())renderer.sharedMaterial=mat;
    }
    static Vector3 Grounded(float x,float z,float burial)=>new Vector3(x,ExteriorLandscapeBuilder.Height(x,z)-burial,z);
    static Material FoundationMaterial(Material source)
    {
        var mat=AssetDatabase.LoadAssetAtPath<Material>(Art+"FoundationStone.mat");
        if(!mat){mat=new Material(source);mat.name="FoundationStone";mat.SetColor("_BaseColor",new Color(.62f,.68f,.72f));mat.SetTextureScale("_BaseMap",new Vector2(2,18));AssetDatabase.CreateAsset(mat,Art+"FoundationStone.mat");}
        return mat;
    }
    static void KeepImpact(Transform root,MeshFilter source)
    {
        var mesh=source.sharedMesh;var v=mesh.vertices;
        var world=v.Select(x=>source.transform.TransformPoint(x)).ToArray();
        var triangles=new List<int>();
        foreach(var sm in Enumerable.Range(0,mesh.subMeshCount))
        {
            var indices=mesh.GetTriangles(sm);
            for(int i=0;i<indices.Length;i+=3)
            {
                var c=(world[indices[i]]+world[indices[i+1]]+world[indices[i+2]])/3;
                if(c.x>-2.9f&&c.x<-1.05f&&c.z>55.05f&&c.z<57.35f)
                {triangles.Add(indices[i]);triangles.Add(indices[i+1]);triangles.Add(indices[i+2]);}
            }
        }
        if(triangles.Count<3)throw new Exception("Water impact geometry was not preserved");
        var copy=new Mesh{name="PreservedWaterImpact"};copy.vertices=world;copy.uv=mesh.uv;copy.triangles=triangles.ToArray();copy.RecalculateNormals();copy.RecalculateBounds();
        var existing=AssetDatabase.LoadAssetAtPath<Mesh>(Art+"PreservedWaterImpact.asset");
        if(existing){EditorUtility.CopySerialized(copy,existing);Object.DestroyImmediate(copy);copy=existing;}else AssetDatabase.CreateAsset(copy,Art+"PreservedWaterImpact.asset");
        var go=new GameObject("逆流冲击残壁 · 原形保留");go.transform.SetParent(root,false);go.AddComponent<MeshFilter>().sharedMesh=copy;
        go.AddComponent<MeshRenderer>().sharedMaterial=source.GetComponent<Renderer>().sharedMaterials[0];
        Debug.Log("Preserved exact water impact triangles="+triangles.Count/3);
    }
}
