"""Run with Python 3, numpy, opencv-python and Pillow. Seeded colour + tangent-normal trim atlas."""
import math
import cv2
import numpy as np
from PIL import Image
from layout import CHARTS, EXPORT, SIZE

def smooth_noise(rng, h, w, cell):
    small = rng.normal(size=(max(2, round(h/cell)+1), max(2, round(w/cell)+1))).astype('f')
    return cv2.resize(small, (w,h), interpolation=cv2.INTER_CUBIC)

def relief(key, w, h, sx, sy):
    cut = np.zeros((h,w), np.uint8)
    def stroke(points, mm, closed=False):
        pts = np.round([(x/sx*(w-1), y/sy*(h-1)) for x,y in points]).astype(np.int32)
        cv2.polylines(cut,[pts],closed,255,max(1,round(w/sx*mm*.001)),cv2.LINE_AA)
    def fret(x,y,step,mirror=1,up=1):
        # An open, returning thunder-scroll with a terminal hook.
        path=[(0,3.1),(1.35,3.1),(1.35,1.75),(3.15,1.75),(3.15,.18),(1.68,.18),(1.68,.85),(2.55,.85)]
        stroke([(x+mirror*a*step,y+up*b*step) for a,b in path],3.8)
    if key in ('tabletop','stele','socle'):
        for side in (-1,1):
            x = .028 if side==1 else sx-.028
            if key == 'tabletop':
                for top in (1,-1):
                    y=.027 if top==1 else sy-.027
                    fret(x,y,.033,side,top)
            else:
                fret(x, .063 if key=='stele' else .025,.043 if key=='stele' else .026,side)
        if key=='tabletop':
            for a in (.021,.032):
                k=.035
                stroke([(a+k,a),(sx-a-k,a),(sx-a,a+k),(sx-a,sy-a-k),(sx-a-k,sy-a),(a+k,sy-a),(a,sy-a-k),(a,a+k)],2,True)
        if key=='stele':
            # Line colour across a physically sculpted groove. The square is a blind socket.
            cx,cy=sx/2,1.65-.95
            radial=np.linspace(0,2*np.pi,513)
            stroke([(cx+.273*np.cos(a),cy+.273*np.sin(a)) for a in radial],4)
            for sign in (-1,1):
                stroke([(cx+sign*.062,cy),(cx+sign*.27,cy)],4)
            stroke([(cx+i*.062,cy+j*.062) for i,j in ((-1,-1),(1,-1),(1,1),(-1,1))],4,True)
    if key=='apron':
        # Mirrored ruyi scroll: a rolled head, a smaller counter-scroll and tapered trailing tendril.
        for sign in (-1,1):
            def scroll(segments,mm):
                pts=[]
                for a,b,c,d in segments:
                    a,b,c,d=map(np.array,(a,b,c,d))
                    for t in np.linspace(0,1,20):
                        q=(1-t)**3*a+3*t*(1-t)**2*b+3*t*t*(1-t)*c+t**3*d
                        pts.append((sx*.5+sign*q[0], q[1]))
                stroke(pts,mm)
            scroll([((.084,.081),(.102,.061),(.074,.047),(.057,.058)),
                    ((.057,.058),(.035,.07),(.052,.099),(.076,.097)),
                    ((.076,.097),(.118,.10),(.131,.051),(.098,.04)),
                    ((.098,.04),(.052,.015),(.022,.044),(.018,.073)),
                    ((.018,.073),(.013,.104),(.032,.123),(.063,.126)),
                    ((.063,.126),(.129,.136),(.15,.078),(.174,.078)),
                    ((.174,.078),(.193,.082),(.19,.099),(.213,.101)),
                    ((.213,.101),(.237,.10),(.236,.081),(.249,.078)),
                    ((.249,.078),(.281,.074),(.313,.112),(.346,.076))],4.8)
            scroll([((.138,.059),(.162,.055),(.164,.031),(.189,.039)),
                    ((.189,.039),(.203,.047),(.197,.064),(.218,.065)),
                    ((.218,.065),(.253,.069),(.252,.052),(.278,.055)),
                    ((.278,.055),(.301,.055),(.314,.069),(.34,.055))],3.2)
    if key in ('post','fascia'):
        inset=.014 if key=='post' else .021
        for a in (inset,inset+.006):
            k=.016
            stroke([(a+k,a),(sx-a-k,a),(sx-a,a+k),(sx-a,sy-a-k),(sx-a-k,sy-a),(a+k,sy-a),(a,sy-a-k),(a,a+k)],1.9,True)
    return cut.astype('f')/255

