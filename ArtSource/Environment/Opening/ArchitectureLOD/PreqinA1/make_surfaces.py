"""Rebuild the shared URP trim sheet from the project's CC0 Workshop scans.
Band coordinates increase upward, as in Blender UV0. U repeats without clamping.
"""
from pathlib import Path
import numpy as np
import cv2
from PIL import Image

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[4]
SCAN = ROOT / 'Assets/Art/Environment/SuspendedPath/Surfaces/Workshop'
DST = ROOT / 'Assets/Art/Environment/Opening/ArchitectureLOD/Textures'
N = 2048
BANDS = dict(roof=(0,720), wood=(720,1120), stone=(1120,1480), plaster=(1480,1616), nose=(1616,1712),
             clay=(1712,1832), end=(1832,1952), bronze=(1952,1992), rule=(1992,2020), ink=(2020,2048))
LANES = 48
COURSES = 16
PERIOD = LANES * .34
COURSE = .315

def periodic_field(u,v,cols,rows,seed):
    # Sample a wrapped random lattice. The low-frequency field survives mip reduction.
    lattice=np.random.default_rng(seed).normal(0,1,(rows,cols)).astype('f')
    return cv2.remap(lattice,(u*cols).astype('f'),(v*rows).astype('f'),cv2.INTER_CUBIC,borderMode=cv2.BORDER_WRAP)

