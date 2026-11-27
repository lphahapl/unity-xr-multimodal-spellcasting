"""Discriminate the sculpted mouth notch from an unintended tunnel side opening."""
import json
import sys
from pathlib import Path
sys.dont_write_bytecode=True
sys.path.insert(0,str(Path(__file__).resolve().parent))

import bpy
import numpy as np
from mathutils import Vector
from audit_wrapped_core import WORK,data_of
from optimize_wrapped_core import bvh

raw = bpy.context.scene.objects["SlideSleeve_Source_Hidden"]
refined = bpy.context.scene.objects["SlideSleeve"]
bpy.context.scene.frame_set(1)
trees=[bvh(data_of(obj)) for obj in (raw,refined)]
reply={}
for normal_axis, side_sign, name in ((0,1,"right"),(0,-1,"left"),(1,-1,"front"),(1,1,"back")):
    across=1-normal_axis
    span=.0045 if normal_axis==0 else .007
    rows=[]
    for z in np.linspace(-.016,.0088,33):
        for lateral in np.linspace(-span,span,21):
            origin=Vector((0,0,z))
            origin[normal_axis]=side_sign*.025
            origin[across]=lateral
            direction=Vector((0,0,0))
            direction[normal_axis]=-side_sign
            pair=[tree.ray_cast(origin,direction,.05)[0] for tree in trees]
            values=[None if v is None else float(v[normal_axis]*side_sign*1000) for v in pair]
            if values[0] is not None and (values[1] is None or values[0]-values[1]>.45):
                rows.append([round(float(lateral*1000),3),round(float(z*1000),3),*values])
    reply[name]={"lostOutermostSurfaceSamples":len(rows),"changedMm":rows}
(WORK/"PineInk-SleeveSideAudit.json").write_text(json.dumps(reply,indent=2),encoding="utf8")
print({key:val["lostOutermostSurfaceSamples"] for key,val in reply.items()},flush=True)

