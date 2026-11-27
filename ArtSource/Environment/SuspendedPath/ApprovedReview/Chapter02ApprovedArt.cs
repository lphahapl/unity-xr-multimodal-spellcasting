using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public static class Chapter02ApprovedArt
{
    const string ScenePath = "Assets/Scene/Chapter02_SuspendedPath.unity";
    static string Output => Environment.GetEnvironmentVariable("CH2_ART_OUTPUT") ?? throw new Exception("Set CH2_ART_OUTPUT");
    public static void Capture()
    {
        EditorSceneManager.OpenScene(ScenePath);
        Directory.CreateDirectory(Output);
        CaptureViews();
    }
    public static void Assemble()
    {
        Directory.CreateDirectory(Output);
        var scene = EditorSceneManager.OpenScene(ScenePath);
        var colliders = CollisionState(scene);
        foreach (var type in new[] { "ApprovedGorgeBuilder", "ApprovedBasketBuilder", "ApprovedWorkshopBuilder" })
        {
            var method = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(type)).Where(t => t != null)
                .Select(t => t.GetMethod("Apply", BindingFlags.Public | BindingFlags.Static)).FirstOrDefault(m => m != null);
            if (method == null) throw new Exception("Missing builder " + type);
            method.Invoke(null, null);
        }
        var after = CollisionState(scene);
        var lost = colliders.Except(after).ToArray();
        var added = after.Except(colliders).ToArray();
        File.WriteAllLines(Path.Combine(Output, "collision-diff.txt"), new[]{"BEFORE "+colliders.Count+" AFTER "+after.Count}.Concat(lost.Select(s=>"- "+s)).Concat(added.Select(s=>"+ "+s)));
        if (lost.Length != 0 || added.Length != 0)
            throw new InvalidOperationException("Scene collider changes found. See collision-diff.txt.");
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new Exception("Scene save failed");
        AssetDatabase.SaveAssets();
        Capture();
    }
    static List<string> CollisionState(Scene scene)
    {
        return scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Collider>(true))
            .Select(c => GlobalObjectId.GetGlobalObjectIdSlow(c)+" "+c.name+" "+c.GetType().Name+" "+c.enabled+" "+c.gameObject.activeInHierarchy+" "+c.bounds.center.ToString("F3")+" "+c.bounds.size.ToString("F3")).OrderBy(x=>x).ToList();
    }
    static string Hierarchy(Transform t) => t.parent ? Hierarchy(t.parent)+"/"+t.name : t.name;
    static void CaptureViews()
    {
        var scene = SceneManager.GetActiveScene();
        var renderers = scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<MeshRenderer>(true)).ToArray();
        File.WriteAllLines(Path.Combine(Output,"renderers.tsv"), new[]{"hierarchy\tactive\tcenter\tsize\tmaterials"}.Concat(renderers.Select(r=>Hierarchy(r.transform)+"\t"+(r.enabled&&r.gameObject.activeInHierarchy)+"\t"+r.bounds.center.ToString("F2")+"\t"+r.bounds.size.ToString("F2")+"\t"+string.Join("|",r.sharedMaterials.Select(m=>m?AssetDatabase.GetAssetPath(m):"<null>")))));
        Shot("hub",new Vector3(32.3f,1.62f,15.3f),new Vector3(27,5,68),58);
        Shot("upper",new Vector3(-8.7f,81.62f,20.75f),new Vector3(35.6f,48.6f,20.4f),62);
        Shot("yard",new Vector3(28.8f,1.63f,44),new Vector3(10,2.1f,50.4f),67);
        Shot("basket-close",new Vector3(-8.7f,81.62f,20.75f),new Vector3(-10.5f,80.2f,21.3f),59);
        Shot("lower",new Vector3(76.8f,-78.38f,26.4f),new Vector3(14,-42,31),56);
    }
    static void Shot(string name,Vector3 eye,Vector3 target,float fov)
    {
        var camera=new GameObject("Chapter02 review camera").AddComponent<Camera>();
        var rt=RenderTexture.GetTemporary(1600,900,24,RenderTextureFormat.ARGB32);
        var frame=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);
        var before=RenderTexture.active;
        try
        {
            camera.transform.position=eye;
            camera.transform.LookAt(target);
            camera.fieldOfView=fov;
            camera.nearClipPlane=.05f;
            camera.farClipPlane=Camera.allCameras.Where(c=>c!=camera).Select(c=>c.farClipPlane).DefaultIfEmpty(1000).Max();
            camera.clearFlags=CameraClearFlags.Skybox;
            camera.targetTexture=rt;
            camera.Render();
            RenderTexture.active=rt;
            frame.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);
            frame.Apply();
            File.WriteAllBytes(Path.Combine(Output,name+".jpg"),frame.EncodeToJPG(94));
            if (name=="hub"||name=="upper"||name=="yard") HitMap(camera,name);
        }
        finally
        {
            RenderTexture.active=before; camera.targetTexture=null;
            RenderTexture.ReleaseTemporary(rt);
            UnityEngine.Object.DestroyImmediate(frame);
            UnityEngine.Object.DestroyImmediate(camera.gameObject);
        }
    }
    static void HitMap(Camera cam,string view)
    {
        var method=typeof(HandleUtility).GetMethod("IntersectRayMesh",BindingFlags.Static|BindingFlags.NonPublic);
        if (method==null) return;
        var meshes=SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<MeshFilter>()).Where(m=>m.sharedMesh && m.TryGetComponent<Renderer>(out var renderer)&&renderer.enabled).ToArray();
        var output=new List<string>{"u\tv\tdistance\tobject\tworldHit"};
        for(int y=0;y<9;y++) for(int x=0;x<15;x++)
        {
            var ray=cam.ViewportPointToRay(new Vector2((x+.5f)/15f,(y+.5f)/9f));
            float dist=float.MaxValue; MeshFilter closest=null; Vector3 point=default;
            foreach(var mesh in meshes)
            {
                if(!mesh.GetComponent<Renderer>().bounds.IntersectRay(ray,out var entry)||entry>dist) continue;
                object[] args={ray,mesh.sharedMesh,mesh.transform.localToWorldMatrix,null};
                if((bool)method.Invoke(null,args))
                {
                    var hit=(RaycastHit)args[3];
                    if(hit.distance<dist){dist=hit.distance;closest=mesh;point=hit.point;}
                }
            }
            if(closest)output.Add($"{(x+.5f)/15f:F3}\t{(y+.5f)/9f:F3}\t{dist:F2}\t{Hierarchy(closest.transform)}\t{point.ToString("F2")}");
        }
        File.WriteAllLines(Path.Combine(Output,view+"-rays.tsv"),output);
    }
}
