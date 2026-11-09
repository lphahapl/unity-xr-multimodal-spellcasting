"""Real Hunyuan workbench conversion. Independent LOD UVs in disjoint shared atlas regions."""
from pathlib import Path
import bpy,json,hashlib,numpy as np
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[3];SRC=ROOT/'ArtSource/Environment/Opening/ArchitectureLOD/A07Workbench';OUT=ROOT/'Assets/Art/Environment/Opening/ArchitectureLOD/A07Workbench';TEX=OUT/'Textures';LOG=ROOT/'Logs/exterior-world-lod-20261109/architecture/a07'
bpy.ops.wm.read_factory_settings(use_empty=True);bpy.context.preferences.filepaths.save_version=0
source=SRC/'workbench-high.glb';assert hashlib.sha256(source.read_bytes()).hexdigest()=='54b748e8aa2c29ea2ee9f64930eb1f2e52f898766be495204de6da0c6f9f4d1b'
bpy.ops.import_scene.gltf(filepath=str(source));meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
def active(ob):
 bpy.ops.object.select_all(action='DESELECT');ob.select_set(True);bpy.context.view_layer.objects.active=ob
def tris(ob):ob.data.calc_loop_triangles();return len(ob.data.loop_triangles)
def bounds(ob):return [[min(v.co[i] for v in ob.data.vertices) for i in range(3)],[max(v.co[i] for v in ob.data.vertices) for i in range(3)]]
active(meshes[0])
for ob in meshes:ob.select_set(True)
bpy.ops.object.join();high=bpy.context.object;high.name='SurveyWorkbench_High';bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
b=bounds(high);dims=[b[1][i]-b[0][i] for i in range(3)];offset=Vector(((b[0][0]+b[1][0])/2,(b[0][1]+b[1][1])/2,b[0][2]))
for v in high.data.vertices:v.co-=offset
high.scale=(2./dims[0],.85/dims[1],.9/dims[2]);bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
assert len(high.data.materials)==1
sc=bpy.context.scene;sc.unit_settings.system='METRIC';sc.render.engine='CYCLES';sc.cycles.samples=16;sc.render.threads_mode='FIXED';sc.render.threads=8
sc.render.bake.cage_extrusion=.035;sc.render.bake.max_ray_distance=.13
lowmat=bpy.data.materials.new('SurveyWorkbench_Baked');lowmat.use_nodes=True;target=lowmat.node_tree.nodes.new('ShaderNodeTexImage');lowmat.node_tree.nodes.active=target
regions=[((.006,.006),(.988,.732)),((.006,.756),(.69,.232)),((.718,.756),(.27,.232))];lows=[]
for lod,budget in enumerate((3400,1400,500)):
 low=high.copy();low.data=high.data.copy();sc.collection.objects.link(low);low.name='SurveyWorkbench_LOD'+str(lod);active(low)
 bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.mesh.remove_doubles(threshold=.00002);bpy.ops.object.mode_set(mode='OBJECT')
 mod=low.modifiers.new('Static COLLAPSE budget','DECIMATE');mod.ratio=budget/tris(low);mod.use_collapse_triangulate=True;bpy.ops.object.modifier_apply(modifier=mod.name)
 mod=low.modifiers.new('Fixed triangulation before tangent baking','TRIANGULATE');bpy.ops.object.modifier_apply(modifier=mod.name)
 for p in low.data.polygons:p.use_smooth=True
 for uv in list(low.data.uv_layers):low.data.uv_layers.remove(uv)
 low.data.uv_layers.new(name='SharedWorkbenchAtlasUV');bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.uv.smart_project(angle_limit=1.0472,island_margin=.012);bpy.ops.object.mode_set(mode='OBJECT')
 offset,scale=regions[lod]
 for uv in low.data.uv_layers[0].data:uv.uv=(uv.uv.x*scale[0]+offset[0],uv.uv.y*scale[1]+offset[1])
 low.data.materials.clear();low.data.materials.append(lowmat)
 for p in low.data.polygons:p.material_index=0
 lows.append(low);print('LOW',lod,tris(low),bounds(low),flush=True)
