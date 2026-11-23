import bpy,math,json
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
from io_scene_fbx.parse_fbx import parse
import numpy as np

out=Path(r"D:/UnityProjects/NarutoVR/ArtSource/Environment/Opening/ConcentricGauge/Bronze/.work")
dest=Path(r"D:/UnityProjects/NarutoVR/Assets/Art/Environment/Opening/ConcentricGauge/Bronze")
names=["Bed","Outer","Inner","Box","Lid"]
checks={}
tree={}
for name in names:
    obj=bpy.data.objects[name]
    tree[name]=BVHTree.FromPolygons([v.co for v in obj.data.vertices],
                                    [p.vertices[:] for p in obj.data.polygons],all_triangles=True)
    raw,_=parse(str(dest/(name+".fbx")))
    objects=next(e for e in raw.elems if e.id==b"Objects")
    mesh,=[e for e in objects.elems if e.id==b"Geometry"]
    data={e.id:e.props for e in mesh.elems}
    positions=np.asarray(data[b"Vertices"][0]).reshape(-1,3)
    idx=data[b"PolygonVertexIndex"][0]
    assert len(idx)==3*len(obj.data.polygons) and all(k<0 for k in idx[2::3])
    # This exporter's FBX positions are in metres, +Y up. Unity reverses FBX X.
    unity=positions.copy()
    unity[:,0]*=-1
    src=np.array([(-v.co.x,v.co.z,-v.co.y) for v in obj.data.vertices])
    assert unity.shape==src.shape and np.allclose(unity,src,atol=.000002), (name,unity[0],src[0])
    checks[name]={"triangles":len(idx)//3,"bounds_m":[unity.min(0).round(6).tolist(),unity.max(0).round(6).tolist()]}
    if name=="Outer":
        samples=unity[(abs(unity[:,0])>.087)&(abs(unity[:,0])<.094)&(abs(unity[:,2])<.002)]
        assert min(samples[:,0])<-.0919 and max(samples[:,0])>.0919 and max(samples[:,1])<.016
        checks[name]["groove_vertex_window_unique"]=np.unique(samples.round(6),axis=0).tolist()
def height(name,x,z):
    hit,_,_,_=tree[name].ray_cast(Vector((-x,-z,.25)),Vector((0,0,-1)))
    assert hit is not None,(name,x,z)
    return hit.z
for name,x,z,y in [("Outer",.155,0,.055),("Inner",0,.068,.062)]:
    top=height(name,x,z)
    bottom=tree[name].ray_cast(Vector((-x,-z,.035)),Vector((0,0,1)))[0].z
    center=(top+bottom)*.5
    assert abs(center-y)<.00001
    checks[name]["physical_grip_m"]=[x,round(center,6),z]
    assert tree[name].ray_cast(Vector((0,0,.25)),Vector((0,0,-1)))[0] is None
for x in [-.092,-.089,.089,.092]:
    # Shoulder vertices have the same radial distance from the axis as their floor.
    z=.003
    shoulder_x=math.copysign(math.sqrt(x*x-z*z),x)
    depth=height("Outer",shoulder_x,z)-height("Outer",x,0)
    assert abs(depth-.00145)<.000025,(x,depth)
checks["Outer"]["shoulder_to_floor_m"]=round(depth,6)
rest=[height("Box",x,z)+.9 for x in [-.0118,0,.0118] for z in [-.070,-.0275,.015]]
assert all(abs(y-.9691)<1e-6 for y in rest)
assert abs(checks["Bed"]["bounds_m"][0][1]+.020)<1e-6
checks["Box"]["leaf_support_world_y_m"]=round(min(rest),6)
checks["Lid"]["minimum_underface_world_y_m"]=round(1.036+checks["Lid"]["bounds_m"][0][1],6)
(out/"fit.json").write_text(json.dumps(checks,indent=2))
print("FBX identity, dimensions, triangle indices, grip cross-sections, channel and rest fit passed.")

