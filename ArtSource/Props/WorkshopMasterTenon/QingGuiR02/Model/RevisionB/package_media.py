"""Publish local evidence contracts, checking frozen core files without modifying them."""
import json,hashlib,datetime
from pathlib import Path
P=Path(__file__).resolve().parent
def read(p):return json.loads(p.read_text('utf-8'))
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def write(p,d):p.write_text(json.dumps(d,ensure_ascii=False,indent=2),'utf-8')
frozen=read(P/'frozen-manifest.json');assert frozen['status']=='FROZEN'
for f in frozen['core_files']:assert sha(Path(f['path']))==f['sha256'],f['relative_path']
specs=[
('game_three_view','Previews/12_ThreeView_SameScale.png','B 实际游戏 LOD0 同尺度三视','Actual Blender Cycles game LOD0 renders; layout only, not Unity'),
('game_front45','Previews/04_Front45.png','B 游戏 45°','Actual Blender Cycles game LOD0'),
('normal_back_held','Previews/B_Game_BackHeld.png','B 背面正常持物比例','Actual Blender Cycles game LOD0; orthographic view height .41 m'),
('back_copper_tail','Previews/B_Game_BackCrownCopperTail.png','B 背冠铜唇终止近景','Actual game LOD0; view height .09 m; 2K edge sampling visible'),
('front_copper_tail','Previews/B_Game_FrontCrownCopperTail.png','B 正冠铜唇终止近景','Actual game LOD0; view height .09 m'),
('back_shoulder','Previews/B_Game_BackShoulder.png','B 背肩铜玉边','Actual Blender Cycles game LOD0'),
('left_copper_wrap','Previews/B_Game_LeftCopperWrap.png','B 侧铜包玉接缝','Actual Blender Cycles game LOD0'),
('gold_contact','Previews/05_GoldContact.png','B 实体金丝接触','Actual game LOD0; independent round solid mesh'),
('aperture','Previews/06_Aperture.png','B 孔口近景','Actual Blender Cycles game LOD0'),
('shoulder','Previews/07_Shoulder.png','B 铜肩与浅刻准线','Actual Blender Cycles game LOD0'),
('compare_45','Previews/13_R02_B_45.png','原 R02 / B 同机位同光对照','Actual game LOD0 renders from frozen original R02 and frozen B; layout only'),
('compare_crown','Previews/14_R02_B_Aperture.png','原 R02 / B 冠孔同机位','Actual game LOD0 renders; layout only'),
('compare_shoulder','Previews/15_R02_B_Shoulder.png','原 R02 / B 铜肩同机位','Actual game LOD0 renders; layout only'),
('wireframe','Previews/11_CleanWireFront.png','B 实际游戏线框','Blender Workbench render of final mesh edges'),
('pbr_glb','QingGuiR02B.glb','B 普通 PBR 旋转预览','Final game LOD0; no Unity jade shader'),
('lookdev_glb','QingGuiR02B_Lookdev.glb','B 标准微透/抛磨预览','Same final mesh; KHR transmission/volume/ior/clearcoat; not Unity or SSS'),
]
for p in sorted((P/'Textures').glob('*.png')):specs.append(('map_'+p.stem.replace('QingGui_',''),p.relative_to(P).as_posix(),p.stem+' 2K','Actual high-to-low Cycles bake or explicit channel packing; Thickness from real inward rays'))
entries=[]
for ident,relative,label,origin in specs:
    p=P/relative;assert p.is_file();entries.append({'id':ident,'path':str(p),'relative_path':relative,'label':label,'origin':origin,'sha256':sha(p),'bytes':p.stat().st_size})
media={'status':'FROZEN','asset':'QingGuiR02B','frozen_at':frozen['frozen_at'],'model_manifest':str(P/'frozen-manifest.json'),'media':entries,'presentation_rules':['Show B and original R02 explicitly; do not silently replace old version history.','Blender images are not Unity screenshots. Parent supplies real Unity evidence.','Ordinary GLB has no Unity jade shader; Lookdev GLB needs KHR support and is not SSS.','Normal held-scale and extreme close views both retained; 2K copper-boundary sampling and finite wire segments remain visible.','Original R02 25 core files and B 25 core files are hash-verified unchanged.']}
write(P/'media-contract.json',media)
files=[]
for p in sorted(P.rglob('*')):
    if not p.is_file() or '__pycache__' in p.parts or p.suffix=='.blend1' or p.name=='final-file-manifest.json':continue
    files.append({'path':str(p),'relative_path':p.relative_to(P).as_posix(),'bytes':p.stat().st_size,'sha256':sha(p)})
write(P/'final-file-manifest.json',{'asset':'QingGuiR02B','status':'FROZEN','generated_at':datetime.datetime.now(datetime.timezone(datetime.timedelta(hours=8))).isoformat(),'core_manifest':str(P/'frozen-manifest.json'),'core_verified':True,'files':files})
print('MEDIA_CONTRACT',len(entries),'FILES',len(files),'CORE',len(frozen['core_files']))
