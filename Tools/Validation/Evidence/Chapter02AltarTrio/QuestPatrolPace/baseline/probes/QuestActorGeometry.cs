using System;
using System.Collections.Generic;
using System.Linq;
using NarutoVR.SealCombat.Enemies;
using Unity.Collections;
using UnityEditor;
using UnityEngine;

/// <summary>只读测量本帧身体、手持件与镇石。用绑定姿态及骨骼权重交叉校准 BakeMesh 的世界坐标。</summary>
internal sealed class QuestActorGeometry : IDisposable
{
    internal struct Extent
    {
        public int count;
        public Vector3 low, high;
        public void Add(Vector3 p)
        {
            if(count++==0)low=high=p;
            else { low=Vector3.Min(low,p);high=Vector3.Max(high,p); }
        }
        public Bounds Bounds=>new((low+high)*.5f,high-low);
    }
    internal struct Sample
    {
        public Extent body,props,stone,soles;
        public float head,leftFoot,rightFoot;
        public bool visible,frustum,unblocked;
    }
    sealed class Skin
    {
        public SkinnedMeshRenderer r;
        public bool[] soles;
        public int basis=-1;
        public float error;
        public readonly List<(int vertex,Transform bone,float weight,Vector3 local)> calibration=new();
        public bool isProp;
        internal Skin(SkinnedMeshRenderer r,Transform lf,Transform rf,bool prop)
        {
            this.r=r;isProp=prop;
            var m=r.sharedMesh;
            var bones=r.bones;
            var weights=m.GetAllBoneWeights();var counts=m.GetBonesPerVertex();var binds=m.bindposes;
            soles=new bool[m.vertexCount];
            using var data=UnityEditor.MeshUtility.AcquireReadOnlyMeshData(m);
            using var points=new NativeArray<Vector3>(m.vertexCount,Allocator.Temp); data[0].GetVertices(points);
            if(counts.Length!=m.vertexCount || weights.Length==0 || binds.Length!=bones.Length)throw new InvalidOperationException("蒙皮绑定数据缺失 "+m.name);
            var stride=Math.Max(1,m.vertexCount/19);
            for(int i=0,k=0;i<counts.Length;i++)
            {
                float foot=0;
                for(int n=0;n<counts[i];n++)
                {
                    var w=weights[k++];var b=bones[w.boneIndex];if(!b)continue;
                    if(b==lf||b==rf||b.IsChildOf(lf)||b.IsChildOf(rf))foot+=w.weight;
                    if(i%stride==0)calibration.Add((i,b,w.weight,binds[w.boneIndex].MultiplyPoint3x4(points[i])));
                }
                soles[i]=foot>=.5f;
            }
        }
        internal Matrix4x4 World(List<Vector3> v,Transform actor)
        {
            Matrix4x4 Matrix(int i)=>i==0? Matrix4x4.TRS(r.transform.position,r.transform.rotation,Vector3.one):
                i==1?r.localToWorldMatrix:Matrix4x4.TRS(r.transform.position,r.transform.rotation,actor.lossyScale);
            if(basis<0)
            {
                var groups=calibration.GroupBy(p=>p.vertex).ToArray(); if(groups.Length==0)throw new Exception("没有可核对的蒙皮顶点");
                float best=float.PositiveInfinity;
                for(int mode=0;mode<3;mode++)
                {
                    var matrix=Matrix(mode); float sum=0;
                    foreach(var g in groups)
                    {
                        var expected=Vector3.zero;foreach(var q in g)expected+=q.bone.TransformPoint(q.local)*q.weight;
                        sum+=(matrix.MultiplyPoint3x4(v[g.Key])-expected).sqrMagnitude;
                    }
                    float rms=Mathf.Sqrt(sum/groups.Length);
                    if(rms<best){best=rms;basis=mode;error=rms;}
                }
            }
            return Matrix(basis);
        }
    }
    readonly struct Rigid
    {
        internal readonly MeshFilter filter;
        internal readonly Vector3[] local;
        internal readonly bool stone;
        internal Rigid(MeshFilter f,bool stone)
        {
            filter=f;this.stone=stone;
            using var desc=UnityEditor.MeshUtility.AcquireReadOnlyMeshData(f.sharedMesh);
            using var vertices=new NativeArray<Vector3>(f.sharedMesh.vertexCount,Allocator.Temp);desc[0].GetVertices(vertices);local=vertices.ToArray();
        }
    }
    readonly EnemyController actor;
    readonly Transform head,lf,rf;
    readonly Skin[] skins;
    readonly Rigid[] rigids;
    readonly Mesh baked=new Mesh();
    readonly List<Vector3> vertices=new();
    readonly Action<string> note;
    bool logged;
    internal QuestActorGeometry(EnemyController c,BallastStone rock,Action<string> note)
    {
        this.note=note;actor=c;
        var a=c.animator;if(!a || !a.isHuman)throw new Exception("角色无 humanoid 头脚骨 "+c.name);
        head=a.GetBoneTransform(HumanBodyBones.Head);lf=a.GetBoneTransform(HumanBodyBones.LeftFoot);rf=a.GetBoneTransform(HumanBodyBones.RightFoot);
        var lh=a.GetBoneTransform(HumanBodyBones.LeftHand);var rh=a.GetBoneTransform(HumanBodyBones.RightHand);
        var groups=c.GetComponentsInChildren<LODGroup>(true).Select(g=>g.GetLODs()).Where(g=>g.Length>0).ToArray();
        var lower=new HashSet<Renderer>(groups.SelectMany(g=>g.Skip(1)).SelectMany(l=>l.renderers));
        lower.ExceptWith(groups.SelectMany(g=>g[0].renderers));
        bool Held(Transform t)=>(lh && t.IsChildOf(lh))||(rh && t.IsChildOf(rh));
        var included=c.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r=>r.sharedMesh && !lower.Contains(r) && !(rock&&r.transform.IsChildOf(rock.transform))).ToArray();
        skins=included.Select(r=>new Skin(r,lf,rf,Held(r.transform))).ToArray();
        if(!skins.Any(s=>!s.isProp))throw new Exception("缺少 LOD0 身体网格 "+c.name);
        rigids=c.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.sharedMesh && f.TryGetComponent<MeshRenderer>(out var mr) && !lower.Contains(mr))
            .Select(f=>new Rigid(f,rock && f.transform.IsChildOf(rock.transform))).ToArray();
        note("几何清单 "+c.name+" 身体/手持蒙皮="+string.Join("|",skins.Select(s=>s.r.name+"/"+s.r.sharedMesh.name+"("+s.r.sharedMesh.vertexCount+") prop="+s.isProp))+
            " 独立刚性附件="+string.Join("|",rigids.Select(r=>r.filter.name+" rock="+r.stone)));
    }
    public Sample Read(Camera camera,Transform player)
    {
        Sample s=new(){head=head.position.y,leftFoot=lf.position.y,rightFoot=rf.position.y};
        foreach(var skin in skins)
        {
            var r=skin.r;r.BakeMesh(baked,false);baked.GetVertices(vertices);var basis=skin.World(vertices,actor.transform);
            if(!skin.isProp)s.visible|=r.enabled && r.gameObject.activeInHierarchy;
            for(int i=0;i<vertices.Count;i++)
            {
                var p=basis.MultiplyPoint3x4(vertices[i]);
                if(skin.isProp)s.props.Add(p);
                else {s.body.Add(p);if(skin.soles[i])s.soles.Add(p);}
            }
        }
        if(!logged)
        {
            logged=true;note("蒙皮世界坐标校准 "+actor.name+" "+string.Join("|",skins.Select(k=>k.r.name+" basis="+k.basis+" RMS_m="+k.error.ToString("F6")))+
                " 足部权重≥0.5顶点="+s.soles.count+" collider="+string.Join("|",actor.GetComponentsInChildren<Collider>(true).Select(c=>c.GetType().Name+":"+c.name)));
        }
        foreach(var r in rigids)
        {
            if(!r.filter || !r.filter.gameObject.activeInHierarchy || !r.filter.GetComponent<MeshRenderer>().enabled)continue;
            var m=r.filter.transform.localToWorldMatrix;
            foreach(var v in r.local){var p=m.MultiplyPoint3x4(v);if(r.stone)s.stone.Add(p);else s.props.Add(p);}
        }
        if(s.body.count>0 && camera)
        {
            s.frustum=GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(camera),s.body.Bounds);
            var look=Vector3.Lerp(s.body.Bounds.center,head.position,.4f)-camera.transform.position;
            s.unblocked=!Physics.RaycastAll(camera.transform.position,look.normalized,look.magnitude,~0,QueryTriggerInteraction.Ignore)
                .Any(h=>!h.transform.IsChildOf(actor.transform) && !h.transform.IsChildOf(player));
        }
        return s;
    }
    public void Dispose(){UnityEngine.Object.Destroy(baked);}
}
