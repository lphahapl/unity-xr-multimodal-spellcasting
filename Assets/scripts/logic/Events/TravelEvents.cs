using NarutoVR.SceneArt;
using UnityEngine;
using UnityEngine.Playables;

namespace NarutoVR.SealCombat.Events
{
    public readonly struct GateEnteredNotice
    {
        public readonly SceneTravelGate Gate;
        public readonly float PresentationSeconds;

        public GateEnteredNotice(SceneTravelGate gate, float presentationSeconds = 0f)
        {
            Gate = gate;
            PresentationSeconds = float.IsFinite(presentationSeconds) && presentationSeconds > 0f
                ? presentationSeconds : 0f;
        }
    }

    public readonly struct StoryPlayRequest
    {
        public readonly PlayableDirector Director;
        public readonly Transform ShotPose;
        public readonly Camera PlayerCamera;

        public StoryPlayRequest(PlayableDirector director, Transform shotPose, Camera playerCamera = null)
        {
            Director = director;
            ShotPose = shotPose;
            PlayerCamera = playerCamera;
        }
    }

    public static class TravelEvents
    {
        public static readonly EventKey<GateEnteredNotice> GateEntered = new("玩家进入传送门");
        public static readonly EventKey<StoryPlayRequest> StoryPlayRequested = new("请求播放剧情镜头");
    }
}
