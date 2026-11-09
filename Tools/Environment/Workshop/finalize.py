from pathlib import Path
import bpy,json,math,numpy as np
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[3];SRC=ROOT/'ArtSource/Environment/Opening/Workshop';OUT=ROOT/'Assets/Art/Environment/Opening/Workshop';LOG=ROOT/'Logs/exterior-polish-20261108/workshop'
bpy.ops.wm.open_mainfile(filepath=str(SRC/'Workshop_pipeline.blend'))
bpy.context.preferences.filepaths.save_version=0
sc=bpy.context.scene;high=bpy.data.objects['Workshop_High'];low=bpy.data.objects['Workshop_LOD0']
lo=Vector([min(v.co[i] for v in low.data.vertices) for i in range(3)]);hi=Vector([max(v.co[i] for v in low.data.vertices) for i in range(3)]);factor=6/(hi.x-lo.x);offset=Vector(((hi.x+lo.x)/2,(hi.y+lo.y)/2,lo.z))
for ob in (high,low):
 for v in ob.data.vertices:v.co=(v.co-offset)*factor
 ob.data.update()
report=json.loads((LOG/'report.json').read_text(encoding='utf8'))
report['final_uniform_adjustment']={'scale':factor,'source_offset':list(offset),'reason':'Ground the reduced mesh at zero and exact 6m width. Uniform transform applied to high and low after bake; no normals/UV/topology changes.'}
report['output']['bounds_blender_xyz']=[[min(v.co[i] for v in low.data.vertices) for i in range(3)],[max(v.co[i] for v in low.data.vertices) for i in range(3)]]
bm=__import__('bmesh').new();bm.from_mesh(low.data)
report['topology']={'boundary_edges':sum(e.is_boundary for e in bm.edges),'nonmanifold_edges':sum(not e.is_manifold for e in bm.edges),'degenerate_triangles':sum(p.area<1e-10 for p in low.data.polygons)};bm.free()
report['uv_bounds']=[min(min(x.uv) for x in low.data.uv_layers[0].data),max(max(x.uv) for x in low.data.uv_layers[0].data)]
(LOG/'report.json').write_text(json.dumps(report,indent=2),encoding='utf8')
bpy.ops.object.select_all(action='DESELECT');low.hide_set(False);low.select_set(True);bpy.context.view_layer.objects.active=low
bpy.ops.export_scene.fbx(filepath=str(OUT/'Workshop.fbx'),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_scale_options='FBX_SCALE_UNITS',bake_space_transform=True,use_tspace=True,add_leaf_bones=False,bake_anim=False,path_mode='STRIP',mesh_smooth_type='FACE')
bpy.ops.wm.save_as_mainfile(filepath=str(SRC/'Workshop_pipeline.blend'))
# Explicit UV wireframe inspection artifact.
uv=low.data.uv_layers[0].data
svg=['<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 2048 2048"><rect width="2048" height="2048" fill="#20252a"/>']
for p in low.data.polygons:
 pts=' '.join(f'{uv[i].uv.x*2048:.2f},{(1-uv[i].uv.y)*2048:.2f}' for i in p.loop_indices)
 svg.append(f'<polygon points="{pts}" fill="none" stroke="#d8be80" stroke-width="0.7"/>')
svg.append('</svg>');(LOG/'uv-layout.svg').write_text('\n'.join(svg))
sc.render.engine='CYCLES';sc.cycles.samples=24;sc.render.threads_mode='FIXED';sc.render.threads=8
world=bpy.data.worlds.new('Review studio');sc.world=world;world.use_nodes=True;world.node_tree.nodes['Background'].inputs[0].default_value=(.4,.45,.5,1);world.node_tree.nodes['Background'].inputs[1].default_value=.5
for loc,power in [((-5,-5,10),1800),((5,4,8),1400)]:
 bpy.ops.object.light_add(type='AREA',location=loc);o=bpy.context.object;o.data.energy=power;o.data.size=6;o.rotation_euler=(Vector((0,0,2.7))-o.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add();cam=bpy.context.object;cam.data.type='ORTHO';cam.data.ortho_scale=9.4;sc.camera=cam
sc.render.resolution_x=1200;sc.render.resolution_y=1000;sc.render.resolution_percentage=100
for name,loc,showhigh in [('comparison-high',(8,-10,7.6),True),('comparison-low',(8,-10,7.6),False),('low-back',(-8,10,7.6),False),('low-left',(-8,-10,7.6),False),('low-top',(0,-1,14),False)]:
 high.hide_set(not showhigh);high.hide_render=not showhigh;low.hide_render=showhigh
 cam.location=loc;cam.rotation_euler=(Vector((0,0,2.7))-cam.location).to_track_quat('-Z','Y').to_euler();sc.render.filepath=str(LOG/(name+'.png'));bpy.ops.render.render(write_still=True)
mat=low.data.materials[0];n=mat.node_tree.nodes;l=mat.node_tree.links;bs=n.get('Principled BSDF');source=bs.inputs['Base Color'].links[0].from_socket
wire=n.new('ShaderNodeWireframe');wire.use_pixel_size=True;wire.inputs['Size'].default_value=.65;mix=n.new('ShaderNodeMixRGB');l.new(wire.outputs[0],mix.inputs[0]);l.new(source,mix.inputs[1]);mix.inputs[2].default_value=(.015,.018,.02,1);l.new(mix.outputs[0],bs.inputs['Base Color'])
cam.location=(8,-10,7.6);cam.rotation_euler=(Vector((0,0,2.7))-cam.location).to_track_quat('-Z','Y').to_euler();sc.render.filepath=str(LOG/'wireframe.png');bpy.ops.render.render(write_still=True)
