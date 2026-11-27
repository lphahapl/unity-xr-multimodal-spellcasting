"""Bake a high-elevation sky and three distant watersheds sampled from the project's DEM."""
from pathlib import Path
import numpy as np
from scipy.ndimage import map_coordinates
from PIL import Image
from bake_surface import HERE, RUNTIME, ROOT, GROUND, AXIS, smooth, mix

W,H=4096,2048
lon=(.5-(np.arange(W)+.5)/W)*2*np.pi
lat=(.5-(np.arange(H)+.5)/H)*180
xx=np.cos(lon);zz=np.sin(lon)
# Angular rather than planar mapping leaves no left/right sky seam.
u=lat[:,None]
sun=np.maximum(0,.78*xx-.5*zz)
gradient=smooth((u+5)/63)
sky=mix(np.array([.745,.816,.853]),np.array([.355,.550,.705]),gradient[...,None])
sky=sky+sun[None,:,None]*np.exp(-((u-1)/16)**2)[...,None]*[.061,.033,.003]
cloudmap=np.asarray(Image.open(ROOT/'Assets/Art/Environment/Opening/Skybox/CirrusDensity.png').convert('L'))/255
up=np.sin(np.radians(u))
plane=.12+np.maximum(0.,up)
cloud=map_coordinates(cloudmap,[(np.broadcast_to(zz/plane*.27,(H,W))%1)*(cloudmap.shape[0]-1),
                                (np.broadcast_to(xx/plane*.27,(H,W))%1)*(cloudmap.shape[1]-1)],order=1,mode='wrap')
cloud*=np.exp(-((u-15)/26)**2)*smooth((u+1)/8)
sky=mix(sky,np.array([.92,.919,.88]),(.49*cloud)[...,None])
def dem(x,z):
    return map_coordinates(GROUND,[(z-AXIS[0])/10,(x-AXIS[0])/10],order=1,mode='reflect')

for i,(base,width,detail,shade) in enumerate([
        (-4.8,1.7,.41,[.663,.752,.815]),
        (-8.2,2.1,.55,[.586,.696,.772]),
        (-13.0,2.5,.67,[.515,.645,.731])]):
    # Read an actual irregular skyline from a ring through measured terrain.
    # The heights are angularly compressed to the intended lookout horizon.
    x=xx*(950+180*i)+[90,560,-320][i]
    z=zz*(980+170*i)+[-150,340,-450][i]
    raw=dem(x,z)
    undulation=(raw-np.median(raw))/max(65,np.std(raw))
    angle=base+width*undulation
    depth=np.maximum(0,angle[None,:]-u)
    # The inner azimuth at the foot of a mountain differs from the peak.
    # Sloping ravines and sun-facing crests remain visible through haze.
    dx=np.sin(lon)[None,:]*depth*25
    dz=-np.cos(lon)[None,:]*depth*25
    fh=dem(x[None,:]+dx,z[None,:]+dz)
    relief=(fh-dem(x[None,:]+dx+28,z[None,:]+dz-19))/35
    foot=np.exp(-depth/8)
    tone=np.clip(relief,-1.7,1.7)*(.030*detail)+.012*foot
    band=np.asarray(shade)+tone[...,None]
    band=mix(band,np.array([.555,.665,.746]),(.56*smooth(depth/17))[...,None])
    alpha=smooth((angle[None,:]-u+.05)/.15)
    sky=mix(sky,band,alpha[...,None])
Image.fromarray(np.uint8(np.clip(sky,0,1)*255+.5)).save(RUNTIME/'GorgeHorizon.jpg',quality=95,subsampling=0)
