"""Create a periodic 0.48 m hammered bronze surface and a pale quarried stone inlay."""
import cv2,numpy as np,re
from PIL import Image
from paths import *

def make():
    meta(ART,PHOTO)
    n=512
    rng=np.random.default_rng(190127)
    yy,xx=np.mgrid[:n,:n]
    def noise(size):
        small=rng.normal(0,1,(size,size)).astype('f')
        tile=cv2.resize(np.tile(small,(3,3)),(n*3,n*3),interpolation=cv2.INTER_CUBIC)
        return tile[n:n*2,n:n*2]
    coarse=noise(8);middle=noise(31);grain=rng.normal(0,1,(n,n))
    height=np.zeros_like(xx,dtype='f')
    # Periodic overlapping blows, radius 6–17 mm. Slight ellipticity records the hammer angle.
    for _ in range(850):
        cx,cy=rng.uniform(0,n,2);rx=rng.uniform(7,18);ry=rx*rng.uniform(.64,1.2)
        dx=(xx-cx+n/2)%n-n/2;dy=(yy-cy+n/2)%n-n/2
        dist=(dx/rx)**2+(dy/ry)**2
        height-=np.maximum(0,1-dist)**2*rng.uniform(.15,.43)
    height+=grain*.035+middle*.08
    wear=np.clip((coarse*.75+middle*.37-.49)*.63,0,1)
    tint=np.array([134,96,56])+middle[:,:,None]*[4,4,3]+grain[:,:,None]*[1.7,1.6,1.3]+(height-height.mean())[:,:,None]*[10,8,6]
    verdigris=np.array([64,83,68])+middle[:,:,None]*3.6
    rgb=tint*(1-wear[:,:,None])+verdigris*wear[:,:,None]
    gx=(np.roll(height,1,1)-np.roll(height,-1,1))*1.75
    gy=(np.roll(height,-1,0)-np.roll(height,1,0))*1.75
    norm=np.stack((gx,gy,np.ones_like(gx)),axis=2);norm/=np.linalg.norm(norm,axis=2,keepdims=True)
    metal=220-128*wear+middle*4
    smooth=145-79*wear+middle*7+grain*2
    textures={'BaseColor':rgb,'Normal':(norm*.5+.5)*255,'MetalSmooth':np.stack((metal,np.full_like(metal,255),np.zeros_like(metal),smooth),2)}
    bronze_template=ART.parent/'ConcentricGauge/Bronze'
    material=(bronze_template/'CG_A1_Bronze.mat').read_text().replace('CG_A1_Bronze','GateHammeredBronze').replace('  - _EMISSION\n','')
    for role,pixels in textures.items():
        path=ART/('GateBronze_'+role+'.png')
        Image.fromarray(np.rint(np.clip(pixels,0,255)).astype('uint8')).save(path)
        template=bronze_template/('CG_A1_'+role+'.png')
        g=meta(path,template);material=material.replace(guid(template),g)
        f=Path(str(path)+'.meta');s=f.read_text()
        s=re.sub(r'(maxTextureSize: )\d+',r'\g<1>512',s)
        for axis in 'UV':s=re.sub(r'(wrap'+axis+': )\d+',r'\g<1>0',s)
        f.write_text(s)
    path=ART/'GateHammeredBronze.mat';path.write_text(material.replace('_BumpScale: 1','_BumpScale: 0.75'));meta(path,bronze_template/'CG_A1_Bronze.mat')
    # Use the same mineral detail and normal map as PhotoArchitecture/Stone with a pale limestone body.
    stone=np.asarray(Image.open(PHOTO/'Stone_Color.jpg').convert('RGB'),dtype='f')
    stone=np.array([201,195,173])+(stone-stone.mean(axis=(0,1)))*.74
    path=ART/'InlayLimestone_Color.jpg'
    Image.fromarray(np.clip(stone,0,255).astype('uint8')).save(path,quality=94,subsampling=0)
    g=meta(path,PHOTO/'Stone_Color.jpg')
    material=(PHOTO/'Stone.mat').read_text().replace('m_Name: Stone','m_Name: InlayLimestone').replace(guid(PHOTO/'Stone_Color.jpg'),g)
    path=ART/'InlayLimestone.mat';path.write_text(material);meta(path,PHOTO/'Stone.mat')

if __name__=='__main__':make()
