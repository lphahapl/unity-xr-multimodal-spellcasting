"""Static COLLAPSE decimation + explicit source projection, not animation retopology."""
from pathlib import Path
import bpy,json,math,time,hashlib,numpy as np,bmesh
from mathutils import Vector
from mathutils.bvhtree import BVHTree
ROOT=Path(__file__).resolve().parents[3];SRC=ROOT/'ArtSource/Environment/Opening/Workshop';OUT=ROOT/'Assets/Art/Environment/Opening/Workshop';TEX=OUT/'Textures';LOG=ROOT/'Logs/exterior-polish-20261108/workshop'
bpy.ops.wm.open_mainfile(filepath=str(SRC/'Workshop_inspected.blend'))
bpy.context.preferences.filepaths.save_version=0
sc=bpy.context.scene;sc.render.engine='CYCLES';sc.cycles.samples=16;sc.render.threads_mode='FIXED';sc.render.threads=8
sc.render.bake.use_selected_to_active=True;sc.render.bake.cage_extrusion=.10;sc.render.bake.max_ray_distance=.30
high=bpy.data.objects['Workshop_High'];high.hide_render=False;high.hide_set(False)
def active(ob):
 bpy.ops.object.select_all(action='DESELECT');ob.select_set(True);bpy.context.view_layer.objects.active=ob
def tris(ob):ob.data.calc_loop_triangles();return len(ob.data.loop_triangles)
def stats(ob):
 p=[ob.matrix_world@v.co for v in ob.data.vertices]
 return {'triangles':tris(ob),'vertices':len(ob.data.vertices),'uv_layers':[u.name for u in ob.data.uv_layers],'materials':[m.name for m in ob.data.materials],'bounds_blender_xyz':[[min(v[i] for v in p) for i in range(3)],[max(v[i] for v in p) for i in range(3)]]}
low=high.copy();low.data=high.data.copy();sc.collection.objects.link(low);low.name='Workshop_LOD0';active(low)
# Weld only coincident export splits in the working low mesh, preserving the source.
bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.mesh.remove_doubles(threshold=.00005);bpy.ops.object.mode_set(mode='OBJECT')
before=tris(low)
mod=low.modifiers.new('Static collapse to Quest budget','DECIMATE');mod.decimate_type='COLLAPSE';mod.ratio=9400/before;mod.use_collapse_triangulate=True
bpy.ops.object.modifier_apply(modifier=mod.name)
print('DECIMATED',stats(low),flush=True)
mod=low.modifiers.new('Fixed bake and export triangulation','TRIANGULATE');bpy.ops.object.modifier_apply(modifier=mod.name)
# Tangent normal bake uses this fixed smooth-normal field, unchanged at export.
for p in low.data.polygons:p.use_smooth=True
for layer in list(low.data.uv_layers):low.data.uv_layers.remove(layer)
low.data.uv_layers.new(name='WorkshopAtlasUV')
bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.uv.smart_project(angle_limit=1.0472,island_margin=.0035);bpy.ops.object.mode_set(mode='OBJECT')
lowmat=bpy.data.materials.new('Workshop_Baked');lowmat.use_nodes=True;low.data.materials.clear();low.data.materials.append(lowmat)
for p in low.data.polygons:p.material_index=0
target=lowmat.node_tree.nodes.new('ShaderNodeTexImage');target.name='Bake target';lowmat.node_tree.nodes.active=target
highmat=high.data.materials[0];hn=highmat.node_tree.nodes;hl=highmat.node_tree.links;hbs=hn.get('Principled BSDF');hout=hn.get('Material Output')
em=hn.new('ShaderNodeEmission');em.name='Temporary bake channel'
original={key:(inp.links[0].from_socket if inp.is_linked else tuple(inp.default_value) if hasattr(inp.default_value,'__len__') else inp.default_value) for key,inp in [('base',hbs.inputs['Base Color']),('metal',hbs.inputs['Metallic']),('rough',hbs.inputs['Roughness'])]}
def channel(role):
 for link in list(em.inputs['Color'].links):hl.remove(link)
 if role=='normal' or role=='ao':hl.new(hbs.outputs[0],hout.inputs[0]);return
 hl.new(em.outputs[0],hout.inputs[0])
 if role=='coverage':em.inputs['Color'].default_value=(1,1,1,1)
 elif role=='base':
  value=original['base']
  if hasattr(value,'node'):hl.new(value,em.inputs[0])
  else:em.inputs[0].default_value=value
 elif role=='mask':
  combine=hn.get('Bake mask combine') or hn.new('ShaderNodeCombineColor');combine.name='Bake mask combine'
  for idx,key in [(0,'metal'),(1,'rough')]:
   value=original[key]
   if hasattr(value,'node'):hl.new(value,combine.inputs[idx])
   else:combine.inputs[idx].default_value=value
  combine.inputs[2].default_value=0;hl.new(combine.outputs[0],em.inputs[0])
def px(im):
 a=np.empty(im.size[0]*im.size[1]*4,np.float32);im.pixels.foreach_get(a);return a.reshape(-1,4)
