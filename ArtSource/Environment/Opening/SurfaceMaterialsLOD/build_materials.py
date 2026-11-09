"""Prepare generated material imagery for Unity; all outputs stay in the delegated folders.
Normal/AO/height are luminance-derived estimates, never labelled as high-poly bakes.
"""
from pathlib import Path
from datetime import datetime
import hashlib, json, re, uuid, math
import numpy as np
from PIL import Image, ImageDraw, ImageFont

ROOT=Path('D:/UnityProjects/NarutoVR')
SRC=ROOT/'ArtSource/Environment/Opening/SurfaceMaterialsLOD'
DST=ROOT/'Assets/Art/Environment/Opening/SurfaceMaterialsLOD'
LOG=ROOT/'Logs/exterior-world-lod-20261109/material-delegation'
NS=uuid.UUID('847895fa-2439-416e-a4db-921b2c7dc841')
SIZE=1024
def guid(p): return uuid.uuid5(NS,p.relative_to(ROOT).as_posix()).hex
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def png(p,a):
    assert any(p.resolve().is_relative_to(x.resolve()) for x in [SRC,DST,LOG])
    Image.fromarray(a).save(p)
def byte(x): return np.rint(np.clip(x,0,1)*255).astype('uint8')

def gaussian_filter(a,sigma,mode='wrap'):
    assert mode=='wrap' and a.ndim==2
    fy=np.fft.fftfreq(a.shape[0])[:,None];fx=np.fft.fftfreq(a.shape[1])[None,:]
    response=np.exp(-2*np.pi**2*sigma**2*(fx*fx+fy*fy))
    return np.fft.ifft2(np.fft.fft2(a)*response).real

def periodic(a):
    """Remove the smooth component responsible for opposite-edge discontinuities.
    A discrete Poisson solve changes low-frequency boundary bias, retaining stone details.
    No noise or synthetic stone pattern is added.
    """
    h,w,c=a.shape;v=np.zeros_like(a)
    v[0]=a[-1]-a[0];v[-1]=a[0]-a[-1]
    v[:,0]+=a[:,-1]-a[:,0];v[:,-1]+=a[:,0]-a[:,-1]
    ky=np.arange(h)[:,None];kx=np.arange(w)[None,:]
    denominator=2*np.cos(2*np.pi*ky/h)+2*np.cos(2*np.pi*kx/w)-4
    denominator[0,0]=1
    spectrum=np.fft.fft2(v,axes=(0,1))/denominator[...,None]
    spectrum[0,0]=0
    return np.clip(a-np.fft.ifft2(spectrum,axes=(0,1)).real,0,1)

def texture_meta(p,kind):
    s=(ROOT/'Assets/Art/Environment/Opening/StrataRock/Textures/StrataRock_BaseColor.png.meta').read_text(encoding='utf-8')
    values={'guid':guid(p),'sRGBTexture':1 if kind=='base' else 0,'textureType':1 if kind=='normal' else 0,
            'alphaUsage':1 if kind=='mask' else 0,'aniso':2,'filterMode':2,'compressionQuality':100,
            'nPOTScale':0,'streamingMipmaps':0,'userData':'EXTERIOR-WORLD-LOD-20261109-MATERIAL; '+kind}
    for key,value in values.items():s=re.sub(r'(?m)^(\s*'+key+r':).*$',lambda m:m[1]+' '+str(value),s)
    (p.with_suffix(p.suffix+'.meta')).write_text(s,encoding='utf-8')

def material(prefix,normal_scale):
    p=DST/(prefix+'.mat')
    s=(ROOT/'Assets/Art/Environment/Opening/StrataRock/StrataRock_Baked.mat').read_text(encoding='utf-8')
    s=s.replace('m_Name: StrataRock_Baked','m_Name: '+prefix)
    old={
        '25f289543ba45499bd6b9da378bb56d4':guid(DST/(prefix+'_BaseColor.png')),
        '312402e8f7e3536886a6ec0856a6c6ae':guid(DST/(prefix+'_Normal.png')),
        '2b14499de36155a6a0e50074ae3f9806':guid(DST/(prefix+'_Mask.png'))}
    for a,b in old.items():s=s.replace(a,b)
    s=s.replace('- _BumpScale: 1','- _BumpScale: '+str(normal_scale))
    s=s.replace('- _OcclusionStrength: 1','- _OcclusionStrength: 0.65')
    s=s.replace('- _Metallic: 1','- _Metallic: 0')
    p.write_text(s,encoding='utf-8')
    p.with_suffix('.mat.meta').write_text('fileFormatVersion: 2\nguid: '+guid(p)+'\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 2100000\n  userData: EXTERIOR-WORLD-LOD-20261109-MATERIAL\n  assetBundleName: \n  assetBundleVariant: \n',encoding='utf-8')
    return p

