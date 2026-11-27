using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace NarutoVR.SealCombat.Rpg
{
    [CreateAssetMenu(menuName = "考工律度/画符/通路符效果")]
    public sealed class PassageGlyphEffect : GlyphEffectDefinition
    {
        [InspectorName("最大连接距离（米，试验值）"), Min(.1f)] public float maxSpan = 7f;
        [InspectorName("食指射线距离（米）"), Min(.1f)] public float reach = 12f;
        [InspectorName("成路时长（秒，试验值）"), Min(.05f)] public float formationSeconds = 2.4f;
        [InspectorName("主动取消：右腕低于头部（米）"), Min(.2f)] public float cancelBelowHead = .60f;
        [InspectorName("主动取消：连续保持（秒）"), Min(.2f)] public float cancelHoldSeconds = 1f;
        [Min(.1f)] public float deckWidth = 1.1f;
        public Material handGold, sigilGold, previewMaterial, deckMaterial;
        public override bool AllowsEmptyHand => true;
        public override GlyphEffectInstance Begin(GlyphEffectContext context) => new Placement(this, context);

        sealed class Placement : GlyphEffectInstance
        {
            readonly PassageGlyphEffect settings;
            readonly Scene scene;
            readonly GameObject cursor;
            readonly LineRenderer beam, selected;
            readonly Transform palm;
            readonly MeshRenderer reticle;
            readonly MaterialPropertyBlock beamState = new(), reticleState = new();
            PassageGlyphAnchor litAnchor;
            readonly RaycastHit[] buffer = new RaycastHit[24];
            PassageGlyphAnchor first, candidate;
            GameObject preview;
            LineRenderer outline;
            Vector3 candidateLocalHit;
            bool fistReady, cancelArmed;
            double lowSince = double.NaN;
            bool complete;

            public override bool IsComplete => complete;
            public override string Status => !first ? "通路符：右食指指向第一岸标记" :
                candidate ? "两岸已选，握拳架桥；空手低位张掌保持取消" : "已选第一岸，指向另一岸；空手低位张掌保持取消";

            internal Placement(PassageGlyphEffect settings, GlyphEffectContext context)
            {
                this.settings = settings;
                scene = context.pen.gameObject.scene;
                cursor = new GameObject("通路符 · 右手金光");
                SceneManager.MoveGameObjectToScene(cursor, scene);
                palm = new GameObject("右手符纹").transform;
                palm.SetParent(cursor.transform, false);
                var ignition = new MaterialPropertyBlock();
                ignition.SetFloat("_LitAt", Time.time);
                foreach (int side in new[]{1,-1})
                {
                    var mark = Sigil(side > 0 ? "手背符光" : "掌心符光", palm, .084f);
                    mark.transform.SetLocalPositionAndRotation(Vector3.up * (side * .019f),
                        Quaternion.LookRotation(Vector3.down * side, Vector3.forward));
                    mark.SetPropertyBlock(ignition);
                }
                FollowHand(context);
                reticle = Sigil("射线命中符纹", cursor.transform, .19f);
                reticle.enabled = false;
                reticleState.SetFloat("_TargetMark", 1);
                beam = Line("食指指向", .028f);
                beamState.SetFloat("_RayRibbon", 1);
                beam.positionCount = 2;
                beam.enabled = false;
                selected = Line("已选岸标", .015f);
                selected.loop = true;
                selected.positionCount = 4;
                selected.enabled = false;
            }

            MeshRenderer Sigil(string name, Transform parent, float size)
            {
                var node = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
                node.transform.SetParent(parent, false);
                node.transform.localScale = Vector3.one * size;
                node.GetComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
                var renderer = node.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = settings.sigilGold;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                return renderer;
            }

            void FollowHand(GlyphEffectContext c) =>
                palm.SetPositionAndRotation(Vector3.Lerp(c.wrist, c.hand, .62f), c.gripRotation);

            void HideRay()
            {
                beam.enabled = reticle.enabled = false;
                litAnchor = null;
            }

            void ShowRay(Ray ray, RaycastHit contact, PassageGlyphAnchor anchor)
            {
                float length = contact.collider ? contact.distance : settings.reach;
                beam.enabled = true;
                beam.SetPosition(0, ray.origin);
                beam.SetPosition(1, ray.GetPoint(length));
                beam.endWidth = Mathf.Clamp(.028f + length * .008f, .028f, .09f);
                beamState.SetFloat("_Length", length);
                beamState.SetFloat("_HasTarget", anchor ? 1 : 0);
                beam.SetPropertyBlock(beamState);
                reticle.enabled = anchor;
                if (anchor)
                {
                    var normal = contact.normal;
                    var along = Mathf.Abs(normal.z) < .92f ? Vector3.forward : Vector3.up;
                    reticle.transform.SetPositionAndRotation(contact.point + normal * .008f,
                        Quaternion.LookRotation(-normal, along));
                    if (anchor != litAnchor)
                    {
                        reticleState.SetFloat("_LitAt", Time.time);
                        reticle.SetPropertyBlock(reticleState);
                    }
                }
                litAnchor = anchor;
            }

            LineRenderer Line(string name, float width)
            {
                var node = new GameObject(name);
                node.transform.SetParent(cursor.transform, false);
                var line = node.AddComponent<LineRenderer>();
                line.sharedMaterial = settings.handGold;
                line.startWidth = line.endWidth = width;
                return line;
            }

            public override void Tick(GlyphEffectContext c, float dt)
            {
                if (complete) return;
                if (!first || !first.Available) { first = null; selected.enabled = false; }
                if (preview && preview.activeSelf && (!first || !first.CanLinkTo(candidate, settings.maxSpan)))
                    ClearCandidate();
                if (candidate) FitDeck();
                palm.gameObject.SetActive(c.tracked);
                if (!c.tracked)
                {
                    HideRay();
                    fistReady = false;
                    lowSince = double.NaN;
                    return;
                }
                FollowHand(c);
                if (CancelHeld(c)) { End(); return; }
                if (c.rightFist)
                {
                    HideRay();
                    bool confirm = fistReady;
                    fistReady = false;
                    if (confirm && candidate && CanConfirm(c))
                    {
                        outline.enabled = false;
                        preview.AddComponent<PassageGlyphRoad>().Begin(first, candidate, settings);
                        first.Register(candidate, preview);
                        candidate.Register(first, preview);
                        preview = null; // The committed collider lives in the level after the pen is released.
                        End();
                    }
                    return;
                }
                if (c.rightPointing || c.rightOpen) fistReady = true;
                if (!c.rightPointing || !c.indexRayValid)
                {
                    HideRay();
                    return;
                }
                var hit = Pick(c.indexRay, c, out var contact);
                var visibleMark = hit;
                if (!visibleMark && contact.collider)
                    visibleMark = contact.collider.GetComponentInParent<PassageGlyphAnchor>();
                ShowRay(c.indexRay, contact, visibleMark);
                if (!hit) return;
                if (!first)
                {
                    first = hit;
                    var center = AboveMarker(hit);
                    for (int i = 0; i < 4; i++)
                    {
                        float angle = i * Mathf.PI * .5f;
                        selected.SetPosition(i, center + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * .25f);
                    }
                    selected.enabled = true;
                    ClearCandidate();
                    return;
                }
                candidate = hit;
                candidateLocalHit = hit.transform.InverseTransformPoint(contact.point);
                if (!preview)
                {
                    preview = new GameObject("通路符搭路预览", typeof(BoxCollider));
                    SceneManager.MoveGameObjectToScene(preview, scene);
                    preview.GetComponent<BoxCollider>().enabled = false;
                    var wire = new GameObject("预览边线");
                    wire.transform.SetParent(preview.transform, false);
                    outline = wire.AddComponent<LineRenderer>();
                    outline.loop = true;
                    outline.startWidth = outline.endWidth = .022f;
                    outline.positionCount = 4;
                    outline.sharedMaterial = settings.previewMaterial;
                    var tint = new MaterialPropertyBlock();
                    tint.SetColor("_BaseColor", new Color(.46f,.86f,.87f));
                    outline.SetPropertyBlock(tint);
                }
                preview.SetActive(true);
                FitDeck();
            }

            bool CanConfirm(GlyphEffectContext c)
            {
                // Recheck the selected mark from the current hand. The index ray disappears while closing the fist.
                var delta = candidate.transform.TransformPoint(candidateLocalHit) - c.hand;
                return delta.sqrMagnitude > .000001f && Pick(new Ray(c.hand, delta.normalized), c, out _) == candidate;
            }

            bool CancelHeld(GlyphEffectContext c)
            {
                if (!c.bodyTracked || c.holdingBeltPen)
                {
                    cancelArmed = false;
                    lowSince = double.NaN;
                    return false;
                }
                float depth = c.body.y - c.wrist.y;
                if (c.rightPointing && depth < settings.cancelBelowHead - .08f) cancelArmed = true;
                if (!cancelArmed || !c.rightOpen || depth < settings.cancelBelowHead)
                {
                    lowSince = double.NaN;
                    return false;
                }
                if (double.IsNaN(lowSince) || c.observed < lowSince) lowSince = c.observed;
                return c.observed - lowSince >= settings.cancelHoldSeconds;
            }

            PassageGlyphAnchor Pick(Ray ray, GlyphEffectContext c, out RaycastHit contact)
            {
                contact = default;
                var hits = buffer;
                int count = Physics.RaycastNonAlloc(ray, hits, settings.reach, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
                if (count == hits.Length)
                {
                    hits = Physics.RaycastAll(ray, settings.reach, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
                    count = hits.Length;
                }
                PassageGlyphAnchor nearest = null;
                RaycastHit solidHit = default, lockedHit = default;
                float solid = float.PositiveInfinity, distance = float.PositiveInfinity;
                for (int i = 0; i < count; i++)
                {
                    var h = hits[i];
                    if (h.transform.gameObject.scene != scene || h.transform.IsChildOf(c.pen) ||
                        (c.owner && h.transform.IsChildOf(c.owner))) continue;
                    var anchor = h.collider.GetComponentInParent<PassageGlyphAnchor>();
                    if (!anchor)
                    {
                        if (!h.collider.isTrigger && h.distance < solid && !h.collider.TryGetComponent<PassageSightWindow>(out _)) { solid = h.distance; solidHit = h; }
                        continue;
                    }
                    if (!anchor.Available) continue;
                    if (anchor == first) { lockedHit = h; continue; }
                    if (h.distance >= distance) continue;
                    if (first && !first.CanLinkTo(anchor, settings.maxSpan)) continue;
                    nearest = anchor;
                    distance = h.distance;
                    contact = h;
                }
                if (nearest && distance <= solid) return nearest;
                contact = lockedHit.collider && lockedHit.distance <= solid ? lockedHit : solidHit;
                return null;
            }

            void FitDeck()
            {
                var a = first.landing.position;
                var b = candidate.landing.position;
                var rotation = Quaternion.LookRotation(b - a, Vector3.up);
                preview.transform.SetPositionAndRotation((a + b) * .5f - rotation * Vector3.up * .06f, rotation);
                preview.transform.localScale = new Vector3(settings.deckWidth, .12f, Vector3.Distance(a, b) + .12f);
                Vector3 side = rotation * Vector3.right * (settings.deckWidth * .5f);
                var start = AboveMarker(first);
                var end = AboveMarker(candidate);
                outline.SetPosition(0, start - side);
                outline.SetPosition(1, end - side);
                outline.SetPosition(2, end + side);
                outline.SetPosition(3, start + side);
            }

            static Vector3 AboveMarker(PassageGlyphAnchor anchor)
            {
                var position = anchor.landing.position;
                position.y = Mathf.Max(position.y, anchor.GetComponent<Collider>().bounds.max.y) + .020f;
                return position;
            }

            void ClearCandidate()
            {
                candidate = null;
                if (preview) preview.SetActive(false);
            }

            public override void End()
            {
                if (complete) return;
                complete = true;
                cursor.SetActive(false);
                Object.Destroy(cursor);
                if (preview) { preview.SetActive(false); Object.Destroy(preview); }
            }
        }
    }
}
