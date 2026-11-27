using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace NarutoVR.SealCombat
{
    // World-space geometry with normal depth testing also places the mark at the correct stereo depth.
    internal sealed class SkillAimReticle : IDisposable
    {
        static readonly Vector3[] unitCircle = BuildUnitCircle();
        readonly Vector3[] circle = new Vector3[unitCircle.Length];

        static Vector3[] BuildUnitCircle()
        {
            var points = new Vector3[32];
            for (int i = 0; i < points.Length; i++)
            {
                float angle = i * (2f * Mathf.PI / points.Length);
                points[i] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0);
            }
            return points;
        }
        readonly GameObject root;
        readonly LineRenderer ring, line;
        readonly Material material;
        Color lastColor;
        internal bool Visible => root && root.activeSelf;

        public SkillAimReticle(Scene scene, string name)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            if (!shader) return;
            root = new GameObject(name);
            SceneManager.MoveGameObjectToScene(root, scene);
            material = new Material(shader) { name = name, hideFlags = HideFlags.DontSave };
            if (material.HasProperty("_Cull")) material.SetFloat("_Cull", (float)CullMode.Off);
            ring = Stroke("落点准心", circle.Length);
            ring.loop = true;
            line = Stroke("发射方向", 2);
            Hide();
        }

        LineRenderer Stroke(string name, int count)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(root.transform, false);
            var stroke = obj.AddComponent<LineRenderer>();
            stroke.sharedMaterial = material;
            stroke.useWorldSpace = true;
            stroke.positionCount = count;
            stroke.shadowCastingMode = ShadowCastingMode.Off;
            stroke.receiveShadows = false;
            return stroke;
        }

        public void Show(SkillAimProfile style, FireballAim aim, Transform eye)
        {
            if (!root) return;
            Vector3 toEye = eye.position - aim.endpoint;
            if (toEye.sqrMagnitude < .0025f) { Hide(); return; }
            Quaternion face = Quaternion.LookRotation(toEye,eye.up);
            float radius = style.RadiusAt(toEye.magnitude);
            // Lift the ring slightly towards the viewer when an attack ends at a surface.
            Vector3 center = aim.endpoint + toEye.normalized * .004f;
            for (int i = 0; i < circle.Length; i++)
                circle[i] = center + face * (unitCircle[i] * radius);
            ring.widthMultiplier = Mathf.Max(.0024f,radius*.24f);
            ring.SetPositions(circle);
            line.enabled = style.showDirectionLine;
            if (line.enabled)
            {
                line.startWidth = .003f;
                line.endWidth = ring.widthMultiplier * .7f;
                line.SetPosition(0,aim.origin);
                line.SetPosition(1,center);
            }
            Color tint = aim.blocked ? style.blockedColor : style.color;
            if (lastColor != tint)
            {
                material.color = tint;
                lastColor = tint;
            }
            if (!root.activeSelf) root.SetActive(true);
        }

        public void Hide() { if (root && root.activeSelf) root.SetActive(false); }
        public void Dispose()
        {
            Hide();
            Destroy(root);
            Destroy(material);
        }
        static void Destroy(Object obj)
        {
            if (!obj) return;
            if (Application.isPlaying) Object.Destroy(obj); else Object.DestroyImmediate(obj);
        }
    }
}