hm=high.data.materials[0];hn=hm.node_tree.nodes;hl=hm.node_tree.links;bs=hn.get('Principled BSDF');out=hn.get('Material Output');em=hn.new('ShaderNodeEmission');em.name='Temporary channel projection'
original={key:(inp.links[0].from_socket if inp.is_linked else tuple(inp.default_value) if hasattr(inp.default_value,'__len__') else inp.default_value) for key,inp in [('base',bs.inputs['Base Color']),('metal',bs.inputs['Metallic']),('rough',bs.inputs['Roughness'])]}
def channel(role):
 for link in list(em.inputs[0].links):hl.remove(link)
 if role in ('Normal','AO'):hl.new(bs.outputs[0],out.inputs[0]);return
 hl.new(em.outputs[0],out.inputs[0])
 if role=='Coverage':em.inputs[0].default_value=(1,1,1,1)
 elif role=='BaseColor':
  value=original['base']
  if hasattr(value,'node'):hl.new(value,em.inputs[0])
  else:em.inputs[0].default_value=value
 else:
  combine=hn.get('Material data pack') or hn.new('ShaderNodeCombineColor');combine.name='Material data pack'
  for i,key in ((0,'metal'),(1,'rough')):
   value=original[key]
   if hasattr(value,'node'):hl.new(value,combine.inputs[i])
   else:combine.inputs[i].default_value=value
  combine.inputs[2].default_value=0;hl.new(combine.outputs[0],em.inputs[0])
history=[]
def image(name,size,space):
 im=bpy.data.images.new(name,width=size,height=size,alpha=True);im.colorspace_settings.name=space;return im
def bake(role,size=1024):
 channel(role);im=image(role+str(size),size,'sRGB' if role=='BaseColor' else 'Non-Color');target.image=im
 for lod,low in enumerate(lows):
  active(low);high.select_set(True)
  bpy.ops.object.bake(type={'Normal':'NORMAL','AO':'AO'}.get(role,'EMIT'),use_selected_to_active=True,use_clear=lod==0,margin=3 if size==1024 else 0,normal_space='TANGENT')
  history.append({'channel':role,'size':size,'lod':lod,'use_clear':lod==0,'region':regions[lod]});print('BAKE',role,lod,'clear',lod==0,flush=True)
 return im
def px(im):a=np.empty(im.size[0]*im.size[1]*4,np.float32);im.pixels.foreach_get(a);return a.reshape(-1,4)
coverage=bake('Coverage',256);target.image=image('Expected occupancy',256,'Non-Color');occupancy=target.image
white=lowmat.node_tree.nodes.new('ShaderNodeEmission');white.inputs[0].default_value=(1,1,1,1);lowmat.node_tree.links.new(white.outputs[0],lowmat.node_tree.nodes.get('Material Output').inputs[0])
for i,low in enumerate(lows):active(low);bpy.ops.object.bake(type='EMIT',use_selected_to_active=False,use_clear=i==0,margin=0)
c=px(coverage)[:,0];occ=px(occupancy)[:,0]>.9;miss=float(np.mean(c[occ]<.8));assert miss<.04,miss
lowmat.node_tree.links.new(lowmat.node_tree.nodes.get('Principled BSDF').outputs[0],lowmat.node_tree.nodes.get('Material Output').inputs[0])
def save(im,role):im.file_format='PNG';im.filepath_raw=str(TEX/('SurveyWorkbench_'+role+'.png'));im.save()
base=bake('BaseColor');save(base,'BaseColor');normal=bake('Normal');save(normal,'Normal');mask=bake('MaskData');ao=bake('AO');p=px(mask);p[:,3]=1-p[:,1];p[:,1]=px(ao)[:,0];mask.pixels.foreach_set(p.ravel());save(mask,'Mask')
hl.new(bs.outputs[0],out.inputs[0]);hn.remove(em)
nodes=lowmat.node_tree.nodes;links=lowmat.node_tree.links;bs=nodes.get('Principled BSDF')
def tex(im):n=nodes.new('ShaderNodeTexImage');n.image=im;return n
links.new(tex(base).outputs[0],bs.inputs['Base Color']);nm=nodes.new('ShaderNodeNormalMap');links.new(tex(normal).outputs[0],nm.inputs['Color']);links.new(nm.outputs[0],bs.inputs['Normal'])
t=tex(mask);sp=nodes.new('ShaderNodeSeparateColor');links.new(t.outputs[0],sp.inputs[0]);links.new(sp.outputs[0],bs.inputs['Metallic']);inv=nodes.new('ShaderNodeMath');inv.operation='SUBTRACT';inv.inputs[0].default_value=1;links.new(t.outputs['Alpha'],inv.inputs[1]);links.new(inv.outputs[0],bs.inputs['Roughness']);nodes.remove(target);nodes.remove(white)
# Final ground correction is a per-object translation only; UVs, triangles and normals frozen.
for low in lows:
 b=bounds(low)
 for v in low.data.vertices:v.co.z-=b[0][2]
 active(low);bpy.ops.export_scene.fbx(filepath=str(OUT/(low.name+'.fbx')),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_scale_options='FBX_SCALE_UNITS',bake_space_transform=True,use_tspace=True,bake_anim=False,add_leaf_bones=False,path_mode='STRIP',mesh_smooth_type='FACE')