def firing(cols,rows,seed):
    col,row=np.meshgrid((np.arange(cols)+.5)/cols,(np.arange(rows)+.5)/rows)
    batch=periodic_field(col,row,max(3,cols//5),max(2,rows//4),seed)
    individual=np.random.default_rng(seed+1).normal(size=(rows,cols))
    return np.clip(.70*batch+.49*individual,-1.9,1.9)

def make():
    atlas = {name:np.empty((N,N,4),np.uint8) for name in ('BaseColor','Normal','Mask')}
    for key,(y0,y1) in BANDS.items():
        h = y1-y0-8
        yy,xx = np.mgrid[0:h,0:N]
        u = (xx+.5)/N
        v = (yy+.5)/h
        src = dict(roof='Tile',clay='Tile',stone='Ashlar',wood='Timber',plaster='Plaster',end='EndGrain').get(key,'Tile')
        color = np.asarray(Image.open(SCAN/(src+'_BaseSmooth.png')).convert('RGBA'))
        norm = np.asarray(Image.open(SCAN/(src+'_NormalGL.png')).convert('RGB'))
        sy,sx=color.shape[:2]
        su,sv=u.copy(),v.copy()
        if key in ('roof','stone'):
            row = np.floor(v*(COURSES if key=='roof' else 6))
            col = np.floor(u*(LANES if key=='roof' else 5)+(row%2)*(.0 if key=='roof' else .5))
            seed = (np.sin((col%(LANES if key=='roof' else 5)+.5)*37.1+row*83.4)*137.73)%1
            su += seed*.63
            sv += seed*.43
        if key=='plaster':
            # A real plaster scan supplies the fine base. Crop it at a physical aspect
            # close to that of the broad elevation rather than stretching paper-like fibres.
            su=u;sv=v*.53
        mapx,mapy = ((su%1)*sx).astype('f'),((1-sv%1)*sy).astype('f')
        colormap = cv2.remap(color,mapx,mapy,cv2.INTER_LINEAR,borderMode=cv2.BORDER_WRAP).astype('f')
        normals = cv2.remap(norm,mapx,mapy,cv2.INTER_LINEAR,borderMode=cv2.BORDER_WRAP).astype('f')/127.5-1
        rgb = colormap[:,:,:3]
        smooth = colormap[:,:,3]
        metal = np.zeros((h,N))
        if key=='roof':
            phase=(u*LANES)%1-.5
            arc=np.sqrt(np.maximum(0,1-(phase/.205)**2))
            pan=(1-np.cos(2*np.pi*(phase+.5)))*.5
            lane=np.floor(u*LANES).astype(int)
            offsets=np.random.default_rng(282).uniform(-.11,.11,LANES)[lane]
            course=v*COURSES+.033*np.sin(u*LANES*2*np.pi)+offsets
            row=np.floor(course).astype(int)%COURSES;lap=course%1
            seam=np.exp(-(np.minimum(lap,1-lap)/.045)**2)
            height=.045*arc+.009*pan+.009*(1-lap)-.006*seam
            dx=(np.roll(height,-1,1)-np.roll(height,1,1))*N/(2*PERIOD)
            dy=(np.roll(height,-1,0)-np.roll(height,1,0))*h/(2*COURSE*COURSES)
            normals[:,:,0]=np.clip(-dx,-1.4,1.4)+normals[:,:,0]*.25
            normals[:,:,1]=np.clip(-dy,-.85,.85)+normals[:,:,1]*.25
            normals[:,:,2]=1
            fired=firing(LANES,COURSES,3421)[row,lane]
            stain=periodic_field(u,v,57,23,174)
            rgb=rgb*.73+[39,33,25]+(fired[:,:,None]*[14.5,12.2,9.0]
                +stain[:,:,None]*2.6+(arc*.09-seam*.22-(1-arc)*.03)[:,:,None]*100)
            smooth=np.clip(smooth*.57+22+fired*5+stain*2.7,38,93)
        elif key=='stone':
            a=(u*5+(row%2)*.5)%1
            b=(v*6)%1
            joint=np.minimum(np.minimum(a,1-a)*.50,np.minimum(b,1-b)*.25)
            factor=np.clip((joint-.004)/.012,0,1)
            rgb=(rgb*.75+[26,24,20]+(seed[:,:,None]-.5)*19)*factor[:,:,None]+np.array([96,89,77])*(1-factor[:,:,None])
            slope=factor*.008
            normals[:,:,0]-=(np.roll(slope,-1,1)-np.roll(slope,1,1))*N/5.12
            normals[:,:,1]-=(np.roll(slope,-1,0)-np.roll(slope,1,0))*h/3
        elif key=='plaster':
            # Compacted ochre earth, lime and exposed sand. The wider bands have
            # broken edges; the rounded inclusions and pinholes remain at hand scale.
            mottling=periodic_field(u,v,9,6,421)
            trowel=periodic_field(u,v,24,16,196)
            sand=periodic_field(u,v,244,119,35)
            quartz=np.clip((sand-.5)*.8,0,1.7)
            voids=np.clip((-sand-.68)*.85,0,1.5)
            lift=v*5.3+.08*periodic_field(u,v,13,5,658)+.065*mottling
            bedding=np.exp(-(np.minimum(lift%1,1-lift%1)/.070)**2)*np.clip(.55+trowel*.25,0,1)
            rgb=rgb*.91+[25,17,8]+mottling[:,:,None]*[5,4,3]+trowel[:,:,None]*2.5
            rgb+=quartz[:,:,None]*[15,13,10]-voids[:,:,None]*[20,17,14]-bedding[:,:,None]*[8,7,5]
            relief=.0019*quartz-.0028*voids-.0018*bedding+.0008*trowel
            normals[:,:,0]+=(np.roll(relief,1,1)-np.roll(relief,-1,1))*N/5.12
            normals[:,:,1]+=(np.roll(relief,1,0)-np.roll(relief,-1,0))*h/2.70
            smooth=np.clip(smooth*.78+2+quartz*4-voids*7+mottling*2,16,46)
        elif key=='clay':
            # Independent firing and weathering for 24 courses in each of eight cover
            # strips. The UV chooses a different strip / start tile per physical run.
            lap=(u*24)%1;ring=np.exp(-((lap-.035)/.026)**2)
            piece=np.floor(u*24).astype(int);strip=np.floor(v*8).astype(int)
            fired=firing(24,8,947)[strip,piece]
            stain=periodic_field(u,v,53,7,991)
            rgb=rgb*.79+[44,35,27]+fired[:,:,None]*[13.5,11,8.5]+stain[:,:,None]*2.5
            rgb+=ring[:,:,None]*21-(lap<.023)[:,:,None]*18
            normals[:,:,0]+=(np.roll(ring,1,1)-np.roll(ring,-1,1))*.85
            smooth=np.clip(smooth*.81+5+fired*4.7+stain*2,37,96)
        elif key=='nose':
            # Low-relief concentric potter's tool marks on the half-disc eave terminal.
            px=((u*16)%1-.5)/.46
            py=(v-.10)/.80
            radius=np.sqrt(px*px+py*py)
            ring=np.exp(-((radius-.80)/.045)**2)+.55*np.exp(-((radius-.58)/.035)**2)
            rgb=rgb*.72+[43,34,24]+ring[:,:,None]*25
            normals[:,:,0]+=(np.roll(ring,1,1)-np.roll(ring,-1,1))*.95
            normals[:,:,1]+=(np.roll(ring,1,0)-np.roll(ring,-1,0))*.95
        elif key in ('bronze','rule','ink'):
            rgb=rgb*.20+dict(bronze=[91,73,46],rule=[161,150,121],ink=[26,24,21])[key]
            metal[:]=142 if key=='bronze' else 0
        normals/=np.maximum(.001,np.linalg.norm(normals,axis=2,keepdims=True))
        for role,pixels in dict(BaseColor=np.dstack((rgb,np.full((h,N),255))),Normal=np.dstack(((normals*.5+.5)*255,np.full((h,N),255))),Mask=np.stack((metal,np.full((h,N),255),np.zeros((h,N)),smooth),axis=2)).items():
            atlas[role][y0:y1] = np.pad(np.clip(pixels,0,255).astype('uint8'),((4,4),(0,0),(0,0)),mode='edge')
    for role,pixels in atlas.items():
        file=DST/('Architecture_'+role+'.png')
        Image.fromarray(pixels[::-1]).save(file)
        meta=Path(str(file)+'.meta')
        meta.write_bytes(meta.read_bytes().replace(b'wrapU: 1',b'wrapU: 0').replace(b'maxTextureSize: 1024',b'maxTextureSize: 2048'))
if __name__=='__main__':
    make()