def stone(key, rect, seed):
    _,_,w,h,sx,sy=rect
    rng=np.random.default_rng(seed)
    def noise(cell): return smooth_noise(rng,h,w,cell)
    strata,cloud,grain,sand,quartz=(noise(n) for n in (184,47,10,2.8,6))
    fine=rng.normal(0,1,(h,w)).astype('f')
    yy,xx=np.indices((h,w),dtype='f')
    edge=np.minimum.reduce((xx,w-1-xx,yy,h-1-yy))
    # Stochastic thin mineral seams, granular inclusions and small spalls.
    warp=noise(85)*20 + noise(24)*4
    veins=np.maximum(0,1-np.abs(np.sin(.010*xx+.018*yy+.019*warp+strata*.8))*13)
    veins*=np.clip(.38+cloud*.48+grain*.16,0,1)
    crystal=np.clip((sand*.73+quartz*.45+fine*.28-1.08)*2.4,0,1)
    pits=np.clip((-sand*.72-grain*.4-fine*.32-1.2)*2,0,1)
    iron=np.clip((noise(19)*.68+cloud*.5+fine*.2-.83)*1.32,0,1)
    worn=np.clip((13+cloud*7+grain*5-edge)/10,0,1) if key not in ('field','arris') else 0
    if key=='arris': worn=np.clip(.42+cloud*.38+grain*.22+fine*.11,0,1)
    cut=relief(key,w,h,sx,sy)
    cut=cv2.GaussianBlur(cut,(0,0),.62)
    lip=np.clip(cv2.dilate(cut,np.ones((5,5),np.uint8))-cut,0,1)
    cracks=np.zeros((h,w),np.uint8)
    for _ in range(round(w*h/91000)):
        side=int(rng.integers(4))
        x,y=[(rng.uniform(0,w),0),(w,rng.uniform(0,h)),(rng.uniform(0,w),h),(0,rng.uniform(0,h))][side]
        angle=[math.pi/2, math.pi, -math.pi/2,0][side]+rng.uniform(-.8,.8)
        pts=[(int(x),int(y))]
        for j in range(int(rng.integers(5,16))):
            a=angle+rng.normal(0,.54)
            step=rng.uniform(4,12)
            x+=step*math.cos(a); y+=step*math.sin(a)
            pts.append((int(x),int(y)))
            if j==5:
                cv2.line(cracks,pts[-1],(int(x+18*math.cos(a+1)),int(y+18*math.sin(a+1))),150,1,cv2.LINE_AA)
        cv2.polylines(cracks,[np.array(pts)],False,235,1,cv2.LINE_AA)
    crack=cracks.astype('f')/255
    surface=strata*4.8+cloud*5.5+grain*4.2+sand*3.8+fine*3+crystal*23-pits*19+veins*19-crack*44
    value=89+surface
    value += worn*(22+grain*4) + lip*18-cut*22
    if key=='stele':
        # Widened incisions. Exposed pale arrises break up locally; recessed floors hold patina.
        px=(xx/(w-1)-.5)*sx
        pz=1.65-yy/(h-1)*sy-.95
        radial=np.hypot(px,pz)
        square=np.maximum(abs(px),abs(pz))
        band=np.hypot(np.maximum(np.maximum(.064-np.abs(px),np.abs(px)-.273),0),pz)
        dist=np.minimum.reduce((np.abs(radial-.273),np.abs(square-.064),band))
        def soften(t): return np.clip(t,0,1)
        edge_wobble=.0009*grain+.00035*sand
        floor=soften((.0071+edge_wobble-dist)/.0023)
        edge=soften((.014+edge_wobble-dist)/.0019)*(1-soften((.0087+edge_wobble-dist)/.0023))
        value=79+surface*.57+worn*17+lip*8-cut*9
        value+=edge*(70+grain*5+cloud*3)-floor*40-soften((.057-square)/.003)*16
        iron*=.20
    tide=np.clip((yy/h-.81)*4+cloud*.22+grain*.09,0,1) if key=='socle' else 0
    rgb=np.dstack((value+2+iron*15+worn*6+tide*3,value+iron*8+worn*3+tide*2,value+1-iron*6-worn*5-tide*12))
    height=grain*.48+sand*.22+crystal*.6-pits*.78+veins*.65-crack*1.15+lip*.36-cut*2.8
    if key=='stele': height=height*.55 + edge*.40 - floor*1.55
    dx=cv2.Sobel(height,cv2.CV_32F,1,0,ksize=3)*.15
    dy=cv2.Sobel(height,cv2.CV_32F,0,1,ksize=3)*.15
    norm=np.sqrt(1+dx*dx+dy*dy)
    normals=np.dstack((-dx/norm,dy/norm,1/norm))*127.5+127.5
    return [np.uint8(np.clip(a,0,255)) for a in (rgb,normals)]

if __name__=='__main__':
    EXPORT.mkdir(parents=True,exist_ok=True)
    atlases=[np.zeros((SIZE,SIZE,3),np.uint8) for _ in range(2)]
    for i,(key,rect) in enumerate(CHARTS.items()):
        x,y,w,h,*_=rect
        for atlas,panel in zip(atlases,stone(key,rect,3501+i)):
            atlas[y-8:y+h+8,x-8:x+w+8]=cv2.copyMakeBorder(panel,8,8,8,8,cv2.BORDER_REPLICATE)
    for atlas,suffix in zip(atlases,('BaseColor','Normal')):
        Image.fromarray(atlas).save(EXPORT/f'Stone_{suffix}.png')