for im in bpy.data.images:
 if im.has_data:im.pack()
high.hide_render=True;high.hide_set(True)
for i,low in enumerate(lows):low.hide_render=i!=0;low.hide_set(i!=0)
bpy.ops.wm.save_as_mainfile(filepath=str(SRC/'SurveyWorkbench_pipeline.blend'))
report={'source_sha256':hashlib.sha256(source.read_bytes()).hexdigest(),'source_triangles':tris(high),'method':'Static COLLAPSE separately from original high; new UV each LOD, shared disjoint atlas regions; each tangent normal rebaked separately','regions':regions,'bake_history':history,'projection_test_miss':miss,'cage_m':.035,'ray_m':.13,'lods':[{'name':o.name,'triangles':tris(o),'bounds_blender_xyz':bounds(o),'materials':len(o.data.materials)} for o in lows],'atlas':1024,'material_slots':1,'mask':'R metallic G AO A smoothness','unity_verified':False,'usage':'Optional A07 candidate; parent compares to A02 built-in bench before selection. No ArchitectureBuilder changes.'}
(LOG/'workbench-report.json').write_text(json.dumps(report,indent=2),encoding='utf8')
# Actual high/low and underside previews.
world=bpy.data.worlds.new('Studio');sc.world=world;world.use_nodes=True;world.node_tree.nodes['Background'].inputs[0].default_value=(.4,.44,.48,1);world.node_tree.nodes['Background'].inputs[1].default_value=.5
for loc,power in [((-2,-3,4),450),((3,2,3),350)]:
 bpy.ops.object.light_add(type='AREA',location=loc);ob=bpy.context.object;ob.data.energy=power;ob.data.size=3;ob.rotation_euler=(Vector((0,0,.45))-ob.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add();cam=bpy.context.object;cam.data.type='ORTHO';cam.data.ortho_scale=2.7;sc.camera=cam;sc.render.resolution_x=1200;sc.render.resolution_y=850;sc.render.resolution_percentage=100;sc.cycles.samples=24
for name,ob,loc in [('high',high,(2,-3,2)),('LOD0',lows[0],(2,-3,2)),('LOD1',lows[1],(2,-3,2)),('LOD2',lows[2],(2,-3,2)),('back',lows[0],(-2,3,1.5)),('open-shelf',lows[0],(0,-3,.50))]:
 for o in [high,*lows]:o.hide_render=o!=ob;o.hide_set(o!=ob)
 cam.location=loc;cam.rotation_euler=(Vector((0,0,.45))-cam.location).to_track_quat('-Z','Y').to_euler();sc.render.filepath=str(LOG/('workbench-'+name+'.png'));bpy.ops.render.render(write_still=True)
print('A07_COMPLETE',json.dumps(report['lods']),flush=True)
