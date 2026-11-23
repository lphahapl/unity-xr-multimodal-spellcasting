"""Build the registered A1 trim sheets with project-owned CC0 surface derivatives."""
from pathlib import Path
import numpy as np
from PIL import Image
from surface_uv import END_RECTS, TILES

SOURCE = Path(__file__).resolve().parent
ROOT = SOURCE.parents[4]
ASSETS = ROOT / 'Assets/Art/Environment/Opening/CourtArchitecture'
PHOTO = ROOT / 'Assets/Art/Environment/Opening/PhotoArchitecture'
WORK = ROOT / 'Assets/Art/Environment/SuspendedPath/Surfaces/Workshop'
# Pixel rectangles on a 2048 sheet, top-left origin. Eight-pixel gutters at LOD0.
ZONES = dict(roof=(0,0,1280,1280), timber=(0,1280,1280,2048),
             clay=(1280,0,2048,480), stamp=(1280,480,2048,768),
             end=END_RECTS[0], cut1=END_RECTS[1], cut2=END_RECTS[2], cut3=END_RECTS[3],
             deck=(1280,1104,2048,1456),
             stone=(1280,1456,2048,2048))

def photo(path, size):
    return np.asarray(Image.open(path).convert('RGB').resize(size, Image.Resampling.LANCZOS), dtype=np.float32)

def unit(a):
    return a / np.maximum(np.linalg.norm(a, axis=-1, keepdims=True), 1e-6)

def fired_tiles(lum,normal,scan_alpha,rng):
    h,w=lum.shape
    yy,xx=np.mgrid[:h,:w]
    u=(xx+.5)*TILES/w
    v=(h-yy-.5)*TILES/h
    phase=u%1-.5
    roll=np.sqrt(np.maximum(0,1-(phase/.235)**2))
    cover=roll>0
    column=np.floor(u).astype(int)
    # Distinct firing values for each pan and cover, including the cut-off edge courses.
    joints=v+rng.uniform(-.12,.12,TILES)[column]+.36*cover+.014*np.sin(u*14.73)
    course=np.floor(joints).astype(int)+1
    firing=rng.normal(0,1,(TILES+2,TILES,2))[course,column,cover.astype(int)]
    mineral=rng.normal(0,1,(TILES+2,TILES,2))[course,column,cover.astype(int)]
    cloud=Image.fromarray(rng.normal(0,1,(5,7)).astype('f')).resize((w,h),Image.Resampling.BICUBIC)
    weather=np.asarray(cloud)
    row=joints%1
    lip=np.exp(-((row-.025)/.042)**2)
    edge=np.exp(-((row-.953)/.036)**2)
    channel=np.exp(-((abs(phase)-.247)/.061)**2)
    fired=np.clip(.76*firing+.38*weather,-2.2,2.2)
    light=91+1.05*(lum-lum.mean())+9.8*fired+3.4*weather+4*roll-25*lip+9*edge-7*channel
    warm=np.clip(2.9*mineral+2*weather,-6.4,6.4)
    color=np.stack((light+6.8+warm,light+1.2,light-4.8-.66*warm),axis=-1)
    relief=.105*roll+.014*(1-np.minimum(1,abs(phase)*2))-.031*lip+.017*edge
    normal[:,:,0]-=np.gradient(relief,axis=1)*w/TILES
    normal[:,:,1]+=np.gradient(relief,axis=0)*h/TILES
    # Smoother on worn shoulders, duller in troughs and on ash-dusted pieces.
    smooth=65+.36*(scan_alpha-np.mean(scan_alpha))+13.5*roll-8*channel-11*lip+12.5*fired+4.2*mineral
    return color,normal,np.clip(smooth,34,108)

def rafter_cut(base,normal,variant):
    h,w=base.shape[:2]
    yy,xx=np.mgrid[:h,:w]
    x=((xx+.5)/w-.5)/.44
    y=(.5-(yy+.5)/h)/.44
    ox,oy=[(-.57,.19),(.81,-.37),(-.21,-.46)][variant]
    x,y=x-ox,y-oy
    ang=np.arctan2(y,x)
    rad=np.hypot(x*.96,y*1.08)
    radial=rad*(1+.038*np.sin(3*ang+.3*variant)+.028*np.sin(5*ang+rad*3))
    phase=(6.7+.56*variant)*radial+.23*np.sin(radial*10+variant)+.09*np.sin(radial*25-variant)
    late=np.exp(-((phase%1-.73)/.175)**2)
    pale=np.exp(-((phase%1-.48)/.23)**2)
    cracks=np.zeros_like(rad)
    patterns=[[(.30,.27),(-2.12,.68)],[(1.90,.54),(-.68,.32)],[(-2.61,.35),(.82,.59)]]
    for direction,start in patterns[variant]:
        length=np.clip((rad-start)/.43,0,1)
        wobble=.048*np.sin(9*rad+variant)+.022*np.sin(27*rad)
        distance=rad*np.abs(np.sin(ang-direction-wobble))
        width=.005+.023*length
        cracks=np.maximum(cracks,length*np.exp(-(distance/width)**2)*(np.cos(ang-direction)>.5))
    # Keep photographed saw marks and pores under the larger latewood and drying checks.
    luma=base@np.array([.299,.587,.114])
    variation=.83*(luma-luma.mean())+5.0*pale-17.5*late-47*cracks
    color=base*.41+[66,48,34]+variation[:,:,None]*np.array([1.03,.92,.78])
    height=-.00042*late-.0015*cracks+.00013*pale
    normal[:,:,0]=.42*normal[:,:,0]-np.gradient(height,axis=1)*w/.196
    normal[:,:,1]=.42*normal[:,:,1]+np.gradient(height,axis=0)*h/.196
    return color,normal,47-8*late-15*cracks+.13*(luma-luma.mean())

