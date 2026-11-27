import json,hashlib,datetime
from pathlib import Path
from PIL import Image
P=Path(__file__).resolve().parent;LOG=P.parents[5]/'Logs/workshop-master-tenon-qinggui-r02-20261126/model/revision-b'
def read(p):return json.loads(p.read_text('utf-8'))
def write(p,d):p.write_text(json.dumps(d,ensure_ascii=False,indent=2),'utf-8')
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
old=read(P.parent/'frozen-manifest.json');oldcheck=[{'relative_path':f['relative_path'],'unchanged':sha(Path(f['path']))==f['sha256']} for f in old['core_files']];assert all(x['unchanged'] for x in oldcheck)
cold=read(LOG/'cold-import.json');high=read(LOG/'source-high-qa.json');interface=read(LOG/'measured-interface.json');report=read(P/'model-report.json')
assert high['pass'] and interface['protected_0_105_mm']['pass'];assert all(o['qa']['pass'] for f in cold.values() for o in f['objects']);assert all(o['matches_geometry_candidate'] for o in cold['QingGuiR02B_LOD0.fbx']['objects'])
core=[P/f for f in ['QingGuiR02B_Source.blend','QingGuiR02B.blend','QingGuiR02B_LOD0.fbx','QingGuiR02B_LODs.fbx','QingGuiR02B.glb','QingGuiR02B_Lookdev.glb','QingGuiR02B_GeometryCandidate.fbx']]
maps=sorted((P/'Textures').glob('*.png'));assert len(maps)==9
for p in maps:assert Image.open(p).size==(2048,2048)
copies=sorted(P.glob('*.fbm/*.png'));assert len(copies)==8
for p in copies:assert sha(p)==sha(P/'Textures'/p.name)
files=core+maps+copies+[P/'SourceMasks/FinePattern.png'];entries=[{'path':str(p),'relative_path':p.relative_to(P).as_posix(),'bytes':p.stat().st_size,'sha256':sha(p)} for p in files]
stamp=datetime.datetime.now(datetime.timezone(datetime.timedelta(hours=8))).isoformat()
warns=sorted(set(w for f in cold.values() for o in f['objects'] for w in o['qa']['warnings']))+high['warnings']
warns+=['Source-only original10um gate reports one pair of aperture faces1um apart; exact projected overlap1.4095e-13m2, no intersection. Parent accepted .5um source distance recheck. Original and precise reports retained; runtime QA unchanged.','Body envelope increases versus R02 by approximately .493mm width/.700mm depth above protected shaft; hand/collision validation belongs to Unity.','Source UV warnings concern Object-space procedural masks; game UVs pass. FinePattern Non-Color is a blend weight, not mis-tagged base color.','Game LOD finite gold-ring segments and2K micro-engraving remain visible at extreme zoom. No headset/performance claim.']
report.update(status='FROZEN',asset='QingGuiR02B',frozen_at=stamp,original_r02_core_unchanged=oldcheck,accepted_warnings=warns,
 geometry_candidate_signatures={o['name']:o['geometry_signature'] for o in cold['QingGuiR02B_LOD0.fbx']['objects']},
 source_geometry='RevisionB: real .50mm bronze wrap step with .30mm narrow rounding and .10mm inward jade-side seam. Stable semantic edge rails in both high and low meshes; smooth derivative copper normals. Sparse low-contrast copper color; independent solid gold wires reprojected to support.',
 protected_insertion_surface=interface['protected_0_105_mm'],
 revision_scope='Copper interface only; C4 outline/true aperture/jade grip profile/0–105mm shaft retained. Original R02 25 frozen core files untouched.',
 renderer_material_contract={'QingGuiR02_Body_LOD0/1/2':'QingGuiR02_Body','QingGuiR02_GoldWire_LOD0/1/2':'QingGuiR02_GoldWire'},
 source_precision_evidence=['source-high-qa-10um-original.json','source-overlap-exact-proof.json','source-high-qa.json'],
 lookdev_glb=read(P/'lookdev-glb-report.json'))
write(P/'model-report.json',report)
manifest={'status':'FROZEN','asset':'QingGuiR02B','frozen_at':stamp,'core_files':entries,'core_count':len(entries),'original_r02_core_unchanged':oldcheck,'geometry_candidate_matches_final':True,'warnings':warns,'media_contract':str(P/'media-contract.json')}
write(P/'frozen-manifest.json',manifest);write(P/'candidate-ready.json',manifest)
print('FROZEN',stamp)
for p in core:print(p.name,sha(p))
