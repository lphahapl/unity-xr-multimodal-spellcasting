using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace NarutoVR.SealCombat.Rpg
{
    /// <summary>Draws the chapter's reachable landing inlays in a shared mesh.</summary>
    internal sealed class OpeningNavigationMarks
    {
        const int MaxLandings = 12;
        internal const float SurfaceOffset = .026f;
        readonly RpgLocomotionController movement;
        readonly Transform holder;
        readonly MeshRenderer renderer;
        readonly Mesh mesh;
        readonly List<(NavigationNode node, float rank)>[] bands = { new(), new(), new() };
        readonly int[] cursors = new int[3];
        readonly List<(NavigationNode node, RaycastHit floor)> marks = new(MaxLandings);
        readonly List<Vector3> vertices = new(52);
        readonly List<Vector2> uv = new(52);
        readonly List<int>[] triangles = { new(72), new(6), new(6) };
        Vector3 lastOrigin, lastFocus, lastForward;
        NavigationNode lastNode;
        bool wasVisible, hadFocus, wasCombat;
        double refreshAfter;

        public OpeningNavigationMarks(Transform parent, RpgLocomotionController movement, Material resting, Material exploration, Material combat)
        {
            this.movement = movement;
            var go = new GameObject("地面落点纹样", typeof(MeshFilter), typeof(MeshRenderer));
            holder = go.transform;
            holder.SetParent(parent, false);
            mesh = new Mesh { name = "准望匣落点纹样" };
            mesh.MarkDynamic();
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterials = new[] { resting, exploration, combat };
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.enabled = false;
        }

        public void Refresh(bool visible, bool focused, RaycastHit focusFloor)
        {
            bool combat = movement.UsingCombatNodes;
            if (!visible || (!combat && !focused))
            {
                renderer.enabled = wasVisible = false;
                return;
            }
            Vector3 origin = movement.PlayerFloorPosition;
            Vector3 forward = Vector3.ProjectOnPlane(movement.head.forward, Vector3.up).normalized;
            var node = movement.CandidateNode;
            double now = Time.unscaledTimeAsDouble;
            bool refresh = !wasVisible || wasCombat != combat || (combat && (now >= refreshAfter ||
                (origin - lastOrigin).sqrMagnitude > .25f || Vector3.Dot(lastForward, forward) < .86f));
            if (!refresh && hadFocus == focused && (!focused || (node == lastNode &&
                (lastFocus - focusFloor.point).sqrMagnitude < .000025f))) return;
            if (refresh)
            {
                refreshAfter = now + .33;
                lastOrigin = origin;
                lastForward = forward;
                marks.Clear();
                if (combat && movement.PreviewNavigationOrigin(out var from)) CollectLandings(from, forward);
            }
            hadFocus = focused;
            wasVisible = true;
            wasCombat = combat;
            lastNode = node;
            lastFocus = focusFloor.point;
            vertices.Clear();
            uv.Clear();
            foreach (var submesh in triangles) submesh.Clear();
            foreach (var mark in marks)
                if (mark.node && mark.node.isActiveAndEnabled && (!focused || (mark.floor.point - lastFocus).sqrMagnitude > .3f))
                    Draw(mark.floor, 0);
            if (focused) Draw(focusFloor, combat ? 2 : 1);
            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uv);
            mesh.subMeshCount = triangles.Length;
            for (int i = 0; i < triangles.Length; i++) mesh.SetTriangles(triangles[i], i, false);
            mesh.RecalculateBounds();
            renderer.enabled = vertices.Count > 0;
        }

        void CollectLandings(RaycastHit from, Vector3 forward)
        {
            foreach (var band in bands) band.Clear();
            System.Array.Clear(cursors, 0, cursors.Length);
            var nodes = movement.NearbyNavigationNodes;
            if (nodes == null) return;
            float range = movement.offNodeRange;
            foreach (var node in nodes)
            {
                if (!node || !node.isActiveAndEnabled || node.gameObject.scene != movement.gameObject.scene) continue;
                Vector3 delta = node.floorPosition - from.point;
                float distance = delta.magnitude;
                if (distance < movement.nodeTolerance || distance > range) continue;
                int band = Mathf.Min(2, (int)(3 * distance / range));
                bands[band].Add((node, distance / range - Vector3.Dot(forward, delta / distance)));
            }
            foreach (var band in bands)
                band.Sort((a, b) => a.rank != b.rank ? a.rank.CompareTo(b.rank) : string.CompareOrdinal(a.node.nodeId, b.node.nodeId));
            // Share the visible slots across near, middle and far distances; a sparse band gives up its slots.
            bool added;
            do
            {
                added = false;
                for (int i = 0; i < bands.Length && marks.Count < MaxLandings; i++)
                    while (cursors[i] < bands[i].Count)
                    {
                        var node = bands[i][cursors[i]++].node;
                        if (!movement.PreviewNavigationLanding(node, from, out var floor)) continue;
                        marks.Add((node, floor));
                        added = true;
                        break;
                    }
            } while (added && marks.Count < MaxLandings);
        }

        void Draw(RaycastHit floor, int material)
        {
            float distance = Vector3.Distance(lastOrigin, floor.point);
            float half = Mathf.Lerp(.30f, .49f, distance / 12f) * (material == 0 ? 1f : 1.55f);
            Vector3 center = floor.point + floor.normal * SurfaceOffset;
            Vector3 forward = Vector3.ProjectOnPlane(Vector3.forward, floor.normal).normalized * half;
            Vector3 right = Vector3.Cross(floor.normal, forward);
            int i = vertices.Count;
            vertices.Add(holder.InverseTransformPoint(center - right - forward));
            vertices.Add(holder.InverseTransformPoint(center - right + forward));
            vertices.Add(holder.InverseTransformPoint(center + right + forward));
            vertices.Add(holder.InverseTransformPoint(center + right - forward));
            uv.Add(Vector2.zero); uv.Add(Vector2.up); uv.Add(Vector2.one); uv.Add(Vector2.right);
            var indices = triangles[material];
            indices.Add(i); indices.Add(i + 1); indices.Add(i + 2);
            indices.Add(i); indices.Add(i + 2); indices.Add(i + 3);
        }

        public void Dispose() => Object.Destroy(mesh);
    }
}
