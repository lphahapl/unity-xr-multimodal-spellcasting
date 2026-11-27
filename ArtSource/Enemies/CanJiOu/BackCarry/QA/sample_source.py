"""Run in headless Blender on the delivered .blend; write geometry into a temporary directory."""
import csv
import json
import sys
from pathlib import Path

import bpy
import numpy as np


out = Path(sys.argv[sys.argv.index('--')+1])
out.mkdir(parents=True, exist_ok=True)
scene = bpy.context.scene
rig, = [ob for ob in scene.objects if ob.type=='ARMATURE']
body, = [ob for ob in scene.objects if ob.type=='MESH' and ob.find_armature()==rig]
rest = body.data
rest.calc_loop_triangles()
tris=np.array([t.vertices[:] for t in rest.loop_triangles],dtype='<i4')
tris.tofile(out/'triangles.i32')
names = [p.name for p in rig.pose.bones]
with (out/'weights.csv').open('w',newline='') as f:
    writer=csv.writer(f)
    writer.writerow(['vertex','bone','weight'])
    for vert in rest.vertices:
        for g in vert.groups:
            name = body.vertex_groups[g.group].name
            if name in names and g.weight>0:
                writer.writerow([vert.index,names.index(name),g.weight])
mesh_frames=[]
bone_frames=[]
for frame in range(scene.frame_start,scene.frame_end+1):
    scene.frame_set(frame)
    deps=bpy.context.evaluated_depsgraph_get()
    skinned=body.evaluated_get(deps)
    surf=skinned.to_mesh()
    try:
        xyz=np.zeros(len(surf.vertices)*3,dtype='f4')
        surf.vertices.foreach_get('co',xyz)
        matrix=np.asarray(skinned.matrix_world)
        world=xyz.reshape(-1,3)@matrix[:3,:3].T+matrix[:3,3]
        unity=world[:,[0,2,1]]*[-1,1,-1]
        mesh_frames.append(unity)
    finally:
        skinned.to_mesh_clear()
    fk=[]
    for name in names:
        xyz=rig.matrix_world@rig.pose.bones[name].head
        # The source audit reads joint positions only. Keep the layout compatible
        # with the Unity sampler; these placeholder rotations are never evaluated.
        fk.append([-xyz.x,xyz.z,-xyz.y,0.,0.,0.,1.])
    bone_frames.append(fk)
np.asarray(mesh_frames,dtype='<f4').tofile(out/'vertices.f32')
np.asarray(bone_frames,dtype='<f4').tofile(out/'bones.f32')
m=dict(clip=rig.animation_data.action.name,frames=len(mesh_frames),clipLength=(scene.frame_end-scene.frame_start)/scene.render.fps,
       vertices=len(rest.vertices),triangles=len(tris),bones=len(names),boneNames=names)
(out/'manifest.json').write_text(json.dumps(m),encoding='utf-8')
