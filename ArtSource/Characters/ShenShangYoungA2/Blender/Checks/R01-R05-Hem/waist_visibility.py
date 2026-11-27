"""Identify the first surface of the complete opaque garment assembly around the waistband."""
import sys,json,hashlib,math
from pathlib import Path
import bpy,numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree
out=Path(__file__).resolve().parent;ver=sys.argv[-1];f=out.parents[1]/'Rebuild'/ver/f'ShenShang_{ver}.glb'
bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.gltf(filepath=str(f))
verts=[];faces=[];owners=[]
for ob in bpy.context.scene.objects:
 if ob.type!='MESH':continue
 me=ob.data;me.calc_loop_triangles();n=len(verts);verts.extend([ob.matrix_world@v.co for v in me.vertices]);faces.extend([tuple(n+i for i in t.vertices) for t in me.loop_triangles]);owners.extend([ob.name]*len(me.loop_triangles))
tree=BVHTree.FromPolygons(verts,faces,all_triangles=True);data={}
for az in range(0,360,45):
 for elev in (-24,0,24,42):
  a,e=map(math.radians,(az,elev));v=Vector((math.sin(a)*math.cos(e),math.cos(a)*math.cos(e),math.sin(e)));right=(-v).cross(Vector((0,0,1))).normalized();up=right.cross(-v).normalized();center=Vector((0,.006,1.02))+v*.68;npants=0;nb=0;example=[]
  for x in np.linspace(-.198,.198,120):
   for z in np.linspace(-.16,.16,110):
    hit,no,i,dist=tree.ray_cast(center+float(x)*right+float(z)*up,-v,1.3)
    if hit is None or hit.z<.988 or hit.z>1.055:continue
    nb+=1
    if 'bifurcated' in owners[i]:
     npants+=1
     if len(example)<3:example.append([round(c,5) for c in hit])
  data[f'az{az}_elevation{elev}']={'firstHitsInWaistband':nb,'firstHitsTrouserFabric':npants,'example_world_m':example}
dest=out/ver;dest.mkdir(exist_ok=True);rec={'sha256':hashlib.sha256(f.read_bytes()).hexdigest(),'zInterval_m':[.988,1.055],'angles':data,'totalFirstHitsInWaistband':sum(v['firstHitsInWaistband'] for v in data.values()),'totalFirstHitsTrouserFabric':sum(v['firstHitsTrouserFabric'] for v in data.values()),'purpose':'Only exterior first hits count. This range lies at the waist, well above the coat hem.'}
(dest/'WaistVisibility.json').write_text(json.dumps(rec,indent=2),encoding='utf8')
print(ver,rec['totalFirstHitsTrouserFabric'],rec['totalFirstHitsInWaistband'])
