using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace NarutoVR.SealCombat.Rpg
{
    [CreateAssetMenu(menuName = "考工律度/画符/千钧符效果")]
    public sealed class QianjunEffectDefinition : GlyphEffectDefinition
    {
        [InspectorName("指物距离（米）"), Min(.1f)] public float reach = 8f;
        [InspectorName("射线检测层")] public LayerMask aimMask = Physics.DefaultRaycastLayers;
        [InspectorName("推拉力增益"), Min(0)] public float forceGain = 180f;
        [InspectorName("最大力（牛顿）"), Min(0)] public float maxForce = 140f;
        [InspectorName("手速死区（米/秒）"), Min(0)] public float deadSpeed = .045f;
        [InspectorName("走动牵绳余量（米）"), Min(0)] public float ropeSlack = .035f;
        [InspectorName("牵绳刚度（牛顿/米）"), Min(0)] public float ropeStiffness = 240f;
        [InspectorName("牵绳阻尼（牛顿秒/米）"), Min(0)] public float ropeDamping = 22f;
        [InspectorName("连线材质")] public Material tetherMaterial;
        [InspectorName("朱砂线颜色")] public Color tetherColor = new Color(1f, .18f, .04f);
        [InspectorName("线宽（米）"), Min(.001f)] public float tetherWidth = .008f;

        public override GlyphEffectInstance Begin(GlyphEffectContext context) => new Tether(this);

        sealed class Tether : GlyphEffectInstance
        {
            readonly QianjunEffectDefinition settings;
            const float MaxSampleGap = .15f;
            const float MaxSampleTravel = .30f;
            RaycastHit[] hits = new RaycastHit[12];
            GlyphHeavyBody target;
            Vector3 localHit, lastHand, lastBody, initialHand, initialBody, pendingImpulse;
            float restLength, freshSeconds;
            double previousTime = double.NaN;
            LineRenderer line;
            Material ownMaterial;
            bool locked, ended, baseline, hadHand, carryingRope;

            public override bool IsComplete => ended;
            public override bool IsTethered => locked && !ended;
            public override string Status => ended ? "牵引结束" : locked ? "走动牵引，持笔手推拉，持续放低持笔手收笔" : "笔尖指向可推拉的重物";
            internal Tether(QianjunEffectDefinition settings) { this.settings = settings; }

            public override void Tick(GlyphEffectContext context, float deltaTime)
            {
                if (ended) return;
                if (locked && (!target || !target.CanManipulate)) { End(); return; }
                bool handTracked = context.tracked && Finite(context.hand);
                if (!locked && (!handTracked || !Finite(context.tip) || !Finite(context.forward) ||
                    !Finite(context.body) || !double.IsFinite(context.observed) || !TryLock(context))) return;
                Vector3 hit = target.transform.TransformPoint(localHit);
                line.SetPosition(1, hit);
                if ((!handTracked && !context.bodyTracked) || !Finite(context.body) || !double.IsFinite(context.observed))
                {
                    pendingImpulse = Vector3.zero;
                    previousTime = double.NaN;
                    baseline = hadHand = false;
                    return;
                }

                // 遮手时按持笔模型的水平步移估计握点，朝向与射线仅用于初次锁定。
                Vector3 bodyTravel = context.body - lastBody;
                Vector3 horizontalTravel = Vector3.ProjectOnPlane(bodyTravel, Vector3.up);
                Vector3 hand = handTracked ? context.hand : lastHand + horizontalTravel;
                Vector3 handTravel = hand - lastHand;
                float elapsed = (float)(context.observed - previousTime);
                bool handReset = context.rebaseHand || handTracked != hadHand;
                if (!handTracked || handReset) pendingImpulse = Vector3.zero;
                if (elapsed == 0 && !handReset) return;
                if (!baseline || elapsed > MaxSampleGap || (elapsed < 0 && (!handReset || elapsed < -MaxSampleGap)) ||
                    bodyTravel.sqrMagnitude > MaxSampleTravel * MaxSampleTravel ||
                    (!handReset && handTravel.sqrMagnitude > MaxSampleTravel * MaxSampleTravel))
                {
                    initialBody = context.body;
                    initialHand = hand;
                    restLength = Vector3.Distance(hand, hit);
                    carryingRope = false;
                    pendingImpulse = Vector3.zero;
                }
                else if (handTracked && !handReset && elapsed > 0 && handTravel.sqrMagnitude > 0)
                {
                    Vector3 direction = (hit - hand).normalized;
                    float reachChange = Vector3.Dot(handTravel - bodyTravel, direction);
                    float speed = reachChange / elapsed;
                    float force = Mathf.Clamp((Mathf.Abs(speed) - settings.deadSpeed) * settings.forceGain, 0, settings.maxForce);
                    pendingImpulse += direction * (Mathf.Sign(speed) * force * elapsed);
                    if (carryingRope && speed > settings.deadSpeed && horizontalTravel.magnitude <= settings.deadSpeed * elapsed)
                    {
                        // 已经牵过石块时，站定主动前推允许放绳；行走时的摆臂不会逐步放长绳子。
                        float distance = Vector3.Distance(WalkingHand(context.body), hit);
                        restLength = Mathf.Max(restLength, distance + reachChange);
                    }
                }
                lastHand = hand;
                lastBody = context.body;
                hadHand = handTracked;
                baseline = true;
                if (context.observed != previousTime) freshSeconds = MaxSampleGap;
                previousTime = context.observed;
                line.SetPosition(0, hand);
            }

            public override void FixedTick(float deltaTime)
            {
                if (ended || !locked || !float.IsFinite(deltaTime) || deltaTime <= 0) return;
                if (!target || !target.CanManipulate) { End(); return; }
                if (!baseline) return;
                float step = Mathf.Min(deltaTime, freshSeconds);
                if (step > 0)
                {
                    Vector3 impulse = pendingImpulse + WalkingForce() * step;
                    target.ApplyGlyphImpulse(Vector3.ClampMagnitude(impulse, settings.maxForce * step));
                }
                pendingImpulse = Vector3.zero;
                freshSeconds -= step;
                if (freshSeconds <= 0) baseline = false;
            }

            Vector3 WalkingHand(Vector3 body) => initialHand + Vector3.ProjectOnPlane(body - initialBody, Vector3.up);

            Vector3 WalkingForce()
            {
                Vector3 hit = target.transform.TransformPoint(localHit);
                Vector3 handle = WalkingHand(lastBody);
                if (!carryingRope)
                {
                    float startDistance = Mathf.Max(.005f, settings.ropeSlack);
                    if ((handle - initialHand).sqrMagnitude < startDistance * startDistance) return Vector3.zero;
                    // 第一次携绳走动时接纳之前原地推开的距离；此后锁长和随身握点一起跨越场景。
                    restLength = Mathf.Max(restLength, Vector3.Distance(initialHand, hit));
                    carryingRope = true;
                }
                float extension = Vector3.Distance(handle, hit) - restLength - settings.ropeSlack;
                if (extension <= 0) return Vector3.zero;
                Vector3 towardHand = (lastHand - hit).normalized;
                float closingSpeed = Vector3.Dot(target.Body.GetPointVelocity(hit), towardHand);
                float tension = Mathf.Clamp(extension * settings.ropeStiffness - closingSpeed * settings.ropeDamping, 0, settings.maxForce);
                return towardHand * tension;
            }

            bool TryLock(GlyphEffectContext context)
            {
                if (context.forward.sqrMagnitude < .0001f) return false;
                var scene = context.owner ? context.owner.gameObject.scene.GetPhysicsScene() : Physics.defaultPhysicsScene;
                Vector3 aim = context.forward.normalized;
                int count;
                while ((count = scene.Raycast(context.tip, aim, hits, settings.reach, settings.aimMask, QueryTriggerInteraction.Ignore)) == hits.Length)
                    Array.Resize(ref hits, hits.Length * 2);

                float nearest = float.PositiveInfinity;
                RaycastHit first = default;
                for (int i = 0; i < count; ++i)
                {
                    RaycastHit hit = hits[i];
                    var tr = hit.collider.transform;
                    if (hit.distance >= nearest || (context.owner && tr.IsChildOf(context.owner)) ||
                        (context.pen && tr.IsChildOf(context.pen))) continue;
                    first = hit;
                    nearest = hit.distance;
                }
                if (!first.rigidbody || !first.rigidbody.TryGetComponent(out GlyphHeavyBody heavy) || !heavy.CanManipulate) return false;
                target = heavy;
                localHit = heavy.transform.InverseTransformPoint(first.point);
                locked = true;
                CreateLine(context.owner);
                return true;
            }

            void CreateLine(Transform owner)
            {
                var go = new GameObject("千钧符·磁墨牵引线");
                if (owner) go.transform.SetParent(owner, false);
                line = go.AddComponent<LineRenderer>();
                line.useWorldSpace = true;
                line.positionCount = 2;
                line.startWidth = line.endWidth = settings.tetherWidth;
                line.shadowCastingMode = ShadowCastingMode.Off;
                line.receiveShadows = false;
                if (settings.tetherMaterial) line.sharedMaterial = settings.tetherMaterial;
                else
                {
                    var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
                    if (shader) line.sharedMaterial = ownMaterial = new Material(shader);
                }
                var color = settings.tetherColor;
                line.startColor = line.endColor = color;
                var properties = new MaterialPropertyBlock();
                properties.SetColor("_BaseColor", color);
                properties.SetColor("_Color", color);
                line.SetPropertyBlock(properties);
            }

            public override void End()
            {
                if (ended) return;
                ended = true;
                pendingImpulse = Vector3.zero;
                if (line) { line.enabled = false; Dispose(line.gameObject); }
                if (ownMaterial) Dispose(ownMaterial);
                target = null;
            }

            static void Dispose(Object value)
            {
                if (Application.isPlaying) Object.Destroy(value);
                else Object.DestroyImmediate(value);
            }

            static bool Finite(Vector3 v) => float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);
        }
    }
}
