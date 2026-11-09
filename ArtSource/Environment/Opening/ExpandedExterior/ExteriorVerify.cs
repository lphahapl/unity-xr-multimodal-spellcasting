using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ExteriorVerify
{
    static readonly List<string> report=new List<string>();
    static void Require(bool ok,string message){if(!ok)throw new Exception(message);report.Add("PASS "+message);}
    public static void Run(){
        report.Clear();report.Add(DateTimeOffset.Now.ToString("o")+" Unity "+Application.unityVersion);
        ExteriorSurvey.Work=EditorSceneManager.OpenPreviewScene("Assets/Scene/QA_ExteriorExpanded.unity");
        try{
            var root=ExteriorSurvey.Work.GetRootGameObjects().Single(g=>g.name=="首章外景拓展 · 窗庭远山");
            Require(PrefabUtility.IsPartOfPrefabInstance(root),"serialized exterior prefab resolves");
            Require(root.GetComponentsInChildren<MonoBehaviour>(true).Length==0,"new environment has no behaviours");
            Require(root.GetComponentsInChildren<Camera>(true).Length==0&&root.GetComponentsInChildren<Light>(true).Length==0,"no added cameras or lights");
            Require(root.GetComponentsInChildren<MeshFilter>(true).All(f=>f.sharedMesh),"all new meshes resolve");
            Require(root.GetComponentsInChildren<Renderer>(true).All(r=>r.sharedMaterials.All(m=>m&&m.shader&&!UnityEditor.ShaderUtil.ShaderHasError(m.shader))),"all new materials and shaders resolve");
            Require(ExteriorSurvey.All<Transform>().Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject))==0,"no missing scripts in candidate scene");
            Require(ExteriorSurvey.All<BoxCollider>().Count(c=>(c.name=="侧壁"||c.name=="房间侧墙")&&!c.enabled)==4,"four full wall colliders disabled");
            Require(root.GetComponentsInChildren<BoxCollider>().Count(c=>c.name.StartsWith("远端空气墙"))==4,"four distant boundaries serialized");
            Physics.SyncTransforms();
            var cols=ExteriorSurvey.All<Collider>().Where(c=>c.enabled&&c.gameObject.activeInHierarchy&&!c.isTrigger).ToArray();
            bool Hit(Vector3 p,Vector3 d,float distance)=>cols.Any(c=>c.Raycast(new Ray(p,d),out var hit,distance));
            foreach(float z in new[]{40.2f,43f,47.5f})foreach(float x in new[]{6f,9f,11.5f})Require(Hit(new Vector3(x,.5f,z),Vector3.down,.7f),"court floor "+x+","+z);
            foreach(float x in new[]{6f,9f,11.5f})Require(Hit(new Vector3(x,1.2f,48),Vector3.forward,2),"gate cannot be bypassed at "+x);
            foreach(float z in new[]{40.5f,43.3f,47.5f})Require(Hit(new Vector3(11.2f,.6f,z),Vector3.right,2),"east rail "+z);
            void Window(float x,float z){var s=Mathf.Sign(x);Require(!Hit(new Vector3(x-s*1.1f,1.8f,z),new Vector3(s,0,0),1.7f),"clear window sightline "+x+","+z);}
            foreach(float z in new[]{.2f,3.6f,7f})Window(-6.38f,z);
            foreach(float z in new[]{1.2f,6.5f})Window(6.38f,z);
            foreach(float z in new[]{20.8f,26f,31.2f})Window(7.2f,z);
            foreach(float z in new[]{21.7f,29.7f})Window(-7.2f,z);
            foreach(var p in new[]{new Vector3(57,12,32),new Vector3(-57,12,32),new Vector3(0,12,97),new Vector3(0,12,-33)}){
                var d=Mathf.Abs(p.x)>1?new Vector3(Mathf.Sign(p.x),0,0):new Vector3(0,0,Mathf.Sign(p.z));
                Require(Hit(p,d,2),"distant boundary "+p);
            }
            report.Add("New static triangles="+root.GetComponentsInChildren<MeshFilter>().Sum(f=>f.sharedMesh.triangles.Length/3));
            report.Add("New renderers="+root.GetComponentsInChildren<Renderer>().Length+" material slots="+root.GetComponentsInChildren<Renderer>().Sum(r=>r.sharedMaterials.Length));
            report.Add("New colliders="+root.GetComponentsInChildren<Collider>().Length);
            ExteriorSurvey.Photos("candidate");
            report.Add("PASS END");
        }catch(Exception e){report.Add("FAIL "+e);throw;}
        finally{File.WriteAllLines(ExteriorSurvey.Out+"candidate-verification.txt",report);EditorSceneManager.ClosePreviewScene(ExteriorSurvey.Work);}
    }
}
