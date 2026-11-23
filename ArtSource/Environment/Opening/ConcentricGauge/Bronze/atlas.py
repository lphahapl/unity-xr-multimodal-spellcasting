"""Rasterize casting-space patina, engraved indices, wear and tangent detail into the shared UV set."""
from pathlib import Path

import numpy as np
from scipy.ndimage import distance_transform_edt
from PIL import Image

cache=Path(__file__).resolve().parent/".work"
out=Path(__file__).resolve().parents[5]/"Assets/Art/Environment/Opening/ConcentricGauge/Bronze"
geo=np.load(cache/"triangles.npz")
size=2048
tri=np.full((size,size),-1,np.int32)
position=np.zeros((size,size,3),np.float32)
for i,uv in enumerate(geo["uv"]):
    uv=uv*size-.5
    l=np.maximum(np.ceil(uv.min(axis=0)).astype(int),0)
    h=np.minimum(np.floor(uv.max(axis=0)).astype(int),size-1)
    if np.any(h<l):continue
    X,Y=np.meshgrid(np.arange(l[0],h[0]+1),np.arange(l[1],h[1]+1))
    a,b,c=uv
    e=b-a;f=c-a
    det=e[0]*f[1]-e[1]*f[0]
    if abs(det)<1e-8:continue
    u=((X-a[0])*f[1]-(Y-a[1])*f[0])/det
    v=(e[0]*(Y-a[1])-e[1]*(X-a[0]))/det
    valid=(u>=0)&(v>=0)&(u+v<=1)
    x,y=X[valid],Y[valid]
    tri[y,x]=i
    A,B,C=geo["xyz"][i]
    position[y,x]=A+u[valid,None]*(B-A)+v[valid,None]*(C-A)

base=np.zeros((size,size,3),np.uint8)
mask=np.zeros((size,size,4),np.uint8)
height=np.zeros((size,size),np.float32)
rng=np.random.default_rng(51217)
grid=rng.uniform(0,1,(100,100,100)).astype(np.float32)
def noise(p,f,offset=0):
    t=p*f+offset
    j=np.floor(t).astype(np.int32);s=t-j;s=s*s*(3-2*s)
    value=0
    for i in range(8):
        d=np.array([i&1,(i>>1)&1,(i>>2)&1])
        q=(j+d)%100
        value+=grid[q[:,0],q[:,1],q[:,2]]*np.prod(np.where(d,s,1-s),axis=1)
    return value
def sat(x):return np.clip(x,0,1)
def mix(a,b,f):return a*(1-f[...,None])+b*f[...,None]
def band(dist,width,aa=.00035):return sat(.5+(width-abs(dist))/aa)
def byte(a):return np.uint8(np.clip(np.rint(a),0,255))

