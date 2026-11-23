using System;
using NarutoVR.SealCombat.Events;
using UnityEngine;
using UnityEngine.Playables;
using InputPoseDriver = UnityEngine.InputSystem.XR.TrackedPoseDriver;
using LegacyPoseDriver = UnityEngine.SpatialTracking.TrackedPoseDriver;

namespace NarutoVR.SceneArt
{
    [DisallowMultipleComponent, AddComponentMenu(""), DefaultExecutionOrder(10000)]
    public sealed class StoryCameraManager : MonoBehaviour
    {
        public static StoryCameraManager Instance { get; private set; }
        public PlayableDirector ActiveDirector => director;
        private IDisposable playSubscription;
        private PlayableDirector director;
        private Transform shotPose;
        private Camera controlledCamera;
        private Pose savedLocalPose;
        private DirectorWrapMode previousWrapMode;
        private InputPoseDriver inputDriverToRestore;
        private LegacyPoseDriver legacyDriverToRestore;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (!Instance) new GameObject(nameof(StoryCameraManager)).AddComponent<StoryCameraManager>();
        }

        private void Awake()
        {
            if (Instance && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            if (Instance == this)
                playSubscription = EventCenter.Subscribe(TravelEvents.StoryPlayRequested, OnPlayRequested, this);
        }

        private void OnPlayRequested(StoryPlayRequest request)
        {
            if (director && director.isActiveAndEnabled && controlledCamera && shotPose) return;
            Stop();
            var target = request.Director;
            Camera head = request.PlayerCamera ? request.PlayerCamera : Camera.main;
            if (!target || !target.isActiveAndEnabled || !target.playableAsset || !head || !head.isActiveAndEnabled ||
                !request.ShotPose || request.ShotPose.IsChildOf(head.transform))
            {
                Debug.LogWarning("[剧情镜头] 无法开始演出。请检查 Playable Director 是否已启用并指定 Timeline、玩家相机是否启用，以及剧情机位是否位于玩家相机的层级之外。", this);
                return;
            }

            var inputDriver = head.GetComponent<InputPoseDriver>();
            var legacyDriver = head.GetComponent<LegacyPoseDriver>();
            if (!inputDriver && !legacyDriver)
            {
                Debug.LogWarning("[剧情镜头] 玩家相机缺少头部姿态追踪组件。请将“玩家相机”绑定到带有 Tracked Pose Driver 的头显相机。", this);
                return;
            }

            director = target;
            shotPose = request.ShotPose;
            controlledCamera = head;
            savedLocalPose = new Pose(head.transform.localPosition, head.transform.localRotation);
            previousWrapMode = director.extrapolationMode;
            inputDriverToRestore = inputDriver && inputDriver.enabled ? inputDriver : null;
            legacyDriverToRestore = legacyDriver && legacyDriver.enabled ? legacyDriver : null;
            try
            {
                if (inputDriverToRestore) inputDriverToRestore.enabled = false;
                if (legacyDriverToRestore) legacyDriverToRestore.enabled = false;
                director.stopped += OnDirectorStopped;
                Application.onBeforeRender += ApplyShot;
                director.extrapolationMode = DirectorWrapMode.None;
                director.time = 0;
                director.Play();
                ApplyShot();
            }
            catch
            {
                Stop();
                throw;
            }
        }

        public void Stop()
        {
            if (ReferenceEquals(director, null)) return;
            try
            {
                if (director)
                {
                    director.stopped -= OnDirectorStopped;
                    if (director.playableGraph.IsValid()) director.Stop();
                }
            }
            finally
            {
                RestoreHeadTracking();
            }
        }

        private void OnDirectorStopped(PlayableDirector stopped)
        {
            if (stopped == director) RestoreHeadTracking();
        }

        private void OnDisable()
        {
            playSubscription?.Dispose();
            Stop();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void LateUpdate()
        {
            if (ReferenceEquals(director, null)) return;
            if (!director || !director.isActiveAndEnabled || !director.playableGraph.IsValid() ||
                !controlledCamera || !controlledCamera.isActiveAndEnabled || !shotPose)
                Stop();
            else
                ApplyShot();
        }

        [BeforeRenderOrder(10000)]
        private void ApplyShot()
        {
            if (controlledCamera && shotPose)
                controlledCamera.transform.SetPositionAndRotation(shotPose.position, shotPose.rotation);
        }

        private void RestoreHeadTracking()
        {
            Application.onBeforeRender -= ApplyShot;
            if (director)
            {
                director.stopped -= OnDirectorStopped;
                director.extrapolationMode = previousWrapMode;
            }
            if (controlledCamera)
                controlledCamera.transform.SetLocalPositionAndRotation(savedLocalPose.position, savedLocalPose.rotation);
            if (legacyDriverToRestore) legacyDriverToRestore.enabled = true;
            if (inputDriverToRestore) inputDriverToRestore.enabled = true;
            controlledCamera = null;
            legacyDriverToRestore = null;
            inputDriverToRestore = null;
            director = null;
            shotPose = null;
        }
    }
}
