using System;
using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;

namespace NarutoVR.Editor
{
    public static class GrassPatchMesh
    {
        private const int MaxPlants = 48;
        private const float Turn = 2f * Mathf.PI;
        private static readonly Color Moss = new Color(.091f, .133f, .055f);
        private static readonly Color Green = new Color(.145f, .196f, .081f);
        private static readonly Color Dry = new Color(.215f, .176f, .081f);
        private static readonly Color Cream = new Color(.66f, .58f, .38f);
        private static readonly Color Mauve = new Color(.395f, .235f, .341f);
        private static readonly Color Pollen = new Color(.58f, .32f, .067f);

        internal readonly struct Shape
        {
            public readonly float Height, Spread, Width, FlowerHeight, BloomSize, PhaseRange, FootSpread, EdgeShade;
            public readonly int ExtraBlades;
            public readonly Color GreenTip;

            public Shape(float height, float spread, float width, int extraBlades, Color greenTip,
                float flowerHeight = 1f, float bloomSize = 1f, float phaseRange = .55f,
                float footSpread = 3.5f, float edgeShade = .26f)
            {
                Height = height;
                Spread = spread;
                Width = width;
                ExtraBlades = extraBlades;
                GreenTip = greenTip;
                FlowerHeight = flowerHeight;
                BloomSize = bloomSize;
                PhaseRange = phaseRange;
                FootSpread = footSpread;
                EdgeShade = edgeShade;
            }
        }

        public static Mesh Build(Vector3[] localRoots, int seed) =>
            Build(localRoots, seed, new Shape(1f, 1f, 1f, 0, Green, phaseRange: Turn, footSpread: 1f, edgeShade: 0f), null);

        internal static Mesh Build(Vector3[] localRoots, int seed, Shape shape, HashSet<int> flowers,
            Vector3[] groundCover = null)
        {
            if (localRoots == null) throw new ArgumentNullException(nameof(localRoots));
            if (localRoots.Length == 0 || localRoots.Length > MaxPlants)
                throw new ArgumentOutOfRangeException(nameof(localRoots), $"A grass patch needs 1–{MaxPlants} plants.");

            var random = new Random(seed);
            var geometry = new PlantGeometry();
            for (int index = 0; index < localRoots.Length; index++)
            {
                Vector3 root = localRoots[index];
                if (!float.IsFinite(root.x) || !float.IsFinite(root.y) || !float.IsFinite(root.z))
                    throw new ArgumentException("Plant roots must have finite coordinates.", nameof(localRoots));
                geometry.Anchor = new Vector4(root.x, root.y, root.z, Range(random, 0f, shape.PhaseRange));

                // Stagger blooms across the same irregular, seeded grass distribution.
                if (flowers?.Contains(index) ?? (index % 6 == seed % 6))
                    AddWildflower(geometry, random, root, index % 5 < 3 ? Cream : Mauve, shape);
                else
                    AddGrass(geometry, random, root, shape);
            }

            if (groundCover != null)
            {
                var low = new Shape(.40f, 1.22f, .90f, 0, new Color(.14f, .205f, .092f), footSpread: 5f);
                foreach (Vector3 root in groundCover)
                {
                    geometry.Anchor = new Vector4(root.x, root.y, root.z, Range(random, 0f, low.PhaseRange));
                    AddGrass(geometry, random, root, low);
                }
            }

            var mesh = new Mesh { name = "Wind grass and wildflowers" };
            mesh.SetVertices(geometry.Positions);
            mesh.SetColors(geometry.Colors);
            mesh.SetUVs(1, geometry.Anchors);
            mesh.SetTriangles(geometry.Indices, 0);
            Bounds bounds = mesh.bounds;
            bounds.Expand(new Vector3(.10f, 0f, .10f));
            mesh.bounds = bounds;
            return mesh;
        }

        private static void AddGrass(PlantGeometry geometry, Random random, Vector3 root, Shape shape)
        {
            int blades = random.Next(5, 8) + shape.ExtraBlades;
            float heading = Range(random, 0f, Turn);
            float height = Range(random, .19f, .42f) * shape.Height;
            Color tint = Color.Lerp(Moss, shape.GreenTip, Range(random, .25f, 1f));
            if (random.Next(7) == 0) tint = Color.Lerp(tint, Dry, Range(random, .38f, .8f));
            for (int blade = 0; blade < blades; blade++)
            {
                Vector3 forward = Heading(heading + Turn * blade / blades + Range(random, -.24f, .24f));
                float bladeHeight = height * (blade == 0 ? 1f : Range(random, .47f, .96f));
                float lean = Range(random, .045f, .19f) * shape.Spread;
                float width = Range(random, .009f, .022f) * shape.Width;
                Vector3 foot = root + forward * (Range(random, .002f, .019f) * shape.FootSpread);
                AddLeaf(geometry, foot, forward, bladeHeight, lean, width, tint * Range(random, .85f, 1.15f), 0f, 1f, shape.EdgeShade);
            }
        }

