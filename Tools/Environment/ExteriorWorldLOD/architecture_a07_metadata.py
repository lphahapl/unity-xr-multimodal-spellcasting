from pathlib import Path
import re,uuid,json
ROOT=Path(__file__).resolve().parents[3];OUT=ROOT/'Assets/Art/Environment/Opening/ArchitectureLOD/A07Workbench';REF=ROOT/'Assets/Art/Environment/Opening/BrokenArmillary'
def guid(name):return uuid.uuid5(uuid.NAMESPACE_URL,'kaogong/A07Workbench/20261109/'+name).hex
def write(p,t):p.write_text(t,encoding='utf8')
mat=(REF/'BrokenArmillary.mat').read_text(encoding='utf8').replace('BrokenArmillary','SurveyWorkbench_Baked')
for role in ('BaseColor','Normal','Mask'):
 template=(REF/'Textures'/('BrokenArmillary_'+role+'.png.meta')).read_text(encoding='utf8');old=re.search(r'(?m)^guid: ([0-9a-f]+)',template).group(1);mat=mat.replace(old,guid(role));write(OUT/'Textures'/('SurveyWorkbench_'+role+'.png.meta'),template.replace(old,guid(role)))
write(OUT/'SurveyWorkbench_Baked.mat',mat);write(OUT/'SurveyWorkbench_Baked.mat.meta','fileFormatVersion: 2\nguid: '+guid('material')+'\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 2100000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
source_mat_guid=re.search(r'(?m)^guid: ([0-9a-f]+)',(REF/'BrokenArmillary.mat.meta').read_text()).group(1)
for i in range(3):
 template=(REF/'BrokenArmillary.fbx.meta').read_text(encoding='utf8');template=re.sub(r'(?m)^guid: [0-9a-f]+','guid: '+guid('LOD'+str(i)),template).replace(source_mat_guid,guid('material')).replace('name: BrokenArmillary','name: SurveyWorkbench_Baked')
 write(OUT/('SurveyWorkbench_LOD'+str(i)+'.fbx.meta'),template)
write(OUT/'Textures.meta','fileFormatVersion: 2\nguid: '+guid('textures-folder')+'\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
print(json.dumps({'material':guid('material'),'models':[guid('LOD'+str(i)) for i in range(3)]}))
