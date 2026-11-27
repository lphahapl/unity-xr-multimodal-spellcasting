"""Local metadata/media handoff only; never writes the workbench or Unity."""
import json,hashlib,datetime
from pathlib import Path
P=Path(__file__).resolve().parent;LOG=P.parents[4]/'Logs/workshop-master-tenon-qinggui-r02-20261126/model'
def read(p):return json.loads(p.read_text('utf-8'))
def write(p,d):p.write_text(json.dumps(d,ensure_ascii=False,indent=2),'utf-8')
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
root=read(P/'frozen-manifest.json');assert all(sha(Path(i['path']))==i['sha256'] for i in root['core_files'])
entries=[
 ('game_three_view','Previews/12_ThreeView_SameScale.png','真实最终 LOD0 PBR 同尺度正/右/背三视','Actual Blender Cycles render of game LOD0; no Unity shader'),
 ('game_front45','Previews/04_Front45.png','真实最终 LOD0 45°','Actual Blender Cycles game LOD0'),
 ('wire_contact','Previews/05_GoldContact.png','实体金丝近景','Actual game LOD0; discrete round segments visible at extreme zoom'),
 ('aperture','Previews/06_Aperture.png','圆孔口与金丝接头','Actual game LOD0; 2K fine motif and finite-segment ring'),
 ('shoulder','Previews/07_Shoulder.png','肩部、铜玉交界及准线','Actual game LOD0'),
 ('compare_45','Previews/13_R01_R02_45.png','R01 左 / R02 右：同机位游戏模型','Layout of existing actual low-model renders, no regenerated imagery'),
 ('compare_aperture','Previews/14_R01_R02_Aperture.png','R01 左 / R02 右：孔口与表面金丝','Actual low-model comparison, same view/scale'),
 ('compare_shoulder','Previews/15_R01_R02_Shoulder.png','R01 左 / R02 右：肩与细纹','Actual low-model comparison, same view/scale'),
 ('source_three_view','SourcePreview/14_ThreeView_Source.png','源高模玉石材质三视','Blender source high with Cycles optical material; not runtime proof'),
 ('source_polish','SourcePreview/10_PolishStrip.png','源材质窄条侧光：抛磨连续高光','Cycles source high, 4W rectangular strip, world .1'),
 ('source_backlight','SourcePreview/11_Backlight_5W.png','源材质正常 5W 背光：厚薄区别','Cycles source high, 5W rear disc, world zero'),
 ('source_stress_backlight','SourcePreview/12_Backlight_25W_Stress.png','25W 极端背光压力测试','Cycles source high stress test; not normal presentation'),
 ('source_no_light','SourcePreview/13_NoLight.png','关灯/环境光关闭','Actual all-lights/world-zero source render; no emission'),
 ('wire_review','Previews/11_CleanWireFront.png','实际 LOD0 线框','Blender Workbench exact mesh edges'),
 ('model_pbr','QingGuiR02.glb','标准游戏 PBR GLB','Final actual LOD0; opaque conventional PBR; no Unity jade shader'),
 ('model_lookdev','QingGuiR02_Lookdev.glb','标准抛磨/微透 Lookdev GLB','Same actual LOD0 plus KHR transmission/volume/ior/clearcoat; jade-mask gated, no alpha, not Unity/SSS equivalence')]
media=[]
for key,rel,label,origin in entries:
    p=P/rel;assert p.is_file();media.append({'id':key,'path':str(p),'relative_path':rel,'label':label,'origin':origin,'sha256':sha(p),'bytes':p.stat().st_size})
write(P/'media-contract.json',{'asset':'QingGuiR02','frozen_at':root['frozen_at'],'model_manifest':str(P/'frozen-manifest.json'),'media':media,'presentation_rules':['Main three-view is actual game LOD0, not source high.','Label source optics separately from Unity results.','Lookdev needs the four listed KHR extensions; ordinary PBR GLB does not contain Unity half-translucence.','Near-field wire faceting and 2K engraving sampling are accepted limits of this LOD.','Unity/Quest media are supplied separately by the Unity task; this contract contains none.']})
contact=read(LOG/'wire-lod-paths.json');summary={name:max(p['max_radial_clearance_m'] for p in paths) for name,paths in contact.items()}
report=read(P/'model-report.json');report['sampled_wire_contact_max_clearance_m']=summary
warning='LOD1/2 quarter-segment wire contact probes allow maxima +0.010/+0.080 mm respectively, within0.20mm QA contact tolerance; only LOD0 had all sampled probes embedded.'
if warning not in report['accepted_warnings']:report['accepted_warnings'].append(warning)
write(P/'model-report.json',report)
for filename in ['frozen-manifest.json','candidate-ready.json']:
    d=read(P/filename)
    if warning not in d['warnings']:d['warnings'].append(warning)
    write(P/filename,d)
files=[]
for p in sorted(P.rglob('*')):
    if p.is_file() and p.name!='final-file-manifest.json' and p.suffix!='.blend1' and '__pycache__' not in p.parts:files.append({'relative_path':p.relative_to(P).as_posix(),'bytes':p.stat().st_size,'sha256':sha(p)})
write(P/'final-file-manifest.json',{'asset':'QingGuiR02','written_at':datetime.datetime.now(datetime.timezone(datetime.timedelta(hours=8))).isoformat(),'core_frozen_at':root['frozen_at'],'files':files,'exclude':['Blender automatic .blend1 backups','__pycache__','self'],'core_manifest_verified':True})
print('METADATA_READY',len(media),'media',len(files),'files','core verified')
