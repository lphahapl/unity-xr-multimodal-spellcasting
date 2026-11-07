#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;
using NarutoVR.SealCombat.Rpg;
using NarutoVR.SealLab;

namespace NarutoVR.SealCombat.Editor
{
    internal static class DebugTextComparison
    {
        const string Key="NarutoVR.DebugTextComparison";
        static readonly List<Action> restore=new();
        static readonly List<double> frames=new(), gaps=new();
        static double start,previous;
        static int phase,frame,focused,xr;
        static string folder;
        static bool originalBackground;
        static SkillTestController controller;
        [InitializeOnLoadMethod] static void Init() {
            EditorApplication.playModeStateChanged+=state=>{
                if(!SessionState.GetBool(Key,false))return;
                if(state==PlayModeStateChange.EnteredPlayMode) {
                    folder="Logs/text-comparison-"+DateTime.Now.ToString("yyyyMMdd-HHmmss");
                    Directory.CreateDirectory(folder);
                    originalBackground=Application.runInBackground;Application.runInBackground=true;
                    phase=0; ResetPhase();EditorApplication.update+=Tick;
                }
                if(state==PlayModeStateChange.ExitingPlayMode){ Cleanup();Application.runInBackground=originalBackground;SessionState.EraseBool(Key); }
            };
        }
        public static void Begin(){
            if(EditorApplication.isPlayingOrWillChangePlaymode || UnityEngine.SceneManagement.SceneManager.GetActiveScene().path!=LinearRpgSceneSetup.ScenePath || UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty || EditorSceneManagerHasOverride())
                throw new InvalidOperationException("需要已保存的战斗场景和默认Play入口");
            SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;
        }
        static bool EditorSceneManagerHasOverride()=>UnityEditor.SceneManagement.EditorSceneManager.playModeStartScene;
        static void Pause(Behaviour b){if(b && b.enabled){b.enabled=false;restore.Add(()=>{if(b)b.enabled=true;});}}
        static void SelectPanel(bool desktopOnly){
            Restore();
            foreach(var v in UnityEngine.Object.FindObjectsByType<RpgSessionView>(FindObjectsSortMode.None)){
                if(desktopOnly){var t=v.statusText;Pause(t);v.statusText=null;restore.Add(()=>v.statusText=t);}
                else {bool before=v.showDesktopControls;v.showDesktopControls=false;restore.Add(()=>v.showDesktopControls=before);}
            }
            foreach(var v in UnityEngine.Object.FindObjectsByType<SkillTestView>(FindObjectsSortMode.None)){
                if(desktopOnly){var t=v.statusText;Pause(t);v.statusText=null;restore.Add(()=>v.statusText=t);}
                else {bool before=v.showDesktopControls;v.showDesktopControls=false;restore.Add(()=>v.showDesktopControls=before);}
            }
            if(desktopOnly)foreach(var v in UnityEngine.Object.FindObjectsByType<SealLabView>(FindObjectsSortMode.None)){
                var t=v.statusText;Pause(t);v.statusText=null;restore.Add(()=>v.statusText=t);
            }
        }
        static void Restore(){foreach(var a in restore)a();restore.Clear();}
        static void Cleanup(){EditorApplication.update-=Tick;Restore();}
        static void ResetPhase(){start=EditorApplication.timeSinceStartup;frame=-1;focused=xr=0;previous=-1;frames.Clear();gaps.Clear();}
        static void Tick(){
            if(!EditorApplication.isPlaying||frame==Time.frameCount)return;
            frame=Time.frameCount;
            double age=EditorApplication.timeSinceStartup-start;
            if(age<5)return;
            frames.Add(Time.unscaledDeltaTime*1000d);
            if(Application.isFocused)focused++;
            if(XRSettings.isDeviceActive)xr++;
            if(!controller)controller=UnityEngine.Object.FindFirstObjectByType<SkillTestController>();
            var f=controller?controller.lab?.Frame:null;
            if(f!=null && f.time>previous){if(previous>=0)gaps.Add((f.time-previous)*1000);previous=f.time;}
            if(age<18 || frames.Count<30)return;
            string name=new[]{"both","desktop","headset","restored"}[phase];
            try{
                StreamCpuProfile.Capture();
                File.Copy("Logs/stream-cpu-sample.txt",folder+"/"+name+"-cpu.txt",true);
                File.WriteAllText(folder+"/"+name+".txt",DateTime.UtcNow.ToString("O")+"\nframes="+frames.Count+" medianMs="+Median(frames)+" xr="+xr+" focused="+focused+"\ngaps="+gaps.Count+" medianGapMs="+Median(gaps)+" over120="+gaps.Count(x=>x>120));
                var root=GameObject.Find("电脑交互调试面板");
                if(root){
                    var labels=root.GetComponentsInChildren<Text>();
                    File.WriteAllLines(folder+"/"+name+"-visible-labels.txt",labels.Where(t=>t.isActiveAndEnabled && t.rectTransform.rect.width>0 && t.rectTransform.rect.height>0).Select(t=>t.text));
                    if(name=="restored")CheckControls(root,folder);
                }
                if(++phase==4){Cleanup();EditorApplication.isPlaying=false;return;}
                if(phase==1)SelectPanel(true);else if(phase==2)SelectPanel(false);else Restore();
                ResetPhase();
            } catch(Exception ex){Debug.LogException(ex);Cleanup();EditorApplication.isPlaying=false;}
        }
        static void CheckControls(GameObject root,string directory){
            var view=root.GetComponentInParent<RpgSessionView>();
            var btn=root.GetComponentsInChildren<Button>().First(b=>b.name=="结印匹配 · 得分与特征明细");
            bool original=view.showSealSection;
            var pointer=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current);
            UnityEngine.EventSystems.ExecuteEvents.Execute(btn.gameObject,pointer,UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
            bool changed=view.showSealSection!=original;
            UnityEngine.EventSystems.ExecuteEvents.Execute(btn.gameObject,pointer,UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
            var scroll=root.GetComponentInChildren<ScrollRect>();
            File.WriteAllText(directory+"/controls.txt","clicked="+changed+" restored="+(view.showSealSection==original)+" scrollHeight="+scroll.content.rect.height+" viewportHeight="+scroll.viewport.rect.height+" left="+scroll.viewport.position.x+" eventSystem="+UnityEngine.EventSystems.EventSystem.current);
        }
        static double Median(List<double> v){if(v.Count==0)return 0;var a=v.OrderBy(x=>x).ToArray();return(a[(a.Length-1)/2]+a[a.Length/2])/2;}
    }
}
#endif
