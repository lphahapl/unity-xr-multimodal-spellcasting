"""Native single-atlas architecture mesh authoring. Run in Blender 5.2 background.
Unity coordinates in JSON. Blender preview applies (x,-z,y). No scene mutation.
"""
from pathlib import Path
import json, math, random, hashlib, sys
import bpy, numpy as np
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[3];OUT=ROOT/'Assets/Art/Environment/Opening/ArchitectureLOD';SRC=ROOT/'ArtSource/Environment/Opening/ArchitectureLOD';LOG=ROOT/'Logs/exterior-world-lod-20261109/architecture'
HALLS_ONLY='--halls-only' in sys.argv
COUNT_ONLY='--count-only' in sys.argv
for p in (OUT/'Textures',SRC,LOG):p.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True);bpy.context.preferences.filepaths.save_version=0

# Six 3x2 atlas swatches: grey masonry / oak / fired grey tile / matte bronze /
# cold-white measuring inlay / dark recess. Deterministic technical textures.
N=1024;Y,X=np.mgrid[0:N,0:N];u=(X/(N/3))%1;v=(Y/(N/2))%1
rng=np.random.default_rng(190109);grain=rng.normal(0,1,(N,N));coarse=np.sin(X*.073+np.sin(Y*.037))*np.sin(Y*.057)
base=np.zeros((N,N,4),np.float32);height=np.zeros((N,N),np.float32);metal=np.zeros((N,N),np.float32);rough=np.ones((N,N),np.float32)*.82
for region,color in enumerate(((.31,.35,.35),(.20,.12,.066),(.16,.205,.22),(.31,.235,.115),(.67,.73,.71),(.065,.078,.075))):
 mask=((X//(N/3)).astype(int).clip(0,2)==region%3)&((Y//(N/2)).astype(int)==region//3)
 detail=.018*grain+.022*coarse
 if region==0:
  joints=(np.minimum(v%(.25),.25-v%(.25))<.008)|((np.minimum((u+(np.floor(v*4)%2)*.17)%(.333),.333-(u+(np.floor(v*4)%2)*.17)%(.333)))<.008)
  h=.025*grain+.03*coarse-.15*joints;detail-=joints*.075
 elif region==1:
  fibers=np.sin(v*260+np.sin(u*16)*2)+np.sin(v*510+u*30)*.4
  h=fibers*.06+grain*.015;detail+=fibers*.018
 elif region==2:
  column=np.cos(u*8*math.pi);row=np.minimum((v*7)%1,1-(v*7)%1)<.065
  h=column*.11-row*.13+grain*.012;detail+=column*.021-row*.035
 elif region==3:h=.03*coarse+.012*grain;metal[mask]=.63;rough[mask]=.57
 elif region==4:h=.007*grain;detail*=.3
 else:h=.012*grain;detail*=.3
 for c in range(3):base[:,:,c][mask]=np.clip(color[c]+detail[mask],.01,1)
 height[mask]=h[mask]
base[:,:,3]=1
dy,dx=np.gradient(height);normal=np.stack((-dx*1.5,-dy*1.5,np.ones_like(dx)),axis=-1);normal/=np.linalg.norm(normal,axis=-1,keepdims=True)
normal=np.concatenate((normal*.5+.5,np.ones((N,N,1))),axis=-1)
packed=np.stack((metal,np.ones_like(metal),np.zeros_like(metal),1-rough),axis=-1)
images={}
for name,data,space in [('BaseColor',base,'sRGB'),('Normal',normal,'Non-Color'),('Mask',packed,'Non-Color')]:
 im=bpy.data.images.new('Architecture_'+name,width=N,height=N,alpha=True);im.colorspace_settings.name=space;im.pixels.foreach_set(data.astype(np.float32).ravel());im.filepath_raw=str(OUT/'Textures'/('Architecture_'+name+'.png'));im.file_format='PNG'
 if not HALLS_ONLY and not COUNT_ONLY:im.save()
 im.pack();images[name]=im
mat=bpy.data.materials.new('Architecture_Atlas');mat.use_nodes=True;n=mat.node_tree.nodes;l=mat.node_tree.links;bs=n.get('Principled BSDF')
def tex(im):node=n.new('ShaderNodeTexImage');node.image=im;return node
l.new(tex(images['BaseColor']).outputs[0],bs.inputs['Base Color']);nm=n.new('ShaderNodeNormalMap');l.new(tex(images['Normal']).outputs[0],nm.inputs[0]);l.new(nm.outputs[0],bs.inputs['Normal'])
t=tex(images['Mask']);sp=n.new('ShaderNodeSeparateColor');l.new(t.outputs[0],sp.inputs[0]);l.new(sp.outputs[0],bs.inputs['Metallic']);inv=n.new('ShaderNodeMath');inv.operation='SUBTRACT';inv.inputs[0].default_value=1;l.new(t.outputs['Alpha'],inv.inputs[1]);l.new(inv.outputs[0],bs.inputs['Roughness'])

class Mesh:
 def __init__(self):self.v=[];self.uv=[];self.t=[]
 def face(self,points,region=0):
  idx=len(self.v);self.v.extend(points)
  # Atlas inset protects mips; per-face trim mapping, no cross-swatch repeat.
  x=region%3;y=region//3;uvs=[(.02,.02),(.98,.02),(.98,.98),(.02,.98)] if len(points)==4 else [(.02,.02),(.98,.02),(.5,.98)] if len(points)==3 else [(.5+.48*math.cos(i*2*math.pi/len(points)),.5+.48*math.sin(i*2*math.pi/len(points))) for i in range(len(points))]
  self.uv.extend([((x+a)/3,(y+b)/2) for a,b in uvs])
  for j in range(1,len(points)-1):self.t.extend((idx,idx+j,idx+j+1))
 def box(self,p,s,region=0,bevel=0,rotation=0):
  # Clipped plan corners provide dressed stone/timber silhouettes, not cube piles.
  x,y,z=p;w,h,d=[i/2 for i in s];b=min(bevel,w*.25,d*.25)
  ring=[(-w+b,-d),(w-b,-d),(w,-d+b),(w,d-b),(w-b,d),(-w+b,d),(-w,d-b),(-w,-d+b)] if b else [(-w,-d),(w,-d),(w,d),(-w,d)]
  def point(a,yy):
   xx,zz=a;return(x+xx*math.cos(rotation)+zz*math.sin(rotation),y+yy,z-xx*math.sin(rotation)+zz*math.cos(rotation))
  bottom=[point(a,-h) for a in ring];top=[point(a,h) for a in ring]
  self.face(bottom,region);self.face(list(reversed(top)),region)
  for i in range(len(ring)):j=(i+1)%len(ring);self.face([bottom[j],bottom[i],top[i],top[j]],region)
 def beam(self,a,b,width,depth,region=1):
  a,b=Vector(a),Vector(b);direction=(b-a).normalized();cross=direction.cross(Vector((0,0,1)))
  if cross.length<.1:cross=direction.cross(Vector((1,0,0)))
  cross.normalize();up=direction.cross(cross).normalized()
  points=[tuple(center+cross*s*width/2+up*t*depth/2) for center in (a,b) for s,t in ((-1,-1),(1,-1),(1,1),(-1,1))]
  self.face([points[i] for i in (3,2,1,0)],region);self.face([points[i] for i in (4,5,6,7)],region)
  for i in range(4):j=(i+1)%4;self.face([points[i],points[j],points[4+j],points[4+i]],region)
 def save(self,name):
  data={'vertices':[c for p in self.v for c in p],'uv':[c for p in self.uv for c in p],'triangles':self.t,'name':name}
  if not COUNT_ONLY and (not HALLS_ONLY or name.startswith('CalibrationHall_')):(OUT/(name+'.json')).write_text(json.dumps(data,separators=(',',':')),encoding='utf8')
  mesh=bpy.data.meshes.new(name);mesh.from_pydata([(x,-z,y) for x,y,z in self.v],[],[self.t[i:i+3] for i in range(0,len(self.t),3)]);mesh.materials.append(mat)
  layer=mesh.uv_layers.new(name='ArchitectureAtlasUV')
  for poly in mesh.polygons:
   for loop in poly.loop_indices:layer.data[loop].uv=self.uv[mesh.loops[loop].vertex_index]
  ob=bpy.data.objects.new(name,mesh);bpy.context.scene.collection.objects.link(ob);ob.hide_render=True;ob.hide_set(True)
  return ob,{'triangles':len(self.t)//3,'vertices':len(self.v),'bounds':[[min(p[i] for p in self.v) for i in range(3)],[max(p[i] for p in self.v) for i in range(3)]]}

def roof(m,w,d,rise,lod,axis='z',rib_spacing=.28,soffit=False):
 # Author ridge along Z; outer workshops rotate this kit 90 degrees.
 # Crown fully straight, no upturned corners, no temple ridge ornaments.
 start=len(m.v);hx=w/2+.48;hz=d/2+.50
 nx=2 if lod==0 else 1;nz=max(2,int(d/1.6)) if lod==0 else 2 if lod==1 else 1
 for side in (-1,1):
  for a in range(nx):
   x0=side*hx*a/nx;x1=side*hx*(a+1)/nx;y0=rise*(1-a/nx);y1=rise*(1-(a+1)/nx)
   for b in range(nz):
    z0=-hz+2*hz*b/nz;z1=-hz+2*hz*(b+1)/nz
    pts=[(x0,y0,z0),(x0,y0,z1),(x1,y1,z1),(x1,y1,z0)]
    if side<0:pts.reverse()
    m.face(pts,2)
  m.box((side*hx,-.095,0),(.20,.22,2*hz+.08),1)
  # Fine barrel-tile ridges are supplied by the existing grey-tile normal/atlas.
  # Keep only broad course seams on near roofs, one seam at middle distance.
  for fraction in ((.33,.66) if lod==0 else (.5,) if lod==1 else ()):
   m.box((side*hx*fraction,rise*(1-fraction)+.012,0),(.045,.035,2*hz),2)
 m.box((0,rise+.07,0),(.28,.14,2*hz+.08),2)
 for z in (-hz+.04,hz-.04):
  pts=[(-hx,-.05,z),(hx,-.05,z),(0,rise-.025,z)]
  if z<0:pts.reverse()
  m.face(pts,0)
  for side in (-1,1):m.beam((0,rise+.05,z),(side*hx,.025,z),.16,.18,1)
  m.box((0,.12,z),(w+.45,.26,.19),1)
  if lod<2:
   # Central gable measuring plaque and slender ventilation grille above room windows.
   m.box((0,.53,z+math.copysign(.10,z)),(min(2.2,w*.4),.55,.07),5)
   bars=5 if lod==0 else 3
   for i in range(bars):m.box((-.8+i*(1.6/(bars-1)),.53,z+math.copysign(.15,z)),(.055,.5,.04),1)
   m.box((0,.22,z+math.copysign(.14,z)),(min(3.,w*.6),.08,.035),4)
   # Cold-white trim remains; millimetre-scale tick boxes are omitted at scene scale.
 if soffit:
  # A02 is enterable: downward-facing timber boards under both slopes, plus the
  # two eave rims and four gable-slope rims. 16 triangles total, present at every LOD.
  # Material remains standard backface-culling URP Lit; A01 keeps its original ceiling.
  thickness=.075
  for side in (-1,1):
   plane=[(0,rise,-hz),(0,rise,hz),(side*hx,0,hz),(side*hx,0,-hz)]
   if side<0:plane.reverse()
   bottom=[(x,y-thickness,z) for x,y,z in plane];m.face(list(reversed(bottom)),1)
   rim=[(side*hx,0,-hz),(side*hx,0,hz),(side*hx,-thickness,hz),(side*hx,-thickness,-hz)]
   if side<0:rim.reverse()
   m.face(rim,1)
   for z in (-hz,hz):
    rim=[(0,rise,z),(side*hx,0,z),(side*hx,-thickness,z),(0,rise-thickness,z)]
    if side*z>0:rim.reverse()
    m.face(rim,1)
 if axis=='x':
  m.v[start:]=[(z,y,-x) for x,y,z in m.v[start:]]

def workshop(variant,lod):
 m=Mesh();w=(6.0,6.8,7.3)[variant];d=4.2;eave=(2.85,2.7,2.95)[variant];rise=(1.0,.9,1.02)[variant]
 # Landings match the entrance contract local +Z3.0, floor 0.
 m.box((0,-.12,.30),(w+.7,.24,5.4),0,bevel=.09)
 # Back and side masonry are substantial; front doorway is intentionally OPEN.
 m.box((0,1.23,-d/2+.08),(w,.0+2.46,.22),0,bevel=.04)
 for side in (-1,1):
  m.box((side*(w/2-.1),.57,0),(.22,1.14,d),0,bevel=.03)
  m.box((side*(w/2-.1),2.0,0),(.17,1.0,d),1)
  for z in (-d/2,0,d/2):
   m.box((side*(w/2-.04),1.43,z),(.26,2.86,.28),1,bevel=.025)
   m.box((side*(w/2-.04),.16,z),(.40,.32,.41),0,bevel=.04)
   if lod<2:m.box((side*(w/2-.04),2.6,z),(.29,.11,.31),3)
  m.box((side*(w/2-.04),eave-.15,0),(.30,.28,d+.28),1,bevel=.02)
 # Stone front jambs and open wide mouth, not a solid fake door texture.
 for side in (-1,1):
  m.box((side*(w/4+.525),.56,d/2),((w-2.1)/2,1.12,.22),0,bevel=.025)
  m.box((side*1.20,1.4,d/2),(.27,2.8,.30),1,bevel=.02)
  if lod<2:
   m.box((side*1.20,.13,d/2),(.4,.26,.42),3)
   m.box((side*1.2,1.65,d/2+.16),(.07,1.6,.025),4)
 m.box((0,eave-.17,d/2),(w+.3,.29,.31),1,bevel=.025)
 m.box((0,eave-.13,d/2+.17),(2.6,.11,.035),3)
 offset=len(m.v);roof(m,d,w,rise,lod,'x',.42,True);m.v[offset:]=[(x,y+eave,z) for x,y,z in m.v[offset:]]
 # Accepted A07 bench is a separate independently scaled/LOD'd Unity instance.
 # Do not bake a second table or its old rulers/legs into any of the hall meshes.
 # Only the wall-mounted measuring frame and reference-stock modules remain here.
 if lod<2:
  m.box((w/2-.7,.7,1.22),(.11,1.4,.14),1)
  m.box((w/2-.7,1.45,.82),(.13,.12,.94),3)
  m.box((w/2-.7,1.15,.41),(.06,.6,.06),4)
 if lod==0:
  # Stacked stock with exposed cut ends, not generic decorative pots.
  for row in range(2):
   for i in range(4):m.box((-w/2+.55+row*.06,.18+row*.15,-.75+i*.17),(.8,.14,.14),1,bevel=.015)
  for i in range(5):m.box((w/2-.65+i*.09,.86,-1.17),(.04,1.72,.045),4)
  # Strong geometric knee braces at the column heads remain above the open doorway.
  for side in (-1,1):m.beam((side*(w/2-.06),2.15,2.1),(side*(w/2-.75),2.6,2.1),.13,.16,1)
 return m

records={};objects={}
for variant in range(3):
 for lod in range(3):
  name=f'CalibrationHall_{variant}_LOD{lod}';objects[name],records[name]=workshop(variant,lod).save(name)
for kind,w,d,rise in [('Wake',13.2,13.35,1.45),('Corridor',6.6,8.,1.20),('Trial',14.9,16.6,1.45)]:
 for lod in range(3):
  m=Mesh();roof(m,w,d,rise,lod);name=f'Roof_{kind}_LOD{lod}';objects[name],records[name]=m.save(name)
# Static budget of all six near kits; runtime foundation/columns add <1800 triangles.
near=sum(r['triangles'] for name,r in records.items() if name.endswith('LOD0'))
if COUNT_ONLY:
 print('ARCHITECTURE_COUNT_ONLY',json.dumps({'source_near':near,'support':1488,'benches':10200,'total':near+1488+10200,'lods':{k:r['triangles'] for k,r in records.items()}}),flush=True);raise SystemExit
assert near+1488+3*3400<18000,(near,records)
for prefix in ('CalibrationHall_0','CalibrationHall_1','CalibrationHall_2','Roof_Wake','Roof_Corridor','Roof_Trial'):
 counts=[records[prefix+f'_LOD{i}']['triangles'] for i in range(3)];assert counts[0]>counts[1]>counts[2],counts
report={'atlas':'1024 RGBA BaseColor / tangent normal / R metallic G neutral AO A smoothness; 6 trim regions, one URP material','near_mesh_triangles':near,'dynamic_support_upper_bound_triangles':1488,'independent_a07_near_triangles':10200,'near_total_with_accepted_benches':near+1488+10200,'old_combined_table_removed':True,'halls_only_rebuild':HALLS_ONLY,'lods':records,'doorway':'Front +Z, local door center z2.1, landing connection z3.0, floor0; clear width2.1m; no door slab','style':'Straight gable roofs; grey stone / oak / matte bronze / white rule inlay; no flying corners or temple ornaments','unity_verified':False}
report['A02_roof_soffit']={'reason':'V6 inside view saw sky through backfaces of the original top-only roof','thickness_m':.075,'added_triangles_per_hall_per_lod':16,'all_three_lods':True,'A01_soffit_added':False,'material_cull_changed':False}
report['performance_revision']={'near_architecture_budget':18000,'A07_near_kept':3400,'A07_atlas_modified':False,'roof_treatment':'Remove modeled fine tile ribs and tiny index boxes; reduce roof panel grid and small trim chamfers. Preserve straight eaves, ridge, primary gable beams, bronze straps, ventilation bars and A02 wood soffits. Existing atlas carries tile relief.','LOD_thresholds_modified':False}
(LOG/'geometry-report.json').write_text(json.dumps(report,indent=2),encoding='utf8')
bench_objects=[]
bench_source=SRC/'A07Workbench/SurveyWorkbench_pipeline.blend'
with bpy.data.libraries.load(str(bench_source),link=False) as (available,loaded):loaded.objects=['SurveyWorkbench_LOD'+str(i) for i in range(3)]
for i,ob in enumerate(loaded.objects):
 bpy.context.scene.collection.objects.link(ob);ob.name='AcceptedA07_IndependentLOD'+str(i);ob.location=(0,-.5,0);ob.hide_render=True;ob.hide_set(True);bench_objects.append(ob)
bpy.ops.wm.save_as_mainfile(filepath=str(SRC/'ArchitectureLOD.blend'))

# Honest standalone previews from the very JSON meshes Unity receives.
sc=bpy.context.scene;sc.render.engine='CYCLES';sc.cycles.samples=24;sc.render.threads_mode='FIXED';sc.render.threads=8
world=bpy.data.worlds.new('Studio');sc.world=world;world.use_nodes=True;world.node_tree.nodes['Background'].inputs[0].default_value=(.34,.39,.43,1);world.node_tree.nodes['Background'].inputs[1].default_value=.5
for loc,power,size in [((-5,7,10),1700,7),((7,-5,7),1200,6)]:
 bpy.ops.object.light_add(type='AREA',location=loc);ob=bpy.context.object;ob.data.energy=power;ob.data.size=size;ob.rotation_euler=(Vector((0,0,2))-ob.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add();cam=bpy.context.object;cam.data.type='ORTHO';sc.camera=cam;sc.render.resolution_x=1200;sc.render.resolution_y=900;sc.render.resolution_percentage=100
for name in ['CalibrationHall_0_LOD0','CalibrationHall_0_LOD1','CalibrationHall_0_LOD2','CalibrationHall_1_LOD0','CalibrationHall_2_LOD0','Roof_Wake_LOD0']:
 if HALLS_ONLY and name.startswith('Roof'):continue
 ob=objects[name];ob.hide_render=False;ob.hide_set(False)
 for i,bench in enumerate(bench_objects):bench.hide_render=name.startswith('Roof') or i!=int(name[-1]);bench.hide_set(bench.hide_render)
 if name.startswith('Roof'):
  cam.location=(15,18,12);cam.data.ortho_scale=22;aim=Vector((0,0,.5))
 else:cam.location=(9,-12,8);cam.data.ortho_scale=11;aim=Vector((0,0,1.65))
 cam.rotation_euler=(aim-cam.location).to_track_quat('-Z','Y').to_euler();sc.render.filepath=str(LOG/(name+'.png'));bpy.ops.render.render(write_still=True);ob.hide_render=True;ob.hide_set(True)
# Local interior eye position looks toward the roof, not a top-down product shot.
# Preview-only fill allows inspection of a closed ceiling blocking the outdoor lamps.
bpy.ops.object.light_add(type='AREA',location=(0,-.6,1.8));fill=bpy.context.object;fill.data.energy=90;fill.data.size=1.5;fill.rotation_euler=(Vector((0,.3,3.2))-fill.location).to_track_quat('-Z','Y').to_euler()
for lod in range(3):
 for ob in objects.values():ob.hide_render=True;ob.hide_set(True)
 ob=objects['CalibrationHall_0_LOD'+str(lod)];ob.hide_render=False;ob.hide_set(False)
 for bench in bench_objects:bench.hide_render=True;bench.hide_set(True)
 cam.data.type='PERSP';cam.data.lens=22;cam.location=(0,-1.2,1.6);cam.rotation_euler=(Vector((0,.5,3.7))-cam.location).to_track_quat('-Z','Y').to_euler();sc.render.filepath=str(LOG/('A02-soffit-interior-LOD'+str(lod)+'.png'));bpy.ops.render.render(write_still=True)
print('ARCHITECTURE_COMPLETE',near,flush=True)
