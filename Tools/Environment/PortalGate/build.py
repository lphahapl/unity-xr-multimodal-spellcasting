"""Deterministic gate authoring. Blender 5.2 --background --python this-file."""
from pathlib import Path
from math import sin, cos, pi
import bpy, json, math, numpy as np
from mathutils import Vector

ROOT=Path(__file__).resolve().parents[3]
SRC=ROOT/'ArtSource/Environment/Opening/PortalGate'
OUT=ROOT/'Assets/Art/Environment/Opening/PortalGate'
TEX=OUT/'Textures'
LOG=ROOT/'Logs/exterior-polish-20261108/portal'
for p in (SRC,OUT,TEX,LOG):p.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.context.preferences.filepaths.save_version=0
sc=bpy.context.scene;sc.unit_settings.system='METRIC'
sc.render.engine='CYCLES';sc.cycles.samples=8
sc.render.threads_mode='FIXED';sc.render.threads=8
parts=[];materials={}

def material(name,lo,hi,metal,rough,scale):
 m=bpy.data.materials.new(name);m.use_nodes=True
 n=m.node_tree.nodes;l=m.node_tree.links;bs=n.get('Principled BSDF')
 tex=n.new('ShaderNodeTexCoord');mapping=n.new('ShaderNodeVectorMath');mapping.operation='MULTIPLY';mapping.inputs[1].default_value=scale
 l.new(tex.outputs['Object'],mapping.inputs[0])
 noise=n.new('ShaderNodeTexNoise');noise.inputs['Scale'].default_value=3;noise.inputs['Detail'].default_value=4
 l.new(mapping.outputs[0],noise.inputs['Vector'])
 ramp=n.new('ShaderNodeValToRGB');ramp.color_ramp.elements[0].position=.16;ramp.color_ramp.elements[0].color=(*lo,1)
 ramp.color_ramp.elements[1].position=.84;ramp.color_ramp.elements[1].color=(*hi,1)
 l.new(noise.outputs[0],ramp.inputs[0]);l.new(ramp.outputs[0],bs.inputs['Base Color'])
 bs.inputs['Metallic'].default_value=metal;bs.inputs['Roughness'].default_value=rough
 bump=n.new('ShaderNodeBump');bump.inputs['Strength'].default_value=.25;bump.inputs['Distance'].default_value=.002 if metal<.1 else .0005
 l.new(noise.outputs[0],bump.inputs['Height']);l.new(bump.outputs[0],bs.inputs['Normal'])
 materials[m]=(bs,ramp,metal,rough);return m

stone=material('Weathered blue-grey dressed stone',(.105,.135,.139),(.31,.34,.325),0,.86,(3.5,4,4))
wood=material('Oiled dark oak endgrain',(.032,.019,.011),(.14,.076,.034),0,.67,(1,36,32))
bronze=material('Aged bronze with muted patina',(.075,.105,.074),(.33,.205,.082),.68,.48,(12,12,12))
gold=material('Worn rule edges',(.19,.105,.034),(.45,.29,.12),.73,.4,(18,18,18))
dark=material('Incised rule channels',(.018,.027,.025),(.037,.046,.038),.3,.78,(10,10,10))

def active(ob):
 bpy.ops.object.select_all(action='DESELECT');ob.select_set(True);bpy.context.view_layer.objects.active=ob

def box(name,loc,dim,mat,bevel=.012):
 bpy.ops.mesh.primitive_cube_add(size=1,location=loc);ob=bpy.context.object;ob.name=name;ob.dimensions=dim
 bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
 ob.data.materials.append(mat)
 if bevel>.004:
  mod=ob.modifiers.new('Hand dressed chamfers','BEVEL');mod.width=bevel;mod.segments=1
  bpy.ops.object.modifier_apply(modifier=mod.name)
 parts.append(ob);return ob