def seam(a):
    a=a.astype(float)
    return {'x_mean':float(abs(a[:,0]-a[:,-1]).mean()),'y_mean':float(abs(a[0]-a[-1]).mean()),
            'interior_x':float(abs(np.diff(a,axis=1)).mean()),'interior_y':float(abs(np.diff(a,axis=0)).mean())}

def preview(prefix,base,normal,mask):
    # Offline Lambert + subdued specular material swatch, not a Unity screenshot.
    font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',20)
    small=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',16)
    canvas=Image.new('RGB',(1536,1480),(27,30,32));d=ImageDraw.Draw(canvas)
    d.text((20,12),prefix+' | Base / tangent normal / packed mask / derived relief',font=font,fill=(225,230,232))
    norm=normal.astype(float)/255*2-1;norm/=np.maximum(np.linalg.norm(norm,axis=2,keepdims=True),1e-6)
    light=np.array([.4,.55,.73]);light/=np.linalg.norm(light)
    diffuse=np.maximum((norm*light).sum(axis=2),0)
    linear=(base.astype(float)/255)**2.2
    lit=np.clip(linear*(.32+.85*diffuse[...,None]),0,1)**(1/2.2)
    tiles=[base,normal,mask[:,:,:3],byte(lit)]
    for i,tile in enumerate(tiles):canvas.paste(Image.fromarray(tile).resize((384,384),Image.Resampling.LANCZOS),(384*i,48))
    tiled=np.tile(base,(2,2,1));canvas.paste(Image.fromarray(tiled).resize((1024,1024),Image.Resampling.LANCZOS),(0,456))
    d.text((1040,464),'2 x 2 repeat (left)',font=font,fill=(225,230,232))
    d.text((1040,502),'AO (G) and smoothness (A)',font=small,fill=(190,202,208))
    for channel,y,label in [(1,534,'AO'),(3,818,'Smoothness')]:
        gray=np.repeat(mask[:,:,channel,None],3,axis=2)
        canvas.paste(Image.fromarray(gray).resize((256,256),Image.Resampling.LANCZOS),(1040,y))
        d.text((1310,y+8),label,font=font,fill=(205,215,220))
    canvas.save(LOG/(prefix+'_Review.png'))
    Image.fromarray(tiled).resize((1536,1536),Image.Resampling.LANCZOS).save(LOG/(prefix+'_Tiling2x2.png'))

