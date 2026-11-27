"""Thin edge curves avoid Wireframe modifier mitre spikes on acute triangulated faces."""
import bpy,math,json
from pathlib import Path
from mathutils import Vector
OUT=Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(OUT/'MasterTenon_R01.blend'))
s=bpy.context.scene;ob=bpy.data.objects['MasterTenon_LOD0']
for o in s.objects:o.hide_render=o!=ob
ob.hide_set(False);ob.color=(.7,.74,.75,1)
cu=bpy.data.curves.new('Exact mesh edge lines','CURVE');cu.dimensions='3D';cu.bevel_depth=.00008;cu.bevel_resolution=0;cu.resolution_u=1
for e in ob.data.edges:
    sp=cu.splines.new('POLY');sp.points.add(1)
    for i,vi in enumerate(e.vertices):sp.points[i].co=(*ob.data.vertices[vi].co,1)
wire=bpy.data.objects.new('Exact mesh edges',cu);s.collection.objects.link(wire);wire.color=(.015,.025,.035,1)
s.render.engine='BLENDER_WORKBENCH';s.display.shading.color_type='OBJECT';s.display.shading.light='STUDIO';s.display.shading.show_shadows=True;s.display.shading.show_cavity=False
s.render.resolution_x=800;s.render.resolution_y=1000;s.render.resolution_percentage=100
cam=bpy.data.objects['Review_Camera'];s.camera=cam;cam.data.ortho_scale=.43
for name,loc in [('11_CleanWireFront',(0,-.75,.175)),('12_CleanWire45',(.44,-.64,.43))]:
    cam.location=loc;cam.rotation_euler=(Vector((0,0,.175))-cam.location).to_track_quat('-Z','Y').to_euler()
    s.render.filepath=str(OUT/'Previews'/f'{name}.png');bpy.ops.render.render(write_still=True)
print('CLEAN_EDGE_REVIEW_DONE')
