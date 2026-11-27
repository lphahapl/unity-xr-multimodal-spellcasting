"""Bake the chapter terrain from Copernicus Tiger Leaping Gorge and authored drainage constraints."""
from pathlib import Path
import json
import numpy as np
from scipy.ndimage import map_coordinates
from PIL import Image

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]
RUNTIME = ROOT / "Assets/Art/Environment/SuspendedPath/ApprovedGorge"
SCANS = RUNTIME.parent / "Surfaces/Geology"
SOURCE = np.load(HERE.parent / "MountainTerrain/GorgeHeights.npz")
AXIS = SOURCE["axis"]
GROUND = SOURCE["before_m"] + SOURCE["hydraulic_delta_m"]
SPAN = 1280.
CENTER = np.array([35.6, 24])
def smooth(x):
    t=np.clip(x,0,1)
    return t*t*(3-2*t)
def mix(x,y,t):
    return x+(y-x)*t
def noise(x,z,size,seed):
    u=x/size; v=z/size
    i=np.floor(u); j=np.floor(v)
    def h(a,b):
        # Keep floating-point precision even on the 2k bake.
        q=np.sin((a.astype(np.float64)+seed)*127.1+b*311.7)*43758.5453
        return ((q-np.floor(q))*2-1).astype(np.float32)
    return mix(mix(h(i,j),h(i+1,j),smooth(u-i)),mix(h(i,j+1),h(i+1,j+1),smooth(u-i)),smooth(v-j))
def centerline(x):
    return -45-.275*x + 24*np.sin(x*.015-.7) - 8*np.sin(x*.031)
def rectangle(x,z,lo,hi):
    return np.hypot(np.maximum.reduce((lo[0]-x, x-hi[0], x*0)),np.maximum.reduce((lo[1]-z,z-hi[1],z*0)))
def field(x,z):
    # Warp the measured north/south trench along the player's down-valley view.
    xc=x+CENTER[0]; zc=z+CENTER[1]
    course=centerline(xc)
    side=zc-course
    along=xc*.96-.24*zc
    across=-307 + side*1.20 + 13*np.sin(along*.009)
    sampled=map_coordinates(GROUND,((along-AXIS[0])/10,(across-AXIS[0])/10),order=1,mode="nearest")
    ground=(sampled+82)*1.06-144
    # Differential weathering, creases and drainage incisions on exposed spurs.
    n70=noise(xc,zc,65,11)
    n22=noise(xc,zc,21,8)
    n7=noise(xc,zc,6.5,3)
    incision=1-np.abs(noise(xc+n70*14,zc,28,53))
    shoulder=smooth((np.abs(side)-6)/45)
    ground+=shoulder*(16*n70+9*n22+3*n7-13*incision**5)
    # Two asymmetric buttresses flank an outward-looking slot.
    def spur(cx,width,cz,length,height,tilt):
        return height*np.exp(-((xc-cx-(zc-cz)*tilt)/width)**2-((zc-cz)/length)**2)
    ground+=spur(76,18,0,40,95,-.65)+spur(124,36,54,70,148,.64)
    ground+=spur(-49,40,130,88,122,-.38)+spur(87,30,175,100,77,.48)
    ground+=spur(210,41,-161,95,98,-.6)+spur(355,72,45,116,100,.21)
    # Northern watershed dips behind the workshop's roofline; higher flanks
    # continue into the measured mountain chain in both directions.
    port=1-smooth((np.abs(xc-(29+.12*(zc-85)))-6)/35)
    ground-=port*smooth((zc-60)/55)*(1-smooth((zc-200)/270))*48
    # The overlook points down 36 degrees. A shelf at only -80 closes
    # the entire horizon from the basket. Let the outlet descend through
    # a series of three overlapping buttresses to a recessed watershed.
    view=smooth((xc-53)/80)*smooth((100-zc)/90)
    drop=82+np.clip(xc,0,1280)*.21
    ground-=view*drop
    slope_side=1-np.exp(-(abs(side)/85)**1.5)
    ribs=(24*np.cos(xc*.029+zc*.014+1.1*n70)+
          17*np.cos(xc*.050+zc*.017+.9*n22))
    ground+=view*slope_side*ribs
    riverbed=-156-np.clip(xc,-180,1280)*.235+3*np.sin(xc*.015)
    shoulder_floor=riverbed+(76+ribs*.46+7*n22)*slope_side-2
    ground=np.maximum(ground,shoulder_floor)
    # The workshop bridge looks slightly west of north. Recess this separate
    # pass below the eye line, leaving a pair of broad rock shoulders visible.
    north=smooth((zc-85)/45)*(1-smooth((zc-740)/380))
    center_north=33-.117*(zc-38)+8*np.sin(zc*.011)
    flank=np.abs(xc-center_north)
    slot=1-smooth((flank-(18+.052*np.maximum(zc-60,0)))/52)
    saddle=-105+.085*np.maximum(zc-70,0)+11*n70+4*n22
    ground=mix(ground,np.minimum(ground,saddle),north*slot)
    # Faulted headlands follow the outer bank. Each returns into the
    # surrounding catchment, with an asymmetric exposed face on the river.
    bank=-side
    for along_x,height,width,wall in [(131,70,34,30),(249,60,45,41),
                                      (385,67,52,43),(523,58,51,52)]:
        strike=xc-along_x+(bank-38)*.45
        nose=np.exp(-(strike/width)**4)
        edge=wall+6*n22+9*np.sin((xc-along_x)*.04)
        face=smooth((bank-edge)/17)
        toe=1-smooth((bank-72)/78)
        ground+=height*face*toe*nose
    halfwidth=1.45+.65*np.sin(xc*.032)**2
    width=np.abs(side)
    channel=1-smooth((width-halfwidth)/21)
    ground=mix(ground,riverbed+np.maximum(width-halfwidth,0)*.94,channel)
    # Keep the whole upper/courtyard footprint, both basket sweeps and
    # all connecting walkways clear. This masks only skyline geometry.
    d=rectangle(xc,zc,(-22,12),(92,68))
    limit=mix(-106.+9*n70+6*n22,230.,smooth(d/39))
    ground=np.minimum(ground,limit)
    # The V2.4 lower landing is at y=-80, x=[58.721,78.721], z=[16.4,40.4].
    # Continue the visible floor beyond its traversable edges into a talus toe.
    floor_dist=rectangle(xc,zc,(53,13),(85,44))
    ground=mix(-80.,ground,smooth((floor_dist-3)/11))
    water=(1-smooth((width-halfwidth)/1.25))*(1-smooth((np.abs(ground-riverbed)-.1)/1.7))
    return ground.astype(np.float32), water.astype(np.float32)

