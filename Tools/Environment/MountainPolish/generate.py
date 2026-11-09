"""Offline deterministic original terrain and stone texture authoring. No network dependencies."""
from pathlib import Path
import json, math, hashlib
import numpy as np
from PIL import Image, ImageFilter
from PIL import ImageDraw

ROOT = Path(__file__).resolve().parents[3]
OUT = ROOT / 'Assets/Art/Environment/Opening/MountainPolish'
SOURCE = ROOT / 'ArtSource/Environment/Opening/MountainPolish'
LOG = ROOT / 'Logs/exterior-polish-20261108/mountains'
for p in [OUT,SOURCE,LOG]: p.mkdir(parents=True, exist_ok=True)

def texture():
    n=512
    yy,xx=np.mgrid[0:n,0:n]/n
    rng=np.random.default_rng(6110802)
    def smooth(scale):
        a=rng.integers(0,256,(scale,scale),dtype=np.uint8)
        tiled=np.tile(a,(3,3))
        return np.asarray(Image.fromarray(tiled).resize((n*3,n*3),Image.Resampling.BICUBIC))[n:2*n,n:2*n].astype(float)/255
    broad=smooth(5); grain=smooth(20); fine=rng.random((n,n))
    # Periodic strata follow a slightly folded bedding plane; fine seams stay quiet in VR.
    phase=yy*7+np.sin(xx*math.tau*2)*.12+np.sin(xx*math.tau*5)*.025
    seam=np.exp(-(np.sin(phase*math.pi)/.25)**2)*(.35+.65*broad)
    fissure=np.exp(-(np.sin(xx*math.tau*5+np.sin(yy*math.tau*2)*.25)/.11)**2)
    tone=.88+.055*broad+.02*grain+.002*fine-.018*seam-.010*fissure
    rgb=np.stack([tone*.42,tone*.46,tone*.50],-1)
    Image.fromarray(np.uint8(np.clip(rgb,0,1)*255)).save(OUT/'WeatheredSlate_Albedo.png')
    height=grain*.025+broad*.025-seam*.004-fissure*.004+fine*.0004
    gy,gx=np.gradient(height)
    normal=np.stack([-gx*18,-gy*18,np.ones_like(gx)],-1)
    normal/=np.linalg.norm(normal,axis=2,keepdims=True)
    Image.fromarray(np.uint8((normal*.5+.5)*255)).save(OUT/'WeatheredSlate_Normal.png')
    packed=np.zeros((n,n,4),dtype=np.uint8)
    packed[:,:,3]=np.uint8((.12+grain*.025)*255)
    Image.fromarray(packed).save(OUT/'WeatheredSlate_MetalSmooth.png')
    # Valley floor is granular soil/slate scree, with no directional bedding lines.
    valley_broad=smooth(7); valley_grain=smooth(100)
    grit=.35+.035*valley_broad+.025*valley_grain+.006*(rng.random((n,n))-.5)
    valley=np.stack([grit*.97,grit*1.025,grit*1.055],-1)
    gravel=Image.fromarray(np.uint8(np.clip(valley,0,1)*255)).convert('RGB')
    brush=ImageDraw.Draw(gravel)
    for i in range(380):
        cx,cy=rng.integers(0,n,2);rx=int(rng.integers(2,7));ry=int(rng.integers(1,4))
        value=int(rng.integers(83,105))
        for dx in [-n,0,n]:
            for dy in [-n,0,n]:
                brush.ellipse((cx-rx+dx,cy-ry+dy,cx+rx+dx,cy+ry+dy),fill=(value,int(value*1.025),int(value*1.045)))
    gravel=gravel.filter(ImageFilter.GaussianBlur(.45));gravel.save(OUT/'ValleyScree_Albedo.png')
    granular_height=np.asarray(gravel).astype(float).mean(-1)/255*.028
    gy,gx=np.gradient(granular_height)
    granular_normal=np.stack([-gx*10,-gy*10,np.ones_like(gx)],-1)
    granular_normal/=np.linalg.norm(granular_normal,axis=2,keepdims=True)
    Image.fromarray(np.uint8((granular_normal*.5+.5)*255)).save(OUT/'ValleyScree_Normal.png')
    # Tile sheet represents 8 columns x 8 rows (1.76m x 2.8m in roof UVs).
    tx=xx*8;ty=yy*8;column=tx%1;row=ty%1
    crown=np.sin(column*math.pi)**2
    row_lip=np.exp(-((row-.04)/.055)**2)
    tile_variation=smooth(8)
    tile_tone=.29+.028*crown+.025*tile_variation-.045*row_lip+.002*(fine-.5)
    roof_rgb=np.stack([tile_tone*.96,tile_tone,tile_tone*1.04],-1)
    Image.fromarray(np.uint8(np.clip(roof_rgb,0,1)*255)).save(OUT/'GreyTile_Albedo.png')
    tile_height=crown*.045-row_lip*.018
    gy,gx=np.gradient(tile_height);tn=np.stack([-gx*160,-gy*110,np.ones_like(gx)],-1);tn/=np.linalg.norm(tn,axis=2,keepdims=True)
    Image.fromarray(np.uint8((tn*.5+.5)*255)).save(OUT/'GreyTile_Normal.png')

