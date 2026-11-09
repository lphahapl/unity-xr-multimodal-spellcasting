using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NarutoVR.SceneArt
{
    /// <summary>Entering the gate with the tracked head loads an explicitly configured build scene.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(BoxCollider))]
    public sealed class SceneTravelGate : MonoBehaviour
    {
        [Tooltip("Configure an enabled Build Settings scene path, then enable scene loading.")]
        [SerializeField] private string destinationScene = "";
        [SerializeField] private bool loadEnabled;
        [Tooltip("Optional XR head. If unassigned, the active MainCamera is used.")]
        [SerializeField] private Transform playerHead;
        private BoxCollider volume;
        private float nextSample;
        private bool sampled, wasInside, loading, reportedInvalid;
        public string DestinationScene => destinationScene;
        public bool LoadEnabled => loadEnabled;

        private void Awake() { volume = GetComponent<BoxCollider>(); }
        private void OnEnable() { sampled = false; wasInside = false; nextSample = 0f; }
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
            if (entered) TryTravel();
        }

        private static bool HeadIsActive(Transform head)
        {
            if (!head || !head.gameObject.activeInHierarchy) return false;
            return !head.TryGetComponent<Camera>(out var camera) || camera.isActiveAndEnabled;
        }

        private void TryTravel()
        {
            string target = (destinationScene ?? "").Trim();
            if (target.Length == 0 || !Application.CanStreamedLevelBeLoaded(target))
            {
                if (!reportedInvalid) Debug.LogWarning("[SceneTravelGate] Destination is unset or absent from enabled Build Settings.", this);
                reportedInvalid = true;
                return;
            }
            if (target == gameObject.scene.path || target == gameObject.scene.name) return;
            loading = true;
            try
            {
                var operation = SceneManager.LoadSceneAsync(target, LoadSceneMode.Single);
                if (operation == null) loading = false;
            }
            catch (Exception exception)
            {
                loading = false;
                Debug.LogException(exception, this);
            }
        }
    }
}
