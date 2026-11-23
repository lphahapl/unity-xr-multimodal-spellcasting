import bpy,math,json
from mathutils import Matrix
from mathutils.bvhtree import BVHTree
from pathlib import Path
def tree(n,angle):
    ob=bpy.data.objects[n]
    mat=ob.matrix_world @ Matrix.Rotation(math.radians(angle),4,"Z")
    return BVHTree.FromPolygons([mat@v.co for v in ob.data.vertices],[tuple(p.vertices) for p in ob.data.polygons],all_triangles=True)
fixed={n:tree(n,0) for n in ["Bed","Inner","Outer"]}
count=0
for a in range(360):
    outer,inner=tree("Outer",a),tree("Inner",a)
    for l,r in [(fixed["Bed"],inner),(fixed["Bed"],outer),(fixed["Outer"],inner)]:
        assert not l.overlap(r),("surface_intersection",a)
        count+=1
print("Surface sweeps",count,"passed")
Path(r"D:/UnityProjects/NarutoVR/ArtSource/Environment/Opening/ConcentricGauge/Bronze/.work/sweep.txt").write_text(f"{count} triangle-BVH intersection checks passed; 360 relative yaw positions for each of Bed/Outer, Bed/Inner and Outer/Inner.")


