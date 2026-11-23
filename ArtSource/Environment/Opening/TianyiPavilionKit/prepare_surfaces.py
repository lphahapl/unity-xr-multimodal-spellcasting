"""Build the pavilion trims from the project's Workshop photographs (Python + Pillow)."""
from pathlib import Path
import sys

import numpy as np
from PIL import Image


ROOT = Path(__file__).resolve().parents[4]
SOURCE = ROOT / 'Assets/Art/Environment/SuspendedPath/Surfaces/Workshop'
DEST = ROOT / 'Assets/Art/Environment/Opening/TianyiPavilionKit'


def photo(name, size):
    with Image.open(SOURCE / name) as im:
        return np.array(im.resize(size, Image.Resampling.LANCZOS))

def normal_with_relief(rgb,height,width_m,height_m,strength):
    n=rgb[:,:,:3].astype(float)/127.5-1
    n[:,:,:2]*=strength
    rows,cols=height.shape
    n[:,:,0]-=(np.roll(height,-1,1)-np.roll(height,1,1))*cols/(2*width_m)
    n[:,:,1]+=np.gradient(height,axis=0)*rows/height_m
    n/=np.linalg.norm(n,axis=2,keepdims=True).clip(.001)
    return ((n+1)*127.5).clip(0,255).astype('uint8')

def wall_atlas():
    """Two horizontal strips: hand-set rubble below, tamped earth above."""
    w,h=2048,512
    y,x=np.mgrid[:h,:w]
    u=(x+.5)/w
    v=(y+.5)/h
    tau=2*np.pi
    photos=ROOT/'Assets/Art/Environment/Opening/PhotoArchitecture'
    def sample_pair(path,u,v):
        # Bilinear, periodic photographic sampling for the colour / normal pair.
        with Image.open(path) as image:
            src=np.asarray(image.convert('RGB'),dtype=float)
        yy=(v%1)*src.shape[0];xx=(u%1)*src.shape[1]
        i=xx.astype(int);j=yy.astype(int)
        fx=(xx-i)[:,:,None];fy=(yy-j)[:,:,None]
        ii=(i+1)%src.shape[1];jj=(j+1)%src.shape[0]
        return ((1-fx)*src[j,i]+fx*src[j,ii])*(1-fy)+((1-fx)*src[jj,i]+fx*src[jj,ii])*fy
    # The joint edges, flaked faces and tone changes come from a photograph of
    # split blocks. Slightly uneven bed heights keep both visible courses legible.
    u_rock=u+.002*np.sin(tau*v+.8)+.002*np.sin(2*tau*u+4*v)
    v_rock=(3.09+1.85*v+.050*np.sin(tau*2*u)+.034*np.sin(tau*5*u+.7))/12
    blocks=sample_pair(photos/'Brick_Color.jpg',u_rock,v_rock)
    blocks_normal=sample_pair(photos/'Brick_Normal.png',u_rock,v_rock)
    mineral=sample_pair(SOURCE/'Ashlar_BaseSmooth.png',u,.22+v*.19)
    mineral_normal=sample_pair(SOURCE/'Ashlar_NormalGL.png',u,.22+v*.19)
    detail=mineral.mean(axis=2)-mineral.mean()
    stone=blocks*.88+[37,33,28]+detail[:,:,None]*.38
    rock_normal=normal_with_relief(blocks_normal,.0014*detail/24,2.65,.44,.97)
    # A seam-free piece of rock at the very bottom serves single cut stones,
    # small returns and column shoes. Wall courses use the rest of this band.
    trim_height=96
    stone=np.vstack((np.array(Image.fromarray(stone.clip(0,255).astype('uint8')).resize((w,h-trim_height))),
                     (mineral*.94+[26,25,22])[-trim_height:]))
    rock_normal=np.vstack((np.array(Image.fromarray(rock_normal).resize((w,h-trim_height))),
                           mineral_normal[-trim_height:]))
    earth=sample_pair(SOURCE/'Plaster_BaseSmooth.png',u,.31+v*.29)
    lime=sample_pair(photos/'Lime_Color.jpg',u,.42+v*.24).mean(axis=2)
    lime=(lime-lime.mean())/24
    earth_luma=earth.mean(axis=2)
    sediment=v*3.4+.13*np.sin(tau*u*2)+.06*np.sin(tau*u*5+1.1)+.11*lime
    seam=np.exp(-((sediment-np.round(sediment))/.083)**2)
    variation=(earth_luma-earth_luma.mean())*.98+lime*15-5.5*seam+2.3*np.sin(tau*sediment)
    earth=np.array([162,146,125])[None,None,:]+variation[:,:,None]
    earth_normal=normal_with_relief(sample_pair(SOURCE/'Plaster_NormalGL.png',u,.31+v*.29),.0014*lime-.0014*seam,2.65,.64,.77)
    for suffix,soil,rock in [('Color',earth,stone),('Normal',earth_normal,rock_normal)]:
        atlas=np.vstack((soil,rock)).clip(0,255).astype('uint8')
        Image.fromarray(atlas).save(DEST/('EarthPlinth'+suffix+'.png'))


