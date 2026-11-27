"""A4 镇石。所有取样和工具痕在同一张周期高度场上生成，单位为毫米。"""
from pathlib import Path
import gc
import json
import numpy as np
from PIL import Image
import cv2

DEST = Path(__file__).resolve().parents[1]
N = 2048
X = np.arange(N, dtype=np.float32)[None, :]
Y = np.arange(N, dtype=np.float32)[:, None]
cv2.setNumThreads(4)
F = np.float32
stats = {}


def field(rng, cols, rows, dx=0, dy=0):
    coarse = rng.standard_normal((rows, cols), dtype=F)
    sx = np.ascontiguousarray(np.broadcast_to((X + dx) * F(cols / N), (N, N)), dtype=F)
    sy = np.ascontiguousarray(np.broadcast_to((Y + dy) * F(rows / N), (N, N)), dtype=F)
    return cv2.remap(coarse, sx, sy, cv2.INTER_CUBIC, borderMode=cv2.BORDER_WRAP)


def blur(im, sigma):
    pad = int(np.ceil(sigma * 3))
    bigger = cv2.copyMakeBorder(im, pad, pad, pad, pad, cv2.BORDER_WRAP)
    return cv2.GaussianBlur(bigger, (2 * pad + 1, 2 * pad + 1), sigma)[pad:-pad, pad:-pad].copy()


def smooth(a, b, value):
    t = np.clip((value - a) / (b - a), 0, 1)
    return t * t * (3 - 2*t)


def poly(im, pts, value, fill=True, width=1):
    """在周期边界的另一侧接续多边形或细线。坐标保留 1/16 像素。"""
    pts = np.asarray(pts, dtype=F)
    lo, hi = pts.min(axis=0)-width-2, pts.max(axis=0)+width+2
    for ox in range(int(np.ceil(-hi[0]/N)), int(np.floor((N-1-lo[0])/N))+1):
        for oy in range(int(np.ceil(-hi[1]/N)), int(np.floor((N-1-lo[1])/N))+1):
            q = np.rint((pts + np.array([ox*N, oy*N]))*16).astype(np.int32)
            if fill:
                cv2.fillConvexPoly(im, q, int(value), cv2.LINE_AA, shift=4)
            else:
                cv2.polylines(im, [q], False, int(value), width, cv2.LINE_AA, shift=4)


def chisel(rng):
    old = np.zeros((N, N), np.uint8)
    new = old.copy()
    facets = old.copy()
    for _ in range(57):
        center = rng.uniform(0, N, 2)
        direction = rng.uniform(-.42, .25)
        d = np.array([np.cos(direction), np.sin(direction)])
        side = np.array([-d[1], d[0]])
        for _ in range(rng.integers(5, 19)):
            start = center + d*rng.normal(0,64) + side*rng.normal(0,26)
            length = rng.uniform(12, 68)
            width = rng.uniform(2.3, 7.8)
            tip = start + length*d + side*rng.normal(0, 1.6)
            lip = start+d*length*.28+side*width
            points = [start, lip, tip, start+d*length*.34-side*width*.38]
            target = old if rng.random() < .72 else new
            poly(target, points, rng.uniform(88, 242))
            poly(facets, [start, lip, start+d*length*.75+side*width*.08], rng.uniform(80, 220))
    return blur(old.astype(F)/255,1.1), blur(new.astype(F)/255,.5), facets.astype(F)/255


def fissures(rng):
    lines = np.zeros((N,N), np.uint8)
    for _ in range(30):
        initial = rng.uniform(0,N,2)
        angle = rng.uniform(-.6,.35) + (np.pi/2 if rng.random()<.25 else 0)
        path=[initial]; value=rng.uniform(95,230)
        for _ in range(rng.integers(5,18)):
            a=angle+rng.normal(0,.26); step=rng.uniform(5,23)
            path.append(path[-1]+step*np.array([np.cos(a),np.sin(a)]))
            if rng.random()<.24:
                branch=[path[-1]]; angle2=a+rng.choice([-1,1])*rng.uniform(.35,1.0)
                for _ in range(rng.integers(2,6)):
                    a2=angle2+rng.normal(0,.19)
                    branch.append(branch[-1]+rng.uniform(4,14)*np.array([np.cos(a2),np.sin(a2)]))
                poly(lines,branch,value*.65,False,1)
        poly(lines,path,value,False,int(rng.choice([1,2,2,3])))
    return blur(lines.astype(F)/255,.62)