# Clear opening 1.80 m x 2.70 m. Every jamb course is a solid stone, with deep reveal.
for side in (-1,1):
 x=side*1.115
 box('Foot plinth', (x,0,.085),(.55,.78,.17),stone,.045)
 box('Beveled foot cap',(x,0,.23),(.50,.69,.12),stone,.035)
 for i in range(4):
  h=.598
  box('Jamb dressed course %d'%i,(side*1.115,0,.29+h/2+i*.601),(.43,.58,h),stone,.025 if i!=2 else .037)
 # Bronze channels on broad outward surfaces; repeated both faces.
 for face in (-1,1):
  y=face*.298
  box('Recessed rule bed',(side*1.115,y,1.50),(.105,.018,2.12),dark,.004)
  box('Vertical bronze rule',(side*1.115,y+face*.012,1.50),(.028,.012,2.10),bronze,.003)
  for i in range(9):
   box('Scale index',(side*1.115+side*.023,y+face*.016,.56+i*.225),(.06 if i%4 else .092,.014,.012),gold,.002)
 # Load-spreading stone capital and visible tenon shoulder.
 box('Stone capital',(side*1.115,0,2.735),(.57,.73,.13),stone,.035)
 box('Timber bearing block',(side*1.115,0,2.858),(.47,.64,.11),wood,.015)
 box('Bronze shoulder shoe',(side*1.115,0,2.925),(.49,.66,.045),bronze,.009)

box('Main mortised lintel',(0,0,3.045),(2.72,.66,.25),wood,.028)
box('Lintel crown ledge',(0,0,3.225),(2.79,.76,.11),stone,.028)
# Paired bronze straps wrap across the beam, giving front and rear equal treatment.
for x in (-1.115,1.115):
 for face in (-1,1):
  box('Bronze lintel strap',(x,face*.337,3.045),(.125,.028,.24),bronze,.009)
  for z in (2.976,3.114):
   bpy.ops.mesh.primitive_uv_sphere_add(segments=8,ring_count=4,radius=.022,location=(x,face*.36,z))
   ob=bpy.context.object;ob.name='Square headed bronze fastener';ob.scale=(1,.5,1);ob.data.materials.append(gold);parts.append(ob)
# A restrained central measuring plaque, a true square inside an interrupted circle.
def segment(name,a,b,width,depth,mat):
 a,b=Vector(a),Vector(b);ob=box(name,(a+b)/2,(width,depth,(b-a).length),mat,.002)
 ob.rotation_mode='QUATERNION';ob.rotation_quaternion=(b-a).to_track_quat('Z','Y');return ob
for face in (-1,1):
 y=face*.348
 box('Recessed central bronze plaque',(0,y,3.042),(.39,.028,.218),bronze,.013)
 for x in (-.062,.062):box('Square measurement motif',(x,y+face*.019,3.042),(.008,.012,.124),gold,.001)
 for z in (2.98,3.104):box('Square measurement motif',(0,y+face*.019,z),(.124,.012,.008),gold,.001)
 for i in range(24):
  if i%6==0:continue
  a=2*pi*i/24;b=2*pi*(i+.86)/24
  segment('Circle measurement ticks',(.095*cos(a),y+face*.019,3.042+.095*sin(a)),(.095*cos(b),y+face*.019,3.042+.095*sin(b)),.006,.011,gold)
 # Nested carpenter square motifs left/right, horizontal marks emphasize human scale.
 for side in (-1,1):
  x=side*.63
  box('Inlaid lintel square long',(x,y+face*.004,3.105),(.49,.014,.012),bronze,.002)
  box('Inlaid lintel square return',(x+side*.239,y+face*.004,3.057),(.012,.014,.108),bronze,.002)
  for i in range(5):box('Lintel rule interval',(side*(.43+i*.081),y+face*.004,3.07),(.009,.014,.031 if i%2 else .048),bronze,.001)

# Join actual bevel geometry, unwrap unique UVs, keep an editable source collection copy.
active(parts[0])
for ob in parts:ob.select_set(True)
bpy.ops.object.join();gate=bpy.context.object;gate.name='PortalGate_Mesh'
bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
bpy.context.scene.cursor.location=(0,0,0);bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.uv.smart_project(angle_limit=1.15,island_margin=.008)
bpy.ops.object.mode_set(mode='OBJECT')
gate.data.uv_layers.active.name='AtlasUV'

