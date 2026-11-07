import bpy, math, pathlib, json, hashlib, numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree
W=pathlib.Path(__file__).parent; root=W.parent.parent; out=W/'staging'; out.mkdir(exist_ok=True)
src=pathlib.Path.home()/'Downloads'/'Robot_TPose_Rigged.fbx'; maps=src.parent/'机械人_TPose.fbm'
def log(s): print('PIPE',s,flush=True)
def active(o):
 bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active=o
bpy.ops.wm.read_factory_settings(use_empty=True); bpy.context.preferences.filepaths.save_version=0
bpy.ops.import_scene.fbx(filepath=str(src))
high=next(o for o in bpy.context.scene.objects if o.type=='MESH'); rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
rig.data.pose_position='REST'; bpy.context.view_layer.update()
high.name='Source_Robot'; low=high.copy(); low.data=high.data.copy(); bpy.context.collection.objects.link(low); low.name='CanJiOu_Body'
log(('parent',str(low.parent),'matrix',str(low.matrix_world)))
skin=low.modifiers.get('Armature'); low.modifiers.remove(skin)
for uv in list(low.data.uv_layers):low.data.uv_layers.remove(uv)
vg=low.vertex_groups.new(name='_ProtectBends')
for v in low.data.vertices:
 weights=sorted([g.weight for g in v.groups],reverse=True)
 blend=min(1.,(1.-weights[0])*2)
 vg.add([v.index],1.-.70*blend,'REPLACE')
active(low)
mod=low.modifiers.new('Preserve articulated transitions','DECIMATE'); mod.decimate_type='COLLAPSE';mod.ratio=4060/49998;mod.vertex_group=vg.name;mod.vertex_group_factor=1.5;mod.use_collapse_triangulate=True
bpy.ops.object.modifier_apply(modifier=mod.name);low.vertex_groups.remove(low.vertex_groups['_ProtectBends'])
tri=low.modifiers.new('Freeze triangles','TRIANGULATE');bpy.ops.object.modifier_apply(modifier=tri.name)
for v in low.data.vertices:
 groups=sorted([(g.group,g.weight) for g in v.groups if g.weight>0],key=lambda x:-x[1]); assert groups
 keep=groups[:4]; total=sum(t for _,t in keep)
 for i,_ in groups[4:]:low.vertex_groups[i].remove([v.index])
 for i,t in keep:low.vertex_groups[i].add([v.index],t/total,'REPLACE')
arm=low.modifiers.new('Skin','ARMATURE');arm.object=rig
for p in low.data.polygons:p.use_smooth=True
import xatlas
low.data.calc_loop_triangles()
verts=np.array([tuple(v.co) for v in low.data.vertices],dtype=np.float32); faces=np.array([tuple(t.vertices) for t in low.data.loop_triangles],dtype=np.uint32)
at=xatlas.Atlas();at.add_mesh(verts,faces);pk=xatlas.PackOptions();pk.resolution=2048;pk.padding=12;at.generate(pack_options=pk)
vm,ix,uv=at[0];layer=low.data.uv_layers.new(name='CanJiOu_Atlas')
for t,uvtri in zip(low.data.loop_triangles,uv[ix]):
 for loop,pair in zip(t.loops,uvtri):layer.data[loop].uv=pair
for m in (low.data,high.data):m.calc_loop_triangles()
count=len(low.data.loop_triangles);log(('triangles',count)); assert 3200<=count<=4200
mat=high.active_material; nt=mat.node_tree; bs=next(n for n in nt.nodes if n.type=='BSDF_PRINCIPLED'); output=next(n for n in nt.nodes if n.type=='OUTPUT_MATERIAL')
base=bs.inputs['Base Color'].links[0].from_node
base.image=bpy.data.images.load(str(maps/'texture_pbr_20250901.png'));base.image.pack()
def source(kind):
 im=bpy.data.images.load(str(maps/('texture_pbr_20250901_'+kind+'.png')));im.colorspace_settings.name='Non-Color';im.pack()
 n=nt.nodes.new('ShaderNodeTexImage');n.image=im;return n
