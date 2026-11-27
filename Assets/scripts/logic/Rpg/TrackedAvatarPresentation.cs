using System.Collections.Generic;
using Oculus.Interaction;
using UnityEngine;
using UnityEngine.Rendering;

namespace NarutoVR.SealCombat.Rpg
{
    // Mount on the model only. All tracked input, UI and locomotion stay on the local player.
    [DefaultExecutionOrder(410), RequireComponent(typeof(TrackedAvatarArms)), DisallowMultipleComponent]
    public sealed class TrackedAvatarPresentation : MonoBehaviour
    {
        [InspectorName("本地玩家")] public LinearRpgController player;
        [InspectorName("全身观众相机（单目）")] public Camera spectator;
        [InspectorName("头显躯干与袖子")] public SkinnedMeshRenderer[] firstPersonSkin = System.Array.Empty<SkinnedMeshRenderer>();
        [Range(0,.30f),InspectorName("眼睛相对躯干向前距离（米）")] public float eyesAheadOfTorso = .16f;
        [Tooltip("转头幅度超过此角度，估算的躯干才开始跟转。")]
        [Range(0,80)] public float torsoYawFreedom = 30;
        readonly List<Renderer> body = new(), localHands = new(), localBody = new();
        readonly Stack<RenderScope> activePasses = new(), reusable = new();
        bool hasHeading;
        RpgLocomotionController dodgeOwner;

        sealed class RenderScope
        {
            public readonly List<Renderer> changed = new();
            public Camera camera;
            public void Hide(List<Renderer> renderers)
            {
                foreach (var r in renderers)
                    if (r && !r.forceRenderingOff) { changed.Add(r); r.forceRenderingOff = true; }
            }
            public void Restore()
            {
                foreach (var r in changed) if (r) r.forceRenderingOff = false;
                changed.Clear();
                camera = null;
            }
        }

        void OnEnable()
        {
            GetComponentsInChildren(true, body);
            localBody.Clear();
            foreach (var skin in firstPersonSkin)
            {
                if (!skin) continue;
                body.Remove(skin);
                localBody.Add(skin);
                skin.enabled = player;
            }
            RenderPipelineManager.beginCameraRendering += BeginCamera;
            RenderPipelineManager.endCameraRendering += EndCamera;
            BindDodgeVisual();
        }
        void Start() => BindPlayerVisuals();
        void BindDodgeVisual()
        {
            if (!Application.isPlaying) return;
            var next = isActiveAndEnabled && player ? player.locomotion : null;
            if (dodgeOwner && dodgeOwner != next) dodgeOwner.ReleaseDodgeAvatar(this);
            dodgeOwner = next;
            if (dodgeOwner) dodgeOwner.BindDodgeAvatar(this);
        }
        public void BindPlayerVisuals()
        {
            BindDodgeVisual();
            localHands.Clear();
            if (!player) return;
            if (Application.isPlaying)
                foreach (var skin in firstPersonSkin) if (skin) skin.enabled = true;
            var lab = player.handTracking ? player.handTracking : player.skills ? player.skills.lab : null;
            if (!lab) return;
            GetComponent<TrackedAvatarArms>().source = lab;
            if (lab.xrHandsSource)
            {
                foreach (var visual in lab.xrHandsSource.GetComponentsInChildren<HandVisual>(true))
                    if (visual.transform.parent == lab.xrHandsSource.transform)
                        localHands.AddRange(visual.GetComponentsInChildren<SkinnedMeshRenderer>(true));
            }
            if (player.wristMenu && player.wristMenu.wornDevice)
                localHands.AddRange(player.wristMenu.wornDevice.GetComponentsInChildren<Renderer>(true));
        }
        void OnDisable()
        {
            if (dodgeOwner) dodgeOwner.ReleaseDodgeAvatar(this);
            dodgeOwner = null;
            RenderPipelineManager.beginCameraRendering -= BeginCamera;
            RenderPipelineManager.endCameraRendering -= EndCamera;
            while (activePasses.Count > 0) activePasses.Pop().Restore();
            foreach (var skin in firstPersonSkin) if (skin) skin.enabled = false;
            hasHeading = false;
        }
        void LateUpdate()
        {
            if (!player || !player.locomotion) return;
            var movement = player.locomotion;
            if (!movement.head || !movement.rigRoot) return;
            Vector3 feet = movement.head.position;
            feet.y = movement.rigRoot.position.y + movement.rigFloorOffset;
            Vector3 forward = Vector3.ProjectOnPlane(movement.head.forward, Vector3.up);
            if (forward.sqrMagnitude >= .02f)
            {
                float yaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
                float delta = Mathf.DeltaAngle(transform.eulerAngles.y, yaw);
                if (!hasHeading || Mathf.Abs(delta) > torsoYawFreedom)
                    transform.rotation = Quaternion.Euler(0, hasHeading ? yaw - Mathf.Sign(delta)*torsoYawFreedom : yaw, 0);
                hasHeading = true;
            }
            transform.position = feet - transform.forward * eyesAheadOfTorso;
        }

        void BeginCamera(ScriptableRenderContext context, Camera camera)
        {
            if (!player) return;
            var pass = reusable.Count > 0 ? reusable.Pop() : new RenderScope();
            // Restore the outer pass temporarily if a reflection or camera stack renders recursively.
            if (activePasses.TryPeek(out var outer))
                foreach (var r in outer.changed) if (r) r.forceRenderingOff = false;
            activePasses.Push(pass);
            pass.camera = camera;
            bool observer = (camera == spectator && !camera.stereoEnabled &&
                (!player.locomotion || !player.locomotion.head || !camera.transform.IsChildOf(player.locomotion.head))) ||
                camera.cameraType == CameraType.SceneView;
            pass.Hide(observer ? localHands : body);
            if (observer) pass.Hide(localBody);
        }
        void EndCamera(ScriptableRenderContext context, Camera camera)
        {
            if (!activePasses.TryPeek(out var pass) || pass.camera != camera) return;
            activePasses.Pop();
            pass.Restore();
            reusable.Push(pass);
            if (activePasses.TryPeek(out var outer))
                foreach (var r in outer.changed) if (r) r.forceRenderingOff = true;
        }
    }
}