# Same-mesh shader bake: no speculative selected-to-active rays or overlapping projection.
def target_image(name,space):
 im=bpy.data.images.new(name,width=1024,height=1024,alpha=True);im.colorspace_settings.name=space
 for m in materials:
  n=m.node_tree.nodes;node=n.get('BAKE_TARGET') or n.new('ShaderNodeTexImage');node.name='BAKE_TARGET';node.image=im;n.active=node
 return im
def bake(role):
 im=target_image('PortalGate_'+role,'sRGB' if role=='BaseColor' else 'Non-Color')
 for m,(bs,ramp,metal,rough) in materials.items():
  n=m.node_tree.nodes;l=m.node_tree.links;out=n.get('Material Output')
  if role in ('BaseColor','Mask'):
   em=n.get('Channel bake') or n.new('ShaderNodeEmission');em.name='Channel bake'
   for link in list(em.inputs[0].links):l.remove(link)
   if role=='BaseColor':l.new(ramp.outputs[0],em.inputs[0])
   else:em.inputs[0].default_value=(metal,1.,0.,1-rough)
   l.new(em.outputs[0],out.inputs[0])
  else:l.new(bs.outputs[0],out.inputs[0])
 sc.render.bake.margin=6;sc.render.bake.use_selected_to_active=False
 bpy.ops.object.bake(type={'BaseColor':'EMIT','Mask':'EMIT','Normal':'NORMAL','AO':'AO'}[role],use_clear=True,normal_space='TANGENT')
 print('BAKED',role,flush=True);return im

base=bake('BaseColor');normal=bake('Normal');mask=bake('Mask');ao=bake('AO')
def pixels(im):
 a=np.empty(1024*1024*4,np.float32);im.pixels.foreach_get(a);return a.reshape(-1,4)
a=pixels(ao);b=pixels(base);b[:,:3]*=(.65+.35*a[:,:1]);base.pixels.foreach_set(b.ravel())
p=pixels(mask)
# Mask bake alpha defaults opaque; recover roughness per-material with a dedicated shader channel.
for m,(bs,ramp,metal,rough) in materials.items():
 em=m.node_tree.nodes.get('Channel bake');em.inputs[0].default_value=(metal,1-rough,0,1)
 m.node_tree.links.new(em.outputs[0],m.node_tree.nodes.get('Material Output').inputs[0])
roughim=target_image('SmoothnessTemp','Non-Color');bpy.ops.object.bake(type='EMIT',use_clear=True,margin=6)
r=pixels(roughim);p[:,1]=a[:,0];p[:,3]=r[:,1];mask.pixels.foreach_set(p.ravel())
for m,(bs,ramp,metal,rough) in materials.items():
 m.node_tree.links.new(bs.outputs[0],m.node_tree.nodes.get('Material Output').inputs[0])
for im,role in ((base,'BaseColor'),(normal,'Normal'),(mask,'Mask')):
 im.file_format='PNG';im.filepath_raw=str(TEX/('PortalGate_'+role+'.png'));im.save()
final=bpy.data.materials.new('PortalGate_Baked');final.use_nodes=True;n=final.node_tree.nodes;l=final.node_tree.links;bs=n.get('Principled BSDF')
def tex(im):
 ob=n.new('ShaderNodeTexImage');ob.image=im;return ob