def load_rgb(path):
    return np.asarray(Image.open(path).convert("RGB")).astype(np.float32)/255
def repeated(photo,u,v):
    m,n=photo.shape[:2]
    return photo[(np.mod(v,1)*m).astype(int)%m, (np.mod(u,1)*n).astype(int)%n]
def bake():
    RUNTIME.mkdir(parents=True,exist_ok=True)
    rock=load_rgb(SCANS/'CH_Rock_BaseColor.png')
    scree=load_rgb(SCANS/'CH_Scree_BaseColor.png')
    normal=load_rgb(SCANS/'CH_Rock_Normal.png')
    mask=np.asarray(Image.open(SCANS/'CH_Rock_Mask.png').convert('RGBA'))
    image=np.empty((*rock.shape[:2],4),np.uint8)
    image[...,:2]=np.uint8(normal[...,:2]*255+.5)
    image[...,2]=np.uint8(np.clip((rock@np.array([.21,.72,.07]))/(np.mean(rock)*2),0,1)*255)
    image[...,3]=255-mask[...,3]
    Image.fromarray(image).resize((1024,1024),Image.Resampling.LANCZOS).save(RUNTIME/'StrataDetail.png')
    # Bake high-resolution normal lighting, cavity and deposition into a unique
    # terrain map. Sample dense around the chapter and coarse towards the horizon.
    N=2048
    u=(np.arange(N,dtype='float32')+.5)/N*2-1
    axis=warp(u)
    output=np.empty((N,N,4),np.uint8)
    high=np.empty((N,N),np.float32)
    for j in range(0,N,32):
        x,z=np.meshgrid(axis,axis[j:j+32])
        h,river=field(x,z)
        high[j:j+32]=h
        hx=(field(x+.5,z)[0]-field(x-.5,z)[0]); hz=(field(x,z+.5)[0]-field(x,z-.5)[0])
        slope=np.hypot(hx,hz); scale=np.sqrt(1+slope*slope)
        n=noise(x,z,26,17)+.55*noise(x,z,4,32)
        exposure=smooth((slope-.43)/1.03+.25*n)
        lichen=smooth((noise(x,z,54,43)+.16*noise(x,z,15,13)+.40)/.72)
        face=repeated(rock,(x-.37*z+2.1*n)/18,(h+.17*z+.2*x)/16)
        gravel=repeated(scree,(x+.37*z)/13,(z-.43*x)/13)
        rock_color=mix(face, np.array([.57,.50,.39]),.38)*[1.12,1.03,.91]
        sediment=mix(gravel,np.array([.32,.325,.27]),.68)
        scrub=mix(np.array([.095,.155,.105]),np.array([.23,.31,.16]),lichen[...,None])
        sediment=mix(sediment,scrub,(smooth((1.20-slope)/.86)*(.70+.27*lichen))[...,None])
        rgb=mix(sediment,rock_color,exposure[...,None])
        # Broad inclines catch the warm mountain light, with readable cool recesses.
        light=(.45 + .61*hx - .49*hz)/scale
        shelter=np.maximum(0,field(x-4,z+3)[0]-h+4*hx-3*hz)
        rgb*=(.88+.41*np.clip(light,-.75,1)-.18*smooth(shelter/2.8)+.045*n)[...,None]
        floor=1-smooth(rectangle(x+CENTER[0],z+CENTER[1],(54,14),(84,43))/6)
        rgb=mix(rgb,mix(gravel,np.array([.35,.34,.31]),.45),floor[...,None])
        ripples=smooth((noise(x,z,7,1)+.1)/.8)
        rgb=mix(rgb,np.array([.21,.33,.36])+ripples[...,None]*.14,river[...,None]*.92)
        output[j:j+32,:,:3]=np.uint8(np.clip(rgb,0,1)*255+.5)
        output[j:j+32,:,3]=np.uint8(exposure*(1-river)*255+.5)
    Image.fromarray(output[::-1]).save(RUNTIME/'GorgeSurface.png')
    # Persist high surface as compact precision heights for further sculpting.
    np.savez_compressed(HERE/'GorgeSculpt.npz',axis=axis,height_m=high.astype(np.float16))
    for intervals in (176,112,64):
        a=warp(np.linspace(-1,1,intervals+1,dtype='float32'))
        x,z=np.meshgrid(a,a);h,_=field(x,z)
        np.savez_compressed(HERE/f'Surface{intervals}.npz',axis=a,height_m=h)
def warp(t):
    return SPAN*(.20*t+.80*t**3)
if __name__=='__main__':bake()
