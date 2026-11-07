#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;

namespace NarutoVR.SealCombat.Editor
{
    [InitializeOnLoad]
    internal static class EmptyStreamProbe
    {
        const string Key="NarutoVR.EmptyStreamProbe", PathName="Assets/Scene/EmptyStreamProbe.unity";
        const string Result="Logs/empty-stream-result.json";
        static readonly List<double> times=new();
        static int xrFrames, focusedFrames, frame=-1;
        static double start,firstSample;
        static StreamHandLines hands;
        [Serializable] class Report {
            public string utcStart,utcEnd,scene,device;
            public int samples,xrActiveSamples,focusedSamples,above120ms,handUpdates,bothVisibleUpdates;
            public double medianMs,p95Ms;
        }
        static EmptyStreamProbe() {
            EditorApplication.playModeStateChanged+=state=>{
                if(!SessionState.GetBool(Key,false))return;
                if(state==PlayModeStateChange.EnteredPlayMode) {
                    times.Clear();xrFrames=focusedFrames=0;frame=-1;start=Time.realtimeSinceStartupAsDouble;
                    hands=SessionState.GetBool(Key+".hands",false)?new StreamHandLines(Camera.main?Camera.main.transform.parent:null):null;
                    EditorApplication.update-=Sample;EditorApplication.update+=Sample;
                }
                if(state==PlayModeStateChange.ExitingPlayMode){hands?.Detach();}
                if(state==PlayModeStateChange.EnteredEditMode) {
                    EditorApplication.update-=Sample;
                    string previous=SessionState.GetString(Key+".previous","");
                    EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(previous);
                    SessionState.EraseBool(Key);
                }
            };
        }
        public static void Begin(bool drawHands=false) {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("需要编辑模式");
            if(!File.Exists(PathName)) {
                var active=SceneManager.GetActiveScene();
                var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
                try {
                    SceneManager.SetActiveScene(scene);
                    var go=new GameObject("XR相机"); go.tag="MainCamera";
                    var cam=go.AddComponent<Camera>();cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.10f,.13f,.17f);
                    var pose=go.AddComponent<TrackedPoseDriver>();
                    pose.positionInput=new InputActionProperty(new InputAction(type:InputActionType.Value,binding:"<XRHMD>/centerEyePosition"));
                    pose.rotationInput=new InputActionProperty(new InputAction(type:InputActionType.Value,binding:"<XRHMD>/centerEyeRotation"));
                    pose.ignoreTrackingState=true;
                    var marker=GameObject.CreatePrimitive(PrimitiveType.Cube);marker.name="静止参照物";marker.transform.position=new Vector3(0,1.4f,3);marker.transform.localScale=Vector3.one*.4f;
                    var light=new GameObject("照明").AddComponent<Light>();light.type=LightType.Directional;light.transform.rotation=Quaternion.Euler(35,-25,0);
                    if(!EditorSceneManager.SaveScene(scene,PathName))throw new IOException(PathName);
                } finally {SceneManager.SetActiveScene(active);EditorSceneManager.CloseScene(scene,true);}
            }
            SessionState.SetString(Key+".previous",AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene)??"");
            EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(PathName);
            SessionState.SetBool(Key+".hands",drawHands);SessionState.SetBool(Key,true); EditorApplication.isPlaying=true;
        }
        static DateTime first;
        static void Sample() {
            if(!EditorApplication.isPlaying || frame==Time.frameCount)return;
            frame=Time.frameCount;hands?.Poll();
            double elapsed=Time.realtimeSinceStartupAsDouble-start;
            if(elapsed<5)return;
            if(times.Count==0){first=DateTime.UtcNow;firstSample=elapsed;}
            times.Add(1000.0*Time.unscaledDeltaTime);
            if(XRSettings.isDeviceActive)xrFrames++;
            if(Application.isFocused)focusedFrames++;
            if(elapsed-firstSample<30)return;
            EditorApplication.update-=Sample;times.Sort();
            var result=new Report {utcStart=first.ToString("O"),utcEnd=DateTime.UtcNow.ToString("O"),scene=SceneManager.GetActiveScene().path,device=XRSettings.loadedDeviceName,
                handUpdates=hands?.Updates??0,bothVisibleUpdates=hands?.BothVisible??0,samples=times.Count,xrActiveSamples=xrFrames,focusedSamples=focusedFrames,above120ms=times.Count(t=>t>120),
                medianMs=times[times.Count/2],p95Ms=times[(int)((times.Count-1)*.95)]};
            File.WriteAllText(hands==null?Result:"Logs/hand-stream-result.json",JsonUtility.ToJson(result,true));EditorApplication.isPlaying=false;
        }
    }
}
#endif
