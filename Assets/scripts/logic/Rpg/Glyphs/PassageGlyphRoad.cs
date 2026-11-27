using UnityEngine;
using UnityEngine.Rendering;

namespace NarutoVR.SealCombat.Rpg
{
    [DisallowMultipleComponent, RequireComponent(typeof(BoxCollider))]
    public sealed class PassageGlyphRoad : MonoBehaviour
    {
        public float Progress { get; private set; }
        public Vector3 Front => Vector3.Lerp(start, end, Progress);
        Vector3 start, end;
        const float Thickness = .12f;
        // Keep the walking surface flush with the bank and draw a single film above it.
        const float SurfaceClearance = .012f;
        float width, duration, elapsed;
        MeshRenderer surface;
        BoxCollider floor;
        MaterialPropertyBlock properties;

        internal void Begin(PassageGlyphAnchor from, PassageGlyphAnchor to, PassageGlyphEffect effect)
        {
            start = from.landing.position;
            end = to.landing.position;
            width = effect.deckWidth;
            duration = Mathf.Max(.05f, effect.formationSeconds);
            transform.rotation = Quaternion.LookRotation(end - start, Vector3.up);
            gameObject.layer = from.landing.gameObject.layer;
            name = "通路符桥面";
            properties = new MaterialPropertyBlock();
            var sheet = new GameObject("淡金符面", typeof(MeshFilter), typeof(MeshRenderer));
            sheet.layer = gameObject.layer;
            sheet.transform.SetParent(transform, false);
            sheet.transform.SetLocalPositionAndRotation(Vector3.up * (.5f + SurfaceClearance / Thickness),
                Quaternion.Euler(90f, 0f, 0f));
            sheet.GetComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            surface = sheet.GetComponent<MeshRenderer>();
            surface.sharedMaterial = effect.deckMaterial;
            surface.shadowCastingMode = ShadowCastingMode.Off;
            surface.receiveShadows = false;
            floor = GetComponent<BoxCollider>();
            floor.enabled = false;
            Advance(0);
        }

        internal static PassageGlyphRoad Restore(PassageGlyphAnchor a, PassageGlyphAnchor b, PassageGlyphEffect effect)
        {
            var road = new GameObject("通路符桥面").AddComponent<PassageGlyphRoad>();
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(road.gameObject, a.gameObject.scene);
            road.Begin(a, b, effect);
            a.Register(b, road.gameObject);
            b.Register(a, road.gameObject);
            road.Advance(effect.formationSeconds);
            return road;
        }

        void Update() => Advance(Time.deltaTime);

        internal void Advance(float seconds)
        {
            elapsed += Mathf.Max(0, seconds);
            Progress = Mathf.Clamp01(elapsed / duration);
            var front = Front;
            float length = Vector3.Distance(start, front);
            transform.position = (start + front) * .5f - transform.up * (Thickness * .5f);
            transform.localScale = new Vector3(width, Thickness, Mathf.Max(.0001f, length));
            surface.enabled = Progress > 0;
            properties.SetFloat("_Length", length);
            properties.SetFloat("_Growing", 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.92f, 1f, Progress)));
            surface.SetPropertyBlock(properties);
            if (Progress < 1f) return;
            floor.enabled = true;
            enabled = false;
        }
    }
}
