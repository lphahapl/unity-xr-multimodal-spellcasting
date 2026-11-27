"""Candidate UVs and actual isolated JadeCore ray thickness, never B thickness."""
import bpy,bmesh,sys,json,hashlib,math,time
import numpy as np
from pathlib import Path
from mathutils.bvhtree import BVHTree
from mathutils.kdtree import KDTree
SOURCE=Path(__file__).resolve().parent;P=SOURCE/('RevisionA' if '--revision-a' in sys.argv else '.');LOG=SOURCE.parents[4]/'Logs/workshop-master-tenon-qinggui-r03-20261126/model';LOG=LOG/'revision-a' if '--revision-a' in sys.argv else LOG;TEX=P/'CandidateTextures';TEX.mkdir(exist_ok=True)
sys.path.insert(0,r'C:/Users/鲁迅/.codex/skills/blender-asset-qa/scripts');import qa_check
bpy.ops.wm.open_mainfile(filepath=str(P/'QingGuiR03_StructureCandidate.blend'));s=bpy.context.scene
obs=[bpy.data.objects['QingGuiR03_'+n+'_LOD0'] for n in ['JadeCore','BronzeFrame','GoldWire']];jade,bronze,wire=obs
def select(ob):
    for o in s.objects:o.select_set(False)
    ob.select_set(True);bpy.context.view_layer.objects.active=ob
for ob in [jade,bronze]:
    select(ob);bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.uv.smart_project(angle_limit=math.radians(66),island_margin=.015,area_weight=.15,correct_aspect=True,scale_to_bounds=False);bpy.ops.object.mode_set(mode='OBJECT')
me=jade.data;me.update();tree=BVHTree.FromObject(jade,bpy.context.evaluated_depsgraph_get());thickness=[];miss=[]
for i,v in enumerate(me.vertices):
    normal=v.normal.normalized();hit=tree.ray_cast(v.co-normal*.000015,-normal,.6)
    if hit[0] is None:thickness.append(.12);miss.append(i)
    else:thickness.append(max(0,float(hit[3])+.000015))
raw=np.asarray(thickness,np.float32);a=me.attributes.get('JadeThickness') or me.attributes.new('JadeThickness','FLOAT','POINT');a.data.foreach_set('value',np.clip(raw/.12,0,1))
tr={'method':'Actual candidate JadeCore closed mesh only; inward BVH ray per vertex; 15um origin inset; same final candidate UV','rays':len(raw),'miss_count':len(miss),'miss_ids':miss,'miss_fallback_m':.12,'clamped_above_120mm':int(np.count_nonzero(raw>.12)),'min_m':float(raw.min()),'max_m':float(raw.max()),'percentiles_m':{str(q):float(np.percentile(raw,q)) for q in [1,5,25,50,75,95,99]},'decode':'linear R * 0.12 m','source_is_B_thickness':False,'limitation':'Candidate vertex-sampled geometry thickness; final high-detail bake may refine it.'}
(LOG/'candidate-jade-thickness-rays.json').write_text(json.dumps(tr,indent=2),'utf-8')
mat=jade.data.materials[0];nt=mat.node_tree;out=next(n for n in nt.nodes if n.type=='OUTPUT_MATERIAL');bs=next(n for n in nt.nodes if n.type=='BSDF_PRINCIPLED');attr=nt.nodes.new('ShaderNodeAttribute');attr.attribute_name='JadeThickness';em=nt.nodes.new('ShaderNodeEmission');nt.links.new(attr.outputs['Fac'],em.inputs[0]);nt.links.new(em.outputs[0],out.inputs['Surface'])
im=bpy.data.images.new('QingGuiR03_JadeCore_Thickness',2048,2048,alpha=False);im.colorspace_settings.name='Non-Color';node=nt.nodes.new('ShaderNodeTexImage');node.image=im;nt.nodes.active=node
select(jade);s.render.engine='CYCLES';s.cycles.samples=8;s.render.bake.use_selected_to_active=False;s.render.bake.margin=12;bpy.ops.object.bake(type='EMIT')
im.filepath_raw=str(TEX/'QingGuiR03_JadeCore_Thickness.png');im.file_format='PNG';im.save();nt.links.new(bs.outputs[0],out.inputs['Surface']);nt.nodes.remove(em);nt.nodes.remove(attr)
# Stable true per-renderer masks avoid a mixed copper/jade Body texture.
for label,value in [('JadeMask',1.)]:
    mask=bpy.data.images.new('QingGuiR03_JadeCore_'+label,8,8,alpha=False);mask.colorspace_settings.name='Non-Color';mask.pixels.foreach_set([value,value,value,1.]*64);mask.filepath_raw=str(TEX/('QingGuiR03_JadeCore_'+label+'.png'));mask.file_format='PNG';mask.save()
# Verify protected geometry against the frozen functional reference.
reference=SOURCE.parents[1]/'R03/MasterTenon_R03.blend'
with bpy.data.libraries.load(str(reference),link=False) as (src,dst):dst.objects=['MasterTenon_LOD0']
old=dst.objects[0];ov=[v.co.copy() for v in old.data.vertices if v.co.z<=.1050001];nv=[v.co.copy() for v in me.vertices if v.co.z<=.1050001];kd=KDTree(len(nv))
for i,v in enumerate(nv):kd.insert(v,i)
kd.balance();maximum=max(kd.find(v)[2] for v in ov);bpy.data.objects.remove(old)
protected={'old_vertices':len(ov),'new_vertices':len(nv),'max_nearest_difference_m':maximum,'pass':maximum<5e-7};assert protected['pass'],protected
q=qa_check.run(objects=[o.name for o in obs],profile='game',eps=.00001,tol=.00020,max_tris=60000);(LOG/'candidate-qa.json').write_text(json.dumps(q,indent=2),'utf-8');assert q['pass'],q['errors']
for o in s.objects:o.select_set(False)
for o in obs:o.select_set(True)
bpy.context.view_layer.objects.active=jade
bpy.ops.export_scene.fbx(filepath=str(P/'QingGuiR03_StructureCandidate.fbx'),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,mesh_smooth_type='FACE')
bpy.ops.export_scene.gltf(filepath=str(P/'QingGuiR03_StructureCandidate.glb'),use_selection=True,export_format='GLB',export_yup=True)
bpy.ops.wm.save_as_mainfile(filepath=str(P/'QingGuiR03_StructureCandidate.blend'))
report=json.loads((P/'structure-report.json').read_text('utf-8'));report.update(UV_status='Unique smart-projected candidate atlas on JadeCore/BronzeFrame; GoldWire cylinder UV',protected_insertion=protected,thickness=tr,qa=q)
files=[P/'QingGuiR03_StructureCandidate.fbx',P/'QingGuiR03_StructureCandidate.glb',P/'QingGuiR03_StructureCandidate.blend']+list(TEX.glob('*.png'))
report['files']=[{'path':str(f),'relative_path':f.relative_to(P).as_posix(),'sha256':hashlib.sha256(f.read_bytes()).hexdigest(),'bytes':f.stat().st_size} for f in files]
(P/'candidate-ready.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),'utf-8');print('CANDIDATE_READY',protected,tr,flush=True)
