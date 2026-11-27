using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace NarutoVR.SealCombat.Items
{
    /// <summary>A square, one-way projectile screen. Dimensions are authored in metres, independent of item scale.</summary>
    internal sealed class StakeScreen : MonoBehaviour
    {
        static readonly List<StakeScreen> Active = new();
        static readonly int Phase = Shader.PropertyToID("_FieldPhase");
        static readonly int Impact = Shader.PropertyToID("_Impact");
        static Mesh fabric;
        MeshRenderer cloth;
        MaterialPropertyBlock properties;
        CooldownStake stake;
        Vector3 lastImpact;
        double openedAt, hitAt = double.NegativeInfinity;
        float halfWidth, height;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetRegistry() => Active.Clear();

        internal void Open(CooldownStake source, Material material, float width, float metresHigh)
        {
            stake = source;
            openedAt = source.FieldTime;
            halfWidth = width;
            height = metresHigh;
            var origin = source.insertionTip;
            transform.SetPositionAndRotation(origin.position, source.transform.rotation);
            var parentScale = transform.parent.lossyScale;
            transform.localScale = new Vector3(width / parentScale.x, height / parentScale.y, width / parentScale.z);
            if (!cloth)
            {
                var filter = gameObject.AddComponent<MeshFilter>();
                if (!fabric) fabric = BuildCloth();
                filter.sharedMesh = fabric;
                cloth = gameObject.AddComponent<MeshRenderer>();
                cloth.shadowCastingMode = ShadowCastingMode.Off;
                cloth.receiveShadows = false;
                cloth.lightProbeUsage = LightProbeUsage.Off;
                cloth.reflectionProbeUsage = ReflectionProbeUsage.Off;
                properties = new MaterialPropertyBlock();
            }
            cloth.sharedMaterial = material;
            hitAt = double.NegativeInfinity;
            gameObject.SetActive(true);
            if (!Active.Contains(this)) Active.Add(this);
        }

        void OnDisable() => Active.Remove(this);

        internal void Present(double now, float unfold, float opacity)
        {
            properties.SetVector(Phase, new Vector4(unfold, opacity, (float)(now - openedAt), 0));
            properties.SetVector(Impact, new Vector4(lastImpact.x,lastImpact.y,lastImpact.z,
                (float)System.Math.Min(2, now-hitAt)));
            cloth.SetPropertyBlock(properties);
        }

        internal readonly struct Entry
        {
            readonly StakeScreen screen;
            internal readonly float Distance;
            readonly Vector3 point;
            internal Entry(StakeScreen screen, float distance, Vector3 point)
            { this.screen=screen; Distance=distance; this.point=point; }
            internal void Ripple()
            {
                if (!screen || !screen.stake.FieldActive) return;
                screen.lastImpact = screen.transform.InverseTransformPoint(point);
                screen.hitAt = screen.stake.FieldTime;
            }
        }

        internal static bool TryEntry(Scene scene, Vector3 start, Vector3 direction, float distance, out Entry entry)
        {
            entry = default;
            if (distance <= 0) return false;
            bool found = false;
            foreach (var screen in Active)
            {
                if (!screen || screen.gameObject.scene != scene || !screen.stake.FieldActive) continue;
                if (!screen.EnterDistance(start, direction, distance, out float hit)) continue;
                if (found && hit >= entry.Distance) continue;
                entry = new Entry(screen, hit, start + direction * hit);
                found = true;
            }
            return found;
        }

        // Intersect the open interior so motion along an edge or a single-point corner touch is allowed.
        bool EnterDistance(Vector3 start, Vector3 direction, float length, out float result)
        {
            var inverse = Quaternion.Inverse(transform.rotation);
            Vector3 p = inverse * (start - transform.position), d = inverse * direction;
            result = 0;
            float near=float.NegativeInfinity, far=float.PositiveInfinity;
            if (!Clip(p.x,d.x,-halfWidth,halfWidth,ref near,ref far) ||
                !Clip(p.y,d.y,0,height,ref near,ref far) ||
                !Clip(p.z,d.z,-halfWidth,halfWidth,ref near,ref far)) return false;
            if (near < -.00001f || near > length || far <= Mathf.Max(0,near)+.00001f) return false;
            result = Mathf.Max(0,near);
            return true;
        }

        static bool Clip(float p, float d, float low, float high, ref float near, ref float far)
        {
            if (Mathf.Abs(d) < .000001f) return p > low+.000001f && p < high-.000001f;
            float a=(low-p)/d, b=(high-p)/d;
            near=Mathf.Max(near,Mathf.Min(a,b));
            far=Mathf.Min(far,Mathf.Max(a,b));
            return far > near;
        }

        static Mesh BuildCloth()
        {
            const int cols=24, rows=6;
            var vertices=new List<Vector3>();
            var normals=new List<Vector3>();
            var uv=new List<Vector2>();
            var triangles=new List<int>();
            for (int side=0;side<4;side++)
            {
                var rotation=Quaternion.Euler(0,side*90,0);
                int first=vertices.Count;
                for(int y=0;y<=rows;y++)
                for(int x=0;x<=cols;x++)
                {
                    float u=(float)x/cols, v=(float)y/rows;
                    vertices.Add(rotation*new Vector3(2*u-1,v,1));
                    normals.Add(rotation*Vector3.forward);
                    uv.Add(new Vector2(u,v));
                    if (x==0 || y==0) continue;
                    int i=first+y*(cols+1)+x;
                    triangles.Add(i-cols-2);triangles.Add(i-cols-1);triangles.Add(i);
                    triangles.Add(i-cols-2);triangles.Add(i);triangles.Add(i-1);
                }
            }
            var mesh=new Mesh {name="铜橛 · 四面素纱",hideFlags=HideFlags.HideAndDontSave};
            mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);
            mesh.bounds=new Bounds(Vector3.up*.5f,new Vector3(2.35f,1.25f,2.35f));
            mesh.UploadMeshData(true);
            return mesh;
        }
    }
}