l.new(tex(base).outputs[0],bs.inputs['Base Color'])
nm=n.new('ShaderNodeNormalMap');l.new(tex(normal).outputs[0],nm.inputs['Color']);l.new(nm.outputs[0],bs.inputs['Normal'])
tx=tex(mask);sp=n.new('ShaderNodeSeparateColor');l.new(tx.outputs[0],sp.inputs[0]);l.new(sp.outputs[0],bs.inputs['Metallic'])
inv=n.new('ShaderNodeMath');inv.operation='SUBTRACT';inv.inputs[0].default_value=1;l.new(tx.outputs['Alpha'],inv.inputs[1]);l.new(inv.outputs[0],bs.inputs['Roughness'])
gate.data.materials.clear();gate.data.materials.append(final)
for poly in gate.data.polygons:poly.material_index=0
root=bpy.data.objects.new('PortalGate',None);sc.collection.objects.link(root);gate.parent=root
for name,pos in [('PortalArrival',(0,.95,0)),('PortalTrigger',(0,0,1.35))]:
 ob=bpy.data.objects.new(name,None);sc.collection.objects.link(ob);ob.location=pos;ob.parent=root;ob.empty_display_type='ARROWS';ob.empty_display_size=.24
gate.data.calc_loop_triangles()
coords=[gate.matrix_world@Vector(c) for c in gate.bound_box]
report={'triangles':len(gate.data.loop_triangles),'vertices':len(gate.data.vertices),'material_slots':len(gate.data.materials),'uv_layers':[u.name for u in gate.data.uv_layers],
 'bounds_blender_xyz':[[min(v[i] for v in coords) for i in range(3)],[max(v[i] for v in coords) for i in range(3)]],
 'clear_opening_width_m':1.66,'jamb_opening_width_m':1.8,'clear_opening_height_m':2.67,'texture_size':1024,'mask_channels':'R metallic, G AO, B unused, A smoothness','anchors':'PortalArrival=(0,+.95,0) Blender; PortalTrigger=(0,0,1.35) Blender. Trigger anchor only, no collider/gameplay.',
 'uv_min':min(min(loop.uv) for loop in gate.data.uv_layers[0].data),'uv_max':max(max(loop.uv) for loop in gate.data.uv_layers[0].data)}
assert report['triangles']<=5000,report
assert report['uv_min']>=0 and report['uv_max']<=1.001
(LOG/'validation.json').write_text(json.dumps(report,indent=2),encoding='utf8')
active(gate)
for ob in root.children:ob.select_set(True)
root.select_set(True)
bpy.ops.export_scene.fbx(filepath=str(OUT/'PortalGate.fbx'),use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',apply_scale_options='FBX_SCALE_UNITS',bake_space_transform=True,use_tspace=True,add_leaf_bones=False,bake_anim=False,path_mode='STRIP')
# Neutral studio evidence is source only, not exported.
for im in (base,normal,mask):im.pack()
bpy.ops.wm.save_as_mainfile(filepath=str(SRC/'PortalGate.blend'))
world=sc.world or bpy.data.worlds.new('World');sc.world=world;world.use_nodes=True;world.node_tree.nodes['Background'].inputs[0].default_value=(.35,.39,.42,1);world.node_tree.nodes['Background'].inputs[1].default_value=.5
floor=box('Studio floor',(0,0,-.075),(200,200,.12),stone,.0)
for loc,power,size in [((-3,-4,6),1000,5),((3,2,5),850,4),((2,-1,3),200,3)]:
 bpy.ops.object.light_add(type='AREA',location=loc);ob=bpy.context.object;ob.data.energy=power;ob.data.shape='DISK';ob.data.size=size;ob.rotation_euler=(Vector((0,0,1.5))-ob.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add(location=(4.4,-6.7,3.65));cam=bpy.context.object;cam.data.type='ORTHO';cam.data.ortho_scale=4.25;sc.camera=cam
sc.render.resolution_x=1000;sc.render.resolution_y=1100;sc.render.resolution_percentage=100;sc.cycles.samples=32
sc.view_settings.view_transform='AgX'
for name,loc in [('front',(4.4,-6.7,3.65)),('back',(-4.4,6.7,3.45))]:
 cam.location=loc;cam.rotation_euler=(Vector((0,0,1.61))-cam.location).to_track_quat('-Z','Y').to_euler();sc.render.filepath=str(LOG/(name+'.png'));bpy.ops.render.render(write_still=True)
print(json.dumps(report),flush=True)