def make_maps(name, seed):
    sheets = [Image.new('RGB',(2048,2048)), Image.new('RGB',(2048,2048),(128,128,255)), Image.new('RGBA',(2048,2048),(0,255,0,60))]
    rng = np.random.default_rng(seed)
    for zone, (x,y,right,bottom) in ZONES.items():
        w,h = right-x-16,bottom-y-16
        size = w,h
        family='end' if zone.startswith('cut') else zone
        surface = {'roof':'Tile','clay':'Tile','stamp':'Tile','end':'EndGrain','stone':'Ashlar','timber':'Timber','deck':'Timber'}[family]
        base = photo(WORK / (surface+'_BaseSmooth.png'), size)
        normal = photo(WORK / (surface+'_NormalGL.png'), size) / 127.5 - 1
        if zone=='timber':
            base = photo(PHOTO/'Timber_Color.jpg', size)
            normal = photo(PHOTO/'Timber_Normal.png', size) / 127.5 - 1
        lum = base @ np.array([.299,.587,.114],dtype='f')
        ypix,xpix = np.mgrid[:h,:w].astype('f')
        if zone=='deck':
            # Strip sampling preserves longitudinal fibres and separates the visible soffit boards.
            deck_rng=np.random.default_rng(seed)
            deck_rng.standard_normal(8*8*2)  # Preserve the original A1 board selection.
            count={'CourtMainRoof':61,'CourtLowWing':47,'CourtGatehouse':23}[name]
            s=xpix/w*count
            board=np.floor(s).astype(int)
            grain=photo(PHOTO/'Timber_Color.jpg',(1024,1024))
            offset=deck_rng.integers(0,1024,(count,2))
            px=(ypix/h*1510+offset[board,0]).astype(int)%1024
            py=((s%1)*135+offset[board,1]).astype(int)%1024
            base=grain[py,px]*.85+deck_rng.normal(0,4,count)[board,None]
            seam=np.exp(-((s%.9999)/.066)**2)
            base-=seam[:,:,None]*28
            normal=np.zeros_like(normal)
            normal[:,:,2]=1
            normal[:,:,0]=-.23*seam
            lum=base @ np.array([.299,.587,.114],dtype='f')
        if zone=='roof':
            with Image.open(WORK/'Tile_BaseSmooth.png') as scan:
                alpha=np.asarray(scan.getchannel('A').resize(size,Image.Resampling.LANCZOS),dtype='f')
            base,normal,smooth=fired_tiles(lum,normal,alpha,rng)
        elif zone in ('clay','stamp'):
            light = 88 + 1.22 * (lum-lum.mean())
            warmth = np.zeros_like(light)
            if zone=='stamp':
                # Two shallow concentric tool marks on the exposed fired end.
                radius=np.hypot((xpix/w-.50)/.44,(ypix/h-.50)/.44)
                light+=7*np.exp(-((radius-.78)/.045)**2)-14*np.exp(-((radius-.72)/.038)**2)
                light+=4*np.exp(-((radius-.52)/.035)**2)-6*np.exp(-((radius-.48)/.034)**2)
            base = np.stack((light+5+warmth,light+1,light-3-.4*warmth),axis=-1)
            smooth = 49 + (light-90)*.27
        elif zone in ('timber','deck'):
            base = .68*base + .32*lum[:,:,None] + [7,3,0]
            smooth = 54 + (lum-lum.mean())*.28
        elif zone.startswith('cut'):
            base,normal,smooth=rafter_cut(base,normal,int(zone[-1])-1)
        elif zone=='end':
            base = base*.80 + [2,1,0]
            smooth = 46 + (lum-lum.mean())*.18
        else:
            base = base*.85 + [8,6,4]
            smooth = 43 + (lum-lum.mean())*.23
        mask=np.zeros((h,w,4),dtype='uint8')
        mask[:,:,1]=255
        mask[:,:,3]=np.clip(smooth,24,108)
        for sheet,array in zip(sheets,[base,127.5+127.5*unit(normal),mask]):
            pixels=np.pad(np.uint8(np.clip(array,0,255)),((8,8),(8,8),(0,0)),mode='edge')
            sheet.paste(Image.fromarray(pixels),(x,y))
    dest = ASSETS/name
    for tier,res in enumerate([2048,1024,512]):
        for suffix,sheet in zip(['BaseColor','Normal','Mask'],sheets):
            out = sheet if res==2048 else sheet.resize((res,res),Image.Resampling.LANCZOS)
            out.save(dest/f'{name}_Tier{tier}_{suffix}.png')

if __name__=='__main__':
    for i,name in enumerate(('CourtMainRoof','CourtLowWing','CourtGatehouse')):
        make_maps(name, 10601+i)

