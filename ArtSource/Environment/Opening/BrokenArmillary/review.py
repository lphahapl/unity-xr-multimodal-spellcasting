"""Replay both source and runtime silhouettes, atlas coverage and welded FBX topology."""
import bpy
import numpy as np
import json, os
from pathlib import Path
from mathutils import Vector
from bpy_extras.object_utils import world_to_camera_view
HERE=Path(__file__).resolve().parent
OUT=HERE.parents[3]/'Assets/Art/Environment/Opening/BrokenArmillary'
TMP=Path(os.environ.get('TEMP',str(HERE)))/'KaogongArmillaryReview';TMP.mkdir(parents=True,exist_ok=True)
sc=bpy.context.scene;low=bpy.data.objects['BrokenArmillary'];highs=[o for o in sc.objects if o.type=='MESH' and o!=low]
sc.render.engine='CYCLES';sc.cycles.samples=24;sc.cycles.use_denoising=True
sc.render.threads_mode='FIXED';sc.render.threads=8
sc.render.resolution_x=720;sc.render.resolution_y=720;sc.render.resolution_percentage=100
sc.render.image_settings.file_format='PNG';sc.render.film_transparent=False
world=bpy.data.worlds.new('Canal overcast and stone bounce');sc.world=world;world.use_nodes=True
background=world.node_tree.nodes.get('Background')
background.inputs['Color'].default_value=(.18,.225,.22,1);background.inputs['Strength'].default_value=.89
def light(name,loc,color,power,size):
    bpy.ops.object.light_add(type='AREA',location=loc);o=bpy.context.object;o.name=name
    o.data.energy=power;o.data.color=color;o.data.shape='DISK';o.data.size=size
    o.rotation_euler=(Vector((0,0,1.22))-o.location).to_track_quat('-Z','Y').to_euler()
light('High warm sky',(-3.9,-3.3,6.3),(1,.86,.68),750,4.8)
light('Cool cliff bounce',(4,-3.2,2.8),(.64,.80,1),390,4)
light('Upper edge',(1.1,2.7,4),(1,.82,.54),680,3.1)
bpy.ops.mesh.primitive_plane_add(size=100);floor=bpy.context.object;floor.name='Studio only'
mat=bpy.data.materials.new('Studio ground');mat.diffuse_color=(.075,.091,.089,1);floor.data.materials.append(mat);floor.location.z=-.003
bpy.ops.object.camera_add();camera=bpy.context.object;sc.camera=camera;camera.data.type='ORTHO'
def camera_at(point,direction,scale):
    camera.location=Vector(point)+Vector(direction);camera.rotation_euler=(-Vector(direction)).to_track_quat('-Z','Y').to_euler()
    camera.data.ortho_scale=scale;bpy.context.view_layer.update()
def render(key,showhigh):
    low.hide_render=showhigh
    for ob in highs:ob.hide_render=not showhigh
    sc.render.filepath=str(TMP/(key+'.png'));bpy.ops.render.render(write_still=True)
camera_at((0,0,1.36),(.35,-6,.28),3.35)
render('source',True);render('game',False)
camera_at((.40,-.04,1.68),(.48,-4,.17),1.55);render('break',False)
camera_at((0,0,1.36),(.35,-6,.28),3.35)
wire=[]
for edge in low.data.edges:
    pts=[world_to_camera_view(sc,camera,low.matrix_world@low.data.vertices[i].co) for i in edge.vertices]
    wire.append([[v.x*720,(1-v.y)*720] for v in pts])
uv=low.data.uv_layers.active.data
coord=[[list(uv[i].uv) for i in f.loop_indices] for f in low.data.polygons]
(TMP/'review_geometry.json').write_text(json.dumps({'wire':wire,'uv':coord}),encoding='utf8')
# Fresh FBX import into a new scene: compare source dimensions, UV occupancy, normals and real extents.
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(OUT/'BrokenArmillary.fbx'),use_image_search=False)
meshes=[o for o in bpy.context.scene.objects if o.type=='MESH'];assert len(meshes)==1
ob=meshes[0];me=ob.data;me.calc_loop_triangles()
assert me.uv_layers.active and len(me.materials)==1 and me.materials[0].name=='BrokenArmillary'
assert len(me.loop_triangles)==1776 and all(len(t.vertices)==3 for t in me.polygons)
verts=np.array([list(ob.matrix_world@v.co) for v in me.vertices]);ext=[verts.min(0),verts.max(0)]
assert abs(ext[0][2])<.0005 and ext[1][2]>2.70
print('FBX',ob.name,len(me.vertices),len(me.loop_triangles),ext,flush=True)
r={'blender':bpy.app.version_string,'name':ob.name,'vertices':len(me.vertices),'triangles':len(me.loop_triangles),
   'uv_sets':len(me.uv_layers),'material':me.materials[0].name,'world_min':ext[0].tolist(),'world_max':ext[1].tolist()}
(HERE/'fbx_readback.json').write_text(json.dumps(r,indent=2),encoding='utf8')
