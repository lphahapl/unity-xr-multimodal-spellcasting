using UnityEngine;
using UnityEngine.Rendering;

namespace NarutoVR.SealCombat.Rpg
{
    // The visual samples the installed Meta OpenXR right-hand model and pinch pose. It never drives tracked joints.
    internal sealed class QuestGestureHand
    {
        readonly Transform root, wrist, indexTip, thumbTip, middleBase;
        readonly Transform[] bones;
        readonly Quaternion[] open, pinch;
        readonly Vector3[] lengths;
        readonly AnimationClip defaultPose;
        AnimationClip sampledPose;

        public QuestGestureHand(Transform parent, GameObject model, AnimationClip closedPose, Material material)
        {
            root = Object.Instantiate(model, parent).transform;
            root.name = "任务手势 · 右手示范";
            var skin = root.GetComponentInChildren<SkinnedMeshRenderer>(true);
            bones = skin ? skin.bones : System.Array.Empty<Transform>();
            open = new Quaternion[bones.Length];
            pinch = new Quaternion[bones.Length];
            lengths = new Vector3[bones.Length];
            defaultPose = closedPose;
            foreach (var animator in root.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
            foreach (var bone in root.GetComponentsInChildren<Transform>(true))
            {
                switch (bone.name)
                {
                    case "XRHand_Wrist": wrist = bone; break;
                    case "XRHand_IndexTip": indexTip = bone; break;
                    case "XRHand_ThumbTip": thumbTip = bone; break;
                    case "XRHand_MiddleProximal": middleBase = bone; break;
                }
            }
            for (int i = 0; i < bones.Length; i++)
            {
                open[i] = pinch[i] = bones[i].localRotation;
                lengths[i] = bones[i].localPosition;
            }
            if (wrist && indexTip)
            {
                float span = Vector3.Distance(wrist.position,indexTip.position);
                if (span > .001f) root.localScale *= .17f/span;
            }
            if (skin)
            {
                skin.sharedMaterial = material;
                skin.shadowCastingMode = ShadowCastingMode.Off;
                skin.receiveShadows = false;
                skin.updateWhenOffscreen = true;
            }
            if (!skin || !wrist || !thumbTip || !indexTip || !middleBase)
                Debug.LogWarning("示范手需要带蒙皮的 OpenXR 手骨骼。", model);
            root.gameObject.SetActive(false);
        }

        void SamplePose(AnimationClip pose)
        {
            if (pose == sampledPose) return;
            sampledPose = pose;
            for (int i = 0; i < bones.Length; i++) bones[i].localRotation = open[i];
            if (pose) pose.SampleAnimation(wrist.parent.gameObject, 0f);
            for (int i = 0; i < bones.Length; i++)
            {
                pinch[i] = bones[i].localRotation;
                // Keep the imported bind-pose lengths even when a clip contains translations in metres.
                bones[i].localPosition = lengths[i];
            }
        }

        public void Show(QuestGestureHint hint, Transform head, float t)
        {
            if (!wrist || !indexTip || !thumbTip || !middleBase) return;
            root.gameObject.SetActive(true);
            var motion = hint.motion;
            SamplePose(motion.pose ? motion.pose : defaultPose);
            float approach = Mathf.SmoothStep(0,1,Mathf.InverseLerp(.04f,.35f,t));
            float action = Mathf.SmoothStep(0,1,Mathf.InverseLerp(.46f,.75f,t));
            float release = Mathf.SmoothStep(0,1,Mathf.InverseLerp(.78f,.97f,t));
            float close = motion.grip switch {
                QuestGestureMotion.Grip.Hold => 1f,
                QuestGestureMotion.Grip.Release => 1f-release,
                _ => Mathf.SmoothStep(0,1,Mathf.InverseLerp(.22f,.46f,t))*(1f-release)
            };
            for (int i = 0; i < bones.Length; i++) bones[i].localRotation = Quaternion.Slerp(open[i],pinch[i],close);

            var forward = Vector3.ProjectOnPlane(hint.target.position-head.position,Vector3.up).normalized;
            if (forward.sqrMagnitude < .1f) forward = Vector3.ProjectOnPlane(head.forward,Vector3.up).normalized;
            if (forward.sqrMagnitude < .1f) forward = Vector3.forward;
            var aim = (forward-Vector3.up*.28f).normalized;
            var alongHand = (middleBase.position-wrist.position).normalized;
            var handNormal = Vector3.Cross(thumbTip.position-wrist.position,alongHand).normalized;
            bool useFrame = motion.orientToFrame && hint.frame;
            var aimRotation = useFrame ? hint.frame.rotation : Quaternion.LookRotation(aim, Vector3.up);
            var rotation = aimRotation * Quaternion.Euler(motion.handEuler) * Quaternion.Inverse(Quaternion.LookRotation(alongHand,handNormal));
            root.rotation = rotation * root.rotation;

            var approachFrame = useFrame ? hint.frame.rotation : Quaternion.LookRotation(forward);
            var contact = hint.ContactAt(action) + approachFrame * motion.approachOffset * (1f-approach+release);
            var tip = motion.contact switch {
                QuestGestureMotion.Contact.IndexTip => indexTip.position,
                QuestGestureMotion.Contact.Wrist => wrist.position,
                _ => (indexTip.position + thumbTip.position) * .5f
            };
            root.position += contact - tip;
        }

        public void Hide()
        {
            if (root.gameObject.activeSelf) root.gameObject.SetActive(false);
        }
        public void Dispose() => Object.Destroy(root.gameObject);
    }
}