ys,xs=np.nonzero(tri>=0)
for begin in range(0,len(ys),150000):
    s=slice(begin,begin+150000)
    y,x=ys[s],xs[s]
    t=tri[y,x]
    P=position[y,x];X,Y,Z=P.T
    N=geo["normal"][t]
    tag=geo["finish"][t];obj=geo["item"][t]
    broad=noise(P,29,13)
    mid=noise(P,100,8)
    salt=noise(P,334,33)
    fine=noise(P,930,4)
    grain=noise(P,2800,19)
    wear=sat((.50*broad+.29*mid+.14*salt+.07*fine-.49)*4.4)
    oxidation=sat((.48*broad+.31*mid+.14*salt+.07*fine-.54)*5.6)
    oxidation*=np.choose(tag,[.87,.24,.9,.95,.7,.48,1])
    tone=np.array([[110,86,62],[185,145,93],[64,55,44],[111,98,72],[81,68,52],[146,113,76],[98,75,51]])[tag]
    tone=np.where(((obj==3)|(obj==4))[:,None] & (tag==0)[:,None],np.array([87,77,63]),tone)
    color=tone*(.82+.18*mid+.16*fine+.07*grain)[:,None]
    color=mix(color,np.array([66,83,71]),oxidation)
    color=mix(color,np.array([83,97,82]),oxidation*salt*.33)
    rad=np.hypot(X,Y)
    ang=np.arctan2(Y,X)
    top=N[:,2]>.68
    outer=(obj==1)&top&(tag==3);inner=(obj==2)&top&(tag==3)
    # Chased indices: brass-filled major strokes, punched intermediate dots,
    # a pair of fine turned register lines and a compass-rose on the inner hub.
    dphi=np.abs((ang+np.pi/12)%(np.pi/6)-np.pi/12)
    major=band(rad*np.sin(dphi),.0016+.00075*sat((rad-.133)/.028))*sat((rad-.132)/.0011)*sat((.163-rad)/.0011)*outer
    rimticks=band(rad*np.sin((ang+np.pi/96)%(np.pi/48)-np.pi/96),.00038,.0004)*sat((rad-.165)/.0006)*sat((.168-rad)/.0006)*outer
    rdot=np.hypot(rad-.126,rad*dphi)
    studs=band(rdot,.00125)*outer
    turned=(band(rad-.123,.00035)+band(rad-.1647,.00033))*outer
    innerline=(band(rad-.0728,.00034)+band(rad-.043,.00035))*inner
    petal=band(rad-(.054+.0054*np.cos(8*ang)),.00052)*inner
    petal+=band(rad-(.0525+.0048*np.cos(8*ang)),.00033)*inner
    border=band(rad*np.sin(dphi),.00345)*sat((rad-.131)/.00055)*sat((.164-rad)/.00055)*outer
    stamps=np.maximum(major,np.maximum(studs,rimticks))
    incision=sat(turned+innerline+petal)
    # Fine annular inscription around the vault button and a scalloped lotus collar.
    dome=(obj==4)&top&(Z>.008)&(Z<.0196)
    lotus=band(rad-(.032+.006*np.cos(8*ang)),.00053)*dome
    lotus+=band(rad-(.034+.0063*np.cos(8*ang)),.0004)*dome
    incision=sat(incision+lotus+band(rad-.042,.00038)*dome)
    # Double string borders on the casket, with interrupted lozenge chasing.
    wall=(obj==3)&(np.abs(N[:,2])<.28)
    ribbon=band(Z-.023,.00046)*wall+band(Z-.081,.00043)*wall
    diag=((np.where(abs(N[:,0])>.7,Y,X)+.0025)%.011-.0055)
    lozenge=band(abs(diag)*.40+abs(Z-.096)-.0022,.00029)*wall
    incision=sat(incision+ribbon+lozenge)
    floor=(obj==3)&top&(tag==4)&(Z<.055)
    frame=np.maximum(abs(X)-.085,abs(Y)-.073)
    incision=sat(incision+band(frame,.00045)*floor+band(frame+.003,.00032)*floor)
    # Irregular casting pits and discontinuous hairline scoring. Direction differs
    # between lathe-turned discs and hand-finished box panels.
    pit=sat((.42*salt+.58*fine-.72)*6)
    line=band(np.sin(X*4150+Y*2110+Z*2830+2.8*mid),.06,.08)*sat((salt-.56)*4)
    turning=(.5+.5*np.sin(rad*10000+mid*3))*((obj<=2)&top)
    color*= (1-.28*pit-.13*incision+.020*(turning-.5))[:,None]
    color=mix(color,np.array([75,56,38]),incision*.74)
    color=mix(color,np.array([59,47,35]),border*.84)
    color=mix(color,np.array([219,191,149]),stamps*(.96-.09*oxidation))
    color=mix(color,np.array([191,154,106]),line*.19)
    edge=(tag==1)*(.30+.5*wear)
    color=mix(color,np.array([203,159,103]),edge*.37)
    metal=sat(.87-.53*oxidation-.22*pit-.10*incision+.09*stamps)
    smooth=sat(.55+.17*(tag==1)+.09*wear-.25*oxidation-.20*pit+.10*stamps+.04*(fine-.5))
    base[y,x]=byte(color)
    mask[y,x]=byte(np.stack([metal,np.ones_like(metal),np.zeros_like(metal),smooth],axis=1)*255)
    height[y,x]=(.5*fine+.12*grain+.17*salt-.71*pit-.30*incision-.18*line-.32*stamps+.06*turning)

# Dilate chart edges before deriving normals and generate proper gutters for mipmaps.
nearest=distance_transform_edt(tri<0,return_distances=False,return_indices=True)
ny,nx=nearest
H=height[ny,nx]
dy,dx=np.gradient(H)
normal=np.stack([-dx*.27,-dy*.27,np.ones_like(H)],axis=2)
normal/=np.linalg.norm(normal,axis=2,keepdims=True)
for suffix,data in [("BaseColor",base[ny,nx]),("MetalSmooth",mask[ny,nx]),("Normal",byte((normal[ny,nx]*.5+.5)*255))]:
    Image.fromarray(data[::-1]).save(out/("CG_A1_"+suffix+".png"),compress_level=6)
print("Shared casting atlas coverage",round(len(ys)/size**2,3))
