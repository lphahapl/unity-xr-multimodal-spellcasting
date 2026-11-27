using System;
using NarutoVR.SealCombat.Events;
using NarutoVR.SealCombat.Rpg;
using UnityEngine;
using UnityEngine.Events;

namespace NarutoVR.SceneArt
{
    /// <summary>Entering the gate with the tracked head loads an explicitly configured build scene.</summary>
    [AddComponentMenu("NarutoVR/剧情/场景传送门"), DisallowMultipleComponent, RequireComponent(typeof(BoxCollider))]
    public sealed class SceneTravelGate : MonoBehaviour
    {
        [InspectorName("目标场景"), Tooltip("填写构建场景列表中已勾选的目标场景路径。")]
        [SerializeField] private string destinationScene = "";
        [SerializeField, InspectorName("启用传送"), Tooltip("勾选后，玩家头部从门外进入触发区时开始加载目标场景。")] private bool loadEnabled;
        [InspectorName("最短等待时间（秒）"), Tooltip("从进门开始计时。填 0 跳过演出。大于 0 时，等待这段时间且进门演出结束，才允许激活目标场景。")]
        [SerializeField, Min(0f)] private float minimumDelaySeconds;
        [InspectorName("进门演出"), Tooltip("可选。拖入源场景的“剧情镜头序列”组件。最短等待时间大于 0 时，预加载一启动就播放。")]
        [SerializeField] private StoryCameraSequence presentation;
        [InspectorName("加载蒙版"), Tooltip("拖入带进度条的蒙版预制体。演出结束后显示，等待时间为 0 时立即显示。进度从 0 起步，至少显示 0.6 秒，在新场景达到 100% 后收起。")]
        [SerializeField] private SceneLoadingOverlay loadingOverlayPrefab;
        [InspectorName("开始传送时"), Tooltip("目标场景成功开始预加载时调用，可在这里绑定其他响应。")]
        [SerializeField] private UnityEvent onTravelStarted = new UnityEvent();
        [InspectorName("玩家头部"), Tooltip("拖入用于检测进门的头部节点。留空时使用当前启用的主相机。")]
        [SerializeField] private Transform playerHead;
        private BoxCollider volume;
        private float nextSample;
        private bool sampled, wasInside, loading, reportedInvalid;
        private IDisposable entrySubscription;
        public string DestinationScene => destinationScene;
        public bool LoadEnabled => loadEnabled;

        private void Awake() { volume = GetComponent<BoxCollider>(); }
        private void OnEnable()
        {
            sampled = false;
            wasInside = false;
            nextSample = 0f;
            entrySubscription = EventCenter.Subscribe(TravelEvents.GateEntered, OnGateEntered, this);
        }
        private void OnDisable() => entrySubscription?.Dispose();
        private void Reset() { GetComponent<BoxCollider>().isTrigger = true; }

        private void Update()
        {
            if (!loadEnabled) { sampled = false; wasInside = false; return; }
            if (loading || Time.unscaledTime < nextSample) return;
            nextSample = Time.unscaledTime + .05f;
            if (!HeadIsActive(playerHead))
            {
                var camera = Camera.main;
                playerHead = camera && camera.isActiveAndEnabled ? camera.transform : null;
                sampled = false;
                if (!playerHead) { nextSample += .95f; return; }
            }
            if (!volume || !volume.enabled) { sampled = false; return; }
            var local = volume.transform.InverseTransformPoint(playerHead.position) - volume.center;
            var half = volume.size * .5f;
            bool inside = Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y && Mathf.Abs(local.z) <= half.z;
            bool entered = sampled && inside && !wasInside;
            sampled = true; wasInside = inside;
            if (entered) EventCenter.Publish(TravelEvents.GateEntered, new GateEnteredNotice(this, minimumDelaySeconds));
        }

        private static bool HeadIsActive(Transform head)
        {
            if (!head || !head.gameObject.activeInHierarchy) return false;
            return !head.TryGetComponent<Camera>(out var camera) || camera.isActiveAndEnabled;
        }

        private void OnGateEntered(GateEnteredNotice notice)
        {
            if (notice.Gate != this || !loadEnabled || loading) return;
            string target = (destinationScene ?? "").Trim();
            if (target.Length > 0 && (target == gameObject.scene.path || target == gameObject.scene.name)) return;
            var view = playerHead ? playerHead.GetComponent<Camera>() : null;
            if (!LinearRpgController.TryCaptureGateDeparture(gameObject.scene, playerHead, target, out var entry, out var reason))
            {
                Debug.LogWarning("[场景传送门] " + reason, this);
                return;
            }
            var result = AsyncSceneLoader.TryLoad(target, notice.PresentationSeconds, loadingOverlayPrefab, presentation, view);
            loading = result == AsyncSceneLoader.Result.Started;
            if (loading)
            {
                LinearRpgController.PublishGateEntry(entry);
                onTravelStarted.Invoke();
            }
            if (result == AsyncSceneLoader.Result.InvalidDestination && !reportedInvalid)
            {
                Debug.LogWarning($"[场景传送门] 无法加载目标场景“{target}”。请填写目标场景路径，并在构建场景列表中勾选该场景。", this);
                reportedInvalid = true;
            }
        }
    }
}
