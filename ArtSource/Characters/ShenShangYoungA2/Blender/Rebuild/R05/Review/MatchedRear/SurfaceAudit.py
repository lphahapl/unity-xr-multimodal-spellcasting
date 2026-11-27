import bpy,numpy as np,math,sys,json,hashlib
from mathutils import Vector
from mathutils.bvhtree import BVHTree
from pathlib import Path
v=sys.argv[-1];root=Path('D:/UnityProjects/NarutoVR/ArtSource/Characters/ShenShangYoungA2/Blender/Rebuild');f=root/v/('ShenShang_'+v+'.glb');out=root/'R05/Review/MatchedRear'/v
bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.gltf(filepath=str(f))
oblist=[o for o in bpy.context.scene.objects if o.type=='MESH'];pos=[];tris=[];owners=[];n=0;sk=None;hc=None
for i,o in enumerate(oblist):
 me=o.data;local=np.empty(len(me.vertices)*3,dtype='f');me.vertices.foreach_get('co',local);p=local.reshape(-1,3)@np.asarray(o.matrix_world)[:3,:3].T+np.asarray(o.matrix_world)[:3,3]
 me.calc_loop_triangles();idx=np.empty(len(me.loop_triangles)*3,'i');me.loop_triangles.foreach_get('vertices',idx);idx=idx.reshape(-1,3)
 if 'anatomical head' in o.name or 'swept scalp' in o.name:
  tr=BVHTree.FromPolygons(p.tolist(),idx.tolist(),all_triangles=True)
  if 'swept scalp' in o.name:
   hc=tr;loopids=np.empty(idx.size,'i');me.loop_triangles.foreach_get('loops',loopids);uvs=np.empty(len(me.uv_layers.active.data)*2,'f');me.uv_layers.active.data.foreach_get('uv',uvs);uvs=uvs.reshape(-1,2)[loopids.reshape(-1,3)];capUvs=uvs.mean(1);cp=p;ctri=idx
  else:sk=tr
 pos.extend(p.tolist());tris.extend((idx+n).tolist());owners.extend([o.name]*len(idx));n+=len(p)
alltree=BVHTree.FromPolygons(pos,tris,all_triangles=True)
def iscoat(s):return s is not None and any(k in s.lower() for k in ['jacket','collar','undertunic','short coat','sleeve','sash'])
def closest(p,d,cull=False):
 for _ in range(38):
  h,no,i,dist=alltree.ray_cast(p,d,4)
  if h is None:return None
  if not cull or no.dot(d)<-1e-6:return owners[i]
  p=h+d*.00002
 return None
# The hairline boundary is deliberately translucent; count penetration behind the nape, inside the opaque cap region.
occ={}
for label,view in [('rear',(0,1,0)),('left_3q',(.60,1,.15)),('right_3q',(-.60,1,.15)),('above',(0,1,.72)),('highRear',(-.20,1,1.55))]:
 vv=Vector(view).normalized();dr=-vv;right=dr.cross(Vector((0,0,1))).normalized();up=right.cross(dr).normalized();center=Vector((0,.016,1.613))+vv*.52;ds=[]
 for xp in np.linspace(-.115,.115,155):
  for yp in np.linspace(-.113,.126,157):
   ray=center+right*xp+up*yp;h,no,i,dh=hc.ray_cast(ray,dr,1);skin,_,_,ds0=sk.ray_cast(ray,dr,1)
   if h is None or skin is None or h.y<.018 or no.dot(dr)>-.20 or capUvs[i,1]<.082:continue
   ds.append((ds0-dh)*1000)
 occ[label]={'comparedOpaquePosteriorPixels':len(ds),'skinInFront':int(np.sum(np.asarray(ds)<-.02)),'minimumClearanceAlongRay_mm':round(float(min(ds)),3)}
shoulder={}
for s in [-1,1]:
 c=0;geom=0
 for x in s*np.linspace(.067,.153,100):
  for z in np.linspace(1.362,1.463,102):
   first=closest(Vector((x,.50,z)),Vector((0,-1,0)),True)
   if first is not None:geom+=1
   if first and 'anatomical head' in first:c+=1
 shoulder['positiveX' if s>0 else 'negativeX']={'occupiedSampleRays':geom,'exposedNeckOrShoulderSkin':c}
cull={}
for label,vv0 in [('rear',(0,1,0)),('left_3q',(.62,1,.19)),('right_3q',(-.62,1,.19)),('high',(0,1,.88)),('underHem',(0,1,-.23))]:
 vv=Vector(vv0).normalized();dr=-vv;right=dr.cross(Vector((0,0,1))).normalized();up=right.cross(dr).normalized();ctr=Vector((0,.004,1.16))+.83*vv;counts={'baseVisibleClothing':0,'afterCullingBackground':0,'afterCullingSkin':0,'afterCullingOtherOpaque':0,'backgroundFirstPart':{},'backgroundWorldHits':[]}
 for xp in np.linspace(-.285,.285,138):
  for yp in np.linspace(-.355,.355,167):
   ray=ctr+right*xp+up*yp;first=closest(ray,dr)
   if not iscoat(first):continue
   counts['baseVisibleClothing']+=1;nextn=closest(ray,dr,True)
   if nextn is None:
    counts['afterCullingBackground']+=1;counts['backgroundFirstPart'][first]=counts['backgroundFirstPart'].get(first,0)+1
    counts['backgroundWorldHits'].append([first,*[round(c,5) for c in alltree.ray_cast(ray,dr,4)[0]]])
   elif 'anatomical' in nextn:counts['afterCullingSkin']+=1
   elif not iscoat(nextn):counts['afterCullingOtherOpaque']+=1
 cull[label]=counts
report={'version':v,'modelSHA256':hashlib.sha256(f.read_bytes()).hexdigest(),'opaqueNape':occ,'shoulders':shoulder,'singleSidedClothingRays':cull,'notes':'Camera-to-surface signed separation in millimetres is an oblique ray measurement. Hair and shoulder checks use actual reimported GLB geometry. After-culling other opaque may be a cuff, adjacent garment or underlayer.'}
(out/'SurfaceAudit.json').write_text(json.dumps(report,indent=2));print('AUDITED',v,occ,shoulder,cull)
