"""Rebuild chapter-two A1 paving and survey inlay colors. Requires Pillow, NumPy and requests."""
import json,requests,pathlib,hashlib,concurrent.futures,tempfile
from PIL import Image
import numpy as np
r=pathlib.Path(__file__).resolve().parents[4]; a=r/'Assets/Art/Environment/SuspendedPath/ApprovedWorkshop'; s=r/'ArtSource/Environment/SuspendedPath/ApprovedWorkshop'
w=pathlib.Path(tempfile.gettempdir())/'CH2WorkshopPhotoSources';w.mkdir(parents=True,exist_ok=True); key='red_sandstone_pavement'; j=requests.get('https://api.polyhaven.com/files/'+key,timeout=25).json(); channels=('Diffuse','nor_gl','Rough')
def load(c):
    info=j[c]['2k']['jpg'];p=w/(c+'.jpg')
    for i in range(4):
        try:
            if not p.exists() or hashlib.md5(p.read_bytes()).hexdigest()!=info['md5']:
                resp=requests.get(info['url'],timeout=20);resp.raise_for_status()
                if hashlib.md5(resp.content).hexdigest()!=info['md5']:raise Exception('MD5 mismatch')
                p.write_bytes(resp.content)
            return np.asarray(Image.open(p).convert('RGB'))
        except Exception:
            if i==3:raise
data=list(concurrent.futures.ThreadPoolExecutor(3).map(load,channels));rgb=data[0].astype(float);l=rgb@np.array([.2126,.7152,.0722]);out=l[:,:,None]*.59+rgb*.41
out=np.clip(out*np.array([1.09,1.085,1.06])+1.5,0,255).astype('uint8');Image.fromarray(np.dstack([out,255-data[2][:,:,0]])).save(a/'Flagstones_BaseSmooth.png');Image.fromarray(data[1]).save(a/'Flagstones_NormalGL.png')
try:meta=requests.get('https://api.polyhaven.com/info/'+key,timeout=15).json()
except:meta={}
record=dict(source='https://polyhaven.com/a/'+key,license='CC0-1.0',license_url='https://polyhaven.com/license',authors=meta.get('authors'),dimensions_mm=meta.get('dimensions'),channels={c:j[c]['2k']['jpg'] for c in channels},processing='Diffuse lightly desaturated for a weathered cool plateau, alpha=1-roughness, GL normal unchanged. Source channel JPG hashes verified before packing.',texel_tile_m=2.15)
(s/'Flagstones.source.json').write_text(json.dumps(record,indent=2,ensure_ascii=False),encoding='utf8')
print(meta.get('dimensions'),[hashlib.sha256(x.read_bytes()).hexdigest() for x in a.glob('*.png')])

from PIL import Image
from pathlib import Path
import numpy as np
r=Path(__file__).resolve().parents[4];a=r/'Assets/Art/Environment/SuspendedPath/ApprovedWorkshop';old=r/'Assets/Art/Environment/Opening/PreqinGateFittings/GateBronze_BaseColor.png'
src=Image.open(old).convert('RGB');src=src.crop((80,20,80+int(src.width*.24),20+int(src.height*.94))).resize((256,1024),Image.Resampling.LANCZOS)
rgb=np.array(src).astype(float);yy,xx=np.mgrid[0:1024,0:256];x=(xx/255-.04)/.92*2-1;y=(yy/1023-.015)/.97*2-1
inlay=(abs(x)<.55)&(abs(y)<.866);ceramic=np.array([57,102,101])[None,None,:]+(rgb.mean(axis=2)[:,:,None]-66)*.37
rgb[inlay]=ceramic[inlay]; edge=(abs(np.abs(x)-.58)<.043)&(abs(y)<.884);rgb[edge]=np.array([137,108,64])*.78+rgb[edge]*.22
smooth=np.where(inlay,142,109)+np.clip((rgb.mean(axis=2)-90)*.32,-35,35)
Image.fromarray(np.dstack((np.clip(rgb,0,255).astype('uint8'),smooth.astype('uint8')))).save(a/'SurveyInlay_BaseSmooth.png')
emit=np.zeros_like(rgb,'uint8');emit[inlay]=[36,103,104];Image.fromarray(emit).save(a/'SurveyInlay_Emission.png')

# A soft cluster of overlapping wisps. Only one chimney uses the mask, at 3.1 particles per second.
from PIL import ImageFilter
rng = np.random.default_rng(83)
yy, xx = np.mgrid[-1:1:128j, -1:1:128j]
opacity = np.zeros_like(xx)
for _ in range(16):
    cx, cy = rng.uniform(-.41, .41, size=2)
    sx, sy = rng.uniform(.18, .36, size=2)
    opacity += rng.uniform(.35, .8) * np.exp(-((xx-cx)/sx)**2 - ((yy-cy)/sy)**2)
opacity *= np.clip((1-np.maximum(abs(xx),abs(yy)))*5, 0, 1)
smoke = Image.new('RGBA', (128,128), (255,255,255,0))
smoke.putalpha(Image.fromarray(np.uint8(255*np.clip(opacity/opacity.max(),0,1))).filter(ImageFilter.GaussianBlur(1)))
smoke.save(a/'ForgeSmoke.png')
