using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class ExteriorSurvey
{
    public const string ScenePath="Assets/Scene/Chapter01_Awakening.unity";
    public const string Out="D:/UnityProjects/NarutoVR/Logs/exterior-expand-20261108/";
    public static Scene Work;
    public static T[] All<T>() where T:Component => Work.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<T>(true)).ToArray();
    public static string PathOf(Transform t) => t.parent ? PathOf(t.parent)+"/"+t.name : t.name;
    public static void Run()
    {
        Work=EditorSceneManager.OpenPreviewScene(ScenePath);
        try {
        var lines=All<Renderer>()
            .Where(r=>r.bounds.center.z>-5 && r.bounds.center.z<90)
            .Select(r=>$"R|{r.enabled}|{r.gameObject.activeSelf}|{PathOf(r.transform)}|center={r.bounds.center:F3}|size={r.bounds.size:F3}|mesh={(r.GetComponent<MeshFilter>()?AssetDatabase.GetAssetPath(r.GetComponent<MeshFilter>().sharedMesh):"skin")}").ToList();
        lines.AddRange(All<Collider>()
            .Where(r=>!r.isTrigger).Select(r=>$"C|{r.enabled}|{PathOf(r.transform)}|center={r.bounds.center:F3}|size={r.bounds.size:F3}"));
        File.WriteAllLines(Out+"survey.txt",lines);
        Photos("before");
        } finally {EditorSceneManager.ClosePreviewScene(Work);}
    }
    public static void Photos(string prefix)
    {
        var cam=new GameObject("Exterior review camera").AddComponent<Camera>();
        SceneManager.MoveGameObjectToScene(cam.gameObject,Work);cam.scene=Work;
        cam.enabled=false;cam.fieldOfView=70;cam.nearClipPlane=.03f;cam.farClipPlane=400;
        cam.clearFlags=CameraClearFlags.Skybox;cam.backgroundColor=RenderSettings.fogColor;
        var rt=new RenderTexture(1440,900,24,RenderTextureFormat.ARGB32);cam.targetTexture=rt;
        var previous=RenderTexture.active;var image=new Texture2D(1440,900,TextureFormat.RGB24,false);
        void Capture(string n,Vector3 at,Vector3 look){
            cam.transform.position=at;cam.transform.LookAt(look);cam.Render();RenderTexture.active=rt;
            image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);image.Apply();
            File.WriteAllBytes(Out+prefix+"-"+n+".png",image.EncodeToPNG());
        }
        Capture("door",new Vector3(0,1.65f,36.1f),new Vector3(0,1.65f,45));
        Capture("court",new Vector3(3.7f,1.65f,40.5f),new Vector3(-3,1.8f,46));
        Capture("canal",new Vector3(3.3f,1.65f,53),new Vector3(-.5f,1.7f,63));
        Capture("wake-window",new Vector3(-3.8f,1.65f,3.0f),new Vector3(-14,1.65f,3.6f));
        Capture("trial-window",new Vector3(4.8f,1.65f,25),new Vector3(18,1.65f,26));
        Capture("overview",new Vector3(35,32,18),new Vector3(0,0,40));
        RenderTexture.active=previous;cam.targetTexture=null;rt.Release();
        Object.DestroyImmediate(rt);Object.DestroyImmediate(image);Object.DestroyImmediate(cam.gameObject);
    }
}
