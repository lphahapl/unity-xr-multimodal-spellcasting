"""Generate only this new asset's Unity importer data from project conventions."""
from pathlib import Path
import re,uuid,json
ROOT=Path(__file__).resolve().parents[3]
OUT=ROOT/'Assets/Art/Environment/Opening/StrataRock'
REF=ROOT/'Assets/Art/Environment/Opening/BrokenArmillary'
def guid(name):return uuid.uuid5(uuid.NAMESPACE_URL,'kaogong/strata-rock-hunyuan/20261108/'+name).hex
def write(path,text):path.write_text(text,encoding='utf8')
for role in ('BaseColor','Normal','Mask'):
 text=(REF/'Textures'/('BrokenArmillary_'+role+'.png.meta')).read_text(encoding='utf8')
 text=re.sub(r'(?m)^guid: [0-9a-f]+','guid: '+guid(role),text)
 write(OUT/'Textures'/('StrataRock_'+role+'.png.meta'),text)
text=(REF/'BrokenArmillary.mat').read_text(encoding='utf8').replace('BrokenArmillary','StrataRock_Baked')
for role in ('BaseColor','Normal','Mask'):
 old=re.search(r'(?m)^guid: ([0-9a-f]+)',(REF/'Textures'/('BrokenArmillary_'+role+'.png.meta')).read_text()).group(1)
 text=text.replace(old,guid(role))
write(OUT/'StrataRock_Baked.mat',text)
write(OUT/'StrataRock_Baked.mat.meta','fileFormatVersion: 2\nguid: '+guid('material')+'\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 2100000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
text=(REF/'BrokenArmillary.fbx.meta').read_text(encoding='utf8')
text=re.sub(r'(?m)^guid: [0-9a-f]+','guid: '+guid('fbx'),text)
old=re.search(r'(?m)^guid: ([0-9a-f]+)',(REF/'BrokenArmillary.mat.meta').read_text()).group(1)
text=text.replace(old,guid('material')).replace('name: BrokenArmillary','name: StrataRock_Baked').replace('preserveHierarchy: 0','preserveHierarchy: 1')
write(OUT/'StrataRock.fbx.meta',text)
write(OUT/'Textures.meta','fileFormatVersion: 2\nguid: '+guid('textures-folder')+'\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
print(json.dumps({'model_guid':guid('fbx'),'material_guid':guid('material')},indent=2))
