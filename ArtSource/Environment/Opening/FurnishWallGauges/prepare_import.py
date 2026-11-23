"""Prepare the baked FBX/materials with stable metadata for independent Unity assembly."""
from pathlib import Path
import shutil,re,uuid,sys
source=Path(__file__).resolve().parent
project=source.parents[3]
out=Path(sys.argv[1])/'Assets/Art/Environment/Opening/FurnishWallGauges'
temp=Path(sys.argv[2])
out.mkdir(exist_ok=True,parents=True)
template=project/'Assets/Art/Environment/Opening/Interior'
def guid(name):
    return uuid.uuid5(uuid.NAMESPACE_DNS,'NarutoVR.FurnishWallGauges.'+name).hex
def getmeta(path):
    return Path(str(path)+'.meta')
def old_guid(path):
    return re.search(r'^guid: ([0-9a-f]{32})',getmeta(path).read_text(),re.M).group(1)
def meta(old,new):
    txt=getmeta(old).read_text('utf8').replace(old_guid(old),guid(new.name))
    return txt
getmeta(out).write_text(meta(template,out),encoding='utf8')
mat=(template/'GaugeWorkshop_Baked.mat').read_text('utf8')
mat=mat.replace('GaugeWorkshop_Baked','WallGauge_Baked').replace('  - _EMISSION\n','')
mat=re.sub(r'(- _EmissionMap:\s+m_Texture: )\{[^}]+\}',r'\g<1>{fileID: 0}',mat)
mat=mat.replace('{r: 0.3, g: 0.3, b: 0.3, a: 1}','{r: 0, g: 0, b: 0, a: 1}')
for kind in ('Normal','BaseColor','Mask'):
    old=template/'Textures'/('GaugeWorkshop_'+kind+'.png')
    new=out/('WallGauge_'+kind+'.png')
    shutil.copyfile(temp/new.name,new)
    txt=meta(old,new)
    getmeta(new).write_text(txt,encoding='utf8')
    mat=mat.replace(old_guid(old),guid(new.name))
new=out/'WallGauge_Baked.mat';new.write_text(mat,encoding='utf8')
getmeta(new).write_text(meta(template/'GaugeWorkshop_Baked.mat',new),encoding='utf8')
for fbx in temp.glob('*_LOD?.fbx'):
    new=out/fbx.name;shutil.copyfile(fbx,new)
    txt=meta(template/'WorkshopWallLamp.fbx',new)
    txt=txt.replace('name: GaugeWorkshop_Baked','name: WallGauge_Baked').replace(old_guid(template/'GaugeWorkshop_Baked.mat'),guid('WallGauge_Baked.mat'))
    getmeta(new).write_text(txt,encoding='utf8')

