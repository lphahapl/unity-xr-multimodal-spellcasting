"""Bind the room atlas and FBXs using the project's existing URP importer settings."""
from pathlib import Path
import re,uuid

HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[3]/'Assets/Art/Environment/Opening'
DEST=ROOT/'FurnishWakeTrial'
SOURCE=ROOT/'MeasureRoomProps'

def meta(path):
    return Path(str(path)+'.meta')

def guid(path):
    return re.search(r'(?m)^guid: (\w+)',meta(path).read_text('utf8')).group(1)

def write_meta(path, template, replacements=None):
    new_guid=guid(path) if meta(path).exists() else uuid.uuid5(uuid.NAMESPACE_URL,'NarutoVR/FurnishWakeTrial/'+path.name).hex
    text=meta(template).read_text('utf8').replace(guid(template),new_guid)
    for before,after in (replacements or {}).items():
        text=text.replace(before,after)
    meta(path).write_text(text,encoding='utf8')

write_meta(DEST,SOURCE)
oldmat=SOURCE/'MeasureRoom_Baked.mat'
newmat=DEST/'RoomFurnish_Baked.mat'
data=oldmat.read_text('utf8').replace('MeasureRoom_Baked','RoomFurnish_Baked').replace('  - _EMISSION\n','')
for kind in ('Normal','Mask','BaseColor'):
    tex=DEST/f'RoomFurnish_{kind}.png'; template=SOURCE/f'MeasureRoom_{kind}.png'
    write_meta(tex,template);data=data.replace(guid(template),guid(tex))
newmat.write_text(data,encoding='utf8')
write_meta(newmat,oldmat)
for mesh in DEST.glob('*.fbx'):
    old=SOURCE/('GraduatedRuleRack_'+mesh.stem.split('_')[-1]+'.fbx')
    write_meta(mesh,old,{'MeasureRoom_Baked':'RoomFurnish_Baked',guid(oldmat):guid(newmat)})
