using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

namespace NarutoVR.SceneArt
{
    public static class AsyncSceneLoader
    {
        public enum Result
        {
            Started,
            Busy,
            InvalidDestination,
            AlreadyInScene,
            Failed
        }

        private static AsyncOperation pendingLoad;
        private static bool presentingTransition;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState()
        {
            pendingLoad = null;
            presentingTransition = false;
        }

        /// <summary>Call on the Unity main thread with a build scene name or asset path.</summary>
        public static Result TryLoad(string destinationScene) => TryLoad(destinationScene, 0f);

        /// <summary>Preload immediately and allow activation after the specified real-time delay.</summary>
        public static Result TryLoad(string destinationScene, float minimumDelaySeconds)
            => TryLoad(destinationScene, minimumDelaySeconds, null, null, null);

        internal static Result TryLoad(string destinationScene, float minimumDelaySeconds,
            SceneLoadingOverlay overlayPrefab, StoryCameraSequence presentation, Camera view)
        {
            if (presentingTransition || (pendingLoad != null && !pendingLoad.isDone)) return Result.Busy;

            string target = destinationScene?.Trim();
            if (string.IsNullOrEmpty(target) || !Application.CanStreamedLevelBeLoaded(target))
                return Result.InvalidDestination;

            var active = SceneManager.GetActiveScene();
            if (string.Equals(target, active.path, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(target, active.name, StringComparison.OrdinalIgnoreCase))
                return Result.AlreadyInScene;

            try
            {
                pendingLoad = SceneManager.LoadSceneAsync(target, LoadSceneMode.Single);
                if (pendingLoad != null)
                {
                    float delay = float.IsFinite(minimumDelaySeconds) ? Mathf.Max(0f, minimumDelaySeconds) : 0f;
                    if (delay > 0f || overlayPrefab)
                        PresentTransition(pendingLoad, delay, overlayPrefab, presentation, view);
                    return Result.Started;
                }
                Debug.LogError($"[场景加载] 无法开始加载目标场景“{target}”。");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            return Result.Failed;
        }

        private static async void PresentTransition(AsyncOperation operation, float seconds,
            SceneLoadingOverlay overlayPrefab, StoryCameraSequence presentation, Camera view)
        {
            var exitToken = Application.exitCancellationToken;
            double activateAt = Time.realtimeSinceStartupAsDouble + seconds;
            operation.allowSceneActivation = false;
            presentingTransition = true;
            SceneLoadingOverlay overlay = null;
            try
            {
                if (seconds > 0f && presentation && presentation.isActiveAndEnabled)
                {
                    var director = presentation.GetComponent<PlayableDirector>();
                    presentation.Play();
                    var manager = StoryCameraManager.Instance;
                    while (director && manager && manager.ActiveDirector == director)
                        await Awaitable.NextFrameAsync(exitToken);
                }
                else
                {
                    // A gate can reserve time for a presentation driven by onTravelStarted.
                    while (Time.realtimeSinceStartupAsDouble < activateAt)
                        await Awaitable.NextFrameAsync(exitToken);
                }

                if (overlayPrefab)
                {
                    overlay = UnityEngine.Object.Instantiate(overlayPrefab);
                    UnityEngine.Object.DontDestroyOnLoad(overlay.gameObject);
                    overlay.Show(view, operation);
                    // Give the outgoing camera a frame to draw the mask before activation.
                    await Awaitable.NextFrameAsync(exitToken);
                }
                while (Time.realtimeSinceStartupAsDouble < activateAt)
                    await Awaitable.NextFrameAsync(exitToken);
                operation.allowSceneActivation = true;
                while (overlay && !overlay.Finished)
                    await Awaitable.NextFrameAsync(exitToken);
                if (overlay) await Awaitable.NextFrameAsync(exitToken);
            }
            catch (OperationCanceledException) when (exitToken.IsCancellationRequested)
            {
                // Leaving Play mode must also release a preloaded scene.
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                operation.allowSceneActivation = true;
                if (overlay)
                {
                    overlay.gameObject.SetActive(false);
                    UnityEngine.Object.Destroy(overlay.gameObject);
                }
                presentingTransition = false;
            }
        }
    }
}
