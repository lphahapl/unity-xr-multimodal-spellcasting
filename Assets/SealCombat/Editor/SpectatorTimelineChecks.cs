#if UNITY_EDITOR
using System;
using Unity.Cinemachine;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;

namespace NarutoVR.SealCombat.Editor
{
    public static class SpectatorTimelineChecks
    {
        public static int Run()
        {
            var scene = EditorSceneManager.NewPreviewScene();
            var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            GameObject NewObject(string name)
            {
                var obj = new GameObject(name);
                SceneManager.MoveGameObjectToScene(obj, scene);
                return obj;
            }
            try
            {
                var head = NewObject("XR head pose proxy").transform;
                head.position = new Vector3(1, 1.7f, 3);
                var initial = head.localToWorldMatrix;
                var output = NewObject("Spectator output");
                var camera = output.AddComponent<Camera>();
                camera.stereoTargetEye = StereoTargetEyeMask.None;
                camera.GetUniversalAdditionalCameraData().allowXRRendering = false;
                var brain = output.AddComponent<CinemachineBrain>();
                var director = NewObject("Shot sequence").AddComponent<PlayableDirector>();
                var track = timeline.CreateTrack<CinemachineTrack>(null, "Spectator");
                CinemachineCamera AddShot(int index, Vector3 position)
                {
                    var shotCamera = NewObject("Shot " + index).AddComponent<CinemachineCamera>();
                    shotCamera.transform.position = position;
                    var clip = track.CreateClip<CinemachineShot>();
                    clip.start = index; clip.duration = 1;
                    ((CinemachineShot)clip.asset).VirtualCamera.defaultValue = shotCamera;
                    return shotCamera;
                }
                var a = AddShot(0, new Vector3(-4, 2, 1));
                var b = AddShot(1, new Vector3(5, 3, 4));
                director.playableAsset = timeline;
                director.SetGenericBinding(track, brain);
                void Check(double time, CinemachineCamera shot)
                {
                    director.time = time;
                    director.Evaluate();
                    brain.ManualUpdate();
                    if ((camera.transform.position - shot.transform.position).sqrMagnitude > .0001f ||
                        head.localToWorldMatrix != initial || camera.GetUniversalAdditionalCameraData().allowXRRendering)
                        throw new InvalidOperationException("观众镜头切换或头姿隔离失败，时间 " + time);
                }
                Check(.5, a);
                Check(1.5, b);
                director.Stop();
                return 2;
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
                UnityEngine.Object.DestroyImmediate(timeline);
            }
        }
    }
}
#endif
