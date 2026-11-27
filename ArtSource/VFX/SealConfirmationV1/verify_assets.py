"""Static asset integrity gate. Runtime playback evidence is collected separately in Unity."""
from pathlib import Path
import hashlib, json, re, wave
import numpy as np
from PIL import Image

ROOT=Path(__file__).resolve().parents[3]
DEST=ROOT/'Assets/Art/VFX/Skills/SealConfirmation'
OUT=ROOT/'Logs/t01-seal-feedback-20261124'
NAMES={'Gui':'规','Ju':'矩','Zhun':'准','Sheng':'绳','Heng':'衡'}
checks=[]
def require(label,condition,detail=None):
    checks.append({'check':label,'pass':bool(condition),'detail':detail})
def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest()
def read(path): return path.read_bytes().decode('utf-8-sig')
def guid(path): return re.search(r'^guid: (\w+)',read(path),re.M).group(1)
baseline=json.loads((OUT/'before.json').read_text(encoding='utf-8'))
for file,digest in baseline.items():
    if file.startswith('Assets/Art/VFX/Skills/SealConfirmation/') or file.endswith('律印.asset') or file.endswith('.unity'):
        require('preserved '+file,sha(ROOT/file)==digest)
all_guids=[]; manifests={}
for key,char in NAMES.items():
    profile=ROOT/f'Assets/ScriptableObjects/手印识别配置/{char}印.asset'
    actual=read(profile); before=read(OUT/f'{char}印.before.asset')
    strip=lambda x: re.sub(r'^  confirmationPrefab:.*\n?','',x,flags=re.M).replace('\r\n','\n')
    require(key+' recognition fields unchanged',strip(actual)==strip(before))
    prefab=read(DEST/f'{key}SealFlash.prefab')
    pg,sg,ag=[guid(DEST/(key+suffix+'.meta')) for suffix in ['SealFlash.prefab','Glyph.png','Chime.wav']]
    all_guids += [pg,sg,ag]
    require(key+' correct profile->prefab->sprite/audio refs',f'guid: {pg}' in actual and f'guid: {sg}' in prefab and f'guid: {ag}' in prefab)
    require(key+' inherited single-shot and lifetime',all(x in prefab for x in ['m_PlayOnAwake: 1','Loop: 0','duration: 0.8','m_Volume: 0.62','guid: 08b6ce02fedfe374692451ad1a5583e2']))
    mat=DEST/('HengSealGlyph.mat' if key=='Heng' else 'SealGlyph.mat')
    require(key+' expected depth material',guid(Path(str(mat)+'.meta')) in prefab and ('_HandDepthBias: '+('0.12' if key=='Heng' else '0.08')) in read(mat))
    with wave.open(str(DEST/f'{key}Chime.wav'),'rb') as wav:
        props=(wav.getframerate(),wav.getnchannels(),wav.getsampwidth())
        samples=np.frombuffer(wav.readframes(wav.getnframes()),dtype='<i2')
        seconds=len(samples)/wav.getframerate()
    require(key+' audio completes before root expires',props==(48000,1,2) and seconds<=.75,{'format':props,'duration':seconds})
    require(key+' no clipped/abrupt endpoints',np.max(np.abs(samples.astype(int)))<32767 and samples[0]==0 and samples[-1]==0)
    im=Image.open(DEST/f'{key}Glyph.png'); a=np.array(im)[:,:,3]
    require(key+' transparent correctly sized texture',im.mode=='RGBA' and im.size==(480,576) and a.min()==0 and a.max()>240)
    edge=np.concatenate([a[0],a[-1],a[:,0],a[:,-1]])
    require(key+' no clipped visible stroke',edge.max()<=1,{'max_border_alpha_255':int(edge.max())})
    meta=read(DEST/f'{key}Glyph.png.meta')
    require(key+' sprite importer matches sample',all(x in meta for x in ['spritePixelsToUnits: 576','spriteMode: 1','alphaIsTransparency: 1','enableMipMap: 1']))
    manifests[key]={p.name:sha(p) for p in DEST.glob(key+'*') if p.is_file()}
require('15 new distinct GUIDs',len(set(all_guids))==15)
require('Heng material independent GUID',guid(DEST/'HengSealGlyph.mat.meta') not in all_guids+[guid(DEST/'SealGlyph.mat.meta')])
for key in ['Zhun','Sheng']:
    p=read(ROOT/f'Assets/ScriptableObjects/手印识别配置/{NAMES[key]}印.asset')
    require(key+' remains uncalibrated',bool(re.search(r'^  calibrated: 0$',p,re.M)))
result={'scope':'Static content validation; not recognition or playback evidence','checks':checks,'passed':sum(x['pass'] for x in checks),'total':len(checks),'assets':manifests}
(OUT/'asset-checks.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
print(f"{result['passed']}/{result['total']} static content checks passed")
for c in checks:
    if not c['pass']: print('FAIL',c)
raise SystemExit(0 if all(c['pass'] for c in checks) else 1)
