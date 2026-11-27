import json,hashlib,datetime
from pathlib import Path
from PIL import Image
P=Path(__file__).resolve().parent;LOG=P.parents[4]/'Logs/workshop-master-tenon-qinggui-r02-20261126/model'
def read(p):return json.loads(p.read_text('utf-8'))
def write(p,d):p.write_text(json.dumps(d,ensure_ascii=False,indent=2),'utf-8')
def digest(p):return hashlib.sha256(p.read_bytes()).hexdigest()
report=read(P/'model-report.json');cold=read(LOG/'cold-import.json');high=read(LOG/'source-high-qa.json');interface=read(LOG/'measured-interface.json')
assert high['pass'] and interface['protected_0_105_mm']['pass']
assert all(o['qa']['pass'] for f in cold.values() for o in f['objects'])
assert all(o['matches_geometry_candidate'] for o in cold['QingGuiR02_LOD0.fbx']['objects'])
now=datetime.datetime.now(datetime.timezone(datetime.timedelta(hours=8))).isoformat()
core=[P/f for f in ['QingGuiR02_Source.blend','QingGuiR02.blend','QingGuiR02_LOD0.fbx','QingGuiR02_LODs.fbx','QingGuiR02.glb','QingGuiR02_Lookdev.glb','QingGuiR02_GeometryCandidate.fbx']]
textures=sorted((P/'Textures').glob('*.png'));assert len(textures)==9
for p in textures:assert Image.open(p).size==(2048,2048)
sidecars=sorted(P.glob('*.fbm/*.png'));assert len(sidecars)==8
for p in sidecars:assert digest(p)==digest(P/'Textures'/p.name)
files=core+textures+sidecars+[P/'SourceMasks/FinePattern.png']
items=[{'path':str(p),'relative_path':p.relative_to(P).as_posix(),'bytes':p.stat().st_size,'sha256':digest(p)} for p in files]
old=P.parents[1]/'QingGuiR01/Model';prior={
 'QingGui_LOD0.fbx':'c813b0e7632431109b5bb8ea70a5614281be832d4ee1e28cdbdb0bf50e429641',
 'QingGui_LODs.fbx':'834742b44f77fbb217e4abc0be4442447f1104af708add611c3a53d25b553431',
 'QingGuiR01.glb':'58bfa6e6440d5c215b87fcb7ba4cda3e3e0eae4e7ee941e4d32ad14080d43607',
 'QingGuiR01.blend':'8f1e3642ef8bf93a9cae553834aceddde0d847c46a9bf1ff24038e2143405ea0'}
prior_status={name:digest(old/name)==sha for name,sha in prior.items()};assert all(prior_status.values()),prior_status
warnings=[
 'Source high QA: 59 zero-area UV faces and 1 near-duplicate vertex; source uses Object-space procedural/projected masks, not these UVs. No degenerate mesh faces, non-manifold edges, floating parts or z-fighting. Runtime UVs pass.',
 'Source Non-Color FinePattern image is a scalar mask mixing colors; QA color-space warning is expected, not a BaseColor image incorrectly tagged.',
 'LOD1 body/wire has 253 hidden back-to-back triangle pairs from intentional shallow contact of closed solid strands; kept closed. No floating or z-fighting errors.',
 'GLB has 477 Body and 723 GoldWire duplicate-position vertices from UV/tangent seams; wire UV optimization reduced the latter from 10206 without changing any vertex position or triangle index.',
 'glTF exporter combines Metallic/Roughness samplers; both source texture nodes use Linear/Repeat.',
 'LOD0 is 12474 tris, above provisional 10000 target. Parent accepted the actual near-hand budget. LOD1/2 are 6398/3356. Each LOD uses two meshes/materials; Quest performance and shader pass cost remain unmeasured.',
 '2K fine engraving and discrete round-wire segments can be seen in extreme 75mm-frame close-ups; no claim of unlimited zoom or perfectly analytic runtime curves.',
 'Thickness has 0 unresolved misses after 1 retry across607035 rays; 44254 values above120mm are clamped. Normalized data is an approximation, not optical path tracing in Unity.'
]
report.update(status='FROZEN_R02_CORE_PENDING_USER_AESTHETIC_AND_UNITY_HEADSET_ACCEPTANCE',frozen_at=now,accepted_warnings=warnings,
 renderer_material_contract={'QingGuiR02_Body_LOD0/1/2':'QingGuiR02_Body: jade and bronze via masks','QingGuiR02_GoldWire_LOD0/1/2':'QingGuiR02_GoldWire: uniform metal=1, roughness=.24, no transmission'},
 geometry_candidate_signatures={o['name']:o['geometry_signature'] for o in cold['QingGuiR02_LOD0.fbx']['objects']},
 source_high_qa={'pass':high['pass'],'errors':high['errors'],'warnings':high['warnings']},
 source_optics={'jade_roughness':.13,'jade_only_coat':.18,'coat_roughness':.09,'ior':1.53,'transmission_jade_only':.055,'sss_jade_only':.32,'sss_scale_m':.0055,'volume_absorption_density':23,'emission':False},
 lookdev_glb=read(P/'lookdev-glb-report.json'),
 source_geometry='Real rounded shoulder, swept rounded through-hole, feathered bronze relief; long gold/edge/ellipse wires are separate closed meshes with rounded caps and shallow contact. Original crown/collar fine patterns remain inward engraved metallic detail, not raised main wires.',
 reproducibility=['fine_patterns.py','build_source.py','source_optics.py','build_delivery.py','measure_interface.py','make_lookdev_glb.py','verify_delivery.py','review_delivery.py','assemble_review.py','final_source_qa.py','freeze_delivery.py'],
 geometry_only_uv_note='Final wire charts are cylindrical strips plus cap disks. UV-only optimization assertions and candidate cold comparison prove vertex positions and topology unchanged.',
 scope='No formal Assets, Unity project/scene, workbench, prior R01/R02/R03/QingGuiR01 or global handover writes.'
)
write(P/'model-report.json',report)
manifest={'status':'FROZEN','asset':'QingGuiR02','frozen_at':now,'core_files':items,'core_count':len(items),'geometry_candidate_matches_final':True,'prior_qinggui_r01_core_unchanged':prior_status,'texture_policy':'BaseColor sRGB; all eight data maps linear; GoldWire has no texture dependency','warnings':warnings,'review_images':['Previews/12_ThreeView_SameScale.png','Previews/13_R01_R02_45.png','Previews/14_R01_R02_Aperture.png','Previews/15_R01_R02_Shoulder.png','SourcePreview/10_PolishStrip.png','SourcePreview/11_Backlight_5W.png']}
write(P/'frozen-manifest.json',manifest);write(P/'candidate-ready.json',manifest)
print('FROZEN',now)
for p in core:print(p.name,digest(p))