def save(im,name):
 im.file_format='PNG';im.filepath_raw=str(TEX/('Workshop_'+name+'.png'));im.save()
def bake(role,size=2048,save_name=None):
 channel(role)
 im=bpy.data.images.new('Workshop_'+role+str(size),width=size,height=size,alpha=True);im.colorspace_settings.name='sRGB' if role=='base' else 'Non-Color'
 target.image=im;active(low);high.select_set(True)
 bpy.ops.object.bake(type={'base':'EMIT','mask':'EMIT','coverage':'EMIT','normal':'NORMAL','ao':'AO'}[role],use_selected_to_active=True,use_clear=True,margin=8 if size==2048 else 1,normal_space='TANGENT',normal_r='POS_X',normal_g='POS_Y',normal_b='POS_Z')
 if save_name:save(im,save_name)
 print('BAKED',role,size,flush=True);return im
# Compare white high->low projection to low UV coverage in an independent self-bake.
coverage=bake('coverage',256);c=px(coverage)[:,0]
white=lowmat.node_tree.nodes.new('ShaderNodeEmission');white.inputs[0].default_value=(1,1,1,1);lowmat.node_tree.links.new(white.outputs[0],lowmat.node_tree.nodes.get('Material Output').inputs[0])
occupancy=bpy.data.images.new('UV coverage',width=256,height=256,alpha=True);target.image=occupancy;active(low)
bpy.ops.object.bake(type='EMIT',use_selected_to_active=False,margin=1,use_clear=True)
occ=px(occupancy)[:,0]>.9;miss=float(np.mean(c[occ]<.8));print('PROJECTION_MISS',miss,flush=True)
(LOG/'projection-test.json').write_text(json.dumps({'size':256,'cage_extrusion_m':.1,'max_ray_distance_m':.3,'miss_fraction_uv_pixels':miss},indent=2),encoding='utf8')
assert miss<.035,('Projection coverage failed',miss)
lowmat.node_tree.links.new(lowmat.node_tree.nodes.get('Principled BSDF').outputs[0],lowmat.node_tree.nodes.get('Material Output').inputs[0])
bpy.ops.wm.save_as_mainfile(filepath=str(SRC/'Workshop_bake_checkpoint.blend'))
base=bake('base',save_name='BaseColor');normal=bake('normal',save_name='Normal');mask=bake('mask');ao=bake('ao')
m=px(mask);a=px(ao);m[:,3]=1-m[:,1];m[:,1]=a[:,0];mask.pixels.foreach_set(m.ravel());save(mask,'Mask')
hl.new(hbs.outputs[0],hout.inputs[0]);hn.remove(em)
nodes=lowmat.node_tree.nodes;links=lowmat.node_tree.links;bs=nodes.get('Principled BSDF')
def texture(im):node=nodes.new('ShaderNodeTexImage');node.image=im;return node
links.new(texture(base).outputs[0],bs.inputs['Base Color'])
nm=nodes.new('ShaderNodeNormalMap');links.new(texture(normal).outputs[0],nm.inputs['Color']);links.new(nm.outputs[0],bs.inputs['Normal'])
t=texture(mask);sp=nodes.new('ShaderNodeSeparateColor');links.new(t.outputs[0],sp.inputs[0]);links.new(sp.outputs[0],bs.inputs['Metallic']);inv=nodes.new('ShaderNodeMath');inv.operation='SUBTRACT';inv.inputs[0].default_value=1;links.new(t.outputs['Alpha'],inv.inputs[1]);links.new(inv.outputs[0],bs.inputs['Roughness'])
nodes.remove(target);nodes.remove(white)
# LOD1 needs its own projected atlas to avoid long UV seam strips. Keep primary delivery focused.
high.hide_render=True;high.hide_set(True)
active(low)
bpy.ops.export_scene.fbx(filepath=str(OUT/'Workshop.fbx'),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_scale_options='FBX_SCALE_UNITS',bake_space_transform=True,use_tspace=True,add_leaf_bones=False,bake_anim=False,path_mode='STRIP',mesh_smooth_type='FACE')
for im in (base,normal,mask):im.pack()
report={'algorithm':'Static Blender Decimate COLLAPSE; not character retopology','blender':bpy.app.version_string,'input':stats(high),'output':stats(low),'projection_test_miss':miss,'cage_extrusion_m':.1,'max_ray_distance_m':.3,'atlas':2048,'mask':'R metallic, G AO, B unused, A smoothness','normal':'tangent +X +Y +Z','lod1':'not generated','unity_verified':False,'quest_verified':False,'source_sha256':hashlib.sha256((ROOT/'ArtSource/Environment/Opening/HunyuanWorkshop/workshop-high.glb').read_bytes()).hexdigest()}
assert 8000<=tris(low)<=10000,report
(LOG/'report.json').write_text(json.dumps(report,indent=2),encoding='utf8')
bpy.ops.wm.save_as_mainfile(filepath=str(SRC/'Workshop_pipeline.blend'))
print('COMPLETE',json.dumps(report),flush=True)
