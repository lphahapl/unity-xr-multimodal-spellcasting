using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace NarutoVR.SealCombat
{
    /// <summary>World-space blade ribbon only; the cast owns sampling, discontinuities and lifetime.</summary>
    public sealed class ThunderSwordBladeTrail : IDisposable
    {
        const int Capacity = 16;
        const float MinimumMotionSquared = .008f * .008f;
        readonly Vector3[] bases = new Vector3[Capacity], tips = new Vector3[Capacity];
        readonly double[] stamps = new double[Capacity];
        readonly Vector3[] vertices = new Vector3[Capacity * 2];
        readonly Color32[] colors = new Color32[Capacity * 2];
        readonly int[] indices = new int[(Capacity - 1) * 6];
        readonly float lifetime;
        GameObject root;
        Mesh mesh;
        MeshRenderer renderer;
        int count;
        double lastTime = double.NaN;
        bool disposed;

        public ThunderSwordBladeTrail(Scene scene, Material material, float lifetime = .14f)
        {
            this.lifetime = float.IsNaN(lifetime) || float.IsInfinity(lifetime) || lifetime <= 0 ? .14f : lifetime;
            if (!material || !scene.IsValid() || !scene.isLoaded) return;
            root = new GameObject("雷光剑 · 刃轨");
            SceneManager.MoveGameObjectToScene(root, scene);
            mesh = new Mesh { name = "雷光剑 · 动态刃轨" };
            mesh.MarkDynamic();
            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            renderer = root.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            renderer.enabled = false;
            for (int i = 0; i < Capacity - 1; i++)
            {
                int k = i * 6, a = i * 2;
                indices[k] = a; indices[k + 1] = a + 1; indices[k + 2] = a + 2;
                indices[k + 3] = a + 1; indices[k + 4] = a + 3; indices[k + 5] = a + 2;
            }
        }

        public void Sample(Vector3 bladeBase, Vector3 bladeTip, double now)
        {
            if (disposed || !mesh || !Finite(now) || !Finite(bladeBase) || !Finite(bladeTip)) return;
            Prepare(now);
            if (count > 0 && (bladeBase - bases[count - 1]).sqrMagnitude < MinimumMotionSquared &&
                (bladeTip - tips[count - 1]).sqrMagnitude < MinimumMotionSquared) return;
            if (count == Capacity) RemoveOldest();
            bases[count] = bladeBase; tips[count] = bladeTip; stamps[count] = now; count++;
            Upload(now);
        }

        /// <summary>Call every cast update even when stationary, so the ribbon expires without movement.</summary>
        public void Tick(double now)
        {
            if (disposed || !mesh || !Finite(now)) return;
            Prepare(now);
            Upload(now);
        }

        void Prepare(double now)
        {
            if (!double.IsNaN(lastTime) && now < lastTime) Clear();
            lastTime = now;
            while (count > 0 && now - stamps[0] >= lifetime) RemoveOldest();
        }

        void RemoveOldest()
        {
            count--;
            for (int i = 0; i < count; i++)
            {
                bases[i] = bases[i + 1]; tips[i] = tips[i + 1]; stamps[i] = stamps[i + 1];
            }
        }

        void Upload(double now)
        {
            if (count < 2) { if (renderer) renderer.enabled = false; return; }
            Vector3 minimum = bases[0], maximum = bases[0];
            for (int i = 0; i < count; i++)
            {
                vertices[i * 2] = bases[i]; vertices[i * 2 + 1] = tips[i];
                float fade = Mathf.Clamp01(1f - (float)((now - stamps[i]) / lifetime));
                var color = new Color32(36, 185, 255, (byte)(82 * fade));
                colors[i * 2] = color; colors[i * 2 + 1] = color;
                minimum = Vector3.Min(minimum, Vector3.Min(bases[i], tips[i]));
                maximum = Vector3.Max(maximum, Vector3.Max(bases[i], tips[i]));
            }
            // Arrays and topology are allocated once; unused vertices are outside the active index range.
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(indices, 0, (count - 1) * 6, 0, false);
            mesh.bounds = new Bounds((minimum + maximum) * .5f, maximum - minimum + Vector3.one * .002f);
            if (renderer) renderer.enabled = true;
        }

        public void Clear()
        {
            count = 0; lastTime = double.NaN;
            if (renderer) renderer.enabled = false;
        }

        public void Dispose()
        {
            if (disposed) return;
            Clear(); disposed = true;
            Destroy(root); Destroy(mesh);
            root = null; mesh = null; renderer = null;
        }

        static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
        static void Destroy(Object value)
        {
            if (!value) return;
            if (Application.isPlaying) Object.Destroy(value); else Object.DestroyImmediate(value);
        }
    }
}
