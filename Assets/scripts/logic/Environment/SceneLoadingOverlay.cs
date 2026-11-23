using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NarutoVR.SceneArt
{
    [AddComponentMenu("NarutoVR/剧情/场景加载蒙版"), DisallowMultipleComponent, RequireComponent(typeof(Canvas))]
    public sealed class SceneLoadingOverlay : MonoBehaviour
    {
        // Older overlay prefabs have direct references instead of a nested progress bar.
        [SerializeField, HideInInspector] private Image progressFill;
        [SerializeField, HideInInspector] private Text percentText;

        private const float MinimumVisibleSeconds = 0.6f;
        private Canvas surface;
        private AsyncOperation operation;
        private int lastPercent = -1;
        private double shownAt, lastSample;
        private float displayedProgress;
        internal bool Finished { get; private set; }

        internal void Show(Camera view, AsyncOperation load)
        {
            var bar = GetComponentInChildren<SceneLoadingProgressBar>(true);
            if (bar)
            {
                progressFill = bar.Fill;
                percentText = bar.Percentage;
            }
            operation = load;
            surface = GetComponent<Canvas>();
            surface.renderMode = RenderMode.ScreenSpaceCamera;
            surface.worldCamera = view && view.isActiveAndEnabled ? view : Camera.main;
            shownAt = lastSample = Time.realtimeSinceStartupAsDouble;
            enabled = true;
            surface.enabled = true;
            gameObject.SetActive(true);
            SceneManager.sceneLoaded += OnSceneLoaded;
            Display(0f);
        }

        private void LateUpdate()
        {
            if (operation == null) return;
            if (!surface.worldCamera || !surface.worldCamera.isActiveAndEnabled)
                surface.worldCamera = Camera.main;
            double now = Time.realtimeSinceStartupAsDouble;
            float ramp = Mathf.Clamp01((float)(now - shownAt) / MinimumVisibleSeconds);
            float target = operation.isDone ? 1f : Mathf.Clamp01(operation.progress);
            float next = lastSample < shownAt + MinimumVisibleSeconds
                ? Mathf.Lerp(0f, target, ramp)
                : Mathf.MoveTowards(displayedProgress, target, (float)(now - lastSample) / MinimumVisibleSeconds);
            Display(Mathf.Max(displayedProgress, next));
            lastSample = now;
            Finished = operation.isDone && ramp >= 1f && displayedProgress >= 1f;
        }

        private void Display(float value)
        {
            displayedProgress = value;
            if (progressFill) progressFill.fillAmount = displayedProgress;
            int percent = Mathf.FloorToInt(displayedProgress * 100f);
            if (percentText && percent != lastPercent) percentText.text = $"{percent}%";
            lastPercent = percent;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!surface.worldCamera || !surface.worldCamera.isActiveAndEnabled)
                surface.worldCamera = Camera.main;
        }

        private void OnDestroy() => SceneManager.sceneLoaded -= OnSceneLoaded;
    }
}
