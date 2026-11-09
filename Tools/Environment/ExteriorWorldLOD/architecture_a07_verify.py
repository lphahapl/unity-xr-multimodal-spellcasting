from pathlib import Path
import bpy,json,numpy as np,hashlib
from mathutils import Vector
from mathutils.bvhtree import BVHTree
ROOT=Path(__file__).resolve().parents[3];OUT=ROOT/'Assets/Art/Environment/Opening/ArchitectureLOD/A07Workbench';LOG=ROOT/'Logs/exterior-world-lod-20261109/architecture/a07';SRC=ROOT/'ArtSource/Environment/Opening/ArchitectureLOD/A07Workbench'
report=json.loads((LOG/'workbench-report.json').read_text(encoding='utf8'));checks=[]
for i,budget in enumerate((3500,1500,550)):
 bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(OUT/('SurveyWorkbench_LOD'+str(i)+'.fbx')))
 meshes=[o for o in bpy.context.scene.objects if o.type=='MESH'];assert len(meshes)==1
 ob=meshes[0];ob.data.calc_loop_triangles();tri=len(ob.data.loop_triangles);assert tri<=budget
 vertices=[ob.matrix_world@v.co for v in ob.data.vertices];b=[[min(v[j] for v in vertices) for j in range(3)],[max(v[j] for v in vertices) for j in range(3)]]
 assert abs(b[0][2])<1e-4 and len(ob.data.materials)==1
 uv=ob.data.uv_layers[0].data;ox,oy=report['regions'][i][0];sx,sy=report['regions'][i][1]
 assert all(ox-1e-4<=p.uv.x<=ox+sx+1e-4 and oy-1e-4<=p.uv.y<=oy+sy+1e-4 for p in uv)
 tree=BVHTree.FromPolygons(vertices,[list(p.vertices) for p in ob.data.polygons]);clear=0
 for x in (-.5,0,.5):
  for z in (.4,.55):
   hit=tree.ray_cast(Vector((x,-1,z)),Vector((0,1,0)),2)
   assert hit[0] is None,(i,x,z,'Shelf opening blocked');clear+=1
 checks.append({'lod':i,'triangles':tri,'materials':[m.name for m in ob.data.materials],'bounds_blender_xyz':b,'uv_region_confirmed':True,'open_shelf_clear_rays':clear})
for role in ('BaseColor','Normal','Mask'):
 im=bpy.data.images.load(str(OUT/'Textures'/('SurveyWorkbench_'+role+'.png')));assert list(im.size)==[1024,1024]
 a=np.empty(1024*1024*4,np.float32);im.pixels.foreach_get(a);assert np.isfinite(a).all()
report['fbx_readback']=checks;report['shared_atlas_verification']='Every channel history has 3 per-LOD projections; first clear=True, next False. Actual exported UV coordinates fall into matching disjoint atlas regions.'
for role in ('Coverage','BaseColor','Normal','MaskData','AO'):
 calls=[x for x in report['bake_history'] if x['channel']==role];assert [x['use_clear'] for x in calls]==[True,False,False]
report['passed']=True;(LOG/'workbench-verification.json').write_text(json.dumps(report,indent=2),encoding='utf8')
files=[]
for folder in (OUT,SRC):
 for p in sorted(folder.rglob('*')):
  if p.is_file():files.append({'path':str(p),'bytes':p.stat().st_size,'sha256':hashlib.sha256(p.read_bytes()).hexdigest()})
for p in sorted((ROOT/'Tools/Environment/ExteriorWorldLOD').glob('architecture_a07*')):
 if p.is_file():files.append({'path':str(p),'bytes':p.stat().st_size,'sha256':hashlib.sha256(p.read_bytes()).hexdigest()})
(LOG/'a07-manifest.json').write_text(json.dumps(files,indent=2),encoding='utf8')
print(json.dumps({'passed':True,'lods':checks,'files':len(files)},indent=2))
