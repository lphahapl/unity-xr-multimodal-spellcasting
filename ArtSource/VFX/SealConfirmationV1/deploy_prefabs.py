"""Deploy only the five new seal feedback assets and confirmationPrefab fields.

Existing GUIDs are retained on subsequent runs. Calibration, thresholds and rules
are byte-for-byte preserved. The Lv prefab/texture/audio/material are read-only.
"""
from pathlib import Path
import re
import uuid
import json
import hashlib

HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[2]
DEST=ROOT/'Assets/Art/VFX/Skills/SealConfirmation'
PROOF=ROOT/'Logs/t01-seal-feedback-20261124'
NAMES={'Gui':'规','Ju':'矩','Zhun':'准','Sheng':'绳','Heng':'衡'}

def read(p): return p.read_bytes().decode('utf-8-sig')
def guid(text): return re.search(r'^guid: ([0-9a-f]+)',text,re.M).group(1)
def asset_meta(name,reference):
    target=DEST/(name+'.meta')
    content=read(DEST/(reference+'.meta'))
    g=guid(read(target)) if target.exists() else uuid.uuid4().hex
    content=re.sub(r'^guid: [0-9a-f]+',f'guid: {g}',content,flags=re.M)
    if 'spriteID:' in content:
        old=read(target) if target.exists() else ''
        m=re.search(r'spriteID: ([0-9a-f]+)',old)
        content=re.sub(r'spriteID: [0-9a-f]+','spriteID: '+(m.group(1) if m else uuid.uuid4().hex),content)
    target.write_bytes(content.encode('utf-8'))
    return g

def main():
    baseline=read(DEST/'LvSealFlash.prefab')
    old_sprite=guid(read(DEST/'LvGlyph.png.meta'))
    old_audio=guid(read(DEST/'LvChime.wav.meta'))
    # The real wrist-case prefab occluded the upper character with the shared .08m
    # bias. Keep the left-wrist anchor and isolate the .12m bias to Heng only.
    old_material=guid(read(DEST/'SealGlyph.mat.meta'))
    heng_material=asset_meta('HengSealGlyph.mat','SealGlyph.mat')
    (DEST/'HengSealGlyph.mat').write_bytes(read(DEST/'SealGlyph.mat').replace('m_Name: SealGlyph','m_Name: HengSealGlyph').replace('_HandDepthBias: 0.08','_HandDepthBias: 0.12').encode('utf-8'))
    manifest={}
    for key,char in NAMES.items():
        sprite=asset_meta(key+'Glyph.png','LvGlyph.png')
        audio=asset_meta(key+'Chime.wav','LvChime.wav')
        prefab=asset_meta(key+'SealFlash.prefab','LvSealFlash.prefab')
        body=baseline.replace('m_Name: LvSealFlash','m_Name: '+key+'SealFlash').replace(old_sprite,sprite).replace(old_audio,audio)
        if key=='Heng': body=body.replace(old_material,heng_material)
        (DEST/(key+'SealFlash.prefab')).write_bytes(body.encode('utf-8'))
        profile=ROOT/f'Assets/ScriptableObjects/手印识别配置/{char}印.asset'
        before=read(profile)
        line=f'  confirmationPrefab: {{fileID: 479509920300367230, guid: {prefab}, type: 3}}'
        if '  confirmationPrefab:' in before:
            after=re.sub(r'^  confirmationPrefab:.*$',line,before,flags=re.M)
        else:
            after=re.sub(r'(^  calibrated:)',line+'\n'+r'\1',before,count=1,flags=re.M)
        # The sole allowed profile mutation is this single serialized reference.
        strip=lambda x: re.sub(r'^  confirmationPrefab:.*\n?','',x,flags=re.M).replace('\r\n','\n')
        assert strip(before)==strip(after), f'Unexpected profile mutation: {profile}'
        profile.write_bytes(after.encode('utf-8'))
        manifest[key]={'character':char,'spriteGuid':sprite,'audioGuid':audio,'prefabGuid':prefab,'materialGuid':heng_material if key=='Heng' else old_material,'depthBiasMetres':.12 if key=='Heng' else .08,'calibrated':int(re.search(r'^  calibrated: (\d+)',after,re.M).group(1)),'handMode':1 if char=='衡' else 0}
    (HERE/'asset-manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
    print('Bound five new prefabs; preserved Lv and all calibration/rule fields.')

if __name__=='__main__': main()
