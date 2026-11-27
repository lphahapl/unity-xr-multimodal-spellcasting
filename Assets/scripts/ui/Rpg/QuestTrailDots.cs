using UnityEngine;
using UnityEngine.Rendering;

namespace NarutoVR.SealCombat.Rpg
{
    // The CPU places stable centres along the route. The shader animates one soft sprite per centre.
    internal sealed class QuestTrailDots
    {
        const int Count = 16;
        const float Spacing = .36f;
        readonly GameObject owner;
        readonly Mesh mesh;
        readonly MeshRenderer renderer;
        readonly Vector3[] vertices = new Vector3[Count * 4];
        readonly int[] indices = new int[Count * 6];
        readonly RaycastHit[] hits = new RaycastHit[16];
        int visibleCount;

        public QuestTrailDots(Transform parent, Material material)
        {
            owner = new GameObject("任务寻路 · 流动铜点", typeof(MeshFilter), typeof(MeshRenderer));
            owner.transform.SetParent(parent, false);
            mesh = new Mesh {name = "任务寻路光点"};
            mesh.MarkDynamic();
            var corners = new Vector2[vertices.Length];
            for (int i = 0; i < Count; i++)
            {
                int v = i * 4, k = i * 6;
                corners[v] = new Vector2(-1, -1);
                corners[v + 1] = new Vector2(1, -1);
                corners[v + 2] = Vector2.one;
                corners[v + 3] = new Vector2(-1, 1);
                indices[k] = v; indices[k + 1] = v + 1; indices[k + 2] = v + 2;
                indices[k + 3] = v; indices[k + 4] = v + 2; indices[k + 5] = v + 3;
            }
            mesh.vertices = vertices;
            mesh.uv = corners;
            owner.GetComponent<MeshFilter>().sharedMesh = mesh;
            renderer = owner.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        public void Draw(Transform[] path, int end, RpgLocomotionController movement, float height, bool requireFloor = false)
        {
            var foot = movement.PlayerFloorPosition;
            int segment = -1;
            float best = float.PositiveInfinity;
            float arc = 0f, joinArc = 0f;
            Vector3 join = foot;
            for (int i = 0; i + 1 < path.Length; i++)
            {
                if (!path[i] || !path[i+1]) continue;
                var a = path[i].position;
                var leg = path[i+1].position - a;
                float t = Mathf.Clamp01(Vector3.Dot(foot-a,leg) / Mathf.Max(.00001f,leg.sqrMagnitude));
                var p = a + leg * t;
                float length = leg.magnitude;
                float along = arc + t * length;
                arc += length;
                float sq = (p-foot).sqrMagnitude;
                if (sq >= best || sq > 36f || !ClearLeg(foot,p,movement,requireFloor)) continue;
                best = sq; join = p; segment = i; joinArc = along;
            }
            // A single authored point is a direct, obstruction-tested approach.
            if ((segment < 0 && path.Length != 1) || !path[end] || (foot-path[end].position).sqrMagnitude < .45f*.45f) { Hide(); return; }
            int step = end <= segment ? -1 : 1;
            int next = step < 0 ? segment : segment+1;
            // Anchor the spacing to route distance so that the lights stay over the same stones as the player walks.
            float offset = Mathf.Repeat(Vector3.Distance(foot, join) - step * joinArc, Spacing);
            if (offset < .22f) offset += Spacing;
            int drawn = 0;
            Bounds bounds = default;
            var from = foot;
            var to = join;
            while (drawn < Count)
            {
                var leg = to-from;
                float length = leg.magnitude;
                if (length > .001f && !ClearLeg(from,to,movement,requireFloor)) break;
                while (drawn < Count && offset <= length)
                {
                    var at = from + leg * (offset/length) + Vector3.up*height;
                    var center = owner.transform.InverseTransformPoint(at);
                    if (drawn == 0) bounds = new Bounds(center, Vector3.zero);
                    else bounds.Encapsulate(center);
                    int v = drawn++ * 4;
                    for (int j = 0; j < 4; j++) vertices[v + j] = center;
                    offset += Spacing;
                }
                offset -= length;
                if ((step > 0 && next > end) || (step < 0 && next < end) || !path[next]) break;
                from = to; to = path[next].position; next += step;
            }
            if (drawn == 0) { Hide(); return; }
            mesh.vertices = vertices;
            if (drawn != visibleCount)
            {
                mesh.SetIndices(indices, 0, drawn * 6, MeshTopology.Triangles, 0, false);
                visibleCount = drawn;
            }
            bounds.Expand(.30f); // Includes the shader's maximum sprite radius and vertical movement.
            mesh.bounds = bounds;
            renderer.enabled = true;
        }

        bool ClearLeg(Vector3 a, Vector3 b, RpgLocomotionController movement, bool requireFloor)
        {
            var delta = b-a;
            if (delta.sqrMagnitude < .0025f) return true;
            // A cliff-edge route is allowed to join the player only across supported floor. Test the
            // visible stretch of long authored segments, including the point where the next dot would land.
            float distance = requireFloor ? Mathf.Min(delta.magnitude, (Count + 1) * Spacing) : delta.magnitude;
            if (requireFloor)
            {
                int samples = Mathf.Max(1, Mathf.CeilToInt(distance / .35f));
                for (int i = 0; i <= samples; i++)
                {
                    var at = a + delta.normalized * (distance * i / samples);
                    int count = Physics.RaycastNonAlloc(at + Vector3.up * .22f, Vector3.down, hits, .45f,
                        movement.floorLayers, QueryTriggerInteraction.Ignore);
                    if (count == hits.Length) return false;
                    bool supported = false;
                    for (int h = 0; h < count; h++)
                    {
                        if (hits[h].transform.IsChildOf(movement.rigRoot) || hits[h].normal.y < .82f ||
                            Mathf.Abs(hits[h].point.y - at.y) > .18f) continue;
                        supported = true;
                        break;
                    }
                    if (!supported) return false;
                }
            }
            int n = Physics.SphereCastNonAlloc(a+Vector3.up*.55f,.12f,delta.normalized,hits,distance,
                movement.obstacleLayers,QueryTriggerInteraction.Ignore);
            if (n == hits.Length) return false;
            for (int i=0;i<n;i++)
                if (!hits[i].transform.IsChildOf(movement.rigRoot)) return false;
            return true;
        }

        public void Hide() => renderer.enabled = false;
        public void Dispose() { Object.Destroy(mesh); Object.Destroy(owner); }
    }
}