        private static void AddWildflower(PlantGeometry geometry, Random random, Vector3 root, Color petal, Shape shape)
        {
            float heading = Range(random, 0f, Turn);
            Vector3 lean = Heading(heading) * Range(random, .025f, .084f);
            float height = Range(random, .25f, .43f) * shape.FlowerHeight;
            Vector3 head = root + Vector3.up * height + lean;
            Color stalk = Color.Lerp(Moss, Green, .35f);
            // Two narrow strips give the stem a silhouette from any heading.
            for (int strip = 0; strip < 2; strip++)
            {
                Vector3 side = Heading(heading + strip * Mathf.PI * .5f) * .0045f;
                Vector3 bend = root + Vector3.up * (height * .53f) + lean * .33f;
                int low = geometry.Pair(root, side, stalk * .73f, 0f);
                int middle = geometry.Pair(bend, side * .82f, stalk, .53f);
                int high = geometry.Pair(head, side * .64f, stalk * 1.1f, 1f);
                geometry.Strip(low, middle);
                geometry.Strip(middle, high);
            }

            for (int leaf = 0; leaf < 3; leaf++)
            {
                Vector3 forward = Heading(heading + .91f + leaf * Turn / 3f);
                float level = .12f + .16f * leaf;
                Vector3 foot = root + Vector3.up * (height * level) + lean * (level * .65f);
                AddLeaf(geometry, foot, forward, Range(random, .045f, .072f), Range(random, .06f, .105f),
                    Range(random, .012f, .020f), stalk, level, level + .16f);
            }

            // Slightly tilt each flower so petals show both from the path and from above.
            Vector3 normal = (Vector3.up + Heading(heading) * Range(random, .27f, .57f)).normalized;
            Vector3 axis = Vector3.Cross(normal, Vector3.forward).normalized;
            Vector3 across = Vector3.Cross(normal, axis);
            float radius = Range(random, .025f, .043f) * shape.BloomSize;
            int petalCount = random.Next(5, 7);
            int center = geometry.Point(head + normal * .008f, Pollen, 1f);
            int previous = 0;
            int first = geometry.Positions.Count;
            for (int i = 0; i < petalCount; i++)
            {
                float angle = i * Turn / petalCount;
                Vector3 outward = axis * Mathf.Cos(angle) + across * Mathf.Sin(angle);
                Vector3 side = Vector3.Cross(normal, outward);
                int inner = geometry.Point(head + outward * (radius * .28f), Pollen * .87f, 1f);
                if (i > 0) geometry.Face(center, previous, inner);
                previous = inner;
                Color tint = petal * Range(random, .85f, 1.08f);
                Vector3 shoulder = head + outward * (radius * .64f) + normal * (radius * .18f);
                int pair = geometry.Pair(shoulder, side * (radius * .27f), tint, 1f);
                int tip = geometry.Point(head + outward * radius + normal * (radius * .29f), tint * 1.05f, 1f);
                geometry.Face(inner, pair, pair + 1);
                geometry.Face(pair, tip, pair + 1);
            }
            geometry.Face(center, previous, first);
        }

        private static void AddLeaf(PlantGeometry geometry, Vector3 foot, Vector3 forward, float height, float lean,
            float width, Color tint, float rootWeight, float tipWeight, float edgeShade = 0f)
        {
            Vector3 side = new Vector3(-forward.z, 0f, forward.x);
            int bottom = geometry.Pair(foot, side * (width * .63f), tint * .63f, rootWeight, edgeShade);
            int lower = geometry.Pair(foot + Vector3.up * (height * .38f) + forward * (lean * .17f),
                side * width, tint * .84f, Mathf.Lerp(rootWeight, tipWeight, .38f), edgeShade);
            int upper = geometry.Pair(foot + Vector3.up * (height * .79f) + forward * (lean * .54f),
                side * (width * .49f), tint, Mathf.Lerp(rootWeight, tipWeight, .79f), edgeShade);
            int tip = geometry.Point(foot + Vector3.up * height + forward * lean, tint * 1.08f, tipWeight);
            geometry.Strip(bottom, lower);
            geometry.Strip(lower, upper);
            geometry.Face(upper, upper + 1, tip);
        }

        private static Vector3 Heading(float angle) => new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));

        private static float Range(Random random, float low, float high) =>
            low + (high - low) * (float)random.NextDouble();

        private sealed class PlantGeometry
        {
            public readonly List<Vector3> Positions = new();
            public readonly List<Color32> Colors = new();
            public readonly List<Vector4> Anchors = new();
            public readonly List<int> Indices = new();
            public Vector4 Anchor { get; set; }

            public int Point(Vector3 position, Color color, float weight)
            {
                int index = Positions.Count;
                color.a = weight;
                Positions.Add(position);
                Colors.Add(color);
                Anchors.Add(Anchor);
                return index;
            }

            public int Pair(Vector3 center, Vector3 side, Color color, float weight, float edgeShade = 0f)
            {
                int index = Point(center - side, color * (1f - edgeShade), weight);
                Point(center + side, color, weight);
                return index;
            }

            public void Strip(int low, int high)
            {
                Face(low, low + 1, high);
                Face(low + 1, high + 1, high);
            }

            public void Face(int a, int b, int c)
            {
                if (Vector3.Cross(Positions[b] - Positions[a], Positions[c] - Positions[a]).sqrMagnitude <= 1e-12f)
                    throw new InvalidOperationException("Generated a degenerate plant triangle.");
                Indices.Add(a);
                Indices.Add(b);
                Indices.Add(c);
            }
        }
    }
}