def wood():
    # Bottom 3/8 of the UV atlas is the sawn end. Keep the photographic colour
    # and normal crops registered. UVs leave a guard band at the atlas border.
    for source_suffix, target in [('BaseSmooth', 'Color'), ('NormalGL', 'Normal')]:
        long = photo(f'Timber_{source_suffix}.png', (2048, 1280))
        end = photo(f'EndGrain_{source_suffix}.png', (768, 768))
        y,x=np.mgrid[:768,:768]/768
        dx=x-.458;dy=(y-.514)*1.07
        radius=np.hypot(dx,dy)
        angle=np.arctan2(dy,dx)
        phase=radius*26+.13*np.sin(angle*3+radius*14)+.12*np.sin(radius*35)
        latewood=np.exp(-((phase-np.round(phase))/.12)**2)
        checks=np.zeros_like(radius)
        for direction,start in ((.42,.19),(2.69,.29),(-1.62,.24)):
            delta=angle-direction-.021*np.sin(radius*34+direction)
            dist=np.abs(np.arctan2(np.sin(delta),np.cos(delta)))*radius
            checks=np.maximum(checks,np.exp(-(dist/.0028)**2)*np.clip((radius-start)*12,0,1))
        if target=='Color':
            end=end[:,:,:3].astype(float)
            end=(end*(1.08-.13*latewood-.27*checks)[:,:,None]).clip(0,255).astype('uint8')
            long=long[:,:,:3]
        else:
            end=normal_with_relief(end,-.00032*latewood-.00073*checks,.32,.32,.80)
        end = np.tile(end, (1, 3, 1))[:, :2048]
        atlas = np.concatenate((long, end))
        if target == 'Color':
            # Unity materials supply their own smoothness. Export only RGB so
            # the source alpha cannot premultiply and darken the colour in DCCs.
            atlas = np.clip(atlas[:, :, :3]*[1.09,1.07,1.055]+[8,9,10],0,255).astype('uint8')
        Image.fromarray(atlas).save(DEST / f'Timber{target}.png')


def tile(folder=DEST,normal_only=False):
    # Eight barrels and four courses per repeat, aligned with the modeled
    # 28 cm mouths. Keep the firing pattern and the source ceramic micro-normal.
    size = 1024
    y, x = np.mgrid[:size, :size]
    u = (x + .5) * 8 / size
    v = (1 - (y + .5) / size) * 4
    ridge = np.cos(2*np.pi*u)
    course = (v + np.where(ridge > .1, .43, 0)) % 1
    lip = np.exp(-(np.minimum(course, 1-course) / .049)**2)
    band = np.floor(v + np.where(ridge > .1, .43, 0)) % 4
    row = np.floor(u + .5) % 8
    firing = .064*np.sin(row*4.2+band*5.17) + .037*np.cos(row*5.3-band*1.68)
    base = photo('Tile_BaseSmooth.png', (size, size)).astype(float)
    pigment = base[:, :, :3] * [1.03, .99, .91] + [37, 32, 28]
    base[:, :, :3] = pigment * (1 + firing + .052*ridge - .20*lip)[:, :, None]
    if not normal_only:
        Image.fromarray(base[:, :, :3].clip(0,255).astype('uint8')).save(folder / 'TileColor.png')
    n = photo('Tile_NormalGL.png', (size, size))[:, :, :3].astype(float)/127.5-1
    distance=np.abs(u-np.round(u))*.28
    barrel_course=(v+.43)%1
    taper=.83+.17*barrel_course
    cap=np.clip(distance/(.084*taper),0,1)
    # Rounded cap, raised shoulders of the pan and a short, stepped clay lap.
    barrel=.078*taper*np.sqrt(1-cap**2)
    pan=.021*np.clip((.14-distance)/.062,0,1)**2
    h = barrel + pan + .007*(1-course) - .0025*lip
    n[:, :, 0] -= (np.roll(h,-1,1)-np.roll(h,1,1)) * size/(2*2.24)
    n[:, :, 1] += (np.roll(h,-1,0)-np.roll(h,1,0)) * size/(2*1.44)
    n /= np.linalg.norm(n,axis=2,keepdims=True).clip(.001)
    Image.fromarray(((n+1)*127.5).clip(0,255).astype('uint8')).save(folder/'TileNormal.png')


if __name__ == '__main__':
    if '--stage-normal' in sys.argv:
        tile(Path(__file__).resolve().parent/'Review',normal_only=True)
    elif '--wall-and-wood' in sys.argv:
        wall_atlas()
        wood()
    else:
        wall_atlas()
        wood()
        tile()
