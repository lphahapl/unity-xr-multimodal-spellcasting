"""LOD geometry registration and visible UV coverage probes after re-import."""
import sys,bpy,math,json
import numpy as np
from pathlib import Path
from mathutils import Vector

def main(model,out):
    bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(model))
    rows=[]
    for ob in sorted((x for x in bpy.context.scene.objects if x.type=="MESH"),key=lambda o:o.name):
        mesh=ob.data;mesh.calc_loop_triangles()
        corners=np.asarray([ob.matrix_world@Vector(v) for v in ob.bound_box])
        uv=mesh.uv_layers.active
        if not uv:raise ValueError("Missing UV "+ob.name)
        coords=np.empty(len(uv.data)*2,dtype='f');uv.data.foreach_get('uv',coords)
        if not np.isfinite(coords).all():raise ValueError("Invalid UV")
        rows.append(dict(mesh=ob.name,verts=len(mesh.vertices),triangles=len(mesh.loop_triangles),low=corners.min(0).tolist(),high=corners.max(0).tolist(),materials=[m.name for m in mesh.materials],uv_min=float(coords.min()),uv_max=float(coords.max())))
    if len(rows)!=3 or not rows[0]["triangles"]>rows[1]["triangles"]>rows[2]["triangles"]:raise ValueError("Incorrect LOD hierarchy")
    out.write_text(json.dumps(rows,indent=2),encoding='utf-8');print("COURT_FBX_READBACK_PASS")
if __name__=="__main__":
    model,out=sys.argv[sys.argv.index("--")+1:];main(Path(model),Path(out))