def colour(base, scalar, layers):
    rgb = np.empty((N,N,3), np.float32)
    for i in range(3):
        channel = base[i] + scalar
        for mask, swatch in layers:
            channel = channel*(1-mask) + mask*swatch[i]
        rgb[:,:,i] = channel
    return rgb


def save(name, rgb, height, rough, metal, ao, tile_mm, extra):
    target = DEST / name
    target.mkdir(parents=True, exist_ok=True)
    def store(suffix, pixels):
        Image.fromarray(pixels).save(target / f"{name}_{suffix}.png", compress_level=5)
    u8=lambda v: np.uint8(np.clip(v*255+.5,0,255))
    rough=np.clip(rough,0,1); metal=np.broadcast_to(metal,(N,N)); ao=np.clip(ao,0,1)
    store("BaseColor",np.uint8(np.clip(rgb+.5,0,255)))
    store("Roughness",u8(rough)); store("Metallic",u8(metal))
    store("ORM",np.stack([u8(ao),u8(rough),u8(metal)],axis=2))
    mm_per_pixel=tile_mm/N
    nx=(np.roll(height,1,axis=1)-np.roll(height,-1,axis=1))/(2*mm_per_pixel)
    ny=(np.roll(height,-1,axis=0)-np.roll(height,1,axis=0))/(2*mm_per_pixel)
    inv=1/np.sqrt(1+nx*nx+ny*ny)
    normal=np.stack([nx*inv,ny*inv,inv],axis=2)
    store("NormalGL",u8(normal*.5+.5))
    stats[name]=dict(tileMetres=tile_mm/1000, heightMMPercentiles=np.percentile(height,[1,50,99]).tolist(),
        roughnessPercentiles=np.percentile(rough,[1,50,99]).tolist(),
        tiltDegreesPercentiles=np.percentile(np.degrees(np.arccos(np.clip(inv,0,1))),[50,95,99]).tolist(),**extra)


def stone():
    rng=np.random.default_rng(74591)
    broad=field(rng,7,8)
    geology=field(rng,19,21)
    flank=field(rng,7,8)*6.5+field(rng,16,12)*1.7
    lam=field(rng,14,112,dy=flank)
    sheets=field(rng,40,310,dy=flank)
    cleavage=smooth(.65,1.72,lam+.38*sheets)
    fine=field(rng,238,423,dy=flank*.3)
    grains=field(rng,492,599)
    grit=field(rng,774,775)
    calcite=smooth(1.0,1.95, fine*.73+grains*.39+geology*.24)
    mica=smooth(.94,1.7, -fine*.59+grains*.48+lam*.19)
    old,new,facet=chisel(rng)
    seam=fissures(rng)
    seam *= .55+.45*smooth(-.8,.6,geology)
    rim=np.clip(blur(seam,1.9)-.45*seam,0,1)
    # 矿物充填只出现在部分片理交界。晶脉随剪切面分叉，凿痕另走独立角度。
    band=field(rng,6,18,dy=flank*1.8)+.19*field(rng,24,43,dy=flank)+.065*field(rng,98,141)
    pocket=smooth(.15,1.1,geology*.69+broad*.43)
    vein=(1-smooth(.015,.129,np.abs(band-.23)))*pocket*(.65+.35*smooth(-1,1,fine))
    soil=smooth(.45,1.9,.55*broad+.55*geology+.7*cleavage+.55*old)
    height=(.33*broad+.25*geology+.125*lam+.068*sheets+.057*fine+.039*grains+
        .015*grit-.25*cleavage-.62*old-.80*new+.24*facet-.72*seam+.13*calcite-.085*mica+.105*vein)
    variation=4.5*broad+4.5*geology+5*lam+3.1*sheets+3.0*fine+2.4*grains+10*calcite-6*mica-6.0*cleavage
    rgb=colour((83,84,82),variation,[(soil*.36,(64,55,43)),(old*.38,(63,65,63)),(new*.64,(113,116,112)),
        (seam*.70,(42,41,36)),(np.clip(2.6*rim,0,.6),(137,131,113)),(facet*.45,(133,130,118)),
        (vein*.54,(154,148,132))])
    rgb[:,:,0]+=calcite*5; rgb[:,:,2]-=calcite*4
    rough=.745+.04*broad+.025*fine+.065*soil+.065*old-.13*new-.081*mica-.085*facet-.065*vein
    cavity=np.maximum(0,blur(height,4)-height)
    ao=1-.18*np.clip(cavity/.65,0,1)-.09*seam-.05*old
    save("Stone",rgb,height,rough,0.,ao,1000,dict(chiselCutCoverage=float(np.mean(old+new>.10))))


