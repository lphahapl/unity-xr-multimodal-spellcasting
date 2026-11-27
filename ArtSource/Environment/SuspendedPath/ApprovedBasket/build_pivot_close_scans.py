"""Build H03 metric microdetail derived from measured 1.35 m metal and 2 m quarry scans."""
from pathlib import Path
from concurrent.futures import ThreadPoolExecutor
import hashlib
import json
import sys
import numpy as np
import requests
from PIL import Image, ImageFilter

project, cache = map(Path, sys.argv[1:])
out = project/'Assets/Art/Environment/SuspendedPath/ApprovedBasket'
art = project/'ArtSource/Environment/SuspendedPath/ApprovedBasket'
cache.mkdir(parents=True,exist_ok=True)
source = 'rusty_metal_03'
headers={'User-Agent':'NarutoVR game art material processing'}
def api(endpoint):
    response=requests.get('https://api.polyhaven.com/'+endpoint,headers=headers,timeout=25)
    response.raise_for_status()
    return response.json()
info,files=api('info/'+source),api('files/'+source)
def get(key):
    entry=files[key]['1k']['jpg']
    path=cache/(key+'.jpg')
    if not path.exists() or hashlib.md5(path.read_bytes()).hexdigest()!=entry['md5']:
        r=requests.get(entry['url'],headers=headers,timeout=35);r.raise_for_status()
        if hashlib.md5(r.content).hexdigest()!=entry['md5']:raise ValueError(key+' checksum failed')
        path.write_bytes(r.content)
    return key,{f:entry[f] for f in ('url','md5')}
with ThreadPoolExecutor(max_workers=3) as workers:
    refs=dict(workers.map(get,('Diffuse','nor_gl','Rough')))
def pack(name,normal,color,roughness):
    size=(1024,1024)
    lum=color.convert('L').resize(size,Image.Resampling.LANCZOS)
    low=np.asarray(lum.filter(ImageFilter.GaussianBlur(27)),dtype=float)
    mono=np.asarray(lum,dtype=float)
    # Neutral grey gives zero modulation. Retain pit walls, scratches and mineral grain but discard broad paint colour.
    signal=(mono-low)*1.16+(low-np.median(mono))*.26
    scale=max(8.,np.std(signal))
    value=np.clip(128+signal*44/scale,15,239).astype('uint8')
    nx,ny,_=normal.convert('RGB').resize(size,Image.Resampling.LANCZOS).split()
    texture=Image.merge('RGBA',(nx,ny,Image.fromarray(value),roughness.resize(size,Image.Resampling.LANCZOS)))
    texture.save(out/(name+'.png'),optimize=True)
pack('CastSurfaceDetail',Image.open(cache/'nor_gl.jpg'),Image.open(cache/'Diffuse.jpg'),Image.open(cache/'Rough.jpg').convert('L'))
workshop=project/'Assets/Art/Environment/SuspendedPath/Surfaces/Workshop'
ashlar=Image.open(workshop/'Ashlar_BaseSmooth.png').convert('RGBA')
pack('CutStoneDetail',Image.open(workshop/'Ashlar_NormalGL.png'),ashlar,Image.fromarray(255-np.asarray(ashlar.getchannel('A'))))
metadata={'metal_source':'https://polyhaven.com/a/'+source,'metal_authors':info['authors'],'metal_scan_extent_m':[round(x/1000,2) for x in info['dimensions']],'metal_inputs':refs,'license':'CC0-1.0','license_url':'https://polyhaven.com/license','stone_source':'https://polyhaven.com/a/quarry_wall','stone_inputs':'../Surfaces/Workshop/sources.json','packing':'linear RG=tangent normal XY; B=signed luminance modulation centered at 0.5; A=roughness','use':'Casting pits and roughness use a scan of oxidized metal; actual bronze coloration remains authored. Course stone retains the quarry scan.'}
(art/'PivotCloseScans.json').write_text(json.dumps(metadata,indent=2),encoding='utf8')
