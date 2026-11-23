"""Render the same side, doorway and high-angle views of the three halls, at each revision."""
from pathlib import Path
import sys
import bpy
from mathutils import Vector
root=Path(__file__).resolve().parent
tag=sys.argv[sys.argv.index('--')+1]
s=bpy.context.scene
for ob in s.objects:ob.hide_render=True
s.render.engine='CYCLES';s.cycles.samples=12;s.cycles.use_denoising=True
s.render.threads_mode='FIXED';s.render.threads=8
s.world=bpy.data.worlds.new('Courtyard sky fill');s.world.use_nodes=True
bg=next(n for n in s.world.node_tree.nodes if n.type=='BACKGROUND')
bg.inputs[0].default_value=(.62,.68,.75,1);bg.inputs[1].default_value=.6
bpy.ops.object.light_add(type='SUN');sun=bpy.context.object;sun.data.energy=3.2;sun.data.angle=.11
sun.rotation_euler=Vector((-.6,.7,-1.05)).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add();cam=bpy.context.object;s.camera=cam;cam.data.type='ORTHO'
s.render.image_settings.file_format='JPEG';s.render.image_settings.quality=93
temp=[]
for idx,(hall,target,direction,width) in enumerate([(0,(0,0,1.72),(9,-5,2.1),8.6),(0,(0,0,1.70),(4,-12,2.4),8.9),(0,(0,0,3.0),(3,-5,8),8.5),(1,(0,0,3.0),(3,-5,8),9.2),(2,(0,0,3.0),(3,-5,8),10)]):
    ob=bpy.data.objects[f'CalibrationHall_{hall}_Grounded_LOD0'];ob.hide_render=False
    cam.location=Vector(target)+Vector(direction);cam.rotation_euler=(-Vector(direction)).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=width
    s.render.resolution_x=680;s.render.resolution_y=440;s.render.resolution_percentage=100
    path=root/(f'{tag}_{idx}.jpg');s.render.filepath=str(path);bpy.ops.render.render(write_still=True);ob.hide_render=True
