using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEditor;
using UnityEditor.SceneManagement;
using NarutoVR.SealCombat;
using NarutoVR.SealCombat.Rpg;
using NarutoVR.SealCombat.Editor;
using Obj=UnityEngine.Object;

[InitializeOnLoad]
public static class DiskRecoveryChecks
{
    const string Key="DiskRecoveryChecks.stage", Dir="Logs/recovery-20261107/";
    const string SnapReport="Logs/snap-tool-adaptation-20261104/check.txt", RoomReport="Logs/enemy-pooling-20261104/play-check.txt";
    static DiskRecoveryChecks(){ EditorApplication.update+=Poll; EditorApplication.playModeStateChanged+=async state=>{
        if(state!=PlayModeStateChange.EnteredPlayMode || SessionState.GetInt(Key,0)!=1)return;
        var go=new GameObject("Narration gate verification");
        try{
            var nc=go.AddComponent<NarrationController>(); int done=0;
            nc.lines.Add(new NarrationLine{advanceMode=NarrationAdvanceMode.Operation});
            nc.lines.Add(new NarrationLine{advanceMode=NarrationAdvanceMode.Operation});
            nc.onDialogueCompleted.AddListener(()=>done++);nc.PublishCurrentOperation();nc.StartDialogue();
            Check(nc.CurrentLineIndex==0 && !nc.CompleteOperation(1),"early operation reached a future line");
            nc.PublishCurrentOperation();
            async System.Threading.Tasks.Task WaitFor(Func<bool> ready) {
                double until=EditorApplication.timeSinceStartup+10;
                while(!ready()){if(EditorApplication.timeSinceStartup>until)throw new TimeoutException(nc.Status);await System.Threading.Tasks.Task.Yield();}
            }
            await WaitFor(()=>nc.CurrentLineIndex==1);Check(nc.CurrentLineIndex==1 && !nc.OperationCompleted,"line 1 incorrectly latched");
            Check(!nc.CompleteOperation(0),"stale id");nc.PublishCurrentOperation();nc.PublishCurrentOperation();
            await WaitFor(()=>nc.CurrentLineIndex==-1);Check(nc.CurrentLineIndex==-1 && done==1,"dialogue completed twice");
            File.WriteAllText(Dir+"dialogue-check.txt","PASS: early input ignored; current event advances one step; wrong and stale IDs rejected; one completion.");
        }catch(Exception ex){File.WriteAllText(Dir+"dialogue-check.txt","FAILED "+ex);}
        finally{Obj.Destroy(go);}
    }; }
    static void Check(bool ok,string desc){if(!ok) throw new Exception(desc);}
    static void Stage(int n){SessionState.SetInt(Key,n);SessionState.SetString(Key+"time",DateTime.UtcNow.Ticks.ToString());}
    public static void Run(){
        Directory.CreateDirectory(Dir);
        try {
            EditorSceneManager.OpenScene("Assets/Scene/KaogongP1Trial.unity");
            ProjectLayoutOnce.Verify();
            var r = new List<string>{EnemyPoolingSetup.Validate()};
            var button=Obj.FindFirstObjectByType<ZhunwangPressButton>();
            Check(button && button.handInput && button.cap,"P1 button or input missing");
            var cap=button.cap;var body=button.GetComponentsInChildren<MeshFilter>().Single(m=>m.name=="Zhunwang_Body");
            var bodyRest=body.transform.position;var rest=cap.position;var bounds=cap.GetComponent<MeshFilter>().sharedMesh.bounds;
            var front=cap.TransformPoint(new Vector3(bounds.center.x,bounds.center.y,bounds.min.z));
            var axis=cap.TransformVector(Vector3.forward).normalized;
            int press=0,release=0;button.onPressed.AddListener(()=>press++);button.onReleased.AddListener(()=>release++);
            button.Sample(front-.005f*axis);button.Sample(front+.003f*axis);button.Sample(front+.003f*axis);
            float mm=Vector3.Distance(rest,cap.position)*1000f;
            Check(button.IsPressed && Mathf.Abs(mm-1.5f)<.05f && body.transform.position==bodyRest && press==1,"button travel "+mm);
            button.Sample(front-.006f*axis);Check(!button.IsPressed && release==1 && cap.position==rest,"button release");
            r.Add($"Button: {mm:F3} mm; cap only, one press and one release; input wired.");
            var a=Obj.FindFirstObjectByType<P1AudioManager>();Check(a && !a.explorationMusic && a.keepExplorationSilent,"P1 exploration BGM");
            r.Add("Exploration slot empty and fallback disabled.");
            const string dir="Assets/Art/Enemies/CanJiOu/Animations";
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Enemies/CanJiOu/CanJiOu_Room.prefab");
            var clips=Directory.GetFiles(dir,"*.fbx").OrderBy(x=>x).ToArray();Check(clips.Length==11,"Expected 11 clips");
            foreach(var path in clips){
                var clip=AssetDatabase.LoadAllAssetsAtPath(path.Replace('\\','/')).OfType<AnimationClip>().Single(c=>!c.name.StartsWith("__preview__"));
                Check(clip.length>0 && clip.humanMotion,"invalid clip "+clip.name);
                var g=Obj.Instantiate(prefab);var graph=PlayableGraph.Create("RecoveryPoseCheck");
                try{
                    g.SetActive(true);var anim=g.GetComponentInChildren<Animator>(true);Check(anim && anim.avatar && anim.avatar.isValid && anim.avatar.isHuman,"invalid target Avatar");
                    var ctrl=anim.runtimeAnimatorController;Check(ctrl,"no Animator Controller on room enemy"); bool bound=ctrl.animationClips.Contains(clip);
                    anim.cullingMode=AnimatorCullingMode.AlwaysAnimate;anim.enabled=true;anim.applyRootMotion=false;anim.Rebind();anim.Update(0);
                    graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                    var play=AnimationClipPlayable.Create(graph,clip);AnimationPlayableOutput.Create(graph,"motion",anim).SetSourcePlayable(play);graph.Play();
                    var bones=Enumerable.Range(0,(int)HumanBodyBones.LastBone).Select(i=>anim.GetBoneTransform((HumanBodyBones)i)).Where(t=>t).ToArray();
                    var poses=new Quaternion[bones.Length];float maxDelta=0;
                    for(int s=0;s<9;s++){play.SetTime(clip.length*(s+.1)/9.2);graph.Evaluate(.0001f);
                        for(int i=0;i<bones.Length;i++){if(s==0)poses[i]=bones[i].localRotation;else maxDelta=Mathf.Max(maxDelta,Quaternion.Angle(poses[i],bones[i].localRotation));}}
                    r.Add($"{clip.name}: {clip.length:F2}s; loop={clip.isLooping}; controller={bound}; retarget max bone delta={maxDelta:F2} deg");
                    Check(maxDelta>.05f,"no animated target bones: "+clip.name);
                }finally{graph.Destroy();Obj.DestroyImmediate(g);}
            }
            File.WriteAllLines(Dir+"assets-check.txt",r);Debug.Log(string.Join("\n",r));
            Stage(1);SnapZoneChecks.StartChecks();
        }catch(Exception ex){File.WriteAllText(Dir+"error.txt",ex.ToString());Debug.LogException(ex);Stage(0);EditorApplication.Exit(1);}
    }
    public static void EnemyAndSkills(){
        try{EditorSceneManager.OpenScene("Assets/Scene/KaogongP1Trial.unity");ProjectLayoutOnce.Verify();ProjectLayoutOnce.Checks();Stage(3);EnemySetupAndChecks.StartChecks();}
        catch(Exception ex){File.WriteAllText(Dir+"extra-error.txt",ex.ToString());Debug.LogException(ex);Stage(0);EditorApplication.Exit(1);}
    }
    static void Poll(){
        int n=SessionState.GetInt(Key,0);if(n==0)return;
        try{
            if(DateTime.UtcNow.Ticks-long.Parse(SessionState.GetString(Key+"time","0"))>TimeSpan.FromMinutes(4).Ticks)throw new TimeoutException("stage "+n);
            if(Application.isPlaying || EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isUpdating || EditorApplication.isCompiling)return;
            string path=n==1?SnapReport:n==3?"Logs/enemy-fsm-20261104/check.txt":RoomReport;if(!File.Exists(path))return;
            if(File.GetLastWriteTimeUtc(path).Ticks<long.Parse(SessionState.GetString(Key+"time","0")))return;
            string report=File.ReadAllText(path);if(report.Contains("FAILED"))throw new Exception(report);if(n==3 ? !report.Contains("真实帧导航 3 项通过") : !report.Contains("PASS"))return;
            if(n==1){Check(File.ReadAllText(Dir+"dialogue-check.txt").StartsWith("PASS"),"narration gate verification");EditorSceneManager.OpenScene("Assets/Scene/KaogongP1Trial.unity");Stage(2);P1RoomPlayChecks.Begin();}
            else if(n==2){EnemyAndSkills();}
            else{Stage(0);EditorApplication.Exit(0);}
        }catch(Exception ex){File.WriteAllText(Dir+"error.txt",ex.ToString());Debug.LogException(ex);Stage(0);EditorApplication.Exit(1);}
    }
}