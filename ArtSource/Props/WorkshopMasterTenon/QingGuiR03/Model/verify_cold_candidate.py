import bpy,sys,json,hashlib
from pathlib import Path
P=Path(__file__).resolve().parent;LOG=P.parents[4]/'Logs/workshop-master-tenon-qinggui-r03-20261126/model'
sys.path.insert(0,r'C:/Users/鲁迅/.codex/skills/blender-asset-qa/scripts');import qa_check
bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(P/'QingGuiR03_StructureCandidate.fbx'))
obs=[o for o in bpy.context.scene.objects if o.type=='MESH'];assert len(obs)==3
q=qa_check.run(objects=[o.name for o in obs],profile='game',eps=.00001,tol=.00020,max_tris=60000)
record={'file':'QingGuiR03_StructureCandidate.fbx','sha256':hashlib.sha256((P/'QingGuiR03_StructureCandidate.fbx').read_bytes()).hexdigest(),'qa':q,'objects':[{'name':o.name,'location':list(o.location),'dimensions_m':list(o.dimensions),'materials':[m.name for m in o.data.materials],'uv_layers':len(o.data.uv_layers)} for o in obs]}
(LOG/'candidate-fbx-cold-read.json').write_text(json.dumps(record,indent=2),'utf-8');assert q['pass'],q['errors'];print('CANDIDATE_COLD_READ_PASS',q['warnings'])