def mesh(name,seed,bluff=False):
    nx,nz=(24,16) if bluff else (48,24)
    width,depth=(22.,17.) if bluff else (64.,42.)
    x,z=np.meshgrid(np.linspace(-1,1,nx+1),np.linspace(-1,1,nz+1))
    rng=np.random.default_rng(seed)
    # Several displaced ridges with unequal saddles. Never a radial cone or regular sawtooth.
    h=np.zeros_like(x)
    count=3 if bluff else 4
    accumulated=np.zeros_like(x)
    for i in range(count):
        cx=-.78+i*1.56/(count-1)
        cz=rng.uniform(-.24,.24)
        amplitude=rng.uniform(.48,1.0)
        cx+=rng.uniform(-.07,.07)
        ridge=np.exp(-((x-cx)/rng.uniform(.36,.63))**2-((z-cz)/rng.uniform(.48,.82))**2)
        h=np.maximum(h,amplitude*ridge)
        accumulated+=amplitude*ridge
    h=h*.55+accumulated*.45
    h/=h.max()
    # Incised folds run down the flanks and terminate below the skyline.
    gullies=(np.abs(np.sin(x*15+np.sin(z*4+seed)*.7))**10)*.08
    h-=gullies*np.clip(np.abs(z)*2,0,1)*h
    warp=.013*np.sin(x*13+seed)+.012*np.sin(z*11+x*7)
    h=np.maximum(0,h+warp*(1-x*x)*(1-z*z))
    if not bluff:
        # Upper-ridge detail only: 7-13m crest waviness, and two shallow unequal
        # saddles. No new near instances, no change to submerged mountain feet.
        summit=np.clip((h-.40)/.25,0,1);summit=summit*summit*(3-2*summit)
        crest=(.022*np.sin(x*16+seed*.8)+.016*np.sin(x*29+seed))
        saddle=.075*np.exp(-((x+.16)/.065)**2)+.060*np.exp(-((x-.45)/.075)**2)
        h=np.maximum(0,h+(crest-saddle)*summit*np.exp(-(z/.60)**2))
    # C1 edge easing: border enters the submerged base with zero slope.
    # Linear clipping created a clearly visible vertical-looking shoulder in actual overview.
    ex=np.clip((1-np.abs(x))/.42,0,1);ez=np.clip((1-np.abs(z))/.42,0,1)
    edge=(ex*ex*(3-2*ex))*(ez*ez*(3-2*ez))
    h*=edge
    if bluff:
        h=np.minimum(h*2.8,.83+np.sin(x*5)*.09)
        # Bedding steps are geometric ledges, not flat-shaded triangles.
        h=h*.83+np.floor(h*13)/13*.17
    else:
        h+=np.sin(x*38+z*13)*np.sin(z*23)*.018*h
    scale=11 if bluff else (24+(seed%4)*2.2)
    y=h*scale-1.0
    pts=np.stack([x*width/2,y,z*depth/2],-1).reshape(-1,3)
    triangles=[]
    for j in range(nz):
        for i in range(nx):
            a=j*(nx+1)+i;b=a+1;c=a+nx+1;d=c+1
            triangles.extend([(a,c,b),(b,c,d)])
    normals=np.zeros_like(pts)
    for t in triangles:
        a,b,c=pts[list(t)]; n=np.cross(b-a,c-a)
        for index in t:normals[index]+=n
    normals/=np.maximum(np.linalg.norm(normals,axis=1,keepdims=True),1e-12)
    # Per-triangle dominant-plane UVs avoid stretched cliffs; normals remain shared/smooth.
    lines=['# Original procedural weathered ridge; Y up; metres.','o '+name]
    lines += ['v %.6f %.6f %.6f'%tuple(p) for p in pts]
    lines += ['vn %.6f %.6f %.6f'%tuple(p) for p in normals]
    faces=[];uvcount=0
    for t in triangles:
        a,b,c=pts[list(t)];n=np.abs(np.cross(b-a,c-a));axis=int(np.argmax(n))
        for index in t:
            p=pts[index]
            u,v=((p[2],p[1]) if axis==0 else (p[0],p[2]) if axis==1 else (p[0],p[1]))
            lines.append('vt %.6f %.6f'%(u*.045,v*.045))
        faces.append('f '+' '.join('%d/%d/%d'%(index+1,uvcount+k+1,index+1) for k,index in enumerate(t)))
        uvcount+=3
    lines+=faces
    (OUT/(name+'.obj')).write_text('\n'.join(lines)+'\n',encoding='utf-8')
    return dict(name=name,triangles=len(triangles),vertices=len(pts),bounds=[pts.min(0).tolist(),pts.max(0).tolist()],finite=bool(np.isfinite(pts).all()),upward_winding=bool(all(np.cross(pts[b]-pts[a],pts[c]-pts[a])[1]>0 for a,b,c in triangles)))

texture()
report=[mesh('Ridge_BrokenCrown',71),mesh('Ridge_LongShoulder',74),mesh('Ridge_SplitSaddle',78),mesh('Ridge_WeatheredSpine',83),mesh('Bluff_BeddedSlate',89,True)]
report={'generator':'original numpy heightfields and Pillow material maps; no external source assets','meshes':report,'placed_triangles':8*2304,'material_count':2,'texture_size':512,'sha256':{p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in OUT.iterdir() if p.is_file() and p.suffix in ['.obj','.png']}}
(LOG/'asset-audit.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
(SOURCE/'provenance.json').write_text(json.dumps({'source':'Tools/Environment/MountainPolish/generate.py','license':'Original project-authored procedural geometry and textures','seeds':[71,74,78,83,89,6110802]},indent=2),encoding='utf-8')
print(json.dumps(report,indent=2))
