using System;
using UnityEngine;

namespace NarutoVR.SceneArt
{
    /// <summary>Shares the viewer position with all grass patches and culls whole patches at distance.</summary>
    [DisallowMultipleComponent]
    public sealed class OpeningGrassWind : MonoBehaviour
    {
        private static readonly int ViewerProperty = Shader.PropertyToID("_OpeningGrassViewer");
        private const float VisibilityInterval = 0.2f;
        private const float TeleportDistanceSquared = 4f;
        private const float EnableDistanceSquared = 18f * 18f;
        private const float DisableDistanceSquared = 19f * 19f;

        [SerializeField] private MeshRenderer[] patches = Array.Empty<MeshRenderer>();
        [Tooltip("Optional center-eye transform. Defaults to the active MainCamera.")]
        [SerializeField] private Transform viewerOverride;

        private Camera mainCamera;
        private Transform sampledViewer;
        private Vector3 sampledPosition;
        private float nextVisibilityCheck;
        private float nextCameraSearch;

        public void Configure(MeshRenderer[] patchRenderers, Transform explicitViewer = null)
        {
            RestorePatchRendering();
            patches = patchRenderers ?? Array.Empty<MeshRenderer>();
            viewerOverride = explicitViewer;
            if (Application.isPlaying && isActiveAndEnabled) RefreshVisibility();
        }

        private void OnEnable()
        {
            nextCameraSearch = 0f;
            RefreshVisibility();
        }

        private void LateUpdate() => UpdateViewer(false);

        private void OnDisable() => ClearViewer();

        /// <summary>Forces a distance check before the next scheduled sample.</summary>
        public void RefreshVisibility()
        {
            if (isActiveAndEnabled) UpdateViewer(true);
        }

        private void UpdateViewer(bool forceVisibilityCheck)
        {
            float now = Time.unscaledTime;
            Transform viewer = ResolveViewer(now);
            if (!viewer)
            {
                if (sampledViewer || nextVisibilityCheck > 0f) ClearViewer();
                return;
            }

            Vector3 position = viewer.position;
            Shader.SetGlobalVector(ViewerProperty, new Vector4(position.x, position.y, position.z, 1f));
            if (!forceVisibilityCheck && viewer == sampledViewer && now < nextVisibilityCheck &&
                (position - sampledPosition).sqrMagnitude < TeleportDistanceSquared) return;

            sampledViewer = viewer;
            sampledPosition = position;
            nextVisibilityCheck = now + VisibilityInterval;
            foreach (MeshRenderer patch in patches)
            {
                if (!patch) continue;
                float distanceSquared = patch.bounds.SqrDistance(position);
                bool cull = patch.forceRenderingOff
                    ? distanceSquared > EnableDistanceSquared
                    : distanceSquared > DisableDistanceSquared;
                if (cull != patch.forceRenderingOff) patch.forceRenderingOff = cull;
            }
        }

        private Transform ResolveViewer(float now)
        {
            if (viewerOverride && viewerOverride.gameObject.activeInHierarchy) return viewerOverride;
            if (mainCamera && mainCamera.isActiveAndEnabled) return mainCamera.transform;
            if (now < nextCameraSearch) return null;

            nextCameraSearch = now + VisibilityInterval;
            mainCamera = Camera.main;
            return mainCamera && mainCamera.isActiveAndEnabled ? mainCamera.transform : null;
        }

        private void ClearViewer()
        {
            sampledViewer = null;
            nextVisibilityCheck = 0f;
            Shader.SetGlobalVector(ViewerProperty, Vector4.zero);
            RestorePatchRendering();
        }

        private void RestorePatchRendering()
        {
            foreach (MeshRenderer patch in patches)
                if (patch && patch.forceRenderingOff) patch.forceRenderingOff = false;
        }
    }
}