def scratches(rng):
    lines=np.zeros((N,N),np.uint8)
    for _ in range(210):
        root=rng.uniform(0,N,2)
        theta= np.pi/2+rng.normal(0,.135) if rng.random()<.82 else rng.uniform(-np.pi,np.pi)
        vec=np.array([np.cos(theta),np.sin(theta)]); side=np.array([-vec[1],vec[0]])
        for _ in range(rng.integers(1,5)):
            p=root+side*rng.uniform(-9,9)+vec*rng.uniform(-12,12)
            length=rng.uniform(7,66)
            mid=p+vec*length*.6+side*rng.uniform(-1.2,1.2)
            poly(lines,[p,mid,p+vec*length],rng.uniform(48,218),False,1)
    return blur(lines.astype(F)/255,.40)


def bronze():
    rng=np.random.default_rng(92426)
    large=field(rng,6,8)
    middle=field(rng,26,29)
    tissue=field(rng,87,82)
    porefield=field(rng,261,248)
    warp=field(rng,6,5)*2.1
    burnish=field(rng,67,8,dx=warp)
    brush=field(rng,433,16,dx=warp)+.45*field(rng,850,21,dx=warp)
    scratches_v=scratches(rng)
    hammer=field(rng,107,114)
    crystal=field(rng,549,545)
    oxfield=.52*large+.38*middle+.25*tissue+.085*hammer+.09*porefield
    brown=smooth(-.27,.90,oxfield)
    islands=.40*large+.41*middle+.30*tissue+.21*porefield+.10*hammer+.043*crystal
    a,b=np.percentile(islands,[89.5,97.5])
    green=smooth(a,b,islands)*(.63+.37*smooth(-1.05,1.15,.65*porefield+.65*crystal))
    pits=smooth(1.0,1.83,-porefield+.42*tissue)*(.25+.75*brown)
    wear=1-.89*brown
    polish=smooth(-.2,1.15,-large+.22*burnish)
    exposed=np.clip(scratches_v*(.60+.40*polish),0,1)
    green*=1-.65*exposed
    brown*=1-.66*exposed
    height=(.065*large+.060*middle+.027*tissue+.011*porefield
        +.018*burnish+.020*brush*wear-.134*scratches_v-.129*pits+.073*green+.008*hammer)
    copper_luma=4*large+3.4*middle+1.6*tissue+1.7*burnish+1.8*brush*wear
    rgb=colour((190,149,96),copper_luma,[(brown*.88,(116,78,43)),(pits*.63,(70,54,37)),
        (green*.88,(73,100,80)),(exposed*.68,(214,170,105))])
    crust=2.4*tissue+3.8*porefield+2.6*crystal
    for i in range(3):
        rgb[:,:,i]+=green*crust + brown*(2.6*hammer+1.2*porefield)+1.2*brush*wear
    rough=.38+.032*burnish+.023*tissue+.23*brown+.22*green+.10*pits-.085*polish-.07*exposed
    metal=np.clip(.98-.46*brown-.52*green-.22*pits+.1*exposed,.035,.99)
    ao=1-.09*pits-.07*scratches_v-.035*green
    save("Bronze",rgb,height,rough,metal,ao,500,dict(verdigrisCoverageAboveHalf=float(np.mean(green>.5)),
        polishedMetalCoverage=float(np.mean(metal>.8))))


if __name__ == "__main__":
    stone(); gc.collect(); bronze()
    (Path(__file__).parent/"material-stats.json").write_text(json.dumps(stats,indent=2),encoding="utf8")
