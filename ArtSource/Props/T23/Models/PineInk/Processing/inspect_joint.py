"""Author coordinate frames and inspect seam-plane topology for both cloud components."""
import json
import sys
from pathlib import Path

sys.dont_write_bytecode=True
sys.path.insert(0,str(Path(__file__).resolve().parent))

import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Vector

from audit_wrapped_core import WORK, SOURCE, bbox, data_of, digest, load_source, studio_asset
from optimize_wrapped_core import bvh


def object_in_new_frame(obj, mode):
    coords,_=data_of(obj)
    if mode=="core":
        # The original +Y has a clear, wrapped side. Canonical front is Blender -Y.
        basis=Matrix.Diagonal(Vector((-1.0,-1.0,1.0)))
    else:
        centered=coords-coords.mean(axis=0)
        axes,_,_=np.linalg.svd(np.cov(centered.T))
        front=Vector(axes[:,2])
        if front.dot(Vector((0,-1,0)))<0:front.negate()
        seed_axis=Vector((1.,0,1.)).normalized()
        axial=(seed_axis-seed_axis.dot(front)*front).normalized()
        right=(-front).cross(axial).normalized()
        basis=Matrix((right,-front,axial))
    canonical=basis.to_4x4() @ obj.matrix_world
    obj.data.transform(canonical)
    obj.matrix_world=Matrix.Identity(4)
    bpy.context.view_layer.update()
    return [list(row) for row in basis]


def component_groups(edit):
    edit.verts.index_update()
    not_seen=set(edit.verts)
    groups=[]
    while not_seen:
        seed=not_seen.pop()
        stack=[seed]
        group=[seed]
        while stack:
            vertex=stack.pop()
            for edge in vertex.link_edges:
                neighbor=edge.other_vert(vertex)
                if neighbor in not_seen:
                    not_seen.remove(neighbor)
                    stack.append(neighbor)
                    group.append(neighbor)
        groups.append(group)
    return groups


def boundary_loops(edit, verts):
    inside=set(verts)
    neighbors={}
    for edge in edit.edges:
        if len(edge.link_faces)==1 and edge.verts[0] in inside:
            a,b=edge.verts
            neighbors.setdefault(a,[]).append(b)
            neighbors.setdefault(b,[]).append(a)
    if any(len(adjacent)!=2 for adjacent in neighbors.values()):
        return []
    seen=set()
    loops=[]
    for start in neighbors:
        if start in seen:continue
        points=[start]
        last,current=start,neighbors[start][0]
        while current not in points:
            points.append(current)
            a,b=neighbors[current]
            last,current=current,b if a==last else a
        seen.update(points)
        loops.append(points)
    return loops


def area(loop):
    xy=np.array([tuple(v.co)[:2] for v in loop])
    n=np.roll(xy,1,axis=0)
    return abs(np.sum(xy[:,0]*n[:,1]-xy[:,1]*n[:,0]))*.5


def clipped_bmesh(obj, height, keep_upper):
    edit=bmesh.new()
    edit.from_mesh(obj.data)
    bmesh.ops.bisect_plane(edit,geom=list(edit.verts)+list(edit.edges)+list(edit.faces),dist=1e-7,
                          plane_co=(0,0,height),plane_no=(0,0,1),clear_outer=not keep_upper,
                          clear_inner=keep_upper)
    edit.normal_update()
    return edit


def cross_sections(obj, fractions, keep_upper=True):
    data=data_of(obj)
    lo,hi=data[0].min(axis=0),data[0].max(axis=0)
    results=[]
    for fraction in fractions:
        height=lo[2]+fraction*(hi[2]-lo[2])
        edit=clipped_bmesh(obj,height,keep_upper)
        shapes=[]
        for group in component_groups(edit):
            loops=boundary_loops(edit,group)
            shapes.append({
                "vertices":len(group),
                "loops":[{"count":len(loop),"area":round(area(loop),6),
                    "minZ":round(min(point.co.z for point in loop),7),
                    "maxZ":round(max(point.co.z for point in loop),7)} for loop in loops],
                "minZ":round(min(v.co.z for v in group),7),
                "maxZ":round(max(v.co.z for v in group),7),
            })
        results.append({"fraction":fraction,"z":height,"components":sorted(shapes,key=lambda shape:shape["vertices"],reverse=True)})
        edit.free()
    return results


def end_visibility(coords,sign):
    points,_=coords
    tree=bvh(coords)
    lo,hi=points.min(axis=0),points.max(axis=0)
    size=hi-lo
    rays=[]
    for x in np.linspace(lo[0]+.17*size[0],hi[0]-.17*size[0],19):
        for y in np.linspace(lo[1]+.17*size[1],hi[1]-.17*size[1],19):
            z=(hi[2]+.05*size[2]) if sign<0 else (lo[2]-.05*size[2])
            hit,*_=tree.ray_cast(Vector((x,y,z)),Vector((0,0,sign)),float(size[2]*1.15))
            if hit is not None:rays.append(float((hit.z-lo[2])/size[2]))
    return {"hits":len(rays),"percents01_25_50_75_99":[float(v*100) for v in np.percentile(rays,[1,25,50,75,99])]}


def main():
    report={}
    for mode,name,fractions in (
        ("core","WrappedCore",[.12,.16,.21,.24,.28,.31,.35]),
        ("segment","DetachableSegment",[.60,.65,.70,.75,.78,.81,.85,.89])
    ):
        asset=SOURCE.with_name(name+".glb")
        obj=load_source(asset)
        basis=object_in_new_frame(obj,mode)
        data=data_of(obj)
        report[name]={
            "sourceSha256":digest(asset),"frame":basis,"bboxBlenderCanonical":bbox(data[0]),
            "endVisibleFromTop":end_visibility(data,-1),"endVisibleFromBottom":end_visibility(data,1),
            "junctionCandidates":cross_sections(obj,fractions,mode=="core")
        }
        (WORK/"PineInk-JointInvestigation.json").write_text(json.dumps(report,indent=2),encoding="utf8")
        if mode=="segment":
            studio_asset(obj,("SegmentCanonical",{"front":(0,-1,0),"side":(1,0,0),"joint":(0,-.0001,1),"end":(0,-.0001,-1),"angle":(-1,-2,1.5)}))


if __name__=="__main__":
    main()

