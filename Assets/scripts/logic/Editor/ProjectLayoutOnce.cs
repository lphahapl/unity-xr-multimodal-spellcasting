#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;


public static class ProjectLayoutOnce
{
    const string Log = "Logs/project-layout-20261105";
    [Serializable] public class Entry { public string from, to, guid, hash, metaHash, type; public bool script; }
    [Serializable] public class Plan { public Entry[] files; public int scriptCount, soCount; }
    [Serializable] public class SceneState { public string path; public bool dirty; }
    [Serializable] public class Result
    {
        public string time, stage, error;
        public bool passed, playing;
        public SceneState[] scenes;
        public int scripts, assets, scriptableObjects, missingScripts, references, checks;
        public List<string> failures = new List<string>();
    }
    static SceneState[] Scenes() => Enumerable.Range(0, SceneManager.sceneCount)
        .Select(i => SceneManager.GetSceneAt(i)).Select(s => new SceneState {path=s.path,dirty=s.isDirty}).ToArray();
    static string Hash(string path)
    { using(var sha=SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant(); }
    static void Write(string name, Result result)
    {
        result.time=DateTimeOffset.Now.ToString("O");result.scenes=Scenes();result.playing=Application.isPlaying;
        Directory.CreateDirectory(Log);File.WriteAllText(Log+"/"+name+".json",JsonUtility.ToJson(result,true));
    }
    static void VerifyObject(UnityEngine.Object obj,Result result,string path)
    {
        var serialized=new SerializedObject(obj);var property=serialized.GetIterator();
        while(property.Next(true))
        {
            if(property.propertyType!=SerializedPropertyType.ObjectReference)continue;
            result.references++;
            if(property.objectReferenceValue==null && property.objectReferenceInstanceIDValue!=0)
                result.failures.Add(path+" "+property.propertyPath+" missing object reference");
        }
    }
    public static void Verify()
    {
        var plan=JsonUtility.FromJson<Plan>(File.ReadAllText("docs/research/20261107-recovery/layout-map.json"));
        var result=new Result{stage="verify",scripts=plan.scriptCount,scriptableObjects=plan.soCount};
        foreach(var item in plan.files)
        {
            if(AssetDatabase.AssetPathToGUID(item.to)!=item.guid){result.failures.Add("GUID: "+item.to);continue;}
            var asset=AssetDatabase.LoadMainAssetAtPath(item.to);
            if(!asset){result.failures.Add("Cannot load: "+item.to);continue;}
            result.assets++;
            if(item.script)
            {
                var script=asset as MonoScript;
                if(!script)result.failures.Add("Not MonoScript: "+item.to);
            }
            if(!string.IsNullOrEmpty(item.type))
            {
                if(!(asset is ScriptableObject)||asset.GetType().Name!=item.type)result.failures.Add("SO type: "+item.to);
                VerifyObject(asset,result,item.to);
            }
            if(item.to.EndsWith(".prefab"))
            {
                var prefab=PrefabUtility.LoadPrefabContents(item.to);
                try{foreach(var t in prefab.GetComponentsInChildren<Transform>(true))
                {result.missingScripts+=GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
                 foreach(var c in t.GetComponents<Component>())if(c)VerifyObject(c,result,item.to+"/"+t.name);}}
                finally{PrefabUtility.UnloadPrefabContents(prefab);}
            }
        }
        // Additively inspect scenes and close only those opened here; preserve the user's loaded scenes.
        foreach(var path in Directory.GetFiles("Assets/Scene","*.unity",SearchOption.AllDirectories))
        {
            string normalized=path.Replace('\\','/');var scene=SceneManager.GetSceneByPath(normalized);
            bool opened=!scene.IsValid()||!scene.isLoaded;
            if(opened)scene=EditorSceneManager.OpenScene(normalized,OpenSceneMode.Additive);
            try{foreach(var root in scene.GetRootGameObjects())foreach(var t in root.GetComponentsInChildren<Transform>(true))
            {result.missingScripts+=GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
             foreach(var c in t.GetComponents<Component>())if(c)VerifyObject(c,result,normalized+"/"+t.name);}}
            finally{if(opened)EditorSceneManager.CloseScene(scene,true);}
        }
        result.passed=result.failures.Count==0&&result.missingScripts==0;
        Write("verify-result",result); if(!result.passed)throw new Exception(JsonUtility.ToJson(result));
    }
    public static void Checks()
    {
        int count=NarutoVR.SealCombat.Editor.SkillCoreChecks.Run()
            +NarutoVR.SealCombat.Editor.SkillArchitectureChecks.Run()
            +NarutoVR.SealCombat.Editor.SkillMigrationChecks.Run()
            +NarutoVR.SealCombat.Editor.RpgDataChecks.Run();
        string layout=NarutoVR.SealCombat.Editor.ConfigurationAssetOrganizer.Validate();
        Write("checks-result",new Result{passed=true,stage=layout,checks=count});
    }
}
#endif
