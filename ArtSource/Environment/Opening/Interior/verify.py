"""Fresh-process FBX readback. Verify stereo-scale silhouettes and UV atlas."""
from pathlib import Path
import bpy, math, json
from collections import defaultdict
from mathutils import Vector

here=Path(__file__).resolve().parent
root=here.parents[3];work=root/"Logs/chapter01-interior/model-work"
directory=root/"Assets/Art/Environment/Opening/Interior"
report={};triangles=[]
bpy.ops.wm.read_factory_settings(use_empty=True)
for f in sorted(directory.glob("*.fbx")):
    bpy.ops.import_scene.fbx(filepath=str(f))
    objs=[o for o in bpy.context.selected_objects if o.type=="MESH"];assert len(objs)==1,f
    obj=objs[0];mesh=obj.data;mesh.calc_loop_triangles()
    assert len(obj.material_slots)==1 and obj.material_slots[0].name=="GaugeWorkshop_Baked"
    assert len(mesh.uv_layers)==1 and all(v.normal.length>.98 for v in mesh.vertices),f
    mesh.calc_tangents(); assert all(loop.tangent.length>.98 for loop in mesh.loops),f
    uv=mesh.uv_layers.active.data
    for p in mesh.loop_triangles:
        tri=[tuple(uv[li].uv) for li in p.loops]
        assert all(0<u<1 and 0<v<1 for u,v in tri),f
        triangles.append((f.stem,tri))
    xyz=[obj.matrix_world @ Vector(v) for v in obj.bound_box]
    bounds=[[round(min(v[j] for v in xyz),6),round(max(v[j] for v in xyz),6)] for j in range(3)]
    report[f.stem]={"triangles":len(mesh.loop_triangles),"material":obj.material_slots[0].name,"bounds_world_blender":bounds}
    bpy.data.objects.remove(obj,do_unlink=True);bpy.data.meshes.remove(mesh)
    for mat in list(bpy.data.materials):
        if mat.users==0:bpy.data.materials.remove(mat)

def cross(a,b,c):
    return (b[0]-a[0])*(c[1]-a[1])-(b[1]-a[1])*(c[0]-a[0])

def area(p):
    return abs(sum(p[i][0]*p[(i+1)%len(p)][1]-p[(i+1)%len(p)][0]*p[i][1] for i in range(len(p))))*.5

def clipped(tri,clip):
    result=list(tri)
    for a,b in zip(clip, clip[1:]+clip[:1]):
        out=[]
        if not result:break
        for p,q in zip(result,result[1:]+result[:1]):
            dp=cross(a,b,p);dq=cross(a,b,q)
            if dp>=0:out.append(p)
            if (dp>0 and dq<0) or (dp<0 and dq>0):
                t=dp/(dp-dq);out.append((p[0]+t*(q[0]-p[0]),p[1]+t*(q[1]-p[1])))
        result=out
    return area(result)

seen=defaultdict(list);overlaps=[];total=0.
for i,(name,t) in enumerate(triangles):
    if cross(*t)<0:t.reverse()
    a=area(t);assert a>1e-12,(name,i,a)
    total+=a;x,y=zip(*t)
    cells=[(c,r) for c in range(int(min(x)*32),int(max(x)*32)+1) for r in range(int(min(y)*32),int(max(y)*32)+1)]
    for j in set(j for cell in cells for j in seen[cell]):
        other,tri=triangles[j];intersection=clipped(t,tri)
        if intersection>1e-10:overlaps.append([name,other,i,j,intersection*1024**2])
    for cell in cells:seen[cell].append(i)
report["uv_atlas"]={"triangles":len(triangles),"coverage":total,"overlap_pairs":overlaps}
(work/"fbx-readback.json").write_text(json.dumps(report,indent=2),encoding="utf8")
assert not overlaps,(len(overlaps),overlaps[:5])
print("FBX & UV PASS",json.dumps(report))