normal=source('normal');metal=source('metallic');rough=source('roughness')
norm=next(n for n in nt.nodes if n.type=='NORMAL_MAP');nt.links.new(normal.outputs['Color'],norm.inputs['Color'])
nt.links.new(metal.outputs['Color'],bs.inputs['Metallic']);nt.links.new(rough.outputs['Color'],bs.inputs['Roughness'])
lm=bpy.data.materials.new('CanJiOu_Surface');lm.use_nodes=True;low.data.materials.clear();low.data.materials.append(lm)
target=lm.node_tree.nodes.new('ShaderNodeTexImage');lm.node_tree.nodes.active=target
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=4;scene.render.threads_mode='FIXED';scene.render.threads=6
try:
 cp=bpy.context.preferences.addons['cycles'].preferences;cp.compute_device_type='OPTIX';cp.refresh_devices()
 for dev in cp.devices:dev.use=dev.type=='OPTIX'
 if any(dev.use for dev in cp.devices):scene.cycles.device='GPU'
except Exception as ex:log(ex)
scene.render.bake.use_selected_to_active=True
# Corresponding surfaces already coincide. Set the trace distance using the simplified surface error.
world=lambda ob:[ob.matrix_world@v.co for v in ob.data.vertices]
hv=world(high);lv=world(low);bvh=BVHTree.FromPolygons(hv,[t.vertices[:] for t in high.data.loop_triangles],all_triangles=True)
dist=np.array([bvh.find_nearest(sum((lv[i] for i in t.vertices),Vector())/3)[3] for t in low.data.loop_triangles])
cage=float(max(np.percentile(dist,98)*2.5,.003));scene.render.bake.cage_extrusion=cage;scene.render.bake.max_ray_distance=cage*2.5
log(('distance',dist.max(),cage,scene.cycles.device))
active(low);high.select_set(True)
def bake(name,kind,size,linear,socket=None,margin=12):
 im=bpy.data.images.new(name,width=size,height=size,alpha=True);im.colorspace_settings.name='Non-Color' if linear else 'sRGB';target.image=im;lm.node_tree.nodes.active=target
 if socket:
  em=nt.nodes.new('ShaderNodeEmission');nt.links.new(socket,em.inputs['Color']);nt.links.new(em.outputs[0],output.inputs['Surface'])
 scene.render.bake.margin=margin
 log('bake '+name);bpy.ops.object.bake(type=kind);im.filepath_raw=str(out/(name+'.png'));im.file_format='PNG';im.save()
 if socket:nt.links.new(bs.outputs[0],output.inputs[0]);nt.nodes.remove(em)
 return im