def astc_bytes(size=1024):
    total=0
    while True:
        total+=math.ceil(size/6)**2*16
        if size==1:return total
        size=max(1,size//2)

entries=[]
for prefix,coverage,amplitude,normal_scale in [
    ('M01_QingHuiStrata',2.0,.016,.75),('M02_MossScree',1.5,.012,.65)]:
    raw=SRC/(prefix+'_Generated.png')
    original=np.array(Image.open(raw).convert('RGB').resize((SIZE,SIZE),Image.Resampling.LANCZOS))/255
    albedo=periodic(original)
    base=byte(albedo)
    # Estimate shallow relief from local luminance while suppressing broad pigment changes.
    lum=albedo@np.array([.2126,.7152,.0722])
    local=lum-gaussian_filter(lum,24,mode='wrap')
    height=gaussian_filter(local,1.15,mode='wrap')
    qlo,qhi=np.percentile(height,[1,99]);height=np.clip((height-qlo)/(qhi-qlo),0,1)
    height=gaussian_filter(height,1.1,mode='wrap')
    dx=(np.roll(height,-1,axis=1)-np.roll(height,1,axis=1))*(amplitude*SIZE/(2*coverage))
    dy=(np.roll(height,-1,axis=0)-np.roll(height,1,axis=0))*(amplitude*SIZE/(2*coverage))
    n=np.stack([-dx,dy,np.ones_like(dx)],axis=2);n/=np.linalg.norm(n,axis=2,keepdims=True)
    normal=byte(n*.5+.5)
    cavity=np.maximum(gaussian_filter(height,6,mode='wrap')-height,0)
    ao=np.clip(1-cavity*.65,.78,1)
    micro=np.abs(lum-gaussian_filter(lum,2,mode='wrap'))
    smooth=np.clip(.22-micro*.7,.13,.25)
    if prefix.startswith('M02'):
        moss=np.clip((albedo[:,:,1]-albedo[:,:,2]-.008)*9,0,1)
        smooth=np.clip(smooth-moss*.065,.10,.24)
    mask=np.stack([np.zeros_like(ao),ao,np.zeros_like(ao),smooth],axis=2)
    packed=byte(mask)
    for suffix,array,kind in [('BaseColor',base,'base'),('Normal',normal,'normal'),('Mask',packed,'mask')]:
        p=DST/(prefix+'_'+suffix+'.png');png(p,array);texture_meta(p,kind)
    png(SRC/(prefix+'_DerivedHeight16.png'),np.rint(height*65535).astype('uint16'))
    mat=material(prefix,normal_scale);preview(prefix,base,normal,packed)
    entries.append({'id':prefix[:3],'name':prefix,'material':mat.relative_to(ROOT).as_posix(),'material_guid':guid(mat),
        'texture_size':[1024,1024],'tile_coverage_metres':coverage,'normal_scale':normal_scale,'occlusion_strength':.65,
        'source':raw.relative_to(ROOT).as_posix(),'source_sha256':sha(raw),'generation_platform':'OpenAI built-in image_gen',
        'maps_provenance':'basecolor generated; shallow normal, cavity AO and smoothness estimated offline from basecolor; not photogrammetry or high-poly bake',
        'albedo_mean_rgb':base.mean(axis=(0,1)).tolist(),'basecolor_seam':seam(base),'normal_seam':seam(normal),'mask_seam':seam(packed),
        'metallic_max':int(packed[:,:,0].max()),'blue_max':int(packed[:,:,2].max()),
        'ao_range':[float(ao.min()),float(ao.max())],'smoothness_range':[float(smooth.min()),float(smooth.max())],
        'normal_z_min':float(n[:,:,2].min()),'normal_z_mean':float(n[:,:,2].mean()),
        'estimated_astc6x6_bytes_with_mips':astc_bytes()*3})

manifest={'task':'EXTERIOR-WORLD-LOD-20261109-MATERIAL','generated_at':datetime.now().astimezone().isoformat(),
    'parent_acceptance':'pending; Unity not launched by material worker','materials':entries,
    'shader':'Universal Render Pipeline/Lit','shader_guid':'933532a4fcc9baf4fa0491de14d08ed7',
    'maps':{'BaseColor':'RGB sRGB','Normal':'RGB tangent +Y, Unity NormalMap importer, linear','Mask':'RGBA linear: R=metallic 0, G=AO, B=0, A=smoothness'},
    'import':{'android':'ASTC_6x6 (50), overridden=true, maxSize=1024, quality=100','mipmaps':True,'wrap':'Repeat','filter':'Trilinear','aniso':2,'readable':False,'streamingMipmaps':False},
    'runtime_files':[{'path':p.relative_to(ROOT).as_posix(),'sha256':sha(p),'bytes':p.stat().st_size} for p in sorted(DST.iterdir()) if p.is_file()],
    'source_terms':{'third_party_textures_reused':False,'ownership_reference':'https://openai.com/policies/terms-of-use/','verified':'2026-11-09','terms_effective':'2026-01-01','note':'Generated in this task from text only. No third-party image input, no Asset Store texture copied, no CC0 claim. Output ownership between account user and OpenAI is addressed by the applicable OpenAI terms; output uniqueness is not guaranteed.'},
    'limitations':['normal/AO are image-derived estimates, not measured geometry','offline preview only; no Unity import, ASTC visual inspection or headset performance validation performed by this worker','parent must check close/distant views and choose material coverage/tint on scene mesh','folder-level SurfaceMaterialsLOD.meta is outside worker write scope and left to parent/Unity']}
(LOG/'material-manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'materials':entries,'runtime_files':len(manifest['runtime_files'])},ensure_ascii=True,indent=2))
