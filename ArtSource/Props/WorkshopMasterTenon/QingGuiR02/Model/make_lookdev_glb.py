"""Add standard glTF optical preview extensions, leaving ordinary game GLB unchanged.
No alpha blending. Body transmission is gated by JadeMask; GoldWire is untouched.
This previews glossy attenuated transport, not Cycles SSS or Unity's custom shader.
"""
from pathlib import Path
import struct,json,hashlib
P=Path(__file__).resolve().parent;src=P/'QingGuiR02.glb';raw=src.read_bytes();pos=12;chunks={}
while pos<len(raw):
    length,kind=struct.unpack_from('<II',raw,pos);chunks[kind]=raw[pos+8:pos+8+length];pos+=8+length
j=json.loads(chunks[0x4E4F534A]);data=bytearray(chunks[0x004E4942])
def texture(file):
    while len(data)%4:data.append(0)
    pic=file.read_bytes();off=len(data);data.extend(pic);vi=len(j.setdefault('bufferViews',[]));j['bufferViews'].append({'buffer':0,'byteOffset':off,'byteLength':len(pic)})
    ii=len(j.setdefault('images',[]));j['images'].append({'name':file.stem,'bufferView':vi,'mimeType':'image/png'})
    ti=len(j.setdefault('textures',[]));j['textures'].append({'source':ii});return {'index':ti,'texCoord':0}
mask=texture(P/'Textures/QingGui_JadeMask.png');thick=texture(P/'Textures/QingGui_Thickness.png')
body=next(m for m in j['materials'] if m['name']=='QingGuiR02_Body');ex=body.setdefault('extensions',{})
ex['KHR_materials_transmission']={'transmissionFactor':.12,'transmissionTexture':mask}
ex['KHR_materials_volume']={'thicknessFactor':.12,'thicknessTexture':thick,'attenuationDistance':.045,'attenuationColor':[.82,.79,.71]}
ex['KHR_materials_ior']={'ior':1.53}
ex['KHR_materials_clearcoat']={'clearcoatFactor':.18,'clearcoatRoughnessFactor':.09,'clearcoatTexture':mask}
body['extras']={'preview_only':True,'meaning':'JadeMask-gated smooth translucent polish; no alpha; standard glTF attenuation only, no volume scattering'}
for e in ex:
    if e not in j.setdefault('extensionsUsed',[]):j['extensionsUsed'].append(e)
j['buffers'][0]['byteLength']=len(data);jb=json.dumps(j,separators=(',',':')).encode()
jb+=b' '*((-len(jb))%4);data+=b'\0'*((-len(data))%4);out=struct.pack('<III',0x46546C67,2,12+8+len(jb)+8+len(data))+struct.pack('<II',len(jb),0x4E4F534A)+jb+struct.pack('<II',len(data),0x004E4942)+data
target=P/'QingGuiR02_Lookdev.glb';target.write_bytes(out)
report={'source_game_glb_sha256':hashlib.sha256(raw).hexdigest(),'lookdev_sha256':hashlib.sha256(out).hexdigest(),'body_extensions':ex,'gold_material_has_transmission':any('KHR_materials_transmission' in m.get('extensions',{}) for m in j['materials'] if 'GoldWire' in m['name']),'alpha_mode':body.get('alphaMode','OPAQUE'),'limitations':['Viewer must implement the listed KHR extensions.','Standard transmission/volume is not subsurface scattering.','No claim this matches the separate Unity shader or Quest output.']}
assert not report['gold_material_has_transmission'];(P/'lookdev-glb-report.json').write_text(json.dumps(report,indent=2),'utf-8');print(json.dumps(report,indent=2))