white=nt.nodes.new('ShaderNodeRGB');white.outputs[0].default_value=(1,1,1,1)
trial=bake('ProjectionTest','EMIT',512,True,white.outputs[0],0)
# Compare projected white vs UV raster of the low mesh itself.
high.select_set(False);scene.render.bake.use_selected_to_active=False
lowem=lm.node_tree.nodes.new('ShaderNodeEmission');lowout=next(n for n in lm.node_tree.nodes if n.type=='OUTPUT_MATERIAL');lowbs=next(n for n in lm.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
lm.node_tree.links.new(lowem.outputs[0],lowout.inputs[0]); ref=bake('UVFootprint','EMIT',512,True,margin=0)
lm.node_tree.links.new(lowbs.outputs[0],lowout.inputs[0]);lm.node_tree.nodes.remove(lowem);high.select_set(True);scene.render.bake.use_selected_to_active=True
def pix(im):
 a=np.empty(512*512*4,np.float32);im.pixels.foreach_get(a);return a.reshape(-1,4)[:,0]>.5
valid=pix(ref);coverage=float(pix(trial)[valid].mean());log(('projection coverage',coverage));assert coverage>.97
baseim=bake('CanJiOu_BaseColor','EMIT',2048,False,base.outputs[0])
normalim=bake('CanJiOu_Normal','NORMAL',2048,True)
ao=nt.nodes.new('ShaderNodeAmbientOcclusion');ao.inputs['Distance'].default_value=.038;ao.samples=12;ao.only_local=True
comb=nt.nodes.new('ShaderNodeCombineColor')
for idx,sock in enumerate([metal.outputs[0],ao.outputs['AO'],rough.outputs[0]]):nt.links.new(sock,comb.inputs[idx])
packed=bake('Raw_Metal_AO_Rough','EMIT',2048,True,comb.outputs[0])
# Non-color RGBA for URP: metal (R), occlusion (G), spare (B), smoothness (A).
a=np.empty(2048*2048*4,np.float32);packed.pixels.foreach_get(a);a=a.reshape(-1,4);a[:,3]=1-a[:,2];a[:,2]=0
mask=bpy.data.images.new('CanJiOu_Mask',width=2048,height=2048,alpha=True);mask.colorspace_settings.name='Non-Color';mask.alpha_mode='CHANNEL_PACKED';mask.pixels.foreach_set(a.ravel());mask.filepath_raw=str(out/'CanJiOu_Mask.png');mask.file_format='PNG';mask.save()
lt=lm.node_tree;target.image=baseim;lt.links.new(target.outputs[0],lowbs.inputs['Base Color'])
n=lt.nodes.new('ShaderNodeTexImage');n.image=normalim;nm=lt.nodes.new('ShaderNodeNormalMap');lt.links.new(n.outputs[0],nm.inputs['Color']);lt.links.new(nm.outputs[0],lowbs.inputs['Normal'])
n=lt.nodes.new('ShaderNodeTexImage');n.image=mask;se=lt.nodes.new('ShaderNodeSeparateColor');lt.links.new(n.outputs[0],se.inputs[0]);lt.links.new(se.outputs[0],lowbs.inputs['Metallic'])
inv=lt.nodes.new('ShaderNodeMath');inv.operation='SUBTRACT';inv.inputs[0].default_value=1;lt.links.new(n.outputs['Alpha'],inv.inputs[1]);lt.links.new(inv.outputs[0],lowbs.inputs['Roughness'])
for im in (baseim,normalim,mask):im.pack()
high.hide_render=True;high.hide_set(True)
height=max(v.z for v in lv)-min(v.z for v in lv);factor=1.80/height
# Mesh and rig share the same parent transform.
assert low.parent==rig and high.parent==rig
rig.scale*=factor;bpy.context.view_layer.update()
active(low);rig.select_set(True)
bpy.ops.export_scene.fbx(filepath=str(out/'CanJiOu.fbx'),use_selection=True,object_types={'ARMATURE','MESH'},axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=False,use_tspace=True,path_mode='STRIP',use_mesh_modifiers=False)
rig.data.pose_position='POSE'
bpy.ops.wm.save_as_mainfile(filepath=str(W/'CanJiOu_pipeline.blend'),compress=True)
report=dict(source=str(src),source_sha256=hashlib.sha256(src.read_bytes()).hexdigest(),blender=bpy.app.version_string,source_triangles=49998,triangles=count,vertices=len(lv),bones=len(rig.data.bones),vertex_groups=len(low.vertex_groups),materials=1,height_source=height,height_target=1.8,cage_m=cage,ray_distance=cage*2.5,projection_coverage=coverage,max_weights=max(sum(g.weight>0 for g in v.groups) for v in low.data.vertices),texture_size=2048)
(W/'report.json').write_text(json.dumps(report,indent=2),encoding='utf8')
log('BAKE COMPLETE')
import bpy, numpy as np, math, pathlib,json
from mathutils import Vector,Matrix
w=pathlib.Path(__file__).parent
src=bpy.data.objects['CanJiOu_Body'];rig=src.parent;high=bpy.data.objects['Source_Robot']
bpy.context.preferences.filepaths.save_version=0
rig.animation_data_clear()
for b in rig.pose.bones:b.matrix_basis=Matrix.Identity(4)
rig.data.pose_position='REST'
def active(obj):
 bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
low=src.copy();low.data=src.data.copy();bpy.context.collection.objects.link(low);low.name='CanJiOu_Body_Light';low.data.name=low.name
low.modifiers.remove(next(m for m in low.modifiers if m.type=='ARMATURE'));active(low)
mod=low.modifiers.new('LOD1_Collapse','DECIMATE');mod.ratio=2250/4058;mod.use_collapse_triangulate=True
bpy.ops.object.modifier_apply(modifier=mod.name)
for v in low.data.vertices:
 gs=sorted([(g.group,g.weight) for g in v.groups if g.weight>0],key=lambda g:-g[1]);assert gs
 total=sum(g[1] for g in gs[:4])
 for i,t in gs[4:]:low.vertex_groups[i].remove([v.index])
 for i,t in gs[:4]:low.vertex_groups[i].add([v.index],t/total,'REPLACE')
arm=low.modifiers.new('Skin','ARMATURE');arm.object=rig
low.data.calc_loop_triangles();count=len(low.data.loop_triangles);assert count<=2300
# Keep the very same external material and atlas as LOD0.
src.hide_render=True;src.hide_set(True);high.hide_render=True;high.hide_set(True)
active(low);rig.select_set(True)
bpy.ops.export_scene.fbx(filepath=str(w/'CanJiOu_LOD1.fbx'),use_selection=True,object_types={'ARMATURE','MESH'},axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=False,use_tspace=True,path_mode='STRIP',use_mesh_modifiers=False)
print('EXPORT',count,len(low.data.vertices),flush=True)