"""Prepare Blender exports and generate the soft-edged valley cloud texture."""
from pathlib import Path
import shutil,re,uuid,sys,json
import numpy as np
from PIL import Image
def unique(name):
    return uuid.uuid5(uuid.NAMESPACE_DNS,'NarutoVR.CourtyardInfill.'+name).hex
def meta(path):
    return Path(str(path)+'.meta')
def guid(path):
    return re.search(r'^guid: (\w+)',meta(path).read_text(),re.M)[1]
def retarget(old,new):
    return meta(old).read_text('utf8').replace(guid(old),unique(new.name))
here=Path(__file__).resolve().parent;project=here.parents[3]
dest=Path(sys.argv[1])/'Assets/Art/Environment/Opening/CourtyardInfill'
stage=Path(sys.argv[2])
template=project/'Assets/Art/Environment/Opening/Interior'
dest.mkdir(exist_ok=True,parents=True);meta(dest).write_text(retarget(template,dest),'utf8')
for key in ('B01','B02','B03','B04','B05'):
    group=dest/key;group.mkdir(exist_ok=True)
    size=2048 if key in ('B01','B02') else 1024
    meta(group).write_text(retarget(template,group),'utf8')
    mt=template/'GaugeWorkshop_Baked.mat';mtext=mt.read_text('utf8')
    mtext=mtext.replace('GaugeWorkshop_Baked',key+'_Infill').replace('  - _EMISSION\n','')
    mtext=re.sub(r'(- _EmissionMap:\s+m_Texture: )\{[^}]+\}',r'\g<1>{fileID: 0}',mtext)
    mtext=mtext.replace('{r: 0.3, g: 0.3, b: 0.3, a: 1}','{r: 0, g: 0, b: 0, a: 1}')
    for kind in ('BaseColor','Normal','Mask'):
        old=template/'Textures'/('GaugeWorkshop_'+kind+'.png');new=group/(key+'_'+kind+'.png')
        shutil.copyfile(stage/key/new.name,new)
        meta(new).write_text(retarget(old,new).replace('maxTextureSize: 1024','maxTextureSize: '+str(size)),'utf8')
        mtext=mtext.replace(guid(old),unique(new.name))
    new=group/(key+'_Infill.mat')
    new.write_text(mtext,'utf8');meta(new).write_text(retarget(mt,new),'utf8')
    for f in (stage/key).glob('*_LOD?.fbx'):
        new=group/f.name;shutil.copyfile(f,new)
        s=retarget(template/'WorkshopWallLamp.fbx',new).replace('GaugeWorkshop_Baked',key+'_Infill').replace(guid(mt),unique(key+'_Infill.mat'))
        meta(new).write_text(s,'utf8')
cloud=dest/'C01';cloud.mkdir(exist_ok=True);meta(cloud).write_text(retarget(template,cloud),'utf8')
rng=np.random.default_rng(29211);size=512
def field(n):
    small=Image.fromarray(rng.integers(0,256,(n,n),dtype=np.uint8))
    return np.array(small.resize((size,size),Image.Resampling.BICUBIC))/255.
y,x=np.mgrid[-1:1:complex(size),-1:1:complex(size)]
noise=sum(field(n)*w for n,w in [(7,.47),(14,.25),(32,.16),(70,.09),(130,.03)])
falloff=np.maximum(0,1-(x*x+y*y)**.70)
a=np.clip((falloff-.025)*2.3,0,1)**2 * np.clip((noise-.10)*1.6,0,.86)
light=np.clip(.66+.47*noise+3*(np.roll(noise,-2,axis=0)-np.roll(noise,2,axis=0)),.48,.98)
rgb=np.stack([light*.95,light*.981,light],axis=2)
pixels=np.dstack((rgb,a))
out=cloud/'ValleyCloud.png';Image.fromarray(np.uint8(pixels*255)).save(out)
s=retarget(template/'Textures/GaugeWorkshop_BaseColor.png',out)
s=s.replace('maxTextureSize: 1024','maxTextureSize: 512').replace('alphaIsTransparency: 0','alphaIsTransparency: 1')
for prop in ('wrapU','wrapV','wrapW'):s=s.replace(prop+': 0',prop+': 1')
meta(out).write_text(s,'utf8');shutil.copy2(here/'ValleyCloud.shader',cloud/'ValleyCloud.shader')
(here/'C01-alpha.json').write_text(json.dumps({'size':size,'alpha_max':float(a.max()),'corner_alpha':float(max(a[0,0],a[-1,-1])),'border_alpha':float(max(a[0].max(),a[-1].max(),a[:,0].max(),a[:,-1].max())),'texture_samples_per_fragment':1},indent=2),'utf8')
