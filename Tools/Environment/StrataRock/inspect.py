from pathlib import Path
import bpy,json,hashlib
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[3];LOG=ROOT/'Logs/exterior-polish-20261108/strata-rock';SRC=ROOT/'ArtSource/Environment/Opening/StrataRock'
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(ROOT/'ArtSource/Environment/Opening/HunyuanRock/rock-high.glb'))
meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
report={'objects':[],'images':[],'materials':[]}
for ob in meshes:
 ob.data.calc_loop_triangles();p=[ob.matrix_world@v.co for v in ob.data.vertices]
 report['objects'].append({'name':ob.name,'triangles':len(ob.data.loop_triangles),'vertices':len(ob.data.vertices),'bounds':[[min(v[i] for v in p) for i in range(3)],[max(v[i] for v in p) for i in range(3)]],'uvs':[u.name for u in ob.data.uv_layers],'materials':[m.name for m in ob.data.materials]})
for im in bpy.data.images:report['images'].append({'name':im.name,'size':list(im.size),'packed':bool(im.packed_file),'colorspace':im.colorspace_settings.name})
for m in bpy.data.materials:
 report['materials'].append({'name':m.name,'blend':m.surface_render_method,'nodes':[{'name':n.name,'type':n.type} for n in m.node_tree.nodes]})
(LOG/'input-inspection.json').write_text(json.dumps(report,indent=2),encoding='utf8');print(json.dumps(report),flush=True)
# Normalize imported geometry in a working copy only: 3 m across X, centered at XY and grounded in Z.
bpy.ops.object.select_all(action='DESELECT')
for ob in meshes:ob.select_set(True)
bpy.context.view_layer.objects.active=meshes[0];bpy.ops.object.convert(target='MESH');bpy.ops.object.join();high=bpy.context.object;high.name='StrataRock_High'
bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
p=[high.matrix_world@v.co for v in high.data.vertices];lo=Vector([min(v[i] for v in p) for i in range(3)]);hi=Vector([max(v[i] for v in p) for i in range(3)]);scale=3/(hi.x-lo.x)
for v in high.data.vertices:v.co=(high.matrix_world@v.co-Vector(((lo.x+hi.x)/2,(lo.y+hi.y)/2,lo.z)))*scale
high.matrix_world.identity();high.data.update()
for im in bpy.data.images:
 if im.has_data:im.pack()
bpy.context.scene.unit_settings.system='METRIC';bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(SRC/'StrataRock_inspected.blend'))
sc=bpy.context.scene;sc.render.engine='CYCLES';sc.cycles.samples=16;sc.render.threads_mode='FIXED';sc.render.threads=8
world=bpy.data.worlds.new('Studio');sc.world=world;world.use_nodes=True;world.node_tree.nodes['Background'].inputs[0].default_value=(.4,.45,.5,1);world.node_tree.nodes['Background'].inputs[1].default_value=.5
for loc,power in [((-5,-5,10),1800),((5,4,8),1400)]:
 bpy.ops.object.light_add(type='AREA',location=loc);o=bpy.context.object;o.data.energy=power;o.data.size=6;o.rotation_euler=(Vector((0,0,1.1))-o.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add();cam=bpy.context.object;cam.data.type='ORTHO';cam.data.ortho_scale=4.8;sc.camera=cam
sc.render.resolution_x=1000;sc.render.resolution_y=800;sc.render.resolution_percentage=100
for name,loc in [('high-front',(4,-5,3.5)),('high-back',(-4,5,3.5)),('high-left',(-4,-5,3.5)),('high-top',(0,-.5,7))]:
 cam.location=loc;cam.rotation_euler=(Vector((0,0,1.1))-cam.location).to_track_quat('-Z','Y').to_euler();sc.render.filepath=str(LOG/(name+'.png'));bpy.ops.render.render(write_still=True)
