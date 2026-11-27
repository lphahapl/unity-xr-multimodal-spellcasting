"""Place sheltered pine stands on the triangle surface and prepare their cross-card atlas."""
from pathlib import Path
import numpy as np
from PIL import Image
from scipy.ndimage import distance_transform_edt, gaussian_filter
from bake_surface import HERE, ROOT, RUNTIME, CENTER, centerline, noise

grid=np.load(HERE/'Surface176.npz')
axis=grid['axis']
surface=grid['height_m']

def sample(x,z):
    i=np.clip(np.searchsorted(axis,x)-1,0,len(axis)-2)
    j=np.clip(np.searchsorted(axis,z)-1,0,len(axis)-2)
    u=(x-axis[i])/(axis[i+1]-axis[i])
    v=(z-axis[j])/(axis[j+1]-axis[j])
    return (1-np.maximum(u,v))*surface[j,i]+np.maximum(u-v,0)*surface[j,i+1]+np.maximum(v-u,0)*surface[j+1,i]+np.minimum(u,v)*surface[j+1,i+1]

def atlas():
    reference=ROOT/'ArtSource/Environment/Opening/HunyuanPine/pine-reference.png'
    src=np.asarray(Image.open(reference).convert('RGB'),dtype=np.float32)
    # The reference was authored on neutral cream; pale fine branches are
    # warmer/darker than this background, including around the canopy.
    alpha=np.clip((238-np.min(src,axis=2)-7)/45,0,1)
    alpha[alpha<.07]=0
    reference_rgba=Image.fromarray(np.dstack((src,alpha*255)).astype(np.uint8))
    cells=[reference_rgba,Image.open(HERE/'PineAzimuth1.png').convert('RGBA')]
    canvas=Image.new('RGBA',(1024,512))
    for k,im in enumerate(cells):
        im=im.crop(im.getchannel('A').getbbox())
        im.thumbnail((490,486),Image.Resampling.LANCZOS)
        canvas.paste(im,(k*512+(512-im.width)//2,500-im.height))
    rgba=np.array(canvas)
    rgb=rgba[:,:,:3]
    # Tint shadows into cool greens and preserve readable warm canopy tips.
    rgb[:]=np.uint8(np.clip(rgb*[.75,.86,.69],0,255))
    transparent=rgba[:,:,3]<50
    nearest=distance_transform_edt(transparent,return_distances=False,return_indices=True)
    rgb[transparent]=rgb[nearest[0][transparent],nearest[1][transparent]]
    Image.fromarray(rgba).save(RUNTIME/'PineAtlas.png')

def locations():
    rng=np.random.default_rng(624)
    # Use stands rather than an even jitter grid. Several layers follow
    # the southern riverbank; others sit behind the middle workshop.
    groups=np.array([[112,-86,44,34,190],[165,-124,42,38,260],[215,-173,62,32,290],
        [290,-243,71,50,440],[405,-217,70,52,310],[510,-275,80,55,410],
        [700,-330,99,69,390],[650,-64,85,60,390],[330,30,63,55,340],
        [160,148,44,46,280],[35,150,33,40,190],[-72,136,44,48,183],
        [-93,-45,46,53,230],[-190,36,60,41,140],[210,-410,70,60,238]])
    trees=[]
    for cx,cz,sx,sz,count in groups:
        # Seed each stand with small copses. Canopies overlap within each,
        # leaving broken strips of exposed rock and scree between them.
        copses=max(1,count//4)
        roots_x=rng.normal(cx,sx,copses)
        roots_z=rng.normal(cz,sz,copses)
        copse=rng.integers(0,copses,count)
        x=roots_x[copse]+rng.normal(0,4.2,count)
        z=roots_z[copse]+rng.normal(0,5.2,count)
        local_x=x-CENTER[0];local_z=z-CENTER[1]
        h=sample(local_x,local_z)
        slope=np.hypot(sample(local_x+1,local_z)-sample(local_x-1,local_z),
                       sample(local_x,local_z+1)-sample(local_x,local_z-1))/2
        use=(slope<1.50)&(np.abs(z-centerline(x))>9)
        use&=((x< -34)|(x>133)|(z< -35)|(z>108))
        use&=(noise(x,z,31,13)+.37*noise(x,z,10,1)>-.29)
        n=np.count_nonzero(use)
        size=rng.uniform(4.3,10.2,n)
        # First atlas view carries finer needle and branch detail, whereas
        # the 90-degree baked view gives the cross-card depth in stereo.
        trees.extend(np.column_stack((local_x[use],local_z[use],h[use]-.20,size,
             rng.uniform(.75,1.08,n),rng.uniform(-.62,.62,n),rng.uniform(.83,1.13,n))))
    trees=np.array(trees,dtype=np.float32)
    np.savez_compressed(HERE/'PineStands.npz',trees=trees)
    # Bake broad canopy contact and shadows into a metric terrain overlay.
    # Splat in warped macro UV so it tracks each exported tree at every LOD.
    texsize=1024
    pixels=np.linspace(0,texsize-1,len(axis))
    shade=np.zeros((texsize,texsize),dtype=np.float32)
    for x,z,height,size,width,angle,tint in trees:
        cx=np.interp(x+.20*size,axis,pixels)
        cz=np.interp(z-.16*size,axis,pixels)
        rx=max(.55,np.interp(x+size*.55,axis,pixels)-np.interp(x,axis,pixels))
        rz=max(.55,np.interp(z+size*.73,axis,pixels)-np.interp(z,axis,pixels))
        lo_x=max(0,int(cx-2*rx));hi_x=min(texsize,int(cx+2*rx)+1)
        lo_z=max(0,int(cz-2*rz));hi_z=min(texsize,int(cz+2*rz)+1)
        yy,xx=np.mgrid[lo_z:hi_z,lo_x:hi_x]
        stain=np.exp(-1.65*(((xx-cx)/rx)**2+((yy-cz)/rz)**2))
        region=shade[lo_z:hi_z,lo_x:hi_x]
        region[:]=1-(1-region)*(1-stain*.86)
    Image.fromarray(np.uint8(gaussian_filter(shade,.55)[::-1]*255)).save(RUNTIME/'CanopyShade.png')
    print('Pine instances:',len(trees))
if __name__=='__main__':
    atlas()
    locations()
