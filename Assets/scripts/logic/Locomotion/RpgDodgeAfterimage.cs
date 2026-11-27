using System;
using System.Collections.Generic;
using NarutoVR.SealCombat.Enemies;
using NarutoVR.SealCombat.Items;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace NarutoVR.SealCombat.Rpg
{
    [DisallowMultipleComponent]
    public sealed class RpgDodgeAfterimage : MonoBehaviour
    {
        static readonly int Opacity = Shader.PropertyToID("_Opacity");
        static readonly int InkShape = Shader.PropertyToID("_InkShape");
        static readonly int GroundY = Shader.PropertyToID("_GroundY");
        const float MaximumSpread = 4.6f;
        // Each segment follows one real Humanoid bone. End joints are used to attach the
        // simplified outer surface when the imported mesh has CPU read/write disabled.
        static readonly (HumanBodyBones start, HumanBodyBones end)[] Segments =
        {
            (HumanBodyBones.Hips, HumanBodyBones.Spine), (HumanBodyBones.Spine, HumanBodyBones.Chest),
            (HumanBodyBones.Chest, HumanBodyBones.Neck), (HumanBodyBones.Neck, HumanBodyBones.Head),
            (HumanBodyBones.Head, HumanBodyBones.LastBone),
            (HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm), (HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand),
            (HumanBodyBones.LeftHand, HumanBodyBones.LeftMiddleProximal),
            (HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm), (HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand),
            (HumanBodyBones.RightHand, HumanBodyBones.RightMiddleProximal),
            (HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg), (HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot),
            (HumanBodyBones.LeftFoot, HumanBodyBones.LeftToes),
            (HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg), (HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot),
            (HumanBodyBones.RightFoot, HumanBodyBones.RightToes)
        };

        struct Joint
        {
            public Transform bone;
            public Vector3 start, delta;
            public float inverseLengthSquared;
            public Matrix4x4 bindInverse, frozen;
        }

        struct Vertex
        {
            public Vector3 firstLocal, secondLocal;
            public int first, second;
            public float secondWeight;
        }

        Animator avatar;
        TrackedAvatarPresentation presentation;
        readonly Joint[] joints = new Joint[Segments.Length];
        Vertex[] vertices;
        Vector3[] posed;
        int[] washGroup;
        Vector3[] washCenters;
        int[] washCounts;
        float spacingAtBind, scaleAtBind, frozenSpacing;
        Mesh silhouette;
        MeshRenderer imprint;
        Material ink;
        MaterialPropertyBlock properties;
        Vector3 anchor;
        Quaternion heading;
        bool captured, registered;
        double started;
        float duration;

        internal bool Bind(Animator fullBody, TrackedAvatarPresentation display)
        {
            if (fullBody && fullBody == avatar && presentation == display && silhouette && ink)
            {
                registered = true;
                return true;
            }
            Clear();
            registered = false;
            if (silhouette) Destroy(silhouette);
            silhouette = null;
            avatar = null;
            vertices = null;
            posed = null;
            washGroup = null;
            washCenters = null;
            washCounts = null;
            if (!fullBody || !fullBody.avatar || !fullBody.avatar.isValid || !fullBody.isHuman ||
                fullBody.GetComponentInParent<EnemyController>() || fullBody.GetComponentInParent<GameItemBase>()) return false;

            var head = fullBody.GetBoneTransform(HumanBodyBones.Head);
            var hips = fullBody.GetBoneTransform(HumanBodyBones.Hips);
            var foot = fullBody.GetBoneTransform(HumanBodyBones.LeftFoot);
            var leftHand = fullBody.GetBoneTransform(HumanBodyBones.LeftHand);
            var rightHand = fullBody.GetBoneTransform(HumanBodyBones.RightHand);
            if (!head || !hips || !foot) return false;
            var model = fullBody.transform;
            float stature = Vector3.Distance(head.position, hips.position) + Vector3.Distance(hips.position, foot.position);
            if (!(stature > .01f)) return false;
            for (int i = 0; i < joints.Length; i++)
            {
                var (start, end) = Segments[i];
                var bone = fullBody.GetBoneTransform(start);
                var tip = end == HumanBodyBones.LastBone ? null : fullBody.GetBoneTransform(end);
                Vector3 direction = start == HumanBodyBones.Head ? model.up : model.forward;
                Vector3 delta = !bone ? Vector3.zero : tip ? tip.position - bone.position : direction * (.08f * stature);
                joints[i] = new Joint
                {
                    bone = bone, start = bone ? bone.position : default, delta = delta,
                    inverseLengthSquared = 1f / Mathf.Max(delta.sqrMagnitude, 1e-8f),
                    bindInverse = bone ? bone.worldToLocalMatrix : default
                };
            }

            // Once per model/equipment binding. BakeMesh also works for non-readable imported FBXs.
            // Cluster the actual skinned triangles in model-oriented space. No textures, UVs,
            // facial shading, individual material draws or source renderers go into the imprint.
            var cells = new Dictionary<Vector3Int, int>();
            var sums = new List<Vector3>();
            var counts = new List<int>();
            var triangles = new List<int>();
            var unique = new HashSet<(int, int, int)>();
            var points = new List<Vector3>();
            var indices = new List<int>();
            var remap = Array.Empty<int>();
            var scratch = new Mesh();
            var frame = Matrix4x4.TRS(model.position, model.rotation, Vector3.one);
            var worldToGrid = frame.inverse;
            float cellSize = stature * .022f;
            try
            {
                foreach (var skin in fullBody.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    if (!skin.enabled || !skin.sharedMesh || skin.GetComponentInParent<Animator>() != fullBody ||
                        (display && display.firstPersonSkin != null && Array.IndexOf(display.firstPersonSkin, skin) >= 0) ||
                        skin.GetComponentInParent<GameItemBase>() || skin.GetComponentInParent<Canvas>() ||
                        skin.GetComponentInParent<Oculus.Interaction.HandVisual>()) continue;
                    // Held props may have a renderer under a wrist, or an independent skeleton
                    // under the same model. Neither is a character skin.
                    if ((leftHand && skin.transform.IsChildOf(leftHand)) || (rightHand && skin.transform.IsChildOf(rightHand))) continue;
                    bool deformsWithAvatar = false;
                    foreach (var bone in skin.bones)
                        if (bone && bone.IsChildOf(hips)) { deformsWithAvatar = true; break; }
                    if (!deformsWithAvatar) continue;
                    // Bake in the skin Transform's local units. The renderer matrix can
                    // instead be relative to its root bone on an imported Humanoid.
                    skin.BakeMesh(scratch, true);
                    scratch.GetVertices(points);
                    if (remap.Length < points.Count) remap = new int[points.Count];
                    var toGrid = worldToGrid * skin.transform.localToWorldMatrix;
                    for (int i = 0; i < points.Count; i++)
                    {
                        Vector3 point = toGrid.MultiplyPoint3x4(points[i]);
                        var key = Vector3Int.RoundToInt(point / cellSize);
                        if (!cells.TryGetValue(key, out int id))
                        {
                            id = sums.Count;
                            cells.Add(key, id);
                            sums.Add(default);
                            counts.Add(0);
                        }
                        sums[id] += point;
                        counts[id]++;
                        remap[i] = id;
                    }
                    for (int sub = 0; sub < scratch.subMeshCount; sub++)
                    {
                        if (scratch.GetTopology(sub) != MeshTopology.Triangles) continue;
                        scratch.GetTriangles(indices, sub);
                        for (int i = 0; i < indices.Count; i += 3)
                        {
                            int a = remap[indices[i]], b = remap[indices[i + 1]], c = remap[indices[i + 2]];
                            if (a == b || b == c || c == a) continue;
                            int low = Mathf.Min(a, Mathf.Min(b, c)), high = Mathf.Max(a, Mathf.Max(b, c));
                            if (!unique.Add((low, a + b + c - low - high, high))) continue;
                            triangles.Add(a); triangles.Add(b); triangles.Add(c);
                        }
                    }
                }
            }
            finally { Destroy(scratch); }
            if (triangles.Count == 0) return false;

            var bound = new Vertex[sums.Count];
            var coverage = new HashSet<HumanBodyBones>();
            // One ink sample per coarse surface cell. Four coincident vertices form
            // an eye-facing Gaussian disc in the shader; neighbours blend into one wash.
            var groupByCell = new Dictionary<Vector3Int, int>();
            var populations = new List<int>();
            var groups = new int[bound.Length];
            spacingAtBind = cellSize * 2.35f;
            scaleAtBind = Mathf.Max(model.lossyScale.magnitude, 1e-6f);
            for (int i = 0; i < bound.Length; i++)
            {
                var localPoint = sums[i] / counts[i];
                var point = frame.MultiplyPoint3x4(localPoint);
                bound[i] = Attach(point);
                coverage.Add(Segments[bound[i].first].start);
                var cell = Vector3Int.RoundToInt(localPoint / spacingAtBind);
                if (!groupByCell.TryGetValue(cell, out int group))
                {
                    group = populations.Count;
                    groupByCell.Add(cell, group);
                    populations.Add(0);
                }
                groups[i] = group;
                populations[group]++;
            }
            if (!coverage.Contains(HumanBodyBones.Head) ||
                !(coverage.Contains(HumanBodyBones.Hips) || coverage.Contains(HumanBodyBones.Spine) || coverage.Contains(HumanBodyBones.Chest)) ||
                !(coverage.Contains(HumanBodyBones.LeftLowerLeg) || coverage.Contains(HumanBodyBones.RightLowerLeg))) return false;

            avatar = fullBody;
            presentation = display;
            vertices = bound;
            washGroup = groups;
            washCounts = populations.ToArray();
            washCenters = new Vector3[washCounts.Length];
            posed = new Vector3[bound.Length + 4 * washCounts.Length];
            var corners = new Vector3[posed.Length];
            for (int group = 0; group < washCounts.Length; group++)
            {
                int k = bound.Length + 4 * group;
                float density = Mathf.Clamp(Mathf.Sqrt(washCounts[group]) * .58f, .48f, 1.4f);
                corners[k]     = new Vector3(-1, -1, density);
                corners[k + 1] = new Vector3( 1, -1, density);
                corners[k + 2] = new Vector3( 1,  1, density);
                corners[k + 3] = new Vector3(-1,  1, density);
                triangles.Add(k); triangles.Add(k + 1); triangles.Add(k + 2);
                triangles.Add(k); triangles.Add(k + 2); triangles.Add(k + 3);
            }
            silhouette = new Mesh { name = "玩家全身 · 水墨轮廓", indexFormat = posed.Length > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            silhouette.MarkDynamic();
            silhouette.vertices = posed;
            silhouette.SetUVs(0, corners);
            silhouette.SetTriangles(triangles, 0, false);
            if (!ink) ink = Resources.Load<Material>("RpgDodgeAfterimage");
            properties ??= new MaterialPropertyBlock();
            registered = ink;
            return registered;
        }

        internal bool Release(TrackedAvatarPresentation owner)
        {
            if (!owner || owner != presentation) return false;
            registered = false;
            Clear();
            return true;
        }

        Vertex Attach(Vector3 point)
        {
            int first = -1, second = -1;
            float nearest = float.PositiveInfinity, next = float.PositiveInfinity;
            for (int i = 0; i < joints.Length; i++)
            {
                ref var joint = ref joints[i];
                if (!joint.bone) continue;
                var from = point - joint.start;
                float along = Mathf.Clamp01(Vector3.Dot(from, joint.delta) * joint.inverseLengthSquared);
                float distance = (from - along * joint.delta).sqrMagnitude;
                if (distance < nearest) { second = first; next = nearest; first = i; nearest = distance; }
                else if (distance < next) { second = i; next = distance; }
            }
            if (second < 0) second = first;
            return new Vertex
            {
                first = first, second = second,
                firstLocal = joints[first].bindInverse.MultiplyPoint3x4(point),
                secondLocal = joints[second].bindInverse.MultiplyPoint3x4(point),
                secondWeight = .5f * Mathf.Clamp01(2f * nearest / Mathf.Max(next, 1e-10f) - 1f)
            };
        }

        // Called before the first successful displacement. Copy only the small fixed joint
        // palette now. A later hit may arrive after the live character has moved or turned.
        internal void CapturePose(Vector3 origin)
        {
            captured = false;
            if (!registered || !isActiveAndEnabled || !avatar || !avatar.gameObject.activeInHierarchy || !ink ||
                (presentation && !presentation.isActiveAndEnabled)) return;
            anchor = origin;
            heading = avatar.transform.rotation;
            frozenSpacing = spacingAtBind * avatar.transform.lossyScale.magnitude / scaleAtBind;
            var toAnchor = Matrix4x4.TRS(anchor, heading, Vector3.one).inverse;
            for (int i = 0; i < joints.Length; i++)
                if (joints[i].bone) joints[i].frozen = toAnchor * joints[i].bone.localToWorldMatrix;
            captured = true;
        }

        /// <summary>兼容旧演出调用。优先使用滑退前快照；模型本身决定朝向和尺寸。</summary>
        public void Show(Vector3 origin, Quaternion facing, float headHeight, float lifetimeSeconds, Scene scene = default)
        {
            if (!captured) CapturePose(origin);
            ShowCaptured(lifetimeSeconds, scene.IsValid() ? scene : gameObject.scene);
        }

        internal void ShowCaptured(float lifetime, Scene scene)
        {
            if (!captured || !isActiveAndEnabled || !avatar || !avatar.gameObject.activeInHierarchy ||
                !scene.IsValid() || !scene.isLoaded || !(lifetime > 0f)) return;
            captured = false;
            Array.Clear(washCenters, 0, washCenters.Length);
            for (int i = 0; i < vertices.Length; i++)
            {
                ref var v = ref vertices[i];
                var a = joints[v.first].frozen.MultiplyPoint3x4(v.firstLocal);
                var b = joints[v.second].frozen.MultiplyPoint3x4(v.secondLocal);
                posed[i] = Vector3.LerpUnclamped(a, b, v.secondWeight);
                washCenters[washGroup[i]] += posed[i];
            }
            for (int i = 0; i < washCenters.Length; i++)
            {
                int k = vertices.Length + i * 4;
                Vector3 center = washCenters[i] / washCounts[i];
                posed[k] = posed[k+1] = posed[k+2] = posed[k+3] = center;
            }
            silhouette.vertices = posed;
            silhouette.RecalculateBounds();
            var bounds = silhouette.bounds;
            bounds.Expand(2f * MaximumSpread * frozenSpacing);
            silhouette.bounds = bounds;
            if (!imprint)
            {
                var mark = new GameObject("极限闪避 · 玩家墨影");
                mark.AddComponent<MeshFilter>();
                imprint = mark.AddComponent<MeshRenderer>();
                imprint.sharedMaterial = ink;
                imprint.shadowCastingMode = ShadowCastingMode.Off;
                imprint.receiveShadows = false;
                imprint.lightProbeUsage = LightProbeUsage.Off;
                imprint.reflectionProbeUsage = ReflectionProbeUsage.Off;
                imprint.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            }
            imprint.GetComponent<MeshFilter>().sharedMesh = silhouette;
            if (imprint.gameObject.scene != scene) SceneManager.MoveGameObjectToScene(imprint.gameObject, scene);
            imprint.transform.SetPositionAndRotation(anchor, heading);
            started = Time.unscaledTimeAsDouble;
            duration = lifetime;
            properties.SetFloat(GroundY, anchor.y);
            SetInkPhase(0f);
            imprint.enabled = true;
        }

        void Update()
        {
            if (!imprint || !imprint.enabled) return;
            float seconds = (float)(Time.unscaledTimeAsDouble - started);
            if (seconds >= duration) { imprint.enabled = false; return; }
            SetInkPhase(seconds);
        }

        void SetInkPhase(float seconds)
        {
            // Preserve the brisk focus change on longer variants. Scale it down only
            // when an intentionally short total lifetime would otherwise cut it off.
            float t = seconds / Mathf.Min(1f, duration / .9f);
            float focus = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.028f, .083f, t));
            float wet = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.025f, .085f, t));
            float blur = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.03f, .13f, t));
            float tail = Mathf.InverseLerp(.13f * Mathf.Min(1f, duration / .9f), duration, seconds);
            float spread = Mathf.Lerp(1.45f, 3.6f, blur) + (MaximumSpread - 3.6f) * tail;
            // Do not smooth the final slope to zero twice: individual translucent
            // marks fall below an 8-bit render target's blend precision too early.
            float remaining = Mathf.Pow(1f - tail, .65f);
            // Conserve approximately the same pigment as each disc grows in area.
            properties.SetVector(InkShape, new Vector4(focus, wet * remaining * 1.5f / (spread * spread), frozenSpacing * spread, tail));
            properties.SetFloat(Opacity, remaining);
            imprint.SetPropertyBlock(properties);
        }

        public void Clear()
        {
            captured = false;
            if (imprint) imprint.enabled = false;
        }
        void OnDisable() => Clear();
        void OnDestroy()
        {
            if (imprint) Destroy(imprint.gameObject);
            if (silhouette) Destroy(silhouette);
        }
    }
}
