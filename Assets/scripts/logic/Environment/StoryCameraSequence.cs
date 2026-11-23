using NarutoVR.SealCombat.Events;
using UnityEngine;
using UnityEngine.Playables;

namespace NarutoVR.SceneArt
{
    [AddComponentMenu("NarutoVR/剧情/剧情镜头序列"), DisallowMultipleComponent, RequireComponent(typeof(PlayableDirector))]
    public sealed class StoryCameraSequence : MonoBehaviour
    {
        [InspectorName("玩家相机"), Tooltip("拖入玩家头显的 CenterEyeAnchor 相机。留空时使用当前启用的主相机。")]
        [SerializeField] private Camera playerCamera;
        [InspectorName("剧情机位"), Tooltip("拖入由 Timeline 或 Cinemachine Brain 控制的机位物体。演出时，玩家相机会跟随它的位置和旋转。")]
        [SerializeField] private Transform shotPose;

        public void Play()
        {
            if (!isActiveAndEnabled) return;
            var request = new StoryPlayRequest(GetComponent<PlayableDirector>(), shotPose, playerCamera);
            EventCenter.Publish(TravelEvents.StoryPlayRequested, request);
        }

        public void Stop()
        {
            var manager = StoryCameraManager.Instance;
            if (manager && manager.ActiveDirector == GetComponent<PlayableDirector>()) manager.Stop();
        }

        private void OnDisable() => Stop();
    }
}
